namespace FreeGLBA.Client;

/// <summary>
/// Base exception for GLBA client errors.
/// </summary>
public class GlbaException : Exception
{
    /// <summary>
    /// The HTTP status code returned by the server, if applicable.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// The error code returned by the server, if applicable.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Creates a new GlbaException.
    /// </summary>
    public GlbaException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new GlbaException with an inner exception.
    /// </summary>
    public GlbaException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new GlbaException with HTTP status code and error code.
    /// </summary>
    public GlbaException(string message, int statusCode, string? errorCode = null) : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when authentication fails (HTTP 401).
/// </summary>
public class GlbaAuthenticationException : GlbaException
{
    /// <summary>
    /// Creates a new GlbaAuthenticationException.
    /// </summary>
    public GlbaAuthenticationException(string message = "API key is invalid or inactive.")
        : base(message, 401, "invalid_api_key")
    {
    }
}

/// <summary>
/// Exception thrown when request validation fails (HTTP 400).
/// </summary>
public class GlbaValidationException : GlbaException
{
    /// <summary>
    /// Details about the validation errors.
    /// </summary>
    public IDictionary<string, string[]>? ValidationErrors { get; }

    /// <summary>
    /// Creates a new GlbaValidationException.
    /// </summary>
    public GlbaValidationException(string message)
        : base(message, 400, "validation_error")
    {
    }

    /// <summary>
    /// Creates a new GlbaValidationException with validation error details.
    /// </summary>
    public GlbaValidationException(string message, IDictionary<string, string[]> validationErrors)
        : base(message, 400, "validation_error")
    {
        ValidationErrors = validationErrors;
    }
}

/// <summary>
/// Exception thrown when a duplicate event is detected (HTTP 409).
/// </summary>
public class GlbaDuplicateException : GlbaException
{
    /// <summary>
    /// The ID of the existing event that matches the duplicate.
    /// </summary>
    public Guid? EventId { get; }

    /// <summary>
    /// Creates a new GlbaDuplicateException.
    /// </summary>
    public GlbaDuplicateException(string message = "Event already exists.")
        : base(message, 409, "duplicate")
    {
    }

    /// <summary>
    /// Creates a new GlbaDuplicateException with the existing event ID.
    /// </summary>
    public GlbaDuplicateException(string message, Guid? eventId)
        : base(message, 409, "duplicate")
    {
        EventId = eventId;
    }
}

/// <summary>
/// Exception thrown when a batch operation exceeds the maximum allowed size.
/// </summary>
public class GlbaBatchTooLargeException : GlbaException
{
    /// <summary>
    /// The maximum number of events allowed in a batch.
    /// </summary>
    public int MaxBatchSize { get; }

    /// <summary>
    /// Creates a new GlbaBatchTooLargeException.
    /// </summary>
    public GlbaBatchTooLargeException(int maxBatchSize = 1000)
        : base($"Maximum {maxBatchSize} events per batch.", 400, "batch_too_large")
    {
        MaxBatchSize = maxBatchSize;
    }
}
