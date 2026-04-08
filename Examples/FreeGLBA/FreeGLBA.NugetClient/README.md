# FreeGLBA.Client

A .NET client library for integrating with [FreeGLBA](https://github.com/WSU-EIT/FreeGLBA) - a GLBA Compliance Data Access Tracking System.

Developed by **Enrollment Information Technology** at **Washington State University**.

## Installation

```bash
dotnet add package FreeGLBA.Client
```

## Quick Start

### Simple Usage

```csharp
using FreeGLBA.Client;

var client = new GlbaClient("https://your-glba-server.com", "your-api-key");

var response = await client.LogAccessAsync(new GlbaEventRequest
{
    AccessedAt = DateTime.UtcNow,
    UserId = "jsmith",
    UserName = "John Smith",
    SubjectId = "S12345678",
    AccessType = "Export",
    Purpose = "Student requested transcript"
});

if (response.Status == "accepted")
{
    Console.WriteLine($"Event logged: {response.EventId}");
}
```

### With Dependency Injection (ASP.NET Core)

```csharp
// Program.cs
builder.Services.AddGlbaClient(options =>
{
    options.Endpoint = "https://your-glba-server.com";
    options.ApiKey = builder.Configuration["GlbaApiKey"];
});

// In your service
public class DataExportService
{
    private readonly IGlbaClient _glbaClient;

    public DataExportService(IGlbaClient glbaClient)
    {
        _glbaClient = glbaClient;
    }

    public async Task<byte[]> ExportStudentDataAsync(string userId, string studentId, string purpose)
    {
        await _glbaClient.LogAccessAsync(new GlbaEventRequest
        {
            AccessedAt = DateTime.UtcNow,
            UserId = userId,
            SubjectId = studentId,
            AccessType = "Export",
            Purpose = purpose
        });

        return await GenerateCsvAsync(studentId);
    }
}
```

### Simplified Methods

```csharp
// Log an export event (single subject)
await client.LogExportAsync("jsmith", "S12345678", "Annual review");

// Log a view event (single subject)
await client.LogViewAsync("jsmith", "S12345678");
```

## Bulk Access (Multiple Subjects)

For systems like Touchpoints that export data for many individuals at once, use the bulk access methods.
Each subject is tracked individually in the Data Subjects table for audit purposes.

### Bulk Export (CSV, Reports, etc.)

```csharp
// Export affecting hundreds of students
var studentIds = new[] { "S10000001", "S10000002", "S10000003", /* ... */ };

var response = await client.LogBulkExportAsync(
    userId: "jsmith",
    subjectIds: studentIds,
    purpose: "Enrollment analysis report for Dean's office",
    userName: "John Smith",
    dataCategory: "Financial Aid",
    agreementText: "I acknowledge this export contains GLBA-protected data..."
);

Console.WriteLine($"Export logged. {response.SubjectCount} subjects affected.");
// Output: Export logged. 347 subjects affected.
```

### Bulk View (Search Results, Dashboards)

```csharp
// Search results displaying multiple students
var searchResults = await SearchStudentsAsync(query);
var studentIds = searchResults.Select(s => s.StudentId);

await client.LogBulkViewAsync(
    userId: "jsmith",
    subjectIds: studentIds,
    purpose: "Student search for enrollment verification"
);
```

### Using GlbaEventRequest Directly

```csharp
var response = await client.LogAccessAsync(new GlbaEventRequest
{
    AccessedAt = DateTime.UtcNow,
    UserId = "jsmith",
    UserName = "John Smith",
    SubjectIds = new List<string> { "S001", "S002", "S003", /* ... */ },
    AccessType = "Export",
    DataCategory = "Financial Records",
    Purpose = "Quarterly compliance audit",
    AgreementText = "I certify this data access is for legitimate business purposes..."
});

if (response.IsBulkAccess)
{
    Console.WriteLine($"Bulk export logged: {response.SubjectCount} subjects");
}
```

### Try Pattern (No Exceptions)

```csharp
if (await client.TryLogAccessAsync(request))
{
    // Success - proceed with data access
}
else
{
    // Failed - handle accordingly
}
```

### Batch Processing (Multiple Events)

For logging many separate events (not one event with multiple subjects):

```csharp
var events = new List<GlbaEventRequest>
{
    new() { UserId = "user1", SubjectId = "S001", AccessType = "View", AccessedAt = DateTime.UtcNow },
    new() { UserId = "user2", SubjectId = "S002", AccessType = "Export", AccessedAt = DateTime.UtcNow }
};

var result = await client.LogAccessBatchAsync(events);
Console.WriteLine($"Accepted: {result.Accepted}, Rejected: {result.Rejected}");
```

## Internal Endpoints (Dashboard/Query Methods)

These methods require **user JWT authentication** (not API keys) and are used for querying data from the FreeGLBA dashboard.

### Setting Up User Authentication

```csharp
// After user authenticates, set their JWT token
client.SetBearerToken(userJwtToken);

// Now you can call internal endpoints
var stats = await client.GetStatsAsync();

// Clear the token when done
client.ClearBearerToken();
```

### Available Query Methods

```csharp
// Get dashboard statistics
var stats = await client.GetStatsAsync();
Console.WriteLine($"Today: {stats.Today} events, {stats.SubjectsToday} subjects");

// Get recent access events
var recentEvents = await client.GetRecentEventsAsync(limit: 50);
foreach (var evt in recentEvents)
{
    Console.WriteLine($"{evt.AccessedAt}: {evt.UserId} {evt.AccessType} {evt.SubjectId}");
}

// Get access history for a specific subject
var subjectHistory = await client.GetSubjectEventsAsync("STU-12345", limit: 100);

// Get a single event by ID
var evt = await client.GetEventAsync(eventId);

// Get status of all source systems
var sources = await client.GetSourceStatusAsync();
foreach (var source in sources)
{
    Console.WriteLine($"{source.DisplayName}: {source.EventCount} events");
}

// Get top data accessors
var topAccessors = await client.GetTopAccessorsAsync(limit: 10);
foreach (var accessor in topAccessors)
{
    Console.WriteLine($"{accessor.UserName}: {accessor.TotalAccesses} accesses");
}
```

### Response Models

| Method | Returns |
|--------|---------|
| `GetStatsAsync()` | `GlbaStats` - Dashboard statistics |
| `GetRecentEventsAsync()` | `List<AccessEvent>` - Recent events |
| `GetSubjectEventsAsync()` | `List<AccessEvent>` - Subject's history |
| `GetEventAsync()` | `AccessEvent?` - Single event or null |
| `GetSourceStatusAsync()` | `List<SourceSystemStatus>` - Source systems |
| `GetTopAccessorsAsync()` | `List<AccessorSummary>` - Top users |

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Endpoint` | string | (required) | Base URL of the FreeGLBA server |
| `ApiKey` | string | (required) | API key for authentication |
| `Timeout` | TimeSpan | 30 seconds | HTTP request timeout |
| `RetryCount` | int | 3 | Number of retry attempts for transient failures |
| `ThrowOnError` | bool | true | Whether to throw exceptions on API errors |

## Error Handling

The client throws specific exceptions for different error conditions:

- `GlbaAuthenticationException` - Invalid or expired API key (HTTP 401)
- `GlbaValidationException` - Invalid request data (HTTP 400)
- `GlbaDuplicateException` - Event already exists (HTTP 409)
- `GlbaException` - Base exception for other errors

```csharp
try
{
    await client.LogAccessAsync(request);
}
catch (GlbaAuthenticationException)
{
    // Handle invalid API key
}
catch (GlbaDuplicateException ex)
{
    // Event already logged - may be okay depending on your use case
    Console.WriteLine($"Duplicate event: {ex.EventId}");
}
catch (GlbaException ex)
{
    // Handle other errors
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Requirements

- .NET 10.0

---

# Building & Publishing (For Maintainers)

This section is for WSU-EIT maintainers who need to build and publish the NuGet package.

## Prerequisites

- .NET 10 SDK installed
- NuGet.org API key (stored securely, provided at runtime)

## Project Structure

```
FreeGLBA.NugetClient/
├── FreeGLBA.NugetClient.csproj    # Package configuration
├── README.md                       # This file (included in package)
├── GlbaClient.cs                   # Main client implementation
├── GlbaClientOptions.cs            # Configuration options
├── IGlbaClient.cs                  # Interface for DI
├── Models/
│   ├── GlbaEventRequest.cs         # Request DTO
│   ├── GlbaEventResponse.cs        # Response DTOs
│   └── GlbaException.cs            # Custom exceptions
└── Extensions/
    └── ServiceCollectionExtensions.cs  # DI helpers
```

## Building the Package

### Option 1: Build from Visual Studio

1. Open the solution in Visual Studio
2. Right-click on `FreeGLBA.NugetClient` project
3. Select **Build** (package is generated automatically on build)
4. Find the `.nupkg` file in `bin\Debug\` or `bin\Release\`

### Option 2: Build from Command Line

```bash
# Navigate to the project directory
cd FreeGLBA.NugetClient

# Build Debug (generates .nupkg automatically)
dotnet build

# Build Release (recommended for publishing)
dotnet build -c Release

# Or explicitly pack
dotnet pack -c Release
```

The NuGet package will be created at:
- Debug: `bin\Debug\FreeGLBA.Client.1.1.0.nupkg`
- Release: `bin\Release\FreeGLBA.Client.1.1.0.nupkg`

A symbol package (`.snupkg`) is also generated for debugging support.

## Updating the Version

Edit the `<Version>` in `FreeGLBA.NugetClient.csproj`:

```xml
<Version>1.0.0</Version>
```

Follow [Semantic Versioning](https://semver.org/):
- **MAJOR** (1.0.0 → 2.0.0): Breaking changes
- **MINOR** (1.0.0 → 1.1.0): New features, backwards compatible
- **PATCH** (1.0.0 → 1.0.1): Bug fixes, backwards compatible

## Publishing to NuGet.org

### Option 1: Command Line (Recommended)

```bash
# Navigate to the project directory
cd FreeGLBA.NugetClient

# Build Release
dotnet build -c Release

# Publish to NuGet.org (replace YOUR_API_KEY with actual key)
dotnet nuget push bin\Release\FreeGLBA.Client.1.1.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json

# Also push the symbol package for debugging support
dotnet nuget push bin\Release\FreeGLBA.Client.1.1.0.snupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

### Option 2: Using Environment Variable

```bash
# Set the API key as an environment variable (more secure)
$env:NUGET_API_KEY = "your-api-key-here"

# Then push without exposing the key in command history
dotnet nuget push bin\Release\FreeGLBA.Client.1.0.0.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json
```

### Option 3: One-Liner Script

```powershell
# PowerShell one-liner (prompts for API key)
$key = Read-Host "Enter NuGet API Key" -AsSecureString; $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($key)); dotnet nuget push bin\Release\FreeGLBA.Client.*.nupkg --api-key $plainKey --source https://api.nuget.org/v3/index.json
```

## Verifying the Published Package

After publishing:

1. **Check NuGet.org**: Visit https://www.nuget.org/packages/FreeGLBA.Client/
   - Note: It may take 15-30 minutes for the package to be indexed and searchable

2. **Test Installation**:
   ```bash
   # Create a test project
   mkdir TestProject && cd TestProject
   dotnet new console
   
   # Install the package
   dotnet add package FreeGLBA.Client
   
   # Verify it works
   dotnet build
   ```

## Pre-Publish Checklist

Before publishing a new version:

- [ ] Version number updated in `.csproj`
- [ ] All changes committed to Git
- [ ] Build succeeds with no warnings: `dotnet build -c Release`
- [ ] README.md is up to date
- [ ] Test the package locally (see below)

## Testing Locally Before Publishing

```bash
# Create a local NuGet source
dotnet nuget add source ./bin/Release --name LocalTest

# In a test project, install from local source
dotnet add package FreeGLBA.Client --source LocalTest

# Clean up when done
dotnet nuget remove source LocalTest
```

## Package Contents

The published package includes:
- Compiled DLL for .NET 10
- XML documentation (IntelliSense)
- README.md (displayed on NuGet.org)
- Symbol package (.snupkg) for debugging
- SourceLink support (debug into source code)

## Troubleshooting

### "Package already exists" Error
You cannot overwrite an existing version on NuGet.org. Increment the version number and publish again.

### "API key invalid" Error
- Verify the API key is correct and not expired
- Ensure the key has "Push" permissions for the `FreeGLBA.Client` package
- Check if the key is scoped to specific packages

### Package Not Appearing on NuGet.org
- Wait 15-30 minutes for indexing
- Check https://www.nuget.org/packages/FreeGLBA.Client/ directly
- Look for validation errors in your NuGet.org account

---

## Links

- [FreeGLBA GitHub Repository](https://github.com/WSU-EIT/FreeGLBA)
- [NuGet Package](https://www.nuget.org/packages/FreeGLBA.Client)
- [Report Issues](https://github.com/WSU-EIT/FreeGLBA/issues)

## About

**FreeGLBA** is developed and maintained by the **Enrollment Information Technology** team at **Washington State University**.

🔗 [Meet Our Team](https://em.wsu.edu/eit/meet-our-staff/)

## License

MIT License - see [LICENSE](LICENSE) for details.
