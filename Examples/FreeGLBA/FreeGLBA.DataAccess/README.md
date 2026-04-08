# FreeGLBA.DataAccess

Data Access Layer for the FreeGLBA GLBA Compliance Data Access Tracking System. Contains all business logic, database operations, and external integrations.

Developed by **Enrollment Information Technology** at **Washington State University**.

## Purpose

This project serves as the central business logic layer, providing:
- **Database Operations** - CRUD operations via Entity Framework
- **Business Logic** - Validation, processing, and workflows
- **External Integrations** - Microsoft Graph, Active Directory, APIs
- **Utilities** - Encryption, JWT, password generation, etc.
- **Data Migrations** - Database schema migrations for all providers

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Azure.Identity` | 1.17.1 | Azure authentication |
| `Brad.Wickett_Sql2LINQ` | 3.0.1 | Dynamic LINQ query building |
| `CsvHelper` | 33.1.0 | CSV import/export |
| `JWTHelpers` | 1.0.1 | JWT token handling |
| `Microsoft.Graph` | 5.98.0 | Microsoft 365 integration |
| `Novell.Directory.Ldap.NETStandard` | 4.0.0 | LDAP/Active Directory |
| `QuestPDF` | 2025.12.0 | PDF report generation |

### Project References
- **FreeGLBA.DataObjects** - DTOs and configuration
- **FreeGLBA.EFModels** - Entity Framework models
- **FreeGLBA.Plugins** - Plugin system

## Project Structure

```
FreeGLBA.DataAccess/
├── FreeGLBA.DataAccess.csproj
├── README.md
├── GlobalUsings.cs
│
├── # Core Data Access
├── DataAccess.cs                        # Main DataAccess class
├── DataAccess.Disposable.cs             # IDisposable implementation
├── DataAccess.App.cs                    # Application customization point
│
├── # GLBA-Specific
├── FreeGLBA.App.IDataAccess.cs          # GLBA interface definitions
├── FreeGLBA.App.DataAccess.cs           # GLBA data operations
├── FreeGLBA.App.DataAccess.ExternalApi.cs   # External API event processing
├── FreeGLBA.App.DataAccess.ApiKey.cs    # API key management
│
├── # Authentication & Security
├── DataAccess.Authenticate.cs           # User authentication
├── DataAccess.JWT.cs                    # JWT token operations
├── DataAccess.Encryption.cs             # Data encryption
├── DataAccess.ActiveDirectory.cs        # AD/LDAP integration
├── RandomPasswordGenerator.cs           # Password generation
├── RandomPasswordGenerator.App.cs       # Custom password rules
│
├── # Entity Operations
├── DataAccess.Users.cs                  # User CRUD
├── DataAccess.UserGroups.cs             # User group CRUD
├── DataAccess.Departments.cs            # Department CRUD
├── DataAccess.Tenants.cs                # Multi-tenant operations
├── DataAccess.Settings.cs               # Application settings
├── DataAccess.ApplicationSettings.cs    # App config management
├── DataAccess.Tags.cs                   # Tagging system
├── DataAccess.UDFLabels.cs              # User-defined fields
├── DataAccess.FileStorage.cs            # File storage operations
├── DataAccess.Language.cs               # Localization
│
├── # External Integrations
├── GraphAPI.cs                          # Microsoft Graph base
├── GraphAPI.App.cs                      # Graph customizations
├── DataAccess.Ajax.cs                   # AJAX endpoint handling
├── DataAccess.SignalR.cs                # Real-time notifications
├── DataAccess.CSharpCode.cs             # Dynamic code execution
├── DataAccess.Plugins.cs                # Plugin management
│
├── # Database Migrations
├── DataAccess.Migrations.cs             # Migration orchestration
├── DataMigrations.SQLServer.cs          # SQL Server migrations
├── DataMigrations.PostgreSQL.cs         # PostgreSQL migrations
├── DataMigrations.MySQL.cs              # MySQL migrations
├── DataMigrations.SQLite.cs             # SQLite migrations
│
├── # Utilities
├── Utilities.cs                         # General utilities
├── Utilities.App.cs                     # App-specific utilities
└── DataAccess.SeedTestData.cs           # Test data seeding
```

## Key Features

### GLBA External API Processing

The `ProcessGlbaEventAsync` method handles incoming access events:

```csharp
public async Task<GlbaEventResponse> ProcessGlbaEventAsync(
    GlbaEventRequest request, 
    Guid sourceSystemId)
{
    // Validate request
    // Check for duplicates via SourceEventId
    // Create AccessEvent entity
    // Update SourceSystem statistics
    // Return response with EventId
}
```

### API Key Management

Secure API key handling for external source systems:

```csharp
// Validate API key from Authorization header
public async Task<SourceSystem?> ValidateApiKeyAsync(string apiKey)

