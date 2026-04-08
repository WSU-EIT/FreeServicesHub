namespace FreeExamples;

public partial class DataObjects
{
    // ── Category 8: Equipment Checkout ──

    public class Equipment : IJsonEntity
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }
        public static string EntityType => "Equipment";
        public static int CurrentSchemaVersion => 1;

        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public EquipmentCategory Category { get; set; }
        public string? SerialNumber { get; set; }
        public string AssetTag { get; set; } = "";
        public string Location { get; set; } = "";
        public EquipmentCondition Condition { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchasePrice { get; set; }
        public List<EquipmentCheckout> Checkouts { get; set; } = [];
    }

    public enum EquipmentCategory { Laptop, Projector, Camera, Microphone, Tablet, Hotspot, Adapter, Other }
    public enum EquipmentCondition { New, Good, Fair, NeedsRepair, Retired }

    public class EquipmentCheckout
    {
        public Guid CheckoutId { get; set; }
        public string BorrowerName { get; set; } = "";
        public string BorrowerEmail { get; set; } = "";
        public string? BorrowerDepartment { get; set; }
        public DateTime CheckoutDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public EquipmentCondition ConditionAtCheckout { get; set; }
        public EquipmentCondition? ConditionAtReturn { get; set; }
        public string? Notes { get; set; }
    }

    public class FilterEquipment : FilterJsonRecords<Equipment>
    {
        public string? Category { get; set; }
        public string? Availability { get; set; }
        public string? Condition { get; set; }
    }
}
