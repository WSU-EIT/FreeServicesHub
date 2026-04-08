namespace FreeExamples.Client;

/// <summary>
/// Interface for the FreeExamples API client.
/// Pattern from: FreeGLBA.Client.IGlbaClient
/// </summary>
public interface IFreeExamplesClient : IDisposable
{
    /// <summary>
    /// Sends data to the protected POST /api/demo/external/data endpoint.
    /// Requires a valid API key.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The server response with authenticated identity and message echo.</returns>
    /// <exception cref="FreeExamplesAuthenticationException">API key is invalid or missing.</exception>
    Task<ApiTestResponse> PostDataAsync(ApiTestRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pings the protected GET /api/demo/external/ping endpoint.
    /// Requires a valid API key.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The server response with "pong" message.</returns>
    /// <exception cref="FreeExamplesAuthenticationException">API key is invalid or missing.</exception>
    Task<ApiTestResponse> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fire-and-forget version of PostDataAsync. Returns true on success, false on any error.
    /// Never throws exceptions.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the request succeeded, false otherwise.</returns>
    Task<bool> TryPostDataAsync(ApiTestRequest request, CancellationToken cancellationToken = default);
}
