namespace FreeExamples;

public partial class DataObjects
{
    /// <summary>
    /// Represents a registered API key with metadata.
    /// </summary>
    public class ApiKeyInfo
    {
        public Guid ApiKeyId { get; set; }
        public string Name { get; set; } = "";
        public string KeyHash { get; set; } = "";
        public string KeyPrefix { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public int RequestCount { get; set; }
    }

    /// <summary>
    /// Request DTO for testing the protected endpoint.
    /// </summary>
    public class ApiKeyTestRequest
    {
        public string? Message { get; set; }
    }

    /// <summary>
    /// Response from the protected endpoint.
    /// </summary>
    public class ApiKeyTestResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? AuthenticatedAs { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// A log entry from an API request hitting the middleware.
    /// </summary>
    public class ApiKeyRequestLog
    {
        public DateTime Timestamp { get; set; }
        public string Path { get; set; } = "";
        public string Method { get; set; } = "";
        public string? KeyName { get; set; }
        public int StatusCode { get; set; }
        public string? Detail { get; set; }
    }
}
