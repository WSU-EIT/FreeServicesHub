# FreeTools Solution

A .NET 10 solution containing a suite of CLI analysis tools, a FreeCRM-based example application, and reference implementations that demonstrate how to extend the FreeCRM framework.

Developed by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

---

## Solution Overview

```
FreeTools.sln
│
├── FreeTools/                           # CLI analysis & testing tool suite
├── FreeExamples/                        # Active FreeCRM-based application + docs
└── ReferenceProjects/                   # Read-only FreeCRM framework implementations
    ├── FreeCRM-main/                    #   The original FreeCRM framework (upstream)
    ├── FreeCRM-FreeExamples_base/       #   Clean FreeCRM renamed to FreeExamples namespace
    ├── FreeCICD-main/                   #   CI/CD pipeline dashboard (extends FreeCRM)
    └── FreeGLBA-main/                   #   GLBA compliance tracking (extends FreeCRM)
```

---

## FreeTools Suite

A collection of .NET CLI tools for analyzing, testing, and documenting web applications. All tools target .NET 10.

### Pipeline Tools (orchestrated by AppHost)

| Tool | Purpose |
|------|---------|
| **[FreeTools.AppHost](FreeTools/FreeTools.AppHost/)** | .NET Aspire orchestrator — runs the full analysis pipeline against a target web app |
| **[FreeTools.Core](FreeTools/FreeTools.Core/)** | Shared library — CLI arg parsing, thread-safe console output, route parsing, path utilities |
| **[FreeTools.EndpointMapper](FreeTools/FreeTools.EndpointMapper/)** | Scans Blazor `.razor` files for `@page` directives and `[Authorize]` attributes → `pages.csv` |
| **[FreeTools.EndpointPoker](FreeTools/FreeTools.EndpointPoker/)** | HTTP GET against every discovered route, saves HTML responses for regression testing |
| **[FreeTools.BrowserSnapshot](FreeTools/FreeTools.BrowserSnapshot/)** | Playwright-powered full-page screenshots with SPA-aware timing, auto-retry, and metadata |
| **[FreeTools.WorkspaceInventory](FreeTools/FreeTools.WorkspaceInventory/)** | Roslyn-powered codebase scan → CSV with file metrics, types, namespaces, routes, and auth info |
| **[FreeTools.WorkspaceReporter](FreeTools/FreeTools.WorkspaceReporter/)** | Aggregates all tool outputs into a markdown dashboard with screenshot gallery and health metrics |
| **[FreeTools.AccessibilityScanner](FreeTools/FreeTools.AccessibilityScanner/)** | Multi-site accessibility audit using Playwright + optional WAVE API for WCAG analysis |

### Standalone Tools

| Tool | Purpose |
|------|---------|
| **[FreeTools.ForkCRM](FreeTools/FreeTools.ForkCRM/)** | Clone FreeCRM from GitHub, remove optional modules, rename the project — outputs a ready-to-build project |

### Output

| Project | Purpose |
|---------|---------|
| **[FreeTools.Docs](FreeTools/Docs/)** | Content project holding generated pipeline outputs under `runs/{Project}/{Branch}/latest/` |

### Pipeline Architecture (v2.1)

```
Phase 0: Start target web app (BlazorApp1 or --target YourProject)
    │
    ▼
Phase 1: Static Analysis (parallel)
    ├─► EndpointMapper ──────► pages.csv
    └─► WorkspaceInventory ──► workspace-inventory.csv
    │
    ▼
Phase 2: EndpointPoker ──────► snapshots/*.html
    │
    ▼
Phase 3: BrowserSnapshot ───► snapshots/*.png + metadata.json
    │
    ▼
Phase 4: WorkspaceReporter ──► {Project}-Report.md
```

### Quick Start

```bash
# Run the full pipeline
cd FreeTools/FreeTools.AppHost
dotnet run

# Run against a specific project
dotnet run -- --target YourProjectName

# View results
ls FreeTools/Docs/runs/BlazorApp1/main/latest/
```

---

## FreeExamples

The active development project — a FreeCRM-based Blazor application used as the working example for this solution.

