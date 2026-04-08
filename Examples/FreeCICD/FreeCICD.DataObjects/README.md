# FreeCICD.DataObjects

## Overview

The `FreeCICD.DataObjects` project is a shared library containing all Data Transfer Objects (DTOs), enums, settings, and common data structures used throughout the FreeCICD application. This project has **no dependencies on Entity Framework** and is designed to be lightweight and shareable across all layers.

---

## Project Structure

```
FreeCICD.DataObjects/
??? Caching.cs                    # Memory caching utilities
??? DataObjects.cs                # Core DTOs and data classes
??? DataObjects.ActiveDirectory.cs # LDAP/AD integration models
??? DataObjects.Ajax.cs           # AJAX request/response models
??? DataObjects.App.cs            # App-specific extension point
??? DataObjects.App.FreeCICD.cs   # FreeCICD-specific DTOs (DevOps, Pipelines)
??? DataObjects.Departments.cs    # Department-related DTOs
??? DataObjects.Services.cs       # Service layer models
??? DataObjects.SignalR.cs        # Real-time communication models
??? DataObjects.Tags.cs           # Tag system models
??? DataObjects.UDFLabels.cs      # User Defined Fields models
??? DataObjects.UserGroups.cs     # User group models
??? GlobalSettings.cs             # Runtime global settings
??? GlobalSettings.App.cs         # App-specific settings extension
??? GlobalSettings.App.FreeCICD.cs # FreeCICD-specific settings
??? FreeCICD.DataObjects.csproj   # Project file
```

---

## Architecture Diagram

```
???????????????????????????????????????????????????????????????????????????????
?                         APPLICATION LAYERS                                  ?
???????????????????????????????????????????????????????????????????????????????
?                                                                             ?
?  ???????????????????    ???????????????????    ???????????????????         ?
?  ?  FreeCICD       ?    ?  FreeCICD       ?    ?  FreeCICD       ?         ?
?  ?  (Server)       ?    ?  .Client        ?    ?  .DataAccess    ?         ?
?  ?                 ?    ?  (Blazor WASM)  ?    ?                 ?         ?
?  ???????????????????    ???????????????????    ???????????????????         ?
?           ?                      ?                      ?                   ?
?           ?                      ?                      ?                   ?
?           ???????????????????????????????????????????????                   ?
?                                  ?                                          ?
?                                  ?                                          ?
?  ?????????????????????????????????????????????????????????????????????????  ?
?  ?                     FreeCICD.DataObjects                              ?  ?
?  ?  ?????????????  ?????????????  ?????????????  ?????????????          ?  ?
?  ?  ?   DTOs    ?  ?   Enums   ?  ?  Settings ?  ?  SignalR  ?          ?  ?
?  ?  ?           ?  ?           ?  ?           ?  ?  Models   ?          ?  ?
?  ?  ?????????????  ?????????????  ?????????????  ?????????????          ?  ?
?  ?????????????????????????????????????????????????????????????????????????  ?
?                                  ?                                          ?
?                                  ?                                          ?
?  ?????????????????????????????????????????????????????????????????????????  ?
?  ?                     FreeCICD.Plugins                                  ?  ?
?  ?              (Plugin definitions - minimal dependency)                ?  ?
?  ?????????????????????????????????????????????????????????????????????????  ?
?                                                                             ?
???????????????????????????????????????????????????????????????????????????????
```

---

## FreeCICD-Specific DTOs (Pipeline Dashboard)

### Pipeline Dashboard Models

These models support the Pipeline Dashboard feature for managing Azure DevOps CI/CD pipelines:

| Class | Purpose | Used By |
|-------|---------|---------|
| `PipelineListItem` | Pipeline in dashboard list with status | Dashboard UI |
| `PipelineRunInfo` | Individual pipeline run with timing | Runs dropdown |
| `PipelineVariableGroupRef` | Variable group reference with link | Library badges |
| `ParsedPipelineSettings` | Parsed YAML settings for import | Wizard import |
| `ParsedEnvironmentSettings` | Per-environment settings from YAML | Import wizard |
| `PipelineDashboardResponse` | API response for dashboard data | Dashboard API |
| `PipelineRunsResponse` | API response for pipeline runs | Runs API |
| `PipelineYamlResponse` | API response for YAML content | YAML viewer |

### Key Pipeline Properties

```csharp
public class PipelineListItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Path { get; set; }
    public string? RepositoryName { get; set; }
    public string? DefaultBranch { get; set; }      // Repository's default branch
    public string? TriggerBranch { get; set; }      // Actual branch from last build (NEW)
    public string? LastRunStatus { get; set; }
    public string? LastRunResult { get; set; }
    public DateTime? LastRunTime { get; set; }
    public string? ResourceUrl { get; set; }
    public string? YamlFileName { get; set; }
    public List<PipelineVariableGroupRef> VariableGroups { get; set; }
}
```

