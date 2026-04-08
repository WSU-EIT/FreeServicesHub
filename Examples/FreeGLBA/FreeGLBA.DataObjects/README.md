# FreeGLBA.DataObjects

Data Transfer Objects (DTOs), view models, configuration helpers, and API endpoint constants for the FreeGLBA GLBA Compliance Data Access Tracking System.

Developed by **Enrollment Information Technology** at **Washington State University**.

## Purpose

This project contains all the data structures used to transfer data between layers of the application:
- **DTOs** for API requests/responses
- **View Models** for UI binding
- **Configuration** helpers for application settings
- **Caching** utilities
- **API Endpoints** as constants

## Dependencies

- `System.Runtime.Caching` 10.0.1 - For in-memory caching
- **FreeGLBA.Plugins** - Plugin system integration

## Project Structure

```
FreeGLBA.DataObjects/
├── FreeGLBA.DataObjects.csproj
├── README.md
│
├── # Configuration
├── GlobalSettings.cs                    # Base global settings
├── GlobalSettings.App.cs                # GLBA-specific settings
├── ConfigurationHelper.cs               # Configuration utilities
├── ConfigurationHelper.App.cs           # GLBA configuration helpers
│
├── # Caching
├── Caching.cs                           # In-memory cache wrapper
│
├── # Core Data Objects
├── DataObjects.cs                       # Base framework DTOs
├── DataObjects.App.cs                   # Application-specific DTOs
├── DataObjects.Users.cs                 # User-related DTOs
├── DataObjects.Departments.cs           # Department DTOs
├── DataObjects.UserGroups.cs            # User group DTOs
├── DataObjects.Tags.cs                  # Tagging DTOs
├── DataObjects.Services.cs              # Service-related DTOs
├── DataObjects.UDFLabels.cs             # User-defined field DTOs
├── DataObjects.Ajax.cs                  # AJAX response DTOs
├── DataObjects.SignalR.cs               # SignalR message DTOs
├── DataObjects.ActiveDirectory.cs       # AD integration DTOs
│
├── # GLBA-Specific Data Objects
├── FreeGLBA.App.DataObjects.cs          # GLBA core DTOs
├── FreeGLBA.App.DataObjects.ExternalApi.cs  # External API DTOs
│
└── # API Endpoints
    └── FreeGLBA.App.Endpoints.cs        # API endpoint constants
```

## Key Data Objects

### GLBA External API DTOs

These DTOs are used for the external API that receives access events:

#### GlbaEventRequest
Incoming event from external source systems:
```csharp
public class GlbaEventRequest
{
    public string? SourceEventId { get; set; }     // For deduplication
    public DateTime AccessedAt { get; set; }
    public string UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserDepartment { get; set; }
    public string SubjectId { get; set; }          // e.g., Student ID
    public string? SubjectType { get; set; }       // Student, Employee, etc.
    public string? DataCategory { get; set; }      // Financial, Academic, etc.
    public string AccessType { get; set; }         // View, Export, Print
    public string? Purpose { get; set; }           // Business justification
    public string? IpAddress { get; set; }
    public string? AdditionalData { get; set; }    // JSON extra data
}
```

#### GlbaEventResponse
Response after processing an event:
```csharp
public class GlbaEventResponse
{
    public Guid? EventId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Status { get; set; }             // accepted, duplicate, error
    public string? Message { get; set; }
}
```

#### GlbaBatchResponse
Response for batch event submission:
```csharp
public class GlbaBatchResponse
{
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public int Duplicate { get; set; }
    public List<GlbaBatchError> Errors { get; set; }
}
```

### Core Framework DTOs

| Class | Purpose |
|-------|---------|
| `User` | User account data transfer |
| `UserGroup` | Role/group information |
| `Department` | Department data |
| `Tenant` | Multi-tenant context |
| `Setting` | Configuration settings |
| `Tag` / `TagItem` | Tagging system |
| `FileStorage` | File metadata |
| `AjaxResults` | Standard AJAX response wrapper |
| `SignalRMessage` | Real-time notification payload |

## API Endpoints

The `Endpoints` class provides compile-time safe endpoint constants:

