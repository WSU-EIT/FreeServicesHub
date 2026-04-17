# FreeCRM Hook: DataAccess.App.cs

> Data layer lifecycle hooks — CRUD mapping, soft-delete, background tasks, settings, filtering, initialization.

**File:** `{ProjectName}.DataAccess/DataAccess.App.cs`
**Class:** `partial class DataAccess` + `partial interface IDataAccess`
**Complexity:** VERY HIGH — 16 hook methods + 3 empty companion files

**Also covers:** `GraphAPI.App.cs`, `Utilities.App.cs`, `RandomPasswordGenerator.App.cs`

---

## Hook Methods

### DataAccessAppInit

```csharp
private void DataAccessAppInit()
```

**Called:** From the `DataAccess` constructor.
**Use for:** One-time initialization — cache setup, seeding lookup data, registering event handlers.

**Custom file:** `{ProjectName}.App.DataAccess.cs`

---

### AppLanguage

```csharp
private Dictionary<string, string> AppLanguage { get; }
```

**Called:** From `DataAccess.Language.cs` during language tag resolution.
**Use for:** Custom language tags or overriding built-in tags like `AppTitle`.

**Example:**

```csharp
private Dictionary<string, string> AppLanguage {
    get {
        return new Dictionary<string, string>
        {
            { "AppTitle", "GLBA Compliance Tracker" },
            { "SourceSystem", "Source System" },
            { "AccessEvent", "Access Event" },
        };
    }
}
```

---

### GetDataApp / SaveDataApp

```csharp
private void GetDataApp(object Rec, object DataObject, DataObjects.User? CurrentUser = null)
private void SaveDataApp(object Rec, object DataObject, DataObjects.User? CurrentUser = null)
```

**Called:** From every framework Get/Save method when mapping between EF models and DTOs.
**Use for:** Mapping custom fields you've added to existing entities (User, Department, etc.).

**Example — adding a custom field to User:**

```csharp
// In DataAccess.App.cs:
private void GetDataApp(object Rec, object DataObject, DataObjects.User? CurrentUser = null)
{
    try {
        if (Rec is EFModels.EFModels.User && DataObject is DataObjects.User) {
            var rec = Rec as EFModels.EFModels.User;
            var user = DataObject as DataObjects.User;
            if (rec != null && user != null) {
                user.EmployeeId = rec.EmployeeId;
            }
            return;
        }
    } catch { }
}

private void SaveDataApp(object Rec, object DataObject, DataObjects.User? CurrentUser = null)
{
    try {
        if (Rec is EFModels.EFModels.User && DataObject is DataObjects.User) {
            var rec = Rec as EFModels.EFModels.User;
            var user = DataObject as DataObjects.User;
            if (rec != null && user != null) {
                rec.EmployeeId = user.EmployeeId;
            }
            return;
        }
    } catch { }
}
```

**Pattern:** Type-check both EF model and DTO, cast, map custom properties. Always `return` after handling a match.

---

### Delete Hooks (3 methods)

```csharp
private async Task<DataObjects.BooleanResponse> DeleteRecordsApp(object Rec, DataObjects.User? CurrentUser = null)
private async Task<DataObjects.BooleanResponse> DeleteRecordImmediatelyApp(string? Type, Guid RecordId, DataObjects.User CurrentUser)
private async Task<DataObjects.BooleanResponse> DeleteAllPendingDeletedRecordsApp(Guid TenantId, DateTime OlderThan)
```

| Method | Called When | Use For |
|--------|------------|---------|
| `DeleteRecordsApp` | Before deleting any entity | Cascade-delete related records in your custom tables |
| `DeleteRecordImmediatelyApp` | Hard-delete by type string | Route `"mytype"` to your delete method |
| `DeleteAllPendingDeletedRecordsApp` | Cleanup job for soft-deletes | Purge old records from your custom tables |

**Example — cascade delete:**

```csharp
private async Task<DataObjects.BooleanResponse> DeleteRecordsApp(object Rec, DataObjects.User? CurrentUser = null)
{
    var output = new DataObjects.BooleanResponse();
    try {
        if (Rec is EFModels.EFModels.SourceSystemItem) {
            var rec = Rec as EFModels.EFModels.SourceSystemItem;
            if (rec != null) {
                // Remove all access events for this source system
                data.AccessEvents.RemoveRange(
                    data.AccessEvents.Where(x => x.SourceSystemId == rec.SourceSystemId));
                await data.SaveChangesAsync();
            }
        }
    } catch (Exception ex) {
        output.Messages.AddRange(RecurseException(ex));
    }
    output.Result = output.Messages.Count == 0;
    return output;
}
```

---

### UndeleteRecordApp

```csharp
private async Task<DataObjects.BooleanResponse> UndeleteRecordApp(string? Type, Guid RecordId, DataObjects.User CurrentUser)
```

**Called:** When restoring soft-deleted records.
**Use for:** Routing your app-specific types through the undelete system.

---

### Settings Hooks (3 methods)

