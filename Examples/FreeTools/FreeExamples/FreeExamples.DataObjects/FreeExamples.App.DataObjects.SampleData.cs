namespace FreeExamples;

public partial class DataObjects
{
    /// <summary>
    /// A sample item used across all FreeExamples demo pages.
    /// Demonstrates common field types: string, bool, int, decimal, DateTime, Guid, enum.
    /// </summary>
    public class SampleItem
    {
        public Guid SampleItemId { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Category { get; set; }
        public SampleItemStatus Status { get; set; }
        public int Priority { get; set; }
        public decimal Amount { get; set; }
        public bool Enabled { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime Added { get; set; }
        public string? AddedBy { get; set; }
        public DateTime LastModified { get; set; }
        public string? LastModifiedBy { get; set; }
        public bool Deleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public enum SampleItemStatus
    {
        Draft = 0,
        Active = 1,
        Completed = 2,
        Archived = 3,
    }

    /// <summary>
    /// Filter DTO extending the base Filter class for SampleItems list page.
    /// </summary>
    public partial class FilterSampleItems : Filter
    {
        public List<SampleItem>? Records { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Enabled { get; set; }
        public List<string> AvailableCategories { get; set; } = [];
    }

    /// <summary>
    /// Response wrapper for the three-endpoint CRUD pattern.
    /// </summary>
    public class SampleDataResponse : ActionResponseObject
    {
        public List<SampleItem> Items { get; set; } = [];
    }

    /// <summary>
    /// Response for server-side file generation endpoints.
    /// </summary>
    public class SampleFileResponse
    {
        public string FileName { get; set; } = "";
        public byte[] FileData { get; set; } = [];
    }

    /// <summary>
    /// Network graph node for the vis.js demo page.
    /// </summary>
    public class SampleGraphNode
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
        public string? Group { get; set; }
    }

    /// <summary>
    /// Network graph edge for the vis.js demo page.
    /// </summary>
    public class SampleGraphEdge
    {
        public int From { get; set; }
        public int To { get; set; }
        public string? Label { get; set; }
    }

    /// <summary>
    /// Full graph data for the network visualization demo.
    /// </summary>
    public class SampleGraphData
    {
        public List<SampleGraphNode> Nodes { get; set; } = [];
        public List<SampleGraphEdge> Edges { get; set; } = [];
    }

    /// <summary>
    /// Dashboard summary data returned from a single endpoint.
    /// Reused by the dashboard, charts, and reporting demo pages.
    /// </summary>
    public class SampleDashboard
    {
        public int TotalItems { get; set; }
        public int ActiveItems { get; set; }
        public int CompletedItems { get; set; }
        public int DraftItems { get; set; }
        public int ArchivedItems { get; set; }
        public List<SampleCategorySummary> ByCategory { get; set; } = [];
        public List<SampleTimelineSummary> ByMonth { get; set; } = [];
    }

    public class SampleCategorySummary
    {
        public string Category { get; set; } = "";
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class SampleTimelineSummary
    {
        public string Month { get; set; } = "";
        public int Added { get; set; }
        public int Completed { get; set; }
    }

    public partial class SignalRUpdateType
    {
        public const string SampleItemSaved = "SampleItemSaved";
        public const string SampleItemDeleted = "SampleItemDeleted";
        public const string CommentSaved = "CommentSaved";
        public const string CommentDeleted = "CommentDeleted";
    }
}