> **Note:** `TriggerBranch` was added in December 2025 to show the actual branch that triggered the most recent build, which is more accurate than `DefaultBranch` (the repository's default). The UI displays `TriggerBranch` with fallback to `DefaultBranch`.

### Azure DevOps Integration Models

| Class | Purpose |
|-------|---------|
| `DevopsProjectInfo` | Azure DevOps project details |
| `DevopsGitRepoInfo` | Git repository information |
| `DevopsGitRepoBranchInfo` | Branch details with commit info |
| `DevopsPipelineDefinition` | Pipeline definition metadata |
| `DevopsVariableGroup` | Variable group with variables |
| `DevopsVariable` | Individual variable name/value |
| `DevOpsBuild` | Build run information |
| `DevopsFileItem` | File in repository |
| `BuildDefinition` | Full pipeline definition |

---

## Core DTOs

| Class | Purpose | Used By |
|-------|---------|---------|
| `User` | User profile with all properties | All layers |
| `UserListing` | Lightweight user list view | UI listings |
| `UserAccount` | Multi-tenant user account info | Authentication |
| `Tenant` | Tenant/organization data | Multi-tenancy |
| `TenantSettings` | Tenant-specific configuration | Settings pages |
| `Department` | Department/team structure | Organization |
| `FileStorage` | File metadata and content | File management |
| `Filter` / `FilterUsers` | Pagination and filtering | Data grids |

### Response Types

| Class | Purpose |
|-------|---------|
| `BooleanResponse` | Success/failure with messages |
| `ActionResponseObject` | Base class for responses with action status |
| `SimpleResponse` | Minimal success/message response |
| `ModuleAction` | Module operation result with focus hint |

### Authentication

| Class | Purpose |
|-------|---------|
| `Authenticate` | Login credentials |
| `AuthenticationProviders` | Available auth methods |
| `CustomLoginProvider` | Plugin-based authentication |

### Configuration

| Class | Purpose |
|-------|---------|
| `ApplicationSettings` | App-wide settings |
| `ApplicationSettingsUpdate` | Settings modification DTO |
| `ConnectionStringConfig` | Database connection config |
| `MailServerConfig` | Email server settings |

---

## Enums

```csharp
// Record deletion behavior
public enum DeletePreference {
    Immediate,      // Hard delete
    MarkAsDeleted   // Soft delete (default)
}

// Setting value types
public enum SettingType {
    Boolean, DateTime, EncryptedObject, EncryptedText,
    Guid, NumberDecimal, NumberDouble, NumberInt,
    Object, Text
}

// User lookup methods
public enum UserLookupType {
    Email, EmployeeId, Guid, Username
}

// Environment types (FreeCICD-specific)
public enum EnvironmentType {
    DEV, PROD, CMS, QA, UAT, STAGING
}

// Parse confidence level (FreeCICD-specific)
public enum ParseConfidence {
    High,    // Found in expected location
    Medium,  // Found in non-standard location
    Low      // Inferred or partially matched
}
```

---

## GlobalSettings

Static runtime state container:

```csharp
public static partial class GlobalSettings {
    public static bool PluginsSavedToCache { get; set; }
    public static long RunningSince { get; set; }
    public static bool StartupError { get; set; }
    public static string StartupErrorCode { get; set; }
    public static List<string> StartupErrorMessages { get; set; }
    public static bool StartupRun { get; set; }
}

// FreeCICD-specific settings (GlobalSettings.App.FreeCICD.cs)
public static partial class GlobalSettings {
    public static partial class App {
        public static string BuildPipelineTemplate { get; set; }
        public static string BuildPiplelinePool { get; set; }
        public static Dictionary<EnvironmentType, EnvironmentOption> EnvironmentOptions { get; set; }
        public static List<EnvironmentType> EnviormentTypeOrder { get; set; }
        public static List<string> AzureDevOpsProjectNameStartsWithIgnoreValues { get; set; }
    }
}
```

---

## SignalR Update Types

Real-time communication event types:

```
?????????????????????????????????????????????????????????????????????????
?                    SignalRUpdateType Constants                        ?
?????????????????????????????????????????????????????????????????????????
?  Department      ?  DepartmentGroup  ?  File          ?  User        ?
?  Language        ?  LastAccessTime   ?  Setting       ?  Tag         ?
?  Tenant          ?  UDF              ?  Undelete      ?  Unknown     ?
?  UserAttendance  ?  UserGroup        ?  UserPreferences               ?
?  LoadingDevOpsInfoStatusUpdate (FreeCICD-specific)                    ?
?????????????????????????????????????????????????????????????????????????

SignalRUpdate {
    TenantId       -> Target tenant (optional, null = all)
    ItemId         -> Specific item updated
    UserId         -> Who made the change
    UpdateType     -> Event type constant
    Message        -> Human-readable description
    Object         -> Changed data (optional)
}
```

---

## Security Considerations

### Sensitive Attribute

Properties marked with `[Sensitive]` should never be exposed in API responses:

```csharp
[Sensitive]
public string? CustomAuthenticationCode { get; set; }

[Sensitive]
public string? JwtRsaPrivateKey { get; set; }

[Sensitive]
public string? LdapLookupPassword { get; set; }
```

### Password Handling

- `User.Password` is only populated for save operations, never returned
- `User.HasLocalPassword` indicates if local auth is configured
- `UserPasswordReset` handles password changes without exposing existing password

---

## Extension Points

### App-Specific Extensions

Use `DataObjects.App.cs` for custom properties:

```csharp
// DataObjects.App.cs
public partial class DataObjects {
    public partial class User {
        public string? MyCustomUserProperty { get; set; }
    }
}
```

### FreeCICD Extensions

For CI/CD-specific models, use `DataObjects.App.FreeCICD.cs`:

```csharp
// DataObjects.App.FreeCICD.cs
public partial class DataObjects {
    public class DevopsProjectInfo { ... }
    public class DevopsPipelineDefinition { ... }
    public class PipelineListItem { ... }
    public class PipelineDashboardResponse { ... }
}
```

---

## Dependencies

```xml
<ItemGroup>
  <PackageReference Include="System.Runtime.Caching" Version="10.0.0" />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\FreeCICD.Plugins\FreeCICD.Plugins.csproj" />
</ItemGroup>
```

- **System.Runtime.Caching**: For `Caching.cs` memory cache utilities
- **FreeCICD.Plugins**: Plugin model definitions

---

## Usage Examples

### Creating a Response

```csharp
var response = new DataObjects.BooleanResponse {
    Result = true,
    Messages = new List<string> { "Operation completed successfully" }
};
```

### Pipeline Dashboard Response

```csharp
var response = new DataObjects.PipelineDashboardResponse {
    Pipelines = pipelineItems,
    TotalCount = pipelineItems.Count,
    Success = true,
    AvailableVariableGroups = variableGroups
};
```

### User Filtering

```csharp
var filter = new DataObjects.FilterUsers {
    TenantId = tenantId,
    Page = 1,
    RecordsPerPage = 25,
    Sort = "LastName",
    SortOrder = "ASC",
    FilterDepartments = new[] { deptId }
};
```

### SignalR Update

```csharp
var update = new DataObjects.SignalRUpdate {
    TenantId = tenantId,
    ItemId = userId,
    UpdateType = DataObjects.SignalRUpdateType.User,
    Message = "User profile updated"
};
```

---

## File Descriptions

| File | Description |
|------|-------------|
| `DataObjects.cs` | Core DTOs, enums, and common types |
| `DataObjects.App.FreeCICD.cs` | Azure DevOps and Pipeline Dashboard DTOs |
| `DataObjects.SignalR.cs` | Real-time update types and models |
| `DataObjects.Departments.cs` | Department and DepartmentGroup DTOs |
| `DataObjects.Tags.cs` | Tag system DTOs |
| `DataObjects.UserGroups.cs` | User group DTOs |
| `DataObjects.UDFLabels.cs` | User-defined field label DTOs |
| `DataObjects.ActiveDirectory.cs` | LDAP lookup result models |
| `DataObjects.Ajax.cs` | AJAX-specific request/response |
| `DataObjects.Services.cs` | Service configuration models |
| `GlobalSettings.cs` | Runtime state container |
| `GlobalSettings.App.FreeCICD.cs` | FreeCICD pipeline settings |
| `Caching.cs` | Memory cache utilities |

---

## Recent Changes

### December 2025 - TriggerBranch Property

Added `TriggerBranch` property to `PipelineListItem` to show the actual branch from the most recent build. This is more accurate than `DefaultBranch` which shows the repository's default branch.

See: [022-Branch-Display-Fix-Sanity-Check.md](../docs/022-Branch-Display-Fix-Sanity-Check.md)

---

## Best Practices

1. **Keep DTOs Simple**: No business logic in data objects
2. **Use Partial Classes**: Extend via `.App.cs` files
3. **Mark Sensitive Data**: Always use `[Sensitive]` attribute
4. **Initialize Collections**: Always initialize lists to avoid null refs
5. **Use ActionResponseObject**: Inherit for consistent response patterns
6. **Document New Properties**: Add XML comments for new fields