| Project | Purpose |
|---------|---------|
| **FreeExamples** | ASP.NET Core server — hosts Blazor WebAssembly, REST API, SignalR hub, authentication |
| **FreeExamples.Client** | Blazor WebAssembly UI — pages, components, state management (`BlazorDataModel`), helpers |
| **FreeExamples.DataAccess** | Business logic & data layer — EF Core operations, Graph API, encryption, JWT, migrations |
| **FreeExamples.DataObjects** | DTOs, view models, configuration helpers, caching, API endpoint constants |
| **FreeExamples.EFModels** | Entity Framework Core models — User, Department, Tag, Setting, FileStorage, etc. |
| **FreeExamples.Plugins** | Plugin system — runtime-loadable extensions with encryption support |
| **FreeExamples Docs** | Documentation project — guides, patterns, style, architecture docs |

---

## Reference Projects

Read-only reference implementations showing how the FreeCRM framework is extended for different purposes. Each was created using the **ForkCRM** tool (clone → remove modules → rename).

### FreeCRM-main (The Original Framework)

The upstream FreeCRM framework. All other projects are derived from this. Contains the base architecture: multi-tenant Blazor WebAssembly app with partial-class extension points.

| Project | Purpose |
|---------|---------|
| CRM | ASP.NET Core server with `Program.App.cs` extension hooks |
| CRM.Client | Blazor WebAssembly UI with `DataModel.App.cs`, `Helpers.App.cs` |
| CRM.DataAccess | Data layer with `DataAccess.App.cs` extension point |
| CRM.DataObjects | DTOs with `DataObjects.App.cs`, `ConfigurationHelper.App.cs` |
| CRM.EFModels | Entity Framework models |
| CRM.Plugins | Plugin system |

### FreeCRM-FreeExamples_base (Clean Renamed Copy)

A clean copy of FreeCRM renamed to the `FreeExamples` namespace using the Rename tool. This serves as the baseline — you can diff it against `FreeExamples/` to see exactly what customizations have been made.

### FreeCICD-main (CI/CD Pipeline Dashboard)

A FreeCRM extension for monitoring Azure DevOps CI/CD pipelines in real-time. Demonstrates the full extension pattern with custom pages, API endpoints, SignalR-powered live updates, and background services.

| Project | Purpose |
|---------|---------|
| FreeCICD | Server — adds `FreeCICD.App.Program.cs`, `FreeCICD.App.API.cs`, `FreeCICD.App.Config.cs`, `FreeCICD.App.PipelineMonitorService.cs` |
| FreeCICD.Client | Blazor UI — pipeline dashboard, wizard, import UI, SignalR connection viewer |
| FreeCICD.DataAccess | Azure DevOps API integration — pipelines, Git files, resources, import/validation |
| FreeCICD.DataObjects | CI/CD-specific DTOs and settings |
| FreeCICD.EFModels | Entity Framework models |
| FreeCICD.Plugins | Plugin system |

### FreeGLBA-main (GLBA Compliance Tracking)

A FreeCRM extension for tracking access to protected financial information under GLBA regulations. Demonstrates the extension pattern plus external API with API key authentication and a published NuGet client library.

| Project | Purpose |
|---------|---------|
| FreeGLBA | Server — adds GLBA API controller, API key middleware, request logging |
| FreeGLBA.Client | Blazor UI — compliance dashboard, access events, data subjects, source systems, reports |
| FreeGLBA.DataAccess | GLBA-specific data operations, external API processing, API key validation |
| FreeGLBA.DataObjects | GLBA DTOs, external API models, endpoint constants |
| FreeGLBA.EFModels | EF models — AccessEvent, SourceSystem, DataSubject, ComplianceReport, ApiRequestLog |
| FreeGLBA.Plugins | Plugin system |
| FreeGLBA.NugetClient | Published NuGet client library (`FreeGLBA.Client` on nuget.org) for external integrations |
| FreeGLBA.NugetClientPublisher | CLI tool for building, packing, and publishing the NuGet package |
| FreeGLBA.TestClient | Test client using project reference (for development/debugging) |
| FreeGLBA.TestClientWithNugetPackage | Test client using published NuGet package (validates consumer experience) |

---

## FreeCRM Extension Pattern

The core philosophy: **never modify base framework files**. All customizations go through a layered extension system:

### How It Works

