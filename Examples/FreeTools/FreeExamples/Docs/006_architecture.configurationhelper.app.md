# FreeCRM Hook: ConfigurationHelper.App.cs

> Custom configuration properties — extending the appsettings pipeline with app-specific values.

**File:** `{ProjectName}.DataObjects/ConfigurationHelper.App.cs`
**Classes:** `partial interface IConfigurationHelper`, `partial class ConfigurationHelper`, `partial class ConfigurationHelperLoader`, `partial class ConfigurationHelperConnectionStrings`
**Complexity:** MEDIUM — 4 partial type extensions that work together

---

## How the Configuration Pipeline Works

```
appsettings.json → Program.App.cs:ConfigurationHelpersLoadApp → ConfigurationHelperLoader
                                                                        ↓
                                          ConfigurationHelper reads from _loader
                                                                        ↓
                                          IConfigurationHelper injected everywhere via DI
```

**Three files work together:**

1. `ConfigurationHelper.App.cs` — Declare properties (this doc)
2. `Program.App.cs` → `ConfigurationHelpersLoadApp` — Load values from appsettings
3. Your custom file — Implementation logic (if any)

---

## The Four Partials

### 1. IConfigurationHelper (Interface)

Declare the contract. All consumers depend on this interface via DI.

```csharp
public partial interface IConfigurationHelper
{
    public string? PAT { get; }
    public string? OrgName { get; }
    public string? ProjectId { get; }
}
```

### 2. ConfigurationHelper (Implementation)

Read values from the private `_loader` field.

```csharp
public partial class ConfigurationHelper : IConfigurationHelper
{
    public string? PAT { get { return _loader.PAT; } }
    public string? OrgName { get { return _loader.OrgName; } }
    public string? ProjectId { get { return _loader.ProjectId; } }
}
```

### 3. ConfigurationHelperLoader (Data Carrier)

Settable properties populated during startup by `ConfigurationHelpersLoadApp`.

```csharp
public partial class ConfigurationHelperLoader
{
    public string? PAT { get; set; }
    public string? OrgName { get; set; }
    public string? ProjectId { get; set; }
}
```

### 4. ConfigurationHelperConnectionStrings

Custom connection strings beyond the default database connection.

```csharp
public partial class ConfigurationHelperConnectionStrings
{
    public string? RedisConnection { get; set; }
    public string? ExternalApiConnection { get; set; }
}
```

---

## Complete Example: Adding Azure DevOps Config

**Step 1 — Declare properties** in `ConfigurationHelper.App.cs` (or your custom file):

```csharp
// In {ProjectName}.App.Config.cs:
public partial interface IConfigurationHelper
{
    public string? PAT { get; }
    public string? OrgName { get; }
}

public partial class ConfigurationHelper : IConfigurationHelper
{
    public string? PAT { get { return _loader.PAT; } }
    public string? OrgName { get { return _loader.OrgName; } }
}

public partial class ConfigurationHelperLoader
{
    public string? PAT { get; set; }
    public string? OrgName { get; set; }
}
```

**Step 2 — Load values** in `Program.App.cs`:

```csharp
public static ConfigurationHelperLoader ConfigurationHelpersLoadApp(
    ConfigurationHelperLoader loader, WebApplicationBuilder builder)
{
    var output = loader;
    output = MyConfigurationHelpersLoadApp(output, builder);
    return output;
}
```

**Step 3 — Implementation** in `{ProjectName}.App.Program.cs`:

```csharp
public static ConfigurationHelperLoader MyConfigurationHelpersLoadApp(
    ConfigurationHelperLoader loader, WebApplicationBuilder builder)
{
    var output = loader;
    output.PAT = builder.Configuration.GetValue<string>("App:AzurePAT");
    output.OrgName = builder.Configuration.GetValue<string>("App:AzureOrgName");
    return output;
}
```

**Step 4 — Use anywhere** via DI:

```csharp
public class MyService
{
    private readonly IConfigurationHelper _config;

    public MyService(IConfigurationHelper config) { _config = config; }

    public string GetOrg() => _config.OrgName ?? "";
}
```

---

## ⚠️ Important: File Location

`ConfigurationHelper.App.cs` should ONLY exist in the **DataObjects** project, NOT in the Server project. Having it in both causes partial class conflicts across assemblies.

---

## Suggested File Names

| Scenario | File Name |
|----------|-----------|
| All config properties in one file | `{ProjectName}.App.Config.cs` |
| Split by feature | `{ProjectName}.App.Config.{Feature}.cs` |

---

*Category: 006_architecture*
*Source: `ReferenceProjects/FreeCRM-main/CRM.DataObjects/ConfigurationHelper.App.cs`*
