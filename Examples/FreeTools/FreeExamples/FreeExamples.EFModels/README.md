# FreeExamples.EFModels

> Entity Framework Core DbContext and entity models — supports SQL Server, SQLite, MySQL, PostgreSQL, and In-Memory providers.

**Target:** .NET 10 · **Type:** Class Library

---

## What This Project Contains

| File | Description |
|------|-------------|
| `EFDataModel.cs` | Partial `DbContext` with `DbSet<>` properties and `OnModelCreating` configuration |
| `Department.cs` | Department entity |
| `DepartmentGroup.cs` | Department group entity |
| `FileStorage.cs` | Binary file storage entity |
| `PluginCache.cs` | Compiled plugin cache entity |
| `Setting.cs` | Application settings entity |
| `Tag.cs` | Tag entity |
| `TagItem.cs` | Tag-to-entity association entity |
| `Tenant.cs` | Multi-tenant entity |
| `UDFLabel.cs` | User-defined field labels entity |
| `User.cs` | User entity |
| `UserGroup.cs` | User group entity |
| `UserInGroup.cs` | User-group membership entity |

---

## Database Providers

| Provider | Package | Status |
|----------|---------|--------|
| SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` | ✅ Primary |
| SQLite | `Microsoft.EntityFrameworkCore.Sqlite` | ✅ Supported |
| MySQL | `MySql.EntityFrameworkCore` | ✅ Supported |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | ✅ Supported |
| In-Memory | `Microsoft.EntityFrameworkCore.InMemory` | ✅ Development |

---

## Migrations

This project does not ship with pre-built migration files. The [DatabaseMigration tool](../FreeExamples.DatabaseMigration/) can generate fresh migrations on demand using menu option **M** or CLI `--Migration:AutoRun=efmigrate`.

To generate migrations manually:

```bash
# Uncomment OnConfiguring in EFDataModel.cs with your connection string, then:
dotnet ef migrations add InitialMigration --project FreeExamples.EFModels
dotnet ef database update --project FreeExamples.EFModels
```

---

*Part of the [FreeExamples](..) suite.*
