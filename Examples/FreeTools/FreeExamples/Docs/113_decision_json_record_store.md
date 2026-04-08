# 113 — Architecture: Generic JSON Record Store

> **Document ID:** 113  
> **Category:** Decision  
> **Purpose:** How we store all new example entities (Projects, Tickets, Work Orders, etc.) without database migrations.  
> **Audience:** Devs, AI agents.  
> **Outcome:** ✅ Single generic store for unlimited entity types, zero EF migrations, schema-free evolution.

---

## The Problem

We're adding 10+ new entity types (Projects, Tickets, Sprints, Work Orders, Budget Requests, etc.) to FreeExamples. Each has a unique data shape with different fields, enums, and sub-records.

**We do NOT want:**
- A new database table per entity type
- EF model migrations every time a schema changes
- Separate `ConcurrentDictionary` + seed method + CRUD methods per entity

**We DO want:**
- Strongly typed C# classes for each entity (autocomplete, compile-time safety)
- One generic store that holds any entity type as a JSON blob
- Schema changes = just update the C# class, no migration
- Version metadata so stale records can be detected
- The same GetMany/SaveMany/DeleteMany pattern we already use

---

## The Design

### Two-Phase Parse with Envelope + Contents

Every record is stored as a `JsonRecord` — a metadata envelope wrapping a JSON-serialized entity. Reading is a two-phase operation:

1. **Phase 1:** Deserialize the envelope → get `RecordType`, `SchemaVersion`, `Format`
2. **Phase 2:** Only if metadata is compatible → deserialize `Contents` into the typed entity

This means a corrupt or outdated blob never crashes the app — it fails at the metadata check, not at deserialization.

---

## Data Model

### JsonRecord — The Envelope

```csharp
// In: FreeExamples.App.DataObjects.JsonStore.cs

public class JsonRecord
{
    public Guid RecordId { get; set; }
    public Guid TenantId { get; set; }

    // --- Metadata (parsed first, before touching Contents) ---
    public string RecordType { get; set; } = "";       // discriminator: "Project", "Ticket", etc.
    public int SchemaVersion { get; set; } = 1;         // version of the entity shape
    public string Format { get; set; } = "json";        // serialization format (always "json" for now)

    // --- Audit (tracked by the store, not by the entity) ---
    public DateTime Created { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime Modified { get; set; }
    public string? ModifiedBy { get; set; }
    public bool Deleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // --- Payload (the actual entity, JSON-serialized) ---
    public string Contents { get; set; } = "";
}
```

**Why `Contents` as a string (not `JsonElement` or `object`):**
- Clear two-phase boundary — you have the metadata in typed properties, and `Contents` is an opaque blob until you choose to deserialize it
- If persisted to a real database later, `Contents` is just an `nvarchar(max)` column
- No ambiguity about what "parsing the inner blob" means

### IJsonEntity — The Entity Contract

```csharp
public interface IJsonEntity
{
    Guid RecordId { get; set; }
    Guid TenantId { get; set; }

    // Each entity class provides these as static members:
    static abstract string EntityType { get; }        // "Project", "Ticket", etc.
    static abstract int CurrentSchemaVersion { get; }  // bump when shape changes
}
```

**Why `static abstract`:** .NET 7+ supports static abstract interface members. Each entity class declares its own type name and version at compile time — no magic strings in calling code, no runtime reflection.

### Example Entity

```csharp
public class Project : IJsonEntity
{
    // --- IJsonEntity ---
    public Guid RecordId { get; set; }
    public Guid TenantId { get; set; }
    public static string EntityType => "Project";
    public static int CurrentSchemaVersion => 1;

    // --- Entity fields ---
    public Guid? ParentProjectId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ProjectKey { get; set; } = "";
    public string LeadName { get; set; } = "";
    public ProjectStatus Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
}

public enum ProjectStatus { Planning, Active, OnHold, Completed, Archived }
```

**Note:** Entities don't carry audit fields (Created, Modified, Deleted). The `JsonRecord` envelope owns those. The entity is pure domain data.

---

## Storage Layer

### One Dictionary, All Entities

```csharp
// In: FreeExamples.App.DataAccess.JsonStore.cs

private static readonly ConcurrentDictionary<Guid, JsonRecord> _jsonStore = new();
```

All entity types share one store. The `RecordType` field is the discriminator.

### Generic CRUD Methods

```csharp
public partial interface IDataAccess
{
    List<T> GetJsonRecords<T>(List<Guid>? ids) where T : class, IJsonEntity;
    DataObjects.FilterJsonRecords<T> GetJsonRecordsFiltered<T>(DataObjects.FilterJsonRecords<T> filter) where T : class, IJsonEntity;
    List<T> SaveJsonRecords<T>(List<T> records, DataObjects.User? currentUser) where T : class, IJsonEntity;
    DataObjects.BooleanResponse DeleteJsonRecords<T>(List<Guid>? ids) where T : class, IJsonEntity;
}
```

