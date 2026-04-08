# FreeCICD.EFModels

## Overview

The `FreeCICD.EFModels` project contains all Entity Framework Core entities and the `EFDataModel` DbContext. This is the **persistence layer** that maps directly to database tables across multiple database providers.

---

## Project Structure

```
FreeCICD.EFModels/
+-- EFModels/
|   +-- EFDataModel.cs        # DbContext with all DbSets
|   +-- Department.cs         # Department entity
|   +-- DepartmentGroup.cs    # Department grouping entity
|   +-- EmailTemplate.cs      # Email template entity
|   +-- FileStorage.cs        # File storage entity
|   +-- PluginCache.cs        # Compiled plugin cache
|   +-- Setting.cs            # Key-value settings
|   +-- Tag.cs                # Tag entity
|   +-- TagItem.cs            # Tag-to-item junction
|   +-- Tenant.cs             # Multi-tenant root entity
|   +-- UDFLabel.cs           # User-defined field labels
|   +-- User.cs               # User entity
|   +-- UserGroup.cs          # User group entity
|   +-- UserInGroup.cs        # User-to-group junction
+-- EFModelOverrides.cs       # Provider-specific configurations
+-- Migrations/               # EF Core migrations (if present)
+-- FreeCICD.EFModels.csproj  # Project file
```

---

## Entity Relationship Diagram

```
+-----------------------------------------------------------------------------+
|                           ENTITY RELATIONSHIPS                              |
+-----------------------------------------------------------------------------+

                              +--------------+
                              |   Tenant     |
                              +--------------+
                              | TenantId (PK)|
                              | Name         |
                              | TenantCode   |
                              | Enabled      |
                              +--------------+
                                     |
           +--------------------------------------+
           |                         |                         |
           |                         |                         |
    +--------------+          +--------------+          +--------------+
    |  Department  |          |    User      |          |  UserGroup   |
    +--------------+          +--------------+          +--------------+
    | DepartmentId |<---------| DepartmentId |          | GroupId (PK) |
    | TenantId     |          | UserId (PK)  |          | TenantId     |
    | Name         |          | TenantId     |          | Name         |
    +--------------+          | Email        |          +--------------+
           |                  | Username     |                 |
           |                  | Password     |                 |
           |                  +--------------+                 |
           |                         |                         |
           |                         |                         |
           |                         |                         |
    +--------------+          +--------------+                 |
    | Department   |          | FileStorage  |                 |
    | Group        |          +--------------+                 |
    +--------------+          | FileId (PK)  |                 |
    | DeptGroupId  |          | UserId (FK)  |                 |
    | TenantId     |          | TenantId     |                 |
    +--------------+          | FileName     |                 |
                              +--------------+                 |
                                                               |
                              +--------------+                 |
                              | UserInGroup  |<----------------+
                              +--------------+
                              | UserInGroupId|
                              | UserId (FK)  |
                              | GroupId (FK) |
                              +--------------+


+-----------------------------------------------------------------------------+
|                              TAG SYSTEM                                     |
+-----------------------------------------------------------------------------+

    +--------------+          +--------------+
    |     Tag      |          |   TagItem    |
    +--------------+          +--------------+
    | TagId (PK)   |<---------| TagId (FK)   |
    | TenantId     |          | TagItemId(PK)|
    | Name         |          | ItemId       |
    | Style        |          | Type         |
    +--------------+          +--------------+


+-----------------------------------------------------------------------------+
|                           SYSTEM TABLES                                     |
+-----------------------------------------------------------------------------+

    +--------------+     +--------------+     +--------------+
    |   Setting    |     |  PluginCache |     |  UDFLabel    |
    +--------------+     +--------------+     +--------------+
    | SettingId(PK)|     | RecordId (PK)|     | Id (PK)      |
    | SettingName  |     | Name         |     | TenantId     |
    | SettingType  |     | Code         |     | Module       |
    | SettingText  |     | CompiledDLL  |     | UDF          |
    | TenantId     |     | Enabled      |     | Label        |
    | UserId       |     +--------------+     +--------------+
    +--------------+
```

