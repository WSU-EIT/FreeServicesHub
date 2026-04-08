# 102 — Research: Complete .App. Hook File Inventory

> **Document ID:** 102
> **Category:** Research
> **Purpose:** Catalog every `.App.` hook file that ships with the base FreeCRM framework.
> **Audience:** Devs extending FreeCRM, AI agents, doc writers.
> **Outcome:** 📋 Authoritative list of all 29 hook files, their methods, and their purposes.

**Source:** `ReferenceProjects/FreeCRM-main/` — all files matching `*.App.*` excluding `obj/` and `bin/`.

---

## Summary

| Project | Count | Files |
|---------|-------|-------|
| Server (`CRM/`) | 2 C# + 1 Razor | `Program.App.cs`, `DataController.App.cs`, `Modules.App.razor` |
| DataAccess (`CRM.DataAccess/`) | 4 C# | `DataAccess.App.cs`, `GraphAPI.App.cs`, `Utilities.App.cs`, `RandomPasswordGenerator.App.cs` |
| DataObjects (`CRM.DataObjects/`) | 3 C# | `DataObjects.App.cs`, `ConfigurationHelper.App.cs`, `GlobalSettings.App.cs` |
| Client (`CRM.Client/`) | 2 C# + 1 Razor + 14 AppComponent Razors + 1 CSS | `DataModel.App.cs`, `Helpers.App.cs`, `MainLayout.App.razor`, 14 × `*.App.razor`, `site.App.css` |
| **Total** | **29 files** | |

---

## Server Project — `{ProjectName}/`

### 1. `Program.App.cs`

**Type:** C# partial class of `Program`
**Complexity:** HIGH — 6 hook methods covering the entire startup lifecycle
**Architecture Doc:** `006_architecture.program.app.md`

| Method | Signature | Called From | Purpose |
|--------|-----------|-------------|---------|
| `AppModifyBuilderStart` | `WebApplicationBuilder → WebApplicationBuilder` | `Program.cs` — early in builder setup | Register early services, config sources |
| `AppModifyBuilderEnd` | `WebApplicationBuilder → WebApplicationBuilder` | `Program.cs` — after all default services | Register late services (BackgroundService, custom DI) |
| `AppModifyStart` | `WebApplication → WebApplication` | `Program.cs` — before middleware pipeline | Add middleware (UseApiKeyAuth, UseCors, etc.) |
| `AppModifyEnd` | `WebApplication → WebApplication` | `Program.cs` — after middleware pipeline | Final app tweaks |
| `AuthenticationPoliciesApp` | `List<string>` property | `Program.cs` — auth policy setup | Add custom auth policy names |
| `ConfigurationHelpersLoadApp` | `(ConfigurationHelperLoader, WebApplicationBuilder) → ConfigurationHelperLoader` | `Program.cs` — config loading | Load app-specific appsettings values |

### 2. `Controllers/DataController.App.cs`

**Type:** C# partial class of `DataController`
**Complexity:** MEDIUM — 3 methods (1 auth hook, 1 example endpoint, 1 SignalR hook)
**Architecture Doc:** `006_architecture.datacontroller.app.md`

| Method | Signature | Called From | Purpose |
|--------|-----------|-------------|---------|
| `Authenticate_App` | `() → DataObjects.User` | `DataController` constructor | Custom auth (API keys, tokens, etc.) |
| `YourEndpoint` | `[HttpGet] → ActionResult<BooleanResponse>` | N/A (example) | Template for adding API endpoints |
| `SignalRUpdateApp` | `(SignalRUpdate) → Task<bool>` | `DataController.cs` SignalR handler | Process app-specific SignalR updates server-side |

### 3. `Components/Modules.App.razor`

**Type:** Server-side Razor component
**Complexity:** MEDIUM — 3 injection areas for HTML head, body, and JavaScript
**Architecture Doc:** `006_architecture.modules.app.md`

| Area (switch case) | Purpose |
|---------------------|---------|
| `"head"` | Add `<link>` or `<meta>` tags to the HTML `<head>` |
| `"body"` | Add `<script>` tags or other body elements |
| `"javascript"` | Inline JavaScript (includes `onReadyApp` pattern) |

---

## DataAccess Project — `{ProjectName}.DataAccess/`

### 4. `DataAccess.App.cs`

