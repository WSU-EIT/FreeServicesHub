namespace FreeExamples;

public partial class DataObjects
{
    // ── Category 2: Tickets ──

    public class Ticket : IJsonEntity
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }
        public static string EntityType => "Ticket";
        public static int CurrentSchemaVersion => 1;

        public Guid ProjectId { get; set; }
        public string TicketNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public TicketType Type { get; set; }
        public TicketStatus Status { get; set; }
        public TicketPriority Priority { get; set; }
        public string? AssignedTo { get; set; }
        public string ReporterName { get; set; } = "";
        public int? StoryPoints { get; set; }
        public string? Labels { get; set; }
        public Guid? SprintId { get; set; }
        public Guid? ParentTicketId { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public int SortOrder { get; set; }
        public List<TicketComment> Comments { get; set; } = [];
    }

    public enum TicketType { Epic, Story, Task, Bug, Improvement, SubTask }
    public enum TicketStatus { Backlog, ToDo, InProgress, InReview, Testing, Done, Closed, Wontfix }
    public enum TicketPriority { Critical, High, Medium, Low, Trivial }

    public class TicketComment
    {
        public Guid CommentId { get; set; }
        public string AuthorName { get; set; } = "";
        public string Body { get; set; } = "";
        public bool IsInternal { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? EditedDate { get; set; }
    }

    public class FilterTickets : FilterJsonRecords<Ticket>
    {
        public string? Status { get; set; }
        public string? Type { get; set; }
        public string? Priority { get; set; }
        public string? AssignedTo { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? SprintId { get; set; }
    }
}
