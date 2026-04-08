# 107 — Deep Dive: Missing Examples & View Variations

> **Document ID:** 107  
> **Category:** Analysis  
> **Purpose:** Comprehensive audit of what FreeExamples has, what the reference projects contain, and every pattern/feature we haven't built yet.  
> **Status:** PENDING APPROVAL

---

## Part 1: What We Currently Have (14 pages)

| # | Page | Pattern Source | Status |
|---|------|---------------|--------|
| 1 | Dashboard | Custom | ✅ Complete |
| 2 | Sample Items (list) | FreeCRM pattern | ✅ Complete |
| 3 | Edit Sample Item | FreeCRM pattern | ✅ Complete |
| 4 | File Demo | FreeCRM Files pattern | ✅ Complete |
| 5 | Bootstrap Showcase | Custom sampler | ✅ Complete |
| 6 | Charts Dashboard | FreeCRM/FreeGLBA Highcharts | ✅ Complete |
| 7 | Code Editor | FreeCRM Monaco | ✅ Complete |
| 8 | SignalR Demo | FreeCRM SignalR pattern | ✅ Complete |
| 9 | Timer Demo | FreeCRM timers pattern | ✅ Complete |
| 10 | Network Graph | FreeCRM vis.js pattern | ✅ Complete |
| 11 | Signature Demo | FreeCRM jSignature | ✅ Complete |
| 12 | Wizard Demo | FreeCICD wizard pattern | ✅ Complete |
| 13 | Git Browser | Custom (LibGit2Sharp) | ✅ Complete |
| 14 | API Key Demo | FreeGLBA middleware pattern | ✅ Complete |

---

## Part 2: Reference Project Patterns NOT Yet Demonstrated

### A. FreeCICD — Pipeline Dashboard Patterns

The FreeCICD Pipeline Dashboard is the **richest single page** in any reference project. It has patterns we haven't touched:

| # | Pattern | Description | FreeCICD Source | Priority |
|---|---------|-------------|-----------------|----------|
| A1 | **Multi-View Toggle** (Card / Table) | A dropdown or button group that switches the same dataset between completely different rendering modes. FreeCICD has Card view (visual cards with status badges, branch info, duration bars) and Table view (dense sortable rows). | `FreeCICD.App.UI.Dashboard.ViewControls.razor` + `PipelineCard.razor` + `TableView.razor` | 🔴 HIGH |
| A2 | **Group By Folder** | Toggle to group items into collapsible folder sections with nested hierarchy. Uses a `FolderNode` tree model with recursive `GetAllPipelines()`. Expand/collapse tracked in a `HashSet<string>`. | `FreeCICD.App.UI.Dashboard.Pipelines.razor` lines 36-62 | 🟡 MEDIUM |
| A3 | **Rich Sort Dropdown** | 12 sort options (Name, Last Run, Status, Branch, Repository, Duration — each asc/desc). Sort applied client-side to the filtered list. | `FreeCICD.App.UI.Dashboard.ViewControls.razor` lines 36-55 | 🔴 HIGH |
| A4 | **Multi-Filter Bar** | Horizontal row of dropdowns: Status, Result, Trigger, Repository, plus a "Failed Only" checkbox. Each filter updates instantly via `@bind:after`. | `FreeCICD.App.UI.Dashboard.FilterBar.razor` | 🟡 MEDIUM |
| A5 | **Pipeline Status Cards** | Cards with color-coded headers (green=success, red=failed), embedded progress bars for duration, branch badges, trigger icons, commit links, relative timestamps via Humanizer. | `FreeCICD.App.UI.Dashboard.PipelineCard.razor` | 🟡 MEDIUM |
| A6 | **Variable Group Badges** | Inline badge pills showing linked configuration groups. Pattern for showing many-to-many relationships inline. | `FreeCICD.App.UI.Dashboard.VarGroupBadges.razor` | 🟢 LOW |

### B. FreeGLBA — Compliance & API Log Patterns

