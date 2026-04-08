using FreeGLBA.Client;
using Microsoft.Extensions.Configuration;

Console.WriteLine("FreeGLBA Test Client - Comprehensive Test Suite");
Console.WriteLine("================================================\n");

// Load configuration from appsettings.json, user secrets, and environment variables
// Environment variables are used by Aspire AppHost to override settings
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()  // Aspire passes config via env vars (FreeGLBA__Endpoint)
    .Build();

var endpoint = configuration["FreeGLBA:Endpoint"] ?? "https://localhost:7271";
var apiKey = configuration["FreeGLBA:ApiKey"] ?? "";

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: API key not configured!");
    Console.WriteLine();
    Console.WriteLine("Please set the API key using user secrets:");
    Console.WriteLine("  dotnet user-secrets set \"FreeGLBA:ApiKey\" \"your-api-key-here\"");
    Console.ResetColor();
    return;
}

Console.WriteLine($"Endpoint: {endpoint}");
Console.WriteLine($"API Key:  {apiKey[..Math.Min(10, apiKey.Length)]}...\n");

var passCount = 0;
var failCount = 0;

// ============================================================
// SECTION 1: AUTHORIZED ACCESS TESTS (Valid API Key)
// ============================================================
Console.WriteLine("=" .PadRight(60, '='));
Console.WriteLine("SECTION 1: AUTHORIZED ACCESS TESTS (Valid API Key)");
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine();

using var client = new GlbaClient(endpoint, apiKey);

// Test 1: LogAccessAsync - Single event
await RunTestAsync("Test 1: LogAccessAsync (single event)", async () =>
{
    var response = await client.LogAccessAsync(new GlbaEventRequest
    {
        SourceEventId = $"TEST-{Guid.NewGuid()}",
        AccessedAt = DateTime.UtcNow,
        UserId = "test-user",
        UserName = "Test User",
        UserEmail = "test@example.edu",
        UserDepartment = "IT",
        SubjectId = "STU-TEST-001",
        SubjectType = "Student",
        DataCategory = "Financial",
        AccessType = "View",
        Purpose = "Testing FreeGLBA client",
        IpAddress = "127.0.0.1"
    });

    if (!response.IsSuccess)
        throw new Exception($"Expected success but got status: {response.Status}");

    Console.WriteLine($"    Event ID: {response.EventId}");
    return true;
});

// Test 2: LogAccessAsync - Bulk subjects
await RunTestAsync("Test 2: LogAccessAsync (bulk subjects)", async () =>
{
    var response = await client.LogAccessAsync(new GlbaEventRequest
    {
        SourceEventId = $"TEST-BULK-{Guid.NewGuid()}",
        AccessedAt = DateTime.UtcNow,
        UserId = "test-user",
        UserName = "Test User",
        SubjectId = "BULK",
        SubjectIds = ["STU-001", "STU-002", "STU-003", "STU-004", "STU-005"],
        AccessType = "Export",
        Purpose = "Testing bulk export"
    });

    if (!response.IsSuccess)
        throw new Exception($"Expected success but got status: {response.Status}");

    if (response.SubjectCount != 5)
        throw new Exception($"Expected SubjectCount=5 but got {response.SubjectCount}");

    Console.WriteLine($"    Event ID: {response.EventId}, SubjectCount: {response.SubjectCount}");
    return true;
});

// Test 3: LogAccessBatchAsync
await RunTestAsync("Test 3: LogAccessBatchAsync (batch of 3 events)", async () =>
{
    var batch = new List<GlbaEventRequest>
    {
        new() { SourceEventId = $"BATCH-1-{Guid.NewGuid()}", UserId = "user1", SubjectId = "STU-B01", AccessType = "View", AccessedAt = DateTime.UtcNow },
        new() { SourceEventId = $"BATCH-2-{Guid.NewGuid()}", UserId = "user2", SubjectId = "STU-B02", AccessType = "View", AccessedAt = DateTime.UtcNow },
        new() { SourceEventId = $"BATCH-3-{Guid.NewGuid()}", UserId = "user3", SubjectId = "STU-B03", AccessType = "Export", AccessedAt = DateTime.UtcNow }
    };

    var response = await client.LogAccessBatchAsync(batch);

    if (response.Accepted != 3)
        throw new Exception($"Expected 3 accepted but got {response.Accepted}");

    Console.WriteLine($"    Accepted: {response.Accepted}, Rejected: {response.Rejected}, Duplicate: {response.Duplicate}");
    return true;
});

// Test 4: TryLogAccessAsync
await RunTestAsync("Test 4: TryLogAccessAsync (fire-and-forget)", async () =>
{
    var success = await client.TryLogAccessAsync(new GlbaEventRequest
    {
        AccessedAt = DateTime.UtcNow,
        UserId = "test-user",
        SubjectId = "STU-TRY-001",
        AccessType = "View",
        Purpose = "Testing TryLogAccessAsync"
    });

    if (!success)
        throw new Exception("TryLogAccessAsync returned false");

    Console.WriteLine($"    Success: {success}");
    return true;
});

