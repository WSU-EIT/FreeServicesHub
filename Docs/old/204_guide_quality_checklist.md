# 204 — Guide: Quality Checklist and Required Patterns

> **Document ID:** 204
> **Category:** Guide
> **Purpose:** Quality standards, required framework patterns, and PR acceptance criteria for FreeServicesHub.
> **Audience:** All developers, AI agents.
> **Outcome:** Consistent, framework-compliant code that passes review.

**Scope:** This checklist covers FreeServicesHub-specific quality gates. For base FreeCRM patterns, see `Examples/FreeTools/FreeExamples/Docs/`.

---

## 1. Required Framework Helpers (MUST Use, Never Reinvent)

If a Helper exists for it, use it. Rolling your own version is a review blocker.

### Navigation

| Helper | Purpose |
|--------|---------|
| `Helpers.NavigateTo("Page")` | Programmatic navigation (auto-prepends tenant URL) |
| `Helpers.NavigateToRoot()` | Navigate to home page |
| `Helpers.BuildUrl("Page")` | Generate URL string for `href` attributes |
| `Helpers.ValidateUrl(TenantCode)` | Validate tenant URL in `OnAfterRenderAsync` |

### Text and Localization

| Helper | Context |
|--------|---------|
| `<Language Tag="..." />` | Razor markup — all user-visible text |
| `<Language Tag="..." IncludeIcon="true" />` | Razor markup — text with icon |
| `Helpers.Text("Tag")` | C# code — validation messages, dynamic strings |
| `Helpers.MissingRequiredField("FieldName")` | Validation error message for a missing field |
| `Helpers.ConfirmButtonTextCancel` | Localized "Cancel" for delete dialogs |
| `Helpers.ConfirmButtonTextDelete` | Localized "Delete" for delete dialogs |
| `Helpers.ConfirmButtonTextConfirmDelete` | Localized "Confirm Delete" for delete dialogs |

### HTTP

| Helper | Purpose |
|--------|---------|
| `Helpers.GetOrPost<T>("api/endpoint")` | GET request — auto-adds TenantId, Token, Fingerprint |
| `Helpers.GetOrPost<T>("api/endpoint", data)` | POST request — same auto-headers |

Never use `HttpClient` directly. `GetOrPost<T>()` handles auth headers, JSON, and errors.

### Validation

| Helper | Purpose |
|--------|---------|
| `Helpers.MissingValue(value, "form-control")` | CSS class to highlight empty required fields |
| `Helpers.MissingRequiredField("Field")` | Localized error message for missing field |
| `Helpers.DelayedFocus("element-id")` | Focus first invalid field after validation |

**Full validation pattern:** `List<string> errors` + `string focus` + check fields + `Model.ErrorMessages(errors)` + `Helpers.DelayedFocus(focus)`.

### UI Components

| Component | Purpose |
|-----------|---------|
| `<Language Tag="..." IncludeIcon="true" />` | Localized text with icon |
| `<Icon Name="..." />` | Standalone icon |
| `<DeleteConfirmation>` | Two-step delete confirmation dialog |
| `<LoadingMessage />` | Loading spinner |
| `<LastModifiedMessage>` | Last modified timestamp display |
| `<UndeleteMessage>` | Undelete prompt for soft-deleted records |

### Data

| Helper | Purpose |
|--------|---------|
| `Helpers.SerializeObject(obj)` | Object to JSON |
| `Helpers.DeserializeObject<T>(json)` | JSON to object |
| `Helpers.DuplicateObject<T>(obj)` | Deep clone via serialize/deserialize |
| `Helpers.StringValue(str)` | Null-safe string (returns `""` not `null`) |
| `Helpers.GuidValue(guid)` | Null-safe GUID (returns `Guid.Empty` not `null`) |

### Formatting

| Helper | Purpose |
|--------|---------|
| `Helpers.FormatDateTime(date)` | Format date and time |
| `Helpers.FormatDate(date)` | Format date only |
| `Helpers.FormatCurrency(value)` | Format currency value |
| `Helpers.BooleanToIcon(value)` | Boolean to checkmark/X icon |
| `Helpers.BytesToFileSizeLabel(bytes)` | Bytes to "1.5mb" |

### Focus

| Helper | Purpose |
|--------|---------|
| `Helpers.DelayedFocus("id")` | Focus element after DOM renders |
| `Helpers.DelayedSelect("id")` | Focus and select all text in element |

### Model State

| Helper | Purpose |
|--------|---------|
| `Model.ClearMessages()` | Clear all messages before new operation |
| `Model.Message_Saving()` | Show "Saving..." message |
| `Model.Message_Deleting()` | Show "Deleting..." message |
| `Model.ErrorMessages(errors)` | Display error message list |
| `Model.UnknownError()` | Display generic error on null API response |
| `Model.View` | Current view identifier |
| `Model.Loaded` | Whether the app is loaded |
| `Model.LoggedIn` | Whether user is authenticated |
| `Model.StickyMenuClass` | CSS class for sticky menu |

---

## 2. Required Patterns

### Three-Endpoint CRUD (All `[HttpPost]`)

| Endpoint | Input | Behavior |
|----------|-------|----------|
| `GetMany` | `List<Guid>?` | `null`/empty = all; IDs = filtered |
| `SaveMany` | `List<T>` | PK exists = update; empty/new PK = insert |
| `DeleteMany` | `List<Guid>` | Must provide IDs; `null`/empty = error |

### File Naming

All new files: `{ProjectName}.App.{Feature}.{Extension}`

