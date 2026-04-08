# 103 — Research: Unique Patterns by Project (Cross-Reference)

> **Document ID:** 103
> **Category:** Research
> **Purpose:** Map every reusable pattern to its source project(s) across our private `_Repos/` and public `ReferenceProjects/`.
> **Audience:** Devs, AI agents, doc writers.
> **Outcome:** Know where every pattern lives, which projects share it, and what's worth showcasing.

**Source:** Scan of 30 `_Repos/` projects + 4 `ReferenceProjects/` projects.

---

## Summary: _Repos Project Classification

### FreeCRM-Based Projects (21)

These share the FreeCRM core (DataAccess, DataObjects, DataController, Blazor client):

| Project | Domain | Notable Patterns |
|---------|--------|-----------------|
| **AcademicCalendarPetitions** | Academic calendar | Largest repo; contains nested ReferenceProjects |
| **Athletic Eligibility 3** | Athletic compliance | Standard CRUD |
| **Credentials** | Credential tracking | Standard CRUD |
| **DependencyManager** | IT dependency tracking | **vis.js NetworkChart** (unique), single-tenant routing |
| **Estimate** | Cost estimation | External API client |
| **Flex5** | Flexible CRM | Nested sub-projects |
| **FormsToImaging** | Document imaging | jSignature, Workflow |
| **GoToWSU** | Campus navigation | Standard CRUD |
| **Helpdesk4** | IT help desk | **IP Address Manager** (unique), asset management, CSV export |
| **nForm** | Form builder | **Monaco editor** (wrapper), **Workflow engine**, **jSignature**, **UploadFile**, Plugin system |
| **RONet** | Research/grants | Nested FreeCRM forks |
| **ScreeshotEXEBlazor** | Screenshot tool | Playwright browser automation |
| **ServiceContracts** | Contract management | Monaco, standard CRUD |
| **SSO** | Single sign-on | **TOTP/MFA** (unique), Okta integration, timer countdown |
| **Tasks** | Scheduled tasks | **Task scheduler** (URL invoke, programs), BackgroundService |
| **TrusselBuilder** | Email builder | **Countdown timer** (unique), **Live preview iframe**, Monaco, HTML editor |
| **WebServices** | API services | External API, middleware |
| **Workday** | Workday integration | External API |
| **WSAF** | Work study | Standard CRUD |
| **FAX** | Fax processing | File upload, background processing |
| **Flex4** | Legacy CRM | Older framework version |

### Non-FreeCRM Projects (9)

| Project | Stack | Notes |
|---------|-------|-------|
| Catalog2 | ASP.NET (non-FreeCRM) | Different architecture |
| FreeForm | Blazor (non-FreeCRM) | Standalone form builder |
| Orientation | ASP.NET (non-FreeCRM) | Student orientation |
| ReleasePipelines | N/A | CI/CD configs only |
| SAP | ASP.NET (non-FreeCRM) | SAP integration |
| SOC | ASP.NET (non-FreeCRM) | Security operations |
| TouchPoints | ASP.NET (non-FreeCRM) | Student engagement |
| Umbraco / Umbraco13 | Umbraco CMS | Different platform |

### Public Reference Projects

| Project | Location | Notable Patterns |
|---------|----------|-----------------|
| **FreeCRM-main** | `ReferenceProjects/` | Base framework — Filter/Pagination, SignalR, CSV, FileDownload, BackgroundProcessor |
| **FreeCICD-main** | `ReferenceProjects/` | **Pipeline dashboard** (cards/table), **PipelineMonitorService** (BackgroundService+SignalR+backoff), Wizard pattern |
| **FreeGLBA-main** | `ReferenceProjects/` | **API key middleware**, external REST API, action filter logging |
| **FreeExamples_base** | `ReferenceProjects/` | Clean rename baseline |

---

## Pattern Cross-Reference Matrix

### Tier 1: Unique / Rare Patterns (1–2 projects)

These are the most interesting for documentation — they exist in very few places.

| Pattern | Source Project(s) | Doc Status | Showcase Priority |
|---------|-------------------|------------|-------------------|
| **vis.js Network Graph** | DependencyManager | ✅ DONE (008_components.network_chart.md) | HIGH — implement |
| **jSignature Capture** | nForm, FormsToImaging | ✅ DONE (008_components.signature.md) | HIGH — implement |
| **Workflow Engine** | nForm, FormsToImaging | ❌ Needs doc | LOW (too complex for example) |
| **IP Address Manager** | Helpdesk4 | ❌ Needs doc | MEDIUM (niche but pattern-rich) |
| **TOTP/MFA + Timer Countdown** | SSO | ❌ Needs doc | HIGH — implement (timer pattern reusable) |
| **Countdown Timer Builder** | TrusselBuilder | ❌ Needs doc | MEDIUM |
| **Live Preview IFrame** | TrusselBuilder | ❌ Needs doc | HIGH — implement |
| **Pipeline Dashboard Cards** | FreeCICD | Partial (wizard doc) | HIGH — implement |
| **BackgroundService + SignalR + Backoff** | FreeCICD | ❌ Needs doc | HIGH — implement |
| **API Key Middleware** | FreeGLBA | ❌ Needs doc | HIGH — implement |
| **Task Scheduler (URL/Program)** | Tasks | ❌ Needs doc | MEDIUM |
| **Barcode/QR** | (none real, just ASP.NET Identity) | N/A | SKIP |