1. **Framework files** (`Program.cs`, `DataController.cs`, etc.) — shipped by FreeCRM, never modified directly
2. **`.App.` hook files** (`Program.App.cs`, `DataController.App.cs`, etc.) — shipped with empty methods that are called at specific lifecycle points
3. **`{ProjectName}.App.{Feature}` files** — your custom code, called from the hook files with single-line additions

### Example: Adding Custom Configuration

```
Program.cs                          ← Framework file (never touch)
    └── calls Program.App.cs        ← Hook file (add one line)
            └── calls FreeCICD.App.Program.cs   ← Your code
```

In `Program.App.cs` (the hook file):
```csharp
public static ConfigurationHelperLoader ConfigurationHelpersLoadApp(
    ConfigurationHelperLoader loader, WebApplicationBuilder builder)
{
    var output = loader;
    output = MyConfigurationHelpersLoadApp(output, builder);  // ← single line added
    return output;
}
```

In `FreeCICD.App.Program.cs` (your custom file):
```csharp
public static ConfigurationHelperLoader MyConfigurationHelpersLoadApp(
    ConfigurationHelperLoader loader, WebApplicationBuilder builder)
{
    output.PAT = builder.Configuration.GetValue<string>("App:AzurePAT");
    output.OrgName = builder.Configuration.GetValue<string>("App:AzureOrgName");
    // ...
    return output;
}
```

### Why This Pattern?

When the FreeCRM framework updates:
1. Copy over the updated framework files (they're untouched)
2. Diff only the few `.App.` hook files where you added single-line calls
3. Your `{ProjectName}.App.{Feature}` files are completely untouched

This makes framework upgrades clean — no need to diff every file in the project.

### Key Hook Points

| Hook File | Methods |
|-----------|---------|
| `Program.App.cs` | `AppModifyBuilderStart()`, `AppModifyBuilderEnd()`, `AppModifyStart()`, `AppModifyEnd()`, `ConfigurationHelpersLoadApp()` |
| `DataController.App.cs` | App-specific API endpoints (partial class extension) |
| `DataAccess.App.cs` | App-specific data operations (partial class extension) |
| `DataObjects.App.cs` | App-specific DTOs and models |
| `DataModel.App.cs` | App-specific client-side state |
| `Helpers.App.cs` | App-specific client-side helpers |
| `ConfigurationHelper.App.cs` | App-specific configuration properties |

### Naming Convention for Custom Files

| Pattern | Example | Description |
|---------|---------|-------------|
| `{ProjectName}.App.{Feature}.cs` | `FreeCICD.App.API.cs` | Server-side extension |
| `{ProjectName}.App.{Feature}.razor` | `FreeCICD.App.UI.Wizard.razor` | UI component extension |
| `{Feature}.App.{SubFeature}.razor` | `About.App.razor` | Simple hook component |

---

## Running Individual Tools

```bash
# Full pipeline via Aspire
cd FreeTools/FreeTools.AppHost
dotnet run

# EndpointMapper
cd FreeTools/FreeTools.EndpointMapper
dotnet run -- <rootToScan> <csvOutputPath> [--clean]

# EndpointPoker
cd FreeTools/FreeTools.EndpointPoker
dotnet run -- <baseUrl> <csvPath> <outputDir> [maxThreads]

# BrowserSnapshot
cd FreeTools/FreeTools.BrowserSnapshot
dotnet run -- <baseUrl> <csvPath> <outputDir> [maxThreads]

# WorkspaceInventory
cd FreeTools/FreeTools.WorkspaceInventory
dotnet run -- <rootDir> <csvOutputPath> [--noCounts]

# WorkspaceReporter
cd FreeTools/FreeTools.WorkspaceReporter
dotnet run -- <repoRoot> <outputPath>

# ForkCRM
cd FreeTools/FreeTools.ForkCRM
dotnet run -- --name MyProject --modules all --output "C:\repos\MyProject"

# AccessibilityScanner
cd FreeTools/FreeTools.AccessibilityScanner
dotnet run   # configure sites in appsettings.json
```

---

## Build

```bash
dotnet build FreeTools.slnx
```

---

## 📬 About

**FreeTools** is developed and maintained by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

We build internal tools and automation to support enrollment management processes across WSU.

📧 Questions or feedback? Visit our [team page](https://em.wsu.edu/eit/meet-our-staff/) or open an issue on [GitHub](https://github.com/WSU-EIT/FreeTools/issues)
