using System.Collections.Concurrent;
using System.Text.Json;

namespace FreeExamples;

public partial interface IDataAccess
{
    List<T> GetJsonRecords<T>(List<Guid>? ids) where T : class, DataObjects.IJsonEntity;
    List<T> SaveJsonRecords<T>(List<T> records, DataObjects.User? currentUser) where T : class, DataObjects.IJsonEntity;
    DataObjects.BooleanResponse DeleteJsonRecords<T>(List<Guid>? ids) where T : class, DataObjects.IJsonEntity;
}

public partial class DataAccess
{
    private static readonly ConcurrentDictionary<Guid, DataObjects.JsonRecord> _jsonStore = new();
    private static bool _jsonDataSeeded = false;

    private static readonly JsonSerializerOptions _jsonOptions = new() {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private void SeedJsonDataIfNeeded()
    {
        if (_jsonDataSeeded) return;
        _jsonDataSeeded = true;

        SeedProjects();
        SeedTickets();
        SeedSprints();
        SeedBoardConfigs();
        SeedWorkOrders();
        SeedBudgetRequests();
        SeedEquipment();
        SeedEvaluations();
        SeedOnboarding();
    }

    private T? TryDeserialize<T>(DataObjects.JsonRecord record) where T : class, DataObjects.IJsonEntity
    {
        if (record.RecordType != T.EntityType) return null;
        if (record.SchemaVersion > T.CurrentSchemaVersion) return null;

        try {
            return JsonSerializer.Deserialize<T>(record.Contents, _jsonOptions);
        } catch {
            return null;
        }
    }

    public List<T> GetJsonRecords<T>(List<Guid>? ids) where T : class, DataObjects.IJsonEntity
    {
        SeedJsonDataIfNeeded();
        string entityType = T.EntityType;

        IEnumerable<DataObjects.JsonRecord> query = _jsonStore.Values
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

    public List<T> SaveJsonRecords<T>(List<T> records, DataObjects.User? currentUser) where T : class, DataObjects.IJsonEntity
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
                existing.SchemaVersion = T.CurrentSchemaVersion;
                existing.Modified = DateTime.UtcNow;
                existing.ModifiedBy = modifiedBy;
                existing.Contents = json;
            } else {
                _jsonStore[entity.RecordId] = new DataObjects.JsonRecord {
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

    public DataObjects.BooleanResponse DeleteJsonRecords<T>(List<Guid>? ids) where T : class, DataObjects.IJsonEntity
    {
        SeedJsonDataIfNeeded();
        var output = new DataObjects.BooleanResponse();

        if (ids == null || ids.Count == 0) {
            output.Messages.Add("You must provide IDs to delete.");
            return output;
        }

        int deleted = 0;
        foreach (var id in ids) {
            if (_jsonStore.TryGetValue(id, out var record) && record.RecordType == T.EntityType) {
                record.Deleted = true;
                record.DeletedAt = DateTime.UtcNow;
                deleted++;
            }
        }

        output.Result = true;
        output.Messages.Add($"Deleted {deleted} record(s).");
        return output;
    }
}
