namespace FreeExamples;

public partial class DataObjects
{
    // ── Category 1: Projects & Hierarchy ──

    public class Project : IJsonEntity
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }
        public static string EntityType => "Project";
        public static int CurrentSchemaVersion => 1;

        public Guid? ParentProjectId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string ProjectKey { get; set; } = "";
        public string LeadName { get; set; } = "";
        public string? LeadEmail { get; set; }
        public string? Department { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? TargetEndDate { get; set; }
        public string? Color { get; set; }
        public int SortOrder { get; set; }
        public int NextTicketNumber { get; set; } = 1;
    }

    public enum ProjectStatus { Planning, Active, OnHold, Completed, Archived }

    public class FilterProjects : FilterJsonRecords<Project>
    {
        public string? Status { get; set; }
        public string? Department { get; set; }
        public Guid? ParentProjectId { get; set; }
    }
}