| # | Pattern | Description | FreeGLBA Source | Priority |
|---|---------|-------------|-----------------|----------|
| B1 | **Master-Detail Split View** | Click a row → page splits into 50/50 (list on left, detail panel on right). Column class toggles between `col-12` and `col-lg-6` based on selection. No navigation away from the list. | `FreeGLBA.App.AccessEventsPage.razor` lines 77, 347-350 | 🔴 HIGH |
| B2 | **Advanced Filters (Expandable)** | "More Filters" button toggles a hidden section with date range pickers, text inputs, and additional dropdowns. A badge count shows how many advanced filters are active. | `FreeGLBA.App.AccessEventsPage.razor` lines 115-168 | 🔴 HIGH |
| B3 | **Active Filter Pills** | Colored badge pills below the filter bar showing each active filter with an (x) button to remove individually. Gives users visual feedback on what's filtering the list. | `FreeGLBA.App.AccessEventsPage.razor` lines 171-199 | 🔴 HIGH |
| B4 | **Custom Pagination Controls** | Hand-built pagination with First/Prev/Page Numbers/Next/Last. Page number window calculation (`GetPageNumbers()`). Shows "1-25 of 342" record count. | `FreeGLBA.App.AccessEventsPage.razor` lines 306-342 | 🟡 MEDIUM |
| B5 | **Sortable Column Headers** | Column headers with click-to-sort. Visual arrows show current sort direction. Sort icon helper methods (`SortClass`, `SortIcon`). | `FreeGLBA.App.AccessEventsPage.razor` lines 250-261 | 🟡 MEDIUM |
| B6 | **API Log Dashboard** | Time range selector buttons (1h/24h/7d/30d), auto-refresh toggle, stats cards with conditional danger styling (error rate > 5% = red border), trend charts. | `FreeGLBA.App.ApiLogDashboard.razor` | 🟡 MEDIUM |
| B7 | **Request Detail View** | Full-page detail view for a single API request: status banner, request/response side-by-side cards, JSON body display in Monaco, response headers table. | `FreeGLBA.App.ViewApiRequestLog.razor` | 🟢 LOW |
| B8 | **Body Logging Settings** | Settings page with PII warning banner, time-limited enable form (duration dropdown + reason field), confirmation dialog, active configs table with countdown timers. | `FreeGLBA.App.BodyLoggingSettings.razor` | 🟢 LOW |
| B9 | **Quick Report Buttons** | One-click report generation buttons (Last Month, Last Quarter, YTD, Export Review, High Volume). Each pre-fills filters and generates a report immediately. | `FreeGLBA.App.ComplianceReportsPage.razor` lines 77-113 | 🟡 MEDIUM |
| B10 | **Compliance Dashboard** | Multi-section dashboard: stats cards (Today/Week/Month/Total), risk indicators, source system breakdown, about section with API integration code samples. | `FreeGLBA.App.GlbaDashboard.razor` | 🟢 LOW |

### C. FreeCRM — Scheduling & Invoice Patterns

| # | Pattern | Description | FreeCRM Source | Priority |
|---|---------|-------------|-----------------|----------|
| C1 | **Calendar/Scheduler** | Radzen Scheduler with Day/Week/Month/Year views, slot selection for creating appointments, appointment rendering with color-coding, drag support. | `CRM.Client/Pages/Scheduling/Schedule.razor` | 🟡 MEDIUM |
| C2 | **PDF Viewer / Invoice Preview** | Server generates PDF, converts pages to images (base64), renders inline. Download button for the actual PDF file. | `CRM.Client/Pages/Invoices/ViewInvoice.razor` | 🟢 LOW |
| C3 | **Dynamic Component / Plugin System** | `BlazorPlugins` component dynamically renders registered components at extension points (toolbar, top, bottom). Pattern for plugin architecture. | `CRM.Client/Pages/TestPages/DynamicComponent.razor` | 🟢 LOW |
| C4 | **Invoice List with Filters** | Same PagedRecordset pattern as SampleItems but with different filter types (date range, closed/open status, user-specific filtering via URL parameter). | `CRM.Client/Pages/Invoices/Invoices.razor` | 🟢 LOW |

### D. Shared Component Patterns Not Yet Showcased

| # | Component | Description | Exists In | Priority |
|---|-----------|-------------|-----------|----------|
| D1 | **TagSelector** | Multi-select tag picker (add/remove tags from a record). Tags displayed as badge pills. | All reference projects | 🟡 MEDIUM |
| D2 | **UserDefinedFields (UDF)** | Dynamic form fields configured by admin — text, dropdown, date, checkbox — rendered at runtime. | All reference projects | 🟡 MEDIUM |
| D3 | **PDF_Viewer** | Embeds a PDF in the browser using an iframe or object tag. | FreeCRM, FreeGLBA | 🟢 LOW |
| D4 | **HtmlEditorDialog** | Rich text editor in a modal dialog (Radzen HTML editor). | FreeCRM, FreeGLBA | 🟢 LOW |
| D5 | **SelectFile** | File picker dialog — browse uploaded files, select one, return the file reference. | All reference projects | 🟢 LOW |
| D6 | **UndeleteMessage** | Banner shown on soft-deleted records with a "Restore" button. | All reference projects | 🟢 LOW |
| D7 | **SwitchTenants** | Dropdown/dialog to switch between tenants in a multi-tenant app. | FreeCRM, FreeCICD | 🟢 LOW |
| D8 | **OffcanvasPopoutMenu** | Slide-out side panel for navigation or settings. Bootstrap offcanvas pattern. | All reference projects | 🟢 LOW |

