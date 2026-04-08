# 109 — Example Pages Deep Dive & What's Next

> **Document ID:** 109  
> **Category:** Analysis  
> **Supersedes:** 108_web_principles_gap_analysis.md (still valid, this is the actionable follow-up)

---

## Part 1: Every Existing Page — What It Teaches

| # | Page (lines) | Patterns Demonstrated |
|---|---|---|
| 1 | **Dashboard** (204) | Summary stat cards, card grid layout, data table, responsive columns, API GET |
| 2 | **Sample Items** (431) | Server-side filter/sort/page (PagedRecordset), keyword search, category/status/enabled dropdowns, CSV export, SignalR row updates, column sorting |
| 3 | **Edit Sample Item** (191) | Navigate-to-edit, form inputs (text/select/number/textarea/switch), required field validation (red border), create vs. update detection, LastModified audit stamp, two-click delete |
| 4 | **File Demo** (216) | File upload (drag-drop, multi-file), file download (text + CSV), file list table, file size formatting, delete uploaded file |
| 5 | **Bootstrap Showcase** (643) | Tabs, status cards, badges, modals (Radzen dialog), forms & inputs (text/email/dropdown/checkbox/switch/range), accordion, layout patterns (columns/grid), alerts/toasts, two-click delete in cards |
| 6 | **Charts Dashboard** (172) | Highcharts pie chart, column chart, click drill-down, chart↔table data sharing |
| 7 | **Code Editor** (147) | Monaco single editor, language switching, insert-at-cursor, two-way binding, live value display |
| 8 | **SignalR Demo** (437) | Real-time SignalR updates, activity log, online user presence, pending update queue, auto-apply toggle, quick-add form with Enter key, row highlight on update |
| 9 | **Timer Demo** (212) | Countdown timer, progress bar (dynamic width + color), debounce input, auto-refresh with interval, timer cleanup on dispose |
| 10 | **Network Graph** (213) | vis.js network visualization, node/edge data from API, interactive graph (zoom/drag/select), physics simulation |
| 11 | **Signature Demo** (144) | jSignature digital signature capture, canvas interaction, base30 data format, clear/save |
| 12 | **Wizard Demo** (330) | Multi-step wizard, step validation, progress stepper, step navigation (back/next), summary review, completion state |
| 13 | **Git Browser** (416) | Repo URL input, server-side git clone (LibGit2Sharp), directory tree browsing, breadcrumb navigation, file viewing in Monaco (read-only, auto-language), binary file detection, progress messages |
| 14 | **API Key Demo** (426) | API key generation, SHA-256 hashing, one-time secret display, key listing, key revocation, test console (live API calls with Bearer token), request/response display, copy-to-clipboard |
| 15 | **Kanban Board** (425) | HTML5 drag-and-drop (native, no library), column-based layout, optimistic update with revert, drop zone highlighting, card animation, move history audit, category filter, search, SignalR sync |

---

## Part 2: What's NOT Demonstrated (Across All 15 Pages)

### Monaco Editor Capabilities We Own But Don't Demo

Our `MonacoEditor.razor` component (511 lines) has these features that the Code Editor page **never uses**:

| Feature | Component Support | Code Editor Demo? | GitBrowser? |
|---------|-------------------|-------------------|-------------|
| Single editor | ✅ `StandaloneCodeEditor` | ✅ Yes | ✅ Yes (read-only) |
| Language switching at runtime | ✅ `SetLanguage()` | ✅ Yes | ✅ Auto-detect |
| Two-way binding (`@bind-Value`) | ✅ `ValueChanged` event | ✅ Yes | ❌ Read-only |
| Insert at cursor | ✅ `InsertValue()` | ✅ Yes | ❌ |
| **Diff editor (side-by-side)** | ✅ `StandaloneDiffEditor` | ❌ **Never shown** | ❌ |
| **Diff inline mode** | ✅ `UseInlineViewWhenSpaceIsLimited` | ❌ **Never shown** | ❌ |
| **Multiple editors on one page** | ✅ Via separate `@ref` | ❌ Only 1 editor | ❌ |
| **Debounced value change** | ✅ Built-in `Timeout` param (default 1000ms) | ❌ **Never explained** | ❌ |
| **Read-only toggle at runtime** | ✅ `ReadOnly` param + `UpdateOptions` | ❌ | ✅ Always read-only |
| **Custom constructor options** | ✅ `ConstructorOptions` param | ❌ Uses defaults | ❌ |
| **Get/set value programmatically** | ✅ `GetValue()` / `SetValue()` | ❌ Only insert | ❌ |
| **Cursor position tracking** | ✅ `EditorCursorPosition` | ❌ | ❌ |
| **Editor focus** | ✅ `Focus()` | ❌ | ❌ |
| **Selection retrieval** | ✅ `GetEditorSelection()` | ❌ | ❌ |
| **Minimap (autohide on hover)** | ✅ Built-in config | ❌ Not discussed | ❌ |
| **Mouse wheel zoom** | ✅ Built-in config | ❌ Not discussed | ❌ |
| **Render whitespace** | ✅ Built-in config | ❌ Not discussed | ❌ |
| **Placeholder text** | ✅ `PlaceholderText` param | ❌ | ❌ |

