# FreeCRM: Extension Hook Pattern Guide

> How to extend FreeCRM without modifying framework files — the `.App.` hook system and one-line tie-in pattern.

**Source:** FreeCRM base template (public), public examples FreeCICD and FreeGLBA

---

## Document Index

| Section | Line | Description |
|---------|------|-------------|
| [Overview](#overview) | ~30 | What the hook pattern is and why it exists |
| [The Three Layers](#the-three-layers) | ~50 | Framework → Hook → Custom |
| [Hook File Inventory](#hook-file-inventory) | ~100 | All shipped hook files and their methods |
| [One-Line Tie-In Pattern](#one-line-tie-in-pattern) | ~170 | How to wire your code into hook files |
| [Razor Hook Pattern](#razor-hook-pattern) | ~270 | How `.App.razor` hooks delegate to components |
| [Partial Class Extension](#partial-class-extension) | ~320 | Adding methods via partial classes (no hook needed) |
| [Exceptions](#exceptions-to-the-pattern) | ~370 | Middleware and other cases that require Program.cs changes |
| [Framework Update Workflow](#framework-update-workflow) | ~400 | How to update FreeCRM cleanly |
| [Real-World Examples](#real-world-examples) | ~440 | FreeCICD and FreeGLBA changes cataloged |

---

## Overview

FreeCRM's core design goal: **never modify base framework files**. All customizations go through a layered extension system so that framework updates are clean and predictable.

**The problem:** When you modify `Program.cs`, `DataController.cs`, or other framework files directly, every framework update becomes a merge nightmare — you have to diff every single file.

**The solution:** FreeCRM ships **hook files** (`.App.` files) with empty methods that are called at specific lifecycle points. You add one-line calls in these hook files to delegate to your custom code files.

---

## The Three Layers

```
Layer 1: FRAMEWORK FILES (never modify)
    Program.cs, DataController.cs, DataAccess.cs, DataObjects.cs, etc.
    These come from FreeCRM and should remain identical across all forks.

Layer 2: HOOK FILES (modify minimally — single-line additions only)
    Program.App.cs, DataController.App.cs, DataAccess.App.cs, etc.
    Shipped with empty methods. You add one-line calls to your custom code.

Layer 3: CUSTOM FILES (your code — untouched by framework updates)
    {ProjectName}.App.Program.cs, {ProjectName}.App.API.cs, etc.
    All your custom logic goes here.
```

**During framework updates:**
1. Copy over all Layer 1 files (they were never modified)
2. Diff only the Layer 2 hook files where you added single-line calls
3. Layer 3 files are completely untouched

---

## Hook File Inventory

These files ship with every FreeCRM project. Each contains empty methods called from the framework.

### Server Project (`{ProjectName}/`)

| Hook File | Methods | Called From |
|-----------|---------|-------------|
| `Program.App.cs` | `AppModifyBuilderStart(builder)` | `Program.cs` — before services are registered |
| | `AppModifyBuilderEnd(builder)` | `Program.cs` — after services are registered |
| | `AppModifyStart(app)` | `Program.cs` — before middleware pipeline |
| | `AppModifyEnd(app)` | `Program.cs` — after middleware pipeline |
| | `AuthenticationPoliciesApp` | `Program.cs` — auth policy registration |
| | `ConfigurationHelpersLoadApp(loader, builder)` | `Program.cs` — configuration loading |
| `DataController.App.cs` | `YourEndpoint()` (example) | N/A — partial class, methods are direct API endpoints |
| | `SignalRUpdateApp(update)` | `DataController.cs` — custom SignalR processing |

### DataAccess Project (`{ProjectName}.DataAccess/`)

| Hook File | Methods | Called From |
|-----------|---------|-------------|
| `DataAccess.App.cs` | `AppLanguage` | `DataAccess.Language.cs` — custom language tags |
| | `DataAccessAppInit()` | `DataAccess.cs` — initialization |
| | `DeleteAllPendingDeletedRecordsApp(TenantId, OlderThan)` | `DataAccess.cs` — cleanup |
| | `DeleteRecordImmediatelyApp(Type, RecordId, CurrentUser)` | `DataAccess.cs` — immediate delete |
| | `DeleteRecordsApp(Rec, CurrentUser)` | Various delete methods — cascade cleanup |
| `GraphAPI.App.cs` | Graph API extensions | `GraphAPI.cs` |
| `Utilities.App.cs` | Utility extensions | `Utilities.cs` |
| `RandomPasswordGenerator.App.cs` | Password generation extensions | `RandomPasswordGenerator.cs` |

### DataObjects Project (`{ProjectName}.DataObjects/`)

| Hook File | Methods | Called From |
|-----------|---------|-------------|
| `DataObjects.App.cs` | Custom DTOs (partial class) | N/A — add your own classes |
| `ConfigurationHelper.App.cs` | Custom config properties | `ConfigurationHelper.cs` |
| `GlobalSettings.App.cs` | Custom global settings | `GlobalSettings.cs` |

### Client Project (`{ProjectName}.Client/`)

| Hook File | Methods | Called From |
|-----------|---------|-------------|
| `DataModel.App.cs` | Custom client-side state | `DataModel.cs` |
| `Helpers.App.cs` | Custom client-side helpers | `Helpers.cs` |

### Client Razor Hook Files (`{ProjectName}.Client/Shared/AppComponents/`)

| Hook File | Purpose |
|-----------|---------|
| `About.App.razor` | Customize About page content |
| `AppSettings.App.razor` | Customize app settings UI |
| `Index.App.razor` | Customize home page content |
| `Settings.App.razor` | Customize settings page |
| `Edit*.App.razor` | Customize entity edit forms |

---

## One-Line Tie-In Pattern

The key pattern: modify a hook file by adding **one line** that calls your custom code.

### C# Example: Loading Custom Configuration

**Step 1:** Create your custom file:

```csharp
// File: FreeCICD.App.Program.cs (Layer 3 — your code)
namespace FreeCICD;

public partial class Program
{
    public static ConfigurationHelperLoader MyConfigurationHelpersLoadApp(
        ConfigurationHelperLoader loader, WebApplicationBuilder builder)
    {
        var output = loader;
        output.PAT = builder.Configuration.GetValue<string>("App:AzurePAT");
        output.OrgName = builder.Configuration.GetValue<string>("App:AzureOrgName");
        return output;
    }
}
```

**Step 2:** Add one line to the hook file:

```csharp
// File: Program.App.cs (Layer 2 — hook file, shipped by FreeCRM)
// Only the ConfigurationHelpersLoadApp method is shown — others remain empty.

public static ConfigurationHelperLoader ConfigurationHelpersLoadApp(
    ConfigurationHelperLoader loader, WebApplicationBuilder builder)
{
    var output = loader;
    output = MyConfigurationHelpersLoadApp(output, builder);  // ← THE ONE LINE
    return output;
}
```

**That's it.** The framework's `Program.cs` calls `ConfigurationHelpersLoadApp()`, which calls your `MyConfigurationHelpersLoadApp()`. Your code is isolated in its own file.

### C# Example: Adding Custom Config Properties

**Step 1:** Create your custom file with partial class extensions:

```csharp
// File: FreeCICD.App.Config.cs (Layer 3)
namespace FreeCICD;

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

**Step 2:** No hook file change needed! Partial classes automatically merge at compile time.

---

## Razor Hook Pattern

Razor `.App.razor` files follow the same principle — a single-line component reference that delegates to your custom component.

### Example: Customizing the Home Page

**The hook file** (shipped by FreeCRM):

```razor
@* File: Index.App.razor (Layer 2 — usually empty) *@
```

**FreeCICD's customization** — add one line:

```razor
@* File: Index.App.razor (Layer 2 — one line added) *@
@* The actual component is in FreeCICD.App.UI files *@
<Index_App_FreeCICD />
```

**The custom component** (your code):

```razor
@* File: FreeCICD.App.Pages.Pipelines.razor (Layer 3) *@
@page "/Pipelines"
@page "/{TenantCode}/Pipelines"
...full pipeline dashboard implementation...
```

### Blazor Class Name Rule

Remember: Blazor converts dots to underscores in component class names.

| File Name | Component Tag |
|-----------|---------------|
| `FreeCICD.App.UI.Dashboard.Pipelines.razor` | `<FreeCICD_App_UI_Dashboard_Pipelines />` |
| `FreeGLBA.App.EditSourceSystem.razor` | `<FreeGLBA_App_EditSourceSystem />` |

---

## Partial Class Extension

For adding new API endpoints, data access methods, or DTOs, you often don't need to modify any hook file at all. C# partial classes handle the merge:

> **Preferred endpoint pattern:** Three endpoints per entity (GetMany/SaveMany/DeleteMany).
> See [007_patterns.crud_api.md](007_patterns.crud_api.md).

```csharp
// File: FreeGLBA.App.DataController.cs (Layer 3)
// This extends the existing DataController without touching any framework file.
namespace FreeGLBA.Server.Controllers;

public partial class DataController
{
    [HttpPost("api/Data/GetSourceSystems")]
    public async Task<ActionResult<DataObjects.SourceSystemFilterResult>> GetSourceSystems(
        [FromBody] DataObjects.SourceSystemFilter filter)
    {
        return Ok(await da.GetSourceSystemsAsync(filter));
    }
}
```

The `partial class DataController` merges with the framework's `DataController` at compile time. No hook file modification needed.

Similarly for IDataAccess:

```csharp
// File: FreeGLBA.App.IDataAccess.cs (Layer 3)
public partial interface IDataAccess
{
    Task<DataObjects.SourceSystemFilterResult> GetSourceSystemsAsync(DataObjects.SourceSystemFilter filter);
    // ...60+ more methods
}
```

---

## Exceptions to the Pattern

Some customizations cannot be done purely through hook files:

### Middleware Registration

If your extension adds custom middleware (e.g., FreeGLBA's API key authentication), you must modify `Program.cs` or use the `AppModifyStart()` hook:

```csharp
// Option A: Modify Program.App.cs AppModifyStart (preferred)
public static WebApplication AppModifyStart(WebApplication app)
{
    var output = app;
    output.UseApiKeyAuth();  // ← Add middleware via hook
    return output;
}

// Option B: FreeGLBA ships a .snippet file documenting where to add in Program.cs
// This is the exception — requires a Program.cs edit if middleware order matters
```

### New Controllers (Not Partial)

If you create an entirely new controller class (not extending `DataController`), it just works — no hook needed:

```csharp
// File: FreeGLBA.App.GlbaController.cs
// This is a brand-new controller, not a partial extension.
[ApiController]
[Route("api/glba")]
public class GlbaController : ControllerBase { ... }
```

ASP.NET Core discovers it automatically via `AddControllers()`.

### Background Services

New `BackgroundService` classes need registration in `Program.cs` or via the builder hook:

```csharp
// In Program.App.cs
public static WebApplicationBuilder AppModifyBuilderEnd(WebApplicationBuilder builder)
{
    var output = builder;
    output.Services.AddHostedService<PipelineMonitorService>();  // ← Register service
    return output;
}
```

---

## Framework Update Workflow

When FreeCRM releases a framework update:

| Step | What to do | Files affected |
|------|-----------|----------------|
| 1 | Copy all framework files | `Program.cs`, `DataController.cs`, `DataAccess.cs`, all non-`.App.` files |
| 2 | Diff the hook files | Only `.App.` files where you added one-line calls |
| 3 | Re-add your one-line calls | Usually 1-3 lines across 1-2 hook files |
| 4 | Verify build | Your `{ProjectName}.App.*` files are untouched |

**Time estimate:** Minutes for hook file merges vs hours for full-file diffs.

### Diffing with the Base

The `ReferenceProjects/FreeCRM-FreeExamples_base/` folder in this solution is a clean FreeCRM rename — use it as a diff baseline:

```bash
# Compare your active project against the clean base
diff -r FreeExamples/ ReferenceProjects/FreeCRM-FreeExamples_base/ --exclude=obj --exclude=bin
```

---

## Real-World Examples

### FreeCICD Changes (CI/CD Pipeline Dashboard)

**Hook files modified (Layer 2):**

| File | Change | Lines Added |
|------|--------|-------------|
| `Program.App.cs` | Added `output = MyConfigurationHelpersLoadApp(output, builder);` in `ConfigurationHelpersLoadApp` | 1 line |
| `Index.App.razor` | Added `<Index_App_FreeCICD />` | 1 line |
| `Pipelines.App.razor` | Added `<Pipelines_App_FreeCICD />` | 1 line |

**Custom files created (Layer 3):**

| File | Purpose |
|------|---------|
| `FreeCICD.App.Program.cs` | Azure DevOps config loading |
| `FreeCICD.App.Config.cs` | Partial interface/class for PAT, OrgName, etc. |
| `FreeCICD.App.API.cs` | 15+ pipeline API endpoints |
| `FreeCICD.App.PipelineMonitorService.cs` | BackgroundService for live polling |
| `FreeCICD.App.DataAccess.cs` | IDataAccess + DataAccess with 40+ methods (split across 6 files) |
| `FreeCICD.App.DataObjects.cs` | Endpoints + 20+ DTOs |
| `FreeCICD.App.Settings.cs` | Environment config + YAML template |
| `FreeCICD.App.UI.Dashboard.*.razor` | 7 dashboard components |
| `FreeCICD.App.UI.Wizard.*.razor` | 12 wizard step components |
| `FreeCICD.App.Pages.*.razor` | 3 routable pages |

**Total: 3 lines added to hook files, 35+ custom files created.**

### FreeGLBA Changes (GLBA Compliance Tracking)

**Hook files modified (Layer 2):** None observed — all extensions use partial classes or new controllers.

**Custom files created (Layer 3):**

| File | Purpose |
|------|---------|
| `FreeGLBA.App.GlbaController.cs` | External REST API with API key auth |
| `FreeGLBA.App.DataController.cs` | Internal CRUD for 4 entities (25+ endpoints) |
| `FreeGLBA.App.ApiKeyMiddleware.cs` | Bearer token validation middleware |
| `FreeGLBA.App.ApiRequestLoggingAttribute.cs` | API request logging action filter |
| `FreeGLBA.App.SkipApiLoggingAttribute.cs` | Skip logging marker attribute |
| `FreeGLBA.App.DataAccess.cs` | 5 data access files for CRUD + API processing |
| `FreeGLBA.App.IDataAccess.cs` | 60+ interface methods |
| `FreeGLBA.App.DataObjects.cs` | DTOs for all entities |
| `FreeGLBA.App.DataObjects.ExternalApi.cs` | External API models |
| `FreeGLBA.App.DataObjects.ApiLogging.cs` | API logging DTOs |
| `FreeGLBA.App.Endpoints.cs` | Endpoint string constants |
| `FreeGLBA.App.*.razor` | 15+ pages and edit components |
| `FreeGLBA.App.EFDataModel.cs` | DbContext extensions |
| `FreeGLBA.App.*.cs` (EFModels) | 6 entity model files |

**Total: 0 lines in hook files (all partial class / new class extensions), 38+ custom files created.**

---

*Category: 006_architecture*
*Created: 2025-07-25*
*Source: Analysis of FreeCRM-main, FreeCICD-main, FreeGLBA-main extension patterns*
