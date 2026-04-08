# 105 — Example Pages Deep Dive & Uncovered Patterns

> **Document ID:** 105  
> **Category:** Research  
> **Purpose:** Catalog every FreeExamples demo page — what it is, what it showcases, which `_Repos/` or `ReferenceProjects/` project shares the pattern. Then list patterns found in those sources that don't have a demo page yet.  
> **Created:** 2026-02-27

---

## Part 1: Existing Example Pages

### 1. Examples Dashboard

| Field | Value |
|-------|-------|
| **Route** | `/Examples/Dashboard` |
| **File** | `FreeExamples.App.Pages.Dashboard.razor` |
| **What It Shows** | Aggregate summary cards (Total, Active, Completed, Draft), quick-link card grid to all example pages, category breakdown table |
| **Key Patterns** | Dashboard card layout, data-driven page index, `GetOrPost<T>` to a dashboard aggregate API |
| **Source Projects** | Every FreeCRM project has an `Index.App.razor` home dashboard. FreeCICD's pipeline dashboard is the richest version (card + table toggle, filters, grouping). |
| **API** | `GET api/Data/GetSampleDashboard` |

---

### 2. Sample Items (List + Edit)

| Field | Value |
|-------|-------|
| **Route** | `/Examples/SampleItems` (list), `/Examples/EditSampleItem/{itemid}` (edit) |
| **Files** | `FreeExamples.App.Pages.SampleItems.razor`, `FreeExamples.App.Pages.EditSampleItem.razor` |
| **What It Shows** | Full CRUD lifecycle: paginated list → edit record → save/delete. Filter bar with keyword search, status/category/enabled dropdowns, "Include Deleted" toggle. Edit page has required field indicators, `DeleteConfirmation` two-click pattern, `RequiredIndicator`, `Helpers.MissingValue()` CSS class. |
| **Key Patterns** | `PagedRecordset` component, `Filter` base class, `ActionHandler` / `Formatter` column config, `NavigationCallbackHandler` pagination, CSV export, SignalR real-time item sync, three-endpoint API (GetMany/SaveMany/DeleteMany) |
| **Source Projects** | **Every FreeCRM project** uses this exact pattern. Helpdesk4 `Requests.razor`, nForm `Forms.razor`, DependencyManager `Index.razor`, FreeCICD `Pipelines.razor` — all are paginated lists with the same filter/recordset structure. |
| **API** | `POST api/Data/GetSampleItems`, `POST api/Data/SaveSampleItems`, `POST api/Data/DeleteSampleItems` |

---

### 3. File Demo

| Field | Value |
|-------|-------|
| **Route** | `/Examples/FileDemo` |
| **File** | `FreeExamples.App.Pages.FileDemo.razor` |
| **What It Shows** | File upload via `UploadFile` component (MudBlazor drag-drop), uploaded file listing table with remove, download-to-browser for text and CSV reports |
| **Key Patterns** | `UploadFile` component, `DownloadFileToBrowser()` JS interop, `CsvHelper` CSV generation, `FormatBytes()` utility |
| **Source Projects** | **nForm** (form attachments), **Helpdesk4** (request attachments + CSV export), **FormsToImaging** (document uploads). `UploadFile.razor` exists in 29 projects. `DownloadFileToBrowser` is in every FreeCRM `Helpers.cs`. |
| **API** | File storage endpoints from base framework |

---

### 4. Bootstrap Showcase

