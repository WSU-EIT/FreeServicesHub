# 106 — InfoTip Placement Audit

> **Document ID:** 106  
> **Category:** Plan  
> **Purpose:** Catalog every location across all example pages where an (ⓘ) InfoTip should be added, with proposed title, description, and code snippet for each.  
> **Status:** PENDING APPROVAL — review this list, approve/modify, then implement.

---

## Legend

| Column | Meaning |
|--------|---------|
| **#** | Sequence number |
| **Page** | Which example page |
| **Location** | Where on the page the InfoTip goes |
| **Title** | The bold header in the popover |
| **Description** | Beginner-friendly explanation (shown in the popover body) |
| **Code** | Short snippet shown in the dark `<pre>` block |
| ✅ | Already exists |
| 🆕 | New — proposed |

---

## Already Existing (6 total)

| # | Page | Location | Title | Status |
|---|------|----------|-------|--------|
| — | FileDemo | Download buttons row | Download Buttons | ✅ |
| — | FileDemo | Upload card header | File Upload Area | ✅ |
| — | FileDemo | Uploaded Files card header | File Listing Table | ✅ |
| — | FileDemo | Size column header | File Size Formatting | ✅ |
| — | FileDemo | Extension badge cell | File Extension Badge | ✅ |
| — | FileDemo | Remove button cell | Remove Button | ✅ |
| — | SampleItems | After action button group | Action Buttons | ✅ |

---

## Dashboard (4 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 1 | Next to summary cards row | Summary Cards | These four cards show aggregate counts from the database. Each card displays a single number (Total, Active, Completed, Draft). The server runs a COUNT query for each status and returns the results as JSON. Blazor renders a card for each value. | `<h3 class="text-primary">@_dashboard.TotalItems</h3>` |
| 2 | Next to "Example Pages" heading | Card Grid | This grid uses Bootstrap's responsive column system. On a phone it shows 1 card per row; on a tablet, 2; on a desktop, 3. Each card links to a demo page. The `row-cols-*` classes control the layout breakpoints. | `<div class="row row-cols-1 row-cols-md-2 row-cols-lg-3 g-3">` |
| 3 | Next to "Sample Data by Category" heading | Data Table | This HTML table shows the same data the charts use. Each row is rendered by looping over the `ByCategory` list from the server. The `ToString("C")` formats numbers as currency ($1,234.56). | `@cat.TotalAmount.ToString("C")` |
| 4 | Next to a category badge on any card | Phase Badge | Badges are small colored labels. The CSS class determines the color — `bg-success` makes it green, `bg-warning` makes it yellow. They give you a quick visual status at a glance without reading text. | `<span class="badge @example.PhaseBadge">@example.Phase</span>` |

---

## Sample Items List (7 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 5 | Next to keyword search input | Keyword Search | When you type in this box and press Enter (or tab away), the page sends your search term to the server. The server searches the Name, Description, and Category fields for matches. This is called a "filter" — it narrows down the full list to only matching records. | `<input class="form-control" value="@Filter.Keyword" @onchange="KeywordChanged" />` |
| 6 | Next to Status filter dropdown | Filter Dropdowns | These dropdown lists let you narrow results by one value. When you select "Active", only Active items are shown. Selecting "All" removes the filter. The `@bind` attribute connects the dropdown to a C# variable — when you change the selection, Blazor automatically updates the variable and re-fetches data. | `<select class="form-select" @bind="Filter.Status" @bind:after="LoadFilter">` |
| 7 | Next to "Include Deleted" toggle | Toggle Switch | This is a Bootstrap "switch" — a styled checkbox that looks like a sliding toggle. When turned on, soft-deleted records (items marked as deleted but not permanently removed) appear in the list. Soft delete means the record stays in the database with a Deleted flag instead of being erased. | `<input type="checkbox" class="form-check-input" @bind="Filter.IncludeDeletedItems" />` |
| 8 | Next to "Showing 1-10 of 25 Records" text | Pagination | Pagination splits a long list into pages (like pages of a book). Instead of loading all 25 records at once, the server sends only 10 at a time. You click page numbers or Next/Previous to see more. This makes the page load faster and uses less memory. | `NavigationLocation="PagedRecordset.NavLocation.Both"` |
| 9 | Next to the page size dropdown (10 ∨) | Page Size | This dropdown controls how many rows appear per page. Choosing 25 shows more rows but takes longer to load. Choosing 10 keeps the page fast. The value is sent to the server with each request so it only returns that many records. | `Config.PageSize = 10;` |
| 10 | Next to a sortable column header (e.g. "▲ Name") | Column Sorting | Clicking a column header sorts the data by that column. The arrow (▲/▼) shows which direction: ascending (A→Z, 1→9) or descending (Z→A, 9→1). Sorting happens on the server — the full dataset is sorted before the page of results is returned. | `Config.SortField = "Name"; Config.SortDirection = "ASC";` |
| 11 | Next to any "Edit" button in the table | Edit Link | Clicking Edit navigates to the edit page for that specific record. The record's unique ID (a GUID — a long random string) is passed in the URL. The edit page uses that ID to load the full record from the server. | `NavigationManager.NavigateTo($"Examples/EditSampleItem/{item.SampleItemId}")` |

