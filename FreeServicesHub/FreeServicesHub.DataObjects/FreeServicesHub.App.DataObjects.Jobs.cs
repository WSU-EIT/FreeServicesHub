namespace FreeServicesHub;

public partial class DataObjects
{
    public class HubJob : ActionResponseObject
    {
        public Guid HubJobId { get; set; }
        public Guid TenantId { get; set; }
        public Guid? AgentId { get; set; }
        public string JobType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public int Priority { get; set; }
        public int MaxRetries { get; set; }
        public int RetryCount { get; set; }
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime LastModified { get; set; }
        public string LastModifiedBy { get; set; } = string.Empty;
        public bool Deleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public static class HubJobStatuses
    {
        public const string Queued = "Queued";
        public const string Assigned = "Assigned";
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
    }
}
