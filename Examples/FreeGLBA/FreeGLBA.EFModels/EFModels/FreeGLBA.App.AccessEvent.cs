using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreeGLBA.EFModels.EFModels;

/// <summary>
/// AccessEvent entity - stored in [AccessEvents] table.
/// </summary>
[Table("AccessEvents")]
public partial class AccessEventItem
{
    [Key]
    public Guid AccessEventId { get; set; }

    public Guid SourceSystemId { get; set; } = Guid.Empty;

    [MaxLength(200)]
    public string SourceEventId { get; set; } = string.Empty;

    public DateTime AccessedAt { get; set; }

    public DateTime ReceivedAt { get; set; }

    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string UserEmail { get; set; } = string.Empty;

    [MaxLength(200)]
    public string UserDepartment { get; set; } = string.Empty;

    /// <summary>
    /// Primary subject ID for single-subject access. For bulk access, this may contain
    /// "BULK" or the first subject ID as a reference.
    /// </summary>
    [MaxLength(200)]
    public string SubjectId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SubjectType { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of all subject IDs when accessing multiple subjects (e.g., CSV export).
    /// For bulk exports from systems like Touchpoints, this captures all affected individuals.
    /// </summary>
    public string SubjectIds { get; set; } = string.Empty;

    /// <summary>
    /// Count of subjects accessed. For single access = 1, for bulk exports = count of SubjectIds.
    /// Useful for quick reporting without parsing SubjectIds JSON.
    /// </summary>
    public int SubjectCount { get; set; } = 1;

    [MaxLength(100)]
    public string DataCategory { get; set; } = string.Empty;

    [MaxLength(50)]
    public string AccessType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;

    [MaxLength(50)]
    public string IpAddress { get; set; } = string.Empty;

    public string AdditionalData { get; set; } = string.Empty;

    /// <summary>
    /// Copy of the privacy notice/agreement text the user acknowledged when accessing data.
    /// Captures what disclosure was shown at time of access for GLBA compliance.
    /// </summary>
    public string AgreementText { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the user acknowledged the privacy agreement (if different from AccessedAt).
    /// </summary>
    public DateTime? AgreementAcknowledgedAt { get; set; }

    // Navigation properties
    [ForeignKey("SourceSystemId")]
    public virtual SourceSystemItem SourceSystem { get; set; } = null!;
}