---

## Edit Sample Item (5 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 12 | Next to Save button | Save Button | Clicking Save sends the record to the server as a POST request. The server checks if the record already exists (by its ID) — if yes, it updates; if no, it creates a new one. This is the "SaveMany" pattern: the same endpoint handles both inserts and updates. | `await Helpers.GetOrPost<DataObjects.SampleItem>("api/Data/SaveSampleItems", ...)` |
| 13 | Next to DeleteConfirmation component | Two-Click Delete | To prevent accidental deletion, the Delete button requires two clicks: first click shows "Confirm Delete", second click actually deletes. This pattern is used in every project. The record is "soft deleted" — marked as deleted but not erased from the database. | `<DeleteConfirmation OnConfirmed="Delete" />` |
| 14 | Next to RequiredIndicator component | Required Fields | The red asterisk (*) next to a field label means that field must be filled in before you can save. If you try to save with an empty required field, the input box turns red (via the `is-invalid` CSS class) to show you what's missing. | `<RequiredIndicator />` |
| 15 | Next to the Name input (red border when empty) | Validation Highlight | When a required field is empty, `Helpers.MissingValue()` adds the `is-invalid` CSS class, which makes the input border turn red. This gives you instant visual feedback — you can see what needs fixing without reading error messages. | `class="form-control @Helpers.MissingValue(_item.Name)"` |
| 16 | Next to LastModifiedMessage | Last Modified | This component shows who last edited the record and when. It helps teams track changes — "Modified by John Smith on Jan 15 at 3:42 PM". The data comes from audit fields (LastModifiedBy, LastModifiedDate) that the server updates automatically on every save. | `<LastModifiedMessage DataObject="@_item" />` |

---

## Bootstrap Showcase (10 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 17 | Next to the tab strip | Tabs Navigation | Tabs organize content into panels. Only one tab panel is visible at a time. When you click a tab, Blazor updates a `_activeTab` variable and re-renders to show the matching panel. The `aria-selected` attribute tells screen readers which tab is active. | `<button class="nav-link @(_activeTab == "cards" ? "active" : "")" @onclick="@(() => _activeTab = "cards")">` |
| 18 | Next to first card's Edit/Delete buttons (Status Cards tab) | Two-Click Delete (Card) | The trash icon starts the delete. When clicked, it changes to show Cancel and Confirm buttons. You must click Confirm to actually delete. This prevents accidents — especially important on touch screens where taps can be imprecise. | `@if (_confirmDeleteId == item.SampleItemId) { <button class="btn-danger">Confirm</button> }` |
| 19 | Next to "Status Badges" heading (Badges tab) | Status Badges | A badge is a small colored label. The color communicates meaning at a glance: green = good/active, red = error/failed, yellow = warning/draft, grey = archived. The CSS class `bg-success`, `bg-danger`, etc. sets the color. Badges are used everywhere — next to names, in table cells, on cards. | `<span class="badge bg-success">Active</span>` |
| 20 | Next to "Open Dialog" button (Modals tab) | Modal Dialog | A modal is a popup window that appears on top of the page and blocks interaction with everything behind it. It focuses your attention on one task (like confirming a delete or entering information). Clicking outside or pressing Escape closes it. | `await Helpers.GetInputDialog(DialogService, "Dialog Title", "Label", "Default")` |
| 21 | Next to search-with-clear input (Forms tab) | Search with Clear | This input has a search icon on the left and a clear (X) button on the right. The X only appears when you've typed something. Clicking it empties the search box instantly. The `@bind:event="oninput"` makes it update as you type, not just when you leave the field. | `@bind="_searchTerm" @bind:event="oninput"` |
| 22 | Next to toggle switches (Forms tab) | Toggle Switches | A toggle switch is a styled checkbox that looks like a physical on/off switch. It's more intuitive than a checkbox for settings like "Enable Notifications" because the sliding action makes the on/off state visually obvious. | `<input class="form-check-input" type="checkbox" role="switch" @onchange="..." />` |
| 23 | Next to Validate button (Forms tab) | Form Validation | When you click Validate, the code checks each required field. Empty fields get a red border (`is-invalid`), and an error message appears below them with `role="alert"` so screen readers announce it. The `aria-invalid` attribute also communicates the error state programmatically. | `class="form-control @(_showFormValidation && empty ? "is-invalid" : "")"`  |
| 24 | Next to accordion section (Layout tab) | Accordion | An accordion is a list of collapsible sections — click a header to expand its content, click again to collapse. Only one section is open at a time. This saves vertical space when you have lots of content. The expand/collapse is done by toggling an `_openAccordion` variable. | `@onclick="@(() => _openAccordion = isOpen ? -1 : idx)"` |
| 25 | Next to Cards/Table toggle buttons (Layout tab) | View Toggle | These buttons let you switch between two ways of viewing the same data: as visual cards or as a compact table. The `_viewMode` variable controls which is rendered. Both views show the same data — just laid out differently. Card view is scannable; table view is dense and sortable. | `<button class="btn @(_viewMode == "card" ? "btn-primary" : "btn-outline-primary")" @onclick="...">` |
| 26 | Next to toast notification buttons (Feedback tab) | Toast Notifications | A toast is a brief message that pops up at the top of the page and fades away after a few seconds (like bread popping out of a toaster). It confirms that an action worked ("Saved!") or warns about a problem. The `Model.AddMessage()` method triggers the toast from anywhere in code. | `Model.AddMessage("Saved!", MessageType.Success)` |

