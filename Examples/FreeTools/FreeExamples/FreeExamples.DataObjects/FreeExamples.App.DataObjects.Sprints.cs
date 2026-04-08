namespace FreeExamples;

public partial class DataObjects
{
    // ── Category 3: Board Views (configuration entity) ──

    public class BoardConfig : IJsonEntity
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }
        public static string EntityType => "BoardConfig";
        public static int CurrentSchemaVersion => 1;

        public Guid ProjectId { get; set; }
        public string BoardName { get; set; } = "";
        public BoardType BoardType { get; set; }
        public string? ColumnConfig { get; set; }
        public string? SwimlaneField { get; set; }
        public string? WipLimits { get; set; }
        public string? FilterPreset { get; set; }
        public string CreatedBy { get; set; } = "";
    }

    public enum BoardType { Kanban, Sprint }

    // ── Category 4: Sprint Planning ──

    public class Sprint : IJsonEntity
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }
        public static string EntityType => "Sprint";
        public static int CurrentSchemaVersion => 1;

        public Guid ProjectId { get; set; }
        public string Name { get; set; } = "";
        public string? Goal { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public SprintStatus Status { get; set; }
        public int? CapacityPoints { get; set; }
    }

    public enum SprintStatus { Planning, Active, Completed, Cancelled }

    public class FilterSprints : FilterJsonRecords<Sprint>
    {
        public string? Status { get; set; }
        public Guid? ProjectId { get; set; }
    }

    // ── Category 5: Backlog Saved Views ──

    public class SavedView : IJsonEntity
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }
        public static string EntityType => "SavedView";
        public static int CurrentSchemaVersion => 1;

        public string Name { get; set; } = "";
        public Guid? ProjectId { get; set; }
        public string? FilterJson { get; set; }
        public string? SortJson { get; set; }
        public string? GroupByField { get; set; }
        public string CreatedBy { get; set; } = "";
    }
}
