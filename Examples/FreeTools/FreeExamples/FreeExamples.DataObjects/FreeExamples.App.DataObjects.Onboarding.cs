namespace FreeExamples;

public partial class DataObjects
{
    // ── Category 10: Employee Onboarding ──

    public class Onboarding : IJsonEntity
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }
        public static string EntityType => "Onboarding";
        public static int CurrentSchemaVersion => 1;

        public string EmployeeName { get; set; } = "";
        public string EmployeeEmail { get; set; } = "";
        public string EmployeeTitle { get; set; } = "";
        public string Department { get; set; } = "";
        public DateTime HireDate { get; set; }
        public DateTime StartDate { get; set; }
        public string SupervisorName { get; set; } = "";
        public string? MentorName { get; set; }
        public EmploymentType EmploymentType { get; set; }
        public OnboardingStatus Status { get; set; }
        public string? Notes { get; set; }
        public List<ChecklistItem> ChecklistItems { get; set; } = [];
    }

    public enum EmploymentType { FullTime, PartTime, Temporary, GradAssistant, StudentWorker }
    public enum OnboardingStatus { Pending, InProgress, Completed, Withdrawn }

    public class ChecklistItem
    {
        public Guid ChecklistItemId { get; set; }
        public string TaskName { get; set; } = "";
        public string? Description { get; set; }
        public ChecklistCategory Category { get; set; }
        public string AssignedTo { get; set; } = "";
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool IsCompleted { get; set; }
        public string? CompletedBy { get; set; }
        public string? Notes { get; set; }
        public int DisplayOrder { get; set; }
    }

    public enum ChecklistCategory { HR, IT, Facilities, Department, Training, Compliance }

    public class FilterOnboarding : FilterJsonRecords<Onboarding>
    {
        public string? Status { get; set; }
        public string? Department { get; set; }
        public string? EmploymentType { get; set; }
    }
}
