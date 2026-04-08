# FreeTools.ForkCRM — FreeCRM Project Fork Tool

> **Purpose:** Clones FreeCRM from GitHub, removes selected optional modules, renames the project to your chosen name, and writes a ready-to-build output directory.  
> **Version:** 1.0  
> **Last Updated:** 2025-07-26

---

## Overview

**ForkCRM** is a standalone CLI tool that automates the three-step process of starting a new FreeCRM-based project:

1. **Clone** — Pulls the latest `main` branch from `https://github.com/WSU-EIT/FreeCRM`
2. **Remove modules** — Strips out optional features you don't need (using `Remove Modules from FreeCRM.exe`)
3. **Rename** — Renames all namespaces, files, and references from `CRM` to your project name (using `Rename FreeCRM.exe`)

Everything runs in a temp directory — the final output is a clean, ready-to-build project at your chosen output path.

---

## Prerequisites

ForkCRM requires two external Windows executables from the **FreeCRM-utilities** folder:

| File | Purpose |
|------|---------|
| `Remove Modules from FreeCRM.exe` | Strips optional feature modules from the cloned repo |
| `Rename FreeCRM.exe` | Renames all namespaces and files to your project name |

The tool searches for these files in:
1. Same directory as the ForkCRM executable
2. Any `FreeCRM-utilities/` folder up to 5 levels up the directory tree

These are found in `FreeTools/FreeCRM-utilities/`.

---

## Usage

### Full Fork

```bash
cd FreeTools/FreeTools.ForkCRM
dotnet run -- --name MyProject --modules remove:all --output "C:\repos\MyProject"
```

### Keep a Specific Module

```bash
dotnet run -- --name MyProject --modules keep:Invoices --output "C:\repos\MyProject"
```

### Positional Arguments (shorthand)

```bash
dotnet run -- MyProject remove:all "C:\repos\MyProject"
```

---

## Arguments

| Argument | Flag | Description |
|----------|------|-------------|
| `NewName` | `-n`, `--name` | New project name — letters and numbers only, must start with a letter |
| `ModuleSelection` | `-m`, `--modules` | What to keep or remove (see below) |
| `OutputDirectory` | `-o`, `--output` | Where to write the finished project |
| *(optional)* | `-b`, `--branch` | Git branch to clone (default: `main`) |

---

## Module Selection

Format: `remove:<module>` or `keep:<module>`

| Selection | Result |
|-----------|--------|
| `remove:all` | Remove ALL optional modules — minimal clean project |
| `keep:Tags` | Keep only the Tags module |
| `keep:Appointments` | Keep only the Appointments module |
| `keep:Invoices` | Keep only the Invoices module |
| `keep:EmailTemplates` | Keep only the EmailTemplates module |
| `keep:Locations` | Keep only the Locations module |
| `keep:Payments` | Keep only the Payments module |
| `keep:Services` | Keep only the Services module |

---

## What Happens

```
1. Clone   https://github.com/WSU-EIT/FreeCRM → temp dir
2. Copy    Remove Modules exe + Rename exe → temp dir
3. Run     Remove Modules from FreeCRM.exe "<ModuleSelection>"
4. Run     Rename FreeCRM.exe "<NewName>"
5. Clean   Remove .git/, .github/, artifacts/, exe tools
6. Read    All transformed files into memory
7. Write   Files to OutputDirectory (preserves existing .git if present)
8. Delete  Temp directory
```

---

## Output

A ready-to-build FreeCRM project at your output path:

```
C:\repos\MyProject\
├── MyProject\              ← Server project (was CRM\)
├── MyProject.Client\       ← Blazor WASM client
├── MyProject.DataAccess\
├── MyProject.DataObjects\
├── MyProject.EFModels\
├── MyProject.Plugins\
└── MyProject.slnx
```

### Next Steps After Fork

```bash
cd C:\repos\MyProject
dotnet restore
dotnet build
```

---

## Technology Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 10.0 | Runtime |
| LibGit2Sharp | 0.30.0 | In-memory git clone (no `git` CLI required) |
| `Remove Modules from FreeCRM.exe` | external | Module stripping |
| `Rename FreeCRM.exe` | external | Project rename |