```csharp
public static class Endpoints
{
    public static class FreeGLBA
    {
        // SourceSystem Endpoints
        public const string GetSourceSystems = "api/Data/GetSourceSystems";
        public const string GetSourceSystem = "api/Data/GetSourceSystem";
        public const string SaveSourceSystem = "api/Data/SaveSourceSystem";
        public const string DeleteSourceSystem = "api/Data/DeleteSourceSystem";

        // AccessEvent Endpoints
        public const string GetAccessEvents = "api/Data/GetAccessEvents";
        public const string GetAccessEvent = "api/Data/GetAccessEvent";
        public const string SaveAccessEvent = "api/Data/SaveAccessEvent";
        public const string DeleteAccessEvent = "api/Data/DeleteAccessEvent";

        // DataSubject Endpoints
        public const string GetDataSubjects = "api/Data/GetDataSubjects";
        // ... etc
    }
}
```

Usage:
```csharp
var response = await Http.GetFromJsonAsync<List<SourceSystem>>(Endpoints.FreeGLBA.GetSourceSystems);
```

## Global Settings

The `GlobalSettings` class provides application-wide configuration:

```csharp
public static class GlobalSettings
{
    public static string DatabaseType { get; set; }
    public static string ConnectionString { get; set; }
    public static bool MultiTenantMode { get; set; }
    public static string DefaultTenantId { get; set; }
    // ... etc
}
```

## Caching

The `Caching` class provides a simple in-memory cache wrapper:

```csharp
// Store a value
Caching.Set("key", myObject, TimeSpan.FromMinutes(10));

// Retrieve a value
var cached = Caching.Get<MyType>("key");

// Remove a value
Caching.Remove("key");
```

## Configuration Helper

`ConfigurationHelper` provides utilities for reading `appsettings.json`:

```csharp
// Get a configuration value
var apiKey = ConfigurationHelper.GetValue("GlbaApiKey");

// Get a typed section
var settings = ConfigurationHelper.GetSection<MySettings>("MySettings");
```

## File Listing

| File | Description |
|------|-------------|
| `GlobalSettings.cs` | Base application-wide settings |
| `GlobalSettings.App.cs` | GLBA-specific global settings |
| `ConfigurationHelper.cs` | Configuration reading utilities |
| `ConfigurationHelper.App.cs` | GLBA configuration helpers |
| `Caching.cs` | In-memory cache wrapper |
| `DataObjects.cs` | Base framework DTOs |
| `DataObjects.App.cs` | Application-specific DTOs |
| `DataObjects.Ajax.cs` | AJAX response wrappers |
| `DataObjects.SignalR.cs` | SignalR message types |
| `DataObjects.ActiveDirectory.cs` | Active Directory DTOs |
| `DataObjects.Departments.cs` | Department DTOs |
| `DataObjects.UserGroups.cs` | User group DTOs |
| `DataObjects.Tags.cs` | Tag DTOs |
| `DataObjects.Services.cs` | Service DTOs |
| `DataObjects.UDFLabels.cs` | User-defined field DTOs |
| `FreeGLBA.App.DataObjects.cs` | GLBA core DTOs |
| `FreeGLBA.App.DataObjects.ExternalApi.cs` | External API request/response DTOs |
| `FreeGLBA.App.Endpoints.cs` | API endpoint string constants |

## Usage Examples

### Using DTOs in Blazor
```csharp
@inject HttpClient Http

@code {
    private List<AccessEvent>? events;

    protected override async Task OnInitializedAsync()
    {
        events = await Http.GetFromJsonAsync<List<AccessEvent>>(
            Endpoints.FreeGLBA.GetAccessEvents);
    }
}
```

### Using Cache
```csharp
public async Task<List<SourceSystem>> GetSourceSystemsAsync()
{
    const string cacheKey = "SourceSystems";
    
    var cached = Caching.Get<List<SourceSystem>>(cacheKey);
    if (cached != null) return cached;
    
    var systems = await _context.SourceSystems.ToListAsync();
    Caching.Set(cacheKey, systems, TimeSpan.FromMinutes(5));
    return systems;
}
```

## Related Projects

- **FreeGLBA.EFModels** - Entity Framework entities (database layer)
- **FreeGLBA.DataAccess** - Data access layer that creates/uses these DTOs
- **FreeGLBA.Client** - Blazor UI that consumes these DTOs
- **FreeGLBA.NugetClient** - External client library with matching DTOs

---

## About

**FreeGLBA** is developed and maintained by the **Enrollment Information Technology** team at **Washington State University**.

🔗 [Meet Our Team](https://em.wsu.edu/eit/meet-our-staff/) | 📦 [GitHub](https://github.com/WSU-EIT/FreeGLBA)
