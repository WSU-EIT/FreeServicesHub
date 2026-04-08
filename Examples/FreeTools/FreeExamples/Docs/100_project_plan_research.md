# FreeTools Solution: Project Research Plan

> Plan for researching, documenting, and updating READMEs across the full FreeTools solution suite.

---

## Objective

Audit every project in the solution, understand what it does, and produce:
1. Updated READMEs for each area (root, FreeTools, reference projects)
2. A comprehensive research summary (`101_project_research.md`)

---

## Solution Structure (discovered)

```
FreeTools.sln
│
├── FreeTools/                           # CLI analysis & testing tools
│   ├── FreeTools.AppHost/               # Aspire orchestrator — runs the full pipeline
│   ├── FreeTools.Core/                  # Shared library (CLI args, console output, route parsing)
│   ├── FreeTools.EndpointMapper/        # Scans Blazor @page directives → pages.csv
│   ├── FreeTools.EndpointPoker/         # HTTP GET each route → saves HTML responses
│   ├── FreeTools.BrowserSnapshot/       # Playwright screenshots of each route
│   ├── FreeTools.WorkspaceInventory/    # Codebase file metrics → workspace-inventory.csv
│   ├── FreeTools.WorkspaceReporter/     # Aggregates all outputs → markdown report
│   ├── FreeTools.AccessibilityScanner/  # WCAG/a11y scanning via Playwright + WAVE API
│   ├── FreeTools.ForkCRM/               # Clone FreeCRM, remove modules, rename → new project
│   └── Docs/                            # Output repository for pipeline runs
│
├── FreeExamples/                        # Active development project (FreeCRM-based)
│   ├── FreeExamples/                    # ASP.NET Core server (Blazor host, API, SignalR)
│   ├── FreeExamples.Client/             # Blazor WebAssembly UI
│   ├── FreeExamples.DataAccess/         # Business logic & data layer
│   ├── FreeExamples.DataObjects/        # DTOs, settings, API endpoint definitions
│   ├── FreeExamples.EFModels/           # Entity Framework models
│   ├── FreeExamples.Plugins/            # Plugin system
│   └── Docs/                            # Documentation (this file lives here)
│
└── ReferenceProjects/                   # Read-only reference implementations
    ├── FreeCRM-main/                    # The original FreeCRM framework (upstream source)
    │   └── CRM, CRM.Client, CRM.DataAccess, CRM.DataObjects, CRM.EFModels, CRM.Plugins
    │
    ├── FreeCRM-FreeExamples_base/       # Clean copy of FreeCRM renamed to FreeExamples namespace
    │   └── FreeExamples, FreeExamples.Client, FreeExamples.DataAccess, ...
    │
    ├── FreeCICD-main/                   # CI/CD dashboard built on FreeCRM
    │   └── FreeCICD, FreeCICD.Client, FreeCICD.DataAccess, FreeCICD.DataObjects, ...
    │
    └── FreeGLBA-main/                   # GLBA compliance tracking built on FreeCRM
        └── FreeGLBA, FreeGLBA.Client, FreeGLBA.DataAccess, FreeGLBA.DataObjects, ...
        └── FreeGLBA.NugetClient, FreeGLBA.NugetClientPublisher, FreeGLBA.TestClient, ...
```

---

## Research Steps

### Phase 1: FreeTools Suite

| # | Task | Status |
|---|------|--------|
| 1 | Read each FreeTools Program.cs and csproj | ✅ Done |
| 2 | Document what each tool does | ✅ Done |
| 3 | Verify existing tool READMEs are accurate | ✅ Done |
| 4 | Update tool READMEs where needed | Pending |

### Phase 2: FreeCRM Extension Pattern

| # | Task | Status |
|---|------|--------|
| 5 | Study FreeCRM-main Program.cs and Program.App.cs | ✅ Done |
| 6 | Study FreeCRM-FreeExamples_base (clean rename) | ✅ Done |
| 7 | Study FreeCICD extension pattern ({ProjectName}.App.{Feature}) | ✅ Done |
| 8 | Study FreeGLBA extension pattern | ✅ Done |
| 9 | Document the extension/customization philosophy | ✅ Done — `006_architecture.extension_hooks.md` |

### Phase 2b: Deep Dive (Added)

| # | Task | Status |
|---|------|--------|
| 9a | Deep dive FreeCICD — read all extension files in full | ✅ Done |
| 9b | Deep dive FreeGLBA — read all extension files in full | ✅ Done |
| 9c | Read all docs 000-008 in full | ✅ Done |
| 9d | Audit docs for hook/tie-in pattern coverage | ✅ Done — gap found and filled |
| 9e | Create 006_architecture.extension_hooks.md | ✅ Done |
| 9f | Update 101_project_research.md with deep dive findings | ✅ Done |

### Phase 3: README Updates

| # | Task | Status |
|---|------|--------|
| 10 | Update root README.md with full project suite | Pending |
| 11 | Verify FreeTools tool READMEs | Pending |
| 12 | Write 101_project_research.md summary | ✅ Done |

---

## Key Findings (Preview)

### FreeCRM Extension Pattern

The core philosophy: **never modify base framework files**. Instead:

1. **`{Feature}.App.cs`** — Shipped hook files (e.g., `Program.App.cs`, `DataController.App.cs`, `DataAccess.App.cs`)
   - These contain empty methods that are called from the framework at specific lifecycle points
   - Example: `Program.App.cs` has `AppModifyBuilderStart()`, `AppModifyBuilderEnd()`, `AppModifyStart()`, `AppModifyEnd()`

2. **`{ProjectName}.App.{Feature}.cs`** — Your custom extension files
   - Example: `FreeCICD.App.Program.cs` contains `MyConfigurationHelpersLoadApp()` which is called from `Program.App.cs`
   - Example: `FreeCICD.App.API.cs` adds Azure DevOps pipeline endpoints
   - Example: `FreeGLBA.App.GlbaController.cs` adds GLBA compliance API

3. **Update workflow**: When FreeCRM framework updates, you:
   - Copy over the `.App.` hook files
   - Only diff the few hook files where you added single-line calls to your `{ProjectName}.App.{Feature}` methods
   - Your custom code files are untouched

### FreeTools Pipeline

Tools are orchestrated by AppHost using .NET Aspire:
- Phase 0: Start target web app
- Phase 1: Static analysis (EndpointMapper + WorkspaceInventory in parallel)
- Phase 2: HTTP testing (EndpointPoker)
- Phase 3: Screenshots (BrowserSnapshot via Playwright)
- Phase 4: Report generation (WorkspaceReporter)

### FreeGLBA Extras

FreeGLBA adds beyond the standard FreeCRM pattern:
- **NuGet client library** (`FreeGLBA.NugetClient`) — Published to NuGet as `FreeGLBA.Client` for external integrations
- **NuGet publisher** (`FreeGLBA.NugetClientPublisher`) — Automates NuGet package publishing
- **Test clients** — `FreeGLBA.TestClient` (project reference) and `FreeGLBA.TestClientWithNugetPackage` (NuGet reference)
- **External API** — REST API with API key auth for receiving compliance events from source systems
- **API request logging middleware** — Logs all external API calls

---

*Created: 2025-07-25*
*Category: Project Research*
