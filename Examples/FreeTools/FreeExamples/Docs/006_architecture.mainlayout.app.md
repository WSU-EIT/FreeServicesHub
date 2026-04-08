# FreeCRM Hook: MainLayout.App.razor

> Complete layout override — replace the default page layout with a custom one.

**File:** `{ProjectName}.Client/Layout/MainLayout.App.razor`
**Type:** Blazor component with parameters
**Complexity:** MEDIUM — full layout replacement capability

---

## Parameters

| Parameter | Type | Default | Purpose |
|-----------|------|---------|---------|
| `Enabled` | `bool` | `false` | When `true`, replaces the default layout |
| `BodyContent` | `RenderFragment?` | `null` | The page body content to render |
| `Loading` | `bool` | `false` | Loading state indicator |
| `OverrideCompleteLayout` | `bool` | `false` | When `true`, hides ALL framework UI (menus, modals, maintenance messages) |

---

## How to Use

### Option A: Custom Layout with Framework Chrome

Set `Enabled = true` to replace only the main content area while keeping the navigation menu, offcanvas, and other framework UI.

```razor
@* In MainLayout.App.razor: *@
@if (Enabled) {
    <div class="custom-layout">
        <div class="custom-sidebar">
            <{ProjectName}_App_Sidebar />
        </div>
        <div class="custom-content">
            @BodyContent
        </div>
    </div>
}
```

### Option B: Complete Override

Set both `Enabled = true` and `OverrideCompleteLayout = true` to control 100% of the rendered output.

```razor
@if (Enabled && OverrideCompleteLayout) {
    <div class="my-entire-app">
        <{ProjectName}_App_NavBar />
        <main>@BodyContent</main>
        <{ProjectName}_App_Footer />
    </div>
}
```

---

## Suggested File Names

| Scenario | File Name |
|----------|-----------|
| Layout customization | `{ProjectName}.App.Layout.razor` |
| Custom sidebar component | `{ProjectName}.App.Layout.Sidebar.razor` |

---

*Category: 006_architecture*
*Source: `ReferenceProjects/FreeCRM-main/CRM.Client/Layout/MainLayout.App.razor`*