// Test 5: LogViewAsync (convenience method)
await RunTestAsync("Test 5: LogViewAsync (convenience method)", async () =>
{
    var response = await client.LogViewAsync(
        userId: "test-user",
        subjectId: "STU-VIEW-001",
        userName: "Test User");

    if (!response.IsSuccess)
        throw new Exception($"Expected success but got status: {response.Status}");

    Console.WriteLine($"    Event ID: {response.EventId}");
    return true;
});

// Test 6: LogExportAsync (convenience method)
await RunTestAsync("Test 6: LogExportAsync (convenience method)", async () =>
{
    var response = await client.LogExportAsync(
        userId: "test-user",
        subjectId: "STU-EXPORT-001",
        purpose: "Testing export logging",
        userName: "Test User");

    if (!response.IsSuccess)
        throw new Exception($"Expected success but got status: {response.Status}");

    Console.WriteLine($"    Event ID: {response.EventId}");
    return true;
});

// Test 7: LogBulkViewAsync (convenience method)
await RunTestAsync("Test 7: LogBulkViewAsync (convenience method)", async () =>
{
    var response = await client.LogBulkViewAsync(
        userId: "test-user",
        subjectIds: ["STU-BV-001", "STU-BV-002", "STU-BV-003"],
        purpose: "Testing bulk view logging",
        userName: "Test User");

    if (!response.IsSuccess)
        throw new Exception($"Expected success but got status: {response.Status}");

    if (response.SubjectCount != 3)
        throw new Exception($"Expected SubjectCount=3 but got {response.SubjectCount}");

    Console.WriteLine($"    Event ID: {response.EventId}, SubjectCount: {response.SubjectCount}");
    return true;
});

// Test 8: LogBulkExportAsync (convenience method)
await RunTestAsync("Test 8: LogBulkExportAsync (convenience method)", async () =>
{
    var response = await client.LogBulkExportAsync(
        userId: "test-user",
        subjectIds: ["STU-BE-001", "STU-BE-002", "STU-BE-003", "STU-BE-004"],
        purpose: "Testing bulk export logging",
        userName: "Test User",
        dataCategory: "Financial Aid",
        agreementText: "I acknowledge accessing protected data under GLBA.");

    if (!response.IsSuccess)
        throw new Exception($"Expected success but got status: {response.Status}");

    if (response.SubjectCount != 4)
        throw new Exception($"Expected SubjectCount=4 but got {response.SubjectCount}");

    Console.WriteLine($"    Event ID: {response.EventId}, SubjectCount: {response.SubjectCount}");
    return true;
});

// Test 9: Duplicate detection
await RunTestAsync("Test 9: Duplicate event detection", async () =>
{
    var sourceEventId = $"DUP-TEST-{Guid.NewGuid()}";

    // First submission should succeed
    var response1 = await client.LogAccessAsync(new GlbaEventRequest
    {
        SourceEventId = sourceEventId,
        AccessedAt = DateTime.UtcNow,
        UserId = "test-user",
        SubjectId = "STU-DUP-001",
        AccessType = "View"
    });

    if (!response1.IsSuccess)
        throw new Exception($"First submission failed: {response1.Status}");

    // Second submission with same SourceEventId should be duplicate
    try
    {
        await client.LogAccessAsync(new GlbaEventRequest
        {
            SourceEventId = sourceEventId,
            AccessedAt = DateTime.UtcNow,
            UserId = "test-user",
            SubjectId = "STU-DUP-001",
            AccessType = "View"
        });
        throw new Exception("Expected GlbaDuplicateException but none was thrown");
    }
    catch (GlbaDuplicateException)
    {
        Console.WriteLine($"    Correctly detected duplicate for SourceEventId: {sourceEventId}");
        return true;
    }
});

// ============================================================
// SECTION 2: UNAUTHORIZED ACCESS TESTS (Invalid/Missing API Key)
// ============================================================
Console.WriteLine();
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("SECTION 2: UNAUTHORIZED ACCESS TESTS");
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine();

// Test 10: Invalid API key
await RunTestAsync("Test 10: Invalid API key returns 401", async () =>
{
    using var badClient = new GlbaClient(endpoint, "invalid-api-key-12345");

    try
    {
        await badClient.LogAccessAsync(new GlbaEventRequest
        {
            AccessedAt = DateTime.UtcNow,
            UserId = "test-user",
            SubjectId = "STU-UNAUTH-001",
            AccessType = "View"
        });
        throw new Exception("Expected GlbaAuthenticationException but none was thrown");
    }
    catch (GlbaAuthenticationException)
    {
        Console.WriteLine($"    Correctly received 401 Unauthorized for invalid API key");
        return true;
    }
});

