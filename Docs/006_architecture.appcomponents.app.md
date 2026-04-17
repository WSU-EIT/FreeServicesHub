# FreeCRM Hook: AppComponents *.App.razor (14 Files)

> Blazor component injection points for customizing framework pages — edit forms, listings, settings, navigation.

**Location:** `{ProjectName}.Client/Shared/AppComponents/`
**Count:** 14 Razor component files
**Complexity:** HIGH (collectively) — covers every major framework page

---

## Overview

Each `*.App.razor` file in `AppComponents/` is a Blazor component embedded in a specific framework page. They use three injection patterns:

| Pattern | How It Works | Example Files |
|---------|-------------|---------------|
| **Area switch** | Framework passes an `Area` string; you render content for specific areas | `Settings.App.razor`, `AppSettings.App.razor`, `EditTag.App.razor` |
| **@bind-Value** | Two-way binding to the entity being edited | `EditDepartment.App.razor`, `EditTenant.App.razor` |
| **OverridePage** | When `true`, replaces the entire framework page | `EditUser.App.razor`, `Users.App.razor`, `UserGroups.App.razor` |

---

## Edit Form Hooks (7 files)

### EditAppointment.App.razor

**Bound type:** `DataObjects.Appointment`
**Parameters:** `Enabled` (bool), `Area` (string)
**Areas:** `top`, `style`, and others
**Use for:** Adding custom fields to the appointment edit form.

### EditDepartment.App.razor

**Bound type:** `DataObjects.Department` via `@bind-Value`
**Use for:** Adding custom fields to the department edit form.

```razor
@* In EditDepartment.App.razor: *@
<div class="mb-2">
    <label for="app-dept-CostCenter"><Language Tag="CostCenter" /></label>
    <input id="app-dept-CostCenter" class="form-control"
           @bind="Value.CostCenter" @bind:after="ValueHasChanged" />
</div>
```

### EditDepartmentGroup.App.razor

**Bound type:** `DataObjects.DepartmentGroup` via `@bind-Value`
**Use for:** Adding custom fields to the department group edit form.

### EditTag.App.razor

**Bound type:** `DataObjects.Tag`
**Areas:** `th` (header cells), `td` (row cells), `edit` (edit form fields)
**Use for:** Adding columns to the tag listing and fields to the tag edit form.

```razor
@switch(Helpers.StringLower(Area)) {
    case "th":
        <th>My Column</th>
        break;
    case "td":
        <td>@Value.MyProperty</td>
        break;
    case "edit":
        <div class="mb-2">
            <label><Language Tag="MyProperty" /></label>
            <input class="form-control" @bind="Value.MyProperty" @bind:after="ValueHasChanged" />
        </div>
        break;
}
```

### EditTenant.App.razor

**Bound type:** `DataObjects.Tenant` via `@bind-Value`
**Use for:** Adding custom fields to the tenant edit form. Access tenant settings via `Value.TenantSettings.MyProperty`.

### EditUser.App.razor

**Bound type:** `DataObjects.User` via `@bind-Value`
**Parameters:** `OverridePage` (bool), `Area` (string)
**Use for:** Adding permission toggles or custom fields to the user edit form. Set `OverridePage = true` to completely replace the built-in edit user page.

```razor
@if (!Value.Admin) {
    <tr>
        <td>
            <div class="form-check form-switch right">
                <input type="checkbox" id="app-editUser-CanViewGLBA" role="switch"
                       class="form-check-input" @bind="Value.CanViewConfidentialData"
                       @bind:after="ValueHasChanged" />
            </div>
        </td>
        <td><label for="app-editUser-CanViewGLBA"><Language Tag="CanViewConfidentialData" /></label></td>
    </tr>
}
```

### EditUserGroup.App.razor

**Bound type:** `DataObjects.UserGroup` via `@bind-Value`
**Parameters:** `OverridePage` (bool)
**Use for:** Adding permission toggles to the user group edit form.

---

## Page Override Hooks (3 files)

### Users.App.razor

**Bound type:** `List<DataObjects.User>` via `@bind-Value`
**Parameters:** `OverridePage` (bool)
**Use for:** Set `OverridePage = true` to completely replace the users listing page.