### Tier 2: Widely-Used Base Patterns (Documented or Partially Documented)

These patterns exist across 10+ projects as part of the FreeCRM base template.

| Pattern | Projects Using | Doc Status | Showcase Priority |
|---------|---------------|------------|-------------------|
| **Monaco Editor (wrapper)** | 14 projects | ✅ DONE (008_components.monaco.md) | HIGH — implement |
| **Highcharts** | 22 projects | ✅ DONE (008_components.highcharts.md) | HIGH — implement |
| **SignalR Real-Time** | All FreeCRM projects | ✅ DONE (007_patterns.signalr.md) | HIGH — implement |
| **Filter/Pagination** | All FreeCRM projects | Partial (razor_templates.md) | HIGH — implement |
| **CSV Export** | 30 projects (CsvHelper + DownloadFileToBrowser) | ❌ Needs doc | HIGH — implement |
| **File Upload (MudBlazor drag-drop)** | 29 projects | ❌ Needs doc | HIGH — implement |
| **File Download to Browser** | All FreeCRM projects | ❌ Needs doc | HIGH — bundle with CSV |
| **Wizard (multi-step)** | FreeCICD, nForm | ✅ DONE (008_components.wizard.md) | HIGH — implement |
| **Plugin System** | 18 projects | ❌ Needs doc | LOW (complex) |

### Tier 3: UI Micro-Patterns (Ubiquitous)

These are small patterns used everywhere — worth documenting as quick-reference.

| Pattern | Projects Using | Doc Status | Showcase Priority |
|---------|---------------|------------|-------------------|
| **Bootstrap Cards** | 23 projects | ❌ Needs pattern doc | HIGH — implement |
| **Pagination Component** | 28 projects | ❌ Needs pattern doc | HIGH — bundle with filter |
| **Tabs (Bootstrap)** | 26 projects | ❌ Needs pattern doc | HIGH — implement |
| **Modal/Dialog (Radzen)** | 28 projects | ❌ Needs pattern doc | HIGH — implement |
| **Debounce Timer** | 25 projects | ❌ Needs pattern doc | HIGH — implement |
| **LocalStorage** | 26 projects (Blazored) | ❌ Needs pattern doc | MEDIUM |
| **Clipboard Copy** | 23 projects | ❌ Needs pattern doc | MEDIUM — implement |
| **Toast/Notifications** | 29 projects | ❌ Needs pattern doc | MEDIUM |
| **Drag-and-Drop** | 29 projects | ❌ Needs pattern doc | LOW |
| **DateTimePicker** | 18 projects | ❌ Needs pattern doc | MEDIUM |
| **HtmlEditorDialog** | 22 projects | ❌ Needs pattern doc | MEDIUM |

---

## Project Pattern Heatmap

Projects sorted by pattern diversity (richest → simplest):

| Project | Unique | Advanced | Base | Total Patterns |
|---------|--------|----------|------|---------------|
| **nForm** | 3 (Monaco wrapper, Workflow, jSignature) | 4 (Plugin, UploadFile, HtmlEditor, Drag) | 10+ | **17+** |
| **TrusselBuilder** | 2 (Countdown, LivePreview) | 3 (Monaco, HtmlEditor, Templates) | 10+ | **15+** |
| **FreeCICD** | 2 (PipelineCards, MonitorService) | 3 (Wizard, SignalR live, BackgroundService) | 8+ | **13+** |
| **Helpdesk4** | 1 (IpManager) | 3 (AssetMgmt, CSV export, PDF) | 10+ | **14+** |
| **SSO** | 1 (TOTP/MFA) | 2 (Timer, Okta) | 6+ | **9+** |
| **FreeGLBA** | 1 (ApiKeyMiddleware) | 2 (External API, ActionFilter) | 8+ | **11+** |
| **DependencyManager** | 1 (NetworkChart) | 1 (SingleTenant routing) | 8+ | **10+** |
| **Tasks** | 1 (TaskScheduler) | 1 (URL invoke) | 8+ | **10+** |

---

## Key File Locations for Pattern Extraction

### Unique Pattern Source Files

