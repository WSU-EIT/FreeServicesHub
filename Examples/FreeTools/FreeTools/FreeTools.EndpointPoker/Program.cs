using System.Collections.Concurrent;
using System.Text;
using FreeTools.Core;

namespace FreeTools.EndpointPoker;

internal class Program
{
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
        var tenantCode = Environment.GetEnvironmentVariable("TENANT_CODE") ?? "";

        // Default to 10 threads for parallel processing
        var maxThreads = Math.Max(1, CliArgs.GetEnvOrArgInt("MAX_THREADS", args, 3, 10));

        ConsoleOutput.PrintBanner("EndpointPoker (FreeTools)");
        ConsoleOutput.PrintConfig("Base URL", baseUrl);
        ConsoleOutput.PrintConfig("CSV Path", csvPath);
        ConsoleOutput.PrintConfig("Output directory", outputDir);
        ConsoleOutput.PrintConfig("Max threads", maxThreads.ToString());
        if (!string.IsNullOrWhiteSpace(tenantCode))
            ConsoleOutput.PrintConfig("Tenant Code", tenantCode);
        ConsoleOutput.PrintDivider();

        if (!File.Exists(csvPath))
        {
            Console.Error.WriteLine($"CSV file not found: {csvPath}");
            return 1;
        }

        var (routes, skippedRoutes) = await RouteParser.ParseRoutesFromCsvFileAsync(
            csvPath, tenantCode: string.IsNullOrWhiteSpace(tenantCode) ? null : tenantCode);

        foreach (var skipped in skippedRoutes)
        {
            Console.WriteLine($"  [SKIP] {skipped} — has route parameters");
        }

        if (skippedRoutes.Count > 0)
        {
            Console.WriteLine($"Skipped {skippedRoutes.Count} routes with parameters.");
        }

        if (routes.Count == 0)
        {
            Console.WriteLine("No testable routes found in CSV.");
            return 0;
        }

        Console.WriteLine($"Found {routes.Count} testable routes with {maxThreads} parallel workers.");

        Directory.CreateDirectory(outputDir);

        var httpClientHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        using var httpClient = new HttpClient(httpClientHandler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        var totalCount = routes.Count;
        var connectionErrorCount = 0;
        var httpErrorCount = 0;
        var successCount = 0;

        // Track results by index for ordered output
        var results = new ConcurrentDictionary<int, PokeResult>();
        var nextIndexToWrite = 0;
        var writeLock = new object();

        var semaphore = new SemaphoreSlim(maxThreads);

        var tasks = routes.Select((route, index) => Task.Run(async () =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result = await PokeRouteAsync(httpClient, route, index, totalCount, baseUrl, outputDir);
                
                // Store result
                results[index] = result;

                // Update counters
                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref successCount);
                }
                else if (result.IsHttpError)
                {
                    Interlocked.Increment(ref httpErrorCount);
                }
                else if (result.IsConnectionError)
                {
                    Interlocked.Increment(ref connectionErrorCount);
                }

                // Try to write results in order
                WriteResultsInOrder(results, ref nextIndexToWrite, writeLock);
            }
            finally
            {
                semaphore.Release();
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Write any remaining results
        WriteResultsInOrder(results, ref nextIndexToWrite, writeLock, flush: true);

        Console.WriteLine();
        Console.WriteLine($"Done. Processed {totalCount} routes:");
        Console.WriteLine($"  - Successful (2xx): {successCount}");
        Console.WriteLine($"  - HTTP errors (4xx/5xx): {httpErrorCount}");
        Console.WriteLine($"  - Connection errors: {connectionErrorCount}");

        Console.WriteLine();
        Console.WriteLine("=== Verifying Blazor framework file MIME types ===");
        var mimeTestPassed = await VerifyBlazorFrameworkMimeTypesAsync(httpClient, baseUrl);

        if (connectionErrorCount > 0)
        {
            Console.Error.WriteLine($"FAILED: {connectionErrorCount} connection error(s) occurred (server unreachable).");
            return 1;
        }

        if (!mimeTestPassed)
        {
            Console.WriteLine("WARNING: MIME type checks had issues (non-fatal).");
        }

        if (httpErrorCount > 0)
        {
            Console.WriteLine($"INFO: {httpErrorCount} routes returned HTTP 4xx/5xx (expected for auth-protected routes).");
        }

        Console.WriteLine("Completed successfully.");
        return 0;
    }