// Generate new API key
public string GenerateApiKey()

// Rotate API key for a source system
public async Task<string> RotateApiKeyAsync(Guid sourceSystemId)
```

### Authentication

Multiple authentication methods supported:

```csharp
// Local database authentication
public async Task<User?> AuthenticateAsync(string username, string password)

// Active Directory authentication
public async Task<User?> AuthenticateWithADAsync(string username, string password)

// OAuth callback processing
public async Task<User?> ProcessOAuthCallbackAsync(OAuthProvider provider, ClaimsPrincipal principal)
```

### Data Migrations

Automatic schema migrations for all supported databases:

```csharp
// Run migrations on startup
await dataAccess.RunMigrationsAsync();

// Migrations are database-specific:
// - DataMigrations.SQLServer.cs
// - DataMigrations.PostgreSQL.cs
// - DataMigrations.MySQL.cs
// - DataMigrations.SQLite.cs
```

### PDF Report Generation

Using QuestPDF for compliance reports:

```csharp
public byte[] GenerateComplianceReport(
    DateTime startDate, 
    DateTime endDate, 
    Guid? sourceSystemId = null)
```

### Microsoft Graph Integration

Access Microsoft 365 data:

```csharp
// Get user info from Azure AD
public async Task<GraphUser?> GetGraphUserAsync(string userId)

// Sync users from Azure AD
public async Task SyncUsersFromGraphAsync()
```

## Interface Definition

The `IDataAccess` interface defines all available operations:

```csharp
public interface IDataAccess
{
    // GLBA Operations
    Task<GlbaEventResponse> ProcessGlbaEventAsync(GlbaEventRequest request, Guid sourceSystemId);
    Task<GlbaBatchResponse> ProcessGlbaBatchAsync(List<GlbaEventRequest> events, Guid sourceSystemId);
    Task<GlbaStats> GetGlbaStatsAsync();
    Task<List<AccessEvent>> GetRecentAccessEventsAsync(int limit);
    
    // Source System Operations
    Task<List<SourceSystem>> GetSourceSystemsAsync();
    Task<SourceSystem?> GetSourceSystemAsync(Guid id);
    Task<SourceSystem> SaveSourceSystemAsync(SourceSystem system);
    Task DeleteSourceSystemAsync(Guid id);
    
    // User Operations
    Task<User?> AuthenticateAsync(string username, string password);
    Task<List<User>> GetUsersAsync();
    Task<User> SaveUserAsync(User user);
    
    // ... additional operations
}
```

## Usage

### Dependency Injection Setup

```csharp
// Program.cs
builder.Services.AddScoped<IDataAccess, DataAccess>();
```

### Using in Controllers

```csharp
[ApiController]
public class GlbaController : ControllerBase
{
    private readonly IDataAccess _da;

    public GlbaController(IDataAccess da) => _da = da;

    [HttpPost("events")]
    public async Task<ActionResult<GlbaEventResponse>> PostEvent(GlbaEventRequest request)
    {
        var source = HttpContext.Items["SourceSystem"] as SourceSystem;
        var result = await _da.ProcessGlbaEventAsync(request, source.SourceSystemId);
        return result.Status == "accepted" ? Created(...) : BadRequest(result);
    }
}
```

### Using in Blazor Services

```csharp
public class DashboardService
{
    private readonly IDataAccess _da;
    
    public DashboardService(IDataAccess da) => _da = da;
    
    public async Task<GlbaStats> GetStatsAsync() => await _da.GetGlbaStatsAsync();
}
```

## File Listing

| File | Description |
|------|-------------|
| `DataAccess.cs` | Main DataAccess class with core operations |
| `DataAccess.Disposable.cs` | IDisposable pattern implementation |
| `DataAccess.App.cs` | Application customization hook |
| `FreeGLBA.App.IDataAccess.cs` | GLBA-specific interface definitions |
| `FreeGLBA.App.DataAccess.cs` | GLBA entity CRUD operations |
| `FreeGLBA.App.DataAccess.ExternalApi.cs` | External API event processing |
| `FreeGLBA.App.DataAccess.ApiKey.cs` | API key generation and validation |
| `DataAccess.Authenticate.cs` | User authentication logic |
| `DataAccess.JWT.cs` | JWT token creation and validation |

## About

FreeGLBA is developed and maintained by the **Enrollment Information Technology** team at **Washington State University**.

🔗 [Meet Our Staff](https://em.wsu.edu/eit/meet-our-staff/)
