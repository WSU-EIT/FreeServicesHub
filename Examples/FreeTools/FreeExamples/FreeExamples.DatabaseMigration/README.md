# FreeExamples.DatabaseMigration

> Same-schema database copy/migration tool with dry-run safety, phased execution, live progress, and full CLI automation.

---

## What Is This?

An open-source example of a database migration pattern used across multiple production projects. It copies data from one SQL Server database to another with identical schema using `SqlBulkCopy` for high-speed transfers.

**This is NOT a framework** — it's a self-contained example you can copy and customize for your own projects.

---

## Quick Start

```bash
# 1. Update connection strings (use user-secrets for sensitive values)
dotnet user-secrets set "Migration:SourceDb" "Data Source=prod-server;Initial Catalog=MyApp;..."
dotnet user-secrets set "Migration:TargetDb" "Data Source=(local);Initial Catalog=MyApp;..."

# 2. Run interactively (starts in DRY RUN mode — safe to explore)
dotnet run --project FreeExamples.DatabaseMigration
```

The interactive menu walks you through everything. Start with **option 0** (Verify) to see what's in each database.

---

## Features

| Feature | Description |
|---------|-------------|
| **Dry Run Mode** | Default — previews what would happen without writing data |
| **Phased Migration** | Tables grouped by FK dependency order (A → B → C) |
| **Generic Table Copier** | One method handles ANY table — no entity-specific code |
| **Live Progress** | Batch counter, rows/sec, ETA for large tables |
| **SqlBulkCopy** | High-speed bulk insert (5,000 rows/batch default) |
| **Identity Insert** | Auto-detects identity columns, handles IDENTITY_INSERT |
| **Duplicate Skip** | Loads target PKs into HashSet, skips existing rows |
| **Truncate All** | Wipe target database with FK constraint handling |
| **Data Integrity** | CSV export + SHA256 hash comparison |
| **Column Profiling** | Schema analysis — data types, null counts, min/max |
| **Sample Preview** | View first 3 rows of each table |
| **EF Schema Management** | Create target DB from EF models — fresh migration generation + apply |
| **Full Logging** | Timestamped `.log` files in `runs/` folder |
| **CLI Automation** | Run headless via appsettings or command-line args |
| **Configurable** | appsettings.json + user-secrets + CLI arg overrides |

---

## CLI / Automation Mode

Run without the interactive menu:

```bash
# Verify only (read-only, great for CI health checks)
dotnet run -- --Migration:AutoRun=verify

# Full migration, live mode, skip confirmations
dotnet run -- --Migration:AutoRun=all --Migration:DryRunOnStart=false --Migration:AutoConfirm=true

# Single phase
dotnet run -- --Migration:AutoRun=phaseA

# Truncate target
dotnet run -- --Migration:AutoRun=truncate --Migration:DryRunOnStart=false --Migration:AutoConfirm=true

# Column profiling
dotnet run -- --Migration:AutoRun=profile

# Data integrity check
dotnet run -- --Migration:AutoRun=integrity

# Create target database from EF models (full workflow)
dotnet run -- --Migration:AutoRun=createdb --Migration:DryRunOnStart=false --Migration:AutoConfirm=true

# Generate fresh EF migration files only
dotnet run -- --Migration:AutoRun=efmigrate --Migration:DryRunOnStart=false

# Apply existing EF migration to target database
dotnet run -- --Migration:AutoRun=efupdate --Migration:DryRunOnStart=false

# Override connection strings from CLI
dotnet run -- --Migration:SourceDb="Data Source=server;..." --Migration:AutoRun=verify
```

You can also set `AutoRun` in `appsettings.json` for permanent headless mode.

| AutoRun Value | What It Does |
|---------------|-------------|
| `verify` | Compare source/target row counts |
| `all` | Run all phases (A → C) |
| `phaseA` | Run Phase A only |
| `phaseB` | Run Phase B only |
| `phaseC` | Run Phase C only |
| `truncate` | Wipe all target tables |
| `profile` | Column profiling report |
| `integrity` | CSV + SHA256 comparison |
| `createdb` | Create target DB (fresh EF migration → apply) |
| `efmigrate` | Generate fresh EF migration files only |
| `efupdate` | Apply existing EF migration to target DB |

---

## EF Schema Management

