using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreeGLBA.EFModels.EFModels;

/// <summary>
/// ComplianceReport entity - stored in [ComplianceReports] table.
/// </summary>
[Table("ComplianceReports")]
public partial class ComplianceReportItem
{
    [Key]
    public Guid ComplianceReportId { get; set; }

    [MaxLength(50)]
    public string ReportType { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; }

    [MaxLength(200)]
    public string GeneratedBy { get; set; } = string.Empty;

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public long TotalEvents { get; set; } = 0;

    public int UniqueUsers { get; set; } = 0;

    public int UniqueSubjects { get; set; } = 0;

    public string ReportData { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;

}
