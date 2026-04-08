namespace FreeExamples.Client;

/// <summary>
/// Request DTO for the protected POST /api/demo/external/data endpoint.
/// </summary>
public class ApiTestRequest
{
    /// <summary>
    /// The message to send to the protected endpoint.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Response from the protected endpoints.
/// </summary>
public class ApiTestResponse
{
    /// <summary>Whether the request was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Response message from the server.</summary>
    public string? Message { get; set; }

    /// <summary>The API key name that authenticated this request.</summary>
    public string? AuthenticatedAs { get; set; }

    /// <summary>Server-side UTC timestamp.</summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Error response returned by the middleware on auth failure.
/// </summary>
public class ApiErrorResponse
{
    /// <summary>Error code (e.g. "missing_api_key", "invalid_api_key").</summary>
    public string? Error { get; set; }

    /// <summary>Human-readable error description.</summary>
    public string? Message { get; set; }
}
