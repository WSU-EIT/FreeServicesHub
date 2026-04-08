namespace FreeGLBA;

// ============================================================================
// API REQUEST LOGGING - Data Transfer OBJECTS
// DTOs, filter parameters, and configuration options
// ============================================================================

public partial class DataObjects
{
    #region API Logging DTOs

    /// <summary>
    /// Full details DTO for single API request log view.
    /// </summary>
    public class ApiRequestLog
    {
        public Guid ApiRequestLogId { get; set; }
        public Guid SourceSystemId { get; set; }
        public string SourceSystemName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        
        public string HttpMethod { get; set; } = string.Empty;
        public string RequestPath { get; set; } = string.Empty;
        public string QueryString { get; set; } = string.Empty;
        public string RequestHeaders { get; set; } = string.Empty;
        public string RequestBody { get; set; } = string.Empty;
        public int RequestBodySize { get; set; }
        
        public DateTime RequestedAt { get; set; }
        public DateTime RespondedAt { get; set; }
        public long DurationMs { get; set; }
        
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string ForwardedFor { get; set; } = string.Empty;
        
        public int StatusCode { get; set; }
        public bool IsSuccess { get; set; }
        public string ResponseBody { get; set; } = string.Empty;
        public int ResponseBodySize { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        
        public string CorrelationId { get; set; } = string.Empty;
        public string AuthType { get; set; } = string.Empty;
        public string RelatedEntityId { get; set; } = string.Empty;
        public string RelatedEntityType { get; set; } = string.Empty;
        public bool BodyLoggingEnabled { get; set; }
    }

    /// <summary>
    /// Summary DTO for list/table view (fewer fields for performance).
    /// </summary>
    public class ApiRequestLogListItem
    {
        public Guid ApiRequestLogId { get; set; }
        public string SourceSystemName { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public string RequestPath { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public long DurationMs { get; set; }
        public int StatusCode { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Body logging configuration DTO.
    /// </summary>
    public class BodyLoggingConfig
    {
        public Guid BodyLoggingConfigId { get; set; }
        public Guid SourceSystemId { get; set; }
        public Guid EnabledByUserId { get; set; }
        public string EnabledByUserName { get; set; } = string.Empty;
        public DateTime EnabledAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? DisabledAt { get; set; }
        public bool IsActive { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dashboard statistics DTO with aggregated data.
    /// </summary>
    public class ApiLogDashboardStats
    {
        public int TotalRequests { get; set; }
        public int TotalErrors { get; set; }
        public double ErrorRate { get; set; }
        public double AvgDurationMs { get; set; }
        public long TotalLogCount { get; set; }
        public long LogsOlderThan7Years { get; set; }
        
        public List<SourceSystemStats> BySourceSystem { get; set; } = new();
        public List<StatusCodeStats> ByStatusCode { get; set; } = new();
        public List<TimeSeriesPoint> RequestsOverTime { get; set; } = new();
        public List<ApiRequestLogListItem> RecentErrors { get; set; } = new();
    }
    
    public class SourceSystemStats
    {
        public string SourceSystemName { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class StatusCodeStats
    {
        public int StatusCode { get; set; }
        public string Category { get; set; } = string.Empty;  // 2xx, 4xx, 5xx
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class TimeSeriesPoint
    {
        public DateTime Timestamp { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Request object for dashboard stats endpoint.
    /// </summary>
    public class ApiLogDashboardRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    /// <summary>
    /// Request object for enabling body logging.
    /// </summary>
    public class EnableBodyLoggingRequest
    {
        public Guid SourceSystemId { get; set; }
        public Guid EnabledByUserId { get; set; }
        public string EnabledByUserName { get; set; } = string.Empty;
        public int DurationHours { get; set; } = 24;
        public string Reason { get; set; } = string.Empty;
    }

    #endregion

    #region API Logging Filters

    /// <summary>
    /// Filter parameters for querying API request logs.
    /// </summary>
    public class ApiLogFilter
    {
        // Time range
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Source filter
        public Guid? SourceSystemId { get; set; }

        // Status filters
        public bool? ErrorsOnly { get; set; }
        public bool? SuccessOnly { get; set; }
        public List<int>? StatusCodes { get; set; }

        // Duration filter (slow requests)
        public long? MinDurationMs { get; set; }
        public long? MaxDurationMs { get; set; }

        // Search
        public string? SearchTerm { get; set; }

        // Correlation
        public string? CorrelationId { get; set; }

        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int Skip => (Page - 1) * PageSize;

        // Sorting
        public string SortColumn { get; set; } = "RequestedAt";
        public bool SortDescending { get; set; } = true;
    }

    /// <summary>
    /// Paginated result for API log queries.
    /// </summary>
    public class ApiLogFilterResult
    {
        public List<ApiRequestLogListItem> Records { get; set; } = new();
        public int TotalRecords { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
    }

    #endregion

    #region API Logging Configuration

    /// <summary>
    /// Configuration options for API logging (bound from appsettings.json).
    /// </summary>
    public class ApiLoggingOptions
    {
        /// <summary>Maximum size of request/response body to log (default: 4096 bytes)</summary>
        public int BodyLogLimit { get; set; } = 4096;

        /// <summary>Headers to redact from logs (contain sensitive data)</summary>
        public List<string> SensitiveHeaders { get; set; } = new()
        {
            "Authorization",
            "X-Api-Key",
            "Cookie",
            "Set-Cookie"
        };

        /// <summary>Default duration for body logging when enabled (hours)</summary>
        public int DefaultBodyLoggingDurationHours { get; set; } = 24;

        /// <summary>Maximum duration for body logging (hours)</summary>
        public int MaxBodyLoggingDurationHours { get; set; } = 72;

        /// <summary>Maximum rows for immediate CSV export</summary>
        public int MaxExportRows { get; set; } = 10000;

        /// <summary>Dashboard auto-refresh interval (seconds), 0 = disabled</summary>
        public int DashboardRefreshSeconds { get; set; } = 30;

        // NOTE: Log retention handled externally via SQL jobs (see doc 123)
    }

    #endregion
}
