# FreeTools.AppHost — Aspire Pipeline Orchestrator

> **Purpose:** Orchestrates the FreeTools pipeline using .NET Aspire to test web applications.  
> **Version:** 2.2  
> **Last Updated:** 2025-07-26

---

## Overview

**FreeTools.AppHost** is an Aspire orchestrator that:

1. **Starts** the target web application (FreeExamples by default)
2. **Runs** the tools pipeline in sequence
3. **Collects** all outputs to `Docs/runs/{Project}/{Branch}/latest/`
4. **Manages** backup retention (optional)

---

## Pipeline Phases

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         FreeTools Pipeline v2.2                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Phase 0: Start Web App                                                     │
│  ┌─────────────────────┐                                                    │
│  │  Target Web App     │ ◄─── FreeExamples or your project                   │
│  │  (https://7271)     │                                                    │
│  └─────────────────────┘                                                    │
│           │                                                                 │
│           ▼                                                                 │
│  Phase 1: Static Analysis (Parallel)                                        │
│  ├─► EndpointMapper ────────────────────────► pages.csv                     │
│  └─► WorkspaceInventory ────────────────────► workspace-inventory.csv       │
│           │                                                                 │
│           ▼                                                                 │
│  Phase 2: EndpointPoker ────────────────────► snapshots/*.html              │
│           │                                                                 │
│           ▼                                                                 │
│  Phase 3: BrowserSnapshot (v3.0) ───────────► snapshots/*.png               │
│           │                                   snapshots/*/metadata.json     │
│           ▼                                                                 │
│  Phase 4: WorkspaceReporter (v2.0) ─────────► {Project}-Report.md           │
│           │                                   (with Screenshot Health)      │
│           ▼                                                                 │
│  Outputs: Docs/runs/{Project}/{Branch}/latest/                              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Usage

### Run the Pipeline

```bash
cd FreeTools/FreeTools.AppHost
dotnet run
```

This will:
1. Start FreeExamples on https://localhost:7271 (fixed port)
2. Run all tools in sequence
3. Write outputs to `Docs/runs/FreeExamples/main/latest/`

### Run Against Your Project

```bash
dotnet run -- --target YourProjectName
```

### View Outputs

```bash
# Latest run
ls Docs/runs/FreeExamples/main/latest/

# Generated report
cat Docs/runs/FreeExamples/main/latest/FreeExamples-Report.md
```

---

## Command Line Options

```bash
dotnet run -- [options]

Options:
  --target <name>       Target project folder name (default: FreeExamples)
  --keep-backups <n>    Number of timestamped backups to keep (default: 0)
  --skip-cleanup        Skip cleanup of old run folders
```

---

## Configuration

### Change Target Project

Use the `--target` flag to analyze a different sibling project folder:

```bash
dotnet run -- --target YourProjectName
```

The target is resolved as a sibling folder next to `FreeTools/` in the repo root. Also add a project reference in `.csproj`:

```xml
<ProjectReference Include="..\..\YourProjectName\YourProjectName.csproj" />
```

And update the `AddProject<>` call in `Program.cs` to register the web app.

### Environment Variables

The AppHost sets these for each tool:

| Tool | Variables |
|------|-----------|
| EndpointMapper | args: `[targetRoot, pagesCsv]` |
| WorkspaceInventory | args: `[targetRoot, inventoryCsv]` |
| EndpointPoker | `BASE_URL`, `CSV_PATH`, `OUTPUT_DIR`, `MAX_THREADS`, `START_DELAY_MS` |
| BrowserSnapshot | `BASE_URL`, `CSV_PATH`, `OUTPUT_DIR`, `SCREENSHOT_BROWSER`, `MAX_THREADS`, `TENANT_CODE` |
| AccessibilityScanner | `BASE_URL`, `CSV_PATH`, `OUTPUT_DIR`, `TENANT_CODE`, `START_DELAY_MS`, `EXTRA_PAGES` |
| WorkspaceReporter | `REPO_ROOT`, `OUTPUT_PATH`, `WORKSPACE_CSV`, `PAGES_CSV`, `SNAPSHOTS_DIR`, `TARGET_PROJECT` |

---

## Output Structure

Each run creates:

```
Docs/runs/FreeExamples/main/latest/
├── pages.csv                    # All Blazor routes
├── workspace-inventory.csv      # File inventory with metrics
├── snapshots/
│   ├── root/
│   │   ├── default.html        # HTTP response
│   │   ├── default.png         # Screenshot (unauthenticated)
│   │   ├── logged-in.png       # Screenshot (authenticated)
│   │   └── metadata.json       # Capture metadata
│   ├── Account/
│   │   └── Login/
│   │       ├── default.html
│   │       ├── default.png
│   │       └── metadata.json
│   ├── a11y/                   # Accessibility scanner output
│   │   └── ...
│   └── ...
└── FreeExamples-Report.md       # Summary report
```

---

## Requirements

- **.NET 10 SDK**
- **.NET Aspire workload**: `dotnet workload install aspire`
- **Playwright browsers**: Run once after restore:
  ```bash
  pwsh tools/FreeTools.BrowserSnapshot/bin/Debug/net10.0/playwright.ps1 install
  ```

---

## Customization

### Adding a New Target Project

1. Add the project reference to `FreeTools.AppHost.csproj`
2. Run with `--target YourProjectName` (resolves as a sibling folder to `FreeTools/`)
3. Update the `AddProject<>` call in `Program.cs` for the web app

### Changing Tool Order

Modify the `WaitFor()` dependencies in `Program.cs`:

```csharp
var myTool = builder.AddProject<Projects.MyTool>("my-tool")
    .WaitFor(endpointMapper)  // Run after EndpointMapper
    .WaitFor(webApp);         // Wait for web app
```

### Parallel Execution

Tools with no dependencies run in parallel by default. Use `WaitFor()` to enforce ordering.

---

## Troubleshooting

### Common Issues

- **Port in Use**: If `https://localhost` fails to bind, specify a port in `launchSettings.json`:
  ```json
  "profiles": {
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "applicationUrl": "https://localhost:5001;http://localhost:5000"
    }
  }
  ```
- **Dependency Errors**: Ensure all dependencies are restored:
  ```bash
  dotnet restore
  ```

### Debugging Tips

- Use `dotnet run --verbosity diagnostic` for detailed logs.
- Check generated reports in `Docs/runs/{Project}/{Branch}/latest/` for insights.

---

## 📬 About

**FreeTools** is developed and maintained by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

📧 Questions or feedback? Visit our [team page](https://em.wsu.edu/eit/meet-our-staff/) or open an issue on [GitHub](https://github.com/WSU-EIT/FreeTools/issues)
