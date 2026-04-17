# FreeCRM Hook: Program.App.cs

> Server startup lifecycle hooks — register services, add middleware, load configuration.

**File:** `{ProjectName}/Program.App.cs`
**Class:** `partial class Program`
**Complexity:** HIGH — 6 hook methods covering the entire startup pipeline

---

## Hook Methods

### AppModifyBuilderStart

```csharp
public static WebApplicationBuilder AppModifyBuilderStart(WebApplicationBuilder builder)
```

**Called:** Very early in `Program.cs`, before default services are registered.
**Use for:** Adding early configuration sources, logging providers, or services that other registrations depend on.

**Suggested tie-in:**

```csharp
// In Program.App.cs (hook file) — add one line:
public static WebApplicationBuilder AppModifyBuilderStart(WebApplicationBuilder builder)
{
    var output = builder;
    output = MyAppModifyBuilderStart(output);  // ← tie-in
    return output;
}

// In {ProjectName}.App.Program.cs (your file):
public static WebApplicationBuilder MyAppModifyBuilderStart(WebApplicationBuilder builder)
{
    var output = builder;
    output.Services.AddSingleton<IMyEarlyService, MyEarlyService>();
    return output;
}
```

**Custom file:** `{ProjectName}.App.Program.cs` or `{ProjectName}.App.Program.Services.cs`

---

### AppModifyBuilderEnd

```csharp
public static WebApplicationBuilder AppModifyBuilderEnd(WebApplicationBuilder builder)
```

**Called:** After all default FreeCRM services are registered.
**Use for:** Registering BackgroundService, custom DI services, HttpClient factories — anything that depends on the base framework being fully configured.

**Suggested tie-in:**

```csharp
// In Program.App.cs:
public static WebApplicationBuilder AppModifyBuilderEnd(WebApplicationBuilder builder)
{
    var output = builder;
    output = MyAppModifyBuilderEnd(output);
    return output;
}

// In {ProjectName}.App.Program.cs:
public static WebApplicationBuilder MyAppModifyBuilderEnd(WebApplicationBuilder builder)
{
    var output = builder;
    output.Services.AddHostedService<PipelineMonitorService>();
    output.Services.AddMemoryCache();
    return output;
}
```

**Real-world:** FreeCICD uses this to register `PipelineMonitorService` (BackgroundService).

---

### AppModifyStart

```csharp
public static WebApplication AppModifyStart(WebApplication app)
```

**Called:** After `app` is built but before the middleware pipeline runs.
**Use for:** Middleware registration (e.g., `UseApiKeyAuth()`), CORS, custom request handling.

**Suggested tie-in:**

```csharp
// In Program.App.cs:
public static WebApplication AppModifyStart(WebApplication app)
{
    var output = app;
    output = MyAppModifyStart(output);
    return output;
}

// In {ProjectName}.App.Program.cs:
public static WebApplication MyAppModifyStart(WebApplication app)
{
    var output = app;
    output.UseApiKeyAuth();
    return output;
}
```

**Real-world:** FreeGLBA uses this pattern for API key middleware.

**⚠️ Note:** Middleware order matters. If your middleware must run before `UseAuthentication()`, you may need to modify `Program.cs` directly. Use a `.snippet` file to document required placement.

---

### AppModifyEnd

```csharp
public static WebApplication AppModifyEnd(WebApplication app)
```

**Called:** After the full middleware pipeline is configured.
**Use for:** Final app tweaks, diagnostic endpoints, health checks.

---

### AuthenticationPoliciesApp

```csharp
public static List<string> AuthenticationPoliciesApp { get; }
```

**Called:** During auth policy registration in `Program.cs`.
**Use for:** Declaring custom auth policy names that your endpoints reference.

**Suggested tie-in:**

```csharp
// In Program.App.cs:
public static List<string> AuthenticationPoliciesApp {
    get {
        var output = new List<string>();
        output.Add("ApiKeyPolicy");
        return output;
    }
}
```

---

### ConfigurationHelpersLoadApp

```csharp
public static ConfigurationHelperLoader ConfigurationHelpersLoadApp(
    ConfigurationHelperLoader loader, WebApplicationBuilder builder)
```

**Called:** During configuration loading in `Program.cs`.
**Use for:** Reading app-specific values from `appsettings.json` into the `ConfigurationHelperLoader`.

**Suggested tie-in:**

```csharp
// In Program.App.cs — add one line:
public static ConfigurationHelperLoader ConfigurationHelpersLoadApp(
    ConfigurationHelperLoader loader, WebApplicationBuilder builder)
{
    var output = loader;
    output = MyConfigurationHelpersLoadApp(output, builder);  // ← tie-in
    return output;
}

// In {ProjectName}.App.Program.cs:
public static ConfigurationHelperLoader MyConfigurationHelpersLoadApp(
    ConfigurationHelperLoader loader, WebApplicationBuilder builder)
{
    var output = loader;
    output.PAT = builder.Configuration.GetValue<string>("App:AzurePAT");
    output.OrgName = builder.Configuration.GetValue<string>("App:AzureOrgName");
    return output;
}
```

**Pair with:** `ConfigurationHelper.App.cs` (see `006_architecture.configurationhelper.app.md`) to declare the properties being loaded.

**Real-world:** FreeCICD loads 5 Azure DevOps config values this way.

---

## Suggested File Names

| Scenario | File Name |
|----------|-----------|
| General startup customization | `{ProjectName}.App.Program.cs` |
| Service registration (if complex) | `{ProjectName}.App.Program.Services.cs` |
| Middleware registration (if complex) | `{ProjectName}.App.Program.Middleware.cs` |

---

*Category: 006_architecture*
*Source: `ReferenceProjects/FreeCRM-main/CRM/Program.App.cs`*