**Type:** C# partial class of `DataAccess` + partial `IDataAccess`
**Complexity:** VERY HIGH — 16 hook methods covering CRUD lifecycle, background tasks, settings, data mapping, filtering, and soft-delete
**Architecture Doc:** `006_architecture.dataaccess.app.md`

| Method | Signature | Called From | Purpose |
|--------|-----------|-------------|---------|
| `AppLanguage` | `Dictionary<string, string>` property | `DataAccess.Language.cs` | Custom/override language tags |
| `DataAccessAppInit` | `void` | `DataAccess` constructor | App-specific initialization |
| `DeleteAllPendingDeletedRecordsApp` | `(Guid TenantId, DateTime OlderThan) → Task<BooleanResponse>` | `DataAccess.cs` cleanup job | Purge soft-deleted app records |
| `DeleteRecordImmediatelyApp` | `(string? Type, Guid RecordId, User CurrentUser) → Task<BooleanResponse>` | `DataAccess.cs` | Hard-delete by type |
| `DeleteRecordsApp` | `(object Rec, User? CurrentUser) → Task<BooleanResponse>` | Various delete methods | Cascade-delete related records |
| `GetApplicationSettingsApp` | `(ApplicationSettings) → ApplicationSettings` | `DataAccess.GetApplicationSettings` | Load app-specific settings |
| `GetApplicationSettingsUpdateApp` | `(ApplicationSettingsUpdate) → ApplicationSettingsUpdate` | `DataAccess.AppSettings` property | Load app-specific update settings |
| `GetBlazorDataModelApp` | `(BlazorDataModelLoader, User?) → Task<BlazorDataModelLoader>` | `DataAccess.GetBlazorDataModel` | Inject app data into client model |
| `GetDeletedRecordCountsApp` | `(Guid TenantId, DeletedRecordCounts) → Task<DeletedRecordCounts>` | `DataAccess.cs` | Count soft-deleted app records |
| `GetDeletedRecordsApp` | `(Guid TenantId, DeletedRecords) → Task<DeletedRecords>` | `DataAccess.cs` | List soft-deleted app records |
| `GetFilterColumnsApp` | `(string Type, string Position, Language, User?) → List<FilterColumn>` | `DataAccess.cs` | Add filter columns to listings |
| `GetDataApp` | `(object Rec, object DataObject, User?)` | Various Get methods | Map EF → DTO custom fields |
| `SaveDataApp` | `(object Rec, object DataObject, User?)` | Various Save methods | Map DTO → EF custom fields |
| `SaveApplicationSettingsApp` | `(ApplicationSettings, User) → Task<ApplicationSettings>` | `DataAccess.SaveApplicationSettings` | Persist app-specific settings |
| `SortUsersApp` | `(IQueryable<User>?, string SortBy, bool Ascending) → IQueryable<User>?` | `DataAccess.cs` user listing | Custom user sort columns |
| `UndeleteRecordApp` | `(string? Type, Guid RecordId, User CurrentUser) → Task<BooleanResponse>` | `DataAccess.cs` | Restore soft-deleted app records |
| `ProcessBackgroundTasksApp` | `(Guid TenantId, long Iteration) → Task<BooleanResponse>` | BackgroundProcessor | Periodic app-specific tasks |
| `YourMethod` | `() → BooleanResponse` | N/A (example) | Template for adding DA methods |

**IDataAccess interface additions:**

| Method | Purpose |
|--------|---------|
| `ProcessBackgroundTasksApp(Guid, long)` | Background task processing (public) |
| `YourMethod()` | Example method (public) |

### 5. `GraphAPI.App.cs`

**Type:** C# partial class of `GraphClient`
**Complexity:** EMPTY — placeholder for Microsoft Graph extensions
**Architecture Doc:** (grouped in `006_architecture.dataaccess.app.md`)

### 6. `Utilities.App.cs`

**Type:** C# static partial class `Utilities`
**Complexity:** EMPTY — placeholder for custom utility methods
**Architecture Doc:** (grouped in `006_architecture.dataaccess.app.md`)

### 7. `RandomPasswordGenerator.App.cs`

**Type:** C# static partial class `PasswordGenerator`
**Complexity:** EMPTY — placeholder for custom password generation logic
**Architecture Doc:** (grouped in `006_architecture.dataaccess.app.md`)

---

## DataObjects Project — `{ProjectName}.DataObjects/`

