namespace FreeExamples;

public partial class DataObjects
{
    // ── Category 7: Budget & Approvals ──

    public class BudgetRequest : IJsonEntity
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }
        public static string EntityType => "BudgetRequest";
        public static int CurrentSchemaVersion => 1;

        public string Title { get; set; } = "";
        public string Justification { get; set; } = "";
        public string Department { get; set; } = "";
        public string FiscalYear { get; set; } = "";
        public Guid? ProjectId { get; set; }
        public string RequestedBy { get; set; } = "";
        public DateTime RequestedDate { get; set; }
        public BudgetRequestStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public string? DenialReason { get; set; }
        public string? SupervisorName { get; set; }
        public DateTime? SupervisorDate { get; set; }
        public string? FinanceReviewerName { get; set; }
        public DateTime? FinanceDate { get; set; }
        public string AccountCode { get; set; } = "";
        public List<BudgetLineItem> LineItems { get; set; } = [];
    }

    public enum BudgetRequestStatus { Draft, Submitted, SupervisorApproved, FinanceReview, Approved, Denied, Completed }

    public class BudgetLineItem
    {
        public Guid LineItemId { get; set; }
        public string Description { get; set; } = "";
        public string? Vendor { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public BudgetLineCategory Category { get; set; }
        public string? Notes { get; set; }
    }

    public enum BudgetLineCategory { Supplies, Equipment, Software, Travel, Services, Other }

    public class FilterBudgetRequests : FilterJsonRecords<BudgetRequest>
    {
        public string? Status { get; set; }
        public string? Department { get; set; }
        public string? FiscalYear { get; set; }
    }
}
