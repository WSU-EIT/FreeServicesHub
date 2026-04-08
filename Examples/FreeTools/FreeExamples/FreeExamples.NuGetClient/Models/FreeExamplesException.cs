namespace FreeExamples.Client;

/// <summary>
/// Base exception for FreeExamples client errors.
/// Pattern from: FreeGLBA.Client.GlbaException
/// </summary>
public class FreeExamplesException : Exception
{
    /// <summary>The HTTP status code returned by the server, if applicable.</summary>
    public int? StatusCode { get; }

    /// <summary>The error code returned by the server, if applicable.</summary>
    public string? ErrorCode { get; }

    public FreeExamplesException(string message) : base(message) { }

    public FreeExamplesException(string message, Exception innerException) : base(message, innerException) { }

    public FreeExamplesException(string message, int statusCode, string? errorCode = null) : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when authentication fails (HTTP 401).
/// </summary>
public class FreeExamplesAuthenticationException : FreeExamplesException
{
    public FreeExamplesAuthenticationException(string message = "API key is invalid or inactive.")
        : base(message, 401, "invalid_api_key") { }
}

/// <summary>
/// Exception thrown when the server returns a 400 Bad Request.
/// </summary>
public class FreeExamplesValidationException : FreeExamplesException
{
    public FreeExamplesValidationException(string message)
        : base(message, 400, "validation_error") { }
}