---

## Charts Dashboard (3 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 27 | Next to pie chart card header | Pie Chart | This component renders a Highcharts pie chart. You pass it an array of name/value pairs, and it draws proportional slices. The chart is interactive — hover shows tooltips, clicking a slice fires the `OnItemClicked` callback so C# can respond to the click. | `<Highcharts ChartType="Highcharts.ChartTypes.Pie" SeriesDataItems="@_pieData" OnItemClicked="OnPieClicked" />` |
| 28 | Next to column chart card header | Column Chart | A column (bar) chart compares values side by side. Each bar's height represents a number. This chart shows two data series (Amount and Count) grouped by category. The `yAxisText` label describes what the vertical axis measures. | `<Highcharts ChartType="Highcharts.ChartTypes.Column" SeriesDataArrayItems="@_columnData" />` |
| 29 | Next to the drill-down toast message (when pie is clicked) | Drill-Down | When you click a pie slice, the chart fires a callback with the index of the clicked slice. The C# code looks up that index to find which status was clicked and shows a message. In a real app, this would navigate to a filtered list showing only items with that status. | `void OnPieClicked(int index) { Model.AddMessage($"Clicked: {_pieData[index].name}"); }` |

---

## Code Editor (4 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 30 | Next to Language dropdown | Language Selector | This dropdown changes the programming language the editor understands. When you switch from HTML to C#, the editor re-colors the text using C# syntax rules — keywords like `class` turn blue, strings turn red, comments turn green. The language is passed to Monaco as a parameter. | `<select @bind="_language" @bind:after="OnLanguageChanged">` |
| 31 | Next to "Insert Snippet" button | Insert at Cursor | This button inserts pre-written code at wherever your cursor is positioned in the editor. It demonstrates that C# can programmatically control the editor's content and cursor position via JavaScript interop — C# tells JavaScript "insert this text at the cursor". | `await _editor.InsertAtCursor("snippet text")` |
| 32 | Next to the MonacoEditor component | Monaco Editor Component | This is the Monaco code editor — the same engine that powers VS Code — running in a web page. It's a Blazor component that wraps a JavaScript library. The `@bind-Value` attribute creates two-way binding: changes you type are sent to C#, and changes from C# update the editor. | `<MonacoEditor @ref="_editor" Language="@_language" @bind-Value="_code" MinHeight="400px" />` |
| 33 | Next to "Current Value" pre block | Live Binding Display | This `<pre>` block shows the exact same text that's in the editor above — in real time. As you type, Blazor's two-way binding updates the `_code` variable, and this block re-renders. This proves the binding works and demonstrates how to display raw content from a component. | `<pre>@_code</pre>` |

---