**Summary: We use 4 of 18 Monaco features.** The diff editor, multiple editors, debounced auto-save, and cursor tracking are completely undemoed.

### Patterns from Timer Demo Not Combined with Monaco

The Timer Demo teaches debounce as an isolated concept. The Monaco component already HAS a built-in debounce timer (`Timeout` parameter). But we never show:

- **Auto-save to server**: Editor content changes → debounce → POST to API → show "Saved" indicator
- **Save status indicator**: "Saving..." / "Saved ✓" / "Error ✗" states
- **Version history**: Save snapshots, browse previous versions, diff against them

### Other Missing Patterns

| Category | Gap | Notes |
|----------|-----|-------|
| **Multiple editors on one page** | No page puts 2+ editors side-by-side | Real apps often have input editor + output preview, or HTML + CSS + JS panels |
| **Editor ↔ API round-trip** | Code Editor has no server interaction | Content is entirely client-side; never demonstrates saving/loading from API |
| **Diff viewer** | Component supports it, never used | The most asked-for Monaco feature after basic editing |
| **Output/preview panel** | Nothing shows the result of code | HTML editor → rendered preview, JSON editor → parsed tree, etc. |
| **Template/snippet library** | Only "Insert Snippet" button with hardcoded text | No concept of choosing from multiple templates |
| **Multi-language split** | Can only view one language at a time | Real apps often edit HTML + CSS + JS simultaneously |

---

## Part 3: The "Code Playground" Page Design

### Concept
A page with **multiple Monaco editors** that interact with each other and the server. Think: CodePen/JSFiddle meets our API patterns. Demonstrates every Monaco feature we own plus the auto-save-to-server pattern.

### Tabs / Sections

**Tab 1: Multi-Editor Playground**
- 3 editors side-by-side: HTML, CSS, JavaScript
- Live preview panel below renders the combined output in an iframe
- Each editor has its own language locked
- Type in any editor → preview updates automatically (debounced)
- Shows: multiple editors, per-editor language, debounced rendering

**Tab 2: Diff Viewer**
- Two textareas or editors showing "original" and "modified"
- Toggle button: Side-by-side diff vs. inline diff
- Load sample diffs (code changes, config changes)
- Shows: `ValueToDiff` parameter, `StandaloneDiffEditor`, diff mode toggle

**Tab 3: API Notebook**
- Single editor for writing JSON payloads
- Dropdown to pick an API endpoint (GetSampleItems, SaveSampleItems, GetSampleDashboard)
- "Send" button posts the editor content to the selected endpoint
- Response displayed in a second editor (read-only, JSON language)
- Save/load snippets with auto-save indicator
- Shows: two editors (request/response), API interaction, debounced auto-save, read-only mode, cursor position, save status

### Features to Demonstrate

| Feature | How |
|---------|-----|
| Multiple editors on one page | 3 side-by-side in Tab 1, 2 in Tab 3 |
| Diff editor (side-by-side + inline) | Full tab dedicated to diff |
| Debounced auto-save | Tab 3 saves snippet to server after 2s delay |
| Save status indicator | "Saving..." → "Saved ✓" with timestamp |
| Read-only editor | Response viewer in Tab 3 |
| Language switching | Tab 3 changes response language based on content |
| Cursor position display | Status bar shows "Ln X, Col Y" |
| Placeholder text | Empty editors show instructional placeholder |
| Template/snippet library | Dropdown with pre-built templates per tab |
| API round-trip | Type JSON → POST → see response |
| Live preview | HTML+CSS+JS → rendered output |
| Get/Set value programmatically | Template loading calls `SetValue()` |
| Custom constructor options | Different config per editor (line numbers, minimap) |

---

## Part 4: Implementation Plan

### New Files
1. `DataObjects.CodeSnippet` — simple DTO: Id, TenantId, Title, Content, Language, LastSaved
2. API endpoint: `SaveCodeSnippet` / `GetCodeSnippets` (follows three-endpoint pattern)
3. Data access methods for snippet CRUD
4. `FreeExamples.App.Pages.CodePlayground.razor` — the page
5. CSS additions to `site.App.css`

### Server-Side (keeps it simple)
- `CodeSnippet` doesn't need an EF model/migration — we can use the existing `SampleItem.Description` field as a vehicle, or add a lightweight in-memory cache. For the demo, saving to the SampleItem description field on an existing item works fine. But a cleaner approach: add a simple `CodeSnippet` class and store it in the in-memory dictionary (like the ApiKey demo does).
- One `SaveCodeSnippet` endpoint that accepts `{ title, content, language }` and returns the saved snippet with timestamp
- One `GetCodeSnippets` endpoint that returns all saved snippets for the tenant

---

*Created: 2025-06-28*  
*Status: IMPLEMENTING*