| Field | Value |
|-------|-------|
| **Route** | `/Examples/BootstrapShowcase` |
| **File** | `FreeExamples.App.Pages.BootstrapShowcase.razor` |
| **What It Shows** | Six tabs of Bootstrap 5 micro-patterns: **Status Cards** (card grid with edit/delete, two-click delete confirmation), **Badges & Status** (status badges, priority badges, boolean icons), **Modals & Dialogs** (Radzen DialogService, DeleteConfirmation component), **Forms & Inputs** (search-with-clear, toggle switches, required field validation with ADA), **Layout Patterns** (accordion, card/table view toggle, progress bars), **Feedback & Alerts** (live toast triggers, spinner variants, alert types, empty-state placeholder) |
| **Key Patterns** | Bootstrap tabs, `DeleteConfirmation` component, `Radzen.DialogService`, `is-invalid` + `aria-invalid` ADA validation, `Model.AddMessage()` toast system, `btn-group` view toggle, accordion expand/collapse |
| **Source Projects** | **FreeCICD** `Dashboard.FilterBar.razor` (search-with-clear), `Dashboard.ViewControls.razor` (card/table toggle, group-by switch, sort dropdown), `Dashboard.PipelineCard.razor` (status cards). **All FreeCRM projects** use `DeleteConfirmation`, `ToastMessages`, `GetInputDialog`. |

---

### 5. Charts Dashboard

| Field | Value |
|-------|-------|
| **Route** | `/Examples/ChartsDashboard` |
| **File** | `FreeExamples.App.Pages.ChartsDashboard.razor` |
| **What It Shows** | Highcharts pie chart (Items by Status) with click drill-down, Highcharts column chart (Amount by Category) |
| **Key Patterns** | `Highcharts` component wrapper, `ChartTypes.Pie` / `ChartTypes.Column`, `SeriesDataItems` / `SeriesDataArrayItems` binding, `OnItemClicked` callback for drill-down |
| **Source Projects** | **22 projects** include the `Highcharts.razor` + `Highcharts.razor.js` wrapper. nForm uses it for form submission analytics, Helpdesk4 for request volume charts, FreeCICD for pipeline success rates. |
| **API** | `GET api/Data/GetSampleDashboard` (reuses dashboard aggregate data) |

---

### 6. Code Editor