---

## Supported Database Providers

```
+-----------------------------------------------------------------------------+
|                         DATABASE PROVIDERS                                  |
+-----------------------------------------------------------------------------+
|                                                                             |
|  +-----------+  +-----------+  +-----------+  +-----------+                |
|  | SQL Server  |  |  PostgreSQL |  |    MySQL    |  |   SQLite    |        |
|  |             |  |             |  |             |  |             |        |
|  | Production  |  | Production  |  | Production  |  |   Dev/Test  |        |
|  | Recommended |  | Supported   |  | Supported   |  |   Only      |        |
|  +-----------+  +-----------+  +-----------+  +-----------+                |
|                                                                             |
|  +-----------+                                                              |
|  |  InMemory   |   -> Unit testing only                                     |
|  |             |                                                            |
|  +-----------+                                                              |
|                                                                             |
+-----------------------------------------------------------------------------+
```

### Provider Configuration

The `EFModelOverrides.cs` handles GUID conversion for non-SQL Server databases:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    var providerName = this.Database.ProviderName;
    switch (providerName.ToUpper()) {
        case "MICROSOFT.ENTITYFRAMEWORKCORE.SQLSERVER":
        case "MICROSOFT.ENTITYFRAMEWORKCORE.INMEMORY":
            // Native GUID support
            break;

        case "MYSQL.ENTITYFRAMEWORKCORE":
        case "NPGSQL.ENTITYFRAMEWORKCORE.POSTGRESQL":
        case "MICROSOFT.ENTITYFRAMEWORKCORE.SQLITE":
            // Convert GUIDs to strings
            configurationBuilder
                .Properties<Guid>()
                .HaveConversion<GuidToStringConverter>();
            break;
    }
}
```

---

## Core Entities

### Tenant (Multi-tenancy Root)

```csharp
public class Tenant {
    public Guid TenantId { get; set; }
    public string Name { get; set; }
    public string TenantCode { get; set; }  // URL-friendly identifier
    public bool Enabled { get; set; }
    
    // Audit fields
    public DateTime Added { get; set; }
    public string? AddedBy { get; set; }
    public DateTime LastModified { get; set; }
    public string? LastModifiedBy { get; set; }
    
    // Navigation
    public virtual ICollection<User> Users { get; set; }
}
```

### User

```csharp
public class User {
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    
    // Identity
    public string Username { get; set; }
    public string Email { get; set; }
    public string? Password { get; set; }  // Hashed
    
    // Profile
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Title { get; set; }
    public string? Location { get; set; }
    public string? EmployeeId { get; set; }
    
    // Permissions
    public bool Enabled { get; set; }
    public bool Admin { get; set; }
    public bool ManageFiles { get; set; }
    public bool ManageAppointments { get; set; }
    public bool CanBeScheduled { get; set; }
    
    // Security
    public int? FailedLoginAttempts { get; set; }
    public DateTime? LastLockoutDate { get; set; }
    public bool PreventPasswordChange { get; set; }
    
    // User Defined Fields
    public string? UDF01 { get; set; }
    // ... through UDF10
    
    // Soft Delete
    public bool Deleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation
    public virtual Tenant Tenant { get; set; }
    public virtual Department? Department { get; set; }
    public virtual ICollection<FileStorage> FileStorages { get; set; }
    public virtual ICollection<UserInGroup> UserInGroups { get; set; }
}
```

### Setting (Key-Value Store)

```csharp
public class Setting {
    public int SettingId { get; set; }
    public string SettingName { get; set; }
    public string? SettingType { get; set; }
    public string? SettingText { get; set; }  // JSON for complex objects
    public string? SettingNotes { get; set; }
    