| Pattern | File Path | Reuse Rating |
|---------|-----------|-------------|
| MonacoEditor wrapper | `_Repos/nForm/nForm.Client/Shared/MonacoEditor.razor` | ⭐⭐⭐⭐⭐ |
| Signature pad | `_Repos/nForm/nForm.Client/Shared/Signature.razor` + `.razor.js` | ⭐⭐⭐⭐⭐ |
| NetworkChart | `_Repos/DependencyManager/.../Shared/NetworkChart.razor` + `.razor.js` | ⭐⭐⭐⭐ |
| File Upload | `_Repos/nForm/nForm.Client/Shared/UploadFile.razor` | ⭐⭐⭐⭐⭐ |
| Pipeline Card | `ReferenceProjects/FreeCICD-main/.../Dashboard.PipelineCard.razor` | ⭐⭐⭐⭐ |
| Pipeline Monitor | `ReferenceProjects/FreeCICD-main/.../PipelineMonitorService.cs` | ⭐⭐⭐⭐ |
| Highcharts | `_Repos/nForm/nForm.Client/Shared/Highcharts.razor` + `.razor.js` | ⭐⭐⭐⭐⭐ |
| TOTP + Timer | `_Repos/SSO/SSO.Client/Pages/SlateTOTP.razor` | ⭐⭐⭐ |
| Preview IFrame | `_Repos/TrusselBuilder/.../PreviewIFrame.razor` + `.razor.js` | ⭐⭐⭐⭐ |
| IP Manager | `_Repos/Helpdesk4/.../Pages/IpManager/IpManager.razor` | ⭐⭐⭐ |
| CSV Export | `_Repos/Helpdesk4/.../Helpers.cs` (GetCsvData<T>) | ⭐⭐⭐⭐⭐ |
| Download to Browser | `ReferenceProjects/FreeCRM-main/.../Helpers.cs` (DownloadFileToBrowser) | ⭐⭐⭐⭐⭐ |
| API Key Middleware | `ReferenceProjects/FreeGLBA-main/.../ApiKeyMiddleware.cs` | ⭐⭐⭐⭐ |
| Wizard Stepper | `ReferenceProjects/FreeCICD-main/.../WizardStepper.razor` | ⭐⭐⭐⭐⭐ |

### Base Framework Utilities (FreeCRM-main)

| Utility | File | Method/Class |
|---------|------|-------------|
| Pagination/Filter base | `CRM.DataObjects/DataObjects.cs` | `class Filter` (Page, PageCount, Sort, etc.) |
| File download JS | `CRM.Client/Helpers.cs` | `DownloadFileToBrowser()` |
| HTTP helper | `CRM.Client/Helpers.cs` | `GetOrPost<T>()` |
| Debounce | Various | `System.Timers.Timer` stop/start pattern |
| LocalStorage | Various | `Blazored.LocalStorage` ILocalStorageService |

---

## What's Missing from Our Docs

### Documented ✅

| Pattern | Doc |
|---------|-----|
| Monaco Editor | `008_components.monaco.md` |
| Network Chart | `008_components.network_chart.md` |
| Signature Capture | `008_components.signature.md` |
| Highcharts | `008_components.highcharts.md` |
| Wizard | `008_components.wizard.md` |
| Razor Templates (CRUD) | `008_components.razor_templates.md` |
| SignalR | `007_patterns.signalr.md` |
| Helpers | `007_patterns.helpers.md` |

### Needs New Doc 📝

| Pattern | Proposed Doc | Source |
|---------|-------------|--------|
| File Upload + Download + CSV Export | `007_patterns.file_operations.md` | nForm, Helpdesk4, FreeCRM-main |
| Filter/Pagination/Sorting | `007_patterns.filter_pagination.md` | FreeCRM-main base |
| Bootstrap UI Patterns (Cards, Tabs, Modals) | `008_components.bootstrap_patterns.md` | FreeCICD cards, all projects |
| Timer/Countdown Patterns | `007_patterns.timers.md` | SSO (TOTP), TrusselBuilder, FreeCICD (BackgroundService) |
| BackgroundService + SignalR Live | `007_patterns.background_services.md` | FreeCICD PipelineMonitorService |
| API Key Middleware | `007_patterns.api_key_auth.md` | FreeGLBA |
| External API Client | `007_patterns.external_api.md` | FreeCICD (Azure DevOps), Tasks, SSO (Okta) |
| Live Preview (IFrame) | `008_components.live_preview.md` | TrusselBuilder |
| Debounce | (include in `007_patterns.timers.md`) | All projects |
| Clipboard Copy | (include in `007_patterns.helpers.md` update) | All projects |

### Needs Update to Existing Doc 🔄

| Doc | What to Add |
|-----|------------|
| `006_architecture.unique_features.md` | Update project counts, add new patterns found |
| `007_patterns.helpers.md` | Add DownloadFileToBrowser, GetCsvData, clipboard pattern |
| `008_components.md` (index) | Add new component doc links |
| `007_patterns.md` (index) | Add new pattern doc links |

---

*Created: 2025-07-25*
*Category: Research*
*Source: Scan of 30 `_Repos/` projects + 4 `ReferenceProjects/` projects*
