# WorkspaceInventory — Codebase Analysis Tool

> **Purpose:** Scans a workspace/repository and generates a CSV inventory of files with metrics, semantic classification, and optional Azure DevOps integration.  
> **Last Reviewed:** 2025-07-26  
> **Version:** 2.0

---

## Overview

The **WorkspaceInventory** tool is a development utility that:

- **Scans directories:** Recursively finds files matching glob patterns
- **Collects metrics:** File size, line count, character count, timestamps
- **Classifies files:** Identifies Razor pages vs components, C# sources, configs, etc.
- **Extracts metadata:** Namespaces, declared types, routes, auth requirements
- **Generates links:** Optional Azure DevOps deep links for each file
- **Outputs CSV:** Spreadsheet-compatible format for analysis

---

## Technology Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 10.0 | Runtime framework |
| Microsoft.Extensions.FileSystemGlobbing | 9.0.0 | Glob pattern matching |
| Microsoft.CodeAnalysis.CSharp | 4.12.0 | Roslyn C# syntax parsing |

---

## What's New in V2

| Feature | Description |
|---------|-------------|
| **File Classification** | Categorizes files by type (RazorPage, CSharpSource, etc.) |
| **Namespace Extraction** | Extracts namespace declarations from C# files |
| **Type Extraction** | Extracts class/record/struct/interface/enum names |
| **Route Extraction** | Extracts @page routes from Razor files |
| **Auth Detection** | Detects @attribute [Authorize] in Razor files |
| **Azure DevOps Links** | Generates deep links to files in ADO |
| **Max Size Guard** | Skips parsing for files > 1MB (configurable) |

---

## Usage

### Via Command Line

```bash
# Default (auto-detect root, write workspace-inventory.csv)
cd tools/WorkspaceInventory
dotnet run

# With explicit paths
dotnet run -- C:\repos\MyProject output.csv

# Skip content reading (faster, metadata only)
dotnet run -- . inventory.csv --noCounts

# Custom include patterns
dotnet run -- . report.csv --include="**/*.cs;**/*.razor"

# Custom exclude directories
dotnet run -- . report.csv --excludeDirs="bin;obj;test-output"
```

### With Azure DevOps Integration

```bash
# Set environment variables
$env:AZDO_ORG_URL = "https://dev.azure.com/MyOrg"
$env:AZDO_PROJECT = "MyProject"
$env:AZDO_REPO = "MyRepo"
$env:AZDO_BRANCH = "main"

dotnet run
```

---

## Environment Variables

### Core Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `ROOT_DIR` | Directory to scan | Auto-detect from BaseDirectory |
| `CSV_PATH` | Output CSV path | `<root>/workspace-inventory.csv` |
| `INCLUDE` | Semicolon-separated glob patterns | See defaults below |
| `EXCLUDE_DIRS` | Semicolon-separated directory names | See defaults below |
| `NO_COUNTS` | Set to "true" to skip content reading | `false` |
| `MAX_PARSE_SIZE` | Max file size to parse (bytes) | `1048576` (1MB) |

### Azure DevOps Configuration (Optional)

| Variable | Description | Default |
|----------|-------------|---------|
| `AZDO_ORG_URL` | Azure DevOps organization URL | (empty) |
| `AZDO_PROJECT` | Azure DevOps project name | (empty) |
| `AZDO_REPO` | Azure DevOps repository name | (empty) |
| `AZDO_BRANCH` | Branch for ADO links | `main` |

---

## Default Patterns

### Include Patterns

```
**/*.cs        C# source files
**/*.razor     Blazor components
**/*.csproj    Project files
**/*.sln       Solution files
**/*.json      JSON configuration
**/*.config    XML configuration
**/*.md        Markdown documentation
**/*.xml       XML files
**/*.yaml      YAML files
**/*.yml       YAML files
```

### Exclude Directories

```
bin            Build output
obj            Intermediate output
.git           Git repository data
.vs            Visual Studio cache
node_modules   NPM packages
packages       NuGet packages (old style)
TestResults    Test output
```

---

## Output Format

### CSV Columns