    // Scope (null = global, set = scoped)
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
}
```

---

## Security Considerations

### Password Storage

- Passwords are **never stored in plaintext**
- Hashing is done in the DataAccess layer
- The `Password` property should only be set during save operations

### Sensitive Fields

```csharp
// These should never be returned in API responses:
- User.Password
- Setting.SettingText (when SettingType = "EncryptedText" or "EncryptedObject")
```

### Soft Delete Pattern

Most entities support soft delete:

```csharp
public bool Deleted { get; set; }
public DateTime? DeletedAt { get; set; }
```

**Query filtering** should exclude deleted records by default:

```csharp
// In DataAccess layer
query.Where(x => x.Deleted == false)
```

### Multi-Tenancy

All queries must be **tenant-scoped** (except for admin operations):

```csharp
// Always filter by TenantId
query.Where(x => x.TenantId == currentUserTenantId)
```

---

## Model Configuration Patterns

### Primary Key Generation

All GUIDs are **client-generated** (not database-generated):

```csharp
entity.Property(e => e.UserId).ValueGeneratedNever();
```

### String Length Constraints

```csharp
entity.Property(e => e.Email).HasMaxLength(100);
entity.Property(e => e.FirstName).HasMaxLength(100);
entity.Property(e => e.Username).HasMaxLength(100);
```

### Foreign Key Relationships

```csharp
// User -> Tenant (required)
entity.HasOne(d => d.Tenant)
    .WithMany(p => p.Users)
    .HasForeignKey(d => d.TenantId)
    .OnDelete(DeleteBehavior.ClientSetNull);

// User -> Department (optional)
entity.HasOne(d => d.Department)
    .WithMany(p => p.Users)
    .HasForeignKey(d => d.DepartmentId);
```

### Indexes

```csharp
entity.HasIndex(e => e.TenantId, "IX_Users_TenantId");
entity.HasIndex(e => e.DepartmentId, "IX_Users_DepartmentId");
```

---

## Dependencies

```xml
<ItemGroup>
  <!-- Core EF -->
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
  
  <!-- Providers -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
  <PackageReference Include="MySql.EntityFrameworkCore" Version="10.0.0-preview" />
</ItemGroup>
```

---

## Migration Commands

```bash
# Add a migration
dotnet ef migrations add MigrationName --project FreeCICD.EFModels

# Update database
dotnet ef database update --project FreeCICD.EFModels

# Generate SQL script
dotnet ef migrations script --project FreeCICD.EFModels
```

---

## DbContext Usage

### Registration (in Program.cs)

```csharp
builder.Services.AddDbContext<EFDataModel>(options => {
    switch (databaseType) {
        case "SQLServer":
            options.UseSqlServer(connectionString);
            break;
        case "PostgreSQL":
            options.UseNpgsql(connectionString);
            break;
        case "MySQL":
            options.UseMySQL(connectionString);
            break;
        case "SQLite":
            options.UseSqlite(connectionString);
            break;
        case "InMemory":
            options.UseInMemoryDatabase("FreeCICD");
            break;
    }
});
```

### Query Examples

```csharp
// Get active users for a tenant
var users = await data.Users
    .Where(u => u.TenantId == tenantId && !u.Deleted && u.Enabled)
    .Include(u => u.Department)
    .OrderBy(u => u.LastName)
    .ToListAsync();

// Get user with groups
var user = await data.Users
    .Include(u => u.UserInGroups)
        .ThenInclude(ug => ug.Group)
    .FirstOrDefaultAsync(u => u.UserId == userId);
```

---

## Best Practices

1. **Always use async methods**: `ToListAsync()`, `FirstOrDefaultAsync()`, etc.
2. **Include only what you need**: Avoid over-fetching with `.Include()`
3. **Filter early**: Apply `Where()` clauses before `Include()`
4. **Use projections for listings**: Select only needed columns for grids
5. **Check tenant scope**: Every query should be tenant-filtered
6. **Handle soft deletes**: Filter out `Deleted == true` by default
