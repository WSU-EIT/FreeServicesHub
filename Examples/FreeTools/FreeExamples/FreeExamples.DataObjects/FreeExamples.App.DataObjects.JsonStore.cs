namespace FreeExamples;

public partial class DataObjects
{
    /// <summary>
    /// Generic JSON envelope — wraps any entity type as a JSON blob with metadata.
    /// All entity types share one ConcurrentDictionary&lt;Guid, JsonRecord&gt;.
    /// Two-phase parse: check metadata first, then deserialize Contents.
    /// See doc 113 for full architecture.
    /// </summary>
    public class JsonRecord
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }

        // --- Metadata (parsed first, before touching Contents) ---
        public string RecordType { get; set; } = "";
        public int SchemaVersion { get; set; } = 1;
        public string Format { get; set; } = "json";

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

    /// <summary>
    /// Contract for all entities stored in the generic JSON store.
    /// Each entity class provides EntityType and CurrentSchemaVersion as static members.
    /// </summary>
    public interface IJsonEntity
    {
        Guid RecordId { get; set; }
        Guid TenantId { get; set; }
        static abstract string EntityType { get; }
        static abstract int CurrentSchemaVersion { get; }
    }

    /// <summary>
    /// Generic filter DTO for paginated/filtered entity queries.
    /// Entity-specific filters inherit from this and add their own filter properties.
    /// </summary>
    public class FilterJsonRecords<T> : Filter where T : class
    {
        public List<T>? Records { get; set; }
    }
}
