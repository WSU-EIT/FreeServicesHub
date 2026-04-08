# FreeCRM Utilities

Windows utilities for customizing FreeCRM-based projects.

## Quick Start - Fork a New Project

Use the all-in-one fork script to create a new project from FreeCRM:

```cmd
# Command line (Windows)
fork-freecrm.cmd FreeManager "keep:Tags" C:\Projects\FreeManager
```

Or with PowerShell directly:

```powershell
.\Fork-FreeCRM.ps1 -NewName "FreeManager" -ModuleSelection "keep:Tags" -OutputDirectory "C:\Projects\FreeManager"
```

This will:
1. Clone the latest FreeCRM from GitHub
2. Remove unwanted modules
3. Rename the project to your specified name
4. Copy the result to your output directory

## Tools

| Tool | Purpose |
|------|---------|
| `Fork-FreeCRM.ps1` | **All-in-one script** - Clone, remove modules, rename, and output |
| `fork-freecrm.cmd` | Batch wrapper for Fork-FreeCRM.ps1 |
| `Rename FreeCRM.exe` | Rename a FreeCRM project to a new name |
| `Remove Modules from FreeCRM.exe` | Remove optional modules (Appointments, Invoices, etc.) |

## Fork-FreeCRM.ps1 Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| `-NewName` | Yes | New project name (letters/numbers, starts with letter) |
| `-ModuleSelection` | Yes | What modules to keep/remove (see below) |
| `-OutputDirectory` | Yes | Where to place the forked project |
| `-Branch` | No | Git branch to clone (default: main) |
| `-SkipClone` | No | Skip cloning, use current directory |

## Module Selections

| Selection | Description |
|-----------|-------------|
| `remove:all` | Remove ALL optional modules (minimal project) |
| `keep:Tags` | Keep only the Tags module |
| `keep:Appointments` | Keep only the Appointments module |
| `keep:Invoices` | Keep only the Invoices module |
| `keep:EmailTemplates` | Keep only the EmailTemplates module |
| `keep:Locations` | Keep only the Locations module |
| `keep:Payments` | Keep only the Payments module |
| `keep:Services` | Keep only the Services module |

## Examples

### Create a minimal project (no optional modules)
```powershell
.\Fork-FreeCRM.ps1 -NewName "MyMinimalApp" -ModuleSelection "remove:all" -OutputDirectory "C:\Projects\MyMinimalApp"
```

### Create a project with Tags support (like FreeManager)
```powershell
.\Fork-FreeCRM.ps1 -NewName "FreeManager" -ModuleSelection "keep:Tags" -OutputDirectory "C:\Projects\FreeManager"
```

### Create from a specific branch
```powershell
.\Fork-FreeCRM.ps1 -NewName "TestApp" -ModuleSelection "keep:Invoices" -OutputDirectory ".\TestApp" -Branch "develop"
```

## Using Individual Tools

If you need more control, use the individual executables:

```powershell
# First, clone the repo manually
git clone https://github.com/WSU-EIT/FreeCRM.git MyProject
cd MyProject

# Remove modules
& ".\Remove Modules from FreeCRM.exe" "keep:Tags"

# Rename project
& ".\Rename FreeCRM.exe" "MyProject"
```

## How It Works

The fork process:

1. **Clone** - Gets the latest FreeCRM source from GitHub
2. **Remove Modules** - Strips out unwanted optional features (Appointments, Invoices, etc.)
3. **Rename** - Updates all namespaces, project files, and references from "CRM"/"FreeCRM" to your new name
4. **Output** - Copies the clean result to your specified directory (without .git/.github folders)

## Requirements

- Windows (the .exe tools are Windows-only)
- Git (for cloning)
- PowerShell 5.1+ or PowerShell Core

## Related

- [FreeCRM Repository](https://github.com/WSU-EIT/FreeCRM)
- See PROJECT.md for more context on how these fit into the FreeManager workflow
