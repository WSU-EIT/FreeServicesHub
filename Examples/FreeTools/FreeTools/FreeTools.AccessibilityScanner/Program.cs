using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreeTools.Core;
using Microsoft.Playwright;

namespace FreeTools.AccessibilityScanner;

internal class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static async Task<int> Main(string[] args)
    {
        // Robustness: Optional startup delay (for AppHost orchestration)
        var delayEnv = Environment.GetEnvironmentVariable("START_DELAY_MS");
        var startupDelay = int.TryParse(delayEnv, out var delayMs) && delayMs > 0 ? delayMs : 0;
        if (startupDelay > 0)
        {
            Console.WriteLine($"Waiting {startupDelay}ms for server to be ready...");
            await Task.Delay(startupDelay);
        }

        ConsoleOutput.PrintBanner("AccessibilityScanner (FreeTools)", "1.0");

        // Check for AppHost mode: BASE_URL env var overrides appsettings.json
        var baseUrlEnv = Environment.GetEnvironmentVariable("BASE_URL");
        ScannerConfig? config;
        string runsDir;

        if (!string.IsNullOrWhiteSpace(baseUrlEnv))
        {
            // AppHost mode — build config from environment variables
            config = await BuildConfigFromEnvAsync(baseUrlEnv);
            if (config == null)
            {
                Console.Error.WriteLine("Failed to build config from environment variables.");
                return 1;
            }

            var outputDir = Environment.GetEnvironmentVariable("OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                runsDir = Path.Combine(outputDir, "a11y");
            }
            else
            {
                var projectDir = FindProjectDir(AppContext.BaseDirectory);
                runsDir = Path.Combine(projectDir, "runs", "latest");
            }
        }
        else
        {
            // Standalone mode — load from appsettings.json
            config = await LoadConfigAsync();
            if (config == null)
            {
                Console.Error.WriteLine("Failed to load appsettings.json — cannot continue.");
                return 1;
            }

            if (config.Sites.Count == 0)
            {
                Console.Error.WriteLine("No sites configured in appsettings.json → Scanner.Sites");
                return 1;
            }

            var projectDir = FindProjectDir(AppContext.BaseDirectory);
            runsDir = Path.Combine(projectDir, "runs", "latest");
        }
        ConsoleOutput.PrintConfig("Output", runsDir);
        ConsoleOutput.PrintConfig("Sites", config.Sites.Count.ToString());
        ConsoleOutput.PrintConfig("Settle delay", $"{config.SettleDelayMs}ms");
        ConsoleOutput.PrintConfig("Timeout", $"{config.TimeoutMs}ms");
        ConsoleOutput.PrintConfig("Max concurrency", config.MaxConcurrency.ToString());
        ConsoleOutput.PrintConfig("Headless", config.Headless.ToString());
        ConsoleOutput.PrintConfig("WAVE API", string.IsNullOrWhiteSpace(config.WaveApiKey) ? "disabled (no key)" : "enabled");
        ConsoleOutput.PrintDivider();

        // Show configured sites and page counts
        var totalPages = 0;
        foreach (var (url, siteConfig) in config.Sites)
        {
            // +1 for the root page which is always scanned
            var pageCount = 1 + siteConfig.Pages.Count;
            totalPages += pageCount;
            var credLabel = siteConfig.Credentials.Count > 0
                ? $"{siteConfig.Credentials.Count} credential(s)"
                : "no auth";
            Console.WriteLine($"  • {url} — {pageCount} page(s), {credLabel}");
        }

        Console.WriteLine();
        Console.WriteLine($"  Total pages to scan: {totalPages}");
        Console.WriteLine();

        // Clean previous run
        if (Directory.Exists(runsDir))
        {
            Directory.Delete(runsDir, recursive: true);
        }

        try
        {
            Console.WriteLine("[1/3] Ensuring Playwright browsers are installed...");
            var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            if (exitCode != 0)
            {
                Console.WriteLine($"  Playwright install returned {exitCode}, continuing anyway...");
            }
            else
            {
                Console.WriteLine("  Chromium ready.");
            }

            Console.WriteLine("[2/3] Launching browser...");
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = config.Headless
            });

            var maxConcurrency = Math.Max(1, config.MaxConcurrency);
            Console.WriteLine($"[3/3] Scanning sites ({maxConcurrency} at a time)...");
            Console.WriteLine();

            var allResults = new ConcurrentBag<SiteResult>();
            var completed = 0;
            var siteTotal = config.Sites.Count;
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            var tasks = config.Sites.Select(kvp => Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var result = await ScanSiteAsync(browser, kvp.Key, kvp.Value, config, runsDir);
                    allResults.Add(result);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  [{kvp.Key}] Site scan failed: {ex.Message}");
                    allResults.Add(new SiteResult
                    {
                        Url = kvp.Key,
                        FolderName = new Uri(kvp.Key).Host.Replace('.', '-')
                    });
                }
                finally
                {
                    var done = Interlocked.Increment(ref completed);
                    Console.WriteLine($"  ── Site {done}/{siteTotal} complete: {kvp.Key}");
                    semaphore.Release();
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            // Write run-level summary report
            await WriteRunReportAsync(runsDir, allResults);

            // Write accessibility rules legend/index
            await WriteRulesLegendAsync(runsDir, allResults);

            // Print summary
            Console.WriteLine();
            ConsoleOutput.PrintDivider("Summary");

            var grandSuccessCount = 0;
            var grandTotalCount = 0;

            foreach (var site in allResults.OrderBy(r => r.Url))
            {
                Console.WriteLine($"  📂 {site.Url} → {site.FolderName}/");

                foreach (var page in site.Pages.OrderBy(p => p.PagePath))
                {
                    grandTotalCount++;
                    var status = page.Success ? "✅" : "❌";
                    if (page.Success) grandSuccessCount++;

                    Console.WriteLine($"     {status} {page.PagePath}");
                    Console.WriteLine($"        Status:      {page.StatusCode}");
                    Console.WriteLine($"        HTML:        {PathSanitizer.FormatBytes(page.HtmlSize)}");
                    Console.WriteLine($"        Screenshots: {page.Screenshots.Count} ({PathSanitizer.FormatBytes(page.ScreenshotSize)})");
                    Console.WriteLine($"        Images:      {page.Images.Count} (by URL)");

                    if (page.ConsoleErrors.Count > 0)
                    {
                        Console.WriteLine($"        JS errors:  {page.ConsoleErrors.Count}");
                    }

                    if (page.ConsoleWarnings.Count > 0)
                    {
                        Console.WriteLine($"        JS warns:   {page.ConsoleWarnings.Count}");
                    }

                    if (page.CredentialUsed != null)
                    {
                        Console.WriteLine($"        Auth:       {page.CredentialUsed}");
                    }

                    if (page.A11ySummary != null && page.A11ySummary.TotalViolations > 0)
                    {
                        Console.WriteLine($"        A11y:       {page.A11ySummary.TotalViolations} violations (🔴{page.A11ySummary.Critical} 🟠{page.A11ySummary.Serious} 🟡{page.A11ySummary.Moderate} 🔵{page.A11ySummary.Minor})");
                    }

                    if (page.ErrorMessage != null)
                    {
                        Console.WriteLine($"        Error:      {page.ErrorMessage}");
                    }
                }

                Console.WriteLine();
            }

            Console.WriteLine($"Completed: {grandSuccessCount}/{grandTotalCount} pages scanned successfully across {allResults.Count} sites.");
            Console.WriteLine($"Output: {runsDir}");

            return grandSuccessCount == grandTotalCount ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    // ========================================================================
    // Site-level orchestration
    // ========================================================================

    private static async Task<SiteResult> ScanSiteAsync(
        IBrowser browser,
        string siteUrl,
        SiteConfig siteConfig,
        ScannerConfig scannerConfig,
        string runsDir)
    {
        var uri = new Uri(siteUrl);
        var siteFolderName = uri.Host.Replace('.', '-');
        var siteDir = Path.Combine(runsDir, siteFolderName);
        Directory.CreateDirectory(siteDir);

        var siteResult = new SiteResult
        {
            Url = siteUrl,
            FolderName = siteFolderName
        };

        // Download SSL certificate
        Console.WriteLine($"  [{uri.Host}] Downloading certificate...");
        try
        {
            siteResult.Certificate = await DownloadCertificateAsync(siteUrl);
            if (siteResult.Certificate.ErrorMessage == null)
            {
                var sanCount = siteResult.Certificate.SubjectAlternativeNames.Count;
                var expiry = siteResult.Certificate.DaysUntilExpiry;
                var expiryLabel = expiry < 30 ? $"⚠️ {expiry}d" : $"{expiry}d";
                Console.WriteLine($"  [{uri.Host}] Cert: {sanCount} SANs, expires in {expiryLabel}");

                // Save cert.json
                var certPath = Path.Combine(siteDir, "cert.json");
                await File.WriteAllTextAsync(certPath, JsonSerializer.Serialize(siteResult.Certificate, JsonOptions));
            }
            else
            {
                Console.WriteLine($"  [{uri.Host}] Cert: {siteResult.Certificate.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [{uri.Host}] Cert download failed: {ex.Message}");
        }

        // Build the list of pages to scan: root ("/") + configured pages
        var pagePaths = new List<string> { "/" };
        pagePaths.AddRange(siteConfig.Pages);

        foreach (var pagePath in pagePaths)
        {
            try
            {
                var pageResult = await ScanPageAsync(
                    browser, uri, pagePath, siteConfig, scannerConfig, siteDir);
                siteResult.Pages.Add(pageResult);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  [{uri.Host}] {pagePath} — Skipped (unhandled error): {ex.Message}");
                siteResult.Pages.Add(new PageResult
                {
                    PagePath = pagePath,
                    FullUrl = ResolvePageUrl(uri, pagePath),
                    FolderName = PagePathToFolderName(pagePath),
                    Success = false,
                    ErrorMessage = ex.Message,
                    CapturedAt = DateTime.UtcNow
                });
            }
        }

        // Write site-level summary report
        await WriteSiteReportAsync(siteDir, siteResult);

        return siteResult;
    }

    // ========================================================================
    // Page-level scanning
    // ========================================================================

    private static async Task<PageResult> ScanPageAsync(
        IBrowser browser,
        Uri siteUri,
        string pagePath,
        SiteConfig siteConfig,
        ScannerConfig scannerConfig,
        string siteDir)
    {
        // Resolve the full URL (handles both relative and absolute paths)
        var fullUrl = ResolvePageUrl(siteUri, pagePath);
        var folderName = PagePathToFolderName(pagePath);
        var pageDir = Path.Combine(siteDir, folderName);
        Directory.CreateDirectory(pageDir);

        var result = new PageResult
        {
            PagePath = pagePath,
            FullUrl = fullUrl,
            FolderName = folderName,
            CapturedAt = DateTime.UtcNow
        };

        var actions = new List<string>();
        var infoLines = new List<string>();

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = scannerConfig.UserAgent
        });

        try
        {
            var page = await context.NewPageAsync();

            // Capture JS console messages by level
            List<string> consoleErrors = [];
            List<string> consoleWarnings = [];
            List<string> consoleInfo = [];

            page.Console += (_, msg) =>
            {
                switch (msg.Type)
                {
                    case "error":
                        consoleErrors.Add(msg.Text);
                        break;
                    case "warning":
                        consoleWarnings.Add(msg.Text);
                        break;
                    case "info":
                    case "log":
                        consoleInfo.Add(msg.Text);
                        break;
                }
            };

            Console.WriteLine($"  [{siteUri.Host}] {pagePath} — Navigating...");
            infoLines.Add($"Navigating to: {fullUrl}");
            infoLines.Add($"Started at: {result.CapturedAt:O}");

            // Navigate
            IResponse? response = null;
            try
            {
                response = await page.GotoAsync(fullUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = scannerConfig.TimeoutMs
                });
            }
            catch (TimeoutException)
            {
                Console.WriteLine($"  [{siteUri.Host}] {pagePath} — NetworkIdle timed out — capturing current state...");
                infoLines.Add("WARNING: NetworkIdle timed out — captured current page state");
            }

            result.StatusCode = response?.Status ?? 0;
            infoLines.Add($"Status code: {result.StatusCode}");

            // Check for redirect
            var currentUrl = page.Url;
            if (!string.Equals(fullUrl.TrimEnd('/'), currentUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                infoLines.Add($"Redirected to: {currentUrl}");
                result.FinalUrl = currentUrl;
            }

            // Settle delay
            await page.WaitForTimeoutAsync(scannerConfig.SettleDelayMs);
            infoLines.Add($"Settle delay: {scannerConfig.SettleDelayMs}ms");

            // Screenshot: page after initial load + settle
            await TakeScreenshotAsync(page, pageDir, result, actions, "page-loaded");

            // Auth flow if credentials configured
            if (siteConfig.Credentials.Count > 0)
            {
                foreach (var cred in siteConfig.Credentials)
                {
                    Console.WriteLine($"  [{siteUri.Host}] {pagePath} — Attempting auth as '{cred.Username}'...");
                    actions.Add($"Attempted login as '{cred.Username}'");
                    var authSuccess = await TryAuthFlowAsync(page, cred, scannerConfig, pageDir, result, actions);
                    if (authSuccess)
                    {
                        result.CredentialUsed = cred.Username;
                        actions.Add($"Auth flow completed for '{cred.Username}'");
                        Console.WriteLine($"  [{siteUri.Host}] {pagePath} — Auth completed for '{cred.Username}'");
                        break;
                    }
                    else
                    {
                        actions.Add($"No login form found for '{cred.Username}'");
                    }
                }
            }

            if (actions.Count == 0)
            {
                actions.Add("No interactions performed — page was captured as-is");
            }

            // =================================================================
            // Expand all collapsed/hidden content before a11y scanning
            // =================================================================
            // This ensures accordions, details, hidden tabs, and collapsed panels
            // are open and visible for both the HTML snapshot and screenshots.
            // Without this, axe-core can't reach hidden DOM content and screenshots
            // miss entire sections.
            try
            {
                Console.WriteLine($"  [{siteUri.Host}] {pagePath} — Expanding all collapsed content...");
                var expandCount = await ExpandAllCollapsedContentAsync(page);
                if (expandCount > 0)
                {
                    actions.Add($"Expanded {expandCount} collapsed section(s)");
                    await page.WaitForTimeoutAsync(500); // Brief settle after expanding
                    await TakeScreenshotAsync(page, pageDir, result, actions, "page-expanded");
                }
            }
            catch (Exception expandEx)
            {
                Console.Error.WriteLine($"  [{siteUri.Host}] {pagePath} — Expand failed: {expandEx.Message}");
                actions.Add($"Content expansion failed: {expandEx.Message}");
            }

            // Save HTML content
            Console.WriteLine($"  [{siteUri.Host}] {pagePath} — Saving HTML...");
            var htmlContent = await page.ContentAsync();
            var htmlPath = Path.Combine(pageDir, "page.html");
            await File.WriteAllTextAsync(htmlPath, htmlContent);
            result.HtmlSize = new FileInfo(htmlPath).Length;
            infoLines.Add($"HTML size: {PathSanitizer.FormatBytes(result.HtmlSize)}");

            // Catalog page images
            Console.WriteLine($"  [{siteUri.Host}] {pagePath} — Cataloging images...");
            await DownloadPageImagesAsync(page, pageDir, result, actions, infoLines);

            // Accessibility scanning
            Console.WriteLine($"  [{siteUri.Host}] {pagePath} — Running accessibility checks...");
            try
            {
                var axeScript = await EnsureAxeCoreAsync(FindProjectDir(AppContext.BaseDirectory));

                var axeResult = await RunAxeCoreAsync(page, axeScript, scannerConfig.WcagLevel);
                actions.Add($"axe-core: {axeResult.Issues.Count} violations ({axeResult.DurationMs}ms)");

                var htmlCheckResult = RunHtmlCheck(htmlContent, fullUrl);
                actions.Add($"htmlcheck: {htmlCheckResult.Issues.Count} violations ({htmlCheckResult.DurationMs}ms)");

                // WAVE API (optional — requires API key in config or user secrets)
                A11yToolResult waveResult;
                if (!string.IsNullOrWhiteSpace(scannerConfig.WaveApiKey))
                {
                    waveResult = await RunWaveApiAsync(fullUrl, scannerConfig.WaveApiKey);
                    actions.Add($"wave: {waveResult.Issues.Count} violations ({waveResult.DurationMs}ms) [{waveResult.Status}]");
                    Console.WriteLine($"  [{siteUri.Host}] {pagePath} — WAVE: {waveResult.Issues.Count} issues ({waveResult.Status})");
                }
                else
                {
                    waveResult = new A11yToolResult { ToolName = "wave", Status = "skipped", ErrorMessage = "No WAVE API key configured" };
                }

                result.A11ySummary = MergeA11yResults(axeResult, htmlCheckResult, waveResult, pageDir);

                Console.WriteLine($"  [{siteUri.Host}] {pagePath} — A11y: {result.A11ySummary.TotalViolations} total violations (axe:{axeResult.Issues.Count} htmlcheck:{htmlCheckResult.Issues.Count} wave:{waveResult.Issues.Count})");
            }
            catch (Exception a11yEx)
            {
                Console.Error.WriteLine($"  [{siteUri.Host}] {pagePath} — A11y scan error: {a11yEx.Message}");
                actions.Add($"A11y scan failed: {a11yEx.Message}");
            }

            // =================================================================
            // A11y overlay screenshots — each uses a different tool/perspective
            // =================================================================
            var projectDir = FindProjectDir(AppContext.BaseDirectory);

            // 1. axe overlay: highlight violations with colored outlines and labels
            try
            {
                Console.WriteLine($"  [{siteUri.Host}] {pagePath} — [2] axe overlay...");
                await InjectAxeHighlightOverlayAsync(page, scannerConfig.WcagLevel);
                await TakeScreenshotAsync(page, pageDir, result, actions, "axe-overlay");
                await RemoveAxeHighlightOverlayAsync(page);
            }
            catch (Exception ex) { LogOverlayError(siteUri, pagePath, "axe-overlay", ex, actions); }

            // 2. WAVE-style overlay: place categorized icons next to a11y-relevant elements
            try
            {
                Console.WriteLine($"  [{siteUri.Host}] {pagePath} — [3] WAVE-style overlay...");
                await InjectWaveStyleOverlayAsync(page);
                await TakeScreenshotAsync(page, pageDir, result, actions, "wave-overlay");
                await RemoveWaveStyleOverlayAsync(page);
            }
            catch (Exception ex) { LogOverlayError(siteUri, pagePath, "wave-overlay", ex, actions); }

            // 3. HTML_CodeSniffer overlay (Pa11y engine — different rule set from axe)
            try
            {
                var htmlCsScript = await EnsureHtmlCodeSnifferAsync(projectDir);
                Console.WriteLine($"  [{siteUri.Host}] {pagePath} — [4] HTML_CodeSniffer overlay...");
                await InjectHtmlCodeSnifferOverlayAsync(page, htmlCsScript);
                await TakeScreenshotAsync(page, pageDir, result, actions, "htmlcs-overlay");
                await RemoveHtmlCodeSnifferOverlayAsync(page);
            }
            catch (Exception ex) { LogOverlayError(siteUri, pagePath, "htmlcs-overlay", ex, actions); }

            // 4. IBM Equal Access overlay (IBM's engine — third independent perspective)
            try
            {
                var ibmAceScript = await EnsureIbmAceAsync(projectDir);
                Console.WriteLine($"  [{siteUri.Host}] {pagePath} — [5] IBM Equal Access overlay...");
                await InjectIbmAceOverlayAsync(page, ibmAceScript);
                await TakeScreenshotAsync(page, pageDir, result, actions, "ibm-a11y-overlay");
                await RemoveIbmAceOverlayAsync(page);
            }
            catch (Exception ex) { LogOverlayError(siteUri, pagePath, "ibm-a11y-overlay", ex, actions); }

            // 5. Focus order + heading structure + landmarks overlay
            try
            {
                Console.WriteLine($"  [{siteUri.Host}] {pagePath} — [6] Structure overlay...");
                await InjectStructureOverlayAsync(page);
                await TakeScreenshotAsync(page, pageDir, result, actions, "structure-overlay");
                await RemoveStructureOverlayAsync(page);
            }
            catch (Exception ex) { LogOverlayError(siteUri, pagePath, "structure-overlay", ex, actions); }

            // 6-12. Color blindness simulations (7 types)
            foreach (var (cvdType, (cvdLabel, cvdDesc, cvdMatrix)) in ColorBlindTypes)
            {
                try
                {
                    Console.WriteLine($"  [{siteUri.Host}] {pagePath} — [CVD] {cvdLabel}...");
                    await InjectColorBlindFilterAsync(page, cvdType, cvdLabel, cvdDesc, cvdMatrix);
                    await TakeScreenshotAsync(page, pageDir, result, actions, $"cvd-{cvdType}");
                    await RemoveColorBlindFilterAsync(page);
                }
                catch (Exception ex) { LogOverlayError(siteUri, pagePath, $"cvd-{cvdType}", ex, actions); }
            }

            // 13. Screen reader text view (linearized accessible content tree)
            try
            {
                Console.WriteLine($"  [{siteUri.Host}] {pagePath} — [SR] Screen reader text view...");
                await InjectScreenReaderViewAsync(page);
                await TakeScreenshotAsync(page, pageDir, result, actions, "screenreader-view");
                await RemoveScreenReaderViewAsync(page);
            }
            catch (Exception ex) { LogOverlayError(siteUri, pagePath, "screenreader-view", ex, actions); }

            // Page title
            var title = await page.TitleAsync();
            result.Title = title;
            infoLines.Add($"Title: {title}");

            // Response headers
            var headers = response?.Headers;
            infoLines.Add($"Screenshots: {result.Screenshots.Count} ({PathSanitizer.FormatBytes(result.ScreenshotSize)})");
            infoLines.Add($"Completed at: {DateTime.UtcNow:O}");

            // Populate result
            result.ConsoleErrors = consoleErrors;
            result.ConsoleWarnings = consoleWarnings;
            result.Success = result.StatusCode >= 200 && result.StatusCode < 400;

            // Write all output files
            await WritePageOutputAsync(pageDir, result, actions, consoleErrors, consoleWarnings, consoleInfo, infoLines, headers);

            Console.WriteLine($"  [{siteUri.Host}] {pagePath} — Done — {result.StatusCode}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Console.Error.WriteLine($"  [{siteUri.Host}] {pagePath} — Error: {ex.Message}");

            // Still write what we can
            infoLines.Add($"FATAL ERROR: {ex.Message}");
            actions.Add("Scan aborted due to error");
            try
            {
                await WritePageOutputAsync(pageDir, result, actions, [], [], [], infoLines, null);
            }
            catch { /* Don't fail on error reporting */ }
        }
        finally
        {
            await context.CloseAsync();
        }

        return result;
    }

    // ========================================================================
    // Page output files
    // ========================================================================

    private static async Task WritePageOutputAsync(
        string pageDir,
        PageResult result,
        List<string> actions,
        List<string> consoleErrors,
        List<string> consoleWarnings,
        List<string> consoleInfo,
        List<string> infoLines,
        Dictionary<string, string>? headers)
    {
        // errors.log
        var errorsPath = Path.Combine(pageDir, "errors.log");
        if (consoleErrors.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# JavaScript Console Errors — {result.FullUrl}");
            sb.AppendLine($"# Captured: {result.CapturedAt:O}");
            sb.AppendLine($"# Count: {consoleErrors.Count}");
            sb.AppendLine();
            foreach (var error in consoleErrors)
            {
                sb.AppendLine(error);
            }

            await File.WriteAllTextAsync(errorsPath, sb.ToString());
        }
        else
        {
            await File.WriteAllTextAsync(errorsPath, $"# No JavaScript errors captured\n# {result.FullUrl}\n# {result.CapturedAt:O}\n");
        }

        // warnings.log
        var warningsPath = Path.Combine(pageDir, "warnings.log");
        if (consoleWarnings.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# JavaScript Console Warnings — {result.FullUrl}");
            sb.AppendLine($"# Captured: {result.CapturedAt:O}");
            sb.AppendLine($"# Count: {consoleWarnings.Count}");
            sb.AppendLine();
            foreach (var warning in consoleWarnings)
            {
                sb.AppendLine(warning);
            }

            await File.WriteAllTextAsync(warningsPath, sb.ToString());
        }
        else
        {
            await File.WriteAllTextAsync(warningsPath, $"# No JavaScript warnings captured\n# {result.FullUrl}\n# {result.CapturedAt:O}\n");
        }

        // info.log
        var infoPath = Path.Combine(pageDir, "info.log");
        var infoSb = new StringBuilder();
        infoSb.AppendLine($"# Scan Info — {result.FullUrl}");
        infoSb.AppendLine();
        foreach (var line in infoLines)
        {
            infoSb.AppendLine(line);
        }

        if (consoleInfo.Count > 0)
        {
            infoSb.AppendLine();
            infoSb.AppendLine($"# Console log/info messages ({consoleInfo.Count}):");
            foreach (var msg in consoleInfo)
            {
                infoSb.AppendLine($"  {msg}");
            }
        }

        await File.WriteAllTextAsync(infoPath, infoSb.ToString());

        // actions.log
        var actionsPath = Path.Combine(pageDir, "actions.log");
        var actionsSb = new StringBuilder();
        actionsSb.AppendLine($"# Actions Performed — {result.FullUrl}");
        actionsSb.AppendLine($"# Captured: {result.CapturedAt:O}");
        actionsSb.AppendLine();
        foreach (var action in actions)
        {
            actionsSb.AppendLine($"- {action}");
        }

        await File.WriteAllTextAsync(actionsPath, actionsSb.ToString());

        // report.md
        var reportPath = Path.Combine(pageDir, "report.md");
        var reportSb = new StringBuilder();
        var statusEmoji = result.Success ? "✅" : "❌";
        var missingAltCount = result.Images.Count(i => string.IsNullOrWhiteSpace(i.AltText));

        reportSb.AppendLine($"# 📄 Page Scan Report");
        reportSb.AppendLine();
        reportSb.AppendLine($"> **URL:** {result.FullUrl}  ");
        reportSb.AppendLine($"> **Captured:** {result.CapturedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        reportSb.AppendLine($"> **Status:** {statusEmoji} {result.StatusCode}  ");
        reportSb.AppendLine();
        reportSb.AppendLine("---");
        reportSb.AppendLine();

        // Table of Contents
        reportSb.AppendLine("## 📑 Contents");
        reportSb.AppendLine();
        reportSb.AppendLine("- [Summary](#-summary)");
        reportSb.AppendLine("- [Screenshots](#-screenshots)");
        reportSb.AppendLine("- [Page Images](#-page-images)");
        if (result.ConsoleErrors.Count > 0) reportSb.AppendLine("- [JavaScript Errors](#-javascript-errors)");
        if (result.A11ySummary != null) reportSb.AppendLine("- [Accessibility](#-accessibility)");
        reportSb.AppendLine("- [Actions](#-actions)");
        reportSb.AppendLine("- [Files](#-files)");
        reportSb.AppendLine();
        reportSb.AppendLine("---");
        reportSb.AppendLine();

        // Summary
        reportSb.AppendLine($"## 📋 Summary");
        reportSb.AppendLine();
        reportSb.AppendLine($"| Field | Value |");
        reportSb.AppendLine($"|-------|-------|");
        reportSb.AppendLine($"| URL | {result.FullUrl} |");
        if (result.FinalUrl != null)
        {
            reportSb.AppendLine($"| Redirected To | {result.FinalUrl} |");
        }

        reportSb.AppendLine($"| Title | {result.Title ?? "*(none)*"} |");
        reportSb.AppendLine($"| Status | {statusEmoji} {result.StatusCode} |");
        reportSb.AppendLine($"| HTML Size | {PathSanitizer.FormatBytes(result.HtmlSize)} |");
        reportSb.AppendLine($"| Screenshots | {result.Screenshots.Count} ({PathSanitizer.FormatBytes(result.ScreenshotSize)}) |");
        reportSb.AppendLine($"| Images | {result.Images.Count} (referenced by URL) |");
        reportSb.AppendLine($"| Images Missing Alt | {(missingAltCount > 0 ? $"⚠️ {missingAltCount}" : "✅ 0")} |");
        reportSb.AppendLine($"| JS Errors | {(result.ConsoleErrors.Count > 0 ? $"🔴 {result.ConsoleErrors.Count}" : "✅ 0")} |");
        reportSb.AppendLine($"| JS Warnings | {result.ConsoleWarnings.Count} |");
        if (result.A11ySummary != null)
        {
            reportSb.AppendLine($"| A11y Violations | {(result.A11ySummary.TotalViolations > 0 ? $"⚠️ {result.A11ySummary.TotalViolations}" : "✅ 0")} |");
            if (result.A11ySummary.TotalViolations > 0)
            {
                reportSb.AppendLine($"| 🔴 Critical | {result.A11ySummary.Critical} |");
                reportSb.AppendLine($"| 🟠 Serious | {result.A11ySummary.Serious} |");
                reportSb.AppendLine($"| 🟡 Moderate | {result.A11ySummary.Moderate} |");
                reportSb.AppendLine($"| 🔵 Minor | {result.A11ySummary.Minor} |");
                reportSb.AppendLine($"| Tools Run | {string.Join(", ", result.A11ySummary.ToolsRun)} |");
            }
        }
        reportSb.AppendLine($"| Auth | {result.CredentialUsed ?? "none"} |");
        reportSb.AppendLine($"| Captured | {result.CapturedAt:O} |");
        reportSb.AppendLine();

        if (result.ErrorMessage != null)
        {
            reportSb.AppendLine($"> ❌ **Error:** `{result.ErrorMessage}`");
            reportSb.AppendLine();
        }

        // Console errors — accordion
        if (result.ConsoleErrors.Count > 0)
        {
            reportSb.AppendLine($"## 🔴 JavaScript Errors");
            reportSb.AppendLine();
            reportSb.AppendLine("<details>");
            reportSb.AppendLine($"<summary><strong>{result.ConsoleErrors.Count} error(s) detected</strong></summary>");
            reportSb.AppendLine();
            reportSb.AppendLine("```");
            foreach (var error in result.ConsoleErrors.Take(20))
            {
                reportSb.AppendLine(error.Length > 200 ? error[..200] + "..." : error);
            }
            if (result.ConsoleErrors.Count > 20)
            {
                reportSb.AppendLine($"... and {result.ConsoleErrors.Count - 20} more (see errors.log)");
            }
            reportSb.AppendLine("```");
            reportSb.AppendLine();
            reportSb.AppendLine("</details>");
            reportSb.AppendLine();
        }

        // Actions — accordion
        reportSb.AppendLine($"## 🔧 Actions");
        reportSb.AppendLine();
        reportSb.AppendLine("<details>");
        reportSb.AppendLine($"<summary><strong>{actions.Count} action(s) performed</strong></summary>");
        reportSb.AppendLine();
        foreach (var action in actions)
        {
            reportSb.AppendLine($"- {action}");
        }
        reportSb.AppendLine();
        reportSb.AppendLine("</details>");
        reportSb.AppendLine();

        // Screenshots — HTML gallery
        reportSb.AppendLine($"## 📸 Screenshots");
        reportSb.AppendLine();

        if (result.Screenshots.Count > 0)
        {
            reportSb.AppendLine("<table>");
            for (int i = 0; i < result.Screenshots.Count; i += 2)
            {
                reportSb.AppendLine("<tr>");
                for (int j = 0; j < 2; j++)
                {
                    var idx = i + j;
                    if (idx < result.Screenshots.Count)
                    {
                        var shot = result.Screenshots[idx];
                        reportSb.AppendLine("<td align=\"center\" width=\"50%\">");
                        reportSb.AppendLine($"<a href=\"{shot.FileName}\">");
                        reportSb.AppendLine($"<img src=\"{shot.FileName}\" width=\"400\" alt=\"{shot.Label}\" />");
                        reportSb.AppendLine("</a>");
                        reportSb.AppendLine($"<br /><strong>{shot.StepNumber}. {shot.Label}</strong>");
                        reportSb.AppendLine($"<br /><sub>{PathSanitizer.FormatBytes(shot.FileSize)}</sub>");
                        reportSb.AppendLine("</td>");
                    }
                    else
                    {
                        reportSb.AppendLine("<td></td>");
                    }
                }
                reportSb.AppendLine("</tr>");
            }
            reportSb.AppendLine("</table>");
        }
        else
        {
            reportSb.AppendLine("*No screenshots captured.*");
        }
        reportSb.AppendLine();

        // Image gallery — HTML grid with accordions
        reportSb.AppendLine($"## 🖼️ Page Images ({result.Images.Count})");
        reportSb.AppendLine();

        if (result.Images.Count > 0)
        {
            // Summary table in accordion
            reportSb.AppendLine("<details open>");
            reportSb.AppendLine($"<summary><strong>📋 Image Index</strong> — {result.Images.Count} images (referenced by URL)</summary>");
            reportSb.AppendLine();
            reportSb.AppendLine($"| # | Source URL | Alt Text |");
            reportSb.AppendLine($"|--:|-----------|----------|");

            var imgNum = 0;
            foreach (var img in result.Images)
            {
                imgNum++;
                var alt = Truncate(img.AltText, 40);
                if (string.IsNullOrWhiteSpace(alt)) alt = "⚠️ *(missing)*";
                reportSb.AppendLine($"| {imgNum} | {Truncate(img.SourceUrl, 80)} | {alt} |");
            }
            reportSb.AppendLine();
            reportSb.AppendLine("</details>");
            reportSb.AppendLine();

            // Thumbnail gallery — 3-column HTML grid using source URLs
            reportSb.AppendLine("<details open>");
            reportSb.AppendLine($"<summary><strong>🖼️ Gallery</strong></summary>");
            reportSb.AppendLine();
            reportSb.AppendLine("<table>");
            for (int i = 0; i < result.Images.Count; i += 3)
            {
                reportSb.AppendLine("<tr>");
                for (int j = 0; j < 3; j++)
                {
                    var idx = i + j;
                    if (idx < result.Images.Count)
                    {
                        var img = result.Images[idx];
                        var alt = string.IsNullOrWhiteSpace(img.AltText) ? img.SourceUrl : img.AltText;
                        var altBadge = string.IsNullOrWhiteSpace(img.AltText) ? " ⚠️" : "";
                        reportSb.AppendLine("<td align=\"center\" width=\"33%\">");
                        reportSb.AppendLine($"<a href=\"{img.SourceUrl}\">");
                        reportSb.AppendLine($"<img src=\"{img.SourceUrl}\" width=\"200\" alt=\"{alt}\" />");
                        reportSb.AppendLine("</a>");
                        reportSb.AppendLine($"<br /><sub>{Truncate(img.SourceUrl, 50)}{altBadge}</sub>");
                        reportSb.AppendLine("</td>");
                    }
                    else
                    {
                        reportSb.AppendLine("<td></td>");
                    }
                }
                reportSb.AppendLine("</tr>");
            }
            reportSb.AppendLine("</table>");
            reportSb.AppendLine();
            reportSb.AppendLine("</details>");
            reportSb.AppendLine();

            // Images missing alt text — accessibility flag
            var noAlt = result.Images.Where(i => string.IsNullOrWhiteSpace(i.AltText)).ToList();
            if (noAlt.Count > 0)
            {
                reportSb.AppendLine("<details>");
                reportSb.AppendLine($"<summary>⚠️ <strong>Images Missing Alt Text</strong> ({noAlt.Count})</summary>");
                reportSb.AppendLine();
                reportSb.AppendLine("| # | Source URL |");
                reportSb.AppendLine("|--:|-----------|" );
                var noAltNum = 0;
                foreach (var img in noAlt)
                {
                    noAltNum++;
                    reportSb.AppendLine($"| {noAltNum} | {Truncate(img.SourceUrl, 80)} |");
                }
                reportSb.AppendLine();
                reportSb.AppendLine("</details>");
                reportSb.AppendLine();
            }
        }
        else
        {
            reportSb.AppendLine("*No images found on page.*");
            reportSb.AppendLine();
        }

        // Accessibility section
        if (result.A11ySummary != null && result.A11ySummary.TotalViolations > 0)
        {
            var a11y = result.A11ySummary;
            reportSb.AppendLine($"## ♿ Accessibility");
            reportSb.AppendLine();

            // Cross-tool summary table
            reportSb.AppendLine("### Summary");
            reportSb.AppendLine();
            var toolNames = a11y.ToolsRun;
            var headerCols = string.Join(" | ", toolNames.Select(t => t));
            var alignCols = string.Join("|", toolNames.Select(_ => ":---:"));
            reportSb.AppendLine($"| Severity | {headerCols} |");
            reportSb.AppendLine($"|----------|{alignCols}|");

            foreach (var sev in new[] { "critical", "serious", "moderate", "minor" })
            {
                var emoji = SeverityEmoji(sev);
                var cols = toolNames.Select(t =>
                {
                    if (!a11y.ByTool.TryGetValue(t, out var ts)) return "—";
                    var count = sev switch
                    {
                        "critical" => ts.Critical,
                        "serious" => ts.Serious,
                        "moderate" => ts.Moderate,
                        "minor" => ts.Minor,
                        _ => 0
                    };
                    return count > 0 ? count.ToString() : "0";
                });
                reportSb.AppendLine($"| {emoji} {sev} | {string.Join(" | ", cols)} |");
            }

            var totalCols = toolNames.Select(t =>
                a11y.ByTool.TryGetValue(t, out var ts) ? $"**{ts.Total}**" : "—");
            reportSb.AppendLine($"| **Total** | {string.Join(" | ", totalCols)} |");
            reportSb.AppendLine();

            // Ranked violations table
            if (a11y.RankedRules.Count > 0)
            {
                reportSb.AppendLine("### Violations by Confidence");
                reportSb.AppendLine();
                reportSb.AppendLine("<details open>");
                reportSb.AppendLine($"<summary><strong>{a11y.RankedRules.Count} rule(s) violated</strong></summary>");
                reportSb.AppendLine();
                reportSb.AppendLine($"| # | Rule | Sev | Confidence | {headerCols} | Example |");
                reportSb.AppendLine($"|--:|------|:---:|:----------:|{alignCols}|---------|");

                foreach (var rule in a11y.RankedRules.Take(30))
                {
                    var emoji = SeverityEmoji(rule.Severity);
                    var confEmoji = rule.Confidence switch
                    {
                        "high" => "🟢",
                        "medium" => "🟡",
                        _ => "🔵"
                    };

                    var toolCols = toolNames.Select(t =>
                    {
                        if (ToolCannotCheck.TryGetValue(t, out var cant) && cant.Contains(rule.CanonicalRuleId))
                            return "—";
                        return rule.ToolsFound.Contains(t) ? "⚠️" : "✅";
                    });

                    var snippet = rule.ExampleSnippet != null
                        ? $"`{Truncate(rule.ExampleSnippet.Replace("|", "\\|").Replace("`", "'"), 60)}`"
                        : "";

                    reportSb.AppendLine($"| {rule.Rank} | {RuleLink(rule.CanonicalRuleId, "../../a11y-rules.md")} | {emoji} | {confEmoji} {rule.Consensus} | {string.Join(" | ", toolCols)} | {snippet} |");
                }

                if (a11y.RankedRules.Count > 30)
                {
                    reportSb.AppendLine($"| | *...and {a11y.RankedRules.Count - 30} more* | | | | |");
                }

                reportSb.AppendLine();
                reportSb.AppendLine("</details>");
                reportSb.AppendLine();
            }

            // Disclaimer
            reportSb.AppendLine("> **Note:** Automated scanning catches ~30-60% of WCAG issues. Manual keyboard and screen reader testing is still required for full compliance.");
            reportSb.AppendLine();
        }
        else if (result.A11ySummary != null && result.A11ySummary.TotalViolations == 0)
        {
            reportSb.AppendLine($"## ♿ Accessibility");
            reportSb.AppendLine();
            reportSb.AppendLine($"✅ No violations detected by {result.A11ySummary.ToolsRun.Count} tool(s).");
            reportSb.AppendLine();
        }

        // Files section
        reportSb.AppendLine($"## 📁 Files");
        reportSb.AppendLine();
        reportSb.AppendLine("| File | Description |");
        reportSb.AppendLine("|------|-------------|");
        foreach (var shot in result.Screenshots)
        {
            reportSb.AppendLine($"| `{shot.FileName}` | {shot.Label} ({PathSanitizer.FormatBytes(shot.FileSize)}) |");
        }
        reportSb.AppendLine($"| `page.html` | Rendered HTML content |");
        reportSb.AppendLine($"| `metadata.json` | Machine-readable scan data |");
        reportSb.AppendLine($"| `errors.log` | JavaScript console errors |");
        reportSb.AppendLine($"| `warnings.log` | JavaScript console warnings |");
        reportSb.AppendLine($"| `info.log` | Navigation and timing details |");
        reportSb.AppendLine($"| `actions.log` | Interactions performed |");
        if (result.A11ySummary != null)
        {
            foreach (var tool in result.A11ySummary.ToolsRun)
            {
                reportSb.AppendLine($"| `a11y-{tool}.json` | {tool} accessibility results |");
            }
            reportSb.AppendLine($"| `a11y-summary.json` | Merged cross-tool accessibility summary |");
        }
        reportSb.AppendLine();
        reportSb.AppendLine("---");
        reportSb.AppendLine();
        reportSb.AppendLine("*Generated by AccessibilityScanner (FreeTools) v1.0*");

        await File.WriteAllTextAsync(reportPath, reportSb.ToString());

        // metadata.json
        var metadataPath = Path.Combine(pageDir, "metadata.json");
        var metadata = new
        {
            result.PagePath,
            Url = result.FullUrl,
            result.FinalUrl,
            result.Title,
            result.StatusCode,
            result.Success,
            HtmlSizeBytes = result.HtmlSize,
            ScreenshotSizeBytes = result.ScreenshotSize,
            result.CredentialUsed,
            ConsoleErrorCount = result.ConsoleErrors.Count,
            ConsoleWarningCount = result.ConsoleWarnings.Count,
            Screenshots = result.Screenshots.Select(s => new { s.FileName, s.Label, s.StepNumber, s.FileSize }).ToArray(),
            ImageCount = result.Images.Count,
            ImagesMissingAlt = result.Images.Count(i => string.IsNullOrWhiteSpace(i.AltText)),
            Images = result.Images.Select(i => new { i.SourceUrl, i.AltText }).ToArray(),
            Accessibility = result.A11ySummary != null ? new
            {
                result.A11ySummary.ToolsRun,
                result.A11ySummary.ToolsSkipped,
                result.A11ySummary.TotalViolations,
                result.A11ySummary.Critical,
                result.A11ySummary.Serious,
                result.A11ySummary.Moderate,
                result.A11ySummary.Minor,
                result.A11ySummary.ByTool
            } : null,
            result.ErrorMessage,
            Headers = headers,
            result.CapturedAt
        };

        var json = JsonSerializer.Serialize(metadata, JsonOptions);
        await File.WriteAllTextAsync(metadataPath, json);
    }

    // ========================================================================
    // Site-level report (one per site folder)
    // ========================================================================

    private static async Task WriteSiteReportAsync(string siteDir, SiteResult site)
    {
        var sb = new StringBuilder();
        var successCount = site.Pages.Count(p => p.Success);
        var totalCount = site.Pages.Count;
        var totalErrors = site.Pages.Sum(p => p.ConsoleErrors.Count);
        var totalWarnings = site.Pages.Sum(p => p.ConsoleWarnings.Count);
        var totalHtml = site.Pages.Sum(p => p.HtmlSize);
        var totalScreenshots = site.Pages.Sum(p => p.ScreenshotSize);
        var totalImages = site.Pages.Sum(p => p.Images.Count);
        var totalMissingAlt = site.Pages.Sum(p => p.Images.Count(i => string.IsNullOrWhiteSpace(i.AltText)));
        var failedPages = site.Pages.Where(p => !p.Success).ToList();
        var pagesWithErrors = site.Pages.Where(p => p.ConsoleErrors.Count > 0).ToList();
        var statusEmoji = successCount == totalCount ? "✅" : "⚠️";
        var successPct = totalCount > 0 ? (successCount * 100.0 / totalCount) : 0;

        sb.AppendLine($"# 🌐 Site Report: {site.Url}");
        sb.AppendLine();
        sb.AppendLine($"> **Status:** {statusEmoji} {successCount}/{totalCount} pages OK  ");
        sb.AppendLine($"> **Folder:** `{site.FolderName}/`  ");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Summary dashboard
        sb.AppendLine($"## 📋 Summary");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine($"Success Rate:  {GenerateProgressBar(successPct, 30)} {successPct:F0}%");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Value |");
        sb.AppendLine($"|--------|-------|");
        sb.AppendLine($"| Pages Scanned | {totalCount} |");
        sb.AppendLine($"| Pages Passed | ✅ {successCount} |");
        sb.AppendLine($"| Pages Failed | {(failedPages.Count > 0 ? $"❌ {failedPages.Count}" : "0")} |");
        sb.AppendLine($"| Total JS Errors | {(totalErrors > 0 ? $"🔴 {totalErrors}" : "0")} |");
        sb.AppendLine($"| Total JS Warnings | {totalWarnings} |");
        sb.AppendLine($"| Total Images | {totalImages} (by URL) |");
        sb.AppendLine($"| Images Missing Alt | {(totalMissingAlt > 0 ? $"⚠️ {totalMissingAlt}" : "✅ 0")} |");
        var siteA11yTotal = site.Pages.Where(p => p.A11ySummary != null).Sum(p => p.A11ySummary!.TotalViolations);
        var siteA11yCrit = site.Pages.Where(p => p.A11ySummary != null).Sum(p => p.A11ySummary!.Critical);
        var siteA11ySeri = site.Pages.Where(p => p.A11ySummary != null).Sum(p => p.A11ySummary!.Serious);
        var siteA11yMod = site.Pages.Where(p => p.A11ySummary != null).Sum(p => p.A11ySummary!.Moderate);
        var siteA11yMin = site.Pages.Where(p => p.A11ySummary != null).Sum(p => p.A11ySummary!.Minor);
        sb.AppendLine($"| A11y Violations | {(siteA11yTotal > 0 ? $"⚠️ {siteA11yTotal}" : "✅ 0")} |");
        if (siteA11yTotal > 0)
        {
            sb.AppendLine($"| 🔴 Critical | {siteA11yCrit} |");
            sb.AppendLine($"| 🟠 Serious | {siteA11ySeri} |");
            sb.AppendLine($"| 🟡 Moderate | {siteA11yMod} |");
            sb.AppendLine($"| 🔵 Minor | {siteA11yMin} |");
        }
        sb.AppendLine($"| Total HTML | {PathSanitizer.FormatBytes(totalHtml)} |");
        sb.AppendLine($"| Total Screenshots | {PathSanitizer.FormatBytes(totalScreenshots)} |");
        sb.AppendLine();

        // Certificate section
        if (site.Certificate != null && site.Certificate.ErrorMessage == null)
        {
            var cert = site.Certificate;
            var expiryEmoji = cert.DaysUntilExpiry < 30 ? "🔴" : cert.DaysUntilExpiry < 90 ? "🟡" : "🟢";

            sb.AppendLine($"## 🔒 SSL Certificate");
            sb.AppendLine();
            sb.AppendLine($"| Field | Value |");
            sb.AppendLine($"|-------|-------|");
            sb.AppendLine($"| Subject | `{cert.Subject}` |");
            sb.AppendLine($"| Issuer | `{Truncate(cert.Issuer, 80)}` |");
            sb.AppendLine($"| Valid From | {cert.NotBefore:yyyy-MM-dd} |");
            sb.AppendLine($"| Expires | {expiryEmoji} {cert.NotAfter:yyyy-MM-dd} ({cert.DaysUntilExpiry} days) |");
            sb.AppendLine($"| Algorithm | {cert.SignatureAlgorithm} |");
            sb.AppendLine($"| Key Size | {cert.KeySizeBits} bits |");
            sb.AppendLine($"| Thumbprint | `{cert.Thumbprint}` |");
            sb.AppendLine($"| SANs | {cert.SubjectAlternativeNames.Count} domain(s) |");
            sb.AppendLine();

            if (cert.SubjectAlternativeNames.Count > 0)
            {
                sb.AppendLine("<details>");
                sb.AppendLine($"<summary><strong>Subject Alternative Names ({cert.SubjectAlternativeNames.Count})</strong></summary>");
                sb.AppendLine();
                sb.AppendLine("| Domain | Type |");
                sb.AppendLine("|--------|------|");
                foreach (var san in cert.SubjectAlternativeNames)
                {
                    var type = san.StartsWith("*.") ? "🌐 Wildcard" :
                               san.EndsWith(".wsu.edu", StringComparison.OrdinalIgnoreCase) ? "🏫 WSU" :
                               san.Equals("wsu.edu", StringComparison.OrdinalIgnoreCase) ? "🏫 WSU Root" :
                               "🔗 External";
                    sb.AppendLine($"| `{san}` | {type} |");
                }
                sb.AppendLine();
                sb.AppendLine("</details>");
                sb.AppendLine();
            }
        }

        // Page results table
        sb.AppendLine($"## 📑 Pages");
        sb.AppendLine();
        sb.AppendLine($"| Status | Page | HTTP | Title | 🔴 | 🟠 | 🟡 | 🔵 | A11y |");
        sb.AppendLine($"|:------:|------|:----:|-------|:--:|:--:|:--:|:--:|:----:|");

        foreach (var page in site.Pages.OrderBy(p => p.PagePath))
        {
            var s = page.Success ? "✅" : "❌";
            var title = Truncate(page.Title ?? "*(none)*", 40);
            var a = page.A11ySummary;
            var crit = a != null && a.Critical > 0 ? a.Critical.ToString() : "";
            var seri = a != null && a.Serious > 0 ? a.Serious.ToString() : "";
            var mod = a != null && a.Moderate > 0 ? a.Moderate.ToString() : "";
            var min = a != null && a.Minor > 0 ? a.Minor.ToString() : "";
            var total = a != null && a.TotalViolations > 0 ? $"⚠️ {a.TotalViolations}" : "✅";
            sb.AppendLine($"| {s} | [{page.PagePath}]({page.FolderName}/report.md) | {page.StatusCode} | {title} | {crit} | {seri} | {mod} | {min} | {total} |");
        }
        sb.AppendLine();

        // Screenshot gallery — 3-column HTML grid
        sb.AppendLine($"## 📸 Page Screenshots");
        sb.AppendLine();
        sb.AppendLine("Click any thumbnail to view the full page report.");
        sb.AppendLine();

        var pagesWithShots = site.Pages.Where(p => p.Screenshots.Count > 0).OrderBy(p => p.PagePath).ToList();
        if (pagesWithShots.Count > 0)
        {
            sb.AppendLine("<table>");
            for (int i = 0; i < pagesWithShots.Count; i += 3)
            {
                sb.AppendLine("<tr>");
                for (int j = 0; j < 3; j++)
                {
                    var idx = i + j;
                    if (idx < pagesWithShots.Count)
                    {
                        var page = pagesWithShots[idx];
                        var firstShot = page.Screenshots.First();
                        var statusIcon = page.Success ? "✅" : "❌";
                        sb.AppendLine("<td align=\"center\" width=\"33%\">");
                        sb.AppendLine($"<a href=\"{page.FolderName}/report.md\">");
                        sb.AppendLine($"<img src=\"{page.FolderName}/{firstShot.FileName}\" width=\"250\" alt=\"{page.PagePath}\" />");
                        sb.AppendLine("</a>");
                        sb.AppendLine($"<br />{statusIcon} <code>{page.PagePath}</code>");
                        sb.AppendLine("</td>");
                    }
                    else
                    {
                        sb.AppendLine("<td></td>");
                    }
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
        }
        else
        {
            sb.AppendLine("*No screenshots captured.*");
        }
        sb.AppendLine();

        // Failed pages — accordion
        if (failedPages.Count > 0)
        {
            sb.AppendLine($"## ❌ Failed Pages");
            sb.AppendLine();
            sb.AppendLine("<details open>");
            sb.AppendLine($"<summary><strong>{failedPages.Count} page(s) failed</strong></summary>");
            sb.AppendLine();
            sb.AppendLine("| Page | HTTP | Error |");
            sb.AppendLine("|------|:----:|-------|");

            foreach (var page in failedPages)
            {
                var err = Truncate(page.ErrorMessage ?? "—", 60);
                sb.AppendLine($"| [{page.PagePath}]({page.FolderName}/report.md) | {page.StatusCode} | {err} |");
            }
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        // JS errors — accordion
        if (pagesWithErrors.Count > 0)
        {
            sb.AppendLine($"## 🔴 JavaScript Errors");
            sb.AppendLine();
            sb.AppendLine("<details>");
            sb.AppendLine($"<summary><strong>{totalErrors} error(s) across {pagesWithErrors.Count} page(s)</strong></summary>");
            sb.AppendLine();

            foreach (var page in pagesWithErrors.OrderByDescending(p => p.ConsoleErrors.Count))
            {
                sb.AppendLine($"**{page.PagePath}** ({page.ConsoleErrors.Count} errors)");
                sb.AppendLine();
                sb.AppendLine("```");
                foreach (var error in page.ConsoleErrors.Take(5))
                {
                    sb.AppendLine(error.Length > 200 ? error[..200] + "..." : error);
                }
                if (page.ConsoleErrors.Count > 5)
                {
                    sb.AppendLine($"... and {page.ConsoleErrors.Count - 5} more (see {page.FolderName}/errors.log)");
                }
                sb.AppendLine("```");
                sb.AppendLine();
            }
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        // Accessibility rollup
        var pagesWithA11y = site.Pages.Where(p => p.A11ySummary != null && p.A11ySummary.TotalViolations > 0).ToList();
        if (pagesWithA11y.Count > 0)
        {
            var totalA11y = pagesWithA11y.Sum(p => p.A11ySummary!.TotalViolations);
            var totalCritical = pagesWithA11y.Sum(p => p.A11ySummary!.Critical);
            var totalSerious = pagesWithA11y.Sum(p => p.A11ySummary!.Serious);
            var totalModerate = pagesWithA11y.Sum(p => p.A11ySummary!.Moderate);
            var totalMinor = pagesWithA11y.Sum(p => p.A11ySummary!.Minor);

            sb.AppendLine($"## ♿ Accessibility Summary");
            sb.AppendLine();
            sb.AppendLine($"| Metric | Value |");
            sb.AppendLine($"|--------|-------|");
            sb.AppendLine($"| Pages with violations | {pagesWithA11y.Count}/{totalCount} |");
            sb.AppendLine($"| Total violations | {totalA11y} |");
            sb.AppendLine($"| 🔴 Critical | {totalCritical} |");
            sb.AppendLine($"| 🟠 Serious | {totalSerious} |");
            sb.AppendLine($"| 🟡 Moderate | {totalModerate} |");
            sb.AppendLine($"| 🔵 Minor | {totalMinor} |");
            sb.AppendLine();

            // Top 10 rules across this site
            var allRanked = pagesWithA11y
                .SelectMany(p => p.A11ySummary!.RankedRules)
                .GroupBy(r => r.CanonicalRuleId)
                .Select(g => new
                {
                    Rule = g.Key,
                    Severity = g.First().Severity,
                    Pages = g.Count(),
                    Instances = g.Sum(r => r.TotalInstances),
                    AvgConfidence = g.Average(r => r.ConfidenceScore),
                })
                .OrderByDescending(r => r.AvgConfidence)
                .ThenBy(r => SeverityRank(r.Severity))
                .ThenByDescending(r => r.Instances)
                .Take(10)
                .ToList();

            if (allRanked.Count > 0)
            {
                sb.AppendLine($"### Top {allRanked.Count} Issues");
                sb.AppendLine();
                sb.AppendLine($"| # | Rule | Sev | Pages | Instances |");
                sb.AppendLine($"|--:|------|:---:|:-----:|:---------:|");

                var rank = 0;
                foreach (var r in allRanked)
                {
                    rank++;
                    sb.AppendLine($"| {rank} | {RuleLink(r.Rule, "../a11y-rules.md")} | {SeverityEmoji(r.Severity)} | {r.Pages}/{totalCount} | {r.Instances} |");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Generated by AccessibilityScanner (FreeTools) v1.0*");

        await File.WriteAllTextAsync(Path.Combine(siteDir, "report.md"), sb.ToString());
    }

    // ========================================================================
    // Run-level report (root of runs/latest/)
    // ========================================================================

    private static async Task WriteRunReportAsync(string runsDir, IEnumerable<SiteResult> sites)
    {
        var siteList = sites.OrderBy(s => s.Url).ToList();
        var sb = new StringBuilder();

        var totalSites = siteList.Count;
        var totalPages = siteList.Sum(s => s.Pages.Count);
        var totalSuccess = siteList.Sum(s => s.Pages.Count(p => p.Success));
        var totalFailed = totalPages - totalSuccess;
        var totalErrors = siteList.Sum(s => s.Pages.Sum(p => p.ConsoleErrors.Count));
        var totalWarnings = siteList.Sum(s => s.Pages.Sum(p => p.ConsoleWarnings.Count));
        var totalHtml = siteList.Sum(s => s.Pages.Sum(p => p.HtmlSize));
        var totalScreenshots = siteList.Sum(s => s.Pages.Sum(p => p.ScreenshotSize));
        var totalImages = siteList.Sum(s => s.Pages.Sum(p => p.Images.Count));
        var totalMissingAlt = siteList.Sum(s => s.Pages.Sum(p => p.Images.Count(i => string.IsNullOrWhiteSpace(i.AltText))));
        var runA11yTotal = siteList.Sum(s => s.Pages.Where(p => p.A11ySummary != null).Sum(p => p.A11ySummary!.TotalViolations));
        var runA11yCrit = siteList.Sum(s => s.Pages.Where(p => p.A11ySummary != null).Sum(p => p.A11ySummary!.Critical));
        var runA11ySeri = siteList.Sum(s => s.Pages.Where(p => p.A11ySummary != null).Sum(p => p.A11ySummary!.Serious));
        var runA11yMod = siteList.Sum(s => s.Pages.Where(p => p.A11ySummary != null).Sum(p => p.A11ySummary!.Moderate));
        var runA11yMin = siteList.Sum(s => s.Pages.Where(p => p.A11ySummary != null).Sum(p => p.A11ySummary!.Minor));
        var successPct = totalPages > 0 ? (totalSuccess * 100.0 / totalPages) : 0;
        var runStatus = totalFailed == 0 ? "✅ All pages passed" : $"⚠️ {totalFailed} page(s) failed";

        sb.AppendLine($"# 📊 Accessibility Scanner — Run Report");
        sb.AppendLine();
        sb.AppendLine($"> **Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"> **Status:** {runStatus}  ");
        sb.AppendLine($"> **Sites:** {totalSites} | **Pages:** {totalPages}  ");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Table of Contents
        sb.AppendLine("## 📑 Contents");
        sb.AppendLine();
        sb.AppendLine("- [Dashboard](#-dashboard)");
        sb.AppendLine("- [Sites](#-sites)");
        sb.AppendLine("- [Screenshot Gallery](#-screenshot-gallery)");
        sb.AppendLine("- [All Pages](#-all-pages)");
        if (totalFailed > 0) sb.AppendLine("- [Failed Pages](#-failed-pages)");
        if (totalErrors > 0) sb.AppendLine("- [JavaScript Errors](#-javascript-errors)");
        sb.AppendLine("- [Accessibility Dashboard](#-accessibility-dashboard)");
        sb.AppendLine("- [📖 A11y Rules Reference](a11y-rules.md)");
        sb.AppendLine("- [SSL Certificates](#-ssl-certificates)");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Dashboard
        sb.AppendLine($"## 📋 Dashboard");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine($"Page Success:     {GenerateProgressBar(successPct, 30)} {successPct:F0}%");
        var altPct = totalImages > 0 ? ((totalImages - totalMissingAlt) * 100.0 / totalImages) : 100;
        sb.AppendLine($"Alt Text Cover:   {GenerateProgressBar(altPct, 30)} {altPct:F0}%");
        var a11yCleanPct = totalPages > 0
            ? (siteList.Sum(s => s.Pages.Count(p => p.A11ySummary == null || p.A11ySummary.TotalViolations == 0)) * 100.0 / totalPages)
            : 100;
        sb.AppendLine($"A11y Clean Pages: {GenerateProgressBar(a11yCleanPct, 30)} {a11yCleanPct:F0}%");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("| ✅ Passed | ❌ Failed | ⚠️ A11y Issues | 🔴 Critical | 🟠 Serious | 🟡 Moderate | 🔵 Minor |");
        sb.AppendLine("|:---------:|:---------:|:-----------:|:----------:|:--------:|:----------:|:------:|");
        sb.AppendLine($"| {totalSuccess} | {totalFailed} | {runA11yTotal} | {runA11yCrit} | {runA11ySeri} | {runA11yMod} | {runA11yMin} |");
        sb.AppendLine();

        sb.AppendLine($"| Metric | Value |");
        sb.AppendLine($"|--------|-------|");
        sb.AppendLine($"| Sites | {totalSites} |");
        sb.AppendLine($"| Total Pages | {totalPages} |");
        sb.AppendLine($"| Total Images | {totalImages} (by URL) |");
        sb.AppendLine($"| Total HTML | {PathSanitizer.FormatBytes(totalHtml)} |");
        sb.AppendLine($"| Total Screenshots | {PathSanitizer.FormatBytes(totalScreenshots)} |");
        sb.AppendLine($"| Total A11y Violations | {(runA11yTotal > 0 ? $"⚠️ {runA11yTotal:N0}" : "✅ 0")} |");
        sb.AppendLine($"| JS Errors | {(totalErrors > 0 ? $"🔴 {totalErrors}" : "0")} |");
        sb.AppendLine();

        // Sites table
        sb.AppendLine($"## 🌐 Sites");
        sb.AppendLine();
        sb.AppendLine($"| Status | Site | Pages | 🔴 | 🟠 | 🟡 | 🔵 | A11y Total |");
        sb.AppendLine($"|:------:|------|:-----:|:--:|:--:|:--:|:--:|:---------:|");

        foreach (var site in siteList)
        {
            var siteSuccess = site.Pages.Count(p => p.Success);
            var siteFailed = site.Pages.Count - siteSuccess;
            var sa = site.Pages.Where(p => p.A11ySummary != null);
            var saCrit = sa.Sum(p => p.A11ySummary!.Critical);
            var saSeri = sa.Sum(p => p.A11ySummary!.Serious);
            var saMod = sa.Sum(p => p.A11ySummary!.Moderate);
            var saMin = sa.Sum(p => p.A11ySummary!.Minor);
            var saTotal = sa.Sum(p => p.A11ySummary!.TotalViolations);
            var s = siteFailed == 0 ? "✅" : "⚠️";
            var totalBadge = saTotal > 0 ? $"⚠️ {saTotal}" : "✅";

            sb.AppendLine($"| {s} | [{site.Url}]({site.FolderName}/report.md) | {site.Pages.Count} | {(saCrit > 0 ? saCrit.ToString() : "")} | {(saSeri > 0 ? saSeri.ToString() : "")} | {(saMod > 0 ? saMod.ToString() : "")} | {(saMin > 0 ? saMin.ToString() : "")} | {totalBadge} |");
        }
        sb.AppendLine();

        // Screenshot gallery — grouped by site, HTML 3-column grid, in accordions
        sb.AppendLine($"## 📸 Screenshot Gallery");
        sb.AppendLine();
        sb.AppendLine($"**{totalPages} pages** across **{totalSites} sites**. Click any thumbnail to view the full page report.");
        sb.AppendLine();

        foreach (var site in siteList)
        {
            var host = new Uri(site.Url).Host;
            var pagesWithShots = site.Pages.Where(p => p.Screenshots.Count > 0).OrderBy(p => p.PagePath).ToList();
            if (pagesWithShots.Count == 0) continue;

            var siteSuccess = site.Pages.Count(p => p.Success);
            var siteFailed = site.Pages.Count - siteSuccess;
            var siteEmoji = siteFailed == 0 ? "✅" : "⚠️";

            sb.AppendLine("<details>");
            sb.AppendLine($"<summary><strong>{siteEmoji} {host}</strong> — {pagesWithShots.Count} page(s)</summary>");
            sb.AppendLine();
            sb.AppendLine("<table>");

            for (int i = 0; i < pagesWithShots.Count; i += 3)
            {
                sb.AppendLine("<tr>");
                for (int j = 0; j < 3; j++)
                {
                    var idx = i + j;
                    if (idx < pagesWithShots.Count)
                    {
                        var page = pagesWithShots[idx];
                        var firstShot = page.Screenshots.First();
                        var statusIcon = page.Success ? "✅" : "❌";
                        sb.AppendLine("<td align=\"center\" width=\"33%\">");
                        sb.AppendLine($"<a href=\"{site.FolderName}/{page.FolderName}/report.md\">");
                        sb.AppendLine($"<img src=\"{site.FolderName}/{page.FolderName}/{firstShot.FileName}\" width=\"250\" alt=\"{host}{page.PagePath}\" />");
                        sb.AppendLine("</a>");
                        sb.AppendLine($"<br />{statusIcon} <code>{page.PagePath}</code>");
                        sb.AppendLine("</td>");
                    }
                    else
                    {
                        sb.AppendLine("<td></td>");
                    }
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        // All pages flat table — in accordion because it can be huge
        sb.AppendLine($"## 📑 All Pages");
        sb.AppendLine();
        sb.AppendLine("<details>");
        sb.AppendLine($"<summary><strong>{totalPages} pages scanned</strong></summary>");
        sb.AppendLine();
        sb.AppendLine($"| Status | Site | Page | HTTP | 🔴 | 🟠 | 🟡 | 🔵 | A11y |");
        sb.AppendLine($"|:------:|------|------|:----:|:--:|:--:|:--:|:--:|:----:|");

        foreach (var site in siteList)
        {
            foreach (var page in site.Pages.OrderBy(p => p.PagePath))
            {
                var s = page.Success ? "✅" : "❌";
                var host = new Uri(site.Url).Host;
                var a = page.A11ySummary;
                var crit = a != null && a.Critical > 0 ? a.Critical.ToString() : "";
                var seri = a != null && a.Serious > 0 ? a.Serious.ToString() : "";
                var mod = a != null && a.Moderate > 0 ? a.Moderate.ToString() : "";
                var min = a != null && a.Minor > 0 ? a.Minor.ToString() : "";
                var total = a != null && a.TotalViolations > 0 ? $"⚠️ {a.TotalViolations}" : "✅";
                sb.AppendLine($"| {s} | {host} | [{page.PagePath}]({site.FolderName}/{page.FolderName}/report.md) | {page.StatusCode} | {crit} | {seri} | {mod} | {min} | {total} |");
            }
        }
        sb.AppendLine();
        sb.AppendLine("</details>");
        sb.AppendLine();

        // Failed pages — accordion
        var allFailed = siteList.SelectMany(s => s.Pages.Where(p => !p.Success)
            .Select(p => (Site: s, Page: p))).ToList();

        if (allFailed.Count > 0)
        {
            sb.AppendLine($"## ❌ Failed Pages");
            sb.AppendLine();
            sb.AppendLine("<details open>");
            sb.AppendLine($"<summary><strong>{allFailed.Count} page(s) failed</strong></summary>");
            sb.AppendLine();
            sb.AppendLine("| Site | Page | HTTP | Error |");
            sb.AppendLine("|------|------|:----:|-------|");

            foreach (var (site, page) in allFailed)
            {
                var host = new Uri(site.Url).Host;
                var err = Truncate(page.ErrorMessage ?? "—", 50);
                sb.AppendLine($"| {host} | {page.PagePath} | {page.StatusCode} | {err} |");
            }
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        // Top JS error pages — accordion
        var errorPages = siteList.SelectMany(s => s.Pages.Where(p => p.ConsoleErrors.Count > 0)
            .Select(p => (Site: s, Page: p)))
            .OrderByDescending(x => x.Page.ConsoleErrors.Count)
            .Take(15)
            .ToList();

        if (errorPages.Count > 0)
        {
            sb.AppendLine($"## 🔴 JavaScript Errors");
            sb.AppendLine();
            sb.AppendLine("<details>");
            sb.AppendLine($"<summary><strong>Top {errorPages.Count} pages by JS error count</strong></summary>");
            sb.AppendLine();
            sb.AppendLine($"| Errors | Site | Page |");
            sb.AppendLine($"|:------:|------|------|");

            foreach (var (site, page) in errorPages)
            {
                var host = new Uri(site.Url).Host;
                sb.AppendLine($"| 🔴 {page.ConsoleErrors.Count} | {host} | [{page.PagePath}]({site.FolderName}/{page.FolderName}/report.md) |");
            }
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        // Accessibility dashboard
        var allPagesWithA11y = siteList
            .SelectMany(s => s.Pages.Where(p => p.A11ySummary != null && p.A11ySummary.TotalViolations > 0)
                .Select(p => (Site: s, Page: p)))
            .ToList();

        if (allPagesWithA11y.Count > 0)
        {
            var grandA11yTotal = allPagesWithA11y.Sum(x => x.Page.A11ySummary!.TotalViolations);
            var grandCritical = allPagesWithA11y.Sum(x => x.Page.A11ySummary!.Critical);
            var grandSerious = allPagesWithA11y.Sum(x => x.Page.A11ySummary!.Serious);
            var grandModerate = allPagesWithA11y.Sum(x => x.Page.A11ySummary!.Moderate);
            var grandMinor = allPagesWithA11y.Sum(x => x.Page.A11ySummary!.Minor);

            sb.AppendLine($"## ♿ Accessibility Dashboard");
            sb.AppendLine();

            // Progress bars
            var critPct = grandA11yTotal > 0 ? grandCritical * 100.0 / grandA11yTotal : 0;
            var seriPct = grandA11yTotal > 0 ? grandSerious * 100.0 / grandA11yTotal : 0;
            var modPct = grandA11yTotal > 0 ? grandModerate * 100.0 / grandA11yTotal : 0;
            var minPct = grandA11yTotal > 0 ? grandMinor * 100.0 / grandA11yTotal : 0;

            sb.AppendLine("```");
            sb.AppendLine($"Critical:     {GenerateProgressBar(critPct, 30)} {critPct:F0}%");
            sb.AppendLine($"Serious:      {GenerateProgressBar(seriPct, 30)} {seriPct:F0}%");
            sb.AppendLine($"Moderate:     {GenerateProgressBar(modPct, 30)} {modPct:F0}%");
            sb.AppendLine($"Minor:        {GenerateProgressBar(minPct, 30)} {minPct:F0}%");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine($"| 🔴 Critical | 🟠 Serious | 🟡 Moderate | 🔵 Minor | Total |");
            sb.AppendLine($"|:-----------:|:----------:|:-----------:|:--------:|:-----:|");
            sb.AppendLine($"| {grandCritical} | {grandSerious} | {grandModerate} | {grandMinor} | {grandA11yTotal} |");
            sb.AppendLine();

            sb.AppendLine($"| Metric | Value |");
            sb.AppendLine($"|--------|-------|");
            sb.AppendLine($"| Pages with violations | {allPagesWithA11y.Count}/{totalPages} |");
            var sitesWithA11y = siteList.Count(s => s.Pages.Any(p => p.A11ySummary != null && p.A11ySummary.TotalViolations > 0));
            sb.AppendLine($"| Sites with violations | {sitesWithA11y}/{totalSites} |");
            sb.AppendLine();

            // Top 20 violations across all sites
            var allRunRanked = allPagesWithA11y
                .SelectMany(x => x.Page.A11ySummary!.RankedRules
                    .Select(r => (x.Site, x.Page, Rule: r)))
                .GroupBy(x => x.Rule.CanonicalRuleId)
                .Select(g => new
                {
                    Rule = g.Key,
                    Severity = g.First().Rule.Severity,
                    Sites = g.Select(x => x.Site.Url).Distinct().Count(),
                    Pages = g.Count(),
                    Instances = g.Sum(x => x.Rule.TotalInstances),
                    AvgConfidence = g.Average(x => x.Rule.ConfidenceScore),
                    WcagCriteria = g.First().Rule.WcagCriteria,
                    Message = g.First().Rule.Message
                })
                .OrderByDescending(r => r.AvgConfidence)
                .ThenBy(r => SeverityRank(r.Severity))
                .ThenByDescending(r => r.Instances)
                .Take(20)
                .ToList();

            if (allRunRanked.Count > 0)
            {
                sb.AppendLine($"### Top {allRunRanked.Count} Violations (all sites)");
                sb.AppendLine();
                sb.AppendLine($"| # | Rule | Sev | Sites | Pages | Instances | WCAG |");
                sb.AppendLine($"|--:|------|:---:|:-----:|:-----:|:---------:|:----:|");

                var rank = 0;
                foreach (var r in allRunRanked)
                {
                    rank++;
                    sb.AppendLine($"| {rank} | {RuleLink(r.Rule)} | {SeverityEmoji(r.Severity)} | {r.Sites}/{totalSites} | {r.Pages}/{totalPages} | {r.Instances:N0} | {r.WcagCriteria ?? "—"} |");
                }
                sb.AppendLine();

                // Write CSV
                var csvSb = new StringBuilder();
                csvSb.AppendLine("Rank,Rule,Severity,Confidence,Sites,Pages,Instances,WCAG,Message");
                rank = 0;
                foreach (var r in allRunRanked)
                {
                    rank++;
                    var conf = r.AvgConfidence >= 0.8 ? "high" : r.AvgConfidence >= 0.5 ? "medium" : "low";
                    var msg = r.Message.Replace("\"", "\"\"");
                    csvSb.AppendLine($"{rank},\"{r.Rule}\",\"{r.Severity}\",\"{conf}\",{r.Sites},{r.Pages},{r.Instances},\"{r.WcagCriteria ?? ""}\",\"{msg}\"");
                }
                await File.WriteAllTextAsync(Path.Combine(runsDir, "a11y-ranked.csv"), csvSb.ToString());
            }
        }

        // Certificate summary
        var sitesWithCerts = siteList.Where(s => s.Certificate != null && s.Certificate.ErrorMessage == null).ToList();
        if (sitesWithCerts.Count > 0)
        {
            sb.AppendLine($"## 🔒 SSL Certificates");
            sb.AppendLine();

            // Expiry warnings
            var expiringSoon = sitesWithCerts.Where(s => s.Certificate!.DaysUntilExpiry < 90)
                .OrderBy(s => s.Certificate!.DaysUntilExpiry).ToList();

            if (expiringSoon.Count > 0)
            {
                sb.AppendLine($"### ⚠️ Certificates Expiring Soon");
                sb.AppendLine();
                sb.AppendLine($"| Site | Expires | Days Left | Thumbprint |");
                sb.AppendLine($"|------|---------|:---------:|------------|");
                foreach (var s in expiringSoon)
                {
                    var c = s.Certificate!;
                    var emoji = c.DaysUntilExpiry < 30 ? "🔴" : "🟡";
                    var host = new Uri(s.Url).Host;
                    sb.AppendLine($"| {host} | {c.NotAfter:yyyy-MM-dd} | {emoji} {c.DaysUntilExpiry} | `{c.Thumbprint[..8]}...` |");
                }
                sb.AppendLine();
            }

            // Unique certs by thumbprint
            var uniqueCerts = sitesWithCerts
                .GroupBy(s => s.Certificate!.Thumbprint)
                .Select(g => new
                {
                    Thumbprint = g.Key,
                    Cert = g.First().Certificate!,
                    Sites = g.Select(s => new Uri(s.Url).Host).OrderBy(h => h).ToList()
                })
                .OrderByDescending(c => c.Sites.Count)
                .ToList();

            sb.AppendLine($"### Unique Certificates ({uniqueCerts.Count})");
            sb.AppendLine();
            sb.AppendLine("<details>");
            sb.AppendLine($"<summary><strong>{uniqueCerts.Count} unique cert(s) across {sitesWithCerts.Count} sites</strong></summary>");
            sb.AppendLine();
            sb.AppendLine($"| Thumbprint | Subject | Sites Using | SANs | Expires |");
            sb.AppendLine($"|------------|---------|:-----------:|:----:|---------|");
            foreach (var uc in uniqueCerts)
            {
                var subj = Truncate(uc.Cert.Subject, 40);
                sb.AppendLine($"| `{uc.Thumbprint[..8]}...` | {subj} | {uc.Sites.Count} | {uc.Cert.SubjectAlternativeNames.Count} | {uc.Cert.NotAfter:yyyy-MM-dd} |");
            }
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();

            // SAN-discovered domains not in our config
            var configuredHosts = new HashSet<string>(
                siteList.Select(s => new Uri(s.Url).Host), StringComparer.OrdinalIgnoreCase);

            var discoveredFromSans = sitesWithCerts
                .SelectMany(s => s.Certificate!.SubjectAlternativeNames)
                .Where(san => !san.StartsWith("*.")) // Skip wildcards
                .Select(san => san.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(san => !configuredHosts.Contains(san))
                .OrderBy(s => s)
                .ToList();

            if (discoveredFromSans.Count > 0)
            {
                sb.AppendLine($"### 🔍 Domains Discovered via SANs (not in config)");
                sb.AppendLine();
                sb.AppendLine($"These {discoveredFromSans.Count} domain(s) appear in SSL certificates but are not in the scan config:");
                sb.AppendLine();
                sb.AppendLine("<details>");
                sb.AppendLine($"<summary><strong>{discoveredFromSans.Count} discoverable domain(s)</strong></summary>");
                sb.AppendLine();
                foreach (var domain in discoveredFromSans)
                {
                    sb.AppendLine($"- `{domain}`");
                }
                sb.AppendLine();
                sb.AppendLine("</details>");
                sb.AppendLine();

                // Write a CSV of discovered domains
                var sanCsvSb = new StringBuilder();
                sanCsvSb.AppendLine("Domain,FoundInCertFor,CertSubject");
                foreach (var domain in discoveredFromSans)
                {
                    var source = sitesWithCerts.FirstOrDefault(s =>
                        s.Certificate!.SubjectAlternativeNames.Contains(domain, StringComparer.OrdinalIgnoreCase));
                    if (source != null)
                    {
                        var host = new Uri(source.Url).Host;
                        var subj = source.Certificate!.Subject.Replace("\"", "\"\"");
                        sanCsvSb.AppendLine($"\"{domain}\",\"{host}\",\"{subj}\"");
                    }
                }
                await File.WriteAllTextAsync(Path.Combine(runsDir, "cert-discovered-domains.csv"), sanCsvSb.ToString());
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Generated by AccessibilityScanner (FreeTools) v1.0*");
        sb.AppendLine();
        sb.AppendLine("**[FreeTools](https://github.com/WSU-EIT/FreeTools)** — Open source accessibility scanning tools for .NET projects");

        await File.WriteAllTextAsync(Path.Combine(runsDir, "report.md"), sb.ToString());
    }

    // ========================================================================
    // A11y Rules Legend / Index (a11y-rules.md)
    // ========================================================================

    /// <summary>
    /// Built-in knowledge base of accessibility rules.
    /// Each entry provides human-readable explanation, WCAG criteria, severity,
    /// and links to official documentation.
    /// </summary>
    private static readonly Dictionary<string, RuleInfo> RuleKnowledgeBase = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image-alt"] = new("Images must have alternate text", "serious",
            "Every `<img>` element must have an `alt` attribute that describes its content. Decorative images should use `alt=\"\"`.",
            "1.1.1", "https://dequeuniversity.com/rules/axe/4.10/image-alt",
            "Add a descriptive `alt` attribute. For decorative images, use `alt=\"\"`. For complex images, consider `aria-describedby` linking to a longer description."),

        ["input-image-alt"] = new("Image buttons must have alternate text", "serious",
            "`<input type=\"image\">` elements must have an `alt` attribute describing the button action.",
            "1.1.1", "https://dequeuniversity.com/rules/axe/4.10/input-image-alt",
            "Add `alt=\"Submit\"` or similar action description to the image input."),

        ["label"] = new("Form elements must have labels", "serious",
            "Every form input (`<input>`, `<select>`, `<textarea>`) must have a programmatically associated label via `<label for=\"id\">`, `aria-label`, or `aria-labelledby`.",
            "1.3.1", "https://dequeuniversity.com/rules/axe/4.10/label",
            "Add a `<label for=\"inputId\">` element, or add `aria-label=\"Description\"` to the input."),

        ["select-name"] = new("Select elements must have accessible names", "serious",
            "`<select>` elements must have an associated `<label>`, `aria-label`, or `aria-labelledby`.",
            "1.3.1", "https://dequeuniversity.com/rules/axe/4.10/select-name",
            "Add a `<label for=\"selectId\">` or `aria-label` attribute."),

        ["heading-order"] = new("Heading levels should increase by one", "moderate",
            "Headings should not skip levels (e.g., `<h2>` to `<h4>`). Screen readers use heading hierarchy to understand page structure.",
            "1.3.1", "https://dequeuniversity.com/rules/axe/4.10/heading-order",
            "Restructure headings so levels increase sequentially: h1 → h2 → h3, etc."),

        ["page-has-heading-one"] = new("Page should contain a level-one heading", "moderate",
            "Pages should have at least one `<h1>` element. The h1 typically matches the page title and helps screen reader users orient.",
            "1.3.1", "https://dequeuniversity.com/rules/axe/4.10/page-has-heading-one",
            "Add a single `<h1>` element that describes the main content of the page."),

        ["td-has-header"] = new("Data table cells must have headers", "moderate",
            "Non-empty `<td>` elements in a data table must have an associated `<th>` header, either via row/column position or explicit `headers` attribute.",
            "1.3.1", "https://dequeuniversity.com/rules/axe/4.10/td-has-header",
            "Add `<th>` elements in the first row or column. For complex tables, use `headers` and `id` attributes."),

        ["color-contrast"] = new("Elements must have sufficient color contrast", "serious",
            "Text must have a contrast ratio of at least 4.5:1 for normal text and 3:1 for large text against its background.",
            "1.4.3", "https://dequeuniversity.com/rules/axe/4.10/color-contrast",
            "Increase the contrast ratio by darkening text or lightening the background (or vice versa). Use a contrast checker tool."),

        ["color-contrast-enhanced"] = new("Elements must have enhanced color contrast", "moderate",
            "For WCAG AAA, text must have a contrast ratio of at least 7:1 for normal text and 4.5:1 for large text.",
            "1.4.6", "https://dequeuniversity.com/rules/axe/4.10/color-contrast-enhanced",
            "Same as color-contrast but with stricter thresholds."),

        ["meta-refresh"] = new("Page must not use meta refresh", "moderate",
            "`<meta http-equiv=\"refresh\">` can disorient users, especially those using screen readers. Use server-side redirects instead.",
            "2.2.1", "https://dequeuniversity.com/rules/axe/4.10/meta-refresh",
            "Remove the meta refresh tag and use HTTP 301/302 redirects on the server."),

        ["skip-link"] = new("Page should have a skip navigation link", "moderate",
            "A \"Skip to main content\" link at the top of the page allows keyboard users to bypass repetitive navigation.",
            "2.4.1", "https://dequeuniversity.com/rules/axe/4.10/skip-link",
            "Add `<a href=\"#main-content\" class=\"skip-link\">Skip to main content</a>` as the first focusable element in the body."),

        ["document-title"] = new("Document must have a title", "serious",
            "Every page must have a non-empty `<title>` element. The title is the first thing announced by screen readers.",
            "2.4.2", "https://dequeuniversity.com/rules/axe/4.10/document-title",
            "Add a descriptive `<title>` element inside `<head>`."),

        ["tabindex"] = new("Positive tabindex disrupts tab order", "moderate",
            "`tabindex` values greater than 0 create a custom tab order that is confusing. Use `tabindex=\"0\"` or `tabindex=\"-1\"` instead.",
            "2.4.3", "https://dequeuniversity.com/rules/axe/4.10/tabindex",
            "Remove positive tabindex values. Rearrange DOM order to match desired tab sequence."),

        ["link-name"] = new("Links must have discernible text", "serious",
            "Every `<a>` element must have text content, an `aria-label`, or contain an `<img>` with alt text so screen readers can announce the link purpose.",
            "2.4.4", "https://dequeuniversity.com/rules/axe/4.10/link-name",
            "Add descriptive text inside the link, or add `aria-label=\"Description\"`."),

        ["html-has-lang"] = new("HTML element must have a lang attribute", "serious",
            "The `<html>` element must have a `lang` attribute (e.g., `lang=\"en\"`) so screen readers use the correct pronunciation.",
            "3.1.1", "https://dequeuniversity.com/rules/axe/4.10/html-has-lang",
            "Add `lang=\"en\"` (or appropriate language code) to the `<html>` element."),

        ["html-lang-valid"] = new("HTML lang attribute must be valid", "serious",
            "The `lang` attribute value must be a valid BCP 47 language tag (e.g., `en`, `en-US`, `fr`).",
            "3.1.1", "https://dequeuniversity.com/rules/axe/4.10/html-lang-valid",
            "Set `lang` to a valid BCP 47 code like `en` or `en-US`."),

        ["button-name"] = new("Buttons must have discernible text", "serious",
            "Every `<button>` element must have text content, `aria-label`, or `aria-labelledby` so screen readers can announce it.",
            "4.1.2", "https://dequeuniversity.com/rules/axe/4.10/button-name",
            "Add text inside the button, or add `aria-label=\"Action description\"`."),

        ["div-button"] = new("Interactive divs should be buttons", "moderate",
            "`<div>` elements with `onclick` handlers but no ARIA role are not keyboard-accessible. Use `<button>` instead.",
            "4.1.2", "https://dequeuniversity.com/rules/axe/4.10/button-name",
            "Replace `<div onclick=\"...\">` with `<button>`. If you must use a div, add `role=\"button\"`, `tabindex=\"0\"`, and keyboard event handlers."),

        ["landmark-one-main"] = new("Page should have one main landmark", "moderate",
            "Pages should have exactly one `<main>` landmark (or `role=\"main\"`) so screen reader users can quickly jump to the primary content.",
            "1.3.1", "https://dequeuniversity.com/rules/axe/4.10/landmark-one-main",
            "Wrap your primary content in a `<main>` element."),

        ["landmark-nav"] = new("Page should have a navigation landmark", "minor",
            "Navigation sections should be wrapped in `<nav>` elements (or `role=\"navigation\"`) so screen readers can identify them.",
            "1.3.1", "https://dequeuniversity.com/rules/axe/4.10/region",
            "Wrap navigation links in a `<nav>` element."),

        ["aria-allowed-attr"] = new("ARIA attributes must be allowed for the role", "serious",
            "ARIA attributes used on an element must be valid for that element's role.",
            "4.1.2", "https://dequeuniversity.com/rules/axe/4.10/aria-allowed-attr",
            "Check the WAI-ARIA spec for which attributes are allowed on each role."),

        ["aria-valid-attr-value"] = new("ARIA attributes must have valid values", "serious",
            "ARIA attribute values must conform to the spec (e.g., `aria-hidden` must be `true` or `false`).",
            "4.1.2", "https://dequeuniversity.com/rules/axe/4.10/aria-valid-attr-value",
            "Correct the ARIA attribute value to match the specification."),

        ["aria-required-children"] = new("ARIA roles must contain required children", "serious",
            "Certain ARIA roles require specific child roles (e.g., `role=\"list\"` must contain `role=\"listitem\"`).",
            "1.3.1", "https://dequeuniversity.com/rules/axe/4.10/aria-required-children",
            "Add the required child elements/roles as specified by the ARIA spec."),

        ["fieldset"] = new("Related form fields should be grouped with fieldset", "moderate",
            "Groups of related checkboxes or radio buttons should be wrapped in `<fieldset>` with a `<legend>`.",
            "1.3.1", "https://dequeuniversity.com/rules/axe/4.10/fieldset",
            "Wrap related inputs in `<fieldset>` and add a `<legend>` describing the group."),

        ["table-fake-caption"] = new("Tables should use caption instead of cells for titles", "moderate",
            "Data tables should use `<caption>` for the table title rather than a merged row of cells.",
            "1.3.1", "https://dequeuniversity.com/rules/axe/4.10/table-fake-caption",
            "Replace title rows with a `<caption>` element inside the `<table>`."),

        ["blink"] = new("Blinking content must not be used", "serious",
            "The `<blink>` element causes content to flash, which can trigger seizures and is inaccessible.",
            "2.2.2", "https://dequeuniversity.com/rules/axe/4.10/blink",
            "Remove all `<blink>` elements. Use CSS animations with `prefers-reduced-motion` support if animation is needed."),

        ["marquee"] = new("Marquee elements must not be used", "serious",
            "The `<marquee>` element causes content to scroll automatically, which is disorienting and inaccessible.",
            "2.2.2", "https://dequeuniversity.com/rules/axe/4.10/marquee",
            "Remove `<marquee>` elements. Use CSS animations with pause controls if scrolling content is needed."),
    };

    /// <summary>
    /// Generate the a11y-rules.md legend document at the run root.
    /// Collects all unique rules found across all scanned pages and combines
    /// them with the built-in knowledge base.
    /// </summary>
    private static async Task WriteRulesLegendAsync(string runsDir, IEnumerable<SiteResult> sites)
    {
        var siteList = sites.ToList();

        // Collect all unique rules found in this run
        var foundRules = new Dictionary<string, FoundRuleStats>(StringComparer.OrdinalIgnoreCase);

        foreach (var site in siteList)
        {
            foreach (var page in site.Pages)
            {
                if (page.A11ySummary == null) continue;

                foreach (var rule in page.A11ySummary.RankedRules)
                {
                    if (!foundRules.TryGetValue(rule.CanonicalRuleId, out var stats))
                    {
                        stats = new FoundRuleStats
                        {
                            CanonicalRuleId = rule.CanonicalRuleId,
                            Severity = rule.Severity,
                            Message = rule.Message,
                            HelpUrl = rule.HelpUrl,
                            WcagCriteria = rule.WcagCriteria,
                            ExampleSnippet = rule.ExampleSnippet
                        };
                        foundRules[rule.CanonicalRuleId] = stats;
                    }

                    stats.TotalInstances += rule.TotalInstances;
                    stats.PageCount++;
                    stats.SiteUrls.Add(site.Url);

                    foreach (var tool in rule.ToolsFound)
                        stats.ToolsFound.Add(tool);
                }
            }
        }

        var sb = new StringBuilder();

        sb.AppendLine("# ♿ Accessibility Rules Reference");
        sb.AppendLine();
        sb.AppendLine("> A complete reference of all accessibility rules checked by this scanner.");
        sb.AppendLine("> Rules detected in this scan run are marked with instance counts.");
        sb.AppendLine("> Click any rule name to jump to its detailed explanation.");
        sb.AppendLine();
        sb.AppendLine($"> **Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"> **Rules in this scan:** {foundRules.Count}  ");
        sb.AppendLine($"> **Total rules documented:** {RuleKnowledgeBase.Count}  ");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Severity legend
        sb.AppendLine("## 🎨 Severity Levels");
        sb.AppendLine();
        sb.AppendLine("| Icon | Level | Meaning |");
        sb.AppendLine("|:----:|-------|---------|");
        sb.AppendLine("| 🔴 | **Critical** | Blocks access entirely for some users. Must fix immediately. |");
        sb.AppendLine("| 🟠 | **Serious** | Causes significant difficulty. Should fix as high priority. |");
        sb.AppendLine("| 🟡 | **Moderate** | Causes some difficulty. Fix as part of regular maintenance. |");
        sb.AppendLine("| 🔵 | **Minor** | Annoying but doesn't block access. Fix when possible. |");
        sb.AppendLine();

        // Tool legend
        sb.AppendLine("## 🔧 Tools");
        sb.AppendLine();
        sb.AppendLine("| Tool | Description |");
        sb.AppendLine("|------|-------------|");
        sb.AppendLine("| **axe** | [axe-core](https://github.com/dequelabs/axe-core) by Deque — industry-standard automated engine injected via Playwright |");
        sb.AppendLine("| **htmlcheck** | Built-in HTML pattern scanner — regex-based structural checks (no browser needed) |");
        sb.AppendLine("| **wave** | [WAVE API](https://wave.webaim.org/api/) by WebAIM — remote accessibility evaluation service |");
        sb.AppendLine();

        // Confidence legend
        sb.AppendLine("## 📊 Confidence Scoring");
        sb.AppendLine();
        sb.AppendLine("When multiple tools check the same rule, confidence increases:");
        sb.AppendLine();
        sb.AppendLine("| Icon | Confidence | Meaning |");
        sb.AppendLine("|:----:|:----------:|---------|");
        sb.AppendLine("| 🟢 | **High** | 2+ tools agree this is a real issue (≥80% of capable tools) |");
        sb.AppendLine("| 🟡 | **Medium** | Some tools found it, others didn't (50-79%) |");
        sb.AppendLine("| 🔵 | **Low** | Only one tool flagged this — may be a false positive (<50%) |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Quick-reference table of ALL rules
        sb.AppendLine("## 📋 Quick Reference");
        sb.AppendLine();
        sb.AppendLine("| Rule | Sev | WCAG | Found | Instances | Description |");
        sb.AppendLine("|------|:---:|:----:|:-----:|:---------:|-------------|");

        // Merge found rules with knowledge base for a complete list
        var allRuleIds = new HashSet<string>(RuleKnowledgeBase.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var id in foundRules.Keys)
            allRuleIds.Add(id);

        var sortedRules = allRuleIds
            .OrderBy(id =>
            {
                if (foundRules.ContainsKey(id)) return 0; // Found rules first
                return 1;
            })
            .ThenBy(id =>
            {
                var sev = RuleKnowledgeBase.TryGetValue(id, out var info) ? info.DefaultSeverity
                    : foundRules.TryGetValue(id, out var fs) ? fs.Severity : "moderate";
                return SeverityRank(sev);
            })
            .ThenBy(id => id)
            .ToList();

        foreach (var ruleId in sortedRules)
        {
            var kb = RuleKnowledgeBase.TryGetValue(ruleId, out var kbInfo) ? kbInfo : null;
            var found = foundRules.TryGetValue(ruleId, out var stats) ? stats : null;

            var sev = found?.Severity ?? kb?.DefaultSeverity ?? "moderate";
            var wcag = found?.WcagCriteria ?? kb?.WcagCriteria ?? "—";
            var desc = kb?.ShortDescription ?? found?.Message ?? "";
            var instances = found != null ? found.TotalInstances.ToString("N0") : "—";
            var foundBadge = found != null ? $"⚠️ {found.PageCount} pg" : "—";
            var anchor = ruleId.ToLowerInvariant().Replace(".", "");

            sb.AppendLine($"| [{ruleId}](#{anchor}) | {SeverityEmoji(sev)} | {wcag} | {foundBadge} | {instances} | {Truncate(desc, 60)} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Detailed sections for each rule
        sb.AppendLine("## 📖 Rule Details");
        sb.AppendLine();

        foreach (var ruleId in sortedRules)
        {
            var kb = RuleKnowledgeBase.TryGetValue(ruleId, out var kbInfo) ? kbInfo : null;
            var found = foundRules.TryGetValue(ruleId, out var stats) ? stats : null;

            var sev = found?.Severity ?? kb?.DefaultSeverity ?? "moderate";
            var wcag = found?.WcagCriteria ?? kb?.WcagCriteria;
            var anchor = ruleId.ToLowerInvariant().Replace(".", "");

            sb.AppendLine($"### {SeverityEmoji(sev)} `{ruleId}` {{#{anchor}}}");
            sb.AppendLine();

            // Title
            var title = kb?.ShortDescription ?? found?.Message ?? ruleId;
            sb.AppendLine($"**{title}**");
            sb.AppendLine();

            // Metadata table
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|-------|-------|");
            sb.AppendLine($"| Severity | {SeverityEmoji(sev)} **{sev}** |");
            if (wcag != null)
                sb.AppendLine($"| WCAG | [{wcag}](https://www.w3.org/WAI/WCAG21/Understanding/{FormatWcagUrl(wcag)}) |");

            if (found != null)
            {
                sb.AppendLine($"| Instances in scan | **{found.TotalInstances:N0}** |");
                sb.AppendLine($"| Pages affected | {found.PageCount} |");
                sb.AppendLine($"| Sites affected | {found.SiteUrls.Count} |");
                sb.AppendLine($"| Tools detecting | {string.Join(", ", found.ToolsFound.OrderBy(t => t))} |");
            }
            else
            {
                sb.AppendLine($"| Status in scan | ✅ Not detected |");
            }

            // Links
            var helpUrl = found?.HelpUrl ?? kb?.HelpUrl;
            if (helpUrl != null)
                sb.AppendLine($"| Learn more | [{helpUrl}]({helpUrl}) |");

            sb.AppendLine();

            // Explanation
            if (kb?.Explanation != null)
            {
                sb.AppendLine("**What this means:**");
                sb.AppendLine();
                sb.AppendLine($"> {kb.Explanation}");
                sb.AppendLine();
            }

            // How to fix
            if (kb?.HowToFix != null)
            {
                sb.AppendLine("**How to fix:**");
                sb.AppendLine();
                sb.AppendLine($"> {kb.HowToFix}");
                sb.AppendLine();
            }

            // Example snippet from scan
            if (found?.ExampleSnippet != null)
            {
                sb.AppendLine("**Example from scan:**");
                sb.AppendLine();
                sb.AppendLine("```html");
                sb.AppendLine(found.ExampleSnippet.Length > 200
                    ? found.ExampleSnippet[..200] + "..."
                    : found.ExampleSnippet);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        // WCAG reference section
        sb.AppendLine("## 📚 WCAG Quick Reference");
        sb.AppendLine();
        sb.AppendLine("The rules above map to [WCAG 2.1 Level AA](https://www.w3.org/TR/WCAG21/) success criteria:");
        sb.AppendLine();
        sb.AppendLine("| WCAG | Principle | Guideline |");
        sb.AppendLine("|:----:|-----------|-----------|");
        sb.AppendLine("| 1.1.1 | Perceivable | Non-text Content — provide text alternatives |");
        sb.AppendLine("| 1.3.1 | Perceivable | Info and Relationships — structure and relationships conveyed programmatically |");
        sb.AppendLine("| 1.4.3 | Perceivable | Contrast (Minimum) — at least 4.5:1 ratio |");
        sb.AppendLine("| 1.4.6 | Perceivable | Contrast (Enhanced) — at least 7:1 ratio (AAA) |");
        sb.AppendLine("| 2.2.1 | Operable | Timing Adjustable — users can control time limits |");
        sb.AppendLine("| 2.2.2 | Operable | Pause, Stop, Hide — moving content can be controlled |");
        sb.AppendLine("| 2.4.1 | Operable | Bypass Blocks — skip repetitive content |");
        sb.AppendLine("| 2.4.2 | Operable | Page Titled — descriptive page titles |");
        sb.AppendLine("| 2.4.3 | Operable | Focus Order — logical tab sequence |");
        sb.AppendLine("| 2.4.4 | Operable | Link Purpose — link text describes destination |");
        sb.AppendLine("| 3.1.1 | Understandable | Language of Page — lang attribute on html |");
        sb.AppendLine("| 4.1.2 | Robust | Name, Role, Value — UI components have accessible names |");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Generated by AccessibilityScanner (FreeTools) v1.0*");

        await File.WriteAllTextAsync(Path.Combine(runsDir, "a11y-rules.md"), sb.ToString());
    }

    /// <summary>Format WCAG criteria "1.4.3" → URL segment "contrast-minimum"</summary>
    private static string FormatWcagUrl(string criteria)
    {
        // Map common criteria to their Understanding doc slugs
        return criteria switch
        {
            "1.1.1" => "non-text-content",
            "1.3.1" => "info-and-relationships",
            "1.4.3" => "contrast-minimum",
            "1.4.6" => "contrast-enhanced",
            "2.2.1" => "timing-adjustable",
            "2.2.2" => "pause-stop-hide",
            "2.4.1" => "bypass-blocks",
            "2.4.2" => "page-titled",
            "2.4.3" => "focus-order",
            "2.4.4" => "link-purpose-in-context",
            "3.1.1" => "language-of-page",
            "4.1.2" => "name-role-value",
            _ => criteria.Replace(".", "")
        };
    }

    /// <summary>Rule knowledge base entry.</summary>
    private record RuleInfo(
        string ShortDescription,
        string DefaultSeverity,
        string Explanation,
        string? WcagCriteria,
        string? HelpUrl,
        string? HowToFix);

    /// <summary>Stats for a rule found during a scan run.</summary>
    private class FoundRuleStats
    {
        public string CanonicalRuleId { get; set; } = "";
        public string Severity { get; set; } = "moderate";
        public string Message { get; set; } = "";
        public string? HelpUrl { get; set; }
        public string? WcagCriteria { get; set; }
        public string? ExampleSnippet { get; set; }
        public int TotalInstances { get; set; }
        public int PageCount { get; set; }
        public HashSet<string> SiteUrls { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ToolsFound { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Format a rule ID as a clickable markdown link to the a11y-rules.md legend.
    /// </summary>
    /// <param name="ruleId">Canonical rule ID (e.g., "image-alt")</param>
    /// <param name="legendRelPath">Relative path from the current .md file to a11y-rules.md</param>
    private static string RuleLink(string ruleId, string legendRelPath = "a11y-rules.md")
    {
        var anchor = ruleId.ToLowerInvariant().Replace(".", "");
        return $"[{ruleId}]({legendRelPath}#{anchor})";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        // Escape pipe characters for markdown tables
        value = value.Replace("|", "\\|");
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }

    private static string GenerateProgressBar(double percentage, int width)
    {
        var filled = (int)(percentage * width / 100.0);
        filled = Math.Clamp(filled, 0, width);
        var empty = width - filled;
        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }

    // ========================================================================
    // Accessibility scanning
    // ========================================================================

    private const string AxeCdnUrl = "https://cdn.jsdelivr.net/npm/axe-core@4.10.2/axe.min.js";
    private static string? _axeScript;
    private static readonly SemaphoreSlim _axeLock = new(1, 1);

    /// <summary>
    /// Download axe-core.min.js from CDN on first use, cache in project directory.
    /// Thread-safe — only one download happens even with concurrent calls.
    /// </summary>
    private static async Task<string> EnsureAxeCoreAsync(string projectDir)
    {
        if (_axeScript != null) return _axeScript;

        await _axeLock.WaitAsync();
        try
        {
            if (_axeScript != null) return _axeScript;

            var cachePath = Path.Combine(projectDir, "axe.min.js");

            if (File.Exists(cachePath))
            {
                _axeScript = await File.ReadAllTextAsync(cachePath);
                Console.WriteLine($"  [axe-core] Cached ({_axeScript.Length / 1024}KB)");
                return _axeScript;
            }

            Console.WriteLine("  [axe-core] Downloading from CDN...");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _axeScript = await http.GetStringAsync(AxeCdnUrl);
            await File.WriteAllTextAsync(cachePath, _axeScript);
            Console.WriteLine($"  [axe-core] Downloaded and cached ({_axeScript.Length / 1024}KB)");
            return _axeScript;
        }
        finally
        {
            _axeLock.Release();
        }
    }

    // HTML_CodeSniffer (the engine behind Pa11y) — a second independent rule engine
    private const string HtmlCsCdnUrl = "https://squizlabs.github.io/HTML_CodeSniffer/build/HTMLCS.js";
    private static string? _htmlCsScript;
    private static readonly SemaphoreSlim _htmlCsLock = new(1, 1);

    /// <summary>
    /// Download HTML_CodeSniffer.js from CDN on first use, cache in project directory.
    /// Thread-safe — only one download happens even with concurrent calls.
    /// </summary>
    private static async Task<string> EnsureHtmlCodeSnifferAsync(string projectDir)
    {
        if (_htmlCsScript != null) return _htmlCsScript;

        await _htmlCsLock.WaitAsync();
        try
        {
            if (_htmlCsScript != null) return _htmlCsScript;

            var cachePath = Path.Combine(projectDir, "HTMLCS.js");

            if (File.Exists(cachePath))
            {
                _htmlCsScript = await File.ReadAllTextAsync(cachePath);
                Console.WriteLine($"  [HTMLCS] Cached ({_htmlCsScript.Length / 1024}KB)");
                return _htmlCsScript;
            }

            Console.WriteLine("  [HTMLCS] Downloading from CDN...");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _htmlCsScript = await http.GetStringAsync(HtmlCsCdnUrl);
            await File.WriteAllTextAsync(cachePath, _htmlCsScript);
            Console.WriteLine($"  [HTMLCS] Downloaded and cached ({_htmlCsScript.Length / 1024}KB)");
            return _htmlCsScript;
        }
        finally
        {
            _htmlCsLock.Release();
        }
    }

    // IBM Equal Access Checker engine — a third independent rule engine from IBM
    private const string IbmAceCdnUrl = "https://cdn.jsdelivr.net/npm/accessibility-checker-engine@latest/ace.js";
    private static string? _ibmAceScript;
    private static readonly SemaphoreSlim _ibmAceLock = new(1, 1);

    /// <summary>
    /// Download IBM Equal Access checker engine from CDN on first use, cache in project directory.
    /// Thread-safe — only one download happens even with concurrent calls.
    /// </summary>
    private static async Task<string> EnsureIbmAceAsync(string projectDir)
    {
        if (_ibmAceScript != null) return _ibmAceScript;

        await _ibmAceLock.WaitAsync();
        try
        {
            if (_ibmAceScript != null) return _ibmAceScript;

            var cachePath = Path.Combine(projectDir, "ace.js");

            if (File.Exists(cachePath))
            {
                _ibmAceScript = await File.ReadAllTextAsync(cachePath);
                Console.WriteLine($"  [IBM-ACE] Cached ({_ibmAceScript.Length / 1024}KB)");
                return _ibmAceScript;
            }

            Console.WriteLine("  [IBM-ACE] Downloading from CDN...");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _ibmAceScript = await http.GetStringAsync(IbmAceCdnUrl);
            await File.WriteAllTextAsync(cachePath, _ibmAceScript);
            Console.WriteLine($"  [IBM-ACE] Downloaded and cached ({_ibmAceScript.Length / 1024}KB)");
            return _ibmAceScript;
        }
        finally
        {
            _ibmAceLock.Release();
        }
    }

    /// <summary>
    /// Inject axe-core into a live Playwright page, run accessibility scan, 
    /// and return normalized results.
    /// </summary>
    private static async Task<A11yToolResult> RunAxeCoreAsync(IPage page, string axeScript, string wcagLevel)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new A11yToolResult { ToolName = "axe" };

        try
        {
            // Inject axe-core into the page
            await page.EvaluateAsync(axeScript);

            // Build the WCAG tags array from the configured level
            var wcagTags = wcagLevel switch
            {
                "wcag22aa" => "['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']",
                "wcag21aa" => "['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']",
                "wcag2aa" => "['wcag2a', 'wcag2aa']",
                "wcag2a" => "['wcag2a']",
                _ => "['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']"
            };

            // Run axe and get violations
            var violationsJson = await page.EvaluateAsync<JsonElement>($@"
                async () => {{
                    const results = await axe.run({{
                        runOnly: {{ type: 'tag', values: {wcagTags} }}
                    }});
                    return results.violations.map(v => ({{
                        id: v.id,
                        impact: v.impact,
                        help: v.help,
                        helpUrl: v.helpUrl,
                        tags: v.tags,
                        nodes: v.nodes.map(n => ({{
                            html: n.html.substring(0, 200),
                            target: n.target,
                            failureSummary: n.failureSummary
                        }}))
                    }}));
                }}
            ");

            // Parse violations into unified issues
            var violations = JsonSerializer.Deserialize<List<AxeViolation>>(
                violationsJson.GetRawText(), JsonOptions) ?? [];

            foreach (var v in violations)
            {
                // Extract WCAG criteria from tags (e.g., "wcag111" → "1.1.1")
                var wcagTag = v.Tags.FirstOrDefault(t =>
                    t.StartsWith("wcag") && t.Length > 5 && char.IsDigit(t[4]));
                var wcagCriteria = wcagTag != null ? FormatWcagTag(wcagTag) : null;

                foreach (var node in v.Nodes)
                {
                    result.Issues.Add(new A11yIssue
                    {
                        Tool = "axe",
                        RuleId = v.Id,
                        CanonicalRuleId = v.Id, // axe IDs are canonical
                        Severity = v.Impact ?? "moderate",
                        Message = v.Help,
                        Selector = node.Target.FirstOrDefault(),
                        Snippet = node.Html.Length > 150 ? node.Html[..150] + "..." : node.Html,
                        HelpUrl = v.HelpUrl,
                        WcagCriteria = wcagCriteria
                    });
                }
            }

            result.Status = "completed";
        }
        catch (Exception ex)
        {
            result.Status = "error";
            result.ErrorMessage = ex.Message;
        }

        sw.Stop();
        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    // ========================================================================
    // A11y Overlay Screenshots
    // ========================================================================

    // ========================================================================
    // Expand All Collapsed Content (pre-scan)
    // ========================================================================

    /// <summary>
    /// Expand ALL collapsed content on the page so every element is visible for
    /// accessibility scanning and screenshots. Handles:
    /// - HTML &lt;details&gt; elements (open attribute)
    /// - Bootstrap .collapse / .accordion-collapse (add .show, aria-expanded)
    /// - Blazor-style conditional panels (click toggle buttons like AboutSection)
    /// - Bootstrap nav-tabs (show all tab panes simultaneously)
    /// - Elements with display:none that are inside expandable containers
    /// - aria-expanded="false" buttons (click to expand)
    /// Returns the count of items expanded.
    /// </summary>
    private static async Task<int> ExpandAllCollapsedContentAsync(IPage page)
    {
        var count = await page.EvaluateAsync<int>(@"
            () => {
                let expanded = 0;

                // 1. HTML <details> elements — set open attribute
                document.querySelectorAll('details:not([open])').forEach(el => {
                    el.setAttribute('open', '');
                    expanded++;
                });

                // 2. Bootstrap .collapse that aren't .show yet
                document.querySelectorAll('.collapse:not(.show)').forEach(el => {
                    el.classList.add('show');
                    el.style.display = '';
                    expanded++;
                });

                // 3. Bootstrap accordion — expand all collapsed panels
                document.querySelectorAll('.accordion-collapse:not(.show)').forEach(el => {
                    el.classList.add('show');
                    el.classList.remove('collapsing');
                    el.style.height = '';
                    expanded++;
                });
                // Fix accordion buttons to show expanded state
                document.querySelectorAll('.accordion-button.collapsed').forEach(btn => {
                    btn.classList.remove('collapsed');
                    btn.setAttribute('aria-expanded', 'true');
                });

                // 4. Bootstrap tab-pane — show ALL tab panes simultaneously
                //    (so every tab's content is visible in the screenshot)
                document.querySelectorAll('.tab-pane:not(.show)').forEach(el => {
                    el.classList.add('show', 'active');
                    el.style.display = '';
                    expanded++;
                });

                // 5. Elements with aria-expanded=""false"" — click them to expand
                //    This covers Blazor's AboutSection and similar toggle patterns
                document.querySelectorAll('[aria-expanded=""false""]').forEach(el => {
                    try {
                        el.click();
                        expanded++;
                    } catch(e) { /* skip if click fails */ }
                });

                // 6. Blazor-specific: elements hidden by @if (conditional rendering)
                //    These can't be expanded from JS — they don't exist in the DOM.
                //    But we CAN click toggle-style elements to reveal them.
                //    Look for common patterns: cursor-pointer headers that toggle content
                document.querySelectorAll('.cursor-pointer, [role=""button""]').forEach(el => {
                    // Only click if the next sibling isn't visible or doesn't exist
                    const next = el.nextElementSibling;
                    if (!next || next.offsetHeight === 0) {
                        try {
                            // Check if it looks like a toggle header
                            const hasChevron = el.querySelector('.fa-chevron-down, .fa-chevron-right, .fa-caret-down, .fa-caret-right, .fa-plus');
                            if (hasChevron) {
                                el.click();
                                expanded++;
                            }
                        } catch(e) { /* skip */ }
                    }
                });

                // 7. MudBlazor panels — expand closed expansion panels
                document.querySelectorAll('.mud-expand-panel:not(.mud-panel-expanded)').forEach(el => {
                    const header = el.querySelector('.mud-expand-panel-header');
                    if (header) {
                        try { header.click(); expanded++; } catch(e) {}
                    }
                });

                // 8. Ensure nothing is display:none that looks like content
                //    (Skip script/style/meta elements)
                document.querySelectorAll('[style*=""display: none""], [style*=""display:none""]').forEach(el => {
                    const tag = el.tagName.toLowerCase();
                    if (['script', 'style', 'meta', 'link', 'head', 'template', 'noscript'].includes(tag)) return;
                    // Only expand if it's inside a known expandable container
                    if (el.closest('.collapse, .accordion, .tab-content, details, .expandable, .toggle-content')) {
                        el.style.display = '';
                        expanded++;
                    }
                });

                // 9. Scroll through the page to trigger lazy-loaded content
                //    Some Blazor components only render when scrolled into view
                const totalHeight = document.body.scrollHeight;
                const viewportHeight = window.innerHeight;
                for (let y = 0; y < totalHeight; y += viewportHeight) {
                    window.scrollTo(0, y);
                }
                // Return to top
                window.scrollTo(0, 0);

                return expanded;
            }
        ");

        return count;
    }

    /// <summary>
    /// Inject axe-core highlight overlay onto the page — draws colored outlines and
    /// labels on every violating element, plus a summary banner at the top.
    /// Replicates the visual output of the axe DevTools browser extension.
    /// axe-core must already be injected on the page (via RunAxeCoreAsync).
    /// </summary>
    private static async Task InjectAxeHighlightOverlayAsync(IPage page, string wcagLevel)
    {
        var wcagTags = wcagLevel switch
        {
            "wcag22aa" => "['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']",
            "wcag21aa" => "['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']",
            "wcag2aa" => "['wcag2a', 'wcag2aa']",
            "wcag2a" => "['wcag2a']",
            _ => "['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']"
        };

        // Run axe again (cheap — already cached in page) and inject visual highlights
        await page.EvaluateAsync($@"
            async () => {{
                const results = await axe.run({{
                    runOnly: {{ type: 'tag', values: {wcagTags} }}
                }});

                // Severity → color map (matches axe DevTools extension colors)
                const colors = {{
                    critical: {{ bg: 'rgba(220, 53, 69, 0.15)', border: '#dc3545', text: '#dc3545', label: '🔴' }},
                    serious:  {{ bg: 'rgba(255, 152, 0, 0.12)',  border: '#ff9800', text: '#e65100', label: '🟠' }},
                    moderate: {{ bg: 'rgba(255, 235, 59, 0.15)', border: '#fbc02d', text: '#f57f17', label: '🟡' }},
                    minor:    {{ bg: 'rgba(33, 150, 243, 0.10)',  border: '#2196f3', text: '#1565c0', label: '🔵' }}
                }};

                let totalNodes = 0;
                const severityCounts = {{ critical: 0, serious: 0, moderate: 0, minor: 0 }};

                for (const violation of results.violations) {{
                    const severity = violation.impact || 'moderate';
                    const c = colors[severity] || colors.moderate;

                    for (const node of violation.nodes) {{
                        totalNodes++;
                        severityCounts[severity] = (severityCounts[severity] || 0) + 1;

                        for (const selector of node.target) {{
                            try {{
                                const el = document.querySelector(selector);
                                if (!el) continue;

                                // Highlight the element
                                el.style.outline = `3px solid ${{c.border}}`;
                                el.style.outlineOffset = '2px';
                                el.style.backgroundColor = c.bg;
                                el.style.position = el.style.position || 'relative';

                                // Add label badge
                                const badge = document.createElement('div');
                                badge.textContent = `${{c.label}} ${{violation.id}}`;
                                badge.style.cssText = `
                                    position: absolute; top: -18px; left: 0; z-index: 99999;
                                    font-size: 10px; font-family: monospace; font-weight: 700;
                                    color: white; background: ${{c.border}};
                                    padding: 1px 5px; border-radius: 3px;
                                    white-space: nowrap; pointer-events: none;
                                    line-height: 14px;
                                `;
                                el.appendChild(badge);
                            }} catch (e) {{ /* selector didn't match */ }}
                        }}
                    }}
                }}

                // Summary banner at top of page
                const banner = document.createElement('div');
                banner.id = 'axe-overlay-banner';
                const parts = [];
                if (severityCounts.critical > 0) parts.push(`🔴 ${{severityCounts.critical}} Critical`);
                if (severityCounts.serious > 0)  parts.push(`🟠 ${{severityCounts.serious}} Serious`);
                if (severityCounts.moderate > 0) parts.push(`🟡 ${{severityCounts.moderate}} Moderate`);
                if (severityCounts.minor > 0)    parts.push(`🔵 ${{severityCounts.minor}} Minor`);

                const summaryText = totalNodes === 0
                    ? '✅ axe-core: No violations detected'
                    : `⚠️ axe-core: ${{totalNodes}} violation(s) — ${{parts.join(' · ')}}`;

                banner.innerHTML = `
                    <div style=""
                        position:fixed; top:0; left:0; right:0; z-index:999999;
                        background: ${{totalNodes === 0 ? '#198754' : '#212529'}};
                        color: white; padding: 8px 16px;
                        font: bold 13px/1.4 -apple-system, 'Segoe UI', Roboto, monospace;
                        display: flex; align-items: center; gap: 12px;
                        box-shadow: 0 2px 8px rgba(0,0,0,0.3);
                    "">
                        <span style=""font-size:16px"">🪓</span>
                        <span>${{summaryText}}</span>
                        <span style=""margin-left:auto; opacity:0.6; font-size:11px"">axe-core 4.10 · WCAG 2.1 AA</span>
                    </div>
                `;
                document.body.prepend(banner);

                // Push page content down so banner doesn't overlap
                document.body.style.marginTop = '40px';
            }}
        ");
    }

    /// <summary>
    /// Remove the axe highlight overlay so the page returns to its original state.
    /// </summary>
    private static async Task RemoveAxeHighlightOverlayAsync(IPage page)
    {
        await page.EvaluateAsync(@"
            () => {
                // Remove the banner
                const banner = document.getElementById('axe-overlay-banner');
                if (banner) banner.remove();
                document.body.style.marginTop = '';
            }
        ");
    }

    /// <summary>
    /// Inject a WAVE-style accessibility evaluation overlay onto the page.
    /// Since the real WAVE browser extension can't be loaded in headless Playwright,
    /// this injects a custom evaluation that replicates WAVE's visual icon style —
    /// placing small categorized icons next to elements with accessibility issues.
    /// 
    /// The overlay checks for: missing alt text, empty links/buttons, missing form labels,
    /// heading hierarchy issues, missing landmarks, contrast hints, and ARIA misuse.
    /// </summary>
    private static async Task InjectWaveStyleOverlayAsync(IPage page)
    {
        await page.EvaluateAsync(@"
            () => {
                // WAVE-style icon categories and colors
                const iconStyles = {
                    error:   { bg: '#dc3545', icon: '✕', label: 'ERROR' },
                    alert:   { bg: '#ffc107', icon: '⚠', label: 'ALERT' },
                    feature: { bg: '#198754', icon: '✓', label: 'FEATURE' },
                    struct:  { bg: '#6f42c1', icon: '▣', label: 'STRUCTURAL' },
                    aria:    { bg: '#0d6efd', icon: 'ⓐ', label: 'ARIA' }
                };

                const issues = [];

                function addIcon(el, type, message) {
                    if (!el || !el.getBoundingClientRect) return;
                    issues.push({ el, type, message });
                }

                // --- Errors ---
                // Missing alt on images
                document.querySelectorAll('img:not([alt])').forEach(img => {
                    addIcon(img, 'error', 'Missing alt text');
                });
                // Empty alt on non-decorative images
                document.querySelectorAll('img[alt=""""]').forEach(img => {
                    if (!img.getAttribute('role') && !img.closest('[role=""presentation""]'))
                        addIcon(img, 'alert', 'Empty alt text');
                });
                // Empty links
                document.querySelectorAll('a').forEach(a => {
                    const text = (a.textContent || '').trim();
                    const ariaLabel = a.getAttribute('aria-label') || '';
                    const title = a.getAttribute('title') || '';
                    const img = a.querySelector('img[alt]');
                    if (!text && !ariaLabel && !title && !img) {
                        addIcon(a, 'error', 'Empty link');
                    }
                });
                // Empty buttons
                document.querySelectorAll('button').forEach(btn => {
                    const text = (btn.textContent || '').trim();
                    const ariaLabel = btn.getAttribute('aria-label') || '';
                    const title = btn.getAttribute('title') || '';
                    if (!text && !ariaLabel && !title) {
                        addIcon(btn, 'error', 'Empty button');
                    }
                });
                // Missing form labels
                document.querySelectorAll('input:not([type=""hidden""]):not([type=""submit""]):not([type=""button""]), select, textarea').forEach(input => {
                    const id = input.id;
                    const ariaLabel = input.getAttribute('aria-label');
                    const ariaLabelledby = input.getAttribute('aria-labelledby');
                    const hasLabel = id && document.querySelector(`label[for=""${id}""]`);
                    const parentLabel = input.closest('label');
                    if (!hasLabel && !parentLabel && !ariaLabel && !ariaLabelledby) {
                        addIcon(input, 'error', 'Missing form label');
                    }
                });

                // --- Structural ---
                // Headings
                document.querySelectorAll('h1,h2,h3,h4,h5,h6').forEach(h => {
                    addIcon(h, 'struct', `Heading: <${h.tagName.toLowerCase()}>`);
                });
                // Landmarks
                document.querySelectorAll('nav, main, header, footer, aside, [role=""navigation""], [role=""main""], [role=""banner""], [role=""contentinfo""]').forEach(el => {
                    const role = el.getAttribute('role') || el.tagName.toLowerCase();
                    addIcon(el, 'struct', `Landmark: ${role}`);
                });

                // --- Features ---
                // Alt text present
                document.querySelectorAll('img[alt]:not([alt=""""])').forEach(img => {
                    addIcon(img, 'feature', `Alt: ${img.alt.substring(0, 40)}`);
                });
                // Skip links
                document.querySelectorAll('a[href^=""#""]').forEach(a => {
                    const text = (a.textContent || '').trim().toLowerCase();
                    if (text.includes('skip') || text.includes('main content')) {
                        addIcon(a, 'feature', 'Skip link');
                    }
                });

                // --- ARIA ---
                document.querySelectorAll('[aria-label], [aria-labelledby], [aria-describedby], [role]').forEach(el => {
                    const attrs = [];
                    if (el.getAttribute('role')) attrs.push(`role=${el.getAttribute('role')}`);
                    if (el.getAttribute('aria-label')) attrs.push('aria-label');
                    if (el.getAttribute('aria-labelledby')) attrs.push('aria-labelledby');
                    if (attrs.length > 0 && !el.matches('h1,h2,h3,h4,h5,h6,nav,main,header,footer,aside')) {
                        addIcon(el, 'aria', attrs.join(', '));
                    }
                });

                // --- Alerts ---
                // No main landmark
                if (!document.querySelector('main, [role=""main""]')) {
                    addIcon(document.body, 'alert', 'No <main> landmark');
                }
                // No skip link
                const firstFocusable = document.querySelector('a, button, input, select, textarea');
                const skipLink = document.querySelector('a[href^=""#""]');
                if (firstFocusable && (!skipLink || !(skipLink.textContent || '').toLowerCase().includes('skip'))) {
                    addIcon(document.body, 'alert', 'No skip navigation link');
                }
                // No h1
                if (!document.querySelector('h1')) {
                    addIcon(document.body, 'alert', 'No <h1> heading');
                }

                // --- Render icons onto page ---
                // Limit to first 200 to avoid overwhelming the page
                const maxIcons = 200;
                const rendered = issues.slice(0, maxIcons);

                rendered.forEach((issue, idx) => {
                    const style = iconStyles[issue.type];
                    const icon = document.createElement('div');
                    icon.className = 'wave-icon';
                    icon.style.cssText = `
                        position: absolute; z-index: ${100000 + idx};
                        width: 20px; height: 20px; border-radius: 50%;
                        background: ${style.bg}; color: white;
                        font-size: 12px; font-weight: bold; line-height: 20px;
                        text-align: center; cursor: default;
                        box-shadow: 0 1px 3px rgba(0,0,0,0.4);
                        pointer-events: none; font-family: sans-serif;
                    `;
                    icon.textContent = style.icon;
                    icon.title = `[${style.label}] ${issue.message}`;

                    // Position near the element
                    try {
                        const rect = issue.el.getBoundingClientRect();
                        const scrollX = window.scrollX;
                        const scrollY = window.scrollY;
                        icon.style.left = `${rect.left + scrollX - 10}px`;
                        icon.style.top = `${rect.top + scrollY - 10}px`;
                        document.body.appendChild(icon);
                    } catch (e) { /* skip positioning errors */ }
                });

                // Summary banner
                const counts = { error: 0, alert: 0, feature: 0, struct: 0, aria: 0 };
                issues.forEach(i => counts[i.type]++);

                const banner = document.createElement('div');
                banner.id = 'wave-overlay-banner';
                banner.innerHTML = `
                    <div style=""
                        position:fixed; top:0; left:0; right:0; z-index:999999;
                        background: #1a1a2e; color: white;
                        padding: 8px 16px;
                        font: bold 13px/1.4 -apple-system, 'Segoe UI', Roboto, monospace;
                        display: flex; align-items: center; gap: 16px;
                        box-shadow: 0 2px 8px rgba(0,0,0,0.3);
                    "">
                        <span style=""font-size:16px"">🌊</span>
                        <span>WAVE-style Evaluation</span>
                        <span style=""background:#dc3545;padding:2px 8px;border-radius:10px;font-size:11px"">${counts.error} Errors</span>
                        <span style=""background:#ffc107;color:#000;padding:2px 8px;border-radius:10px;font-size:11px"">${counts.alert} Alerts</span>
                        <span style=""background:#198754;padding:2px 8px;border-radius:10px;font-size:11px"">${counts.feature} Features</span>
                        <span style=""background:#6f42c1;padding:2px 8px;border-radius:10px;font-size:11px"">${counts.struct} Structural</span>
                        <span style=""background:#0d6efd;padding:2px 8px;border-radius:10px;font-size:11px"">${counts.aria} ARIA</span>
                        <span style=""margin-left:auto; opacity:0.6; font-size:11px"">FreeTools a11y · WCAG 2.1 AA</span>
                    </div>
                `;
                document.body.prepend(banner);
                document.body.style.marginTop = (parseInt(document.body.style.marginTop || '0') + 40) + 'px';
            }
        ");
    }

    /// <summary>
    /// Remove the WAVE-style overlay icons and banner.
    /// </summary>
    private static async Task RemoveWaveStyleOverlayAsync(IPage page)
    {
        await page.EvaluateAsync(@"
            () => {
                document.querySelectorAll('.wave-icon').forEach(el => el.remove());
                const banner = document.getElementById('wave-overlay-banner');
                if (banner) banner.remove();
                document.body.style.marginTop = '';
            }
        ");
    }

    // ========================================================================
    // HTML_CodeSniffer Overlay (Pa11y engine)
    // ========================================================================

    /// <summary>
    /// Inject HTML_CodeSniffer (Squiz/Pa11y engine) into the page, run a WCAG 2.1 AA
    /// scan, and draw colored outlines on violating elements with a summary banner.
    /// This is a completely different rule engine from axe-core — it catches different issues.
    /// </summary>
    private static async Task InjectHtmlCodeSnifferOverlayAsync(IPage page, string htmlCsScript)
    {
        // Inject the HTMLCS engine
        await page.EvaluateAsync(htmlCsScript);

        // Run the scan and inject visual overlay
        await page.EvaluateAsync(@"
            () => new Promise(resolve => {
                const colors = {
                    1: { bg: 'rgba(220, 53, 69, 0.15)', border: '#dc3545', label: '✕ ERROR' },
                    2: { bg: 'rgba(255, 193, 7, 0.12)',  border: '#ffc107', label: '⚠ WARNING' },
                    3: { bg: 'rgba(13, 110, 253, 0.08)', border: '#0d6efd', label: 'ℹ NOTICE' }
                };

                HTMLCS.process('WCAG2AA', document, function() {
                    const messages = HTMLCS.getMessages();
                    const counts = { 1: 0, 2: 0, 3: 0 };

                    // Limit overlays to errors + warnings (skip notices to avoid visual noise)
                    const actionable = messages.filter(m => m.type <= 2);
                    const maxOverlays = 150;
                    const toRender = actionable.slice(0, maxOverlays);

                    toRender.forEach((msg, idx) => {
                        counts[msg.type]++;
                        const c = colors[msg.type];
                        if (!msg.element || !msg.element.getBoundingClientRect) return;

                        try {
                            msg.element.style.outline = `3px solid ${c.border}`;
                            msg.element.style.outlineOffset = '2px';
                            msg.element.style.backgroundColor = c.bg;

                            // Extract short rule code from the full SNIFF code
                            const codeParts = (msg.code || '').split('.');
                            const shortCode = codeParts.length > 3 ? codeParts.slice(-2).join('.') : msg.code;

                            const badge = document.createElement('div');
                            badge.className = 'htmlcs-badge';
                            badge.textContent = `${c.label.charAt(0)} ${shortCode}`;
                            badge.title = msg.msg;
                            badge.style.cssText = `
                                position: absolute; z-index: ${99000 + idx};
                                font-size: 9px; font-family: monospace; font-weight: 700;
                                color: white; background: ${c.border};
                                padding: 1px 4px; border-radius: 2px;
                                white-space: nowrap; pointer-events: none;
                                line-height: 13px; max-width: 200px; overflow: hidden;
                            `;

                            const rect = msg.element.getBoundingClientRect();
                            badge.style.left = `${rect.left + window.scrollX}px`;
                            badge.style.top = `${rect.top + window.scrollY - 16}px`;
                            document.body.appendChild(badge);
                        } catch(e) { /* skip */ }
                    });

                    // Count all messages
                    messages.forEach(m => { if (!counts[m.type]) counts[m.type] = 0; });
                    const totalErrors = messages.filter(m => m.type === 1).length;
                    const totalWarnings = messages.filter(m => m.type === 2).length;
                    const totalNotices = messages.filter(m => m.type === 3).length;

                    // Summary banner
                    const banner = document.createElement('div');
                    banner.id = 'htmlcs-overlay-banner';
                    const summaryText = totalErrors === 0 && totalWarnings === 0
                        ? '✅ HTML_CodeSniffer: No errors or warnings'
                        : `⚠️ HTML_CodeSniffer: ${totalErrors} error(s), ${totalWarnings} warning(s), ${totalNotices} notice(s)`;

                    banner.innerHTML = `
                        <div style=""
                            position:fixed; top:0; left:0; right:0; z-index:999999;
                            background: ${totalErrors > 0 ? '#7b2d26' : '#2d4a7b'};
                            color: white; padding: 8px 16px;
                            font: bold 13px/1.4 -apple-system, 'Segoe UI', Roboto, monospace;
                            display: flex; align-items: center; gap: 12px;
                            box-shadow: 0 2px 8px rgba(0,0,0,0.3);
                        "">
                            <span style=""font-size:16px"">🔍</span>
                            <span>${summaryText}</span>
                            <span style=""background:#dc3545;padding:2px 8px;border-radius:10px;font-size:11px"">${totalErrors} Errors</span>
                            <span style=""background:#ffc107;color:#000;padding:2px 8px;border-radius:10px;font-size:11px"">${totalWarnings} Warnings</span>
                            <span style=""margin-left:auto; opacity:0.6; font-size:11px"">HTML_CodeSniffer · Pa11y engine · WCAG 2.1 AA</span>
                        </div>
                    `;
                    document.body.prepend(banner);
                    document.body.style.marginTop = '40px';

                    resolve();
                });
            })
        ");
    }

    /// <summary>
    /// Remove the HTML_CodeSniffer overlay badges and banner.
    /// </summary>
    private static async Task RemoveHtmlCodeSnifferOverlayAsync(IPage page)
    {
        await page.EvaluateAsync(@"
            () => {
                document.querySelectorAll('.htmlcs-badge').forEach(el => el.remove());
                const banner = document.getElementById('htmlcs-overlay-banner');
                if (banner) banner.remove();
                document.body.style.marginTop = '';
                // Remove outlines added to elements
                document.querySelectorAll('[style*=""outline""]').forEach(el => {
                    el.style.outline = '';
                    el.style.outlineOffset = '';
                    el.style.backgroundColor = '';
                });
            }
        ");
    }

    // ========================================================================
    // IBM Equal Access Overlay
    // ========================================================================

    /// <summary>
    /// Inject IBM Equal Access checker engine into the page, run an accessibility scan,
    /// and draw visual highlights on violating elements. IBM's engine uses different rules
    /// from both axe-core and HTML_CodeSniffer, providing a third independent perspective.
    /// Falls back gracefully if the engine fails to load or execute.
    /// </summary>
    private static async Task InjectIbmAceOverlayAsync(IPage page, string ibmAceScript)
    {
        // Inject the IBM ACE engine
        await page.EvaluateAsync(ibmAceScript);

        // Run the scan and inject visual overlay
        await page.EvaluateAsync(@"
            async () => {
                const colors = {
                    Violation:         { bg: 'rgba(220, 53, 69, 0.15)', border: '#dc3545', icon: '✕' },
                    'Needs review':    { bg: 'rgba(255, 193, 7, 0.12)',  border: '#ffc107', icon: '?' },
                    Recommendation:    { bg: 'rgba(13, 110, 253, 0.08)', border: '#0d6efd', icon: 'ℹ' },
                    Pass:              { bg: 'transparent',               border: '#198754', icon: '✓' }
                };

                let results;
                try {
                    // IBM ACE exposes the checker through different paths depending on build
                    const checker = typeof ace !== 'undefined' && ace.Checker
                        ? new ace.Checker()
                        : (typeof window.aceIBMa11y !== 'undefined'
                            ? new window.aceIBMa11y.Checker()
                            : null);

                    if (!checker) {
                        // Engine loaded but API not found — show informational banner
                        const banner = document.createElement('div');
                        banner.id = 'ibm-ace-overlay-banner';
                        banner.innerHTML = `<div style=""
                            position:fixed;top:0;left:0;right:0;z-index:999999;
                            background:#6c757d;color:white;padding:8px 16px;
                            font:bold 13px/1.4 monospace;box-shadow:0 2px 8px rgba(0,0,0,0.3);
                        ""><span style=""font-size:16px"">🏢</span> IBM Equal Access: Engine loaded but API not available in this build</div>`;
                        document.body.prepend(banner);
                        document.body.style.marginTop = '40px';
                        return;
                    }

                    results = await checker.check(document, ['WCAG_2_1']);
                } catch(e) {
                    // Fallback: run a custom IBM-rules-inspired check
                    results = { results: [] };

                    // Custom checks inspired by IBM's unique rules
                    // ARIA label checks
                    document.querySelectorAll('[role]').forEach(el => {
                        const role = el.getAttribute('role');
                        const label = el.getAttribute('aria-label') || el.getAttribute('aria-labelledby');
                        if (['button', 'link', 'tab', 'menuitem'].includes(role) && !label && !(el.textContent || '').trim()) {
                            results.results.push({ value: ['VIOLATION', 'FAIL'], path: { dom: '' }, ruleId: 'aria_role_label', message: `${role} role missing accessible name`, node: el });
                        }
                    });
                    // Input purpose
                    document.querySelectorAll('input[type=""text""], input[type=""email""], input[type=""tel""], input[type=""url""]').forEach(el => {
                        if (!el.getAttribute('autocomplete')) {
                            results.results.push({ value: ['RECOMMENDATION', 'MANUAL'], ruleId: 'input_autocomplete', message: 'Input may benefit from autocomplete attribute', node: el });
                        }
                    });
                    // Focus visible
                    document.querySelectorAll('a, button, input, select, textarea, [tabindex]').forEach(el => {
                        const style = window.getComputedStyle(el);
                        if (style.outlineStyle === 'none' && style.outlineWidth === '0px') {
                            results.results.push({ value: ['RECOMMENDATION', 'MANUAL'], ruleId: 'focus_visible', message: 'Element may not have visible focus indicator', node: el });
                        }
                    });
                    // Color contrast (basic)
                    document.querySelectorAll('p, span, li, td, th, label, a, h1, h2, h3, h4, h5, h6').forEach(el => {
                        const style = window.getComputedStyle(el);
                        const color = style.color;
                        const bg = style.backgroundColor;
                        if (color === bg && color !== 'rgba(0, 0, 0, 0)') {
                            results.results.push({ value: ['VIOLATION', 'FAIL'], ruleId: 'color_contrast', message: 'Text and background colors are identical', node: el });
                        }
                    });
                }

                // Process results and render overlay
                const counts = { Violation: 0, 'Needs review': 0, Recommendation: 0 };
                const maxOverlays = 150;
                let rendered = 0;

                for (const item of (results.results || [])) {
                    const level = Array.isArray(item.value) ? item.value[0] : 'Recommendation';
                    const mappedLevel = level === 'VIOLATION' || level === 'FAIL' ? 'Violation'
                                      : level === 'RECOMMENDATION' || level === 'MANUAL' ? 'Needs review'
                                      : 'Recommendation';

                    if (mappedLevel === 'Pass') continue;
                    counts[mappedLevel] = (counts[mappedLevel] || 0) + 1;

                    if (rendered >= maxOverlays) continue;
                    rendered++;

                    const c = colors[mappedLevel] || colors.Recommendation;
                    const el = item.node || (item.path && item.path.dom
                        ? (() => { try { return document.querySelector(item.path.dom); } catch { return null; } })()
                        : null);

                    if (!el || !el.getBoundingClientRect) continue;

                    try {
                        el.style.outline = `3px solid ${c.border}`;
                        el.style.outlineOffset = '2px';

                        const badge = document.createElement('div');
                        badge.className = 'ibm-ace-badge';
                        badge.textContent = `${c.icon} ${item.ruleId || 'rule'}`;
                        badge.title = item.message || '';
                        badge.style.cssText = `
                            position: absolute; z-index: ${98000 + rendered};
                            font-size: 9px; font-family: monospace; font-weight: 700;
                            color: white; background: ${c.border};
                            padding: 1px 4px; border-radius: 2px;
                            white-space: nowrap; pointer-events: none;
                            line-height: 13px; max-width: 200px; overflow: hidden;
                        `;
                        const rect = el.getBoundingClientRect();
                        badge.style.left = `${rect.left + window.scrollX}px`;
                        badge.style.top = `${rect.top + window.scrollY - 16}px`;
                        document.body.appendChild(badge);
                    } catch(e) { /* skip */ }
                }

                // Summary banner
                const banner = document.createElement('div');
                banner.id = 'ibm-ace-overlay-banner';
                const total = counts.Violation + counts['Needs review'] + counts.Recommendation;
                const summaryText = counts.Violation === 0
                    ? `✅ IBM Equal Access: ${total} item(s) found, no violations`
                    : `⚠️ IBM Equal Access: ${counts.Violation} violation(s), ${counts['Needs review']} need review`;

                banner.innerHTML = `
                    <div style=""
                        position:fixed; top:0; left:0; right:0; z-index:999999;
                        background: ${counts.Violation > 0 ? '#4a1942' : '#1a3a4a'};
                        color: white; padding: 8px 16px;
                        font: bold 13px/1.4 -apple-system, 'Segoe UI', Roboto, monospace;
                        display: flex; align-items: center; gap: 12px;
                        box-shadow: 0 2px 8px rgba(0,0,0,0.3);
                    "">
                        <span style=""font-size:16px"">🏢</span>
                        <span>${summaryText}</span>
                        <span style=""background:#dc3545;padding:2px 8px;border-radius:10px;font-size:11px"">${counts.Violation} Violations</span>
                        <span style=""background:#ffc107;color:#000;padding:2px 8px;border-radius:10px;font-size:11px"">${counts['Needs review']} Review</span>
                        <span style=""margin-left:auto; opacity:0.6; font-size:11px"">IBM Equal Access Checker</span>
                    </div>
                `;
                document.body.prepend(banner);
                document.body.style.marginTop = '40px';
            }
        ");
    }

    /// <summary>
    /// Remove the IBM Equal Access overlay badges and banner.
    /// </summary>
    private static async Task RemoveIbmAceOverlayAsync(IPage page)
    {
        await page.EvaluateAsync(@"
            () => {
                document.querySelectorAll('.ibm-ace-badge').forEach(el => el.remove());
                const banner = document.getElementById('ibm-ace-overlay-banner');
                if (banner) banner.remove();
                document.body.style.marginTop = '';
                document.querySelectorAll('[style*=""outline""]').forEach(el => {
                    el.style.outline = '';
                    el.style.outlineOffset = '';
                });
            }
        ");
    }

    // ========================================================================
    // Focus Order + Heading Structure Overlay
    // ========================================================================

    /// <summary>
    /// Inject a visual overlay showing keyboard tab order (numbered sequence on all
    /// focusable elements) and heading hierarchy (h1→h2→h3 flow with nesting indicators).
    /// This shows what a keyboard-only user experiences and whether the heading structure
    /// is logical — two things that automated rule engines often miss.
    /// </summary>
    private static async Task InjectStructureOverlayAsync(IPage page)
    {
        await page.EvaluateAsync(@"
            () => {
                // ── Tab Order ──
                // Gather all focusable elements in DOM order (natural tab order)
                const focusable = Array.from(document.querySelectorAll(
                    'a[href], button:not([disabled]), input:not([disabled]):not([type=""hidden""]), ' +
                    'select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex=""-1""])'
                )).filter(el => {
                    const style = window.getComputedStyle(el);
                    return style.display !== 'none' && style.visibility !== 'hidden' && el.offsetParent !== null;
                });

                // Sort: positive tabindex first (in order), then tabindex=0 / no tabindex (DOM order)
                const withTabindex = focusable.filter(el => {
                    const ti = parseInt(el.getAttribute('tabindex') || '0');
                    return ti > 0;
                }).sort((a, b) => parseInt(a.getAttribute('tabindex')) - parseInt(b.getAttribute('tabindex')));

                const withoutTabindex = focusable.filter(el => {
                    const ti = parseInt(el.getAttribute('tabindex') || '0');
                    return ti <= 0;
                });

                const tabOrder = [...withTabindex, ...withoutTabindex];
                const maxTabMarkers = 100; // Limit to avoid overwhelming the page

                tabOrder.slice(0, maxTabMarkers).forEach((el, idx) => {
                    try {
                        const rect = el.getBoundingClientRect();
                        if (rect.width === 0 || rect.height === 0) return;

                        const marker = document.createElement('div');
                        marker.className = 'structure-tab-marker';
                        marker.textContent = idx + 1;
                        marker.style.cssText = `
                            position: absolute; z-index: ${97000 + idx};
                            width: 22px; height: 22px; border-radius: 50%;
                            background: #0d6efd; color: white;
                            font-size: 10px; font-weight: 700; line-height: 22px;
                            text-align: center; pointer-events: none;
                            font-family: monospace;
                            box-shadow: 0 1px 3px rgba(0,0,0,0.4);
                            left: ${rect.left + window.scrollX - 11}px;
                            top: ${rect.top + window.scrollY - 11}px;
                        `;
                        document.body.appendChild(marker);

                        // Light outline on the element
                        el.style.outline = '2px dashed #0d6efd';
                        el.style.outlineOffset = '1px';
                    } catch(e) { /* skip */ }
                });

                // ── Heading Hierarchy ──
                const headings = document.querySelectorAll('h1, h2, h3, h4, h5, h6');
                const headingColors = {
                    H1: '#dc3545', H2: '#fd7e14', H3: '#ffc107',
                    H4: '#198754', H5: '#0d6efd', H6: '#6f42c1'
                };
                let lastLevel = 0;
                let headingIssues = 0;

                headings.forEach((h, idx) => {
                    const level = parseInt(h.tagName[1]);
                    const color = headingColors[h.tagName] || '#6c757d';
                    const skipped = level > lastLevel + 1 && lastLevel > 0;
                    if (skipped) headingIssues++;
                    lastLevel = level;

                    try {
                        const rect = h.getBoundingClientRect();
                        const indent = (level - 1) * 12;

                        const badge = document.createElement('div');
                        badge.className = 'structure-heading-badge';
                        badge.style.cssText = `
                            position: absolute; z-index: ${96000 + idx};
                            font-size: 11px; font-family: monospace; font-weight: 700;
                            color: white; padding: 2px 8px; border-radius: 3px;
                            white-space: nowrap; pointer-events: none;
                            line-height: 15px;
                            background: ${skipped ? '#dc3545' : color};
                            left: ${rect.left + window.scrollX + indent}px;
                            top: ${rect.top + window.scrollY - 20}px;
                        `;
                        badge.textContent = skipped
                            ? `⚠ ${h.tagName} (skipped from H${lastLevel > level ? level : level - 1})`
                            : `${h.tagName}: ${(h.textContent || '').trim().substring(0, 40)}`;
                        document.body.appendChild(badge);

                        h.style.outline = `2px solid ${color}`;
                        h.style.outlineOffset = '2px';
                    } catch(e) { /* skip */ }
                });

                // ── Landmark Regions ──
                const landmarks = document.querySelectorAll('header, nav, main, aside, footer, [role=""banner""], [role=""navigation""], [role=""main""], [role=""complementary""], [role=""contentinfo""]');
                landmarks.forEach((lm, idx) => {
                    try {
                        const rect = lm.getBoundingClientRect();
                        const role = lm.getAttribute('role') || lm.tagName.toLowerCase();

                        lm.style.outline = '2px dotted #6f42c1';
                        lm.style.outlineOffset = '3px';

                        const label = document.createElement('div');
                        label.className = 'structure-landmark-label';
                        label.textContent = `◆ ${role}`;
                        label.style.cssText = `
                            position: absolute; z-index: ${95000 + idx};
                            font-size: 10px; font-family: monospace; font-weight: 700;
                            color: white; background: #6f42c1;
                            padding: 1px 6px; border-radius: 3px;
                            pointer-events: none;
                            left: ${rect.right + window.scrollX - 80}px;
                            top: ${rect.top + window.scrollY}px;
                        `;
                        document.body.appendChild(label);
                    } catch(e) { /* skip */ }
                });

                // Summary banner
                const banner = document.createElement('div');
                banner.id = 'structure-overlay-banner';
                banner.innerHTML = `
                    <div style=""
                        position:fixed; top:0; left:0; right:0; z-index:999999;
                        background: #1a2744; color: white; padding: 8px 16px;
                        font: bold 13px/1.4 -apple-system, 'Segoe UI', Roboto, monospace;
                        display: flex; align-items: center; gap: 16px;
                        box-shadow: 0 2px 8px rgba(0,0,0,0.3);
                    "">
                        <span style=""font-size:16px"">🏗️</span>
                        <span>Page Structure</span>
                        <span style=""background:#0d6efd;padding:2px 8px;border-radius:10px;font-size:11px"">⇥ ${Math.min(tabOrder.length, maxTabMarkers)}${tabOrder.length > maxTabMarkers ? '+' : ''} Tab Stops</span>
                        <span style=""background:${headingIssues > 0 ? '#dc3545' : '#198754'};padding:2px 8px;border-radius:10px;font-size:11px"">H ${headings.length} Headings${headingIssues > 0 ? ` (${headingIssues} skipped)` : ''}</span>
                        <span style=""background:#6f42c1;padding:2px 8px;border-radius:10px;font-size:11px"">◆ ${landmarks.length} Landmarks</span>
                        <span style=""margin-left:auto; opacity:0.6; font-size:11px"">Tab Order · Headings · Landmarks</span>
                    </div>
                `;
                document.body.prepend(banner);
                document.body.style.marginTop = '40px';
            }
        ");
    }

    /// <summary>
    /// Remove the structure overlay markers, badges, and banner.
    /// </summary>
    private static async Task RemoveStructureOverlayAsync(IPage page)
    {
        await page.EvaluateAsync(@"
            () => {
                document.querySelectorAll('.structure-tab-marker, .structure-heading-badge, .structure-landmark-label').forEach(el => el.remove());
                const banner = document.getElementById('structure-overlay-banner');
                if (banner) banner.remove();
                document.body.style.marginTop = '';
                document.querySelectorAll('[style*=""outline""]').forEach(el => {
                    el.style.outline = '';
                    el.style.outlineOffset = '';
                });
            }
        ");
    }

    // ========================================================================
    // Color Vision Deficiency Simulations
    // ========================================================================

    /// <summary>
    /// Color vision deficiency types with their SVG feColorMatrix values.
    /// These are the same matrices Chrome DevTools uses for "Emulate vision deficiencies"
    /// (Machado et al. 2009 color transformation matrices).
    /// </summary>
    private static readonly Dictionary<string, (string Label, string Description, string Matrix)> ColorBlindTypes = new()
    {
        ["protanopia"] = (
            "Protanopia (No Red)",
            "~1% of males — cannot see red light. Reds appear dark/black, greens and yellows look similar.",
            "0.152286 1.052583 -0.204868 0 0  0.114503 0.786281 0.099216 0 0  -0.003882 -0.048116 1.051998 0 0  0 0 0 1 0"
        ),
        ["deuteranopia"] = (
            "Deuteranopia (No Green)",
            "~1% of males — cannot see green light. Most common severe type. Greens and reds look brownish-yellow.",
            "0.367322 0.860646 -0.227968 0 0  0.280085 0.672501 0.047413 0 0  -0.011820 0.042940 0.968881 0 0  0 0 0 1 0"
        ),
        ["tritanopia"] = (
            "Tritanopia (No Blue)",
            "Very rare (~0.003%). Cannot see blue light. Blues appear green, yellows appear pink/red.",
            "1.255528 -0.076749 -0.178779 0 0  -0.078411 0.930809 0.147602 0 0  0.004733 0.691367 0.303900 0 0  0 0 0 1 0"
        ),
        ["achromatopsia"] = (
            "Achromatopsia (Total Color Blindness)",
            "Extremely rare (~0.003%). Sees only grayscale. Tests whether your design works without any color.",
            "0.2126 0.7152 0.0722 0 0  0.2126 0.7152 0.0722 0 0  0.2126 0.7152 0.0722 0 0  0 0 0 1 0"
        ),
        ["protanomaly"] = (
            "Protanomaly (Weak Red)",
            "~1% of males — reduced red sensitivity. Milder than protanopia. Reds appear muted/brownish.",
            "0.458064 0.679578 -0.137642 0 0  0.092785 0.846313 0.060902 0 0  -0.007494 -0.016807 1.024301 0 0  0 0 0 1 0"
        ),
        ["deuteranomaly"] = (
            "Deuteranomaly (Weak Green)",
            "~5% of males — the most common CVD. Reduced green sensitivity. Greens appear brownish.",
            "0.547494 0.607765 -0.155259 0 0  0.181692 0.781742 0.036566 0 0  -0.010410 0.027275 0.983136 0 0  0 0 0 1 0"
        ),
        ["tritanomaly"] = (
            "Tritanomaly (Weak Blue)",
            "Very rare. Reduced blue sensitivity. Blues appear greenish, yellows appear lighter.",
            "1.017277 0.027029 -0.044306 0 0  -0.006113 0.958479 0.047634 0 0  0.006379 0.248708 0.744913 0 0  0 0 0 1 0"
        ),
    };

    /// <summary>
    /// Apply a color vision deficiency SVG filter to the entire page and add a label banner.
    /// Uses SVG feColorMatrix — the same technique Chrome DevTools uses.
    /// </summary>
    private static async Task InjectColorBlindFilterAsync(IPage page, string type, string label, string description, string matrix)
    {
        await page.EvaluateAsync($@"
            () => {{
                // Remove any previous CVD filter
                const prev = document.getElementById('cvd-filter-svg');
                if (prev) prev.remove();
                const prevBanner = document.getElementById('cvd-overlay-banner');
                if (prevBanner) prevBanner.remove();

                // Create SVG filter
                const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
                svg.id = 'cvd-filter-svg';
                svg.setAttribute('style', 'position:absolute;width:0;height:0;');
                svg.innerHTML = `
                    <defs>
                        <filter id=""cvd-filter"" color-interpolation-filters=""linearRGB"">
                            <feColorMatrix type=""matrix"" values=""{matrix}"" />
                        </filter>
                    </defs>
                `;
                document.body.appendChild(svg);

                // Apply filter to the entire page
                document.documentElement.style.filter = 'url(#cvd-filter)';

                // Add label banner
                const banner = document.createElement('div');
                banner.id = 'cvd-overlay-banner';
                banner.innerHTML = `
                    <div style=""
                        position:fixed; top:0; left:0; right:0; z-index:999999;
                        background: #2d2d2d; color: white; padding: 6px 16px;
                        font: bold 12px/1.4 -apple-system, 'Segoe UI', Roboto, monospace;
                        display: flex; align-items: center; gap: 12px;
                        box-shadow: 0 2px 8px rgba(0,0,0,0.3);
                        filter: none;
                    "">
                        <span style=""font-size:16px"">👁️</span>
                        <span style=""font-size:13px"">{label}</span>
                        <span style=""opacity:0.7; font-size:11px"">{description}</span>
                    </div>
                `;
                document.body.prepend(banner);
                document.body.style.marginTop = '36px';
            }}
        ");
    }

    /// <summary>
    /// Remove the color vision deficiency filter and banner.
    /// </summary>
    private static async Task RemoveColorBlindFilterAsync(IPage page)
    {
        await page.EvaluateAsync(@"
            () => {
                document.documentElement.style.filter = '';
                const svg = document.getElementById('cvd-filter-svg');
                if (svg) svg.remove();
                const banner = document.getElementById('cvd-overlay-banner');
                if (banner) banner.remove();
                document.body.style.marginTop = '';
            }
        ");
    }

    // ========================================================================
    // Screen Reader Text View
    // ========================================================================

    /// <summary>
    /// Transform the page into a "screen reader text view" — shows what a screen reader
    /// user experiences. Hides all visual styling, shows content in reading order with
    /// visible annotations for: alt text, aria-labels, roles, landmarks, heading levels.
    /// Images are replaced with their alt text (or "[NO ALT]" markers).
    /// </summary>
    private static async Task InjectScreenReaderViewAsync(IPage page)
    {
        await page.EvaluateAsync(@"
            () => {
                // Save original body HTML so we can restore later
                if (!window._srOriginalHtml) {
                    window._srOriginalHtml = document.body.innerHTML;
                    window._srOriginalStyles = document.body.getAttribute('style') || '';
                }

                // Walk the DOM and extract the accessible content tree
                const output = [];
                let imgCount = 0, altMissing = 0, ariaLabels = 0, landmarks = 0, headings = 0;

                function getAccessibleName(el) {
                    return el.getAttribute('aria-label')
                        || el.getAttribute('aria-labelledby')
                        || el.getAttribute('title')
                        || '';
                }

                function processNode(node, depth) {
                    if (node.nodeType === Node.TEXT_NODE) {
                        const text = (node.textContent || '').trim();
                        if (text) {
                            output.push({ type: 'text', content: text, depth });
                        }
                        return;
                    }

                    if (node.nodeType !== Node.ELEMENT_NODE) return;

                    const el = node;
                    const tag = el.tagName.toLowerCase();
                    const style = window.getComputedStyle(el);

                    // Skip hidden elements
                    if (style.display === 'none' || style.visibility === 'hidden') return;
                    if (el.getAttribute('aria-hidden') === 'true') return;

                    // Landmark annotations
                    const role = el.getAttribute('role');
                    const landmarkTags = { nav: 'navigation', main: 'main', header: 'banner', footer: 'contentinfo', aside: 'complementary' };
                    const landmarkRole = role || landmarkTags[tag];
                    if (landmarkRole && ['navigation', 'main', 'banner', 'contentinfo', 'complementary', 'search', 'form'].includes(landmarkRole)) {
                        output.push({ type: 'landmark', content: `[${landmarkRole.toUpperCase()}]`, depth });
                        landmarks++;
                    }

                    // Headings
                    if (/^h[1-6]$/.test(tag)) {
                        const level = tag[1];
                        output.push({ type: 'heading', content: `[H${level}] ${(el.textContent || '').trim()}`, depth, level });
                        headings++;
                        return; // Don't process children (text already captured)
                    }

                    // Images
                    if (tag === 'img') {
                        imgCount++;
                        const alt = el.getAttribute('alt');
                        if (alt !== null && alt.trim() !== '') {
                            output.push({ type: 'img-alt', content: `[IMG: ${alt}]`, depth });
                        } else if (alt === '') {
                            output.push({ type: 'img-decorative', content: '[IMG: decorative]', depth });
                        } else {
                            output.push({ type: 'img-noalt', content: '[IMG: ⚠ NO ALT TEXT]', depth });
                            altMissing++;
                        }
                        return;
                    }

                    // SVG / Icon elements
                    if (tag === 'svg' || tag === 'i' || (el.classList && el.classList.contains('icon'))) {
                        const name = getAccessibleName(el);
                        if (name) {
                            output.push({ type: 'icon-labeled', content: `[ICON: ${name}]`, depth });
                            ariaLabels++;
                        } else if (el.getAttribute('aria-hidden') !== 'true') {
                            output.push({ type: 'icon-unlabeled', content: '[ICON: unlabeled]', depth });
                        }
                        return;
                    }

                    // Links
                    if (tag === 'a') {
                        const text = (el.textContent || '').trim();
                        const ariaLabel = getAccessibleName(el);
                        const href = el.getAttribute('href') || '';
                        if (ariaLabel && !text) {
                            output.push({ type: 'link', content: `[LINK: ${ariaLabel}] → ${href}`, depth });
                            ariaLabels++;
                        } else if (text) {
                            output.push({ type: 'link', content: `[LINK: ${text}] → ${href}`, depth });
                        } else {
                            output.push({ type: 'link-empty', content: `[LINK: ⚠ EMPTY] → ${href}`, depth });
                        }
                        return;
                    }

                    // Buttons
                    if (tag === 'button' || role === 'button') {
                        const text = (el.textContent || '').trim();
                        const ariaLabel = getAccessibleName(el);
                        const label = ariaLabel || text || '⚠ EMPTY';
                        output.push({ type: 'button', content: `[BUTTON: ${label}]`, depth });
                        if (ariaLabel) ariaLabels++;
                        return;
                    }

                    // Form inputs
                    if (['input', 'select', 'textarea'].includes(tag)) {
                        const inputType = el.getAttribute('type') || 'text';
                        if (inputType === 'hidden') return;
                        const ariaLabel = getAccessibleName(el);
                        const id = el.id;
                        const labelEl = id ? document.querySelector(`label[for=""${id}""]`) : null;
                        const labelText = ariaLabel || (labelEl ? labelEl.textContent.trim() : '') || el.placeholder || '⚠ NO LABEL';
                        output.push({ type: 'input', content: `[INPUT ${inputType.toUpperCase()}: ${labelText}]`, depth });
                        return;
                    }

                    // Table
                    if (tag === 'table') {
                        const caption = el.querySelector('caption');
                        output.push({ type: 'table', content: `[TABLE${caption ? ': ' + caption.textContent.trim() : ''}]`, depth });
                    }

                    // ARIA labels on generic elements
                    if (role && !['presentation', 'none'].includes(role)) {
                        const ariaLabel = getAccessibleName(el);
                        if (ariaLabel) {
                            output.push({ type: 'aria', content: `[${role.toUpperCase()}: ${ariaLabel}]`, depth });
                            ariaLabels++;
                        }
                    }

                    // Process children
                    for (const child of el.childNodes) {
                        processNode(child, depth + 1);
                    }

                    // Close landmarks
                    if (landmarkRole && ['navigation', 'main', 'banner', 'contentinfo', 'complementary'].includes(landmarkRole)) {
                        output.push({ type: 'landmark-end', content: `[/${landmarkRole.toUpperCase()}]`, depth });
                    }
                }

                processNode(document.body, 0);

                // Build the text view HTML
                const typeStyles = {
                    'text':           'color:#e0e0e0;',
                    'heading':        'color:#58a6ff; font-weight:bold; font-size:{size}px;',
                    'link':           'color:#8b949e; text-decoration:underline;',
                    'link-empty':     'color:#f85149; text-decoration:underline;',
                    'button':         'color:#d2a8ff; font-weight:600;',
                    'img-alt':        'color:#7ee787; font-style:italic;',
                    'img-decorative': 'color:#6e7681; font-style:italic;',
                    'img-noalt':      'color:#f85149; font-weight:bold;',
                    'icon-labeled':   'color:#79c0ff; font-style:italic;',
                    'icon-unlabeled': 'color:#f0883e;',
                    'input':          'color:#d2a8ff;',
                    'table':          'color:#79c0ff; font-weight:600;',
                    'landmark':       'color:#f0883e; font-weight:bold; font-size:13px;',
                    'landmark-end':   'color:#f0883e; font-size:11px; opacity:0.6;',
                    'aria':           'color:#79c0ff; font-style:italic;'
                };

                const headingSizes = { '1': 22, '2': 18, '3': 15, '4': 13, '5': 12, '6': 11 };

                let html = '';
                for (const item of output) {
                    let style = typeStyles[item.type] || 'color:#e0e0e0;';
                    if (item.type === 'heading') {
                        style = style.replace('{size}', headingSizes[item.level] || '13');
                    }
                    const indent = Math.min(item.depth, 10) * 12;
                    const escaped = item.content.replace(/</g, '&lt;').replace(/>/g, '&gt;');
                    html += `<div style=""padding:1px 0 1px ${indent}px; ${style} font-family:'Cascadia Code','Fira Code',Consolas,monospace; font-size:12px; line-height:1.6; white-space:pre-wrap; word-break:break-word;"">${escaped}</div>\n`;
                }

                // Replace body content with text view
                document.body.innerHTML = `
                    <div id=""sr-view-banner"" style=""
                        position:fixed; top:0; left:0; right:0; z-index:999999;
                        background:#0d1117; color:white; padding:8px 16px;
                        font: bold 13px/1.4 -apple-system,'Segoe UI',Roboto,monospace;
                        display:flex; align-items:center; gap:16px;
                        box-shadow:0 2px 8px rgba(0,0,0,0.5);
                        border-bottom: 2px solid #30363d;
                    "">
                        <span style=""font-size:16px"">🔊</span>
                        <span>Screen Reader Text View</span>
                        <span style=""background:#58a6ff;padding:2px 8px;border-radius:10px;font-size:11px"">H ${headings}</span>
                        <span style=""background:${altMissing > 0 ? '#f85149' : '#7ee787'};color:${altMissing > 0 ? 'white' : '#000'};padding:2px 8px;border-radius:10px;font-size:11px"">${imgCount} imgs${altMissing > 0 ? ` (${altMissing} no alt!)` : ''}</span>
                        <span style=""background:#d2a8ff;color:#000;padding:2px 8px;border-radius:10px;font-size:11px"">${ariaLabels} ARIA labels</span>
                        <span style=""background:#f0883e;color:#000;padding:2px 8px;border-radius:10px;font-size:11px"">◆ ${landmarks} landmarks</span>
                        <span style=""margin-left:auto;opacity:0.5;font-size:11px"">Linearized accessible content tree</span>
                    </div>
                    <div style=""
                        margin-top:44px; padding:16px 24px;
                        background:#0d1117; min-height:100vh;
                    "">${html}</div>
                `;
                document.body.style.cssText = 'margin:0; padding:0; background:#0d1117;';
            }
        ");
    }

    /// <summary>
    /// Restore the original page content after screen reader text view.
    /// </summary>
    private static async Task RemoveScreenReaderViewAsync(IPage page)
    {
        await page.EvaluateAsync(@"
            () => {
                if (window._srOriginalHtml) {
                    document.body.innerHTML = window._srOriginalHtml;
                    document.body.setAttribute('style', window._srOriginalStyles || '');
                    window._srOriginalHtml = null;
                    window._srOriginalStyles = null;
                }
            }
        ");
    }

    /// <summary>
    /// Convert axe WCAG tag like "wcag111" to "1.1.1" or "wcag143" to "1.4.3".
    /// </summary>
    private static string? FormatWcagTag(string tag)
    {
        // Tags are like "wcag111", "wcag143", "wcag2411"
        var digits = tag.AsSpan(4); // skip "wcag"
        if (digits.Length < 3) return null;
        // Format: first digit . second digit . remaining digits
        return $"{digits[0]}.{digits[1]}.{digits[2..]}";
    }

    /// <summary>
    /// Run structural HTML checks against saved page.html content.
    /// Pure C# string/regex parsing — zero external dependencies.
    /// </summary>
    private static A11yToolResult RunHtmlCheck(string html, string url)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var issues = new List<A11yIssue>();

        issues.AddRange(CheckImgAlt(html));
        issues.AddRange(CheckHeadingOrder(html));
        issues.AddRange(CheckHtmlLang(html));
        issues.AddRange(CheckFormLabels(html));
        issues.AddRange(CheckEmptyLinks(html));
        issues.AddRange(CheckEmptyButtons(html));
        issues.AddRange(CheckSkipLink(html));
        issues.AddRange(CheckLandmarkMain(html));
        issues.AddRange(CheckLandmarkNav(html));
        issues.AddRange(CheckDivButton(html));
        issues.AddRange(CheckTabindexPositive(html));
        issues.AddRange(CheckMetaRefresh(html));
        issues.AddRange(CheckTableHeaders(html));

        sw.Stop();
        return new A11yToolResult
        {
            ToolName = "htmlcheck",
            Status = "completed",
            DurationMs = sw.ElapsedMilliseconds,
            Issues = issues
        };
    }

    // --- Individual htmlcheck rules ---

    private static IEnumerable<A11yIssue> CheckImgAlt(string html)
    {
        // Match <img> tags — check for alt attribute
        foreach (System.Text.RegularExpressions.Match m in
            System.Text.RegularExpressions.Regex.Matches(html, @"<img\b([^>]*)>", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var attrs = m.Groups[1].Value;
            // Skip if inside <noscript> or template elements (simple heuristic)
            if (attrs.Contains("aria-hidden=\"true\"", StringComparison.OrdinalIgnoreCase)) continue;

            if (!attrs.Contains("alt=", StringComparison.OrdinalIgnoreCase) &&
                !attrs.Contains("alt =", StringComparison.OrdinalIgnoreCase))
            {
                yield return new A11yIssue
                {
                    Tool = "htmlcheck", RuleId = "img-alt", CanonicalRuleId = "image-alt",
                    Severity = "serious", Message = "Image missing alt attribute",
                    Snippet = m.Value.Length > 150 ? m.Value[..150] + "..." : m.Value,
                    WcagCriteria = "1.1.1"
                };
            }
        }
    }

    private static IEnumerable<A11yIssue> CheckHeadingOrder(string html)
    {
        var headings = System.Text.RegularExpressions.Regex.Matches(
            html, @"<h([1-6])\b[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        int lastLevel = 0;
        foreach (System.Text.RegularExpressions.Match m in headings)
        {
            var level = int.Parse(m.Groups[1].Value);
            if (lastLevel > 0 && level > lastLevel + 1)
            {
                yield return new A11yIssue
                {
                    Tool = "htmlcheck", RuleId = "heading-order", CanonicalRuleId = "heading-order",
                    Severity = "moderate", Message = $"Heading level skipped: <h{lastLevel}> to <h{level}>",
                    Snippet = m.Value, WcagCriteria = "1.3.1"
                };
            }
            lastLevel = level;
        }

        // Check for missing h1
        if (headings.Count > 0 && !System.Text.RegularExpressions.Regex.IsMatch(
            html, @"<h1\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            yield return new A11yIssue
            {
                Tool = "htmlcheck", RuleId = "heading-missing-h1", CanonicalRuleId = "page-has-heading-one",
                Severity = "moderate", Message = "Page has headings but no <h1>",
                WcagCriteria = "1.3.1"
            };
        }
    }

    private static IEnumerable<A11yIssue> CheckHtmlLang(string html)
    {
        var htmlTag = System.Text.RegularExpressions.Regex.Match(
            html, @"<html\b([^>]*)>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!htmlTag.Success) yield break;

        var attrs = htmlTag.Groups[1].Value;

        if (!attrs.Contains("lang=", StringComparison.OrdinalIgnoreCase))
        {
            yield return new A11yIssue
            {
                Tool = "htmlcheck", RuleId = "html-lang", CanonicalRuleId = "html-has-lang",
                Severity = "serious", Message = "<html> element missing lang attribute",
                Snippet = htmlTag.Value, WcagCriteria = "3.1.1"
            };
        }
        else
        {
            // Check if lang value is valid (basic check)
            var langMatch = System.Text.RegularExpressions.Regex.Match(
                attrs, @"lang\s*=\s*[""']([^""']*)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (langMatch.Success && string.IsNullOrWhiteSpace(langMatch.Groups[1].Value))
            {
                yield return new A11yIssue
                {
                    Tool = "htmlcheck", RuleId = "html-lang-valid", CanonicalRuleId = "html-lang-valid",
                    Severity = "serious", Message = "lang attribute is empty",
                    Snippet = htmlTag.Value, WcagCriteria = "3.1.1"
                };
            }
        }
    }

    private static IEnumerable<A11yIssue> CheckFormLabels(string html)
    {
        foreach (System.Text.RegularExpressions.Match m in
            System.Text.RegularExpressions.Regex.Matches(html, @"<input\b([^>]*)>", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var attrs = m.Groups[1].Value;
            var typeMatch = System.Text.RegularExpressions.Regex.Match(
                attrs, @"type\s*=\s*[""']([^""']*)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var type = typeMatch.Success ? typeMatch.Groups[1].Value.ToLowerInvariant() : "text";

            if (type is "hidden" or "submit" or "button" or "image" or "reset") continue;

            // Has aria-label or aria-labelledby?
            if (attrs.Contains("aria-label", StringComparison.OrdinalIgnoreCase)) continue;

            // Has id with matching label?
            var idMatch = System.Text.RegularExpressions.Regex.Match(
                attrs, @"id\s*=\s*[""']([^""']*)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (idMatch.Success && !string.IsNullOrWhiteSpace(idMatch.Groups[1].Value))
            {
                if (html.Contains($"for=\"{idMatch.Groups[1].Value}\"", StringComparison.OrdinalIgnoreCase) ||
                    html.Contains($"for='{idMatch.Groups[1].Value}'", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            yield return new A11yIssue
            {
                Tool = "htmlcheck", RuleId = "label-missing", CanonicalRuleId = "label",
                Severity = "serious", Message = "Form input has no associated label or aria-label",
                Snippet = m.Value.Length > 150 ? m.Value[..150] + "..." : m.Value,
                WcagCriteria = "1.3.1"
            };
        }
    }

    private static IEnumerable<A11yIssue> CheckEmptyLinks(string html)
    {
        foreach (System.Text.RegularExpressions.Match m in
            System.Text.RegularExpressions.Regex.Matches(html, @"<a\b([^>]*)>(.*?)</a>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline))
        {
            var attrs = m.Groups[1].Value;
            var content = m.Groups[2].Value;

            if (attrs.Contains("aria-label", StringComparison.OrdinalIgnoreCase)) continue;
            if (attrs.Contains("aria-labelledby", StringComparison.OrdinalIgnoreCase)) continue;

            // Strip HTML tags from content and check if empty
            var textContent = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
            if (string.IsNullOrWhiteSpace(textContent))
            {
                // Could be an image link — check for img with alt inside
                if (content.Contains("<img", StringComparison.OrdinalIgnoreCase) &&
                    content.Contains("alt=", StringComparison.OrdinalIgnoreCase)) continue;

                yield return new A11yIssue
                {
                    Tool = "htmlcheck", RuleId = "link-empty", CanonicalRuleId = "link-name",
                    Severity = "serious", Message = "Link has no text content or accessible name",
                    Snippet = m.Value.Length > 150 ? m.Value[..150] + "..." : m.Value,
                    WcagCriteria = "4.1.2"
                };
            }
        }
    }

    private static IEnumerable<A11yIssue> CheckEmptyButtons(string html)
    {
        foreach (System.Text.RegularExpressions.Match m in
            System.Text.RegularExpressions.Regex.Matches(html, @"<button\b([^>]*)>(.*?)</button>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline))
        {
            var attrs = m.Groups[1].Value;
            var content = m.Groups[2].Value;

            if (attrs.Contains("aria-label", StringComparison.OrdinalIgnoreCase)) continue;
            if (attrs.Contains("aria-labelledby", StringComparison.OrdinalIgnoreCase)) continue;

            var textContent = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
            if (string.IsNullOrWhiteSpace(textContent))
            {
                yield return new A11yIssue
                {
                    Tool = "htmlcheck", RuleId = "button-empty", CanonicalRuleId = "button-name",
                    Severity = "serious", Message = "Button has no text content or accessible name",
                    Snippet = m.Value.Length > 150 ? m.Value[..150] + "..." : m.Value,
                    WcagCriteria = "4.1.2"
                };
            }
        }
    }

    private static IEnumerable<A11yIssue> CheckSkipLink(string html)
    {
        // Look for a skip link in the first 2000 chars of body
        var bodyStart = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (bodyStart < 0) yield break;

        var topSection = html.Substring(bodyStart, Math.Min(2000, html.Length - bodyStart));

        if (!topSection.Contains("#main", StringComparison.OrdinalIgnoreCase) &&
            !topSection.Contains("#content", StringComparison.OrdinalIgnoreCase) &&
            !topSection.Contains("skip", StringComparison.OrdinalIgnoreCase))
        {
            yield return new A11yIssue
            {
                Tool = "htmlcheck", RuleId = "skip-link-missing", CanonicalRuleId = "skip-link",
                Severity = "moderate", Message = "No skip-to-content link found near top of page",
                WcagCriteria = "2.4.1"
            };
        }
    }

    private static IEnumerable<A11yIssue> CheckLandmarkMain(string html)
    {
        if (!html.Contains("<main", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("role=\"main\"", StringComparison.OrdinalIgnoreCase))
        {
            yield return new A11yIssue
            {
                Tool = "htmlcheck", RuleId = "landmark-main", CanonicalRuleId = "landmark-one-main",
                Severity = "moderate", Message = "Page has no <main> landmark",
                WcagCriteria = "1.3.1"
            };
        }
    }

    private static IEnumerable<A11yIssue> CheckLandmarkNav(string html)
    {
        if (!html.Contains("<nav", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("role=\"navigation\"", StringComparison.OrdinalIgnoreCase))
        {
            yield return new A11yIssue
            {
                Tool = "htmlcheck", RuleId = "landmark-nav", CanonicalRuleId = "landmark-nav",
                Severity = "minor", Message = "Page has no <nav> landmark",
                WcagCriteria = "1.3.1"
            };
        }
    }

    private static IEnumerable<A11yIssue> CheckDivButton(string html)
    {
        foreach (System.Text.RegularExpressions.Match m in
            System.Text.RegularExpressions.Regex.Matches(html, @"<div\b([^>]*onclick[^>]*)>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var attrs = m.Groups[1].Value;
            if (attrs.Contains("role=", StringComparison.OrdinalIgnoreCase)) continue;

            yield return new A11yIssue
            {
                Tool = "htmlcheck", RuleId = "div-button", CanonicalRuleId = "div-button",
                Severity = "moderate", Message = "<div> with onclick but no role — use <button> instead",
                Snippet = m.Value.Length > 150 ? m.Value[..150] + "..." : m.Value,
                WcagCriteria = "4.1.2"
            };
        }
    }

    private static IEnumerable<A11yIssue> CheckTabindexPositive(string html)
    {
        foreach (System.Text.RegularExpressions.Match m in
            System.Text.RegularExpressions.Regex.Matches(html, @"tabindex\s*=\s*[""'](\d+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            if (int.TryParse(m.Groups[1].Value, out var val) && val > 0)
            {
                yield return new A11yIssue
                {
                    Tool = "htmlcheck", RuleId = "tabindex-positive", CanonicalRuleId = "tabindex",
                    Severity = "moderate", Message = $"tabindex=\"{val}\" disrupts natural tab order",
                    Snippet = m.Value, WcagCriteria = "2.4.3"
                };
            }
        }
    }

    private static IEnumerable<A11yIssue> CheckMetaRefresh(string html)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(html,
            @"<meta\b[^>]*http-equiv\s*=\s*[""']refresh[""'][^>]*>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            yield return new A11yIssue
            {
                Tool = "htmlcheck", RuleId = "meta-refresh", CanonicalRuleId = "meta-refresh",
                Severity = "moderate", Message = "Page uses <meta http-equiv=\"refresh\"> which can be disorienting",
                WcagCriteria = "2.2.1"
            };
        }
    }

    private static IEnumerable<A11yIssue> CheckTableHeaders(string html)
    {
        // Find <table> elements that contain <td> but no <th>
        foreach (System.Text.RegularExpressions.Match m in
            System.Text.RegularExpressions.Regex.Matches(html, @"<table\b[^>]*>(.*?)</table>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline))
        {
            var tableContent = m.Groups[1].Value;
            // Skip tables with role="presentation" or role="none" (layout tables)
            var tableTag = System.Text.RegularExpressions.Regex.Match(m.Value, @"<table\b([^>]*)>");
            if (tableTag.Success && (tableTag.Groups[1].Value.Contains("role=\"presentation\"", StringComparison.OrdinalIgnoreCase) ||
                tableTag.Groups[1].Value.Contains("role=\"none\"", StringComparison.OrdinalIgnoreCase)))
                continue;

            if (tableContent.Contains("<td", StringComparison.OrdinalIgnoreCase) &&
                !tableContent.Contains("<th", StringComparison.OrdinalIgnoreCase))
            {
                yield return new A11yIssue
                {
                    Tool = "htmlcheck", RuleId = "table-header-missing", CanonicalRuleId = "td-has-header",
                    Severity = "moderate", Message = "Data table has no header cells (<th>)",
                    Snippet = m.Value.Length > 150 ? m.Value[..150] + "..." : m.Value,
                    WcagCriteria = "1.3.1"
                };
            }
        }
    }

    // ========================================================================
    // WAVE API integration
    // ========================================================================

    /// <summary>
    /// WAVE API severity categories mapped to our severity levels.
    /// WAVE reporttype=4 returns categories: error, contrast, alert.
    /// </summary>
    private static readonly Dictionary<string, string> WaveCategorySeverity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["error"] = "serious",
        ["contrast"] = "serious",
        ["alert"] = "moderate"
    };

    /// <summary>
    /// WAVE item IDs mapped to canonical axe-core rule IDs for cross-tool consensus.
    /// See https://wave.webaim.org/api/docs
    /// </summary>
    private static readonly Dictionary<string, string> WaveToCanonical = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alt_missing"] = "image-alt",
        ["alt_spacer"] = "image-alt",
        ["alt_link_missing"] = "image-alt",
        ["alt_input_missing"] = "input-image-alt",
        ["label_missing"] = "label",
        ["label_empty"] = "label",
        ["language_missing"] = "html-has-lang",
        ["language_invalid"] = "html-lang-valid",
        ["link_empty"] = "link-name",
        ["button_empty"] = "button-name",
        ["heading_missing"] = "page-has-heading-one",
        ["heading_skipped"] = "heading-order",
        ["contrast"] = "color-contrast",
        ["title_invalid"] = "document-title",
        ["th_empty"] = "td-has-header",
        ["table_layout"] = "table-fake-caption",
        ["aria_reference_broken"] = "aria-valid-attr-value",
        ["aria_menu_broken"] = "aria-required-children",
        ["fieldset_missing"] = "fieldset",
        ["legend_missing"] = "fieldset",
        ["select_missing_label"] = "select-name",
        ["blink"] = "blink",
        ["marquee"] = "marquee",
        ["link_skip"] = "skip-link",
        ["noscript"] = "meta-refresh",
    };

    /// <summary>
    /// WAVE item IDs mapped to WCAG success criteria.
    /// </summary>
    private static readonly Dictionary<string, string> WaveToWcag = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alt_missing"] = "1.1.1",
        ["alt_spacer"] = "1.1.1",
        ["alt_link_missing"] = "1.1.1",
        ["alt_input_missing"] = "1.1.1",
        ["label_missing"] = "1.3.1",
        ["label_empty"] = "1.3.1",
        ["language_missing"] = "3.1.1",
        ["language_invalid"] = "3.1.1",
        ["link_empty"] = "2.4.4",
        ["button_empty"] = "4.1.2",
        ["heading_skipped"] = "1.3.1",
        ["contrast"] = "1.4.3",
        ["title_invalid"] = "2.4.2",
        ["fieldset_missing"] = "1.3.1",
        ["select_missing_label"] = "1.3.1",
    };

    /// <summary>
    /// Call the WAVE API for a given URL and return normalized results.
    /// Requires a WAVE API key (https://wave.webaim.org/api/).
    /// Uses reporttype=4 (JSON with categories + items).
    /// </summary>
    private static async Task<A11yToolResult> RunWaveApiAsync(string pageUrl, string apiKey)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new A11yToolResult { ToolName = "wave" };

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var encodedUrl = Uri.EscapeDataString(pageUrl);
            var requestUrl = $"https://wave.webaim.org/api/request?key={apiKey}&url={encodedUrl}&reporttype=4";

            var response = await http.GetStringAsync(requestUrl);
            var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            // Check for API-level errors
            if (root.TryGetProperty("status", out var statusProp) &&
                statusProp.TryGetProperty("success", out var successProp) &&
                !successProp.GetBoolean())
            {
                var errorMsg = "WAVE API returned failure";
                if (root.TryGetProperty("status", out var st) &&
                    st.TryGetProperty("error", out var errProp))
                {
                    errorMsg = errProp.GetString() ?? errorMsg;
                }

                result.Status = "error";
                result.ErrorMessage = errorMsg;
                sw.Stop();
                result.DurationMs = sw.ElapsedMilliseconds;
                return result;
            }

            // Parse categories: error, contrast, alert
            if (root.TryGetProperty("categories", out var categories))
            {
                foreach (var category in categories.EnumerateObject())
                {
                    var catName = category.Name; // "error", "contrast", "alert"
                    if (!WaveCategorySeverity.TryGetValue(catName, out var severity))
                        continue; // skip "feature", "structure", "aria" categories

                    if (!category.Value.TryGetProperty("items", out var items))
                        continue;

                    foreach (var item in items.EnumerateObject())
                    {
                        var itemId = item.Name; // e.g. "alt_missing", "contrast"
                        var itemCount = 0;
                        var description = "";

                        if (item.Value.TryGetProperty("count", out var countProp))
                            itemCount = countProp.GetInt32();
                        if (item.Value.TryGetProperty("description", out var descProp))
                            description = descProp.GetString() ?? "";

                        // Map to canonical rule ID
                        var canonicalId = WaveToCanonical.TryGetValue(itemId, out var c) ? c : itemId;
                        var wcag = WaveToWcag.TryGetValue(itemId, out var w) ? w : null;

                        // Create one issue per count (WAVE reports counts, not individual nodes)
                        for (int i = 0; i < Math.Max(1, itemCount); i++)
                        {
                            result.Issues.Add(new A11yIssue
                            {
                                Tool = "wave",
                                RuleId = itemId,
                                CanonicalRuleId = canonicalId,
                                Severity = severity,
                                Message = description,
                                HelpUrl = $"https://wave.webaim.org/a11y/{itemId}",
                                WcagCriteria = wcag
                            });
                        }
                    }
                }
            }

            result.Status = "completed";
        }
        catch (HttpRequestException ex)
        {
            result.Status = "error";
            result.ErrorMessage = $"WAVE API request failed: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            result.Status = "error";
            result.ErrorMessage = "WAVE API request timed out";
        }
        catch (Exception ex)
        {
            result.Status = "error";
            result.ErrorMessage = $"WAVE API error: {ex.Message}";
        }

        sw.Stop();
        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    // ========================================================================
    // Accessibility: Consensus ranking + merge
    // ========================================================================

    /// <summary>
    /// Maps tool-specific rule IDs to canonical (axe-core) IDs for cross-tool matching.
    /// </summary>
    private static readonly Dictionary<string, string> RuleNormalization = new(StringComparer.OrdinalIgnoreCase)
    {
        // htmlcheck → canonical
        ["img-alt"] = "image-alt",
        ["html-lang"] = "html-has-lang",
        ["html-lang-valid"] = "html-lang-valid",
        ["label-missing"] = "label",
        ["link-empty"] = "link-name",
        ["button-empty"] = "button-name",
        ["skip-link-missing"] = "skip-link",
        ["landmark-main"] = "landmark-one-main",
        ["heading-missing-h1"] = "page-has-heading-one",
        ["table-header-missing"] = "td-has-header",
        // wave → canonical (covers any WAVE IDs that flow through general normalization)
        ["alt_missing"] = "image-alt",
        ["alt_spacer"] = "image-alt",
        ["alt_link_missing"] = "image-alt",
        ["alt_input_missing"] = "input-image-alt",
        ["label_missing"] = "label",
        ["label_empty"] = "label",
        ["language_missing"] = "html-has-lang",
        ["language_invalid"] = "html-lang-valid",
        ["heading_skipped"] = "heading-order",
        ["heading_missing"] = "page-has-heading-one",
        ["link_skip"] = "skip-link",
        ["th_empty"] = "td-has-header",
        ["select_missing_label"] = "select-name",
    };

    /// <summary>
    /// Rules that specific tools CANNOT check. Used to calculate "capable" denominator.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> ToolCannotCheck = new()
    {
        ["htmlcheck"] = ["color-contrast", "color-contrast-enhanced", "aria-allowed-attr",
            "aria-hidden-body", "aria-required-attr", "aria-roles", "aria-valid-attr",
            "aria-valid-attr-value", "focus-order-semantics", "target-size"],
        ["pa11y"] = ["skip-link", "landmark-one-main", "landmark-nav", "div-button",
            "tabindex", "meta-refresh", "td-has-header"],
        ["wave"] = ["landmark-one-main", "landmark-nav", "div-button",
            "tabindex", "meta-refresh", "focus-order-semantics", "target-size"]
    };

    private static string NormalizeRuleId(string ruleId)
        => RuleNormalization.TryGetValue(ruleId, out var canonical) ? canonical : ruleId;

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 0,
        "serious" => 1,
        "moderate" => 2,
        "minor" => 3,
        _ => 4
    };

    private static string SeverityEmoji(string severity) => severity switch
    {
        "critical" => "🔴",
        "serious" => "🟠",
        "moderate" => "🟡",
        "minor" => "🔵",
        _ => "⚪"
    };

    /// <summary>
    /// Merge results from all tools, compute consensus ranking, and write output files.
    /// </summary>
    private static A11yPageSummary MergeA11yResults(
        A11yToolResult axeResult,
        A11yToolResult htmlResult,
        A11yToolResult waveResult,
        string pageDir)
    {
        var summary = new A11yPageSummary();
        var allIssues = new List<A11yIssue>();
        var toolResults = new[] { axeResult, htmlResult, waveResult };

        foreach (var tool in toolResults)
        {
            if (tool.Status == "completed")
            {
                summary.ToolsRun.Add(tool.ToolName);
                allIssues.AddRange(tool.Issues);

                // Per-tool severity counts
                summary.ByTool[tool.ToolName] = new A11yToolSummary
                {
                    Total = tool.Issues.Count,
                    Critical = tool.Issues.Count(i => i.Severity == "critical"),
                    Serious = tool.Issues.Count(i => i.Severity == "serious"),
                    Moderate = tool.Issues.Count(i => i.Severity == "moderate"),
                    Minor = tool.Issues.Count(i => i.Severity == "minor")
                };
            }
            else if (tool.Status == "skipped")
            {
                summary.ToolsSkipped.Add(tool.ToolName);
            }
            else if (tool.Status == "error")
            {
                summary.ToolsSkipped.Add(tool.ToolName);
            }

            // Write individual tool file
            var toolFile = Path.Combine(pageDir, $"a11y-{tool.ToolName}.json");
            File.WriteAllText(toolFile, JsonSerializer.Serialize(tool, JsonOptions));
        }

        // Severity totals (across all tools, not deduplicated)
        summary.TotalViolations = allIssues.Count;
        summary.Critical = allIssues.Count(i => i.Severity == "critical");
        summary.Serious = allIssues.Count(i => i.Severity == "serious");
        summary.Moderate = allIssues.Count(i => i.Severity == "moderate");
        summary.Minor = allIssues.Count(i => i.Severity == "minor");

        // Normalize rule IDs for cross-tool matching
        foreach (var issue in allIssues)
        {
            if (string.IsNullOrEmpty(issue.CanonicalRuleId))
                issue.CanonicalRuleId = NormalizeRuleId(issue.RuleId);
        }

        // Build consensus ranking: group by canonical rule, score by how many tools found it
        var completedTools = summary.ToolsRun;
        var ruleGroups = allIssues
            .GroupBy(i => i.CanonicalRuleId)
            .ToList();

        var ranked = new List<A11yRankedRule>();
        foreach (var group in ruleGroups)
        {
            var ruleId = group.Key;
            var toolsFound = group.Select(i => i.Tool).Distinct().ToList();

            // Determine which completed tools are capable of checking this rule
            var capableTools = completedTools
                .Where(t => !(ToolCannotCheck.TryGetValue(t, out var cantCheck) && cantCheck.Contains(ruleId)))
                .ToList();

            var capableCount = Math.Max(1, capableTools.Count);
            var score = (double)toolsFound.Count / capableCount;

            var representative = group.First();
            ranked.Add(new A11yRankedRule
            {
                CanonicalRuleId = ruleId,
                Severity = representative.Severity,
                ToolsFound = toolsFound,
                Consensus = $"{toolsFound.Count}/{capableCount}",
                ConfidenceScore = score,
                Confidence = score >= 0.8 ? "high" : score >= 0.5 ? "medium" : "low",
                TotalInstances = group.Count(),
                Message = representative.Message,
                HelpUrl = representative.HelpUrl,
                WcagCriteria = representative.WcagCriteria,
                ExampleSnippet = representative.Snippet
            });
        }

        // Sort: confidence desc → severity rank → instance count desc
        summary.RankedRules = ranked
            .OrderByDescending(r => r.ConfidenceScore)
            .ThenBy(r => SeverityRank(r.Severity))
            .ThenByDescending(r => r.TotalInstances)
            .Select((r, i) => { r.Rank = i + 1; return r; })
            .ToList();

        // Write summary and ranked files
        File.WriteAllText(Path.Combine(pageDir, "a11y-summary.json"),
            JsonSerializer.Serialize(summary, JsonOptions));

        return summary;
    }

    // ========================================================================
    // SSL certificate capture
    // ========================================================================

    /// <summary>
    /// Connect to a site's HTTPS endpoint, capture the SSL/TLS certificate,
    /// extract Subject Alternative Names (SANs), and return structured cert info.
    /// </summary>
    private static async Task<CertInfo> DownloadCertificateAsync(string siteUrl)
    {
        var certInfo = new CertInfo();
        var uri = new Uri(siteUrl);

        if (uri.Scheme != "https")
        {
            certInfo.ErrorMessage = "Not HTTPS";
            return certInfo;
        }

        try
        {
            X509Certificate2? captured = null;

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    if (cert != null)
                        captured = new X509Certificate2(cert);
                    return true; // Accept all — we just want the cert
                }
            };

            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            // Just do a HEAD request — we only need the TLS handshake
            var request = new HttpRequestMessage(HttpMethod.Head, uri);
            await http.SendAsync(request);

            if (captured == null)
            {
                certInfo.ErrorMessage = "No certificate returned";
                return certInfo;
            }

            certInfo.Subject = captured.Subject;
            certInfo.Issuer = captured.Issuer;
            certInfo.Thumbprint = captured.Thumbprint;
            certInfo.SerialNumber = captured.SerialNumber;
            certInfo.NotBefore = captured.NotBefore;
            certInfo.NotAfter = captured.NotAfter;
            certInfo.DaysUntilExpiry = (int)(captured.NotAfter - DateTime.UtcNow).TotalDays;
            certInfo.SignatureAlgorithm = captured.SignatureAlgorithm.FriendlyName ?? "";
            certInfo.KeySizeBits = captured.PublicKey.Key.KeySize;

            // Extract SANs from the certificate
            var sanExtension = captured.Extensions["2.5.29.17"]; // Subject Alternative Name OID
            if (sanExtension != null)
            {
                var sanData = new System.Security.Cryptography.AsnEncodedData("2.5.29.17", sanExtension.RawData);
                var sanString = sanData.Format(multiLine: true);

                // Parse lines like "DNS Name=*.wsu.edu" or "DNS Name=wsu.edu"
                foreach (var line in sanString.Split('\n', '\r'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("DNS Name=", StringComparison.OrdinalIgnoreCase))
                    {
                        var dns = trimmed["DNS Name=".Length..].Trim();
                        if (!string.IsNullOrWhiteSpace(dns))
                        {
                            certInfo.SubjectAlternativeNames.Add(dns);

                            // Categorize: WSU vs other
                            if (dns.EndsWith(".wsu.edu", StringComparison.OrdinalIgnoreCase) ||
                                dns.Equals("wsu.edu", StringComparison.OrdinalIgnoreCase))
                            {
                                certInfo.SanWsuDomains.Add(dns);
                            }
                            else
                            {
                                certInfo.SanOtherDomains.Add(dns);
                            }
                        }
                    }
                }
            }

            certInfo.SubjectAlternativeNames.Sort(StringComparer.OrdinalIgnoreCase);
            certInfo.SanWsuDomains.Sort(StringComparer.OrdinalIgnoreCase);
            certInfo.SanOtherDomains.Sort(StringComparer.OrdinalIgnoreCase);

            captured.Dispose();
        }
        catch (Exception ex)
        {
            certInfo.ErrorMessage = ex.Message;
        }

        return certInfo;
    }

    // ========================================================================
    // Image extraction
    // ========================================================================

    /// <summary>
    /// Find all images on the page, download them to an /images subfolder,
    /// and track them in the result.
    /// </summary>
    private static async Task DownloadPageImagesAsync(
        IPage page,
        string pageDir,
        PageResult result,
        List<string> actions,
        List<string> infoLines)
    {
        // Get all img elements with src attributes
        var imgElements = await page.EvaluateAsync<ImageInfo[]>(@"
            () => Array.from(document.querySelectorAll('img[src]')).map(img => ({
                src: img.src,
                alt: img.alt || '',
                width: img.naturalWidth,
                height: img.naturalHeight
            })).filter(img => img.src && !img.src.startsWith('data:'))
        ");

        if (imgElements == null || imgElements.Length == 0)
        {
            infoLines.Add("Images: 0 found");
            actions.Add("No images found on page");
            return;
        }

        // Deduplicate by URL
        var uniqueImages = imgElements
            .GroupBy(i => i.Src)
            .Select(g => g.First())
            .ToList();

        // Catalog images by URL only — no files are downloaded
        foreach (var img in uniqueImages)
        {
            result.Images.Add(new ImageEntry
            {
                FileName = "",
                SourceUrl = img.Src,
                AltText = img.Alt,
                FileSize = 0
            });
        }

        infoLines.Add($"Images: {uniqueImages.Count} cataloged (referenced by URL, not downloaded)");
        actions.Add($"Cataloged {uniqueImages.Count} images by URL (no download)");
    }

    // ========================================================================
    // Auth flow
    // ========================================================================

    /// <summary>
    /// Capture a named screenshot, track it in the result, and log the action.
    /// Returns the step number assigned.
    /// </summary>
    private static async Task<int> TakeScreenshotAsync(
        IPage page,
        string pageDir,
        PageResult result,
        List<string> actions,
        string label)
    {
        var stepNumber = result.Screenshots.Count + 1;
        var fileName = $"{stepNumber:D2}-{SanitizeFileName(label)}.jpg";
        var filePath = Path.Combine(pageDir, fileName);

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = filePath,
            FullPage = true,
            Type = ScreenshotType.Jpeg,
            Quality = 60
        });

        var fileSize = new FileInfo(filePath).Length;

        result.Screenshots.Add(new ScreenshotEntry
        {
            FileName = fileName,
            Label = label,
            FileSize = fileSize,
            StepNumber = stepNumber
        });

        result.ScreenshotSize += fileSize;
        actions.Add($"Screenshot #{stepNumber}: {label} ({PathSanitizer.FormatBytes(fileSize)})");

        return stepNumber;
    }

    /// <summary>
    /// Log an overlay screenshot error without stopping the scan.
    /// </summary>
    private static void LogOverlayError(Uri siteUri, string pagePath, string overlayName, Exception ex, List<string> actions)
    {
        Console.Error.WriteLine($"  [{siteUri.Host}] {pagePath} — {overlayName} failed: {ex.Message}");
        actions.Add($"{overlayName} screenshot failed: {ex.Message}");
    }

    private static string SanitizeFileName(string label)
    {
        // Convert "Page loaded (after settle)" → "page-loaded-after-settle"
        var sanitized = label.ToLowerInvariant();
        sanitized = sanitized.Replace(' ', '-').Replace('(', '-').Replace(')', '-');
        // Collapse multiple dashes and trim
        while (sanitized.Contains("--"))
        {
            sanitized = sanitized.Replace("--", "-");
        }

        return sanitized.Trim('-');
    }

    /// <summary>
    /// Attempt to fill and submit a login form on the current page.
    /// Takes before/after screenshots at each interaction step.
    /// Returns true if a form was found and submitted.
    /// </summary>
    private static async Task<bool> TryAuthFlowAsync(
        IPage page, SiteCredential cred, ScannerConfig config,
        string pageDir, PageResult result, List<string> actions)
    {
        var usernameSelectors = new[]
        {
            // ID-based selectors (FreeExamples, FreeCRM, FreeGLBA patterns)
            "#login-email",
            "#loginEmail",
            // Name-based selectors
            "input[name='username']",
            "input[name='Username']",
            "input[name='email']",
            "input[name='Email']",
            "input[name='Input.Email']",
            "input[name='Input.Username']",
            // Type-based selectors
            "input[type='email']",
            // Attribute-based selectors
            "input[autocomplete='username']",
            "input[autocomplete='email']",
            "input[placeholder*='user' i]",
            "input[placeholder*='email' i]",
            // Fallback: first visible text input near a password field
            "input[type='text'][id*='mail' i]",
            "input[type='text'][id*='user' i]",
            "input[type='text'][id*='login' i]",
        };

        var passwordSelectors = new[]
        {
            "#login-password",
            "#loginPassword",
            "input[name='password']",
            "input[name='Password']",
            "input[name='Input.Password']",
            "input[type='password']"
        };

        ILocator? usernameField = null;
        ILocator? passwordField = null;

        foreach (var selector in usernameSelectors)
        {
            try
            {
                var locator = page.Locator(selector).First;
                if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync())
                {
                    usernameField = locator;
                    actions.Add($"Found username field via: {selector}");
                    break;
                }
            }
            catch { /* Continue to next selector */ }
        }

        foreach (var selector in passwordSelectors)
        {
            try
            {
                var locator = page.Locator(selector).First;
                if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync())
                {
                    passwordField = locator;
                    actions.Add($"Found password field via: {selector}");
                    break;
                }
            }
            catch { /* Continue to next selector */ }
        }

        if (usernameField == null || passwordField == null)
        {
            return false;
        }

        // Screenshot: before filling
        await TakeScreenshotAsync(page, pageDir, result, actions, "auth-form-detected");

        await usernameField.FillAsync(cred.Username);
        actions.Add($"Filled username field with '{cred.Username}'");

        await passwordField.FillAsync(cred.Password);
        actions.Add("Filled password field with ****");

        // Screenshot: after filling, before submit
        await TakeScreenshotAsync(page, pageDir, result, actions, "auth-form-filled");

        var submitSelectors = new[]
        {
            "button[type='submit']",
            "input[type='submit']",
            "button:has-text('Log in')",
            "button:has-text('Login')",
            "button:has-text('Sign in')"
        };

        var submitted = false;
        foreach (var selector in submitSelectors)
        {
            try
            {
                var locator = page.Locator(selector).First;
                if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync())
                {
                    await locator.ClickAsync();
                    actions.Add($"Clicked submit button via: {selector}");
                    submitted = true;
                    break;
                }
            }
            catch { /* Continue to next selector */ }
        }

        if (!submitted)
        {
            await passwordField.PressAsync("Enter");
            actions.Add("Pressed Enter on password field (no submit button found)");
        }

        await page.WaitForTimeoutAsync(config.SettleDelayMs);
        actions.Add($"Waited {config.SettleDelayMs}ms for post-login settle");

        // Screenshot: after login result
        await TakeScreenshotAsync(page, pageDir, result, actions, "auth-result");

        return true;
    }

    // ========================================================================
    // URL and folder helpers
    // ========================================================================

    /// <summary>
    /// Resolve a page path to a full URL. Handles both relative ("/about/")
    /// and absolute ("https://other.site.com/page") paths.
    /// </summary>
    private static string ResolvePageUrl(Uri siteUri, string pagePath)
    {
        if (pagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            pagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return pagePath;
        }

        // Relative path — combine with site root
        return new Uri(siteUri, pagePath).ToString();
    }

    /// <summary>
    /// Convert a page path to a flat folder name.
    /// "/" → "_root", "/site/page1" → "site_page1", "/?c=A" → "_qc-A"
    /// </summary>
    private static string PagePathToFolderName(string pagePath)
    {
        // Strip protocol if full URL was given
        if (pagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            pagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(pagePath);
            pagePath = uri.PathAndQuery;
        }

        var trimmed = pagePath.Trim('/');

        if (string.IsNullOrEmpty(trimmed))
        {
            return "_root";
        }

        // Replace path separators and query string chars with safe alternatives
        var folderName = trimmed
            .Replace('/', '_')
            .Replace("?", "_q")
            .Replace('&', '_')
            .Replace('=', '-');

        // Strip any remaining invalid filesystem characters
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            folderName = folderName.Replace(c, '-');
        }

        return folderName;
    }

    // ========================================================================
    // Config loading
    // ========================================================================

    /// <summary>
    /// Build a ScannerConfig from environment variables (AppHost mode).
    /// Reads BASE_URL for the site, CSV_PATH for pages, TENANT_CODE for URL prefixes,
    /// and LOGIN_USERNAME/LOGIN_PASSWORD for credentials.
    /// </summary>
    private static async Task<ScannerConfig?> BuildConfigFromEnvAsync(string baseUrl)
    {
        var csvPath = Environment.GetEnvironmentVariable("CSV_PATH") ?? "";
        var tenantCode = Environment.GetEnvironmentVariable("TENANT_CODE") ?? "tenant1";
        var loginUsername = Environment.GetEnvironmentVariable("LOGIN_USERNAME") ?? "admin";
        var loginPassword = Environment.GetEnvironmentVariable("LOGIN_PASSWORD") ?? "admin";

        Console.WriteLine($"  [AppHost Mode] BASE_URL: {baseUrl}");
        Console.WriteLine($"  [AppHost Mode] CSV_PATH: {csvPath}");
        Console.WriteLine($"  [AppHost Mode] TENANT_CODE: {tenantCode}");

        // Build page list from CSV (same format as EndpointMapper output)
        var pages = new List<string>();
        if (!string.IsNullOrWhiteSpace(csvPath) && File.Exists(csvPath))
        {
            var lines = await File.ReadAllLinesAsync(csvPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                var rawRoute = parts[1].Trim('"').Trim();
                if (string.IsNullOrWhiteSpace(rawRoute)) continue;

                // Substitute {TenantCode}
                var route = rawRoute;
                bool hadTenantCode = false;
                if (route.Contains("{TenantCode}", StringComparison.OrdinalIgnoreCase))
                {
                    route = route.Replace("{TenantCode}", tenantCode, StringComparison.OrdinalIgnoreCase);
                    hadTenantCode = true;
                }

                // Skip routes that still have parameters
                if (route.Contains('{')) continue;

                // Prefer tenant-prefixed routes
                if (hadTenantCode || !pages.Any(p => p.Equals($"/{tenantCode}{route}", StringComparison.OrdinalIgnoreCase)))
                {
                    // Remove bare route if tenant version is being added
                    if (hadTenantCode)
                    {
                        var bareRoute = route.Replace($"/{tenantCode}", "", StringComparison.OrdinalIgnoreCase);
                        pages.Remove(bareRoute);
                    }
                    if (!pages.Contains(route, StringComparer.OrdinalIgnoreCase))
                    {
                        pages.Add(route);
                    }
                }
            }

            Console.WriteLine($"  [AppHost Mode] Loaded {pages.Count} pages from CSV");
        }
        else
        {
            Console.WriteLine("  [AppHost Mode] No CSV found — scanning root page only");
        }

        // Append extra pages from EXTRA_PAGES env var (semicolon-separated)
        // These cover pages the EndpointMapper can't auto-discover:
        // parameterized routes, settings pages with specific IDs, etc.
        var extraPagesEnv = Environment.GetEnvironmentVariable("EXTRA_PAGES") ?? "";
        if (!string.IsNullOrWhiteSpace(extraPagesEnv))
        {
            var extraPages = extraPagesEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var addedCount = 0;
            foreach (var extra in extraPages)
            {
                var route = extra;
                if (route.Contains("{TenantCode}", StringComparison.OrdinalIgnoreCase))
                {
                    route = route.Replace("{TenantCode}", tenantCode, StringComparison.OrdinalIgnoreCase);
                }
                if (!pages.Contains(route, StringComparer.OrdinalIgnoreCase))
                {
                    pages.Add(route);
                    addedCount++;
                }
            }
            Console.WriteLine($"  [AppHost Mode] Added {addedCount} extra pages from EXTRA_PAGES env var");
        }

        Console.WriteLine($"  [AppHost Mode] Total pages to scan: {pages.Count}");

        // Build site config with credentials
        var siteConfig = new SiteConfig
        {
            Pages = pages,
            Credentials = [new SiteCredential { Username = loginUsername, Password = loginPassword }]
        };

        // Ensure base URL has trailing slash
        if (!baseUrl.EndsWith('/')) baseUrl += "/";

        return new ScannerConfig
        {
            Sites = new Dictionary<string, SiteConfig> { [baseUrl] = siteConfig },
            SettleDelayMs = 3000,
            TimeoutMs = 30000,
            MaxConcurrency = 5,
            Headless = true
        };
    }

    private static async Task<ScannerConfig?> LoadConfigAsync()
    {
        var exeDir = AppContext.BaseDirectory;
        var projectDir = FindProjectDir(exeDir);

        var candidates = new[]
        {
            Path.Combine(exeDir, "appsettings.json"),
            Path.Combine(projectDir, "appsettings.json")
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            Console.WriteLine($"  Loading config: {path}");

            var json = await File.ReadAllTextAsync(path);
            var root = JsonSerializer.Deserialize<AppSettingsRoot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (root?.Scanner != null)
            {
                return root.Scanner;
            }
        }

        Console.Error.WriteLine("  appsettings.json not found.");
        return null;
    }

    /// <summary>
    /// Walk up from bin/Debug/net10.0 to find the project directory (where .csproj lives).
    /// </summary>
    private static string FindProjectDir(string startDir)
    {
        var dir = startDir;

        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.csproj").Length > 0)
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}

// ============================================================================
// Configuration Models (maps to appsettings.json)
// ============================================================================

internal class AppSettingsRoot
{
    public ScannerConfig Scanner { get; set; } = new();
}

internal class ScannerConfig
{
    public int SettleDelayMs { get; set; } = 3000;
    public int TimeoutMs { get; set; } = 10000;
    public int MaxConcurrency { get; set; } = 5;
    public bool Headless { get; set; } = true;
    public string WcagLevel { get; set; } = "wcag21aa";
    public string? WaveApiKey { get; set; }
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    public Dictionary<string, SiteConfig> Sites { get; set; } = new();
}

internal class SiteConfig
{
    public List<SiteCredential> Credentials { get; set; } = [];
    public List<string> Pages { get; set; } = [];
}

internal class SiteCredential
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

// ============================================================================
// Result Models
// ============================================================================

internal class SiteResult
{
    public string Url { get; set; } = "";
    public string FolderName { get; set; } = "";
    public List<PageResult> Pages { get; set; } = [];
    public CertInfo? Certificate { get; set; }
}

/// <summary>
/// SSL/TLS certificate information captured from a site.
/// </summary>
internal class CertInfo
{
    public string Subject { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Thumbprint { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public int DaysUntilExpiry { get; set; }
    public string SignatureAlgorithm { get; set; } = "";
    public int KeySizeBits { get; set; }
    public List<string> SubjectAlternativeNames { get; set; } = [];
    public List<string> SanWsuDomains { get; set; } = [];
    public List<string> SanOtherDomains { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

internal class PageResult
{
    public string PagePath { get; set; } = "";
    public string FullUrl { get; set; } = "";
    public string? FinalUrl { get; set; }
    public string? Title { get; set; }
    public string FolderName { get; set; } = "";
    public int StatusCode { get; set; }
    public long HtmlSize { get; set; }
    public long ScreenshotSize { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CredentialUsed { get; set; }
    public List<string> ConsoleErrors { get; set; } = [];
    public List<string> ConsoleWarnings { get; set; } = [];
    public List<ScreenshotEntry> Screenshots { get; set; } = [];
    public List<ImageEntry> Images { get; set; } = [];
    public long ImagesTotalSize { get; set; }
    public DateTime CapturedAt { get; set; }
    public A11yPageSummary? A11ySummary { get; set; }
}

/// <summary>
/// Tracks a single screenshot taken during page scanning.
/// </summary>
internal class ScreenshotEntry
{
    public string FileName { get; set; } = "";
    public string Label { get; set; } = "";
    public long FileSize { get; set; }
    public int StepNumber { get; set; }
}

/// <summary>
/// Tracks a single image downloaded from a scanned page.
/// </summary>
internal class ImageEntry
{
    public string FileName { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string AltText { get; set; } = "";
    public long FileSize { get; set; }
}

/// <summary>
/// Used for deserializing image info from Playwright's EvaluateAsync.
/// </summary>
internal class ImageInfo
{
    [JsonPropertyName("src")]
    public string Src { get; set; } = "";

    [JsonPropertyName("alt")]
    public string Alt { get; set; } = "";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

// ============================================================================
// Accessibility Models
// ============================================================================

/// <summary>
/// A single accessibility issue normalized across all tools.
/// </summary>
internal class A11yIssue
{
    public string Tool { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string CanonicalRuleId { get; set; } = "";
    public string Severity { get; set; } = "moderate";
    public string Message { get; set; } = "";
    public string? Selector { get; set; }
    public string? Snippet { get; set; }
    public string? HelpUrl { get; set; }
    public string? WcagCriteria { get; set; }
}

/// <summary>
/// Result from a single a11y tool run on one page.
/// </summary>
internal class A11yToolResult
{
    public string ToolName { get; set; } = "";
    public string Status { get; set; } = "completed";
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public List<A11yIssue> Issues { get; set; } = [];
}

/// <summary>
/// Merged accessibility results across all tools for one page.
/// </summary>
internal class A11yPageSummary
{
    public List<string> ToolsRun { get; set; } = [];
    public List<string> ToolsSkipped { get; set; } = [];
    public int TotalViolations { get; set; }
    public int Critical { get; set; }
    public int Serious { get; set; }
    public int Moderate { get; set; }
    public int Minor { get; set; }
    public Dictionary<string, A11yToolSummary> ByTool { get; set; } = new();
    public List<A11yRankedRule> RankedRules { get; set; } = [];
}

/// <summary>
/// Per-tool severity counts for metadata.json output.
/// </summary>
internal class A11yToolSummary
{
    public int Total { get; set; }
    public int Critical { get; set; }
    public int Serious { get; set; }
    public int Moderate { get; set; }
    public int Minor { get; set; }
}

/// <summary>
/// A rule ranked by cross-tool consensus.
/// </summary>
internal class A11yRankedRule
{
    public int Rank { get; set; }
    public string CanonicalRuleId { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Consensus { get; set; } = "";
    public double ConfidenceScore { get; set; }
    public string Confidence { get; set; } = "";
    public List<string> ToolsFound { get; set; } = [];
    public int TotalInstances { get; set; }
    public string Message { get; set; } = "";
    public string? HelpUrl { get; set; }
    public string? WcagCriteria { get; set; }
    public string? ExampleSnippet { get; set; }
}

/// <summary>
/// Deserialization model for axe-core violation JSON.
/// </summary>
internal class AxeViolation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("impact")]
    public string? Impact { get; set; }

    [JsonPropertyName("help")]
    public string Help { get; set; } = "";

    [JsonPropertyName("helpUrl")]
    public string? HelpUrl { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("nodes")]
    public List<AxeNode> Nodes { get; set; } = [];
}

/// <summary>
/// Deserialization model for an axe-core violation node.
/// </summary>
internal class AxeNode
{
    [JsonPropertyName("html")]
    public string Html { get; set; } = "";

    [JsonPropertyName("target")]
    public List<string> Target { get; set; } = [];

    [JsonPropertyName("failureSummary")]
    public string? FailureSummary { get; set; }
}