## SignalR Demo (7 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 34 | Next to "Active Users" card header | Active Users Presence | This list shows who is online right now. The server tracks each user's last activity time. Users active in the last 30 seconds show as "Active" (green); those quiet for 30s–5min show as "Away" (yellow). After 5 minutes they disappear. The "You" badge marks your own entry. | `Model.ActiveUsers.Where(x => x.LastAccess > DateTime.UtcNow.AddMinutes(-5))` |
| 35 | Next to "online" badge count | Online Count Badge | This green pill-shaped badge shows how many users are currently online. It recalculates every time SignalR delivers an update. The `rounded-pill` class makes the badge oval-shaped. The count comes from filtering the active users list by last access time. | `<span class="badge bg-success rounded-pill">@onlineCount online</span>` |
| 36 | Next to Quick Add Save button | Quick Add Form | This mini-form saves a new item and broadcasts the save to all connected browser tabs via SignalR. The Enter key also triggers save (via `@onkeydown`). After saving, every browser tab receives a SignalR update and can choose to refresh its data. | `@onkeydown="@(async (e) => { if (e.Key == "Enter") await SaveItem(); })"` |
| 37 | Next to Activity Feed header | Activity Feed | This scrollable list shows every SignalR event in real time — saves, deletes, and data changes. Each entry has a colored icon (green disk = saved, red trash = deleted), a description, and a millisecond-precision timestamp. New events appear at the top. | `<div class="small">@entry.Message</div>` |
| 38 | Next to "pending" badge in Live Items header | Pending Updates Badge | When `Auto-apply` is off, incoming SignalR updates are queued instead of applied immediately. This yellow badge pulses to get your attention, showing how many updates are waiting. Click "Apply" to merge them into the table. This pattern prevents jarring table refreshes while you're reading. | `<span class="badge bg-warning animate__pulse">@_pendingUpdates pending</span>` |
| 39 | Next to "Auto-apply" toggle switch | Auto-Apply Toggle | When ON, every SignalR update is applied to the table immediately — rows appear, update, or disappear in real time. When OFF, updates queue up and the pending badge shows the count. You decide when to apply them. This gives users control over when their view changes. | `<input type="checkbox" @bind="_autoApply" />` |
| 40 | Next to bolt icon on a recently-updated row | Row Highlight | When a row receives a SignalR update, it briefly turns light blue (`table-info`) and shows a bolt icon. This visual flash draws your eye to what changed. The highlight fades after a few seconds. The `_recentlyUpdatedIds` set tracks which rows were just updated. | `class="@(justUpdated ? "table-info" : "")"` |

---

## Timer Demo (4 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 41 | Next to countdown number display | Countdown Timer | This large number counts down from 60 to 0, then resets. A C# `Timer` fires every 1 second, decrements the counter, and tells Blazor to re-render. At 10 seconds or less, the progress bar turns red (`bg-danger`) to signal urgency. This is the exact pattern used for TOTP codes in the SSO app. | `timer.Elapsed += (s, e) => { _secondsRemaining--; InvokeAsync(StateHasChanged); }` |
| 42 | Next to progress bar | Progress Bar | The progress bar width is calculated as a percentage: `(secondsRemaining / 60) * 100`. CSS handles the visual — `style="width:83%"` fills 83% of the bar. The color class changes dynamically: blue normally, red when ≤10 seconds. `aria-valuenow` keeps screen readers updated. | `style="width:@(pct)%"` |
| 43 | Next to debounce input | Debounce Input | Debouncing means "wait until the user stops typing before doing something." Without debounce, every keystroke would trigger a server request. With a 500ms debounce, the code waits half a second after the last keystroke before processing. This saves server resources and feels smoother. | `Helpers.SetTimeout(async () => { _debouncedValue = text; }, 500)` |
| 44 | Next to auto-refresh toggle | Auto-Refresh | This toggle starts or stops a repeating timer. When enabled, the page fetches fresh data from the server every 10 seconds automatically. When disabled, data only updates when you manually click Refresh. The `IDisposable` pattern ensures the timer is stopped when you leave the page. | `<input type="checkbox" @bind="_autoRefreshEnabled" @bind:after="ToggleAutoRefresh" />` |

---