When the target database doesn't exist yet, use these options to create it directly from the EFModels project:

### How It Works

1. **Delete** existing `Migrations/` folder in the EFModels project (clean slate)
2. **Write** a temporary `IDesignTimeDbContextFactory` into the EFModels project (so `dotnet ef` can instantiate the context)
3. **Run** `dotnet ef migrations add InitialMigration` to generate fresh migration files
4. **Run** `dotnet ef database update` to create/update the database with the target connection string
5. **Delete** the temporary factory file (always, even on failure)

### Why Fresh Migrations?

EF migration files are snapshots of the model at a point in time. If the model has changed since the last migration was generated, the files are stale. By deleting and regenerating every time, we guarantee the migration matches the **current** model. This is safe because we're creating a brand new database — there's no existing data to preserve.

### Menu Options

| Key | Description |
|-----|-------------|
| **E** | Full workflow: delete old migrations → generate fresh → apply to target DB |
| **M** | Generate fresh migration files only (no database update) |
| **U** | Apply existing migration to target DB (creates DB if missing) |

### Configuration

Set these in `appsettings.json`:

```json
{
  "Migration": {
    "EfModelsProject": "FreeExamples\\FreeExamples.EFModels\\FreeExamples.EFModels.csproj",
    "SolutionRoot": "%USERPROFILE%\\source\\repos\\WSU-EIT\\FreeTools"
  }
}
```

| Key | Description |
|-----|-------------|
| `EfModelsProject` | Relative path from solution root to the EFModels `.csproj` file |
| `SolutionRoot` | Absolute path to solution root (supports `%USERPROFILE%`). Leave empty to auto-detect |

---

## Configuration

### appsettings.json

All configuration lives in the `"Migration"` section. See the file for full documentation of every option.

### User Secrets (Recommended for Connection Strings)

```bash
dotnet user-secrets init
dotnet user-secrets set "Migration:SourceDb" "your-source-connection-string"
dotnet user-secrets set "Migration:TargetDb" "your-target-connection-string"
```

### Customizing Table Phases

Edit the phase arrays in `appsettings.json` to match your schema:

```json
{
  "Migration": {
    "PhaseA": [ "Tenants", "Settings" ],
    "PhaseB": [ "Users", "Departments" ],
    "PhaseC": [ "Orders", "OrderItems", "Invoices" ]
  }
}
```

**Rule:** Parent tables first, child tables after. The generic copier handles the rest.

---

## How to Repurpose

### Same-Schema Copy (Simplest)

1. Update `appsettings.json` with your connection strings
2. Update the Phase arrays with your table names
3. Run it — the generic table copier handles everything

### Cross-Schema Migration (Different Column Names)

Look for the `TRANSFORM HOOK` comments in `Program.cs` → `MigrateTable()`:

```csharp
// Instead of:
row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);

// Map a renamed column:
if (sourceColumns[i] == "OldName")
    row[targetIndex] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);

// Transform a value:
if (sourceColumns[i] == "Status")
    row[i] = MapStatusValue(reader.GetInt32(i));
```

### ID Type Changes (int → GUID)

Look for the `ID TRANSFORM HOOK` comments. Add a tracking table and replace PKs during migration:

```csharp
var newGuid = Guid.NewGuid();
row[pkOrdinal] = newGuid;
// Record mapping for FK resolution in later phases
```

---

## Pattern Origin

This tool is based on patterns from three internal projects:

| Tool | Approach | Key Feature |
|------|----------|-------------|
| **Touchpoints.DatabaseImport** | Cross-schema (V1→V2) | Column mapping, integrity verification |
| **Touchpoints.DatabaseImportV2** | Same-schema (V2→V2) | Generic table copier (this project's base) |
| **ACP.MigrationTool** | ID transform (int→GUID) | FK mapping, appsettings.json config |

See `docs/300_research.migration_tools.md` for the full analysis.

---

## Output

### Log Files

Every run creates a timestamped log in `runs/`:
```
runs/migration-20250725-143022.log
```

### Column Profile

The profiling feature creates a detailed report:
```
runs/column-profile.txt
```

### Integrity Verification

CSV exports for comparison:
```
runs/data/latest/source/Users.csv
runs/data/latest/target/Users.csv
```

---

*Part of the FreeExamples suite — see the main project for more examples.*