Blazor dot-to-underscore rule:
- File: `FreeServicesHub.App.AgentDashboard.razor`
- Class: `FreeServicesHub_App_AgentDashboard`
- Usage: `<FreeServicesHub_App_AgentDashboard />`

### Page Lifecycle

1. **OnInitialized** — Set `Model.View`, subscribe `OnChange` (check `Model.Subscribers_OnChange.Contains(_pageName)` first)
2. **OnAfterRenderAsync** — `ValidateUrl`, check `Model.Loaded && Model.LoggedIn`, load data
3. **Dispose** — Unsubscribe from `OnChange`

### Save Pattern

```
ClearMessages -> validate (errors + focus) -> Message_Saving
-> GetOrPost -> ClearMessages -> handle result or UnknownError
```

### Delete Pattern

```
ClearMessages -> Message_Deleting -> GetOrPost
-> ClearMessages -> NavigateTo on success or ErrorMessages on failure
```

### SignalR Subscription

Always check before subscribing:
```csharp
if (!Model.Subscribers_OnChange.Contains(_pageName)) {
    Model.Subscribers_OnChange.Add(_pageName);
    Model.OnChange += StateHasChanged;
}
```

### Comment Style

"Calm, experienced developer" voice. Ten core patterns:

| Pattern | Starter |
|---------|---------|
| Sequencing | `// First,` `// Now,` `// Next,` |
| Conditional check | `// See if...` |
| Validation | `// Make sure...` |
| Branching | `// If the..., then...` |
| Context | `// This is a...` |
| File header | `// Use this file as a place to...` |
| Constraint | `// Only...` |
| State transition | `// At this point...` |
| Result state | `// Valid...` `// Still...` |
| Action | `// Remove...` `// Delete...` |

See `005_style.comments.md` for full details.

### File Size

| Threshold | Lines | Action |
|-----------|-------|--------|
| Target | <=300 | Ideal |
| Soft max | 500 | Consider splitting |
| Hard max | 600 | Must split or justify |

---

## 3. Required Accessibility Patterns

- `aria-label` on all interactive elements (buttons, links, inputs)
- `aria-hidden="true"` on decorative icons (icons that duplicate adjacent text)
- `role="alert"` on error/success messages
- `aria-live="polite"` on dynamic content regions (dashboards, status updates)
- Keyboard navigable — logical tab order, no focus traps
- Color is never the sole indicator — always pair with icon or text (e.g., status cards use color + icon + label)
- `<Language Tag="..." Required="true" />` on required field labels

---

## 4. PR Checklist

Every PR must pass these gates before merge:

### Naming and Structure
- [ ] All new files use `{ProjectName}.App.{Feature}` naming
- [ ] No framework files modified (only `.App.` hook files with one-line tie-ins)
- [ ] File under 600 lines (or justified split plan attached)

### Framework Compliance
- [ ] Uses `Helpers.NavigateTo()` (not `NavManager.NavigateTo()`)
- [ ] Uses `Helpers.GetOrPost<T>()` (not `HttpClient` directly)
- [ ] Uses `Helpers.SerializeObject/DeserializeObject` (not `JsonSerializer` directly)
- [ ] Three-endpoint CRUD pattern for new entities (GetMany/SaveMany/DeleteMany)

### Localization and Validation
- [ ] `<Language>` for all user-visible text (no hardcoded strings in UI)
- [ ] Validation uses `MissingValue` + `MissingRequiredField` + `DelayedFocus`
- [ ] Save/Delete patterns follow ClearMessages -> action -> ClearMessages -> handle

### UX and Accessibility
- [ ] `AboutSection` on new pages explaining what the page does
- [ ] `InfoTip` on non-obvious UI elements
- [ ] `aria-label` on all interactive elements
- [ ] Color paired with icon or text (never color alone)

### Code Quality
- [ ] Comment voice matches `005_style.comments.md` patterns
- [ ] Explicit types (not `var`) per project convention
- [ ] Method parameters use PascalCase (not camelCase)
- [ ] Private fields use `_camelCase` prefix

### Documentation
- [ ] Quickstart still works (or updated)
- [ ] New config keys documented in appsettings
- [ ] New behavior has docs or is covered by existing docs

---

## 5. Common Mistakes (Don't Do This)

| Mistake | Correct Approach |
|---------|-----------------|
| `NavManager.NavigateTo("page")` | `Helpers.NavigateTo("page")` |
| `await Http.GetFromJsonAsync<T>(url)` | `await Helpers.GetOrPost<T>(url)` |
| `JsonSerializer.Serialize(obj)` | `Helpers.SerializeObject(obj)` |
| `var result = ...` | `DataObjects.Tag result = ...` (explicit type) |
| `public void Save(string userId)` | `public void Save(string UserId)` (PascalCase params) |
| Flat DTO class in random file | Nested class inside `DataObjects` partial class |
| Individual GET/POST/PUT/DELETE endpoints | Three-endpoint CRUD (GetMany/SaveMany/DeleteMany) |
| Hardcoded `"Save"` in button text | `<Language Tag="Save" IncludeIcon="true" />` |
| `if (name == null \|\| name == "")` | `if (String.IsNullOrWhiteSpace(name))` |
| Modifying base framework files | Create `.App.` extension with one-line hook |
| `new Guid()` for null check | `Helpers.GuidValue(guid) != Guid.Empty` |
| Manual JSON in SignalR handlers | `Helpers.DeserializeObject<T>(update.ObjectAsString)` |
| Subscribing without guard | Check `Model.Subscribers_OnChange.Contains(_pageName)` first |

---

*Created: 2026-04-08*
*Maintained by: [Quality]*