```csharp
private DataObjects.ApplicationSettings GetApplicationSettingsApp(DataObjects.ApplicationSettings settings)
private DataObjects.ApplicationSettingsUpdate GetApplicationSettingsUpdateApp(DataObjects.ApplicationSettingsUpdate settings)
private async Task<DataObjects.ApplicationSettings> SaveApplicationSettingsApp(DataObjects.ApplicationSettings settings, DataObjects.User CurrentUser)
```

**Use for:** Loading and saving app-specific tenant settings that appear on the Settings page.

---

### GetBlazorDataModelApp

```csharp
private async Task<DataObjects.BlazorDataModelLoader> GetBlazorDataModelApp(
    DataObjects.BlazorDataModelLoader blazorDataModelLoader, DataObjects.User? CurrentUser = null)
```

**Called:** When building the Blazor data model sent to the client.
**Use for:** Injecting app-specific data (lists, counts, config) that pages need.

---

### GetDeletedRecordCountsApp / GetDeletedRecordsApp

```csharp
private async Task<DataObjects.DeletedRecordCounts> GetDeletedRecordCountsApp(Guid TenantId, DataObjects.DeletedRecordCounts deletedRecordCounts)
private async Task<DataObjects.DeletedRecords> GetDeletedRecordsApp(Guid TenantId, DataObjects.DeletedRecords deletedRecords)
```

**Use for:** Including your custom entity types in the deleted records management UI.

---

### GetFilterColumnsApp

```csharp
private List<DataObjects.FilterColumn> GetFilterColumnsApp(string Type, string Position, DataObjects.Language Language, DataObjects.User? CurrentUser = null)
```

**Called:** When building filter columns for list pages.
**Use for:** Adding custom filter columns to Users, Invoices, or other listing pages.

---

### SortUsersApp

```csharp
private IQueryable<EFModels.EFModels.User>? SortUsersApp(IQueryable<EFModels.EFModels.User>? recs, string SortBy, bool Ascending)
```

**Called:** When sorting user listings.
**Use for:** Custom sort columns you've added to the User entity.

---

### ProcessBackgroundTasksApp

```csharp
public async Task<DataObjects.BooleanResponse> ProcessBackgroundTasksApp(Guid TenantId, long Iteration)
```

**Called:** By the BackgroundProcessor on each tick (default: every 10 seconds per `appsettings.json`).
**Use for:** Periodic tasks — sync data, send emails, cleanup, cache refresh.

**Suggested tie-in:**

```csharp
// In DataAccess.App.cs:
public async Task<DataObjects.BooleanResponse> ProcessBackgroundTasksApp(Guid TenantId, long Iteration)
{
    var output = new DataObjects.BooleanResponse();
    output = await MyProcessBackgroundTasks(TenantId, Iteration);  // ← tie-in
    output.Result = true;
    return output;
}

// In {ProjectName}.App.DataAccess.BackgroundTasks.cs:
private async Task<DataObjects.BooleanResponse> MyProcessBackgroundTasks(Guid TenantId, long Iteration)
{
    var output = new DataObjects.BooleanResponse();
    // Run every 6 iterations (60s if interval is 10s)
    if (Iteration % 6 == 0) {
        await SyncExternalData(TenantId);
    }
    output.Result = true;
    return output;
}
```

---

## Companion Hook Files (Empty Placeholders)

### GraphAPI.App.cs

```csharp
public partial class GraphClient { }
```

**Use for:** Adding methods to the Microsoft Graph API client (email, calendar, user management via Azure AD).
**Custom file:** `{ProjectName}.App.GraphAPI.cs`

### Utilities.App.cs

```csharp
public static partial class Utilities { }
```

**Use for:** Shared utility methods (string manipulation, date helpers, etc.).
**Custom file:** `{ProjectName}.App.Utilities.cs`

### RandomPasswordGenerator.App.cs

```csharp
public static partial class PasswordGenerator { }
```

**Use for:** Custom password generation rules (specific complexity, character sets).
**Custom file:** `{ProjectName}.App.PasswordGenerator.cs`

---

## Suggested File Names

| Scenario | File Name |
|----------|-----------|
| General DA coordinator | `{ProjectName}.App.DataAccess.cs` |
| Feature CRUD | `{ProjectName}.App.DataAccess.{Feature}.cs` |
| Background tasks | `{ProjectName}.App.DataAccess.BackgroundTasks.cs` |
| Graph API extensions | `{ProjectName}.App.GraphAPI.cs` |
| Utility methods | `{ProjectName}.App.Utilities.cs` |

---

## API Pattern

> **Preferred:** Three DataAccess methods per entity — **GetMany**, **SaveMany**, **DeleteMany**.
> See [007_patterns.crud_api.md](007_patterns.crud_api.md) for full pattern with DataAccess + Controller + Client examples.

| Method | Signature | Behavior |
|--------|-----------|----------|
| `Get{Entity}s` | `(List<Guid>? ids) → List<T>` | `null`/empty → all; IDs → filtered |
| `Save{Entity}s` | `(List<T> items, User) → List<T>` | PK exists → update; empty/new PK → insert |
| `Delete{Entity}s` | `(List<Guid>? ids) → BooleanResponse` | Must provide IDs; `null`/empty → error |

---

*Category: 006_architecture*
*Source: `ReferenceProjects/FreeCRM-main/CRM.DataAccess/DataAccess.App.cs`*
