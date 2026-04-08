# FreeExamples.DataAccess

> Data access layer — EF Core database queries, Microsoft Graph integration, LDAP authentication, PDF generation, plugin execution, and background task processing.

**Target:** .NET 10 · **Type:** Class Library

---

## What This Project Contains

| Area | Description |
|------|-------------|
| **Entity CRUD** | Database operations for all entities (Users, Departments, Tags, Settings, etc.) |
| **Authentication** | Local login, LDAP (Novell.Directory.Ldap), and Microsoft Graph (Azure AD) |
| **File Storage** | Binary file storage and retrieval |
| **PDF Generation** | Document generation using QuestPDF |
| **Plugin Execution** | Runtime loading and execution of compiled plugins |
| **Background Tasks** | `ProcessBackgroundTasksApp` for periodic processing |
| **App Customization** | `DataAccess.App.cs` — application-specific methods and language tags |

---

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `Brad.Wickett_Sql2LINQ` | LINQ query builder |
| `Microsoft.Graph` | Microsoft Graph API for Azure AD users/groups |
| `Azure.Identity` | Azure credential management |
| `Novell.Directory.Ldap.NETStandard` | LDAP/Active Directory authentication |
| `QuestPDF` | PDF document generation |
| `CsvHelper` | CSV import/export |
| `JWTHelpers` | JWT token creation and validation |

---

## Customization

Add application-specific data access methods to `DataAccess.App.cs`:

```csharp
public partial class DataAccess
{
    public async Task<BooleanResponse> ProcessBackgroundTasksApp(Guid TenantId, long Iteration)
    {
        // Your periodic background tasks here
    }
}
```

Override or add language tags in the `AppLanguage` dictionary for localization.

---

*Part of the [FreeExamples](..) suite.*
