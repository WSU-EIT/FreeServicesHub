using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FreeGLBA.EFModels.EFModels;

// ============================================================================
// API REQUEST LOGGING - BodyLoggingConfigItem Entity
// Audit trail for body logging configuration (PII-sensitive feature)
// ============================================================================

[Table("BodyLoggingConfigs")]
[Index(nameof(SourceSystemId), nameof(IsActive))]
public partial class BodyLoggingConfigItem
{
    [Key]
    public Guid BodyLoggingConfigId { get; set; }
    
    // Which source system
    public Guid SourceSystemId { get; set; }
    
    // Who enabled it
    public Guid EnabledByUserId { get; set; }
    
    [MaxLength(200)]
    public string EnabledByUserName { get; set; } = string.Empty;
    
    // When
    public DateTime EnabledAt { get; set; }
    
    public DateTime ExpiresAt { get; set; }
    
    public DateTime? DisabledAt { get; set; }  // Null until disabled
    
    // Status
    public bool IsActive { get; set; }
    
    // Why
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
