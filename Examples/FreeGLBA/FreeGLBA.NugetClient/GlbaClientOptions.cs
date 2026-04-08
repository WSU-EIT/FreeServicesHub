namespace FreeGLBA.Client;

/// <summary>
/// Configuration options for the GLBA client.
/// </summary>
public class GlbaClientOptions
{
    /// <summary>
    /// Gets or sets the base URL of the FreeGLBA server.
    /// Example: "https://glba.example.com"
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key for authentication.
    /// This is provided by the FreeGLBA administrator.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP request timeout. Default is 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the number of retry attempts for transient failures. Default is 3.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to throw exceptions on API errors. Default is true.
    /// When false, methods will return error responses instead of throwing.
    /// </summary>
    public bool ThrowOnError { get; set; } = true;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when required options are missing or invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new ArgumentException("Endpoint is required.", nameof(Endpoint));
        }

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException("Endpoint must be a valid HTTP or HTTPS URL.", nameof(Endpoint));
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new ArgumentException("ApiKey is required.", nameof(ApiKey));
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Timeout must be greater than zero.", nameof(Timeout));
        }

        if (RetryCount < 0)
        {
            throw new ArgumentException("RetryCount cannot be negative.", nameof(RetryCount));
        }
    }
}