// Test 11: Empty API key
await RunTestAsync("Test 11: Empty API key returns 401", async () =>
{
    try
    {
        using var emptyKeyClient = new GlbaClient(endpoint, "");

        await emptyKeyClient.LogAccessAsync(new GlbaEventRequest
        {
            AccessedAt = DateTime.UtcNow,
            UserId = "test-user",
            SubjectId = "STU-UNAUTH-002",
            AccessType = "View"
        });
        throw new Exception("Expected GlbaAuthenticationException or ArgumentException but none was thrown");
    }
    catch (GlbaAuthenticationException)
    {
        Console.WriteLine($"    Correctly received 401 Unauthorized for empty API key");
        return true;
    }
    catch (ArgumentException ex) when (ex.ParamName == "ApiKey")
    {
        // Client validates API key before sending
        Console.WriteLine($"    Correctly rejected empty API key at client level");
        return true;
    }
});

// Test 12: Malformed API key
await RunTestAsync("Test 12: Malformed API key returns 401", async () =>
{
    using var malformedClient = new GlbaClient(endpoint, "not-a-valid-key-format!!@@##");

    try
    {
        await malformedClient.LogAccessAsync(new GlbaEventRequest
        {
            AccessedAt = DateTime.UtcNow,
            UserId = "test-user",
            SubjectId = "STU-UNAUTH-003",
            AccessType = "View"
        });
        throw new Exception("Expected GlbaAuthenticationException but none was thrown");
    }
    catch (GlbaAuthenticationException)
    {
        Console.WriteLine($"    Correctly received 401 Unauthorized for malformed API key");
        return true;
    }
});

// Test 13: Batch endpoint with invalid API key
await RunTestAsync("Test 13: Batch endpoint with invalid API key returns 401", async () =>
{
    using var badClient = new GlbaClient(endpoint, "another-invalid-key");

    var batch = new List<GlbaEventRequest>
    {
        new() { UserId = "user1", SubjectId = "STU-001", AccessType = "View", AccessedAt = DateTime.UtcNow }
    };

    try
    {
        await badClient.LogAccessBatchAsync(batch);
        throw new Exception("Expected GlbaAuthenticationException but none was thrown");
    }
    catch (GlbaAuthenticationException)
    {
        Console.WriteLine($"    Correctly received 401 Unauthorized for batch endpoint");
        return true;
    }
});

// ============================================================
// SECTION 3: VALIDATION TESTS
// ============================================================
Console.WriteLine();
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("SECTION 3: VALIDATION TESTS");
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine();

// Test 14: Missing required field (UserId)
await RunTestAsync("Test 14: Missing UserId returns validation error", async () =>
{
    try
    {
        await client.LogAccessAsync(new GlbaEventRequest
        {
            AccessedAt = DateTime.UtcNow,
            UserId = "",  // Missing required field
            SubjectId = "STU-VAL-001",
            AccessType = "View"
        });
        throw new Exception("Expected GlbaValidationException but none was thrown");
    }
    catch (GlbaValidationException ex)
    {
        Console.WriteLine($"    Correctly received validation error: {ex.Message}");
        return true;
    }
});

// Test 15: Batch too large
await RunTestAsync("Test 15: Batch > 1000 events throws exception", async () =>
{
    var largeBatch = Enumerable.Range(1, 1001).Select(i => new GlbaEventRequest
    {
        UserId = $"user{i}",
        SubjectId = $"STU-{i}",
        AccessType = "View",
        AccessedAt = DateTime.UtcNow
    });

    try
    {
        await client.LogAccessBatchAsync(largeBatch);
        throw new Exception("Expected GlbaBatchTooLargeException but none was thrown");
    }
    catch (GlbaBatchTooLargeException)
    {
        Console.WriteLine($"    Correctly rejected batch with > 1000 events");
        return true;
    }
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
Console.WriteLine($"  PASSED: {passCount}");
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine($"  FAILED: {failCount}");
Console.ResetColor();
Console.WriteLine($"  TOTAL:  {passCount + failCount}");
Console.WriteLine();

if (failCount == 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("All tests passed!");
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"{failCount} test(s) failed!");
}
Console.ResetColor();

Console.WriteLine();
Console.WriteLine("Check your FreeGLBA dashboard at:");
Console.WriteLine($"  {endpoint}/AccessEvents");

// Helper method to run tests with consistent formatting
async Task RunTestAsync(string testName, Func<Task<bool>> test)
{
    Console.Write($"{testName}... ");
    try
    {
        var result = await test();
        if (result)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
            passCount++;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            failCount++;
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"FAILED - {ex.Message}");
        Console.ResetColor();
        failCount++;
    }
}

// Dummy class for user secrets assembly reference
public partial class Program { }