### UserGroups.App.razor

**Bound type:** `List<DataObjects.UserGroup>` via `@bind-Value`
**Parameters:** `OverridePage` (bool)
**Use for:** Set `OverridePage = true` to completely replace the user groups listing page.

### Index.App.razor

**Parameters:** `RequireLogin` (bool, default `true`), `ShowTestPageLinks` (bool, default `true`)
**Use for:** Customizing the home page content. This is the most commonly customized hook file.

**One-line tie-in pattern (preferred):**

```razor
@* In Index.App.razor — add one line: *@
<{ProjectName}_App_HomePage />
```

Then create `{ProjectName}.App.HomePage.razor` with your full implementation.

**Real-world:** FreeCICD uses `<Index_App_FreeCICD />` to embed the pipeline wizard.

---

## Settings Hooks (2 files)

### Settings.App.razor

**Bound type:** `DataObjects.Tenant` via `@bind-Value`
**Parameters:** `Enabled` (bool, default `false`), `ShowAppSettingsTab` (bool), `Area` (string)
**Areas:** `general`, `theme`, `authentication`, `optionalfeatures`, `workschedule`, `email`, default (app-specific tab)

**Use for:** Injecting custom settings into existing settings page sections OR adding an entirely new "App Settings" tab.

```razor
@switch(Helpers.StringLower(Area)) {
    case "general":
        @* Add fields to the General settings section *@
        break;
    default:
        @if (ShowAppSettingsTab) {
            @* This renders as a separate "App Settings" tab *@
            <div class="mb-2">
                <label><Language Tag="MyAppSetting" /></label>
                <input class="form-control" @bind="Value.TenantSettings.MyAppSetting"
                       @bind:after="ValueHasChanged" />
            </div>
        }
        break;
}
```

### AppSettings.App.razor

**Bound type:** `DataObjects.Tenant`
**Areas:** `top`, `tenantcodes`, `mailserver`, and others
**Use for:** Injecting into the admin-level AppSettings page sections.

---

## Navigation & UI Hooks (2 files)

### NavigationMenu.App.razor

**Parameters:** `Enabled` (bool, default `false`), `Loading` (bool)
**Use for:** Set `Enabled = true` to completely replace the default navigation menu.

### OffcanvasPopoutMenu.App.razor

**Parameters:** None (uses `Model.QuickAction`)
**Use for:** Adding custom actions to the offcanvas popout menu.

```razor
@switch(Helpers.StringLower(Model.QuickAction)) {
    case "runpipeline":
        <{ProjectName}_App_QuickRunPipeline />
        break;
}
```

---

## About Page Hook

### About.App.razor

**Parameters:** None
**Use for:** Customizing the About page content. The default shows FreeCRM info.

**One-line tie-in:**

```razor
@* Replace all content with your custom component: *@
<{ProjectName}_App_About />
```

---

## Common @bind-Value Pattern

All edit form hooks follow this pattern:

```razor
@code {
    [Parameter] public DataObjects.{Entity} Value { get; set; } = new();
    [Parameter] public EventCallback<DataObjects.{Entity}> ValueChanged { get; set; }

    public void Dispose() { Model.OnChange -= StateHasChanged; }

    protected override void OnInitialized() { Model.OnChange += StateHasChanged; }

    private async Task ValueHasChanged()
    {
        await ValueChanged.InvokeAsync(Value);
    }
}
```

**Key:** Always call `ValueChanged.InvokeAsync(Value)` after modifying the bound value to propagate changes back to the parent page.

---

## Suggested File Names

| Scenario | File Name |
|----------|-----------|
| Home page replacement | `{ProjectName}.App.HomePage.razor` |
| Custom navigation menu | `{ProjectName}.App.Navigation.razor` |
| Custom about page | `{ProjectName}.App.About.razor` |
| Custom settings tab | `{ProjectName}.App.Settings.razor` |
| Entity edit extensions | `{ProjectName}.App.Edit{Entity}.razor` |
| Page overrides | `{ProjectName}.App.Pages.{PageName}.razor` |

---

*Category: 006_architecture*
*Source: `ReferenceProjects/FreeCRM-main/CRM.Client/Shared/AppComponents/*.App.razor` (14 files)*
