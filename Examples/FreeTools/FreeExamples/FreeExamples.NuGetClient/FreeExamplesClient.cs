using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FreeExamples.Client;

/// <summary>
/// Typed HTTP client for the FreeExamples external API.
/// Pattern from: FreeGLBA.Client.GlbaClient
/// 
/// Handles Bearer authentication, retry with exponential backoff,
/// and typed error responses — same patterns used in production
/// by FreeGLBA, Helpdesk4, and FreeCICD NuGet clients.
/// </summary>
public class FreeExamplesClient : IFreeExamplesClient
{
    private readonly HttpClient _httpClient;
    private readonly FreeExamplesClientOptions _options;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Creates a new client with endpoint and API key.
    /// </summary>
    public FreeExamplesClient(string endpoint, string apiKey)
        : this(new FreeExamplesClientOptions { Endpoint = endpoint, ApiKey = apiKey })
    {
    }

    /// <summary>
    /// Creates a new client with full options.
    /// </summary>
    public FreeExamplesClient(FreeExamplesClientOptions options)
    {
        options.Validate();
        _options = options;
        _httpClient = CreateHttpClient(options);
        _ownsHttpClient = true;
    }

    /// <summary>
    /// Creates a new client with an injected HttpClient (for DI scenarios).
    /// </summary>
    public FreeExamplesClient(HttpClient httpClient, IOptions<FreeExamplesClientOptions> options)
    {
        _options = options.Value;
        _options.Validate();
        _httpClient = httpClient;
        _ownsHttpClient = false;
        ConfigureHttpClient(_httpClient, _options);
    }

    /// <inheritdoc/>
    public async Task<ApiTestResponse> PostDataAsync(ApiTestRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendWithRetryAsync(
            () => SendAuthenticatedAsync(HttpMethod.Post, "api/demo/external/data", request, cancellationToken),
            cancellationToken);

        return await HandleResponseAsync<ApiTestResponse>(response, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ApiTestResponse> PingAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendWithRetryAsync(
            () => SendAuthenticatedAsync(HttpMethod.Get, "api/demo/external/ping", null, cancellationToken),
            cancellationToken);

        return await HandleResponseAsync<ApiTestResponse>(response, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryPostDataAsync(ApiTestRequest request, CancellationToken cancellationToken = default)
    {
        try {
            var result = await PostDataAsync(request, cancellationToken);
            return result.Success;
        } catch {
            return false;
        }
    }

    // ================================================================
    // INTERNAL — Auth, Retry, Response Handling
    // Pattern from: FreeGLBA.Client.GlbaClient
    // ================================================================

    /// <summary>
    /// Sends an authenticated request, creating a new HttpRequestMessage each time
    /// so retries work (HttpRequestMessage cannot be reused).
    /// </summary>
    private Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
        request.Headers.Add("Accept", "application/json");

        if (body != null) {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return _httpClient.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Retry wrapper with exponential backoff.
    /// Only retries on 5xx server errors or network failures — never 4xx client errors.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> sendRequest,
        CancellationToken cancellationToken)
    {
        var retryCount = _options.RetryCount;
        var attempt = 0;
        HttpResponseMessage? response = null;

        while (attempt <= retryCount) {
            try {
                response = await sendRequest();

                // Don't retry client errors (4xx) — not transient
                if (response.IsSuccessStatusCode || (int)response.StatusCode < 500)
                    return response;

                // Server error (5xx) — might be transient, retry if we have attempts left
                if (attempt < retryCount) {
                    response.Dispose();
                    await DelayBeforeRetryAsync(attempt, cancellationToken);
                }
            } catch (HttpRequestException) when (attempt < retryCount) {
                // Network error — retry
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            } catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < retryCount) {
                // Timeout — retry
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }

            attempt++;
        }

        return response ?? throw new FreeExamplesException("Failed to send request after all retry attempts.");
    }

    /// <summary>
    /// Exponential backoff: 1s, 2s, 4s, 8s, etc.
    /// </summary>
    private static async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        await Task.Delay(delay, cancellationToken);
    }

    /// <summary>
    /// Deserializes a successful response or throws a typed exception for error status codes.
    /// </summary>
    private async Task<T> HandleResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new FreeExamplesException("Received empty response from server.");
        }

        var statusCode = (int)response.StatusCode;

        // Try to read error body
        string errorMessage;
        try {
            var errorResponse = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, cancellationToken);
            errorMessage = errorResponse?.Message ?? $"Request failed with status {statusCode}.";
        } catch {
            errorMessage = $"Request failed with status {statusCode}.";
        }

        throw statusCode switch {
            401 => new FreeExamplesAuthenticationException(errorMessage),
            400 => new FreeExamplesValidationException(errorMessage),
            _ => new FreeExamplesException(errorMessage, statusCode),
        };
    }

    // ================================================================
    // HttpClient factory / config
    // ================================================================

    private static HttpClient CreateHttpClient(FreeExamplesClientOptions options)
    {
        var client = new HttpClient { Timeout = options.Timeout };
        ConfigureHttpClient(client, options);
        return client;
    }

    private static void ConfigureHttpClient(HttpClient client, FreeExamplesClientOptions options)
    {
        client.BaseAddress = new Uri(options.Endpoint.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>
    /// Disposes of the client and its resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing && _ownsHttpClient) {
            _httpClient.Dispose();
        }
        _disposed = true;
    }
}