---

## Part 3: Sample Items — 5 View Variations (The Big Idea)

Like FreeCICD's Pipeline Dashboard, the Sample Items page should offer **multiple ways to view the same data**. A dropdown at the top switches between views. All views share the same filter/data — only the rendering changes.

### View 1: Table View (Current Default)
**What it is:** The PagedRecordset table we already have.  
**Pattern from:** Current SampleItems page, FreeCRM lists.  
**Already built:** Yes ✅

### View 2: Card Grid View
**What it is:** Bootstrap card grid (3 columns desktop, 2 tablet, 1 mobile). Each card shows Name, Category badge, Status badge, Amount, Priority stars, Enabled icon. Edit button on each card.  
**Pattern from:** FreeCICD `PipelineCard.razor`, BootstrapShowcase Status Cards tab.  
**Code pattern:**
```html
<div class="row row-cols-1 row-cols-md-2 row-cols-lg-3 g-3">
    @foreach (var item in pagedItems) {
        <div class="col">
            <div class="card h-100 shadow-sm border-0">
                <div class="card-header">
                    <span class="badge @GetStatusBadge(item.Status)">@item.Status</span>
                </div>
                <div class="card-body">
                    <h6>@item.Name</h6>
                    <div class="small text-muted">@item.Category</div>
                    <div>@item.Amount.ToString("C")</div>
                </div>
                <div class="card-footer">
                    <button class="btn btn-sm btn-outline-primary" @onclick="() => Edit(item)">Edit</button>
                </div>
            </div>
        </div>
    }
</div>
```

### View 3: Master-Detail Split View
**What it is:** Click a row → page splits 50/50. Left: compact list. Right: full detail panel with all fields, description, audit info. No page navigation needed.  
**Pattern from:** FreeGLBA `AccessEventsPage.razor` (the `col-lg-6` / `col-12` toggle).  
**Code pattern:**
```html
<div class="@(_selectedItem != null ? "col-lg-6" : "col-12")">
    <!-- compact table -->
</div>
@if (_selectedItem != null) {
    <div class="col-lg-6">
        <!-- detail panel -->
    </div>
}
```

### View 4: Kanban Board View
**What it is:** Columns for each Status (Draft | Active | Completed | Archived). Items shown as cards within their status column. Optionally draggable.  
**Pattern from:** Not in reference projects — new pattern. Common in project management tools.  
**Code pattern:**
```html
<div class="d-flex gap-3 overflow-auto">
    @foreach (var status in statuses) {
        <div class="kanban-column" style="min-width: 250px;">
            <h6>@status <span class="badge">@GetCount(status)</span></h6>
            @foreach (var item in items.Where(i => i.Status == status)) {
                <div class="card mb-2 shadow-sm">...</div>
            }
        </div>
    }
</div>
```

### View 5: Grouped List View
**What it is:** Items grouped by Category (or Status) with collapsible sections. Each section header shows the group name, count, and total amount. Expand/collapse with a chevron.  
**Pattern from:** FreeCICD "Group by Folder" toggle with `FolderNode` hierarchy and `_expandedGroups` HashSet.  
**Code pattern:**
```html
@foreach (var group in items.GroupBy(i => i.Category)) {
    <div class="card mb-2">
        <div class="card-header" @onclick="() => ToggleGroup(group.Key)">
            <i class="fa @(_expanded.Contains(group.Key) ? "fa-chevron-down" : "fa-chevron-right")"></i>
            @group.Key <span class="badge">@group.Count()</span>
            <span class="float-end">@group.Sum(i => i.Amount).ToString("C")</span>
        </div>
        @if (_expanded.Contains(group.Key)) {
            <div class="card-body p-0">
                <table class="table table-sm mb-0">...</table>
            </div>
        }
    </div>
}
```

### View Switcher Implementation

