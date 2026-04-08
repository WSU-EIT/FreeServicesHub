using FreeExamples.Client;
using Microsoft.Extensions.Configuration;

namespace FreeExamples.TestClient;

/// <summary>
/// Test console app that exercises the FreeExamples.Client NuGet client
/// against the API Key middleware-protected endpoints.
/// Pattern from: FreeGLBA.TestClientWithNugetPackage
/// 
/// Usage:
///   1. Start the FreeExamples server
///   2. Open the API Key Demo page and generate a key
///   3. Run: dotnet user-secrets set "FreeExamples:ApiKey" "your-key-here"
///   4. Run this project
/// </summary>
internal class Program
{
    private static int _passCount = 0;
    private static int _failCount = 0;

    static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       FreeExamples API Client Test Suite                     ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<Program>(optional: true)
            .AddCommandLine(args)
            .Build();

        var endpoint = configuration["FreeExamples:Endpoint"] ?? "";
        var apiKey = configuration["FreeExamples:ApiKey"] ?? "";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey)) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: Endpoint and ApiKey are required.");
            Console.WriteLine();
            Console.WriteLine("Configure via user secrets:");
            Console.WriteLine("  dotnet user-secrets set \"FreeExamples:Endpoint\" \"https://localhost:7271\"");
            Console.WriteLine("  dotnet user-secrets set \"FreeExamples:ApiKey\" \"your-api-key\"");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Endpoint: {endpoint}");
        Console.WriteLine($"API Key:  {apiKey[..Math.Min(10, apiKey.Length)]}...");
        Console.WriteLine();

        // ============================================================
        // SECTION 1: AUTHORIZED ACCESS (Valid API Key)
        // ============================================================
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("SECTION 1: AUTHORIZED ACCESS (Valid API Key)");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine();

        using var client = new FreeExamplesClient(endpoint, apiKey);

        // Test 1: Ping
        await RunTestAsync("Test 1: PingAsync", async () =>
        {
            var response = await client.PingAsync();

            if (!response.Success)
                throw new Exception($"Expected success but got: {response.Message}");

            if (response.Message != "pong")
                throw new Exception($"Expected 'pong' but got: {response.Message}");

            Console.WriteLine($"    Message: {response.Message}");
            Console.WriteLine($"    Authenticated as: {response.AuthenticatedAs}");
            return true;
        });

        // Test 2: PostData with message
        await RunTestAsync("Test 2: PostDataAsync (with message)", async () =>
        {
            var response = await client.PostDataAsync(new ApiTestRequest
            {
                Message = "Hello from TestClient!"
            });

            if (!response.Success)
                throw new Exception($"Expected success but got: {response.Message}");

            Console.WriteLine($"    Response: {response.Message}");
            Console.WriteLine($"    Authenticated as: {response.AuthenticatedAs}");
            Console.WriteLine($"    Timestamp: {response.Timestamp:O}");
            return true;
        });

        // Test 3: PostData with empty message
        await RunTestAsync("Test 3: PostDataAsync (empty message)", async () =>
        {
            var response = await client.PostDataAsync(new ApiTestRequest());

            if (!response.Success)
                throw new Exception("Expected success for empty message");

            Console.WriteLine($"    Response: {response.Message}");
            return true;
        });

        // Test 4: TryPostDataAsync (fire-and-forget)
        await RunTestAsync("Test 4: TryPostDataAsync (fire-and-forget)", async () =>
        {
            var result = await client.TryPostDataAsync(new ApiTestRequest
            {
                Message = "Fire and forget test"
            });

            Console.WriteLine($"    Result: {result}");
            return result;
        });

        // Test 5: Multiple rapid requests
        await RunTestAsync("Test 5: Rapid sequential requests (5x)", async () =>
        {
            for (int i = 1; i <= 5; i++) {
                var response = await client.PostDataAsync(new ApiTestRequest
                {
                    Message = $"Rapid request #{i}"
                });
                if (!response.Success) throw new Exception($"Request #{i} failed");
            }
            Console.WriteLine("    All 5 requests succeeded");
            return true;
        });

        // ============================================================
        // SECTION 2: ERROR HANDLING (Invalid/Missing Keys)
        // ============================================================
        Console.WriteLine();
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("SECTION 2: ERROR HANDLING (Invalid/Missing Keys)");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine();

        // Test 6: Invalid API key
        await RunTestAsync("Test 6: Invalid API key throws AuthenticationException", async () =>
        {
            using var badClient = new FreeExamplesClient(endpoint, "this-is-not-a-valid-key");
            try {
                await badClient.PingAsync();
                throw new Exception("Expected FreeExamplesAuthenticationException but none was thrown");
            } catch (FreeExamplesAuthenticationException ex) {
                Console.WriteLine($"    Correctly caught: {ex.Message}");
                Console.WriteLine($"    Status code: {ex.StatusCode}");
                return true;
            }
        });

        // Test 7: Empty API key fails validation
        await RunTestAsync("Test 7: Empty API key fails options validation", async () =>
        {
            await Task.CompletedTask;
            try {
                using var emptyClient = new FreeExamplesClient(endpoint, "");
                throw new Exception("Expected ArgumentException but none was thrown");
            } catch (ArgumentException ex) {
                Console.WriteLine($"    Correctly caught: {ex.Message}");
                return true;
            }
        });

        // Test 8: TryPostDataAsync with bad key returns false
        await RunTestAsync("Test 8: TryPostDataAsync with bad key returns false", async () =>
        {
            using var badClient = new FreeExamplesClient(endpoint, "bad-key");
            var result = await badClient.TryPostDataAsync(new ApiTestRequest { Message = "should fail" });

            if (result)
                throw new Exception("Expected false but got true");

            Console.WriteLine("    Correctly returned false (no exception thrown)");
            return true;
        });

        // ============================================================
        // SUMMARY
        // ============================================================
        Console.WriteLine();
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("TEST SUMMARY");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  PASSED: {_passCount}");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  FAILED: {_failCount}");
        Console.ResetColor();
        Console.WriteLine($"  TOTAL:  {_passCount + _failCount}");
        Console.WriteLine();

        if (_failCount == 0) {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("All tests passed!");
        } else {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{_failCount} test(s) failed!");
        }
        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine("Check the API Key Demo page request log to see all requests.");
    }

    static async Task RunTestAsync(string testName, Func<Task<bool>> test)
    {
        Console.Write($"{testName}... ");
        try {
            var result = await test();
            if (result) {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED");
                Console.ResetColor();
                _passCount++;
            } else {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED");
                Console.ResetColor();
                _failCount++;
            }
        } catch (Exception ex) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"FAILED - {ex.Message}");
            Console.ResetColor();
            _failCount++;
        }
    }
}