| Column | Description |
|--------|-------------|
| FilePath | Absolute path to the file |
| RelativePath | Path relative to scan root (forward slashes) |
| Extension | File extension (lowercase, e.g., `.cs`) |
| SizeBytes | File size in bytes |
| LineCount | Number of lines (empty if --noCounts) |
| CharCount | Number of characters (empty if --noCounts) |
| CreatedUtc | File creation timestamp (ISO 8601) |
| ModifiedUtc | File modification timestamp (ISO 8601) |
| ReadError | Error type if file couldn't be read |
| **Kind** | File classification (see below) |
| **Namespaces** | Semicolon-joined namespace declarations |
| **DeclaredTypes** | Semicolon-joined type names |
| **Routes** | Semicolon-joined @page routes |
| **RequiresAuth** | True if @attribute [Authorize] present |
| **AzureDevOpsUrl** | Deep link to file in ADO |

### Kind Values

| Kind | Extensions | Condition |
|------|------------|-----------|
| `RazorPage` | .razor | Contains `@page` directive |
| `RazorComponent` | .razor | No `@page` directive |
| `CSharpSource` | .cs | Any C# source file |
| `ProjectFile` | .csproj | MSBuild project |
| `SolutionFile` | .sln | Visual Studio solution |
| `Config` | .json, .config, .xml, .yaml, .yml | Configuration files |
| `Markdown` | .md | Documentation |
| `Other` | * | Everything else |

### Example Output

```csv
FilePath,RelativePath,Extension,SizeBytes,LineCount,CharCount,CreatedUtc,ModifiedUtc,ReadError,Kind,Namespaces,DeclaredTypes,Routes,RequiresAuth,AzureDevOpsUrl
"C:\repo\Program.cs","Program.cs",".cs",2048,75,1890,2025-12-14T22:00:00Z,2025-12-14T22:00:00Z,"","CSharpSource","MyApp","Program","","",""
"C:\repo\Home.razor","Pages/Home.razor",".razor",1024,45,890,2025-12-14T22:00:00Z,2025-12-14T22:00:00Z,"","RazorPage","","/;/Home","false",""
"C:\repo\Backdoor.razor","Pages/Backdoor.razor",".razor",5120,150,4200,2025-12-14T22:00:00Z,2025-12-14T22:00:00Z,"","RazorPage","","/Backdoor","true","https://dev.azure.com/..."
```

---

## Console Output

```
============================================================
 WorkspaceInventory (FreeTools) v2.0
============================================================
Root directory:    C:\Users\Administrator\source\repos\Public3
Output CSV:        C:\Users\Administrator\source\repos\Public3\workspace-inventory.csv
Include patterns:  **/*.cs;**/*.razor;**/*.csproj;**/*.sln;**/*.json;**/*.config;**/*.md
Exclude dirs:      bin;obj;.git;.vs;node_modules
Count lines/chars: true
Max parse size:    1.0 MB
Azure DevOps:      Enabled
============================================================

Scanning...
  Found 342 files matching patterns.
  Processing files...
  [1/342] src/Program.cs (2,048 bytes, 75 lines) [CSharpSource]
  [2/342] Pages/Home.razor (1,024 bytes, 45 lines) [RazorPage]
  ...

============================================================
                         SUMMARY
============================================================
  Files matched:     342
  Files scanned:     342
  Files unreadable:  0
  Files too large:   0
  Total size:        2.4 MB
  Total lines:       45,678
  Total chars:       1,234,567

  By Kind:
    CSharpSource         180
    RazorPage             45
    RazorComponent        32
    Config                50
    Markdown              25
    ProjectFile           10

  CSV written to: workspace-inventory.csv
============================================================
```

---

## Use Cases

### Build a Project Dashboard

Import the CSV into a Blazor page to show:
- File distribution by Kind (pie chart)
- Largest files (bar chart)
- Route map (table)
- Pages requiring auth (highlighted list)

### Code Review Prep

Generate clickable links to all changed files in Azure DevOps.

### Security Audit

List all pages: which require auth vs which are public.

### Namespace Analysis

Find files with inconsistent or missing namespace declarations.

### Track Codebase Growth

Run periodically and commit the CSV to track growth over time.

---

## Error Handling

The tool is designed to be robust:

- **Never crashes on a single file:** Errors are logged in ReadError column
- **Large files:** Skipped for content parsing (metadata still collected)
- **Permission issues:** Logged as `UnauthorizedAccessException`
- **Roslyn parse failures:** Gracefully handled, file still classified
- **Encoding issues:** Logged with exception type

---

## 📬 About

**FreeTools** is developed and maintained by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

📧 Questions or feedback? Visit our [team page](https://em.wsu.edu/eit/meet-our-staff/) or open an issue on [GitHub](https://github.com/WSU-EIT/FreeTools/issues)