A dropdown at the top of the page next to the action buttons:

```html
<div class="d-flex align-items-center gap-2 mb-2">
    <label class="form-label mb-0 small text-muted">View:</label>
    <select class="form-select form-select-sm" style="width: auto;" @bind="_viewMode">
        <option value="table">📋 Table</option>
        <option value="cards">🃏 Cards</option>
        <option value="split">📖 Master-Detail</option>
        <option value="kanban">📊 Kanban Board</option>
        <option value="grouped">📁 Grouped List</option>
    </select>
</div>
```

---

## Part 4: Additional New Example Pages to Build

Based on patterns in reference projects that represent distinct, teachable concepts:

| # | Proposed Page | Primary Pattern | Reference Source | Priority | Justification |
|---|---------------|-----------------|-----------------|----------|---------------|
| E1 | **Filter Playground** | Advanced filters + filter pills + date ranges | FreeGLBA AccessEvents | 🔴 HIGH | Our current filter is basic. This would demo expandable advanced filters, active filter pill badges with (x) remove, date range pickers, and filter count badges — all patterns from FreeGLBA. |
| E2 | **Calendar Demo** | Radzen Scheduler (Day/Week/Month/Year) | FreeCRM Schedule | 🟡 MEDIUM | Calendar/scheduler is one of the most common UI needs. Shows appointment rendering, slot selection, day/week/month views, and color-coded events. |
| E3 | **PDF Viewer Demo** | PDF generation + inline viewer | FreeCRM ViewInvoice | 🟢 LOW | Server-side PDF generation (using a library), convert to images for preview, download button. Common in business apps. |
| E4 | **Dynamic Fields Demo** | UserDefinedFields + TagSelector | FreeCRM UDF + Tags | 🟡 MEDIUM | Admin configures custom fields (text, dropdown, date, checkbox) at runtime. Shows how to build forms dynamically from configuration rather than hard-coded markup. |
| E5 | **Settings/Config Demo** | Body Logging Settings pattern | FreeGLBA BodyLoggingSettings | 🟢 LOW | A settings page with warning banners, time-limited toggle configurations, confirmation dialogs, and active config countdown timers. |

---

## Part 5: Summary — Priority Matrix

### 🔴 Do First (High Impact, Demonstrates Unique Patterns)

1. **5 View Variations on Sample Items** (Part 3) — Card Grid, Master-Detail, Kanban, Grouped, plus existing Table
2. **Advanced Filter Pills** (B3) — Could be added to Sample Items or as part of Filter Playground
3. **Expandable Advanced Filters** (B2) — "More Filters" with date ranges
4. **Multi-View Toggle** (A1) — The dropdown switcher for the 5 views

### 🟡 Do Next (Valuable, Broadens Coverage)

5. **Rich Sort Dropdown** (A3) — 10+ sort options beyond just column headers
6. **Master-Detail Split View** (B1) — As View 3 above
7. **Calendar Demo** (E2) — Radzen Scheduler
8. **Group By Toggle** (A2) — As View 5 above
9. **Dynamic Fields Demo** (E4) — UDF + TagSelector
10. **Custom Pagination** (B4) — Hand-built vs PagedRecordset comparison

### 🟢 Do Later (Nice to Have)

11. **PDF Viewer Demo** (E3)
12. **Settings/Config Demo** (E5)
13. **Plugin System Demo** (C3)
14. **UndeleteMessage Demo** (D6)
15. **OffcanvasPopoutMenu Demo** (D8)

---

## Part 6: Quick Wins — Can Add to Existing Pages Now

These don't need new pages — they enhance what we already have:

| # | Enhancement | Target Page | Effort |
|---|-------------|-------------|--------|
| Q1 | Add sort dropdown (Name, Category, Status, Amount, Priority, Date — asc/desc) | Sample Items | Small |
| Q2 | Add "More Filters" expandable section with date range | Sample Items | Medium |
| Q3 | Add active filter pills with (x) dismiss | Sample Items | Medium |
| Q4 | Add view mode dropdown (table/cards/split/kanban/grouped) | Sample Items | Large |
| Q5 | Add time range selector buttons (1h/24h/7d/30d) | Charts Dashboard | Small |
| Q6 | Add relative timestamps via `Humanizer` | SignalR Demo, API Key Demo | Small |

---

*Created: 2025-06-28*  
*Status: PENDING APPROVAL*  
*Next step: Review, prioritize, then implement starting with Part 3 (5 view variations).*
