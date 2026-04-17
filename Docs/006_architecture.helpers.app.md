# FreeCRM Hook: Helpers.App.cs

> Client-side utilities — menus, SignalR handlers, icons, deleted records, tags, model reload.

**File:** `{ProjectName}.Client/Helpers.App.cs`
**Class:** `static partial class Helpers`
**Complexity:** HIGH — 12 hook methods/properties

---

## Navigation & Menus

### MenuItemsApp

```csharp
public static List<DataObjects.MenuItem> MenuItemsApp { get; }
```

**Called:** When building the navigation menu.
**Use for:** Adding top-level menu items for your app pages.

```csharp
// In Helpers.App.cs or {ProjectName}.App.Helpers.cs:
public static List<DataObjects.MenuItem> MenuItemsApp {
    get {
        var output = new List<DataObjects.MenuItem>();
        if (Model.User.Admin) {
            output.Add(new DataObjects.MenuItem {
                Title = "Pipeline Dashboard",
                Icon = "Home",
                PageNames = new List<string> { "pipelines" },
                SortOrder = 1000,
                url = Helpers.BuildUrl("Pipelines"),
            });
        }
        return output;
    }
}
```

### MenuItemsAdminApp

```csharp
public static List<DataObjects.MenuItem> MenuItemsAdminApp { get; }
```

**Use for:** Admin-only menu items (shown only in admin settings area).

---

## SignalR Handlers

### ProcessSignalRUpdateApp

```csharp
public static async Task ProcessSignalRUpdateApp(DataObjects.SignalRUpdate update)
```

**Called:** From `MainLayout.razor` when a SignalR update arrives that isn't handled by the framework.
**Use for:** Client-side reactions to custom SignalR events — reload data, show notifications, update UI.

```csharp
public static async Task ProcessSignalRUpdateApp(DataObjects.SignalRUpdate update)
{
    if (update != null && (update.TenantId == null || update.TenantId == Model.TenantId)) {
        switch (update.UpdateType) {
            case DataObjects.SignalRUpdateType.PipelineLiveStatusUpdate:
                // Update pipeline data in the model
                var liveUpdate = Helpers.DeserializeObject<DataObjects.PipelineLiveUpdate>(update.Object);
                if (liveUpdate != null) {
                    Model.PipelineStatuses = liveUpdate.ChangedPipelines;
                }
                break;
        }
    }
}
```

### ProcessSignalRUpdateAppUndelete

```csharp
public static async Task ProcessSignalRUpdateAppUndelete(DataObjects.SignalRUpdate update)
```

**Use for:** Handling undelete-specific SignalR events for your custom entity types.

---

## Icons

### AppIcons

```csharp
public static Dictionary<string, List<string>> AppIcons { get; }
```

**Use for:** Mapping icon names to Font Awesome classes for the `Icon()` helper.

```csharp
public static Dictionary<string, List<string>> AppIcons {
    get {
        return new Dictionary<string, List<string>> {
            { "fa:fa-solid fa-gauge", new List<string> { "Dashboard", "Pipelines" } },
            { "fa:fa-solid fa-shield", new List<string> { "Compliance", "GLBA" } },
        };
    }
}
```

---

## Deleted Records System

### GetDeletedRecordTypesApp

```csharp
private static List<string> GetDeletedRecordTypesApp()
```

**Use for:** Declaring your app-specific soft-delete types so they appear in the Deleted Records management UI.

### GetDeletedRecordsForAppType

```csharp
public static List<DataObjects.DeletedRecordItem>? GetDeletedRecordsForAppType(DataObjects.DeletedRecords deletedRecords, string type)
```

**Use for:** Returning the list of deleted records for a given app type.

### GetDeletedRecordsLanguageTagForAppType

```csharp
public static string GetDeletedRecordsLanguageTagForAppType(string type)
```

**Use for:** Mapping type strings to language tags for display.

### UpdateModelDeletedRecordCountsForAppItems

```csharp
private static void UpdateModelDeletedRecordCountsForAppItems(DataObjects.DeletedRecords deletedRecords)
```

**Use for:** Updating the model's deleted record counts from the server response.

---

## Tags

### AvailableTagListApp

```csharp
public static List<DataObjects.Tag> AvailableTagListApp(DataObjects.TagModule? Module, List<Guid> ExcludeTags)
```

**Use for:** Filtering available tags by your app-specific tag modules.

---

## Model Reload

### ReloadModelApp

```csharp
private async static Task ReloadModelApp(DataObjects.BlazorDataModelLoader? blazorDataModelLoader)
```

**Called:** From `Helpers.ReloadModel()` after the main model data is reloaded.
**Use for:** Loading app-specific data from the model loader into the Blazor data model.

---

## Suggested File Names

| Scenario | File Name |
|----------|-----------|
| All client helpers | `{ProjectName}.App.Helpers.cs` |
| Feature-specific helpers | `{ProjectName}.App.Helpers.{Feature}.cs` |

---

*Category: 006_architecture*
*Source: `ReferenceProjects/FreeCRM-main/CRM.Client/Helpers.App.cs`*
