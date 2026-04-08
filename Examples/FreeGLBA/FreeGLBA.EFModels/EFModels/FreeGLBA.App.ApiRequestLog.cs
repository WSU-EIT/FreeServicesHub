using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FreeGLBA.EFModels.EFModels;

// ============================================================================
// API REQUEST LOGGING - ApiRequestLogItem Entity
// Stores all API request/response data for compliance and debugging
// ============================================================================

[Table("ApiRequestLogs")]
[Index(nameof(SourceSystemId), nameof(RequestedAt))]
[Index(nameof(RequestedAt))]
[Index(nameof(StatusCode))]
[Index(nameof(CorrelationId))]
public partial class ApiRequestLogItem
{
    [Key]
    public Guid ApiRequestLogId { get; set; }
    
    // === WHO ===
    public Guid SourceSystemId { get; set; }
    
    [MaxLength(200)]
    public string SourceSystemName { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string UserName { get; set; } = string.Empty;
    
    public Guid? TenantId { get; set; }
    
    // === WHAT ===
    [MaxLength(10)]
    public string HttpMethod { get; set; } = string.Empty;  // GET, POST, etc.
    
    [MaxLength(500)]
    public string RequestPath { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string QueryString { get; set; } = string.Empty;
    
    public string RequestHeaders { get; set; } = string.Empty;  // JSON
    
    public string RequestBody { get; set; } = string.Empty;  // Truncated to 4KB
    
    public int RequestBodySize { get; set; }  // Actual size before truncation
    
    // === WHEN ===
    public DateTime RequestedAt { get; set; }
    
    public DateTime RespondedAt { get; set; }
    
    public long DurationMs { get; set; }
    
    // === WHERE ===
    [MaxLength(50)]
    public string IpAddress { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string UserAgent { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string ForwardedFor { get; set; } = string.Empty;
    
    // === RESULT ===
    public int StatusCode { get; set; }
    
    public bool IsSuccess { get; set; }
    
    public string ResponseBody { get; set; } = string.Empty;  // Truncated
    
    public int ResponseBodySize { get; set; }
    
    [MaxLength(1000)]
    public string ErrorMessage { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string ExceptionType { get; set; } = string.Empty;
    
    // === TRACING ===
    [MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string AuthType { get; set; } = string.Empty;  // ApiKey, JWT, etc.
    
    [MaxLength(100)]
    public string RelatedEntityId { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string RelatedEntityType { get; set; } = string.Empty;
    
    public bool BodyLoggingEnabled { get; set; }
}