| Field | Value |
|-------|-------|
| **Route** | `/Examples/CodeEditor` |
| **File** | `FreeExamples.App.Pages.CodeEditor.razor` |
| **What It Shows** | Monaco editor with language selector (HTML, CSS, JS, C#, JSON, SQL), insert-at-cursor button, live two-way value binding, diff editor mode |
| **Key Patterns** | `MonacoEditor` component wrapper (`BlazorMonaco`), `MonacoLanguage` static class, `@bind-Value`, `ReadOnly` toggle, language switching, cursor position insertion |
| **Source Projects** | **14 projects** include `MonacoEditor.razor`. nForm uses it for custom code plugins, ServiceContracts for JSON editing, TrusselBuilder for HTML email template editing, FreeCICD for YAML pipeline editing (`Wizard.StepPreview.razor`). |

---

### 7. SignalR Demo

| Field | Value |
|-------|-------|
| **Route** | `/Examples/SignalRDemo` |
| **File** | `FreeExamples.App.Pages.SignalRDemo.razor` |
| **What It Shows** | Three-panel real-time demo: **Active Users presence board** (online/away status, current view, "You" badge), **Quick-Add form** that broadcasts saves via SignalR, **Activity Feed** (timestamped event log of save/delete events), **Live Item Table** (in-place row updates, pending-updates badge, auto-refresh toggle) |
| **Key Patterns** | `Model.OnSignalRUpdate` subscription, `Model.ActiveUsers` presence list, `SignalRUpdateType` enum dispatching, in-place row update vs. full refresh, auto-refresh toggle, pending update badge |
| **Source Projects** | **Helpdesk4** (live ticket updates on `Request.razor`, active users on `Index.razor`, pending updates badge). **FlexCRM** (call center agent presence board). **FreeCICD** (`PipelineMonitorService` broadcasts pipeline status changes via SignalR). All FreeCRM projects have the base SignalR hub. |

---

### 8. Timer Demo

| Field | Value |
|-------|-------|
| **Route** | `/Examples/TimerDemo` |
| **File** | `FreeExamples.App.Pages.TimerDemo.razor` |
| **What It Shows** | **Countdown timer** (60-second cycle with progress bar, danger-red at ≤10s), **Debounce input** (500ms delay before processing, shows debounce count), **Auto-refresh** (configurable interval, start/stop toggle, elapsed count) |
| **Key Patterns** | `System.Timers.Timer` with `Elapsed` + `InvokeAsync(StateHasChanged)`, `Helpers.SetTimeout()` debounce wrapper, progress bar with dynamic width/color, `IDisposable` timer cleanup |
| **Source Projects** | **SSO** `SlateTOTP.razor` (TOTP countdown timer, 60-second cycle, progress bar that turns red at ≤10s — this is the *exact* origin of the timer pattern). **TrusselBuilder** `EditCountdownTimer.razor` (configurable countdown with color/style settings). **All FreeCRM projects** use `SetTimeout()` for debounce. |

---

### 9. Network Graph

| Field | Value |
|-------|-------|
| **Route** | `/Examples/NetworkGraph` |
| **File** | `FreeExamples.App.Pages.NetworkGraph.razor` |
| **What It Shows** | vis.js interactive network visualization with category hub nodes connected to item nodes, solver toggle (repulsion/Barnes-Hut), layout randomize, node/edge click selection, physics configuration |
| **Key Patterns** | `NetworkChart` component wrapper (`NetworkChart.razor` + `NetworkChart.razor.js`), `vis.Network` initialization, `DotNetObjectReference` for JS→C# callbacks, `vis.DataSet` for nodes/edges, solver configuration |
| **Source Projects** | **DependencyManager** — the *sole origin* of the NetworkChart pattern. `DependencyManager.Client/Shared/NetworkChart.razor` + `.razor.js`. Used to visualize IT system dependencies (servers → applications → databases). |

---

### 10. Signature Demo

| Field | Value |
|-------|-------|
| **Route** | `/Examples/SignatureDemo` |
| **File** | `FreeExamples.App.Pages.SignatureDemo.razor` |
| **What It Shows** | jSignature digital signature pad with clear button, live base30 data binding, signature preview rendering |
| **Key Patterns** | `Signature` component wrapper (`Signature.razor` + `Signature.razor.js`), `DotNetObjectReference` JS interop, base30 signature data format, `@bind-Value` two-way binding |
| **Source Projects** | **nForm** (form signature fields), **FormsToImaging** (document signing). `Signature.razor` + `Signature.razor.js` exist in these two projects. |

---

### 11. Wizard Demo

| Field | Value |
|-------|-------|
| **Route** | `/Examples/WizardDemo` |
| **File** | `FreeExamples.App.Pages.WizardDemo.razor` |
| **What It Shows** | Four-step wizard (Category → Details → Priority → Review) with clickable breadcrumb stepper, inline selection summary badges, per-step validation guards (`NextDisabled`), ADA-compliant form fields (`aria-required`, `aria-invalid`, `role="alert"` errors), review table |
| **Key Patterns** | `WizardStepper` component (clickable circles + connector lines + selected value preview), `WizardSummary` component (badge pills), `WizardStepHeader` component (Back/Next/Finish with disabled state), `_attemptedNext` validation flag, `IsCurrentStepValid()` gate |
| **Source Projects** | **FreeCICD** Pipeline Wizard — the *direct origin*. `FreeCICD.App.UI.Wizard.razor` with `Wizard.Stepper.razor`, `Wizard.Summary.razor`, `Wizard.StepHeader.razor`, and 8 step-specific components (`StepPAT`, `StepProject`, `StepRepository`, `StepBranch`, `StepCsproj`, `StepEnvironments`, `StepPipeline`, `StepPreview`). Also **nForm** has a form-creation wizard. |

---

### 12. Git Browser

| Field | Value |
|-------|-------|
| **Route** | `/Examples/GitBrowser` |
| **File** | `FreeExamples.App.Pages.GitBrowser.razor` |
| **What It Shows** | Clone a public git repo into a temp directory, browse its directory tree with folder/file listing, clickable breadcrumb path navigation, file viewing in read-only Monaco editor with 35+ extension-to-language mappings, binary file detection, size formatting |
| **Key Patterns** | `LibGit2Sharp` server-side clone, `GitBrowserService` singleton with caching, `GitRepoEntry` / `GitFileContent` DTOs, GetMany-style API, breadcrumb `<nav>` with path segments, `MonacoEditor` read-only viewer, file icon mapping, `FormatBytes()` |
| **Source Projects** | **FreeCICD** works extensively with Azure DevOps repos (browsing branches, files, .csproj discovery). The file-browsing and breadcrumb patterns are adapted from FreeCICD's wizard steps (`StepRepository`, `StepBranch`, `StepCsproj`). LibGit2Sharp is new to FreeExamples. |
| **API** | `POST api/Data/GetGitRepoContents`, `POST api/Data/GetGitFileContent` |

---

## Part 2: Patterns NOT Yet on an Example Page

These patterns exist in `_Repos/` and `ReferenceProjects/` but have no corresponding FreeExamples demo page.

### Tier 1: High Value — Should Build Next

| # | Pattern | Source Project(s) | What It Demonstrates | Complexity |
|---|---------|-------------------|---------------------|------------|
| 1 | **Live Preview IFrame** | TrusselBuilder `EditTemplate.razor` | Side-by-side HTML editor + live preview iframe that refreshes on save. Uses `Toolbelt.Blazor.Splitter.V2` for resizable panes. Template has Monaco on left, rendered HTML preview on right. | Medium |
| 2 | **BackgroundService + SignalR + Exponential Backoff** | FreeCICD `PipelineMonitorService.cs` | A `BackgroundService` that polls an external source, caches state, diffs changes, and broadcasts only deltas via SignalR. Includes subscriber-aware polling (only polls when clients are listening), exponential backoff on errors (`_consecutiveErrors * baseDelay`), and cache seeding on first run. | Medium |
| 3 | **API Key Middleware** | FreeGLBA `FreeGLBA.App.ApiKeyMiddleware.cs` | Custom middleware that intercepts specific routes and validates a `Bearer` API key from the `Authorization` header. Demonstrates path-based middleware routing, hashed key comparison, RFC 7807 error responses. | Low |
| 4 | **DateTimePicker** | Helpdesk4 `IpManager.razor`, 18 projects total | The `DateTimePicker` component used across 18 projects for date range filters. Shows `DateOnly?` / `DateTime?` binding, `Helpers.DateOnlyToDateTime()` converters, filter integration. | Low |
| 5 | **HtmlEditorDialog** | TrusselBuilder, nForm, 22 projects | Rich text editor dialog (Radzen HTML editor wrapped in a dialog). Used for email body editing, form field descriptions, content blocks. | Low |
| 6 | **Clipboard Copy** | All projects (via `Helpers.cs`) | One-click copy-to-clipboard with tooltip feedback. Used for copying API keys, share links, generated codes. Not yet showcased despite being in 23 projects. | Low |
| 7 | **LocalStorage Preferences** | All projects (Blazored.LocalStorage) | Persist user preferences (theme, sidebar state, filter settings, view mode) to browser local storage. Used in 26 projects. SSO stores MFA preferences, FreeCICD stores dashboard view mode. | Low |

### Tier 2: Medium Value — Useful Demonstrations

| # | Pattern | Source Project(s) | What It Demonstrates | Complexity |
|---|---------|-------------------|---------------------|------------|
| 8 | **TOTP/MFA Code Display** | SSO `SlateTOTP.razor` | Generates and displays a 6-digit TOTP code with a 60-second countdown, progress bar that turns red at ≤10s, auto-refresh at the top of each minute. The timer pattern is already in TimerDemo, but the TOTP code generation + display UX is unique. | Medium |
| 9 | **Drag-and-Drop Reorder** | nForm (field ordering), 29 projects (via MudBlazor) | Drag-to-reorder items in a list. nForm uses it for form field ordering within sections, TrusselBuilder for template section ordering. MudBlazor `MudDropZone` component. | Medium |
| 10 | **PDF Viewer** | nForm, TrusselBuilder, FreeCICD (all have `PDF_Viewer.razor`) | Inline PDF display using embedded object/iframe with fallback download link. | Low |
| 11 | **Tag Selector** | TrusselBuilder `TagSelector.razor`, all projects with `Tags` module | Multi-select tag picker that shows available tags as chips/badges, click-to-toggle, sorted by name. Used in 18+ projects for entity tagging. | Low |
| 12 | **Duplicate Record** | TrusselBuilder `EditTemplate.razor`, Helpdesk4 `EditRequest.razor` | "Duplicate" button pattern: copies an existing record into a new unsaved record with a modified name. TrusselBuilder appends " (Copy)" to template name. | Low |
| 13 | **Import / Export** | TrusselBuilder (template JSON export/import), nForm (`ImportExport.razor` for forms), FreeCICD (YAML import) | Serialize an entity to JSON/YAML, download it, then import from file to recreate. TrusselBuilder has both single and batch export. | Medium |
| 14 | **Tooltip Component** | TrusselBuilder `Tooltip.razor`, nForm `Tooltip.razor` | Reusable Bootstrap tooltip wrapper component with placement options and HTML content support. | Low |

### Tier 3: Niche / Advanced — Worth Documenting but Complex

| # | Pattern | Source Project(s) | What It Demonstrates | Complexity |
|---|---------|-------------------|---------------------|------------|
| 15 | **Workflow Engine** | nForm `Workflow.razor`, FormsToImaging | Multi-step approval workflows with conditional routing, status transitions, email notifications at each step. Too complex for a single example page but a key pattern. | High |
| 16 | **Plugin System + Dynamic Compilation** | nForm, 18 projects | `PluginPrompts.razor` renders dynamic forms from plugin definitions. `CompilationService.cs` in FreeExamples already has the Roslyn infrastructure for dynamic Blazor compilation. | High |
| 17 | **IP Address Manager** | Helpdesk4 `IpManager.razor` | Full CRUD for IP address records with date range filtering, `DateTimePicker`, and paginated listing. Niche domain but demonstrates the filter pattern with date ranges well. | Medium |
| 18 | **Task Scheduler** | Tasks project | Background task scheduler that invokes URLs or runs programs on a cron-like schedule. Uses `BackgroundService` with EF for task definitions. | High |
| 19 | **Email Template System** | TrusselBuilder (full template builder), Helpdesk4 `EmailTemplates/` | WYSIWYG email template editor with variable substitution tokens, snippet library, send-test functionality. TrusselBuilder's is the most advanced with live preview + countdown timer embedding. | High |
| 20 | **External API Client** | FreeCICD (Azure DevOps REST API), SSO (Okta API), Tasks (URL invoke), Workday | Typed HTTP client calling external REST APIs with auth, retry, error handling. FreeCICD uses `VssConnection` for Azure DevOps. SSO calls Okta for MFA enrollment. | Medium |
| 21 | **Resizable Split Panes** | TrusselBuilder `EditTemplate.razor` | `Toolbelt.Blazor.Splitter.V2` for resizable side-by-side panes. Used in template editor for code/preview split. | Low |
| 22 | **Action Filter Logging** | FreeGLBA `DataController` | Custom action filter that logs API requests/responses for audit compliance. Captures request body, response status, timing, user identity. | Medium |

---

## Part 3: Coverage Summary

### What's Covered ✅

| Category | Example Page | Core Pattern |
|----------|-------------|-------------|
| CRUD List + Edit | Sample Items | `PagedRecordset`, Filter, GetMany/SaveMany/DeleteMany |
| File Operations | File Demo | Upload, Download, CSV export |
| Bootstrap UI | Bootstrap Showcase | Cards, tabs, modals, forms, alerts, toggles, spinners |
| Data Visualization | Charts Dashboard | Highcharts pie + column |
| Code Editing | Code Editor | Monaco editor, language modes, diff |
| Real-Time | SignalR Demo | Presence, activity feed, live table, auto-refresh |
| Timers | Timer Demo | Countdown, debounce, auto-refresh |
| Graph Visualization | Network Graph | vis.js nodes/edges, physics solvers |
| Digital Signature | Signature Demo | jSignature, JS interop |
| Multi-Step Wizard | Wizard Demo | Stepper, summary, validation, ADA |
| Git Integration | Git Browser | LibGit2Sharp, file browser, Monaco viewer |

### What's Missing ❌ (Prioritized Recommendations)

| Priority | Pattern | Recommended Page Name | Est. Effort |
|----------|---------|----------------------|-------------|
| 🔴 HIGH | Live Preview IFrame | `LivePreviewDemo` | 1 day |
| 🔴 HIGH | BackgroundService + SignalR Polling | `BackgroundServiceDemo` | 1 day |
| 🔴 HIGH | API Key Middleware | `ApiKeyDemo` | Half day |
| 🟡 MEDIUM | Clipboard Copy | Add to Bootstrap Showcase "Forms" tab | 1 hour |
| 🟡 MEDIUM | LocalStorage Preferences | Add to Bootstrap Showcase or new `PreferencesDemo` | Half day |
| 🟡 MEDIUM | DateTimePicker | Add to Bootstrap Showcase "Forms" tab | 2 hours |
| 🟡 MEDIUM | HtmlEditorDialog | Add to Bootstrap Showcase "Modals" tab | 2 hours |
| 🟡 MEDIUM | PDF Viewer | Add to File Demo page | 2 hours |
| 🟢 LOW | Drag-and-Drop Reorder | `DragDropDemo` | Half day |
| 🟢 LOW | Import/Export JSON | Add to File Demo page | Half day |
| 🟢 LOW | Tag Selector | Add to Bootstrap Showcase | 2 hours |
| 🟢 LOW | Duplicate Record | Already in EditSampleItem — just needs docs | 1 hour |

---

## Part 4: Pattern Source File Quick Reference

For anyone building the next example pages, here are the exact files to reference:

| Pattern | Primary Source File |
|---------|-------------------|
| Live Preview IFrame | `_Repos/TrusselBuilder/Trussel.Client/Pages/App/Templates/EditTemplate.razor` |
| Resizable Split Panes | Same file (uses `Toolbelt.Blazor.Splitter.V2`) |
| BackgroundService + SignalR | `ReferenceProjects/FreeCICD-main/FreeCICD/Services/FreeCICD.App.PipelineMonitorService.cs` |
| API Key Middleware | `ReferenceProjects/FreeGLBA-main/FreeGLBA/Controllers/FreeGLBA.App.ApiKeyMiddleware.cs` |
| TOTP Code Display | `_Repos/SSO/SSO.Client/Pages/SlateTOTP.razor` |
| DateTimePicker | `_Repos/Helpdesk4/HelpDesk.Client/Pages/IpManager/IpManager.razor` |
| HtmlEditorDialog | `_Repos/TrusselBuilder/Trussel.Client/Shared/HtmlEditorDialog.razor` |
| Clipboard Copy | `Helpers.cs` → search for `CopyToClipboard` or `navigator.clipboard` in any project |
| LocalStorage | Any project's `Helpers.cs` → `LocalStorage.GetItemAsync` / `SetItemAsync` |
| PDF Viewer | `_Repos/nForm/nForm.Client/Shared/PDF_Viewer.razor` |
| Tag Selector | `_Repos/TrusselBuilder/Trussel.Client/Shared/TagSelector.razor` |
| Drag-and-Drop | `_Repos/nForm/nForm.Client/Shared/EditField.razor` (MudDropZone) |
| Import/Export | `_Repos/TrusselBuilder/Trussel.Client/Pages/App/Templates/EditTemplate.razor` (Export/Import methods) |
| Tooltip | `_Repos/TrusselBuilder/Trussel.Client/Shared/Tooltip.razor` |
| Workflow | `_Repos/nForm/nForm.Client/Shared/Workflow.razor` |
| Plugin Prompts | Any project's `Shared/PluginPrompts.razor` |

---

*Created: 2026-02-27*  
*Category: Research*  
*Source: Deep read of 13 FreeExamples pages + 30 `_Repos/` projects + 4 `ReferenceProjects/`*
