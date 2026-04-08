namespace FreeExamples;

public partial class DataObjects
{
    // ── Category 6: Work Orders ──

    public class WorkOrder : IJsonEntity
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }
        public static string EntityType => "WorkOrder";
        public static int CurrentSchemaVersion => 1;

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Building { get; set; } = "";
        public string Floor { get; set; } = "";
        public string RoomNumber { get; set; } = "";
        public WorkOrderCategory Category { get; set; }
        public WorkOrderUrgency Urgency { get; set; }
        public WorkOrderStatus Status { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedTeam { get; set; }
        public string RequestedBy { get; set; } = "";
        public string RequestedByEmail { get; set; } = "";
        public DateTime RequestedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string? CompletionNotes { get; set; }
        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
    }

    public enum WorkOrderCategory { Plumbing, Electrical, HVAC, Custodial, Grounds, Locksmith, Other }
    public enum WorkOrderUrgency { Low, Normal, High, Emergency }
    public enum WorkOrderStatus { Submitted, Assigned, InProgress, OnHold, Completed, Closed }

    public class FilterWorkOrders : FilterJsonRecords<WorkOrder>
    {
        public string? Status { get; set; }
        public string? Urgency { get; set; }
        public string? Building { get; set; }
        public string? Category { get; set; }
    }
}