## Network Graph (3 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 45 | Next to solver buttons (Repulsion/Barnes-Hut) | Physics Solvers | The graph uses physics simulation to arrange nodes. "Repulsion" makes every node push away from every other node (like magnets repelling). "Barnes-Hut" is a faster algorithm that approximates the same effect for large graphs. Switching between them shows different layout results. | `NetworkSolver="@_solver"` |
| 46 | Next to NetworkChart component | Network Chart Component | This component wraps the vis.js JavaScript library. C# builds arrays of nodes and edges (connections), passes them to JavaScript, and vis.js draws the interactive diagram. You can drag nodes, zoom in/out, and click elements. When you click, JavaScript calls back into C# via `DotNetObjectReference`. | `<NetworkChart Nodes="_nodes" Relationships="_relationships" OnElementSelected="OnNodeSelected" />` |
| 47 | Next to "X nodes, Y edges" text | Node and Edge Counts | Nodes are the circles in the graph — each represents a category or item. Edges are the lines connecting them — each represents a relationship. The count updates when data loads. In a real app (like DependencyManager), nodes would be servers, apps, or databases. | `@_nodes.Count nodes, @_relationships.Count edges` |

---

## Signature Demo (3 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 48 | Next to Signature Pad component | Signature Pad Component | This component wraps jSignature, a JavaScript library that creates a drawing surface (HTML canvas). Draw with your mouse or finger. The library captures your strokes and converts them to a compact text format called base30. The `@bind-Value` sends that text back to C# automatically. | `<Signature @ref="_signaturePad" @bind-Value="_signatureValue" />` |
| 49 | Next to Clear button | Clear Signature | This button erases the signature pad and resets the bound value to empty. It's disabled (greyed out) when there's nothing to clear — the `disabled` attribute checks if the value is empty. This prevents a confusing "nothing happened" experience. | `disabled="@(string.IsNullOrWhiteSpace(_signatureValue))"` |
| 50 | Next to "Raw Value (base30)" card | Raw Data Display | This shows the actual data that gets saved to the database — a string of characters that encodes your drawing. Base30 is more compact than storing the image pixels. The signature can be reconstructed from this string later. In a real app, this string goes into a database column. | `<pre>@_signatureValue</pre>` |

---

## Wizard Demo (5 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 51 | Next to WizardStepper component | Wizard Stepper | The numbered circles at the top show your progress through the wizard. Completed steps are clickable — you can jump back to review or change answers. The current step is highlighted. Connector lines between circles show the flow. Each circle can also display your selection as a preview. | `<WizardStepper Steps="@GetStepperInfo()" CurrentStep="@_currentStep" OnStepClick="@GoToStep" />` |
| 52 | Next to WizardSummary component | Selection Summary | These colored badge pills show what you chose in previous steps — like breadcrumbs of your decisions. "Category: Engineering" and "Priority: 3" remind you what you picked without going back. They appear between the stepper and the current step content. | `<WizardSummary Selections="@GetSelections()" />` |
| 53 | Next to WizardStepHeader (Back/Next) | Step Navigation | Back and Next buttons control movement through steps. Next is disabled (greyed out) until required fields are filled. The `NextDisabled` parameter is calculated from validation — for example, `string.IsNullOrWhiteSpace(_selectedCategory)` prevents advancing without picking a category. | `NextDisabled="@(string.IsNullOrWhiteSpace(_selectedCategory))"` |
| 54 | Next to the required field (*) in Step 1 | Required Fields | The red asterisk means this field must be filled in. If you click Next without selecting a value, the field turns red with an error message. The `_attemptedNext` flag tracks whether you've tried to advance — validation errors only show after your first attempt so the form doesn't start angry. | `aria-required="true" aria-invalid="@(_attemptedNext && empty)"` |
| 55 | Next to Review table (Step 4) | Review Step | The final step shows all your choices in a summary table before submitting. This is a common UX pattern — let users verify everything before committing. The Finish button replaces Next, signaling that this is the last step. No data is saved until you click Finish. | `<WizardStepHeader ShowFinish="true" OnFinish="@Finish" />` |

---