    private static async Task<PokeResult> PokeRouteAsync(
        HttpClient httpClient,
        string route,
        int index,
        int totalCount,
        string baseUrl,
        string outputDir)
    {
        var result = new PokeResult
        {
            Index = index,
            Route = route,
            Number = index + 1,
            TotalCount = totalCount
        };

        var url = RouteParser.BuildUrl(baseUrl, route);
        result.Url = url;

        try
        {
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var statusCode = (int)response.StatusCode;

            result.StatusCode = statusCode;

            var filePath = PathSanitizer.GetOutputFilePath(outputDir, route, "default.html");
            PathSanitizer.EnsureDirectoryExists(filePath);

            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
            result.FilePath = filePath;
            result.FileSize = content.Length;

            if (statusCode >= 200 && statusCode < 300)
            {
                result.IsSuccess = true;
            }
            else if (statusCode >= 400)
            {
                result.IsHttpError = true;
            }
        }
        catch (Exception ex)
        {
            result.IsConnectionError = true;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static void WriteResultsInOrder(
        ConcurrentDictionary<int, PokeResult> results,
        ref int nextIndexToWrite,
        object writeLock,
        bool flush = false)
    {
        lock (writeLock)
        {
            while (results.TryGetValue(nextIndexToWrite, out var result))
            {
                WriteResult(result);
                results.TryRemove(nextIndexToWrite, out _);
                nextIndexToWrite++;
            }

            if (flush && results.Count > 0)
            {
                foreach (var kvp in results.OrderBy(k => k.Key))
                {
                    WriteResult(kvp.Value);
                }
                results.Clear();
            }
        }
    }

    private static void WriteResult(PokeResult result)
    {
        Console.WriteLine($"[{result.Number}/{result.TotalCount}] {result.Route}");
        
        if (result.IsConnectionError)
        {
            Console.WriteLine($"  !! Error: {result.ErrorMessage}");
        }
        else
        {
            Console.WriteLine($"  -> Status: {result.StatusCode}, Saved: {Path.GetFileName(result.FilePath)} ({result.FileSize:N0} bytes)");
        }
    }

    private static async Task<bool> VerifyBlazorFrameworkMimeTypesAsync(HttpClient httpClient, string baseUrl)
    {
        var criticalFiles = new Dictionary<string, string>
        {
            { "/_framework/blazor.web.js", "application/javascript" },
        };

        var allPassed = true;

        foreach (var (path, expectedMimeType) in criticalFiles)
        {
            var url = RouteParser.BuildUrl(baseUrl, path);
            try
            {
                var response = await httpClient.GetAsync(url);
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"  ⚠ WARN: {path} returned status {(int)response.StatusCode}");
                    allPassed = false;
                    continue;
                }

                if (contentType.StartsWith(expectedMimeType, StringComparison.OrdinalIgnoreCase) ||
                    contentType.Equals("text/javascript", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  ✓ PASS: {path} -> {contentType}");
                }
                else
                {
                    Console.WriteLine($"  ⚠ WARN: {path} has MIME type '{contentType}', expected '{expectedMimeType}'");
                    allPassed = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ WARN: {path} -> Error: {ex.Message}");
                allPassed = false;
            }
        }

        if (allPassed)
        {
            Console.WriteLine("All Blazor framework MIME type checks passed.");
        }
        else
        {
            Console.WriteLine("Some Blazor framework MIME type checks had warnings.");
        }

        return allPassed;
    }
}

// Result tracking for ordered output
internal class PokeResult
{
    public int Index { get; set; }
    public int Number { get; set; }
    public int TotalCount { get; set; }
    public string Route { get; set; } = "";
    public string Url { get; set; } = "";
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsHttpError { get; set; }
    public bool IsConnectionError { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FilePath { get; set; }
    public int FileSize { get; set; }
}