### 8. `DataObjects.App.cs`

**Type:** C# partial class of `DataObjects`
**Complexity:** LOW — placeholder for custom DTOs, SignalR types, User extensions
**Architecture Doc:** `006_architecture.dataobjects.app.md`

| Section | Purpose |
|---------|---------|
| `partial class SignalRUpdateType` | Add custom SignalR update type constants |
| `partial class User` | Add custom properties to the User object |
| New classes | Define entirely new DTOs |

### 9. `ConfigurationHelper.App.cs`

**Type:** C# partial interface + classes (3 partials)
**Complexity:** MEDIUM — extends the configuration system at 3 layers (interface, implementation, loader)
**Architecture Doc:** `006_architecture.configurationhelper.app.md`

| Partial | Purpose |
|---------|---------|
| `IConfigurationHelper` | Declare new config property contracts |
| `ConfigurationHelper` | Implement properties reading from `_loader` |
| `ConfigurationHelperLoader` | Declare settable properties populated from appsettings |
| `ConfigurationHelperConnectionStrings` | Declare custom connection string properties |

### 10. `GlobalSettings.App.cs`

**Type:** C# static partial class `GlobalSettings`
**Complexity:** LOW — placeholder for app-wide constants and enums
**Architecture Doc:** `006_architecture.globalsettings.app.md`

---

## Client Project — `{ProjectName}.Client/`

### 11. `DataModel.App.cs`

**Type:** C# partial class of `BlazorDataModel`
**Complexity:** MEDIUM — custom client state, deleted record checking, plugin precompilation
**Architecture Doc:** `006_architecture.datamodel.app.md`

| Member | Purpose |
|--------|---------|
| `HaveDeletedRecordsApp` | Check if app-specific deleted records exist |
| `MyCustomDataModelMethod()` | Template for custom methods |
| `MyValues` (property with change notification) | Example: custom reactive property with `NotifyDataChanged()` |
| `PrecompileBlazorPlugins` | Control plugin compilation timing |

### 12. `Helpers.App.cs`

**Type:** C# static partial class `Helpers`
**Complexity:** HIGH — 10 hook methods/properties for menus, SignalR, deleted records, tags, icons, model reload
**Architecture Doc:** `006_architecture.helpers.app.md`

| Method/Property | Purpose |
|----------------|---------|
| `AppIcons` | Define custom icon mappings |
| `AppMethod()` | Template for custom helper methods |
| `AvailableTagListApp(Module, ExcludeTags)` | Filter tags by app-specific module type |
| `GetDeletedRecordTypesApp()` | List app-specific soft-delete types |
| `GetDeletedRecordsForAppType(deletedRecords, type)` | Return deleted records by app type |
| `GetDeletedRecordsLanguageTagForAppType(type)` | Get language tags for deleted record types |
| `MenuItemsApp` | Add top-level navigation menu items |
| `MenuItemsAdminApp` | Add admin-only menu items |
| `ProcessSignalRUpdateApp(update)` | Handle app-specific SignalR updates client-side |
| `ProcessSignalRUpdateAppUndelete(update)` | Handle undelete-specific SignalR updates |
| `ReloadModelApp(blazorDataModelLoader)` | Reload app-specific data into the model |
| `UpdateModelDeletedRecordCountsForAppItems(deletedRecords)` | Update deleted record counts in the model |

### 13. `Layout/MainLayout.App.razor`

**Type:** Blazor component with `Enabled` and `OverrideCompleteLayout` parameters
**Complexity:** MEDIUM — full layout override capability
**Architecture Doc:** `006_architecture.mainlayout.app.md`

| Parameter | Type | Purpose |
|-----------|------|---------|
| `Enabled` | `bool` (default: `false`) | Replaces the default layout when `true` |
| `BodyContent` | `RenderFragment?` | The page body to render |
| `Loading` | `bool` | Loading state |
| `OverrideCompleteLayout` | `bool` (default: `false`) | When `true`, hides ALL framework UI (menus, modals, etc.) |

### 14–27. `Shared/AppComponents/*.App.razor` (14 files)

**Type:** Blazor components injected into framework pages
**Architecture Doc:** `006_architecture.appcomponents.app.md`