## Git Browser (4 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 56 | Next to repo URL input | Repository URL Input | Paste the URL of any public Git repository here (e.g., from GitHub). When you click Browse, the server downloads just the latest snapshot (depth=1, no history) into memory, reads all files, then deletes the temp clone. Everything is served from memory after that. | `git clone --depth 1 --single-branch --no-tags` |
| 57 | Next to breadcrumb navigation | Breadcrumb Navigation | This clickable path trail shows where you are in the repository folder tree. Each segment is a folder name — click any segment to jump back to that folder. "root" takes you to the top level. The breadcrumb is built by splitting the current path on "/" and creating a link for each segment. | `<nav aria-label="Repository path"><ol class="breadcrumb">...</ol></nav>` |
| 58 | Next to directory listing header | Directory Listing | This list shows the folders and files in the current directory — folders first (with folder icons), then files (with type-specific icons). Click a folder to navigate into it; click a file to view its contents. The ".." entry at the top takes you up one directory level. | `@foreach (var entry in _entries) { <button @onclick="OnEntryClicked(entry)"> }` |
| 59 | Next to Monaco editor in file viewer | File Viewer | When you click a file, its contents load into a read-only Monaco editor with syntax highlighting. The editor language is auto-detected from the file extension — .cs files get C# coloring, .js gets JavaScript, etc. Binary files (images, compiled programs) show a placeholder message instead. | `<MonacoEditor Language="@_monacoLanguage" Value="@_viewingFile.Content" ReadOnly="true" />` |

---

## API Key Demo (7 new)

| # | Location | Title | Description | Code |
|---|----------|-------|-------------|------|
| 60 | Next to Generate button | Generate Key | Clicking Generate creates a cryptographically random 32-byte key. The server stores only the SHA-256 hash (a one-way scramble) — never the raw key. The plaintext key is shown once in the yellow warning box. If you lose it, you must generate a new one. This is how real API keys work (GitHub, Stripe, etc.). | `var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))` |
| 61 | Next to "Copy this key now" warning box | One-Time Key Display | This yellow warning box shows the raw API key exactly once. After you navigate away or generate another key, it disappears forever. The Copy button puts the key on your clipboard. This pattern mimics how GitHub Personal Access Tokens work — shown once, stored hashed. | `<code>@_lastGeneratedKey</code>` |
| 62 | Next to Copy button | Copy to Clipboard | This button uses the browser's Clipboard API to copy the key to your clipboard. One click — the text is copied without selecting it manually. In code, `navigator.clipboard.writeText()` is called via JavaScript interop. A success toast confirms the copy. | `await Helpers.CopyToClipboard(text)` |
| 63 | Next to Revoke button on a key row | Revoke Key | Revoking a key marks it as inactive in the database. Any future requests using that key will be rejected with a 401 error. The key is not deleted — it stays in the list as "Revoked" (red badge) for audit purposes. This is safer than deleting because you keep a record of what existed. | `<button @onclick="@(() => RevokeKey(key.ApiKeyId))">` |
| 64 | Next to Test Console header | Test Console | This panel lets you send real HTTP requests to the protected API endpoint. You choose a method (GET or POST), paste an API key into the Bearer Token field, and click Send. The response shows the status code (200 = success, 401 = unauthorized) and the response body. It's like Postman built into the page. | `Authorization: Bearer <your-key>` |
| 65 | Next to "Send Without Key" / "Send Bad Key" buttons | Failure Test Buttons | These buttons intentionally send invalid requests so you can see what happens. "Send Without Key" omits the Authorization header entirely — the middleware returns 401. "Send Bad Key" sends a random string — the hash won't match any stored key, so it also returns 401. This demonstrates the security in action. | `<button @onclick="SendWithoutKey">Send Without Key</button>` |
| 66 | Next to Request Log header | Request Log | Every API request is logged here with timestamp, HTTP method, which key was used, the response status code, and a detail message. Green rows = successful (200). Red rows = unauthorized (401). This audit log is essential for security — in production you'd use this to detect suspicious activity. | `<table class="table" aria-label="API request log">` |

---

## Summary

| Page | Existing | New | Total |
|------|----------|-----|-------|
| Dashboard | 0 | 4 | 4 |
| Sample Items (list) | 1 | 7 | 8 |
| Edit Sample Item | 0 | 5 | 5 |
| FileDemo | 6 | 0 | 6 |
| Bootstrap Showcase | 0 | 10 | 10 |
| Charts Dashboard | 0 | 3 | 3 |
| Code Editor | 0 | 4 | 4 |
| SignalR Demo | 0 | 7 | 7 |
| Timer Demo | 0 | 4 | 4 |
| Network Graph | 0 | 3 | 3 |
| Signature Demo | 0 | 3 | 3 |
| Wizard Demo | 0 | 5 | 5 |
| Git Browser | 0 | 4 | 4 |
| API Key Demo | 0 | 7 | 7 |
| **TOTAL** | **7** | **66** | **73** |

---

*Created: 2026-02-28*  
*Status: PENDING APPROVAL*  
*Next step: Review this list. Approve, remove, or modify entries. Then implementation begins.*
