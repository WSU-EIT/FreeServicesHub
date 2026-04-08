using System.Collections.Concurrent;
using System.Text.Json;
using FreeTools.Core;
using Microsoft.Playwright;

namespace FreeTools.BrowserSnapshot;

internal class Program
{
    private static readonly object _consoleLock = new();
    
    // P1: Configurable thresholds
    private const int SuspiciousFileSizeThreshold = 10 * 1024; // 10KB
    private const int RetryExtraDelayMs = 3000;

    // Default login credentials
    private const string DefaultUsername = "admin";
    private const string DefaultPassword = "admin";

    private static async Task<int> Main(string[] args)
    {
        // Robustness: Optional startup delay (default 5 seconds for server warmup)
        var delayEnv = Environment.GetEnvironmentVariable("START_DELAY_MS");
        var startupDelay = int.TryParse(delayEnv, out var delayMs) && delayMs > 0 ? delayMs : 5000;
        
        Console.WriteLine($"Waiting {startupDelay}ms for server to be ready...");
        await Task.Delay(startupDelay);

        var baseUrl = CliArgs.GetEnvOrArg("BASE_URL", args, 0, "https://localhost:5001");
        var csvPath = CliArgs.GetEnvOrArg("CSV_PATH", args, 1, "pages.csv");
        var outputDir = CliArgs.GetEnvOrArg("OUTPUT_DIR", args, 2, "page-snapshots");
        var browserEnv = Environment.GetEnvironmentVariable("SCREENSHOT_BROWSER");
        var viewportEnv = Environment.GetEnvironmentVariable("SCREENSHOT_VIEWPORT");
        
        // Login credentials (configurable via environment)
        var loginUsername = Environment.GetEnvironmentVariable("LOGIN_USERNAME") ?? DefaultUsername;
        var loginPassword = Environment.GetEnvironmentVariable("LOGIN_PASSWORD") ?? DefaultPassword;

        // Tenant code for login (FreeCRM apps require a tenant code in the URL to authenticate)
        var tenantCode = Environment.GetEnvironmentVariable("TENANT_CODE") ?? "tenant1";

        // P1: Configurable settle delay (default 3000ms, up from 1500ms)
        var settleDelayEnv = Environment.GetEnvironmentVariable("PAGE_SETTLE_DELAY_MS");
        var settleDelay = int.TryParse(settleDelayEnv, out var sd) && sd > 0 ? sd : 3000;
        
        // Default to 10 threads for parallel processing
        var maxThreads = Math.Max(1, CliArgs.GetEnvOrArgInt("MAX_THREADS", args, 3, 10));

        var browserName = string.IsNullOrWhiteSpace(browserEnv)
            ? "chromium"
            : browserEnv.Trim().ToLowerInvariant();

        ConsoleOutput.PrintBanner("BrowserSnapshot (FreeTools)", "3.0");
        ConsoleOutput.PrintConfig("BASE_URL", baseUrl);
        ConsoleOutput.PrintConfig("CSV_PATH", csvPath);
        ConsoleOutput.PrintConfig("OUTPUT_DIR", outputDir);
        ConsoleOutput.PrintConfig("BROWSER", browserName);
        ConsoleOutput.PrintConfig("VIEWPORT", string.IsNullOrWhiteSpace(viewportEnv) ? "(default)" : viewportEnv);
        ConsoleOutput.PrintConfig("MAX_THREADS", maxThreads.ToString());
        ConsoleOutput.PrintConfig("SETTLE_DELAY", $"{settleDelay}ms");
        ConsoleOutput.PrintConfig("LOGIN_USER", loginUsername);
        ConsoleOutput.PrintConfig("TENANT_CODE", tenantCode);
        ConsoleOutput.PrintDivider();

        if (!File.Exists(csvPath))
        {
            Console.Error.WriteLine($"CSV file not found: {csvPath}");
            return 1;
        }

        // Parse routes with auth info, substituting {TenantCode} with the configured value
        var (routeInfos, skippedRoutes) = await ParseRoutesWithAuthAsync(csvPath, tenantCode);

        foreach (var skipped in skippedRoutes)
        {
            Console.WriteLine($"  [SKIP] {skipped} - has route parameters");
        }

        if (skippedRoutes.Count > 0)
        {
            Console.WriteLine($"Skipped {skippedRoutes.Count} routes with parameters.");
        }

        if (routeInfos.Count == 0)
        {
            Console.WriteLine("No screenshottable routes found in CSV.");
            return 0;
        }

        var authCount = routeInfos.Count(r => r.RequiresAuth);
        var publicCount = routeInfos.Count - authCount;
        Console.WriteLine($"Found {routeInfos.Count} routes ({publicCount} public, {authCount} auth) with {maxThreads} parallel workers.");

        Directory.CreateDirectory(outputDir);

        (int? viewportWidth, int? viewportHeight) = ParseViewport(viewportEnv);

        try
        {
            Console.WriteLine("[1/5] Ensuring Playwright browsers are installed...");
            await EnsurePlaywrightBrowsersInstalledAsync(browserName);

            Console.WriteLine("[2/5] Initializing Playwright...");
            using var playwright = await Playwright.CreateAsync();

            var browserType = browserName switch
            {
                "firefox" => playwright.Firefox,
                "webkit" => playwright.Webkit,
                _ => playwright.Chromium
            };

            Console.WriteLine($"[3/5] Launching {browserName} (headless)...");
            await using var browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            // =================================================================
            // PASS 1: Capture all pages UNAUTHENTICATED ? default.png
            // =================================================================
            Console.WriteLine("[4/5] Pass 1: Capturing all pages (unauthenticated)...");
            Console.WriteLine();

            var totalCount = routeInfos.Count;
            var pass1Results = new ConcurrentDictionary<int, ScreenshotResult>();
            var nextIndexToWrite = 0;
            var writeLock = new object();

            var semaphore = new SemaphoreSlim(maxThreads);

            var pass1Tasks = routeInfos.Select((routeInfo, index) => Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var result = await CapturePublicScreenshotAsync(
                        browser, routeInfo, index, totalCount, baseUrl, outputDir,
                        viewportWidth, viewportHeight, settleDelay);

                    pass1Results[index] = result;
                    WriteResultsInOrder(pass1Results, ref nextIndexToWrite, writeLock, writeAction: WritePublicResult);
                }
                finally
                {
                    semaphore.Release();
                }
            })).ToArray();

            await Task.WhenAll(pass1Tasks);
            WriteResultsInOrder(pass1Results, ref nextIndexToWrite, writeLock, flush: true, writeAction: WritePublicResult);

            var pass1Success = pass1Results.Values.Count(r => r.IsSuccess);
            var pass1Suspicious = pass1Results.Values.Count(r => r.IsSuspiciouslySmall);
            Console.WriteLine();
            Console.WriteLine($"  Pass 1 complete: {pass1Success}/{totalCount} successful, {pass1Suspicious} suspicious");

            // =================================================================
            // LOGIN: Authenticate once, save session for reuse
            // =================================================================
            Console.WriteLine();
            Console.WriteLine("[5/5] Pass 2: Logging in and capturing all pages (authenticated)...");

            var storageState = await PerformLoginAsync(
                browser, baseUrl, settleDelay,
                viewportWidth, viewportHeight,
                loginUsername, loginPassword, outputDir,
                tenantCode);

            if (storageState == null)
            {
                Console.WriteLine("  ?? Login failed — skipping authenticated pass.");
                Console.WriteLine("     Authenticated screenshots will not be available.");
            }
            else
            {
                // =================================================================
                // PASS 2: Capture all pages AUTHENTICATED ? logged-in.png
                // =================================================================
                Console.WriteLine();

                var pass2Results = new ConcurrentDictionary<int, ScreenshotResult>();
                var nextIndex2 = 0;
                var writeLock2 = new object();

                var pass2Tasks = routeInfos.Select((routeInfo, index) => Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var result = await CaptureAuthenticatedScreenshotAsync(
                            browser, routeInfo, index, totalCount, baseUrl, outputDir,
                            viewportWidth, viewportHeight, settleDelay, storageState);

                        pass2Results[index] = result;
                        WriteResultsInOrder(pass2Results, ref nextIndex2, writeLock2, writeAction: WriteAuthenticatedResult);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                })).ToArray();

                await Task.WhenAll(pass2Tasks);
                WriteResultsInOrder(pass2Results, ref nextIndex2, writeLock2, flush: true, writeAction: WriteAuthenticatedResult);

                var pass2Success = pass2Results.Values.Count(r => r.IsSuccess);
                var pass2Suspicious = pass2Results.Values.Count(r => r.IsSuspiciouslySmall);
                Console.WriteLine();
                Console.WriteLine($"  Pass 2 complete: {pass2Success}/{totalCount} successful, {pass2Suspicious} suspicious");

                // Merge pass 2 data into pass 1 results for metadata
                foreach (var kvp in pass2Results)
                {
                    if (pass1Results.TryGetValue(kvp.Key, out var pass1Result))
                    {
                        pass1Result.LoggedInScreenshotPath = kvp.Value.ScreenshotPath;
                        pass1Result.LoggedInFileSize = kvp.Value.FileSize;
                        pass1Result.LoggedInIsSuspiciouslySmall = kvp.Value.IsSuspiciouslySmall;
                        pass1Result.LoggedInStatusCode = kvp.Value.StatusCode;
                        pass1Result.LoginRedirectPath = kvp.Value.LoginRedirectPath;
                    }
                }
            }

            // Write metadata for all routes (includes both pass results)
            foreach (var kvp in pass1Results.OrderBy(k => k.Key))
            {
                var routeInfo = routeInfos[kvp.Key];
                await WriteMetadataAsync(outputDir, routeInfo.Route, kvp.Value);
            }

            // Summary
            Console.WriteLine();
            ConsoleOutput.PrintDivider("Summary");
            Console.WriteLine($"Two-pass screenshot capture complete for {totalCount} routes:");
            Console.WriteLine($"  Pass 1 (public):  {pass1Success}/{totalCount} successful");
            if (storageState != null)
            {
                var p2s = pass1Results.Values.Count(r => r.LoggedInScreenshotPath != null);
                Console.WriteLine($"  Pass 2 (auth):    {p2s}/{totalCount} successful");
            }
            else
            {
                Console.WriteLine($"  Pass 2 (auth):    SKIPPED (login failed)");
            }

            Console.WriteLine();
            Console.WriteLine("Completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Fatal error in BrowserSnapshot:");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    /// <summary>
    /// Parse routes from CSV including the RequiresAuth flag.
    /// Substitutes {TenantCode} with the actual tenant code before checking for parameters.
    /// Deduplicates so tenant-code routes (e.g. /tenant1/About) replace bare routes (e.g. /About).
    /// CSV format: FilePath,Route,RequiresAuth,Project
    /// </summary>
    private static async Task<(List<RouteInfo> routes, List<string> skipped)> ParseRoutesWithAuthAsync(string csvPath, string tenantCode)
    {
        var routeMap = new Dictionary<string, RouteInfo>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<string>();

        var lines = await File.ReadAllLinesAsync(csvPath);

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(',');
            if (parts.Length < 3)
                continue;

            var rawRoute = parts[1].Trim('"').Trim();
            if (string.IsNullOrWhiteSpace(rawRoute))
                continue;

            var requiresAuth = parts.Length >= 3 && 
                parts[2].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

            // Substitute {TenantCode} with the actual tenant code value
            var route = rawRoute;
            bool hadTenantCode = false;
            if (!string.IsNullOrWhiteSpace(tenantCode) && route.Contains("{TenantCode}", StringComparison.OrdinalIgnoreCase))
            {
                route = route.Replace("{TenantCode}", tenantCode, StringComparison.OrdinalIgnoreCase);
                hadTenantCode = true;
            }

            // Skip routes that still have other parameters (e.g. {itemid}, {userid})
            if (RouteParser.HasParameter(route))
            {
                skipped.Add(rawRoute);
                continue;
            }

            // Deduplicate: tenant-code routes take priority over bare routes.
            // e.g. /tenant1/About wins over /About (same page, but tenant-code version
            // ensures the app resolves the correct tenant).
            if (hadTenantCode)
            {
                // This is a tenant-code route — always add/overwrite
                routeMap[route] = new RouteInfo { Route = route, RequiresAuth = requiresAuth };
            }
            else if (!routeMap.ContainsKey(route))
            {
                // Bare route — only add if no tenant-code version exists yet.
                // Also check if a tenant-prefixed version is already in the map.
                var tenantPrefixed = $"/{tenantCode}{route}";
                if (!routeMap.ContainsKey(tenantPrefixed))
                {
                    routeMap[route] = new RouteInfo { Route = route, RequiresAuth = requiresAuth };
                }
            }
        }

        // Second pass: remove bare routes that now have a tenant-code equivalent
        var bareToRemove = new List<string>();
        foreach (var kvp in routeMap)
        {
            var r = kvp.Key;
            if (!string.IsNullOrWhiteSpace(tenantCode) && !r.StartsWith($"/{tenantCode}", StringComparison.OrdinalIgnoreCase))
            {
                var tenantVersion = $"/{tenantCode}{r}";
                if (routeMap.ContainsKey(tenantVersion))
                    bareToRemove.Add(r);
            }
        }
        foreach (var key in bareToRemove)
            routeMap.Remove(key);

        var routes = routeMap.Values.OrderBy(r => r.Route).ToList();
        return (routes, skipped);
    }

    /// <summary>
    /// Pass 1: Capture a single page unauthenticated ? default.png
    /// </summary>
    private static async Task<ScreenshotResult> CapturePublicScreenshotAsync(
        IBrowser browser,
        RouteInfo routeInfo,
        int index,
        int totalCount,
        string baseUrl,
        string outputDir,
        int? viewportWidth,
        int? viewportHeight,
        int settleDelay)
    {
        var result = new ScreenshotResult
        {
            Index = index,
            Route = routeInfo.Route,
            RequiresAuth = routeInfo.RequiresAuth,
            Number = index + 1,
            TotalCount = totalCount,
            CapturedAt = DateTime.UtcNow
        };

        var contextOptions = new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        };
        if (viewportWidth.HasValue && viewportHeight.HasValue)
        {
            contextOptions.ViewportSize = new ViewportSize
            {
                Width = viewportWidth.Value,
                Height = viewportHeight.Value
            };
        }

        var context = await browser.NewContextAsync(contextOptions);
        try
        {
            var page = await context.NewPageAsync();
            var url = RouteParser.BuildUrl(baseUrl, routeInfo.Route);
            result.Url = url;

            List<string> consoleErrors = [];
            page.Console += (_, msg) =>
            {
                if (msg.Type == "error")
                    consoleErrors.Add(msg.Text);
            };

            try
            {
                var response = await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 60000
                });

                result.StatusCode = response?.Status ?? 0;
                result.IsSuccess = result.StatusCode >= 200 && result.StatusCode < 400;
                result.IsHttpError = result.StatusCode >= 400;

                await page.WaitForTimeoutAsync(settleDelay);

                // Simple screenshot — no auth flow, just capture what we see
                var screenshotPath = PathSanitizer.GetOutputFilePath(outputDir, routeInfo.Route, "default.png");
                PathSanitizer.EnsureDirectoryExists(screenshotPath);
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });

                var fi = new FileInfo(screenshotPath);

                // Retry if suspiciously small
                if (fi.Length < SuspiciousFileSizeThreshold)
                {
                    result.RetryAttempted = true;
                    await page.WaitForTimeoutAsync(RetryExtraDelayMs);
                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
                    fi = new FileInfo(screenshotPath);
                }

                result.ScreenshotPath = screenshotPath;
                result.FileSize = fi.Length;
                result.IsSuspiciouslySmall = fi.Length < SuspiciousFileSizeThreshold;
                result.ConsoleErrors = consoleErrors;
            }
            catch (TimeoutException)
            {
                result.IsError = true;
                result.ErrorMessage = "Navigation timed out";
                result.ConsoleErrors = consoleErrors;
            }
            catch (Exception ex)
            {
                result.IsError = true;
                result.ErrorMessage = ex.Message;
                result.ConsoleErrors = consoleErrors;
            }
        }
        finally
        {
            await context.CloseAsync();
        }

        return result;
    }

    /// <summary>
    /// Perform login once and return the browser storage state for reuse.
    /// Navigates to /Login, completes the login flow, returns the serialized storage state.
    /// Returns null if login fails.
    /// </summary>
    private static async Task<string?> PerformLoginAsync(
        IBrowser browser,
        string baseUrl,
        int settleDelay,
        int? viewportWidth,
        int? viewportHeight,
        string username,
        string password,
        string outputDir,
        string tenantCode)
    {
        var contextOptions = new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        };
        if (viewportWidth.HasValue && viewportHeight.HasValue)
        {
            contextOptions.ViewportSize = new ViewportSize
            {
                Width = viewportWidth.Value,
                Height = viewportHeight.Value
            };
        }

        var context = await browser.NewContextAsync(contextOptions);
        try
        {
            var page = await context.NewPageAsync();

            // Capture browser console messages — critical for diagnosing WASM boot failures
            List<string> consoleErrors = [];
            page.Console += (_, msg) =>
            {
                if (msg.Type == "error" || msg.Type == "warning")
                    consoleErrors.Add($"[{msg.Type}] {msg.Text}");
            };

            // Use tenant code in login URL — FreeCRM apps need this to resolve the tenant
            var loginRoute = string.IsNullOrWhiteSpace(tenantCode) ? "/Login" : $"/{tenantCode}/Login";
            var loginUrl = RouteParser.BuildUrl(baseUrl, loginRoute);

            Console.WriteLine($"  Navigating to {loginUrl}...");
            var response = await page.GotoAsync(loginUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 60000
            });
            Console.WriteLine($"  HTTP {response?.Status} {response?.StatusText}");

            // Blazor WASM needs time to download assemblies, boot the runtime,
            // resolve the tenant from the URL, and render the login component.
            // Wait for the login page content to actually appear in the DOM.
            Console.WriteLine("  Waiting for Blazor WASM to initialize...");
            try
            {
                // Wait for the login-page div (wraps all login content) — up to 30s for WASM cold start
                await page.WaitForSelectorAsync(".login-page", new PageWaitForSelectorOptions { Timeout = 30000 });
                Console.WriteLine("  ? Login page rendered");
            }
            catch (TimeoutException)
            {
                Console.WriteLine("  ?? Login page .login-page div not found after 30s, continuing anyway...");
                await page.WaitForTimeoutAsync(settleDelay);
            }

            // Screenshot the login page (step 1: provider selection)
            var loginBeforePath = PathSanitizer.GetOutputFilePath(outputDir, "_auth-flow", "1-initial.png");
            PathSanitizer.EnsureDirectoryExists(loginBeforePath);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = loginBeforePath, FullPage = true });
            Console.WriteLine("  ?? Step 1: Captured login page");

            // Fill and submit login form (handles two-step provider selection flow)
            var filled = await TryFillLoginFormAsync(page, username, password);
            if (!filled)
            {
                // Dump everything we can for diagnosis
                Console.WriteLine($"  ? Could not find login form fields");

                // Show visible text
                var pageText = await page.InnerTextAsync("body");
                Console.WriteLine($"  Visible text ({pageText.Length} chars): {(pageText.Length > 300 ? pageText[..300] + "..." : pageText)}");

                // Show HTML source (check if Blazor markers are present)
                var html = await page.ContentAsync();
                var hasBlazorMarker = html.Contains("blazor", StringComparison.OrdinalIgnoreCase);
                var hasFrameworkJs = html.Contains("_framework/blazor.web.js", StringComparison.OrdinalIgnoreCase);
                Console.WriteLine($"  HTML size: {html.Length} chars, has blazor markers: {hasBlazorMarker}, has _framework/blazor.web.js: {hasFrameworkJs}");

                // Show any browser console errors — this reveals WASM boot failures
                if (consoleErrors.Count > 0)
                {
                    Console.WriteLine($"  Browser console ({consoleErrors.Count} errors/warnings):");
                    foreach (var err in consoleErrors.Take(10))
                        Console.WriteLine($"    {err}");
                }
                else
                {
                    Console.WriteLine("  Browser console: no errors or warnings");
                }

                return null;
            }

            // Screenshot the filled form (step 2)
            await page.WaitForTimeoutAsync(500);
            var loginFilledPath = PathSanitizer.GetOutputFilePath(outputDir, "_auth-flow", "2-filled.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = loginFilledPath, FullPage = true });
            Console.WriteLine($"  ?? Step 2: Form filled with {username}");

            // Submit the login form
            await SubmitLoginFormAsync(page);

            // Wait for Blazor to process the login and navigate away from the login page.
            // After successful login, the .login-page div should disappear.
            Console.WriteLine("  Waiting for login to complete...");
            try
            {
                await page.WaitForSelectorAsync(".login-page", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Hidden,
                    Timeout = 15000
                });
                Console.WriteLine("  ? Login page disappeared — login successful");
            }
            catch (TimeoutException)
            {
                Console.WriteLine("  ?? Login page still visible after 15s");
            }
            await page.WaitForTimeoutAsync(2000); // Extra settle for Blazor to finish rendering

            // Screenshot the result (step 3: after login)
            var loginResultPath = PathSanitizer.GetOutputFilePath(outputDir, "_auth-flow", "3-result.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = loginResultPath, FullPage = true });
            Console.WriteLine("  ?? Step 3: Captured post-login state");

            // Check if we're still on the login page (login may have failed)
            var stillOnLogin = await HasLoginFormAsync(page);
            if (stillOnLogin)
            {
                Console.WriteLine("  ? Still on login page after submit — credentials may be wrong");
                return null;
            }

            // Save storage state (cookies + localStorage) for reuse
            var storageState = await context.StorageStateAsync();
            Console.WriteLine("  ? Login successful — session saved for Pass 2");
            return storageState;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? Login failed: {ex.Message}");
            return null;
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Pass 2: Capture a single page using a pre-authenticated session ? logged-in.png
    /// For auth-required pages, also captures the login redirect as login-redirect.png
    /// </summary>
    private static async Task<ScreenshotResult> CaptureAuthenticatedScreenshotAsync(
        IBrowser browser,
        RouteInfo routeInfo,
        int index,
        int totalCount,
        string baseUrl,
        string outputDir,
        int? viewportWidth,
        int? viewportHeight,
        int settleDelay,
        string storageState)
    {
        var result = new ScreenshotResult
        {
            Index = index,
            Route = routeInfo.Route,
            RequiresAuth = routeInfo.RequiresAuth,
            Number = index + 1,
            TotalCount = totalCount,
            CapturedAt = DateTime.UtcNow
        };

        // For auth-required pages, capture the login redirect first (unauthenticated)
        if (routeInfo.RequiresAuth)
        {
            try
            {
                var unauthContextOptions = new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true
                };
                if (viewportWidth.HasValue && viewportHeight.HasValue)
                {
                    unauthContextOptions.ViewportSize = new ViewportSize
                    {
                        Width = viewportWidth.Value,
                        Height = viewportHeight.Value
                    };
                }

                var unauthContext = await browser.NewContextAsync(unauthContextOptions);
                try
                {
                    var unauthPage = await unauthContext.NewPageAsync();
                    var url = RouteParser.BuildUrl(baseUrl, routeInfo.Route);

                    await unauthPage.GotoAsync(url, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = 60000
                    });

                    await unauthPage.WaitForTimeoutAsync(settleDelay);

                    var loginRedirectPath = PathSanitizer.GetOutputFilePath(outputDir, routeInfo.Route, "login-redirect.png");
                    PathSanitizer.EnsureDirectoryExists(loginRedirectPath);
                    await unauthPage.ScreenshotAsync(new PageScreenshotOptions { Path = loginRedirectPath, FullPage = true });
                    result.LoginRedirectPath = loginRedirectPath;
                }
                finally
                {
                    await unauthContext.CloseAsync();
                }
            }
            catch
            {
                // Non-critical — continue with authenticated capture
            }
        }

        // Authenticated capture
        var contextOptions = new BrowserNewContextOptions
        {
            StorageState = storageState,
            IgnoreHTTPSErrors = true
        };
        if (viewportWidth.HasValue && viewportHeight.HasValue)
        {
            contextOptions.ViewportSize = new ViewportSize
            {
                Width = viewportWidth.Value,
                Height = viewportHeight.Value
            };
        }

        var context = await browser.NewContextAsync(contextOptions);
        try
        {
            var page = await context.NewPageAsync();
            var url = RouteParser.BuildUrl(baseUrl, routeInfo.Route);
            result.Url = url;

            try
            {
                var response = await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 60000
                });

                result.StatusCode = response?.Status ?? 0;
                result.IsSuccess = result.StatusCode >= 200 && result.StatusCode < 400;
                result.IsHttpError = result.StatusCode >= 400;

                await page.WaitForTimeoutAsync(settleDelay);

                var screenshotPath = PathSanitizer.GetOutputFilePath(outputDir, routeInfo.Route, "logged-in.png");
                PathSanitizer.EnsureDirectoryExists(screenshotPath);
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });

                var fi = new FileInfo(screenshotPath);

                if (fi.Length < SuspiciousFileSizeThreshold)
                {
                    result.RetryAttempted = true;
                    await page.WaitForTimeoutAsync(RetryExtraDelayMs);
                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
                    fi = new FileInfo(screenshotPath);
                }

                result.ScreenshotPath = screenshotPath;
                result.FileSize = fi.Length;
                result.IsSuspiciouslySmall = fi.Length < SuspiciousFileSizeThreshold;
            }
            catch (TimeoutException)
            {
                result.IsError = true;
                result.ErrorMessage = "Navigation timed out";
            }
            catch (Exception ex)
            {
                result.IsError = true;
                result.ErrorMessage = ex.Message;
            }
        }
        finally
        {
            await context.CloseAsync();
        }

        return result;
    }
    /// <summary>
    /// Try to find and fill a login form on the page.
    /// Handles two-step login flows where a provider selection page appears first.
    /// Returns true if a form was found and filled.
    /// </summary>
    private static async Task<bool> TryFillLoginFormAsync(IPage page, string username, string password)
    {
        // Step 0: Check if we're on a login provider selection page (two-step flow)
        // If so, click the local login button first to reveal the username/password form
        await TrySelectLoginProviderAsync(page);

        // Common selectors for username/email fields
        // Note: Blazor Identity uses dots in IDs (Input.Email), CSS requires escaping or attribute selectors
        var usernameSelectors = new[]
        {
            "input[id='login-email']",             // FreeCRM/FreeExamples local login
            "input[name='username']",
            "input[name='Username']",
            "input[name='email']",
            "input[name='Email']",
            "input[name='Input.Email']",           // Blazor Identity (name attribute)
            "input[name='Input.Username']",        // Blazor Identity (name attribute)
            "input[id='username']",
            "input[id='Username']",
            "input[id='email']",
            "input[id='Email']",
            "input[id='Input.Email']",             // Blazor Identity (id with dot)
            "input[id='Input.Username']",          // Blazor Identity (id with dot)
            "input[type='email']",
            "input[autocomplete='username']",
            "input[autocomplete='username webauthn']", // Blazor Identity with passkey support
            "input[placeholder*='user' i]",
            "input[placeholder*='email' i]"
        };

        // Common selectors for password fields
        var passwordSelectors = new[]
        {
            "input[id='login-password']",          // FreeCRM/FreeExamples local login
            "input[name='password']",
            "input[name='Password']",
            "input[name='Input.Password']",        // Blazor Identity (name attribute)
            "input[id='password']",
            "input[id='Password']",
            "input[id='Input.Password']",          // Blazor Identity (id with dot)
            "input[type='password']"
        };

        ILocator? usernameField = null;
        ILocator? passwordField = null;

        // Find username field
        foreach (var selector in usernameSelectors)
        {
            try
            {
                var locator = page.Locator(selector).First;
                if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync())
                {
                    usernameField = locator;
                    break;
                }
            }
            catch { /* Continue to next selector */ }
        }

        // Find password field
        foreach (var selector in passwordSelectors)
        {
            try
            {
                var locator = page.Locator(selector).First;
                if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync())
                {
                    passwordField = locator;
                    break;
                }
            }
            catch { /* Continue to next selector */ }
        }

        // If we found both fields, fill them
        if (usernameField != null && passwordField != null)
        {
            await usernameField.FillAsync(username);
            await passwordField.FillAsync(password);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Try to select the local login provider on a provider selection page.
    /// Some apps (e.g., FreeCRM/FreeExamples) show a provider selection screen first
    /// (Local, Google, Microsoft, etc.) before showing the actual username/password form.
    /// </summary>
    private static async Task TrySelectLoginProviderAsync(IPage page)
    {
        // First, wait for ANY login button to appear (Blazor WASM may still be rendering)
        var providerSelectors = new[]
        {
            "#login-button-local",              // FreeCRM/FreeExamples local login button
            "button:has-text('Local Account')", // Generic local account button
            "button:has-text('local')",         // Case-insensitive local button
        };

        foreach (var selector in providerSelectors)
        {
            try
            {
                // Wait up to 10s for the button to appear in the DOM
                var button = page.Locator(selector).First;
                await button.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });

                Console.WriteLine($"  Found provider button: {selector}");
                await button.ClickAsync();

                // Wait for the login form fields to appear after clicking
                try
                {
                    await page.WaitForSelectorAsync("#login-email", new PageWaitForSelectorOptions { Timeout = 10000 });
                    Console.WriteLine("  ? Login form appeared after provider selection");
                }
                catch (TimeoutException)
                {
                    // Form might use different IDs — fall back to a fixed wait
                    await page.WaitForTimeoutAsync(3000);
                }
                return;
            }
            catch (TimeoutException) { /* Button didn't appear, try next selector */ }
            catch { /* Continue to next selector */ }
        }

        Console.WriteLine("  No provider selection button found (might be single-provider login)");
    }


    /// <summary>
    /// Quick check if the current page has a login form (username + password fields visible)
    /// OR a login provider selection page (buttons to choose login method before showing form).
    /// Used to detect unexpected redirects to login pages.
    /// </summary>
    private static async Task<bool> HasLoginFormAsync(IPage page)
    {
        try
        {
            // Check for visible password field - most reliable indicator of a login form
            var passwordField = page.Locator("input[type='password']").First;
            if (await passwordField.CountAsync() > 0 && await passwordField.IsVisibleAsync())
            {
                // Also verify there's some kind of username/email field
                var usernameSelectors = new[]
                {
                    "input[type='email']",
                    "input[name='Input.Email']",
                    "input[name='email']",
                    "input[name='username']",
                    "input[autocomplete='username']"
                };

                foreach (var selector in usernameSelectors)
                {
                    try
                    {
                        var field = page.Locator(selector).First;
                        if (await field.CountAsync() > 0 && await field.IsVisibleAsync())
                        {
                            return true;
                        }
                    }
                    catch { /* Continue */ }
                }
            }

            // Check for login provider selection page (two-step login flow)
            // Some apps show a provider selection screen first before showing the actual form
            var providerSelectors = new[]
            {
                "#login-button-local",              // FreeCRM/FreeExamples local login button
                ".login-page #login-button-local",  // Scoped to login page
                "button.login-button",              // Generic login button class
            };

            foreach (var selector in providerSelectors)
            {
                try
                {
                    var button = page.Locator(selector).First;
                    if (await button.CountAsync() > 0 && await button.IsVisibleAsync())
                    {
                        return true;
                    }
                }
                catch { /* Continue */ }
            }
        }
        catch { /* Ignore errors */ }

        return false;
    }

    /// <summary>
    /// Submit the login form by clicking a submit button or pressing Enter.
    /// </summary>
    private static async Task SubmitLoginFormAsync(IPage page)
    {
        // Common selectors for submit buttons
        // Note: Blazor apps often use type="button" with @onclick instead of type="submit"
        var submitSelectors = new[]
        {
            "button[type='submit']",
            "input[type='submit']",
            "button:has-text('Log-In')",           // FreeCRM/FreeExamples (hyphenated)
            "button:has-text('Log in')",
            "button:has-text('Login')",
            "button:has-text('Sign in')",
            "button:has-text('Submit')",
            "#login-submit",
            ".login-button",
            ".btn-login"
        };

        foreach (var selector in submitSelectors)
        {
            try
            {
                var locator = page.Locator(selector).First;
                if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync())
                {
                    await locator.ClickAsync();
                    return;
                }
            }
            catch { /* Continue to next selector */ }
        }

        // Fallback: Press Enter on the password field
        try
        {
            var passwordField = page.Locator("input[type='password']").First;
            if (await passwordField.CountAsync() > 0)
            {
                await passwordField.PressAsync("Enter");
            }
        }
        catch { /* Ignore */ }
    }

    // Write metadata JSON for reporter to consume
    private static async Task WriteMetadataAsync(string outputDir, string route, ScreenshotResult result)
    {
        var metadataPath = PathSanitizer.GetOutputFilePath(outputDir, route, "metadata.json");

        var metadata = new ScreenshotMetadata
        {
            Route = result.Route,
            Url = result.Url,
            StatusCode = result.StatusCode,
            FileSize = result.FileSize,
            IsSuspiciouslySmall = result.IsSuspiciouslySmall,
            RetryAttempted = result.RetryAttempted,
            ConsoleErrors = result.ConsoleErrors,
            CapturedAt = result.CapturedAt,
            IsSuccess = result.IsSuccess,
            IsHttpError = result.IsHttpError,
            IsError = result.IsError,
            ErrorMessage = result.ErrorMessage,
            // Auth flow fields
            RequiresAuth = result.RequiresAuth,
            AuthFlowCompleted = result.AuthFlowCompleted,
            AuthStep1Path = result.AuthStep1Path != null ? Path.GetFileName(result.AuthStep1Path) : null,
            AuthStep2Path = result.AuthStep2Path != null ? Path.GetFileName(result.AuthStep2Path) : null,
            AuthStep3Path = result.AuthStep3Path != null ? Path.GetFileName(result.AuthStep3Path) : null,
            AuthFlowNote = result.AuthFlowNote,
            // Two-pass fields
            LoggedInFileSize = result.LoggedInFileSize,
            LoggedInIsSuspiciouslySmall = result.LoggedInIsSuspiciouslySmall,
            LoggedInStatusCode = result.LoggedInStatusCode,
            // Per-page login redirect
            LoginRedirectPath = result.LoginRedirectPath != null ? Path.GetFileName(result.LoginRedirectPath) : null,
        };
        
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync(metadataPath, json);
    }

    private static void WriteResultsInOrder(
        ConcurrentDictionary<int, ScreenshotResult> results,
        ref int nextIndexToWrite,
        object writeLock,
        bool flush = false,
        Action<ScreenshotResult>? writeAction = null)
    {
        writeAction ??= WritePublicResult;

        lock (writeLock)
        {
            // Print results in order without removing them — they're needed for
            // summary counts and metadata writing after both passes complete.
            while (results.ContainsKey(nextIndexToWrite))
            {
                writeAction(results[nextIndexToWrite]);
                nextIndexToWrite++;
            }

            if (flush)
            {
                // Print any remaining out-of-order results
                var startFrom = nextIndexToWrite;
                foreach (var kvp in results.Where(k => k.Key >= startFrom).OrderBy(k => k.Key))
                {
                    writeAction(kvp.Value);
                }
            }
        }
    }

    private static void WritePublicResult(ScreenshotResult result)
    {
        Console.WriteLine($"[{result.Number}/{result.TotalCount}] {result.Route}");

        if (result.IsError)
        {
            Console.WriteLine($"  !! {result.ErrorMessage}");
        }
        else if (!string.IsNullOrEmpty(result.ScreenshotPath))
        {
            var sizeStr = PathSanitizer.FormatBytes(result.FileSize);
            var warning = result.IsSuspiciouslySmall ? " ??" : " ?";
            Console.WriteLine($"  -> default.png ({sizeStr}){warning}");
        }
    }

    private static void WriteAuthenticatedResult(ScreenshotResult result)
    {
        Console.WriteLine($"[{result.Number}/{result.TotalCount}] {result.Route}");

        if (result.IsError)
        {
            Console.WriteLine($"  !! {result.ErrorMessage}");
        }
        else if (!string.IsNullOrEmpty(result.ScreenshotPath))
        {
            var sizeStr = PathSanitizer.FormatBytes(result.FileSize);
            var warning = result.IsSuspiciouslySmall ? " ??" : " ?";
            Console.WriteLine($"  -> logged-in.png ({sizeStr}){warning}");
        }
        Console.WriteLine();
    }

    private static async Task EnsurePlaywrightBrowsersInstalledAsync(string browserName)
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", browserName]);
        
        if (exitCode != 0)
        {
            Console.WriteLine($"Playwright install returned exit code {exitCode}, but continuing anyway...");
        }
        else
        {
            Console.WriteLine($"Playwright {browserName} browser ready.");
        }
        
        await Task.CompletedTask;
    }

    private static (int? width, int? height) ParseViewport(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (null, null);

        var parts = value.Split('x', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var w) &&
            int.TryParse(parts[1], out var h))
        {
            return (w, h);
        }

        return (null, null);
    }
}

