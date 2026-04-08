# FreeCRM Hook: DataObjects.App.cs & GlobalSettings.App.cs

> Data transfer objects, SignalR types, User extensions, and global settings.

**Files:** `{ProjectName}.DataObjects/DataObjects.App.cs`, `{ProjectName}.DataObjects/GlobalSettings.App.cs`
**Classes:** `partial class DataObjects`, `static partial class GlobalSettings`
**Complexity:** LOW — placeholder partials for custom DTOs and settings

---

## DataObjects.App.cs

### Custom SignalR Update Types

```csharp
public partial class SignalRUpdateType
{
    public const string PipelineLiveStatusUpdate = "PipelineLiveStatusUpdate";
    public const string DashboardRefresh = "DashboardRefresh";
}
```

**Use for:** Declaring string constants for custom SignalR event types. These are referenced in `SignalRUpdateApp` (server) and `ProcessSignalRUpdateApp` (client).

### User Extensions

```csharp
public partial class User
{
    public string? EmployeeId { get; set; }
    public bool CanViewConfidentialData { get; set; }
}
```

**Use for:** Adding custom properties to the User DTO. Pair with `GetDataApp`/`SaveDataApp` in `DataAccess.App.cs` to map to/from EF models.

### New DTOs

```csharp
public class SourceSystem
{
    public Guid SourceSystemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class SourceSystemFilter
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
```

**No hook file modification needed.** Partial classes merge at compile time.

> **API Convention:** When following the three-endpoint CRUD pattern ([007_patterns.crud_api.md](007_patterns.crud_api.md)), each entity DTO is passed as `List<T>` to **SaveMany** and returned as `List<T>` from **GetMany**. Empty/new PK → insert; existing PK → update.

**Custom files:**

| Scenario | File Name |
|----------|-----------|
| General DTOs | `{ProjectName}.App.DataObjects.cs` |
| Feature-specific DTOs | `{ProjectName}.App.DataObjects.{Feature}.cs` |
| API endpoint constants | `{ProjectName}.App.Endpoints.cs` |
| Settings/enums | `{ProjectName}.App.Settings.cs` |

**Real-world:** FreeCICD has `FreeCICD.App.DataObjects.cs` (endpoints + 20+ DTOs) and `FreeCICD.App.Settings.cs` (enums + YAML template). FreeGLBA splits across 4 files.

---

## GlobalSettings.App.cs

```csharp
public static partial class GlobalSettings
{
    // App-wide constants, enums, and configuration
}
```

**Use for:** App-wide constants, environment enums, version info, feature flags.

**Example:**

```csharp
// In {ProjectName}.App.Settings.cs:
public static partial class GlobalSettings
{
    public enum EnvironmentType { DEV, PROD, CMS }

    public static class App
    {
        public static string Name { get; set; } = "MyProject";
        public static string Version { get; set; } = "1.0.0";
        public static string[] AnonymousPages = ["LOGIN", "ABOUT", "HOME"];
    }
}
```

**Custom file:** `{ProjectName}.App.Settings.cs`

---

*Category: 006_architecture*
*Source: `ReferenceProjects/FreeCRM-main/CRM.DataObjects/DataObjects.App.cs`, `GlobalSettings.App.cs`*