### GetMany Implementation

```csharp
public List<T> GetJsonRecords<T>(List<Guid>? ids) where T : class, IJsonEntity
{
    SeedJsonDataIfNeeded();
    string entityType = T.EntityType;  // static abstract member

    IEnumerable<JsonRecord> query = _jsonStore.Values
        .Where(r => r.RecordType == entityType);

    if (ids != null && ids.Count > 0) {
        query = query.Where(r => ids.Contains(r.RecordId));
    } else {
        query = query.Where(r => !r.Deleted);
    }

    var results = new List<T>();
    foreach (var record in query) {
        var entity = TryDeserialize<T>(record);
        if (entity != null) results.Add(entity);
    }

    return results;
}
```

### Two-Phase Deserialize

```csharp
private T? TryDeserialize<T>(JsonRecord record) where T : class, IJsonEntity
{
    // Phase 1: Metadata check (already parsed — it's in typed properties)
    if (record.RecordType != T.EntityType) return null;
    if (record.SchemaVersion > T.CurrentSchemaVersion) return null;  // future version we can't read

    // Phase 2: Deserialize Contents into the typed entity
    try {
        var entity = JsonSerializer.Deserialize<T>(record.Contents, _jsonOptions);
        return entity;
    } catch {
        // Blob doesn't match current schema — skip it, don't crash
        return null;
    }
}
```

**Version compatibility rules:**
- `record.SchemaVersion == T.CurrentSchemaVersion` → exact match, deserialize normally
- `record.SchemaVersion < T.CurrentSchemaVersion` → old format, JSON will have missing fields → they default to null/0/false, which is fine
- `record.SchemaVersion > T.CurrentSchemaVersion` → future format we don't understand → skip (return null)

### SaveMany Implementation

```csharp
public List<T> SaveJsonRecords<T>(List<T> records, DataObjects.User? currentUser) where T : class, IJsonEntity
{
    SeedJsonDataIfNeeded();
    string modifiedBy = currentUser?.DisplayName ?? "Unknown";
    var saved = new List<T>();

    foreach (var entity in records) {
        if (entity.RecordId == Guid.Empty) {
            entity.RecordId = Guid.NewGuid();
        }

        var json = JsonSerializer.Serialize(entity, _jsonOptions);

        if (_jsonStore.TryGetValue(entity.RecordId, out var existing)) {
            // Update: keep Created/CreatedBy, update Modified and Contents
            existing.SchemaVersion = T.CurrentSchemaVersion;
            existing.Modified = DateTime.UtcNow;
            existing.ModifiedBy = modifiedBy;
            existing.Contents = json;
        } else {
            // Insert
            _jsonStore[entity.RecordId] = new JsonRecord {
                RecordId = entity.RecordId,
                TenantId = entity.TenantId,
                RecordType = T.EntityType,
                SchemaVersion = T.CurrentSchemaVersion,
                Format = "json",
                Created = DateTime.UtcNow,
                CreatedBy = modifiedBy,
                Modified = DateTime.UtcNow,
                ModifiedBy = modifiedBy,
                Contents = json,
            };
        }

        saved.Add(entity);
    }

    return saved;
}
```

---

## Generic Filter DTO

```csharp
public class FilterJsonRecords<T> : Filter where T : class
{
    public List<T>? Records { get; set; }

    // Entity-specific filter properties go here via inheritance:
    // public class FilterProjects : FilterJsonRecords<Project> { public string? Status { get; set; } }
}
```

---

## API Layer

One generic controller base, then thin wrappers per entity type:

```csharp
// Generic endpoints follow the same three-endpoint pattern:
[HttpPost("api/Data/GetProjects")]
public ActionResult<List<Project>> GetProjects([FromBody] List<Guid>? ids)
    => Ok(DataAccess.GetJsonRecords<Project>(ids));

[HttpPost("api/Data/SaveProjects")]
public ActionResult<List<Project>> SaveProjects([FromBody] List<Project> items)
    => Ok(DataAccess.SaveJsonRecords(items, CurrentUser));

[HttpPost("api/Data/DeleteProjects")]
public ActionResult<BooleanResponse> DeleteProjects([FromBody] List<Guid>? ids)
    => Ok(DataAccess.DeleteJsonRecords<Project>(ids));
```

Three thin endpoints per entity. No entity-specific logic in the controller — it's all just `GetJsonRecords<T>`, `SaveJsonRecords<T>`, `DeleteJsonRecords<T>`.

---

## Sub-Records (Comments, Line Items, Checklist Items, etc.)

Sub-records are **embedded in the parent entity's JSON**, not stored as separate JsonRecords:

```csharp
public class Ticket : IJsonEntity
{
    // ... ticket fields ...
    public List<TicketComment> Comments { get; set; } = [];
}

public class TicketComment  // NOT IJsonEntity — just a nested POCO
{
    public Guid CommentId { get; set; }
    public string AuthorName { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsInternal { get; set; }
    public DateTime CreatedDate { get; set; }
}
```

**Why embedded, not separate:** These sub-records are always loaded with the parent. You never query "all comments across all tickets." Keeping them inline means one store read = one complete entity with all its children. Simpler code, fewer lookups.

**Exception:** If a sub-record type needs to be queried independently (e.g., "all tickets across all projects"), it gets its own `IJsonEntity` and its own RecordType in the store. The parent holds just the Guid references.

---

## Seed Data

One `SeedJsonDataIfNeeded()` method replaces the per-entity seed methods:

```csharp
private static bool _jsonDataSeeded = false;

private void SeedJsonDataIfNeeded()
{
    if (_jsonDataSeeded) return;
    _jsonDataSeeded = true;

    SeedProjects();
    SeedTickets();
    SeedSprints();
    // ... each category has a private seed method
}
```

Each seed method creates typed entities and saves them through `SaveJsonRecords<T>()` — so the envelope wrapping happens automatically.

---

## What This Gets Us

| Concern | Solution |
|---------|----------|
| No EF migrations ever | ✅ In-memory ConcurrentDictionary, JSON strings |
| Schema changes are free | ✅ Update the C# class, bump `CurrentSchemaVersion`, done |
| Stale blobs don't crash | ✅ Two-phase parse — metadata check before deserialization |
| Strongly typed entities | ✅ Full C# classes with enums, lists, nullables |
| One store for everything | ✅ Single `ConcurrentDictionary<Guid, JsonRecord>`, discriminated by `RecordType` |
| Three-endpoint CRUD pattern | ✅ Generic `GetJsonRecords<T>` / `SaveJsonRecords<T>` / `DeleteJsonRecords<T>` |
| Sub-records (comments, line items) | ✅ Embedded in parent JSON, no separate storage |
| Future persistence option | ✅ `JsonRecord` maps trivially to a single DB table with an `nvarchar(max)` Contents column |

---

## File Plan

| File | Purpose |
|------|---------|
| `FreeExamples.App.DataObjects.JsonStore.cs` | `JsonRecord`, `IJsonEntity`, `FilterJsonRecords<T>` |
| `FreeExamples.App.DataObjects.Projects.cs` | `Project`, `ProjectStatus`, `FilterProjects` |
| `FreeExamples.App.DataObjects.Tickets.cs` | `Ticket`, `TicketType`, `TicketStatus`, `TicketPriority`, `TicketComment`, `FilterTickets` |
| `FreeExamples.App.DataObjects.Sprints.cs` | `Sprint`, `SprintStatus`, `SavedBoardView`, `FilterSprints` |
| `FreeExamples.App.DataObjects.WorkOrders.cs` | `WorkOrder`, `WorkOrderCategory`, `Urgency`, etc. |
| `FreeExamples.App.DataObjects.BudgetRequests.cs` | `BudgetRequest`, `BudgetLineItem`, etc. |
| ... (one file per entity category) | ... |
| `FreeExamples.App.DataAccess.JsonStore.cs` | Generic CRUD + seed orchestrator |
| `FreeExamples.App.DataAccess.JsonStore.Seed.cs` | Seed methods for all entity types |
| `FreeExamples.App.API.JsonStore.cs` | Three endpoints per entity type (thin wrappers) |

All files follow the `{ProjectName}.App.{Feature}.{Extension}` naming convention from doc 000.

---

## ADR: JSON Envelope Storage

**Context:** Adding 10+ entity types to FreeExamples. Each has a different schema. Traditional approach would require EF migrations for each.

**Decision:** Store all entities as JSON blobs inside a generic `JsonRecord` envelope in a shared in-memory `ConcurrentDictionary`. Strongly typed C# classes serialize/deserialize into the `Contents` field.

**Rationale:**
- Zero database coupling — examples stay portable and self-contained
- Schema evolution is free — change the class, bump the version number
- Follows the existing SampleItems in-memory pattern (familiar)
- Two-phase parse provides graceful degradation for version mismatches
- One generic CRUD implementation serves all entity types

**Consequences:**
- Data resets on app restart (same as SampleItems — this is expected for examples)
- No cross-entity relational queries at the store level (handled in DataAccess with LINQ)
- Sub-records are denormalized (embedded in parent JSON) — acceptable for example data volumes

**Alternatives considered:**
- Separate ConcurrentDictionary per entity type (rejected: code duplication)
- EF Core with migrations (rejected: user explicitly doesn't want this)
- Single generic table in SQLite (possible future upgrade path, but unnecessary for now)

---

*Created: 2025-07-14*  
*Maintained by: [Quality]*