| File | Bound Type | Areas / Parameters | Purpose |
|------|------------|-------------------|---------|
| `About.App.razor` | N/A | Full page content | Customize About page |
| `AppSettings.App.razor` | `DataObjects.Tenant` | `top`, `tenantcodes`, `mailserver`, ... | Inject into AppSettings sections |
| `EditAppointment.App.razor` | `DataObjects.Appointment` | `top`, `style`, ... (Enabled) | Inject into appointment edit form |
| `EditDepartment.App.razor` | `DataObjects.Department` | @bind-Value | Add fields to department edit |
| `EditDepartmentGroup.App.razor` | `DataObjects.DepartmentGroup` | @bind-Value | Add fields to department group edit |
| `EditTag.App.razor` | `DataObjects.Tag` | `th`, `td`, `edit` | Add columns/fields to tag listing/edit |
| `EditTenant.App.razor` | `DataObjects.Tenant` | @bind-Value | Add fields to tenant edit |
| `EditUser.App.razor` | `DataObjects.User` | OverridePage, @bind-Value | Add permissions/fields to user edit |
| `EditUserGroup.App.razor` | `DataObjects.UserGroup` | OverridePage, @bind-Value | Add permissions/fields to user group edit |
| `Index.App.razor` | N/A | RequireLogin, ShowTestPageLinks | Customize home page |
| `NavigationMenu.App.razor` | N/A | Enabled | Replace entire navigation menu |
| `OffcanvasPopoutMenu.App.razor` | N/A | QuickAction switch | Add custom popout actions |
| `Settings.App.razor` | `DataObjects.Tenant` | `general`, `theme`, `authentication`, `optionalfeatures`, `workschedule`, `email`, default (ShowAppSettingsTab) | Inject into settings page sections |
| `UserGroups.App.razor` | `List<DataObjects.UserGroup>` | OverridePage | Override user groups listing |
| `Users.App.razor` | `List<DataObjects.User>` | OverridePage | Override users listing |

### 28. `wwwroot/css/site.App.css`

**Type:** CSS stylesheet
**Complexity:** EMPTY — placeholder for app-specific styles
**Architecture Doc:** (grouped in `006_architecture.modules.app.md`)

---

## Server Component — `{ProjectName}/Components/`

### 29. `Modules.App.razor`

**Already listed** as Server #3 above. Server-side Razor for injecting into `<head>`, `<body>`, and inline JavaScript.

---

## Files NOT in the Hook System

These files exist in the FreeCRM base but are NOT `.App.` hook files — they are direct extension points via C# partial classes:

| File | Purpose |
|------|---------|
| `DataAccess.cs` | Framework DataAccess — calls hook methods |
| `DataController.cs` | Framework controller — calls hook methods |
| `Program.cs` | Framework startup — calls hook methods |
| `signalrHub.cs` | SignalR hub — partial, can extend without hooks |

---

## Architecture Docs to Create

Based on the complexity and groupings above, the following individual docs are needed:

| Doc | Covers | Complexity |
|-----|--------|------------|
| `006_architecture.program.app.md` | `Program.App.cs` (6 methods) | HIGH |
| `006_architecture.datacontroller.app.md` | `DataController.App.cs` (3 methods) | MEDIUM |
| `006_architecture.dataaccess.app.md` | `DataAccess.App.cs` (16 methods) + `GraphAPI.App.cs`, `Utilities.App.cs`, `RandomPasswordGenerator.App.cs` | VERY HIGH |
| `006_architecture.dataobjects.app.md` | `DataObjects.App.cs` + `GlobalSettings.App.cs` | LOW |
| `006_architecture.configurationhelper.app.md` | `ConfigurationHelper.App.cs` (4 partials) | MEDIUM |
| `006_architecture.datamodel.app.md` | `DataModel.App.cs` | MEDIUM |
| `006_architecture.helpers.app.md` | `Helpers.App.cs` (12 methods/properties) | HIGH |
| `006_architecture.mainlayout.app.md` | `MainLayout.App.razor` | MEDIUM |
| `006_architecture.modules.app.md` | `Modules.App.razor` + `site.App.css` | MEDIUM |
| `006_architecture.appcomponents.app.md` | All 14 `*.App.razor` in AppComponents | HIGH |

**Total: 10 architecture docs**

---

*Created: 2025-07-25*
*Category: Research*
*Source: `ReferenceProjects/FreeCRM-main/` — all 29 `.App.` files read in full*