// Route information including auth requirement
internal class RouteInfo
{
    public string Route { get; set; } = "";
    public bool RequiresAuth { get; set; }
}

// Result tracking for ordered output
internal class ScreenshotResult
{
    public int Index { get; set; }
    public int Number { get; set; }
    public int TotalCount { get; set; }
    public string Route { get; set; } = "";
    public string Url { get; set; } = "";
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsHttpError { get; set; }
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ScreenshotPath { get; set; }
    public long FileSize { get; set; }
    
    // Retry and suspicious detection
    public bool RetryAttempted { get; set; }
    public bool IsSuspiciouslySmall { get; set; }
    
    // Console error capture
    public List<string> ConsoleErrors { get; set; } = [];
    public DateTime CapturedAt { get; set; }
    
    // Auth flow fields
    public bool RequiresAuth { get; set; }
    public bool AuthFlowCompleted { get; set; }
    public string? AuthStep1Path { get; set; }  // Initial page screenshot
    public string? AuthStep2Path { get; set; }  // Form filled screenshot
    public string? AuthStep3Path { get; set; }  // After submit screenshot
    public string? AuthFlowNote { get; set; }   // Note if auth flow didn't complete

    // Two-pass: logged-in capture data (merged from Pass 2)
    public string? LoggedInScreenshotPath { get; set; }
    public long LoggedInFileSize { get; set; }
    public bool LoggedInIsSuspiciouslySmall { get; set; }
    public int LoggedInStatusCode { get; set; }

    // Per-page login redirect screenshot (captured for auth pages)
    public string? LoginRedirectPath { get; set; }
}

// Metadata for reporter to consume
internal class ScreenshotMetadata
{
    public string Route { get; set; } = "";
    public string Url { get; set; } = "";
    public int StatusCode { get; set; }
    public long FileSize { get; set; }
    public bool IsSuspiciouslySmall { get; set; }
    public bool RetryAttempted { get; set; }
    public List<string> ConsoleErrors { get; set; } = [];
    public DateTime CapturedAt { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsHttpError { get; set; }
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Auth flow fields
    public bool RequiresAuth { get; set; }
    public bool AuthFlowCompleted { get; set; }
    public string? AuthStep1Path { get; set; }
    public string? AuthStep2Path { get; set; }
    public string? AuthStep3Path { get; set; }
    public string? AuthFlowNote { get; set; }

    // Two-pass fields
    public long LoggedInFileSize { get; set; }
    public bool LoggedInIsSuspiciouslySmall { get; set; }
    public int LoggedInStatusCode { get; set; }

    // Per-page login redirect screenshot
    public string? LoginRedirectPath { get; set; }
}
