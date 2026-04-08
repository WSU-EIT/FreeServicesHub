# 101 — Reference: FreeTools Architecture & Moving Parts

> **Document ID:** 101  
> **Category:** Reference  
> **Purpose:** Complete visual reference for the FreeTools pipeline — what exists, execution order, data flow, configuration, and how to run things independently.  
> **Audience:** Devs, AI agents, new contributors  
> **Outcome:** 📖 Understand the whole machine at a glance.

---

## Table of Contents

| Section | Description |
|---------|-------------|
| [The Full Pipeline](#the-full-pipeline) | ASCII art of the complete Aspire orchestration |
| [Project Map](#project-map) | Every project in the solution and what it does |
| [Data Flow](#data-flow) | What files get created, by whom, and who reads them |
| [Phase-by-Phase Breakdown](#phase-by-phase-breakdown) | Deep dive into each phase |
| [FreeTools.Core Shared Library](#freetoolscore-shared-library) | The four utility classes everything depends on |
| [Configuration Reference](#configuration-reference) | Every env var, CLI arg, and default value |
| [Running Tools Independently](#running-tools-independently) | How to run any tool outside Aspire |
| [AppHost CLI Options](#apphost-cli-options) | The top-level command line interface |
| [Output Folder Structure](#output-folder-structure) | Where everything lands on disk |
| [Dependency Graph](#dependency-graph) | NuGet and project references |

---

## The Full Pipeline

```
  ┌─────────────────────────────────────────────────────────────────────┐
  │                     FreeTools.AppHost (Aspire)                      │
  │                    "The Orchestrator"                                │
  │                                                                     │
  │   CLI Args:  --target BlazorApp1                                    │
  │              --keep-backups 0                                        │
  │              --skip-cleanup false                                    │
  │                                                                     │
  │   Reads: .git/HEAD for branch name                                  │
  │   Creates: Docs/runs/{Project}/{Branch}/latest/                     │
  └─────────────────┬───────────────────────────────────────────────────┘
                    │
                    │  Launches all projects via Aspire
                    │  Controls ordering with WaitFor / WaitForCompletion
                    │
          ┌─────────▼──────────┐
          │                    │
          │   [0] WEB APP      │
          │   BlazorApp1       │
          │   (or --target)    │
          │                    │
          │   Runs on HTTPS    │
          │   Dev mode         │
          │                    │
          └────────┬───────────┘
                   │
                   │  Web app must be running before HTTP tools start
                   │
    ┌──────────────┼──────────────────────────────────────────┐
    │              │                                          │
    │   PHASE 1: STATIC ANALYSIS (parallel, no web app needed)
    │                                                          │
    │   ┌──────────────────────┐  ┌──────────────────────────┐ │
    │   │  EndpointMapper      │  │  WorkspaceInventory      │ │
    │   │                      │  │                          │ │
    │   │  Scans .razor files  │  │  Scans ALL files         │ │
    │   │  for @page directives│  │  Counts lines/chars/size │ │
    │   │  Detects [Authorize] │  │  Extracts C# namespaces  │ │
    │   │                      │  │  Classifies file kinds   │ │
    │   │  IN:  project root   │  │                          │ │
    │   │  OUT: pages.csv      │  │  IN:  project root       │ │
    │   │                      │  │  OUT: workspace-          │ │
    │   │  ~ 2 seconds         │  │       inventory.csv      │ │
    │   └──────────┬───────────┘  │  ~ 5 seconds             │ │
    │              │              └────────────┬──────────────┘ │
    │              │                           │                │
    └──────────────┼───────────────────────────┼────────────────┘
                   │                           │
                   │  pages.csv must exist      │
                   │  before HTTP tools start   │
                   │                           │
    ┌──────────────┼───────────────────────────┼────────────────┐
    │              │                           │                │
    │   PHASE 2 & 3: LIVE SITE TOOLS (need web app + routes)   │
    │                                                          │
    │   ┌──────────▼───────────┐  ┌──────────────────────────┐ │
    │   │  EndpointPoker       │  │  BrowserSnapshot         │ │
    │   │                      │  │                          │ │
    │   │  HTTP GET each route │  │  Playwright screenshots  │ │
    │   │  Saves HTML response │  │  Full-page PNG capture   │ │
    │   │  Checks status codes │  │  Smart SPA timing        │ │
    │   │  Verifies MIME types │  │  Auto-retry < 10KB       │ │
    │   │                      │  │  Console error capture   │ │
    │   │  IN:  pages.csv      │  │  Auth flow (login/pass)  │ │
    │   │       BASE_URL       │  │  metadata.json per route │ │
    │   │  OUT: snapshots/     │  │                          │ │
    │   │       *.html         │  │  IN:  pages.csv          │ │
    │   │                      │  │       BASE_URL           │ │
    │   │  Parallel: 10 threads│  │  OUT: snapshots/         │ │
    │   │  ~ 10-30 seconds     │  │       *.png              │ │
    │   └──────────┬───────────┘  │       metadata.json      │ │
    │              │              │                          │ │
    │              │              │  Parallel: 10 threads    │ │
    │              │              │  ~ 30-90 seconds         │ │
    │              │              └────────────┬─────────────┘ │
    │              │                           │               │
    └──────────────┼───────────────────────────┼───────────────┘
                   │                           │
                   │  All tools must complete   │
                   │  before reporter starts    │
                   │                           │
    ┌──────────────┼───────────────────────────┼───────────────┐
    │              │                           │               │
    │   PHASE 4: REPORT GENERATION                             │
    │                                                          │
    │   ┌──────────▼───────────────────────────▼─────────────┐ │
    │   │  WorkspaceReporter                                 │ │
    │   │                                                    │ │
    │   │  Reads ALL CSVs + snapshots + metadata             │ │
    │   │  Generates {Project}-Report.md                     │ │
    │   │                                                    │ │
    │   │  Sections:                                         │ │
    │   │    - Workspace overview (files, lines, size)       │ │
    │   │    - File statistics by category                   │ │
    │   │    - Code distribution bar charts                  │ │
    │   │    - Largest files with links                      │ │
    │   │    - Large file warnings (450+ lines)              │ │
    │   │    - Blazor route inventory                        │ │
    │   │    - Mermaid route diagram                         │ │
    │   │    - Screenshot health (success rates, errors)     │ │
    │   │    - Screenshot gallery                            │ │
    │   │                                                    │ │
    │   │  IN:  workspace-inventory.csv                      │ │
    │   │       pages.csv                                    │ │
    │   │       snapshots/**                                 │ │
    │   │  OUT: {Project}-Report.md                          │ │
    │   │                                                    │ │
    │   │  ~ 2-5 seconds                                    │ │
    │   └────────────────────────────────────────────────────┘ │
    │                                                          │
    └──────────────────────────────────────────────────────────┘
```

---

## Project Map

```
FreeTools/                              # Solution root
│
├── FreeTools.sln                       # Solution file (11 projects)
│
│   ┌─────────────────────────────────────────────────────────┐
│   │  ORCHESTRATOR                                           │
│   │                                                         │
├── ├── FreeTools.AppHost/              │  Aspire host         │
│   │   ├── Program.cs                  │  Pipeline config     │
│   │   ├── appsettings.json            │  App settings        │
│   │   └── .csproj                     │  Refs ALL projects   │
│   │                                                         │
│   └─────────────────────────────────────────────────────────┘
│
│   ┌─────────────────────────────────────────────────────────┐
│   │  SHARED LIBRARY (zero NuGet dependencies)               │
│   │                                                         │
├── ├── FreeTools.Core/                 │  4 static classes    │
│   │   ├── CliArgs.cs                  │  Arg parsing         │
│   │   ├── ConsoleOutput.cs            │  Thread-safe I/O     │
│   │   ├── PathSanitizer.cs            │  Route→path utils    │
│   │   └── RouteParser.cs              │  CSV route parsing   │
│   │                                                         │
│   └─────────────────────────────────────────────────────────┘
│
│   ┌─────────────────────────────────────────────────────────┐
│   │  ANALYSIS TOOLS (each is a standalone console app)      │
│   │                                                         │
├── ├── FreeTools.EndpointMapper/       │  Route scanner       │
├── ├── FreeTools.WorkspaceInventory/   │  File metrics        │
├── ├── FreeTools.EndpointPoker/        │  HTTP tester         │
├── ├── FreeTools.BrowserSnapshot/      │  Screenshot capture  │
├── ├── FreeTools.WorkspaceReporter/    │  Report generator    │
├── ├── FreeTools.AccessibilityScanner/ │  ADA/WCAG scanner    │
│   │                                   │  (NEW — in progress) │
│   └─────────────────────────────────────────────────────────┘
│
│   ┌─────────────────────────────────────────────────────────┐
│   │  OTHER                                                  │
│   │                                                         │
├── ├── FreeTools.ForkCRM/              │  Git fork utility    │
│   │                                   │  (LibGit2Sharp)      │
├── ├── Docs/                           │  Docs + output       │
│   │   ├── runs/                       │  Generated reports   │
│   │   ├── Guides/                     │  Dev guides (000-008)│
│   │   └── *.md                        │  Project docs        │
│   │                                                         │
│   └─────────────────────────────────────────────────────────┘
│
└── BlazorApp1/                         # SAMPLE TARGET (sibling folder)
    └── (standard Blazor project)       # Replace with your project
```

---

## Data Flow

```
                    ┌──────────────┐
                    │  .razor      │
                    │  files in    │
                    │  target      │
                    │  project     │
                    └──────┬───────┘
                           │
              ┌────────────▼────────────┐
              │    EndpointMapper       │
              │    scans @page + auth   │
              └────────────┬────────────┘
                           │
                           ▼
              ┌────────────────────────┐
              │      pages.csv         │ ◄─── THE CENTRAL FILE
              │                        │      Everything reads this
              │  FilePath,Route,       │
              │  RequiresAuth,Project  │
              └───┬──────┬─────────┬───┘
                  │      │         │
         ┌────────┘      │         └────────┐
         │               │                  │
         ▼               ▼                  ▼
  ┌──────────────┐ ┌───────────────┐ ┌──────────────────┐
  │ EndpointPoker│ │BrowserSnapshot│ │AccessibilityScanner│
  │              │ │               │ │    (PLANNED)       │
  │ HTTP GET     │ │ Playwright    │ │                    │
  │ each route   │ │ screenshots   │ │ axe + Pa11y +      │
  │              │ │ per route     │ │ Lighthouse per     │
  │ Writes:      │ │               │ │ route              │
  │  *.html      │ │ Writes:       │ │                    │
  │              │ │  *.png        │ │ Writes:            │
  │              │ │  metadata.json│ │  accessibility-    │
  └──────┬───────┘ └───────┬───────┘ │  issues.csv        │
         │                 │         │  accessibility-    │
         │                 │         │  summary.json      │
         │                 │         └────────┬───────────┘
         │                 │                  │
         └────────┐        │        ┌─────────┘
                  │        │        │
                  ▼        ▼        ▼
         ┌────────────────────────────────┐
         │       WorkspaceReporter        │
         │                                │
         │  Also reads:                   │
         │   workspace-inventory.csv      │  ◄── from WorkspaceInventory
         │   (runs in parallel with       │
         │    EndpointMapper)             │
         │                                │
         │  Writes:                       │
         │   {Project}-Report.md          │
         └────────────────────────────────┘


  ALL SOURCE FILES                 ALL PROJECT FILES
       │                                │
       ▼                                │
  ┌────────────────────┐                │
  │ WorkspaceInventory │                │
  │                    │                │
  │ Scans every file   │                │
  │ matching patterns  │                │
  │                    │                │
  │ Writes:            │                │
  │  workspace-        │                │
  │  inventory.csv     │────────────────┘
  │  workspace-        │    (both feed into reporter)
  │  inventory-        │
  │  csharp.csv        │
  │  workspace-        │
  │  inventory-        │
  │  razor.csv         │
  └────────────────────┘
```

---

## Phase-by-Phase Breakdown

### Phase 0: Web App Launch

```
  ┌─────────────────────────────────────────────────────┐
  │  BlazorApp1 (or --target)                           │
  │                                                     │
  │  Environment: ASPNETCORE_ENVIRONMENT=Development    │
  │                                                     │
  │  Starts HTTPS server                                │
  │  Aspire assigns dynamic port                        │
  │  Other tools get URL via webApp.GetEndpoint("https")│
  │                                                     │
  │  Must stay running for entire pipeline              │
  └─────────────────────────────────────────────────────┘
```

**Can run independently?** Yes — it's a normal Blazor project.
```bash
dotnet run --project BlazorApp1
```

---

### Phase 1a: EndpointMapper

```
  ┌─────────────────────────────────────────────────────────┐
  │  EndpointMapper                                         │
  │                                                         │
  │  WHAT:  Scans .razor files for @page directives         │
  │         Detects [Authorize] attributes                  │
  │         Determines which project each file belongs to   │
  │                                                         │
  │  HOW:   Regex on file contents (not Roslyn)             │
  │         @page\s+"([^"]+)"                               │
  │         @attribute\s+\[Authorize                        │
  │                                                         │
  │  EXCLUDES: bin/, obj/, repo/ directories                │
  │                                                         │
  │  INPUT ──────────────────────────────────────────────    │
  │  │  Arg[0] or ROOT_DIR:  project root path              │
  │  │  Arg[1]:              output CSV path                │
  │  │  --clean / CLEAN_OUTPUT_DIRS: delete output dir      │
  │  │  START_DELAY_MS:      startup delay (ms)             │
  │                                                         │
  │  OUTPUT ─────────────────────────────────────────────    │
  │  │  pages.csv                                           │
  │  │  Columns: FilePath,Route,RequiresAuth,Project        │
  │  │  Example:                                            │
  │  │  "Components/Pages/Home.razor","/",false,"BlazorApp1"│
  │                                                         │
  │  EXIT CODES: 0 = success, 1 = root not found            │
  └─────────────────────────────────────────────────────────┘
```

**Can run independently?** Yes.
```bash
dotnet run --project FreeTools.EndpointMapper -- "C:\path\to\BlazorApp1" "output\pages.csv"
```

---

### Phase 1b: WorkspaceInventory

```
  ┌─────────────────────────────────────────────────────────┐
  │  WorkspaceInventory                                     │
  │                                                         │
  │  WHAT:  Scans all files matching glob patterns          │
  │         Counts lines, characters, file size             │
  │         Extracts C# namespaces and types via Roslyn     │
  │         Classifies files: RazorPage, RazorComponent,    │
  │           CSharpSource, Config, Markdown, ProjectFile,  │
  │           SolutionFile, Other                           │
  │                                                         │
  │  HOW:   Microsoft.Extensions.FileSystemGlobbing         │
  │         Microsoft.CodeAnalysis.CSharp (Roslyn)          │
  │         Parallel processing with SemaphoreSlim          │
  │                                                         │
  │  INPUT ──────────────────────────────────────────────    │
  │  │  Arg[0] or ROOT_DIR:       project root path         │
  │  │  Arg[1] or CSV_PATH:       output CSV path           │
  │  │  --noCounts / NO_COUNTS:   skip line/char counting   │
  │  │  --include=<patterns>:     glob patterns (;-sep)     │
  │  │  --excludeDirs=<dirs>:     dirs to skip (;-sep)      │
  │  │  MAX_THREADS:              parallel workers (def 10) │
  │  │  MAX_PARSE_SIZE:           skip files > N bytes      │
  │  │  START_DELAY_MS:           startup delay (ms)        │
  │  │  AZDO_ORG_URL:             Azure DevOps org URL      │
  │  │  AZDO_PROJECT:             Azure DevOps project      │
  │  │  AZDO_REPO:                Azure DevOps repo name    │
  │  │  AZDO_BRANCH:              Azure DevOps branch       │
  │                                                         │
  │  DEFAULTS ───────────────────────────────────────────    │
  │  │  Include: **/*.cs;**/*.razor;**/*.csproj;**/*.sln;   │
  │  │           **/*.json;**/*.config;**/*.md;**/*.xml;    │
  │  │           **/*.yaml;**/*.yml                         │
  │  │  Exclude: bin;obj;.git;.vs;node_modules;packages;   │
  │  │           TestResults;repo                           │
  │  │  MaxParseSize: 1 MB                                  │
  │  │  MaxThreads: 10                                      │
  │                                                         │
  │  OUTPUT ─────────────────────────────────────────────    │
  │  │  workspace-inventory.csv       (all files)           │
  │  │  workspace-inventory-csharp.csv (C# only)            │
  │  │  workspace-inventory-razor.csv  (Razor only)         │
  │  │  Columns: FilePath,RelativePath,Extension,SizeBytes, │
  │  │    LineCount,CharCount,Kind,Namespace,TypeDecl,...    │
  │                                                         │
  │  EXIT CODES: 0 = success, 1 = root/dir error           │
  └─────────────────────────────────────────────────────────┘
```

**Can run independently?** Yes.
```bash
dotnet run --project FreeTools.WorkspaceInventory -- "C:\path\to\project" "output\inventory.csv"

# With options
dotnet run --project FreeTools.WorkspaceInventory -- "C:\path\to\project" "output\inventory.csv" --noCounts --include="**/*.cs;**/*.razor"
```

---

### Phase 2: EndpointPoker

```
  ┌─────────────────────────────────────────────────────────┐
  │  EndpointPoker                                          │
  │                                                         │
  │  WHAT:  Sends HTTP GET to every route from pages.csv    │
  │         Saves full HTML response to disk                │
  │         Tracks status codes (2xx, 4xx, 5xx)             │
  │         Verifies Blazor framework MIME types             │
  │                                                         │
  │  HOW:   HttpClient with parallel SemaphoreSlim          │
  │         SSL cert validation disabled (localhost dev)     │
  │         30s timeout per request                          │
  │         Ordered output (results written in route order)  │
  │                                                         │
  │  WAITS FOR: Web App + EndpointMapper                    │
  │                                                         │
  │  INPUT ──────────────────────────────────────────────    │
  │  │  Arg[0] or BASE_URL:    target URL (def localhost)   │
  │  │  Arg[1] or CSV_PATH:    path to pages.csv            │
  │  │  Arg[2] or OUTPUT_DIR:  where to save HTML           │
  │  │  Arg[3] or MAX_THREADS: parallel workers (def 10)    │
  │  │  START_DELAY_MS:        startup delay (def 5000)     │
  │                                                         │
  │  DEFAULTS ───────────────────────────────────────────    │
  │  │  BASE_URL:    https://localhost:5001                  │
  │  │  CSV_PATH:    pages.csv                               │
  │  │  OUTPUT_DIR:  page-snapshots                          │
  │  │  MAX_THREADS: 10                                      │
  │                                                         │
  │  OUTPUT ─────────────────────────────────────────────    │
  │  │  snapshots/{route}/default.html   per route          │
  │  │  Example: snapshots/Account/Login/default.html       │
  │                                                         │
  │  EXIT CODES: 0 = success                                │
  │              1 = connection errors (server unreachable)  │
  └─────────────────────────────────────────────────────────┘
```

**Can run independently?** Yes, but needs a running web app and a `pages.csv`.
```bash
# Start your web app first, then:
dotnet run --project FreeTools.EndpointPoker -- "https://localhost:5001" "path/to/pages.csv" "output/snapshots"
```

---

### Phase 3: BrowserSnapshot

```
  ┌─────────────────────────────────────────────────────────┐
  │  BrowserSnapshot                                        │
  │                                                         │
  │  WHAT:  Full-page PNG screenshots via Playwright        │
  │         Smart SPA timing (NetworkIdle + settle delay)    │
  │         Auto-retry if screenshot < 10KB (probably blank) │
  │         Captures JavaScript console errors               │
  │         Writes metadata.json per route                   │
  │         3-step auth flow for [Authorize] pages:          │
  │           1-initial.png → 2-filled.png → 3-result.png   │
  │                                                         │
  │  HOW:   Microsoft.Playwright (headless Chromium)         │
  │         New browser context per route                    │
  │         Parallel with SemaphoreSlim                      │
  │         Ordered console output                           │
  │                                                         │
  │  WAITS FOR: Web App + EndpointMapper                    │
  │                                                         │
  │  INPUT ──────────────────────────────────────────────    │
  │  │  Arg[0] or BASE_URL:           target URL            │
  │  │  Arg[1] or CSV_PATH:           path to pages.csv     │
  │  │  Arg[2] or OUTPUT_DIR:         snapshot output dir   │
  │  │  Arg[3] or MAX_THREADS:        parallel (def 10)     │
  │  │  SCREENSHOT_BROWSER:           chromium|firefox|      │
  │  │                                webkit (def chromium) │
  │  │  SCREENSHOT_VIEWPORT:          WxH (e.g. 1920x1080) │
  │  │  PAGE_SETTLE_DELAY_MS:         wait after load       │
  │  │                                (def 3000)            │
  │  │  LOGIN_USERNAME:               auth user (def admin) │
  │  │  LOGIN_PASSWORD:               auth pass (def admin) │
  │  │  START_DELAY_MS:               startup delay         │
  │                                                         │
  │  DEFAULTS ───────────────────────────────────────────    │
  │  │  BASE_URL:            https://localhost:5001          │
  │  │  CSV_PATH:            pages.csv                       │
  │  │  OUTPUT_DIR:          page-snapshots                  │
  │  │  SCREENSHOT_BROWSER:  chromium                        │
  │  │  PAGE_SETTLE_DELAY_MS: 3000                           │
  │  │  LOGIN_USERNAME:      admin                           │
  │  │  LOGIN_PASSWORD:      admin                           │
  │  │  SuspiciousThreshold: 10KB                            │
  │  │  RetryExtraDelay:     3000ms                          │
  │                                                         │
  │  OUTPUT ─────────────────────────────────────────────    │
  │  │  Per PUBLIC route:                                    │
  │  │    snapshots/{route}/default.png                      │
  │  │    snapshots/{route}/metadata.json                    │
  │  │                                                       │
  │  │  Per AUTH route:                                      │
  │  │    snapshots/{route}/1-initial.png                    │
  │  │    snapshots/{route}/2-filled.png                     │
  │  │    snapshots/{route}/3-result.png                     │
  │  │    snapshots/{route}/metadata.json                    │
  │                                                         │
  │  EXIT CODES: 0 = success, 1 = fatal Playwright error    │
  └─────────────────────────────────────────────────────────┘
```

**Can run independently?** Yes, but needs a running web app, a `pages.csv`, and Playwright browsers installed.
```bash
# Install Playwright browsers first (one-time)
pwsh bin/Debug/net10.0/playwright.ps1 install

# Then run
dotnet run --project FreeTools.BrowserSnapshot -- "https://localhost:5001" "path/to/pages.csv" "output/snapshots"
```

---

### Phase 4: WorkspaceReporter

```
  ┌─────────────────────────────────────────────────────────┐
  │  WorkspaceReporter                                      │
  │                                                         │
  │  WHAT:  Reads ALL output from prior tools               │
  │         Generates a comprehensive Markdown report       │
  │         GitHub-flavored Markdown with Mermaid diagrams   │
  │         Expandable <details> sections                    │
  │         Relative links to source files                   │
  │                                                         │
  │  HOW:   StringBuilder → single .md file                 │
  │         Reads CSVs with File.ReadAllLinesAsync           │
  │         Reads metadata.json for screenshot health        │
  │         Pure string manipulation (no NuGet deps)         │
  │                                                         │
  │  WAITS FOR: ALL other tools (last in pipeline)          │
  │                                                         │
  │  INPUT ──────────────────────────────────────────────    │
  │  │  Arg[0] or REPO_ROOT:          project root path     │
  │  │  Arg[1] or OUTPUT_PATH:        report output path    │
  │  │  WORKSPACE_CSV:                 inventory CSV         │
  │  │  WORKSPACE_CSHARP_CSV:         C# inventory CSV      │
  │  │  WORKSPACE_RAZOR_CSV:          Razor inventory CSV    │
  │  │  PAGES_CSV:                     routes CSV            │
  │  │  SNAPSHOTS_DIR:                 screenshots dir       │
  │  │  TARGET_PROJECT:                project name          │
  │  │  WEB_PROJECT_ROOT:             web project path       │
  │  │  START_DELAY_MS:                startup delay         │
  │                                                         │
  │  OUTPUT ─────────────────────────────────────────────    │
  │  │  {Project}-Report.md                                 │
  │  │  (~500-2000 lines of Markdown depending on project)  │
  │                                                         │
  │  EXIT CODES: 0 = success, 1 = repo root not found       │
  └─────────────────────────────────────────────────────────┘
```

**Can run independently?** Yes, if you already have the CSV files and snapshots from prior runs.
```bash
dotnet run --project FreeTools.WorkspaceReporter -- "C:\path\to\project" "output\Report.md"
```

---

## FreeTools.Core Shared Library

```
  ┌──────────────────────────────────────────────────────────────┐
  │  FreeTools.Core — Zero NuGet dependencies                    │
  │  Referenced by: ALL tools (except AppHost and ForkCRM)       │
  │                                                              │
  │  ┌──────────────────────────────────────────────────────┐    │
  │  │  CliArgs (static)                                    │    │
  │  │                                                      │    │
  │  │  Priority: Env Var → CLI Arg → Default               │    │
  │  │                                                      │    │
  │  │  GetEnvOrArg(envVar, args, index, default)           │    │
  │  │  GetEnvOrArgInt(envVar, args, index, default)        │    │
  │  │  GetEnvBool(envVar)                                  │    │
  │  │  HasFlag(args, "--clean", "-c")     ← mutates list   │    │
  │  │  GetOption(args, "--output=")       ← mutates list   │    │
  │  │  GetPositional(args, 0, default)                     │    │
  │  │  GetRequired(args, 0, "name")      ← throws         │    │
  │  └──────────────────────────────────────────────────────┘    │
  │                                                              │
  │  ┌──────────────────────────────────────────────────────┐    │
  │  │  ConsoleOutput (static, thread-safe via lock)        │    │
  │  │                                                      │    │
  │  │  PrintBanner("ToolName", "2.0")                      │    │
  │  │  ============================================        │    │
  │  │   ToolName v2.0                                      │    │
  │  │  ============================================        │    │
  │  │                                                      │    │
  │  │  PrintConfig("Label", "value")                       │    │
  │  │    Label:    value                                   │    │
  │  │                                                      │    │
  │  │  PrintDivider("Title")                               │    │
  │  │  ----------------------------------------            │    │
  │  │                                                      │    │
  │  │  WriteLine(msg, isError: true)  → stderr             │    │
  │  └──────────────────────────────────────────────────────┘    │
  │                                                              │
  │  ┌──────────────────────────────────────────────────────┐    │
  │  │  PathSanitizer (static)                              │    │
  │  │                                                      │    │
  │  │  RouteToDirectoryPath("/Account/Login")              │    │
  │  │    → "Account\Login" (Win) or "Account/Login" (Unix) │    │
  │  │    → "root" if route is "/"                          │    │
  │  │                                                      │    │
  │  │  GetOutputFilePath(outDir, route, "default.png")     │    │
  │  │    → "outDir/Account/Login/default.png"              │    │
  │  │                                                      │    │
  │  │  EnsureDirectoryExists(filePath)                     │    │
  │  │                                                      │    │
  │  │  FormatBytes(1536) → "1.5 KB"                        │    │
  │  └──────────────────────────────────────────────────────┘    │
  │                                                              │
  │  ┌──────────────────────────────────────────────────────┐    │
  │  │  RouteParser (static)                                │    │
  │  │                                                      │    │
  │  │  HasParameter("/user/{id}") → true                   │    │
  │  │  HasParameter("/about")     → false                  │    │
  │  │                                                      │    │
  │  │  ParseRoutesFromCsvFileAsync("pages.csv")            │    │
  │  │    → (routes: ["/", "/about"], skipped: ["/u/{id}"]) │    │
  │  │    Skips header row                                  │    │
  │  │    Skips parameterized routes by default              │    │
  │  │                                                      │    │
  │  │  BuildUrl("https://localhost:5001", "/login")        │    │
  │  │    → "https://localhost:5001/login"                   │    │
  │  └──────────────────────────────────────────────────────┘    │
  │                                                              │
  └──────────────────────────────────────────────────────────────┘
```

---

## Configuration Reference

### AppHost CLI Options (Top-Level)

```
dotnet run --project FreeTools.AppHost -- [OPTIONS]

  --target <name>        Target project folder name
                         Default: "BlazorApp1"
                         The project must be a sibling folder to FreeTools/

  --keep-backups <n>     Number of timestamped backups to keep
                         Default: 0 (no backups, only latest/)
                         When > 0, moves latest/ to {timestamp}/ before run

  --skip-cleanup         Don't delete the previous latest/ folder
                         Default: false
                         When true, old data may appear in new report
```

### Environment Variables — Full Matrix

```
  ┌──────────────────────────┬──────────┬──────────┬──────────┬──────────┬──────────┐
  │  Variable                │ Mapper   │ Invent.  │ Poker    │ Browser  │ Reporter │
  ├──────────────────────────┼──────────┼──────────┼──────────┼──────────┼──────────┤
  │  START_DELAY_MS          │   ✅     │   ✅     │   ✅     │   ✅     │   ✅     │
  │  BASE_URL                │          │          │   ✅     │   ✅     │          │
  │  CSV_PATH                │          │   ✅     │   ✅     │   ✅     │          │
  │  OUTPUT_DIR              │          │          │   ✅     │   ✅     │          │
  │  MAX_THREADS             │          │   ✅     │   ✅     │   ✅     │          │
  │  ROOT_DIR                │   ✅ *   │   ✅     │          │          │          │
  │  REPO_ROOT               │          │          │          │          │   ✅     │
  │  OUTPUT_PATH             │          │          │          │          │   ✅     │
  │  WORKSPACE_CSV           │          │          │          │          │   ✅     │
  │  PAGES_CSV               │          │          │          │          │   ✅     │
  │  SNAPSHOTS_DIR           │          │          │          │          │   ✅     │
  │  TARGET_PROJECT          │          │          │          │          │   ✅     │
  │  CLEAN_OUTPUT_DIRS       │   ✅     │          │          │          │          │
  │  INCLUDE                 │          │   ✅     │          │          │          │
  │  EXCLUDE_DIRS            │          │   ✅     │          │          │          │
  │  NO_COUNTS               │          │   ✅     │          │          │          │
  │  MAX_PARSE_SIZE          │          │   ✅     │          │          │          │
  │  SCREENSHOT_BROWSER      │          │          │          │   ✅     │          │
  │  SCREENSHOT_VIEWPORT     │          │          │          │   ✅     │          │
  │  PAGE_SETTLE_DELAY_MS    │          │          │          │   ✅     │          │
  │  LOGIN_USERNAME          │          │          │          │   ✅     │          │
  │  LOGIN_PASSWORD          │          │          │          │   ✅     │          │
  │  AZDO_ORG_URL            │          │   ✅     │          │          │          │
  │  AZDO_PROJECT            │          │   ✅     │          │          │          │
  │  AZDO_REPO               │          │   ✅     │          │          │          │
  │  AZDO_BRANCH             │          │   ✅     │          │          │          │
  └──────────────────────────┴──────────┴──────────┴──────────┴──────────┴──────────┘

  * EndpointMapper uses positional args, not ROOT_DIR env var

  Priority for all tools: ENV VAR → CLI ARG → HARDCODED DEFAULT
```

### What AppHost Actually Sets (via Aspire)

```
  AppHost wires these env vars when running through Aspire.
  These are NOT user-configurable — Aspire sets them automatically:

  ┌─────────────────────────────────────────────────────────────────────────┐
  │  EndpointMapper                                                        │
  │    Args[0] = projectConfig.ProjectRoot   (positional)                  │
  │    Args[1] = projectConfig.PagesCsv      (positional)                  │
  │    START_DELAY_MS = 2000                                               │
  ├─────────────────────────────────────────────────────────────────────────┤
  │  WorkspaceInventory                                                    │
  │    Args[0] = projectConfig.ProjectRoot   (positional)                  │
  │    Args[1] = projectConfig.InventoryCsv  (positional)                  │
  │    MAX_THREADS = 10                                                    │
  │    START_DELAY_MS = 2000                                               │
  ├─────────────────────────────────────────────────────────────────────────┤
  │  EndpointPoker                                                         │
  │    BASE_URL = webApp.GetEndpoint("https")   ← dynamic Aspire URL      │
  │    CSV_PATH = projectConfig.PagesCsv                                   │
  │    OUTPUT_DIR = projectConfig.SnapshotsDir                             │
  │    MAX_THREADS = 10                                                    │
  │    START_DELAY_MS = 8000  (5000 web + 3000 http delay)                 │
  ├─────────────────────────────────────────────────────────────────────────┤
  │  BrowserSnapshot                                                       │
  │    BASE_URL = webApp.GetEndpoint("https")   ← dynamic Aspire URL      │
  │    CSV_PATH = projectConfig.PagesCsv                                   │
  │    OUTPUT_DIR = projectConfig.SnapshotsDir                             │
  │    SCREENSHOT_BROWSER = chromium                                       │
  │    MAX_THREADS = 10                                                    │
  │    START_DELAY_MS = 8000  (5000 web + 3000 http delay)                 │
  ├─────────────────────────────────────────────────────────────────────────┤
  │  WorkspaceReporter                                                     │
  │    REPO_ROOT = projectConfig.ProjectRoot                               │
  │    OUTPUT_PATH = projectConfig.ReportPath                              │
  │    WORKSPACE_CSV = projectConfig.InventoryCsv                          │
  │    PAGES_CSV = projectConfig.PagesCsv                                  │
  │    SNAPSHOTS_DIR = projectConfig.SnapshotsDir                          │
  │    TARGET_PROJECT = target name (e.g. "BlazorApp1")                    │
  │    START_DELAY_MS = 2000                                               │
  └─────────────────────────────────────────────────────────────────────────┘
```

### ProjectConfig Record (AppHost internal)

```
  record ProjectConfig(Name, ProjectRoot, Branch, ToolsRoot)

  Derived paths:
  ┌──────────────────────────────────────────────────────────────┐
  │  ProjectRunsDir = Docs/runs/{Name}/{Branch}                  │
  │  LatestDir      = Docs/runs/{Name}/{Branch}/latest           │
  │  PagesCsv       = .../latest/pages.csv                       │
  │  InventoryCsv   = .../latest/workspace-inventory.csv         │
  │  SnapshotsDir   = .../latest/snapshots                       │
  │  ReportPath     = .../latest/{Name}-Report.md                │
  └──────────────────────────────────────────────────────────────┘
```

---

## Running Tools Independently

```
  ┌─────────────────────────────────────────────────────────────────────┐
  │                                                                     │
  │  FULL PIPELINE (normal usage)                                       │
  │  ─────────────────────────────                                      │
  │  dotnet run --project FreeTools.AppHost                             │
  │  dotnet run --project FreeTools.AppHost -- --target MyBlazorApp     │
  │  dotnet run --project FreeTools.AppHost -- --keep-backups 5         │
  │                                                                     │
  │  INDIVIDUAL TOOLS (for debugging or partial runs)                   │
  │  ────────────────────────────────────────────                       │
  │                                                                     │
  │  # 1. Route discovery (no web app needed)                           │
  │  dotnet run --project FreeTools.EndpointMapper -- \                 │
  │    "C:\repos\BlazorApp1" "output\pages.csv"                         │
  │                                                                     │
  │  # 2. File inventory (no web app needed)                            │
  │  dotnet run --project FreeTools.WorkspaceInventory -- \             │
  │    "C:\repos\BlazorApp1" "output\inventory.csv"                     │
  │                                                                     │
  │  # 3. HTTP testing (needs running web app + pages.csv)              │
  │  dotnet run --project FreeTools.EndpointPoker -- \                  │
  │    "https://localhost:5001" "output\pages.csv" "output\snapshots"   │
  │                                                                     │
  │  # 4. Screenshots (needs running web app + pages.csv + Playwright)  │
  │  dotnet run --project FreeTools.BrowserSnapshot -- \                │
  │    "https://localhost:5001" "output\pages.csv" "output\snapshots"   │
  │                                                                     │
  │  # 5. Report (needs all CSVs + snapshots from prior steps)          │
  │  dotnet run --project FreeTools.WorkspaceReporter -- \              │
  │    "C:\repos\BlazorApp1" "output\Report.md"                         │
  │                                                                     │
  │  INDEPENDENCE MATRIX                                                │
  │  ──────────────────                                                 │
  │                                                                     │
  │  Tool              │ Web App │ pages.csv │ Other CSVs │ Snapshots   │
  │  ──────────────────┼─────────┼───────────┼────────────┼─────────    │
  │  EndpointMapper    │   No    │  Creates  │     No     │    No       │
  │  WorkspaceInventory│   No    │    No     │  Creates   │    No       │
  │  EndpointPoker     │   YES   │   Reads   │     No     │  Creates    │
  │  BrowserSnapshot   │   YES   │   Reads   │     No     │  Creates    │
  │  WorkspaceReporter │   No    │   Reads   │   Reads    │  Reads      │
  │  AccessibilityScnr │   YES   │   Reads   │     No     │  Creates    │
  │  ──────────────────┼─────────┼───────────┼────────────┼─────────    │
  │                                                                     │
  └─────────────────────────────────────────────────────────────────────┘
```

---

## Output Folder Structure

```
  Docs/
  └── runs/
      └── BlazorApp1/                   ◄── --target name
          └── main/                      ◄── git branch
              ├── latest/                ◄── current run (always exists)
              │   ├── pages.csv                         ◄── EndpointMapper
              │   ├── workspace-inventory.csv           ◄── WorkspaceInventory
              │   ├── workspace-inventory-csharp.csv    ◄── WorkspaceInventory
              │   ├── workspace-inventory-razor.csv     ◄── WorkspaceInventory
              │   ├── BlazorApp1-Report.md              ◄── WorkspaceReporter
              │   ├── accessibility-issues.csv          ◄── AccessibilityScanner (PLANNED)
              │   ├── accessibility-summary.json        ◄── AccessibilityScanner (PLANNED)
              │   └── snapshots/
              │       ├── root/                          ◄── route "/"
              │       │   ├── default.html               ◄── EndpointPoker
              │       │   ├── default.png                ◄── BrowserSnapshot
              │       │   └── metadata.json              ◄── BrowserSnapshot
              │       ├── Account/
              │       │   └── Login/
              │       │       ├── default.html
              │       │       ├── 1-initial.png          ◄── auth flow step 1
              │       │       ├── 2-filled.png           ◄── auth flow step 2
              │       │       ├── 3-result.png           ◄── auth flow step 3
              │       │       └── metadata.json
              │       └── weather/
              │           ├── default.html
              │           ├── default.png
              │           └── metadata.json
              │
              ├── 2025-07-24_103000/     ◄── backup (if --keep-backups > 0)
              └── 2025-07-23_150000/     ◄── older backup
```

---

## Dependency Graph

### NuGet Packages

```
  ┌─────────────────────────────────────────────────────────────────┐
  │  FreeTools.Core           │  (none — zero dependencies)        │
  ├───────────────────────────┼─────────────────────────────────────┤
  │  EndpointMapper           │  FreeTools.Core                     │
  ├───────────────────────────┼─────────────────────────────────────┤
  │  EndpointPoker            │  FreeTools.Core                     │
  ├───────────────────────────┼─────────────────────────────────────┤
  │  WorkspaceInventory       │  FreeTools.Core                     │
  │                           │  Microsoft.CodeAnalysis.CSharp      │
  │                           │  Microsoft.Extensions.              │
  │                           │    FileSystemGlobbing               │
  ├───────────────────────────┼─────────────────────────────────────┤
  │  BrowserSnapshot          │  FreeTools.Core                     │
  │                           │  Microsoft.Playwright               │
  ├───────────────────────────┼─────────────────────────────────────┤
  │  WorkspaceReporter        │  FreeTools.Core                     │
  ├───────────────────────────┼─────────────────────────────────────┤
  │  AppHost                  │  Aspire.AppHost.Sdk                 │
  │                           │  Aspire.Hosting.AppHost             │
  │                           │  System.CommandLine                 │
  │                           │  ProjectRef → ALL tools + target    │
  ├───────────────────────────┼─────────────────────────────────────┤
  │  AccessibilityScanner     │  (bare — needs updating)            │
  │  (PLANNED)                │  Will need:                         │
  │                           │    FreeTools.Core                   │
  │                           │    Microsoft.Playwright             │
  │                           │    Deque.AxeCore.Playwright (TBD)   │
  ├───────────────────────────┼─────────────────────────────────────┤
  │  ForkCRM                  │  LibGit2Sharp                       │
  │                           │  (standalone — not in pipeline)     │
  └───────────────────────────┴─────────────────────────────────────┘
```

### Project References (who references whom)

```
                          ┌──────────────┐
                          │   AppHost    │
                          │  (Aspire)    │
                          └──────┬───────┘
                                 │
                  ┌──────────────┼──────────────────────┐
                  │  References all tools + target app   │
                  │                                      │
     ┌────────────┼──────┬──────┬───────┬───────┐       │
     │            │      │      │       │       │       │
     ▼            ▼      ▼      ▼       ▼       ▼       ▼
  Mapper    Inventory  Poker  Browser  Reporter  ┌──────────┐
     │            │      │      │       │        │BlazorApp1│
     │            │      │      │       │        │ (target) │
     └────────┬───┘──────┘──────┘───────┘        └──────────┘
              │
              ▼
        ┌──────────┐
        │   Core   │    ← shared by all tools (not AppHost)
        │ (no deps)│
        └──────────┘


  Standalone (not in pipeline):
  ┌──────────┐   ┌────────┐
  │ ForkCRM  │   │  Docs  │  ← holds output, no code
  │(LibGit2) │   │(.csproj│
  └──────────┘   │ shell) │
                 └────────┘
```

---

## Timing & Startup Delays

```
  t=0ms        AppHost starts, launches web app
               │
  t=0-5000ms   Web app starting up (HTTPS, Blazor, etc.)
               │
               │  WebAppStartupDelayMs = 5000
               │  ToolStartupDelayMs   = 2000
               │  HttpToolDelayMs      = 3000
               │
  t=2000ms     ┌─ EndpointMapper starts (START_DELAY_MS=2000)
               │  └─ Scans files, writes pages.csv (~2s)
               │
  t=2000ms     ┌─ WorkspaceInventory starts (START_DELAY_MS=2000)
               │  └─ Scans files, writes CSVs (~5s)
               │
  t=8000ms     ┌─ EndpointPoker starts (START_DELAY_MS=8000)
               │  └─ WaitFor(webApp) + WaitForCompletion(mapper)
               │  └─ HTTP GETs each route (~10-30s)
               │
  t=8000ms     ┌─ BrowserSnapshot starts (START_DELAY_MS=8000)
               │  └─ WaitFor(webApp) + WaitForCompletion(mapper)
               │  └─ Screenshots each route (~30-90s)
               │
  t=varies     ┌─ WorkspaceReporter starts
               │  └─ WaitForCompletion(ALL prior tools)
               │  └─ Reads CSVs + snapshots, writes Report.md (~2-5s)
               │
  t=~60-120s   Pipeline complete
```

---

## Common Patterns Across All Tools

```
  Every tool follows this skeleton:

  ┌─────────────────────────────────────────────────────┐
  │  1. Optional startup delay                          │
  │     var delayEnv = Environment.GetEnvironmentVariable│
  │       ("START_DELAY_MS");                            │
  │     if (int.TryParse(delayEnv, out var delayMs)     │
  │         && delayMs > 0)                              │
  │       await Task.Delay(delayMs);                    │
  │                                                     │
  │  2. Read config (env vars → CLI args → defaults)    │
  │     var baseUrl = CliArgs.GetEnvOrArg("BASE_URL",   │
  │       args, 0, "https://localhost:5001");            │
  │                                                     │
  │  3. Print banner + config                           │
  │     ConsoleOutput.PrintBanner("ToolName", "2.0");   │
  │     ConsoleOutput.PrintConfig("Base URL", baseUrl); │
  │     ConsoleOutput.PrintDivider();                   │
  │                                                     │
  │  4. Validate inputs (file exists? dir exists?)      │
  │     if (!File.Exists(csvPath)) return 1;            │
  │                                                     │
  │  5. Do work (often parallel with SemaphoreSlim)     │
  │     var semaphore = new SemaphoreSlim(maxThreads);  │
  │     var tasks = items.Select(async item => { ... });│
  │     await Task.WhenAll(tasks);                      │
  │                                                     │
  │  6. Write output (CSV, JSON, HTML, PNG, MD)         │
  │     await File.WriteAllLinesAsync(outFile, lines);  │
  │                                                     │
  │  7. Print summary + return exit code                │
  │     return 0;  // success                           │
  │     return 1;  // failure                           │
  └─────────────────────────────────────────────────────┘
```

---

*Created: 2025-07-24*  
*Maintained by: [Quality]*
