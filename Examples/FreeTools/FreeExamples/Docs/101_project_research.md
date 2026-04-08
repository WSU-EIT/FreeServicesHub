# FreeTools Solution: Project Research Summary

> Comprehensive audit of every project in the FreeTools solution — what each file does, how the extension pattern works, and how the pieces fit together.

---

## Document Index

| Section | Description |
|---------|-------------|
| [Solution Map](#solution-map) | Full directory tree with project roles |
| [FreeTools Suite](#freetools-suite) | Each CLI tool's Program.cs, csproj, and behavior |
| [FreeExamples](#freeexamples) | Active development project structure |
| [FreeCRM Framework](#frecrm-framework-reference) | The upstream framework and its extension points |
| [FreeCRM-FreeExamples_base](#frecrm-freeexamples_base) | Clean renamed baseline |
| [FreeCICD](#freecicd---cicd-pipeline-dashboard) | CI/CD extension deep dive |
| [FreeGLBA](#freeglba---glba-compliance-tracking) | GLBA extension deep dive |
| [Extension Pattern](#extension-pattern-deep-dive) | How the `.App.` pattern works in practice |
| [Deep Dive Addendum](#deep-dive-addendum-phase-2) | Documentation audit + complete file change catalogs |
| [Files Read](#files-read-during-research) | Every file examined during this audit |

---

## Solution Map

```
FreeTools.sln (46 projects)
│
├── FreeTools/                               # CLI analysis & testing tools (.NET 10)
│   ├── FreeTools.AppHost/                   # Aspire orchestrator
│   ├── FreeTools.Core/                      # Shared CLI utilities library
│   ├── FreeTools.EndpointMapper/            # @page directive scanner → CSV
│   ├── FreeTools.EndpointPoker/             # HTTP GET route tester
│   ├── FreeTools.BrowserSnapshot/           # Playwright screenshot capture
│   ├── FreeTools.WorkspaceInventory/        # Roslyn-powered codebase metrics
│   ├── FreeTools.WorkspaceReporter/         # Markdown report generator
│   ├── FreeTools.AccessibilityScanner/      # WCAG/a11y scanner
│   ├── FreeTools.ForkCRM/                   # FreeCRM project forker
│   └── Docs/                               # Pipeline output repository
│
├── FreeExamples/                            # Active FreeCRM-based app
│   ├── FreeExamples/                        # Server (ASP.NET Core)
│   ├── FreeExamples.Client/                 # Client (Blazor WASM)
│   ├── FreeExamples.DataAccess/             # Data layer
│   ├── FreeExamples.DataObjects/            # DTOs
│   ├── FreeExamples.EFModels/               # EF Core models
│   ├── FreeExamples.Plugins/                # Plugin system
│   └── Docs/                               # This documentation project
│
└── ReferenceProjects/
    ├── FreeCRM-main/                        # Original FreeCRM (6 projects)
    ├── FreeCRM-FreeExamples_base/           # Clean rename (6 projects)
    ├── FreeCICD-main/                       # CI/CD extension (6 projects)
    └── FreeGLBA-main/                       # GLBA extension (10 projects)
```

---

## FreeTools Suite

### FreeTools.AppHost — Pipeline Orchestrator

**csproj:** `net10.0`, OutputType `Exe`, uses .NET Aspire  
**Dependencies:** References all other tool projects, Aspire AppHost SDK  
**README:** Comprehensive, accurate (v2.1)

**Program.cs findings:**
- Uses `System.CommandLine` for CLI parsing (`--skip-cleanup`, `--keep-backups`, `--target`)
- Default target is `BlazorApp1`
- Creates a `DistributedApplication` builder (Aspire)
- Manages output directories: `Docs/runs/{Project}/{Branch}/latest/`
- Gets current Git branch name for folder naming
- Supports backup retention with timestamped folders
- Pipeline phases:
  - Phase 0: Starts web app via `builder.AddProject<Projects.BlazorApp1>`
  - Phase 1: EndpointMapper + WorkspaceInventory (parallel, no web dependency)
  - Phase 2: EndpointPoker (waits for web app + EndpointMapper)
  - Phase 3: BrowserSnapshot (waits for web app + EndpointMapper)
  - Phase 4: WorkspaceReporter (waits for all above)
- Each tool gets environment variables for paths, delays, thread counts
- Uses `.WaitFor()` and `.WaitForCompletion()` for dependency ordering

### FreeTools.Core — Shared Library

**csproj:** `net10.0`, library (no OutputType)  
**README:** Accurate, covers all files

**Key classes:**
- `CliArgs` — Flag detection, option parsing, positional args, env var helpers (`GetEnvOrArg`, `GetEnvBool`)
- `ConsoleOutput` — Thread-safe banner/divider printing
- `RouteParser` — Parses `pages.csv`, detects route parameters (`{id}`)
- `PathSanitizer` — Route-to-filename conversion, byte formatting

### FreeTools.EndpointMapper — Route Discovery

**csproj:** `net10.0`, Exe, references FreeTools.Core  
**README:** Accurate (v2.2)

**Program.cs findings:**
- Optional startup delay via `START_DELAY_MS`
- Accepts root directory and CSV output path as positional args
- Scans all `.razor` files (excludes `bin/`, `obj/`, `repo/`)
- Regex: `@page "(?<route>[^"]+)"` for routes
- Regex: `@attribute \[Authorize` for auth detection
- Outputs CSV: `FilePath,Route,RequiresAuth,Project`
- Uses relative paths (not absolute) for privacy
- `--clean` flag to delete previous output directories
- Determines project name from file path

### FreeTools.EndpointPoker — HTTP Route Tester

**csproj:** `net10.0`, Exe, references FreeTools.Core  
**README:** Accurate (v2.2)

**Program.cs findings:**
- Default 5-second startup delay for server warmup
- Reads routes from CSV via `RouteParser.ParseRoutesFromCsvFileAsync()`
- Skips routes with parameters (e.g., `{id}`)
- Parallel HTTP GET via `SemaphoreSlim` (default 10 threads)
- Ordered result output using `ConcurrentDictionary<int, PokeResult>`
- Saves HTML responses to output directory
- Verifies Blazor framework file MIME types
- Reports: success (2xx), HTTP errors (4xx/5xx), connection errors
- SSL certificate validation disabled for development

### FreeTools.BrowserSnapshot — Screenshot Capture

**csproj:** `net10.0`, Exe, references FreeTools.Core, Microsoft.Playwright  
**README:** Accurate (v2.1)

**Program.cs findings:**
- Uses Playwright for real browser rendering (Chromium/Firefox/WebKit)
- Auto-installs Playwright browsers on first run
- Supports authentication — login with configurable credentials
- SPA-aware: uses `NetworkIdle` wait + configurable settle delay (default 3000ms)
- Auto-retry: screenshots < 10KB are retried with extra delay
- Captures JavaScript console errors during page load
- Writes `metadata.json` alongside each screenshot
- Parallel via `SemaphoreSlim` (default 10 threads)
- Configurable viewport size via `SCREENSHOT_VIEWPORT`
- Distinguishes auth-required vs public routes from CSV

### FreeTools.WorkspaceInventory — Codebase Metrics

**csproj:** `net10.0`, Exe, references FreeTools.Core, Microsoft.CodeAnalysis.CSharp, Microsoft.Extensions.FileSystemGlobbing  
**README:** Accurate (v2.0)

**Program.cs findings:**
- Uses Roslyn (`Microsoft.CodeAnalysis.CSharp`) for C# parsing
- Uses glob patterns for file matching (configurable include/exclude)
- Default includes: `*.cs`, `*.razor`, `*.csproj`, `*.sln`, `*.json`, `*.config`, `*.md`, `*.xml`, `*.yaml`, `*.yml`
- Default excludes: `bin`, `obj`, `.git`, `.vs`, `node_modules`, `packages`, `TestResults`, `repo`
- Extracts per-file: size, line count, char count, timestamps, namespace, declared types, routes, auth
- Classifies files by kind: RazorPage, RazorComponent, CSharpSource, ProjectFile, etc.
- Max parse size guard (default 1MB)
- Optional Azure DevOps deep links
- Parallel via `SemaphoreSlim` (default 10 threads)
- Outputs CSV with all metrics

### FreeTools.WorkspaceReporter — Report Generator

**csproj:** `net10.0`, Exe, references FreeTools.Core  
**README:** Accurate (v2.0)

**Program.cs findings:**
- Aggregates data from: workspace-inventory.csv, pages.csv, snapshots directory
- Generates sections: About, Workspace Overview, File Statistics, Code Distribution, Largest Files, Large File Warnings, Blazor Page Routes, Route Map, Screenshot Health, Screenshot Gallery, Tool Information
- Reads `metadata.json` files from BrowserSnapshot for screenshot health
- GitHub/Azure DevOps compatible markdown with collapsible sections
- Builds relative links for GitHub rendering

### FreeTools.AccessibilityScanner — A11y Audit

**csproj:** `net10.0`, Exe, references FreeTools.Core, Microsoft.Playwright  
**README:** Accurate (v1.0)

**Program.cs findings:**
- Standalone tool (not part of AppHost pipeline)
- Configured via `appsettings.json` (not CLI args)
- Multi-site support with per-site page lists and credentials
- Uses Playwright Chromium for rendering
- Captures: HTML, screenshots, console errors/warnings, images
- Accessibility analysis via axe-core (injected JS) + optional WAVE API
- Reports: per-page results with violation counts by severity (Critical/Serious/Moderate/Minor)
- Writes per-site output folders with summary reports and rules legend
- Parallel execution with configurable concurrency

### FreeTools.ForkCRM — Project Forker

**csproj:** `net10.0`, Exe, references LibGit2Sharp  
**README:** Accurate (v1.0)

**Program.cs findings:**
- Uses LibGit2Sharp for Git clone (not `git` CLI)
- Clones from `https://github.com/WSU-EIT/FreeCRM.git`
- Requires two external executables:
  - `Remove Modules from FreeCRM.exe` — strips optional features
  - `Rename FreeCRM.exe` — renames all namespaces/files
- Workflow: Clone → Copy tools → Remove modules → Rename → Cleanup → Read all files to memory → Write to output
- Valid modules: Tags, Appointments, Invoices, EmailTemplates, Locations, Payments, Services, all
- Cleans up `.git`, `.github`, `artifacts` directories
- Sets normal file attributes before deletion (for `.git` readonly files)
- Uses temp directory, then writes final output

### FreeTools.Docs — Output Repository

**csproj:** `net10.0`, content-only project  
**README:** Accurate (v2.1)

Structure: `runs/{Project}/{Branch}/latest/` contains all pipeline outputs.

---

## FreeExamples

The active development project — a FreeCRM-based Blazor application.

### FreeExamples (Server)

**Program.cs:** Identical structure to FreeCRM-main's `Program.cs` but in `FreeExamples` namespace.
- Calls `AppModifyBuilderStart()` at the top of `Main()`
- Standard FreeCRM setup: Radzen, SignalR, authentication, Blazor, etc.

**Program.App.cs:** Clean/empty hook file — all methods return input unchanged.
- `AppModifyBuilderStart(builder)` / `AppModifyBuilderEnd(builder)`
- `AppModifyStart(app)` / `AppModifyEnd(app)`
- `AuthenticationPoliciesApp` (empty list)
- `ConfigurationHelpersLoadApp(loader, builder)`

### FreeExamples.Client

Standard FreeCRM client structure:
- `DataModel.cs` / `DataModel.App.cs` — State management
- `Helpers.cs` / `Helpers.App.cs` — API helpers
- `Pages/` — Razor pages
- `Shared/AppComponents/` — Hook components (`.App.razor` files)

### FreeExamples.DataAccess

Comprehensive data layer with partial-class files:
- `DataAccess.cs` (base) + domain-specific files: `.Users.cs`, `.Departments.cs`, `.Tags.cs`, `.FileStorage.cs`, `.Authenticate.cs`, `.JWT.cs`, `.Encryption.cs`, `.SignalR.cs`, `.Ajax.cs`, `.Language.cs`, `.Plugins.cs`, `.Settings.cs`, `.Tenants.cs`, `.UDFLabels.cs`, `.Utilities.cs`, `.ActiveDirectory.cs`, `.CSharpCode.cs`, `.SeedTestData.cs`
- `DataAccess.App.cs` — Empty hook for app-specific operations
- Database migration files for all providers: SQLServer, MySQL, SQLite, PostgreSQL
- `GraphAPI.cs` / `GraphAPI.App.cs` — Microsoft Graph integration
- `Utilities.cs` / `Utilities.App.cs` — Helper utilities
- `RandomPasswordGenerator.cs` / `RandomPasswordGenerator.App.cs`

### FreeExamples.DataObjects

DTO and configuration layer:
- `DataObjects.cs` (base) + domain files: `.Tags.cs`, `.UserGroups.cs`, `.Services.cs`, `.Ajax.cs`, `.SignalR.cs`, `.ActiveDirectory.cs`, `.Departments.cs`, `.UDFLabels.cs`
- `DataObjects.App.cs` — Empty hook for app-specific DTOs
- `ConfigurationHelper.cs` / `ConfigurationHelper.App.cs` — DI configuration interface
- `GlobalSettings.cs` / `GlobalSettings.App.cs` — Application-wide settings
- `Caching.cs` — In-memory cache wrapper

### FreeExamples.EFModels

Entity Framework Core models:
- `EFDataModel.cs` — DbContext
- Entities: `User`, `Department`, `DepartmentGroup`, `UserGroup`, `UserInGroup`, `Tag`, `TagItem`, `Setting`, `Tenant`, `FileStorage`, `PluginCache`, `UDFLabel`
- `EFModelOverrides.cs` — Fluent API configuration overrides

### FreeExamples.Plugins

Plugin system:
- `Plugins.cs` — Plugin loading and execution
- `Encryption.cs` — Encryption utilities

---

## FreeCRM Framework (Reference)

**Location:** `ReferenceProjects/FreeCRM-main/`

The original upstream framework. Namespace: `CRM`.

### CRM (Server) Program.cs

Key structure:
```
Main()
├── AppModifyBuilderStart(builder)         ← Hook
├── Services registration (Radzen, SignalR, Auth, EF, etc.)
├── AppModifyBuilderEnd(builder)           ← Hook
├── app = builder.Build()
├── AppModifyStart(app)                    ← Hook
├── Middleware pipeline setup
├── AppModifyEnd(app)                      ← Hook
└── app.Run()
```

### CRM Program.App.cs

All hook methods are empty — they accept and return their inputs unchanged:
- `AppModifyBuilderStart(WebApplicationBuilder) → WebApplicationBuilder`
- `AppModifyBuilderEnd(WebApplicationBuilder) → WebApplicationBuilder`
- `AppModifyStart(WebApplication) → WebApplication`
- `AppModifyEnd(WebApplication) → WebApplication`
- `AuthenticationPoliciesApp → List<string>` (empty)
- `ConfigurationHelpersLoadApp(ConfigurationHelperLoader, WebApplicationBuilder) → ConfigurationHelperLoader`

### Project Structure (6 projects)

| Project | Namespace | Role |
|---------|-----------|------|
| CRM | CRM | ASP.NET Core server |
| CRM.Client | CRM.Client | Blazor WebAssembly UI |
| CRM.DataAccess | CRM.DataAccess | Business logic & data |
| CRM.DataObjects | CRM.DataObjects | DTOs & configuration |
| CRM.EFModels | CRM.EFModels | EF Core models |
| CRM.Plugins | CRM.Plugins | Plugin system |

---

## FreeCRM-FreeExamples_base

**Location:** `ReferenceProjects/FreeCRM-FreeExamples_base/`

A clean copy of FreeCRM renamed to the `FreeExamples` namespace using the Rename tool. Every file is structurally identical to FreeCRM-main, just with `CRM` → `FreeExamples` throughout.

**Purpose:** Serves as a diffable baseline. Compare `FreeExamples/` (active project) against this to see exactly what customizations have been made.

**Program.App.cs:** Identical to FreeCRM-main — all hooks empty.

---

## FreeCICD — CI/CD Pipeline Dashboard

**Location:** `ReferenceProjects/FreeCICD-main/`

A FreeCRM extension for monitoring Azure DevOps CI/CD pipelines in real-time.

### Custom Extension Files Found

#### Server-side (`FreeCICD/`)

| File | What it does |
|------|-------------|
| `FreeCICD.App.Program.cs` | Loads Azure DevOps config from appsettings (PAT, ProjectId, RepoId, Branch, OrgName) via `MyConfigurationHelpersLoadApp()` |
| `FreeCICD.App.Config.cs` | Adds partial interface properties and ConfigurationHelper/Loader extensions for PAT, ProjectId, RepoId, Branch, OrgName |
| `FreeCICD.App.API.cs` | Adds pipeline API endpoints: join/leave SignalR monitor group, pipeline CRUD, DevOps proxy endpoints |
| `FreeCICD.App.PipelineMonitorService.cs` | `BackgroundService` that polls Azure DevOps for pipeline status changes and broadcasts diffs via SignalR to subscribed clients |

#### Client-side (`FreeCICD.Client/`)

| File | What it does |
|------|-------------|
| `FreeCICD.App.Pages.Pipelines.razor` | Pipeline management page |
| `FreeCICD.App.Pages.Wizard.razor` | Pipeline setup wizard page |
| `FreeCICD.App.Pages.SignalRConnections.razor` | SignalR connection monitoring page |
| `FreeCICD.App.UI.Dashboard.Pipelines.razor` | Pipeline dashboard component |
| `FreeCICD.App.UI.Dashboard.PipelineCard.razor` | Individual pipeline card |
| `FreeCICD.App.UI.Dashboard.PipelineGroup.razor` | Grouped pipeline view |
| `FreeCICD.App.UI.Dashboard.FilterBar.razor` | Dashboard filter controls |
| `FreeCICD.App.UI.Dashboard.TableView.razor` | Table view of pipelines |
| `FreeCICD.App.UI.Dashboard.ViewControls.razor` | View toggle (card/table) |
| `FreeCICD.App.UI.Dashboard.VarGroupBadges.razor` | Variable group badges |
| `FreeCICD.App.UI.Wizard.razor` | Wizard orchestrator component |
| `FreeCICD.App.UI.Wizard.Stepper.razor` | Step progress indicator |
| `FreeCICD.App.UI.Wizard.StepHeader.razor` | Step header with nav buttons |
| `FreeCICD.App.UI.Wizard.Summary.razor` | Selection summary |
| `FreeCICD.App.UI.Wizard.StepPAT.razor` | PAT entry step |
| `FreeCICD.App.UI.Wizard.StepProject.razor` | Project selection step |
| `FreeCICD.App.UI.Wizard.StepRepository.razor` | Repository selection step |
| `FreeCICD.App.UI.Wizard.StepBranch.razor` | Branch selection step |
| `FreeCICD.App.UI.Wizard.StepCsproj.razor` | Csproj selection step |
| `FreeCICD.App.UI.Wizard.StepEnvironments.razor` | Environment config step |
| `FreeCICD.App.UI.Wizard.StepPipeline.razor` | Pipeline config step |
| `FreeCICD.App.UI.Wizard.StepPreview.razor` | Preview/review step |
| `FreeCICD.App.UI.Wizard.StepCompleted.razor` | Completion step |
| `FreeCICD.App.UI.Wizard.LoadingIndicator.razor` | Loading spinner |
| `FreeCICD.App.UI.Import.razor` | Pipeline import UI |
| `FreeCICD.App.UI.SignalRConnections.razor` | Connection viewer |

#### Data layer (`FreeCICD.DataAccess/`)

| File | What it does |
|------|-------------|
| `FreeCICD.App.DataAccess.cs` | Base data access extensions |
| `FreeCICD.App.DataAccess.DevOps.Dashboard.cs` | Dashboard data aggregation |
| `FreeCICD.App.DataAccess.DevOps.GitFiles.cs` | Azure DevOps Git file operations |
| `FreeCICD.App.DataAccess.DevOps.Pipelines.cs` | Pipeline CRUD against Azure DevOps API |
| `FreeCICD.App.DataAccess.DevOps.Resources.cs` | DevOps resource queries |
| `FreeCICD.App.DataAccess.Import.Operations.cs` | Pipeline import operations |
| `FreeCICD.App.DataAccess.Import.Validation.cs` | Import validation logic |

#### Data objects (`FreeCICD.DataObjects/`)

| File | What it does |
|------|-------------|
| `FreeCICD.App.DataObjects.cs` | CI/CD-specific DTOs (pipeline models, DevOps responses) |
| `FreeCICD.App.Settings.cs` | CI/CD-specific settings |

### How FreeCICD Hooks Into the Framework

1. **Program.App.cs** → `ConfigurationHelpersLoadApp()` calls `MyConfigurationHelpersLoadApp()` from `FreeCICD.App.Program.cs`
2. **ConfigurationHelper.App.cs** → Adds PAT/ProjectId/RepoId/Branch/OrgName properties via `partial class` and `partial interface`
3. **DataController.App.cs** → Pipeline endpoints are in `FreeCICD.App.API.cs` (partial class extension of `DataController`)
4. **DataAccess.App.cs** → Extended via `FreeCICD.App.DataAccess.*` files
5. **DataObjects.App.cs** → Extended via `FreeCICD.App.DataObjects.cs`

---

## FreeGLBA — GLBA Compliance Tracking

**Location:** `ReferenceProjects/FreeGLBA-main/`

Tracks access to protected financial information under GLBA (Gramm-Leach-Bliley Act) regulations. Demonstrates the extension pattern plus external API with middleware-based API key auth.

### Custom Extension Files Found

#### Server-side (`FreeGLBA/`)

| File | What it does |
|------|-------------|
| `FreeGLBA.App.GlbaController.cs` | REST API controller (`/api/glba/*`) for external event submission — log single events, batch events, query by subject |
| `FreeGLBA.App.DataController.cs` | Internal API endpoints for CRUD on SourceSystems, AccessEvents, DataSubjects, ComplianceReports |
| `FreeGLBA.App.ApiKeyMiddleware.cs` | Middleware that validates Bearer token API keys for external POST endpoints, stores source system in `HttpContext.Items` |
| `FreeGLBA.App.ApiRequestLoggingAttribute.cs` | Attribute for logging API requests |
| `FreeGLBA.App.SkipApiLoggingAttribute.cs` | Attribute to skip API logging on specific endpoints |

#### Client-side (`FreeGLBA.Client/`)

| File | What it does |
|------|-------------|
| `FreeGLBA.App.GlbaDashboard.razor` | Main GLBA compliance dashboard |
| `FreeGLBA.App.AccessEventsPage.razor` | Access event listing/filtering |
| `FreeGLBA.App.AccessorsPage.razor` | Top data accessors view |
| `FreeGLBA.App.SourceSystemsPage.razor` | Source system management |
| `FreeGLBA.App.DataSubjectsPage.razor` | Data subject management |
| `FreeGLBA.App.ComplianceReportsPage.razor` | Compliance report generation |
| `FreeGLBA.App.ApiLogDashboard.razor` | API request log dashboard |
| `FreeGLBA.App.ApiRequestLogs.razor` | API request log browser |
| `FreeGLBA.App.ViewApiRequestLog.razor` | Individual log detail view |
| `FreeGLBA.App.BodyLoggingSettings.razor` | Body logging configuration |
| `FreeGLBA.App.EditAccessEvent.razor` | Access event edit component |
| `FreeGLBA.App.EditSourceSystem.razor` | Source system edit component |
| `FreeGLBA.App.EditDataSubject.razor` | Data subject edit component |
| `FreeGLBA.App.EditComplianceReport.razor` | Compliance report edit component |
| `FreeGLBA.App.Helpers.cs` | GLBA-specific client helpers |

#### Data layer (`FreeGLBA.DataAccess/`)

| File | What it does |
|------|-------------|
| `FreeGLBA.App.DataAccess.cs` | Core GLBA data operations |
| `FreeGLBA.App.DataAccess.ExternalApi.cs` | External event processing (single + batch) |
| `FreeGLBA.App.DataAccess.ApiKey.cs` | API key validation and hashing |
| `FreeGLBA.App.DataAccess.ApiLogging.cs` | API request log storage/queries |
| `FreeGLBA.App.IDataAccess.cs` | Interface extensions for GLBA operations |

#### Data objects (`FreeGLBA.DataObjects/`)

| File | What it does |
|------|-------------|
| `FreeGLBA.App.DataObjects.cs` | GLBA DTOs (AccessEvent, SourceSystem, DataSubject, etc.) |
| `FreeGLBA.App.DataObjects.ExternalApi.cs` | External API request/response models |
| `FreeGLBA.App.DataObjects.ApiLogging.cs` | API logging DTOs |
| `FreeGLBA.App.Endpoints.cs` | API endpoint string constants |

#### EF Models (`FreeGLBA.EFModels/`)

| File | What it does |
|------|-------------|
| `FreeGLBA.App.EFDataModel.cs` | DbContext extensions for GLBA tables |
| `FreeGLBA.App.AccessEvent.cs` | AccessEvent entity |
| `FreeGLBA.App.SourceSystem.cs` | SourceSystem entity |
| `FreeGLBA.App.DataSubject.cs` | DataSubject entity |
| `FreeGLBA.App.ComplianceReport.cs` | ComplianceReport entity |
| `FreeGLBA.App.ApiRequestLog.cs` | ApiRequestLog entity |
| `FreeGLBA.App.BodyLoggingConfig.cs` | BodyLoggingConfig entity |

### Extra Projects (Beyond Standard FreeCRM Pattern)

#### FreeGLBA.NugetClient — Published Client Library

**Package ID:** `FreeGLBA.Client` (on nuget.org)  
**Version:** 1.1.0  
**Purpose:** Strongly-typed client for external systems to log GLBA access events

Key features:
- `GlbaClient` / `IGlbaClient` with DI support (`AddGlbaClient()`)
- `LogAccessAsync()` — single event logging
- `LogAccessBatchAsync()` — batch up to 1000 events
- `TryLogAccessAsync()` — fire-and-forget
- Internal endpoints: `GetStatsAsync()`, `GetRecentEventsAsync()`, `GetSubjectEventsAsync()`
- Bearer token support for internal (user JWT) endpoints
- Convenience methods: `LogViewAsync()`, `LogExportAsync()`, `LogBulkViewAsync()`

#### FreeGLBA.NugetClientPublisher — Package Publishing Tool

CLI menu-driven tool for NuGet publishing:
- Version validation (prevents publishing older versions)
- Dry run mode
- Build → Pack → Publish workflow
- Version management and unlisting
- API key via user secrets

#### FreeGLBA.TestClient — Dev Test Client

Console app using **project reference** to `FreeGLBA.NugetClient`.  
Used for development-time testing and debugging the client library.

#### FreeGLBA.TestClientWithNugetPackage — Published Package Test

Console app using **NuGet package reference** to `FreeGLBA.Client`.  
Simulates the external consumer experience — validates the published package works correctly.

---

## Extension Pattern Deep Dive

### The Three Layers

```
Layer 1: Framework files (NEVER modify)
    Program.cs, DataController.cs, DataAccess.cs, etc.
    These come from FreeCRM and are identical across all forks.

Layer 2: .App. hook files (modify MINIMALLY — single-line additions)
    Program.App.cs, DataController.App.cs, DataAccess.App.cs, etc.
    Shipped with empty methods. You add one-line calls to your code.

Layer 3: {ProjectName}.App.{Feature} files (YOUR code)
    FreeCICD.App.Program.cs, FreeGLBA.App.GlbaController.cs, etc.
    All your custom logic goes here. These files are never touched by framework updates.
```

### Concrete Example: FreeCICD Configuration Loading

**Layer 1** — `Program.cs` (framework, never modified):
```csharp
// Deep inside the framework's startup...
var loader = new ConfigurationHelperLoader();
loader = ConfigurationHelpersLoadApp(loader, builder);  // calls hook
```

**Layer 2** — `Program.App.cs` (hook file, one line added):
```csharp
public static ConfigurationHelperLoader ConfigurationHelpersLoadApp(
    ConfigurationHelperLoader loader, WebApplicationBuilder builder)
{
    var output = loader;
    output = MyConfigurationHelpersLoadApp(output, builder);  // ← the one line
    return output;
}
```

**Layer 3** — `FreeCICD.App.Program.cs` (custom code):
```csharp
public static ConfigurationHelperLoader MyConfigurationHelpersLoadApp(
    ConfigurationHelperLoader loader, WebApplicationBuilder builder)
{
    var output = loader;
    output.PAT = builder.Configuration.GetValue<string>("App:AzurePAT");
    output.ProjectId = builder.Configuration.GetValue<string>("App:AzureProjectId");
    output.RepoId = builder.Configuration.GetValue<string>("App:AzureRepoId");
    output.Branch = builder.Configuration.GetValue<string>("App:AzureBranch");
    output.OrgName = builder.Configuration.GetValue<string>("App:AzureOrgName");
    return output;
}
```

### Concrete Example: FreeGLBA Adding New API Endpoints

FreeCRM's `DataController` is a `partial class`. FreeGLBA extends it by:

1. **`DataController.App.cs`** — The hook file (could add endpoints here, but for clean separation...)
2. **`FreeGLBA.App.DataController.cs`** — Adds CRUD endpoints for SourceSystems, AccessEvents, DataSubjects, ComplianceReports
3. **`FreeGLBA.App.GlbaController.cs`** — Adds an entirely new controller for the external GLBA API

### File Naming Rules

| Pattern | Layer | Example |
|---------|-------|---------|
| `Feature.cs` | Framework | `Program.cs`, `DataController.cs` |
| `Feature.App.cs` | Hook | `Program.App.cs`, `DataController.App.cs` |
| `{Project}.App.{Feature}.cs` | Custom | `FreeCICD.App.API.cs`, `FreeGLBA.App.GlbaController.cs` |
| `{Feature}.App.razor` | Hook (UI) | `About.App.razor`, `Index.App.razor` |
| `{Project}.App.UI.{Feature}.razor` | Custom (UI) | `FreeCICD.App.UI.Dashboard.Pipelines.razor` |

### Update Workflow

When FreeCRM releases a framework update:

1. **Copy** all non-`.App.` framework files (they were never modified)
2. **Diff** only the `.App.` hook files where you added single-line calls
3. **Your** `{ProjectName}.App.{Feature}` files are completely untouched
4. Result: Clean updates with minimal merge effort

---

## Files Read During Research

### FreeTools Program.cs Files
- `FreeTools/FreeTools.AppHost/Program.cs` — Aspire orchestrator with CLI args, pipeline phases, backup management
- `FreeTools/FreeTools.EndpointMapper/Program.cs` — Razor file scanner with regex route extraction
- `FreeTools/FreeTools.EndpointPoker/Program.cs` — Parallel HTTP GET tester with ordered output
- `FreeTools/FreeTools.BrowserSnapshot/Program.cs` — Playwright screenshot capture with auth, retry, metadata
- `FreeTools/FreeTools.WorkspaceInventory/Program.cs` — Roslyn-powered file inventory with glob patterns
- `FreeTools/FreeTools.WorkspaceReporter/Program.cs` — Report generator aggregating all tool outputs
- `FreeTools/FreeTools.AccessibilityScanner/Program.cs` — Multi-site a11y scanner with axe-core + WAVE
- `FreeTools/FreeTools.ForkCRM/Program.cs` — LibGit2Sharp clone + external exe tools for fork/rename

### FreeTools csproj Files
- `FreeTools/FreeTools.Core/FreeTools.Core.csproj` — net10.0, library
- `FreeTools/FreeTools.ForkCRM/FreeTools.ForkCRM.csproj` — net10.0, exe, LibGit2Sharp

### FreeTools README Files
- `FreeTools/FreeTools.AppHost/README.md` — Comprehensive, v2.1
- `FreeTools/FreeTools.Core/README.md` — Accurate, covers all utility classes
- `FreeTools/FreeTools.EndpointMapper/README.md` — Accurate, v2.2
- `FreeTools/FreeTools.EndpointPoker/README.md` — Accurate, v2.2
- `FreeTools/FreeTools.BrowserSnapshot/README.md` — Accurate, v2.1
- `FreeTools/FreeTools.WorkspaceInventory/README.md` — Accurate, v2.0
- `FreeTools/FreeTools.WorkspaceReporter/README.md` — Accurate, v2.0
- `FreeTools/FreeTools.AccessibilityScanner/README.md` — Accurate, v1.0
- `FreeTools/FreeTools.ForkCRM/README.md` — Accurate, v1.0
- `FreeTools/Docs/README.md` — Accurate, v2.1

### Framework Extension Files (Program.App.cs)
- `ReferenceProjects/FreeCRM-main/CRM/Program.App.cs` — All hooks empty
- `ReferenceProjects/FreeCRM-FreeExamples_base/FreeExamples/Program.App.cs` — All hooks empty (matches FreeCRM)
- `FreeExamples/FreeExamples/Program.App.cs` — All hooks empty (no customizations yet)
- `ReferenceProjects/FreeCICD-main/FreeCICD/Program.App.cs` — Calls `MyConfigurationHelpersLoadApp()`
- `ReferenceProjects/FreeGLBA-main/FreeGLBA/Program.App.cs` — All hooks empty

### Framework Base Files
- `ReferenceProjects/FreeCRM-main/CRM/Program.cs` — Framework Program.cs with hook points
- `FreeExamples/FreeExamples/Program.cs` — Renamed copy, identical structure

### FreeCICD Custom Extension Files
- `FreeCICD/FreeCICD.App.Program.cs` — Azure DevOps config loader
- `FreeCICD/Controllers/FreeCICD.App.API.cs` — Pipeline API endpoints with SignalR
- `FreeCICD/Classes/FreeCICD.App.Config.cs` — Partial interface + class for config properties
- `FreeCICD/Services/FreeCICD.App.PipelineMonitorService.cs` — BackgroundService for live pipeline polling

### FreeGLBA Custom Extension Files
- `FreeGLBA/Controllers/FreeGLBA.App.GlbaController.cs` — External GLBA API with API key auth
- `FreeGLBA/Controllers/FreeGLBA.App.DataController.cs` — Internal CRUD endpoints
- `FreeGLBA/Controllers/FreeGLBA.App.ApiKeyMiddleware.cs` — Bearer token validation middleware

### FreeGLBA Extra Project Files
- `FreeGLBA.NugetClient/FreeGLBA.NugetClient.csproj` — NuGet package definition, v1.1.0
- `FreeGLBA.NugetClient/README.md` — Client library docs with DI examples
- `FreeGLBA.NugetClientPublisher/README.md` — Publishing tool docs
- `FreeGLBA.TestClient/FreeGLBA.TestClient.csproj` — Project reference test client
- `FreeGLBA.TestClient/README.md` — Test client docs
- `FreeGLBA.TestClientWithNugetPackage/README.md` — NuGet package test client docs

### Reference Project READMEs
- `ReferenceProjects/FreeCICD-main/FreeCICD/README.md` — Server structure and architecture
- `ReferenceProjects/FreeCICD-main/FreeCICD.Client/README.md` — Client structure and architecture
- `ReferenceProjects/FreeGLBA-main/FreeGLBA/README.md` — Server overview
- `ReferenceProjects/FreeGLBA-main/FreeGLBA.Client/README.md` — Client overview
- `ReferenceProjects/FreeGLBA-main/FreeGLBA.DataAccess/README.md` — Data layer overview
- `ReferenceProjects/FreeGLBA-main/FreeGLBA.DataObjects/README.md` — DTO overview

### Project File Listings (via get_files_in_project)
- `ReferenceProjects/FreeCICD-main/FreeCICD/FreeCICD.csproj` — 41 files
- `ReferenceProjects/FreeGLBA-main/FreeGLBA/FreeGLBA.csproj` — 42 files
- `FreeExamples/FreeExamples.DataAccess/FreeExamples.DataAccess.csproj` — 37 files
- `FreeExamples/FreeExamples.DataObjects/FreeExamples.DataObjects.csproj` — 18 files
- `FreeExamples/FreeExamples.EFModels/FreeExamples.EFModels.csproj` — 17 files
- `FreeExamples/FreeExamples.Plugins/FreeExamples.Plugins.csproj` — 5 files

### File Searches Conducted
- All `.App.` extension files across the solution (127 found)
- All `FreeCICD.App.*` files (36 found)
- All `FreeGLBA.App.*` files (38 found)
- All README.md files (32 found)
- All Program.cs files in FreeTools (8 found)

---

## Deep Dive Addendum (Phase 2)

### Documentation Audit — Does our docs describe the extension pattern?

Read all docs 000 through 008 in full and checked coverage of the `{ProjectName}.App.{Feature}` naming convention and the one-line tie-in pattern.

#### What Was Well-Documented

| Topic | Where | Grade |
|-------|-------|-------|
| File naming convention `{ProjectName}.App.{Feature}` | 000, 004, 005 | ✅ Excellent |
| Blazor class name conversion (dots → underscores) | 004 | ✅ Good |
| File categories (Base / Base Customization / Project Extension / Project-Specific NEW) | 004 | ✅ Excellent |
| Multi-level naming and coordinator pattern | 004 | ✅ Good |
| Quick reference table for which naming pattern to use | 004 | ✅ Good |
| Verification commands (`find . -name "..."`) | 004 | ✅ Good |

#### What Was Missing

| Topic | Gap | Action Taken |
|-------|-----|-------------|
| One-line tie-in pattern (how to wire hook files to custom code) | Not documented anywhere | Created `006_architecture.extension_hooks.md` |
| Hook file inventory (what ships with FreeCRM, what methods) | Not documented | Added to new doc |
| Framework update workflow | Not documented | Added to new doc |
| Razor hook file pattern (`.App.razor` single-line delegation) | Not documented | Added to new doc |
| Partial class extension (no hook needed) | Mentioned but not explained with real examples | Added with FreeCICD/FreeGLBA code |
| Exceptions to the pattern (middleware, new controllers) | Not documented | Added to new doc |
| Real-world change catalogs (what FreeCICD/FreeGLBA actually changed) | Not documented | Added line counts and file lists |

#### Key Discovery: .App.razor Hook Files

FreeCICD's `.App.razor` files (e.g., `Index.App.razor`) contain single-line component references:
```razor
<Index_App_FreeCICD />
```
This follows the same one-line tie-in pattern as C# files. The actual implementation lives in `FreeCICD.App.UI.*` or `FreeCICD.App.Pages.*` files.

#### Key Discovery: FreeGLBA Snippet Files

FreeGLBA ships `.snippet` files (e.g., `FreeGLBA.App.Program.snippet`) documenting where to add middleware in `Program.cs`. This is an exception to the pure hook pattern — middleware order constraints sometimes require direct `Program.cs` modification (or use of the `AppModifyStart()` hook).

### FreeCICD Extension Deep Dive — Complete File Change Catalog

**Hook files modified (3 one-line changes):**

1. `Program.App.cs` → `ConfigurationHelpersLoadApp()` gained: `output = MyConfigurationHelpersLoadApp(output, builder);`
2. `Index.App.razor` → gained: `<Index_App_FreeCICD />`
3. `Pipelines.App.razor` → gained: `<Pipelines_App_FreeCICD />`

**Custom files created (35+ files across 6 projects):**

Server project:
- `FreeCICD.App.Program.cs` — Loads 5 Azure DevOps config values from appsettings.json
- `FreeCICD.App.Config.cs` — Extends `IConfigurationHelper`, `ConfigurationHelper`, `ConfigurationHelperLoader` with PAT/ProjectId/RepoId/Branch/OrgName properties via partial class/interface
- `FreeCICD.App.API.cs` — 15+ API endpoints: pipeline dashboard, CRUD, DevOps proxy (projects/repos/branches/files/pipelines), YAML generation/preview, run pipeline, build timeline/logs, org health, import (validate/conflicts/start/status/upload)
- `FreeCICD.App.PipelineMonitorService.cs` — `BackgroundService` polling Azure DevOps every 5s, exponential backoff on errors, only polls when SignalR subscribers exist, broadcasts diffs via SignalR

DataAccess:
- `FreeCICD.App.DataAccess.cs` — Coordinator with 40+ IDataAccess interface methods + VssConnection factory
- `FreeCICD.App.DataAccess.DevOps.Resources.cs` — Projects, Repos, Branches, Variable Groups
- `FreeCICD.App.DataAccess.DevOps.GitFiles.cs` — Git file read/write operations
- `FreeCICD.App.DataAccess.DevOps.Pipelines.cs` — Pipeline CRUD + YAML generation
- `FreeCICD.App.DataAccess.DevOps.Dashboard.cs` — Dashboard with progressive SignalR loading
- `FreeCICD.App.DataAccess.Import.Validation.cs` — Import conflict checking
- `FreeCICD.App.DataAccess.Import.Operations.cs` — Import execution

DataObjects:
- `FreeCICD.App.DataObjects.cs` — 3 endpoint groups (DevOps, PipelineDashboard, Import), StepNameList, EnvSetting, IISInfo models, 20+ DTOs
- `FreeCICD.App.Settings.cs` — EnvironmentType enum (DEV/PROD/CMS), EnvironmentOptions, pipeline YAML template (~100 lines), global config

Client:
- 3 routable pages: `FreeCICD.App.Pages.Pipelines.razor`, `FreeCICD.App.Pages.Wizard.razor`, `FreeCICD.App.Pages.SignalRConnections.razor`
- 7 dashboard components: `FreeCICD.App.UI.Dashboard.{Pipelines,PipelineCard,PipelineGroup,FilterBar,TableView,ViewControls,VarGroupBadges}.razor`
- 12 wizard components: `FreeCICD.App.UI.Wizard.{Stepper,StepHeader,Summary,StepPAT,StepProject,StepRepository,StepBranch,StepCsproj,StepEnvironments,StepPipeline,StepPreview,StepCompleted,LoadingIndicator}.razor`
- Import and monitoring: `FreeCICD.App.UI.Import.razor`, `FreeCICD.App.UI.SignalRConnections.razor`

### FreeGLBA Extension Deep Dive — Complete File Change Catalog

**Hook files modified: NONE.** All extensions use partial classes or new controller classes.

**Custom files created (38+ files across 7 projects):**

Server project:
- `FreeGLBA.App.GlbaController.cs` — New standalone `[ApiController]` with `[Route("api/glba")]`. Dual auth: external POST endpoints use API key (via middleware), internal GET endpoints use `[Authorize]` JWT. Endpoints: PostEvent, PostBatch, GetStats, GetRecentEvents, GetSubjectEvents, GetEvent, GetSourceStatus, GetTopAccessors
- `FreeGLBA.App.DataController.cs` — Partial class extension with CRUD for 4 entities (SourceSystem, AccessEvent, DataSubject, ComplianceReport) + Accessor endpoints + API log dashboard/filter/detail endpoints
- `FreeGLBA.App.ApiKeyMiddleware.cs` — Request delegate middleware: only intercepts POST to `/api/glba/events` and `/api/glba/events/batch`, validates Bearer token, stores SourceSystem in HttpContext.Items
- `FreeGLBA.App.ApiRequestLoggingAttribute.cs` — ActionFilterAttribute capturing request/response details, timing, auth type, IP address, error info. Writes to database via IDataAccess
- `FreeGLBA.App.SkipApiLoggingAttribute.cs` — Marker attribute to prevent recursive logging on log-viewing endpoints

DataAccess:
- `FreeGLBA.App.DataAccess.cs` — CRUD operations for SourceSystem (with API key hashing, event count aggregation)
- `FreeGLBA.App.DataAccess.ExternalApi.cs` — Event processing (single + batch) with deduplication
- `FreeGLBA.App.DataAccess.ApiKey.cs` — SHA-256 key hashing, key validation
- `FreeGLBA.App.DataAccess.ApiLogging.cs` — API request log storage and queries
- `FreeGLBA.App.IDataAccess.cs` — 60+ interface methods covering all CRUD + external API + logging

DataObjects:
- `FreeGLBA.App.DataObjects.cs` — DTOs for SourceSystem, AccessEvent, DataSubject, ComplianceReport with filter/result/lookup classes
- `FreeGLBA.App.DataObjects.ExternalApi.cs` — GlbaEventRequest, GlbaEventResponse, GlbaBatchResponse, GlbaStats, AccessorSummary
- `FreeGLBA.App.DataObjects.ApiLogging.cs` — ApiRequestLog, ApiLogFilter, ApiLogDashboardStats, BodyLoggingConfig
- `FreeGLBA.App.Endpoints.cs` — String constants for all CRUD + GLBA + API logging endpoints

EFModels:
- `FreeGLBA.App.EFDataModel.cs` — DbContext DbSet extensions for 6 tables
- `FreeGLBA.App.AccessEvent.cs`, `FreeGLBA.App.SourceSystem.cs`, `FreeGLBA.App.DataSubject.cs`, `FreeGLBA.App.ComplianceReport.cs`, `FreeGLBA.App.ApiRequestLog.cs`, `FreeGLBA.App.BodyLoggingConfig.cs`

Client:
- 9 pages: GlbaDashboard, AccessEventsPage, AccessorsPage, SourceSystemsPage, DataSubjectsPage, ComplianceReportsPage, ApiLogDashboard, ApiRequestLogs, ViewApiRequestLog, BodyLoggingSettings
- 4 edit components: EditAccessEvent, EditSourceSystem, EditDataSubject, EditComplianceReport
- `FreeGLBA.App.Helpers.cs` — Client-side GLBA helpers

Extra projects:
- NugetClient: `GlbaClient`/`IGlbaClient` with DI support, 10+ methods, dual auth (API key + Bearer JWT)
- NugetClientPublisher: CLI menu tool for pack/publish/version management
- TestClient: Project reference, integration testing
- TestClientWithNugetPackage: NuGet package reference, consumer validation

---

*Created: 2025-07-25*
*Updated: 2025-07-25 (Deep Dive Addendum)*
*Category: Project Research*
*Author: Generated via comprehensive solution audit*
