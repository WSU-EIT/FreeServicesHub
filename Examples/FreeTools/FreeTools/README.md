# FreeTools — Workspace Analysis & Testing Suite

A collection of .NET CLI tools for analyzing, testing, and maintaining FreeCRM-based projects.

## Tools

### Pipeline Tools (run via AppHost or standalone)

| Tool | Purpose |
|------|---------|
| **FreeTools.AppHost** | Aspire orchestrator — runs the full analysis pipeline against a target project |
| **FreeTools.Core** | Shared library — CLI args, console output, route parsing, path utilities |
| **FreeTools.EndpointMapper** | Scans Blazor projects for `@page` directives and generates a CSV of all routes |
| **FreeTools.EndpointPoker** | Performs HTTP GET requests against all routes and saves HTML responses |
| **FreeTools.BrowserSnapshot** | Captures full-page screenshots of each route using Playwright |
| **FreeTools.WorkspaceInventory** | Scans a codebase and generates a CSV with file metrics, types, routes, and auth |
| **FreeTools.WorkspaceReporter** | Aggregates all tool outputs into a markdown dashboard report |
| **FreeTools.AccessibilityScanner** | Scans sites for accessibility issues using Playwright + optional WAVE API |

### Standalone Tools

| Tool | Purpose |
|------|---------|
| **FreeTools.AppExtractor** | Extracts your `.App.*` customization layer from a FreeCRM fork for safe backup/migration |
| **FreeTools.ForkCRM** | Clones FreeCRM, removes modules, and renames the project via LibGit2Sharp |

### Output

| Tool | Purpose |
|------|---------|
| **FreeTools.Docs** | Content project — holds generated tool outputs under `runs/` and `latest/` |

---

## Quick Start

### Run the Full Pipeline

```bash
cd FreeTools.AppHost
dotnet run
```

This will:
1. Start the target web app (BlazorApp1 by default)
2. Run EndpointMapper + WorkspaceInventory in parallel
3. Run EndpointPoker, BrowserSnapshot, WorkspaceReporter in sequence
4. Write outputs to `Docs/runs/{Project}/{Branch}/latest/`

### View Results

```bash
# Latest run outputs
ls Docs/runs/BlazorApp1/main/latest/

# Generated report
cat Docs/runs/BlazorApp1/main/latest/BlazorApp1-Report.md
```

### Run Against Your Project

```bash
cd FreeTools.AppHost
dotnet run -- --target YourProjectName
```

---

## Pipeline Architecture (v2.1)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         FreeTools Pipeline v2.1                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Phase 0: Start Web App                                                     │
│  ┌─────────────────────┐                                                    │
│  │  Target Web App     │ ◄─── BlazorApp1 or --target YourProject            │
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
│  Phase 3: BrowserSnapshot ──────────────────► snapshots/*.png               │
│           │                                   snapshots/*/metadata.json     │
│           ▼                                                                 │
│  Phase 4: WorkspaceReporter ────────────────► {Project}-Report.md           │
│                                                                             │
│  Outputs: Docs/runs/{Project}/{Branch}/latest/                              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Shared Utilities (FreeTools.Core)

| File | Purpose |
|------|---------|
| `CliArgs.cs` | CLI argument parsing, environment variable helpers |
| `ConsoleOutput.cs` | Thread-safe console output, banner/divider formatting |
| `RouteParser.cs` | CSV route parsing, route parameter detection |
| `PathSanitizer.cs` | Route-to-path conversion, byte formatting |

---

## Running Individual Tools

```bash
# EndpointMapper
cd FreeTools.EndpointMapper
dotnet run -- <rootToScan> <csvOutputPath> [--clean]

# EndpointPoker
cd FreeTools.EndpointPoker
dotnet run -- <baseUrl> <csvPath> <outputDir> [maxThreads]

# BrowserSnapshot
cd FreeTools.BrowserSnapshot
dotnet run -- <baseUrl> <csvPath> <outputDir> [maxThreads]

# WorkspaceInventory
cd FreeTools.WorkspaceInventory
dotnet run -- <rootDir> <csvOutputPath> [--noCounts]

# WorkspaceReporter
cd FreeTools.WorkspaceReporter
dotnet run -- <repoRoot> <outputPath>

# AppExtractor
cd FreeTools.AppExtractor
dotnet run -- --source "C:\...\YourFreeCRMFork" --output "C:\...\extracted"
dotnet run -- --source "..." --output "..." --dry-run true

# ForkCRM
cd FreeTools.ForkCRM
dotnet run -- --name MyProject --modules all --output "C:\repos\MyProject"

# AccessibilityScanner
cd FreeTools.AccessibilityScanner
dotnet run   # configure sites in appsettings.json
```

---

## Environment Variables

### EndpointMapper

| Variable | Description |
|----------|-------------|
| `CLEAN_OUTPUT_DIRS` | Set to `true` to delete previous output before scanning |
| `OUTPUT_DIR` | Directory to clean (default: `page-snapshots`) |

### EndpointPoker / BrowserSnapshot

| Variable | Description |
|----------|-------------|
| `BASE_URL` | Base URL of the web application |
| `CSV_PATH` | Path to `pages.csv` |
| `OUTPUT_DIR` | Directory for output files |
| `MAX_THREADS` | Maximum parallel requests (default: `100`) |
| `PAGE_SETTLE_DELAY_MS` | Post-load wait before screenshot (BrowserSnapshot, default: `3000`) |

### WorkspaceInventory

| Variable | Description |
|----------|-------------|
| `ROOT_DIR` | Directory to scan |
| `CSV_PATH` | Output CSV path |
| `NO_COUNTS` | Set to `true` to skip line/char counting |

### WorkspaceReporter

| Variable | Description |
|----------|-------------|
| `REPO_ROOT` | Repository root directory |
| `OUTPUT_PATH` | Output markdown file path |

---

## Build

```bash
dotnet build FreeTools.slnx
```

---

## Changing Target Project

Pass via CLI or edit `FreeTools.AppHost/Program.cs`:

```bash
dotnet run -- --target YourProjectFolderName
```

---

## 📬 About

**FreeTools** is developed and maintained by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

We build internal tools and automation to support enrollment management processes across WSU.

📧 Questions or feedback? Visit our [team page](https://em.wsu.edu/eit/meet-our-staff/) or open an issue on [GitHub](https://github.com/WSU-EIT/FreeTools/issues)
