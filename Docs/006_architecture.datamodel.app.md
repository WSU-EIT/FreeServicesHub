# FreeCRM Hook: DataModel.App.cs

> Client-side state management — custom reactive properties, deleted record checks, plugin control.

**File:** `{ProjectName}.Client/DataModel.App.cs`
**Class:** `partial class BlazorDataModel`
**Complexity:** MEDIUM — custom state properties with change notification

---

## Hook Members

### HaveDeletedRecordsApp

```csharp
private bool HaveDeletedRecordsApp { get; }
```

**Called:** From the `BlazorDataModel` deleted records check.
**Use for:** Return `true` if any of your app-specific entity types have soft-deleted records.

```csharp
private bool HaveDeletedRecordsApp {
    get {
        bool output = false;
        if (DeletedRecordCounts.SourceSystemCount > 0) output = true;
        if (DeletedRecordCounts.AccessEventCount > 0) output = true;
        return output;
    }
}
```

---

### Custom Properties with Change Notification

The example shows the pattern for reactive properties that trigger UI updates:

```csharp
// In {ProjectName}.App.DataModel.cs:
private List<DataObjects.PipelineStatus> _PipelineStatuses = new();

public List<DataObjects.PipelineStatus> PipelineStatuses {
    get { return _PipelineStatuses; }
    set {
        if (!ObjectsAreEqual(_PipelineStatuses, value)) {
            _PipelineStatuses = value;
            _ModelUpdated = DateTime.UtcNow;
            NotifyDataChanged();  // ← triggers StateHasChanged on all subscribed components
        }
    }
}
```

**Key:** Use `ObjectsAreEqual` to prevent unnecessary re-renders. Call `NotifyDataChanged()` only when the value actually changes.

---

### PrecompileBlazorPlugins

```csharp
public bool PrecompileBlazorPlugins { get; }
```

**Default:** `false`
**Set to `true`** if your app uses Blazor plugins and you want them precompiled during page load rather than on-demand (avoids delay when first accessing plugin pages).

---

## Suggested File Names

| Scenario | File Name |
|----------|-----------|
| Custom model properties | `{ProjectName}.App.DataModel.cs` |

---

*Category: 006_architecture*
*Source: `ReferenceProjects/FreeCRM-main/CRM.Client/DataModel.App.cs`*
