# 006 Architecture Index

> Category index for FreeCRM architecture and design documentation.

**Source:** FreeCRM base template (public). Examples from private repos nForm, Helpdesk4, DependencyManager, TrusselBuilder.

---

## FreeCRM Ecosystem Context

This documentation describes FreeCRM patterns. FreeCRM-main is the authoritative public base template. FreeManager and FreeCICD are public example extensions demonstrating how to build on FreeCRM.

---

## Quick Navigation

| Document | Description | When to Use |
|----------|-------------|-------------|
| [006_architecture.freecrm_overview.md](006_architecture.freecrm_overview.md) | .NET vs FreeCRM custom patterns | Understanding what's custom vs standard |
| [006_architecture.extension_hooks.md](006_architecture.extension_hooks.md) | Extension hook pattern & one-line tie-in | **Extending FreeCRM without modifying framework files** |
| [006_architecture.unique_features.md](006_architecture.unique_features.md) | Project-specific features analysis | Finding reusable patterns |

### Individual Hook File Guides (29 files in 10 docs)

| Document | Hook File(s) | Project |
|----------|-------------|---------|
| [program.app](006_architecture.program.app.md) | `Program.App.cs` - 6 startup methods | Server |
| [datacontroller.app](006_architecture.datacontroller.app.md) | `DataController.App.cs` - auth, API, SignalR | Server |
| [dataaccess.app](006_architecture.dataaccess.app.md) | `DataAccess.App.cs` - 16 methods + 3 companions | DataAccess |
| [dataobjects.app](006_architecture.dataobjects.app.md) | `DataObjects.App.cs` + `GlobalSettings.App.cs` | DataObjects |
| [configurationhelper.app](006_architecture.configurationhelper.app.md) | `ConfigurationHelper.App.cs` - 4 partials | DataObjects |
| [datamodel.app](006_architecture.datamodel.app.md) | `DataModel.App.cs` - client state | Client |
| [helpers.app](006_architecture.helpers.app.md) | `Helpers.App.cs` - 12 methods | Client |
| [mainlayout.app](006_architecture.mainlayout.app.md) | `MainLayout.App.razor` - layout override | Client |
| [modules.app](006_architecture.modules.app.md) | `Modules.App.razor` + `site.App.css` | Server+Client |
| [appcomponents.app](006_architecture.appcomponents.app.md) | 14 `*.App.razor` - forms, pages, settings | Client |

---

## Overview

Architecture guides explain the high-level design decisions in FreeCRM, clarify what's custom vs standard .NET, and document the overall structure of the framework.

> **API Convention:** We use a three-endpoint CRUD pattern — **GetMany**, **SaveMany**, **DeleteMany** — per entity.
> See [007_patterns.crud_api.md](../007_patterns.crud_api.md) for the full pattern.

---

## Available Guides

### 006_architecture.freecrm_overview.md - .NET vs FreeCRM

**Purpose:** Clarifies the boundary between standard .NET/Blazor and FreeCRM custom patterns.

**Key Topics:**
- DataObjects/DataAccess namespace patterns
- Navigation, authentication, HTTP helpers
- BlazorDataModel state management
- Multi-tenant vs single-tenant architecture
- Common gotchas for .NET developers

**Use when:** New to FreeCRM and wondering "is this .NET or custom?", or when deciding whether to use a helper vs standard .NET.

---

### 006_architecture.extension_hooks.md - Extension Hook Pattern ⭐

**Purpose:** How to extend FreeCRM without modifying framework files — the `.App.` hook system, one-line tie-in pattern, and framework update workflow.

**Key Topics:**
- Three-layer architecture (Framework → Hook → Custom)
- Complete hook file inventory with methods and call sites
- One-line tie-in pattern with real code examples
- Razor hook pattern (single-line component delegation)
- Partial class extension (no hook file change needed)
- Exceptions: middleware, new controllers, background services
- Framework update workflow
- Real-world change catalog for FreeCICD (3 hook lines, 35+ custom files) and FreeGLBA (0 hook lines, 38+ custom files)

**Use when:** Extending FreeCRM for a new project, planning how to structure your custom code, or preparing for a framework update.

---

### 006_architecture.unique_features.md - Unique Features Analysis

**Purpose:** Catalogs features that exist in only 1-2 projects and are candidates for documentation or reuse.

**Key Topics:**
- Digital signature capture (nForm)
- Workflow automation engine (nForm)
- Network graph visualization (DependencyManager)
- SignalR real-time updates (base feature)
- Plugin system (base feature)

**Use when:** Looking for advanced patterns to implement, or deciding what features to port to other projects.

---

## Architecture Principles

FreeCRM follows these core architectural principles:

1. **Never Modify Framework Files** - All customization through `.App.` hook files and `{ProjectName}.App.{Feature}` custom files
2. **One-Line Tie-In** - Hook files get single-line calls to your custom code; custom code lives in isolated files
3. **Wrapper Pattern** - Many .NET features are wrapped with tenant-aware helpers
4. **Partial Classes** - Large classes split across files with `DataObjects.X.cs` pattern
5. **Autocomplete-Friendly** - Nested classes provide filtered IntelliSense

---

*Category: Architecture*
*Last Updated: 2025-07-25*
