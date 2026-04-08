# WorkspaceReporter — Dashboard & Report Generator

> **Purpose:** Generates a comprehensive markdown report aggregating outputs from all tools, including screenshot galleries, code statistics, route analysis, and screenshot health monitoring.  
> **Last Reviewed:** 2025-07-26  
> **Version:** 2.0

---

## Overview

The **WorkspaceReporter** tool is a development utility that:

- **Aggregates data:** Reads outputs from WorkspaceInventory, EndpointMapper, EndpointPoker, and BrowserSnapshot
- **Generates statistics:** File counts, line counts, code distribution charts
- **Creates galleries:** Screenshot thumbnails with clickable full-size images
- **Analyzes routes:** Auth requirements, route distribution by area
- **Screenshot health:** NEW — Surfaces blank/failed screenshots with success rate metrics
- **Console errors:** NEW — Reports JavaScript errors captured during screenshots
- **Outputs markdown:** GitHub/Azure DevOps compatible report with collapsible sections

---

## What's New in v2.0

| Feature | Description |
|---------|-------------|
| **Screenshot Health section** | Shows success rate, suspicious captures, HTTP errors |
| **Console error reporting** | Surfaces JavaScript errors from BrowserSnapshot metadata |
| **Metadata parsing** | Reads `metadata.json` files from BrowserSnapshot v2.1+ |
| **Success rate metric** | Calculates overall capture success percentage |

---

## ⚠️ Scope Limitations

This tool focuses on **Blazor pages only**:

| Included | Not Included |
|----------|--------------|
| ✅ Blazor pages (`@page` directives) | ❌ API endpoints (`/api/*`) |
| ✅ Razor components | ❌ Dynamic routes (MapGet/MapPost) |
| ✅ C# source files | ❌ Routes with parameters (`{id}`) — skipped |
| ✅ Configuration files | |

Routes with parameters (e.g., `/Account/Manage/{Id}`) are detected and listed separately but not tested or screenshotted.

---

## Technology Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 10.0 | Runtime framework |
| System.Text.Json | Built-in | Metadata parsing |

---

## Usage

### Via Aspire AppHost (Recommended)

The tool runs automatically as the final phase after all other tools complete:

```bash
cd FreeTools/FreeTools.AppHost
dotnet run
```

### Standalone

```bash
cd FreeTools/FreeTools.WorkspaceReporter
dotnet run [repoRoot] [outputPath]
```

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `REPO_ROOT` | Repository root directory | Auto-detect |
| `OUTPUT_PATH` | Output markdown file path | `<repo>/LatestReport.md` |
| `WORKSPACE_CSV` | Path to workspace-inventory.csv | `<repo>/workspace-inventory.csv` |
| `WORKSPACE_CSHARP_CSV` | Path to C# files CSV | `<repo>/workspace-inventory-csharp.csv` |
| `WORKSPACE_RAZOR_CSV` | Path to Razor files CSV | `<repo>/workspace-inventory-razor.csv` |
| `PAGES_CSV` | Path to pages.csv | `<web>/pages.csv` |
| `SNAPSHOTS_DIR` | Path to page-snapshots directory | `<web>/page-snapshots` |
| `START_DELAY_MS` | Startup delay in milliseconds | `0` |

---

## Output Format

The generated report includes:

### 📁 Workspace Overview
- Total files, lines, characters, size

### 📈 File Statistics
- File counts by category (CSharpSource, RazorPage, Config, etc.)
- Percentage distribution with progress bars

### 📊 Code Distribution
- ASCII bar charts showing lines of code by category
- Extension distribution

### 📏 Largest Files
- Top 15 C# files by line count
- Top 15 Razor files by line count

### ⚠️ Large File Warnings
- LLM-friendly file size guide
- Files exceeding 450/600/900 line thresholds

### 🛤️ Page Routes
- Route summary (total, public, protected)
- Access distribution chart
- Collapsible route listings by area
- Mermaid route hierarchy diagram

### 📊 Screenshot Health (NEW in v2.0)
- Success/suspicious/failed counts
- HTTP error listing
- JavaScript error details
- Overall success rate percentage

### 📸 Screenshot Gallery
- Organized by page area
- Clickable thumbnails (250px width)
- Full-size image links

---

## Screenshot Health Section

The new Screenshot Health section reads `metadata.json` files from BrowserSnapshot and reports:

```markdown
## 📊 Screenshot Health

| Status | Count | Description |
|--------|------:|-------------|
| ✅ Success | 34 | Screenshots > 10KB |
| ⚠️ Suspicious | 0 | Screenshots < 10KB (possible blank) |
| 🔄 Retried | 0 | Required retry attempt |
| ❌ HTTP Error | 2 | 4xx/5xx responses |
| 💥 Failed | 0 | Browser/timeout errors |
| 🔴 JS Errors | 36 | Pages with console errors |

**Overall Success Rate:** 94% (34/36 pages captured cleanly)
```

Expandable sections show details for:
- ⚠️ Suspicious screenshots (route, size, retry status)
- ❌ HTTP errors (route, status code)
- 🔴 JavaScript errors (route, error messages)

---

## Integration

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           FreeTools Pipeline                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Phase 1-3: Data Generation                                                 │
│  ┌─────────────────┐  ┌──────────────┐  ┌────────────┐  ┌────────────────┐  │
│  │WorkspaceInventory│ │EndpointMapper│  │EndpointPoker│ │BrowserSnapshot │  │
│  │                 │  │              │  │            │  │                │  │
│  │ workspace-*.csv │  │  pages.csv   │  │ *.html     │  │ *.png +        │  │
│  │                 │  │              │  │            │  │ metadata.json  │  │
│  └────────┬────────┘  └──────┬───────┘  └─────┬──────┘  └───────┬────────┘  │
│           │                  │                │                 │           │
│           └──────────────────┴────────────────┴─────────────────┘           │
│                                       │                                     │
│  Phase 4: Report Generation           ▼                                     │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                        WorkspaceReporter                              │  │
│  │                                                                       │  │
│  │  Reads all outputs + metadata.json → Generates Report.md              │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Dependencies

Requires BrowserSnapshot v2.1+ for full Screenshot Health functionality. Without `metadata.json` files, the section shows a message to upgrade BrowserSnapshot.

---

## 📬 About

**FreeTools** is developed and maintained by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

📧 Questions or feedback? Visit our [team page](https://em.wsu.edu/eit/meet-our-staff/) or open an issue on [GitHub](https://github.com/WSU-EIT/FreeTools/issues).
