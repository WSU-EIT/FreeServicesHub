using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FreeGLBA.Client;

/// <summary>
/// Client for logging GLBA access events to a FreeGLBA server.
/// </summary>
public class GlbaClient : IGlbaClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly GlbaClientOptions _options;
    private readonly bool _ownsHttpClient;
    private bool _disposed;
    private string? _bearerToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates a new GlbaClient with the specified endpoint and API key.
    /// </summary>
    /// <param name="endpoint">The base URL of the FreeGLBA server.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    public GlbaClient(string endpoint, string apiKey)
        : this(new GlbaClientOptions { Endpoint = endpoint, ApiKey = apiKey })
    {
    }

    /// <summary>
    /// Creates a new GlbaClient with the specified options.
    /// </summary>
    /// <param name="options">The client configuration options.</param>
    public GlbaClient(GlbaClientOptions options)
    {
        options.Validate();
        _options = options;
        _httpClient = CreateHttpClient(options);
        _ownsHttpClient = true;
    }

    /// <summary>
    /// Creates a new GlbaClient with an injected HttpClient and options.
    /// Use this constructor for dependency injection scenarios.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use for requests.</param>
    /// <param name="options">The client configuration options.</param>
    public GlbaClient(HttpClient httpClient, IOptions<GlbaClientOptions> options)
    {
        _options = options.Value;
        _options.Validate();
        _httpClient = httpClient;
        _ownsHttpClient = false;
        ConfigureHttpClient(_httpClient, _options);
    }

    /// <inheritdoc/>
    public async Task<GlbaEventResponse> LogAccessAsync(GlbaEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendWithRetryAsync(
            () => _httpClient.PostAsJsonAsync("api/glba/events", request, JsonOptions, cancellationToken),
            cancellationToken);

        return await HandleResponseAsync<GlbaEventResponse>(response, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<GlbaBatchResponse> LogAccessBatchAsync(IEnumerable<GlbaEventRequest> requests, CancellationToken cancellationToken = default)
    {
        var eventList = requests.ToList();

        if (eventList.Count > 1000)
        {
            throw new GlbaBatchTooLargeException();
        }

        var response = await SendWithRetryAsync(
            () => _httpClient.PostAsJsonAsync("api/glba/events/batch", eventList, JsonOptions, cancellationToken),
            cancellationToken);

        return await HandleResponseAsync<GlbaBatchResponse>(response, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryLogAccessAsync(GlbaEventRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await LogAccessAsync(request, cancellationToken);
            return result.IsSuccess || result.IsDuplicate;
        }
        catch (GlbaDuplicateException)
        {
            return true; // Duplicate is considered success for TryLog
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<GlbaEventResponse> LogExportAsync(
        string userId,
        string subjectId,
        string? purpose = null,
        string? userName = null,
        CancellationToken cancellationToken = default)
    {
        return LogAccessAsync(new GlbaEventRequest
        {
            AccessedAt = DateTime.UtcNow,
            UserId = userId,
            UserName = userName,
            SubjectId = subjectId,
            AccessType = "Export",
            Purpose = purpose
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<GlbaEventResponse> LogBulkExportAsync(
        string userId,
        IEnumerable<string> subjectIds,
        string purpose,
        string? userName = null,
        string? dataCategory = null,
        string? agreementText = null,
        CancellationToken cancellationToken = default)
    {
        var subjectList = subjectIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList()
            ?? throw new ArgumentNullException(nameof(subjectIds));

        if (subjectList.Count == 0)
            throw new ArgumentException("At least one subject ID is required.", nameof(subjectIds));

        return LogAccessAsync(new GlbaEventRequest
        {
            AccessedAt = DateTime.UtcNow,
            UserId = userId,
            UserName = userName,
            SubjectId = subjectList.Count == 1 ? subjectList[0] : "BULK",
            SubjectIds = subjectList,
            AccessType = "Export",
            DataCategory = dataCategory,
            Purpose = purpose,
            AgreementText = agreementText,
            AgreementAcknowledgedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<GlbaEventResponse> LogViewAsync(
        string userId,
        string subjectId,
        string? userName = null,
        CancellationToken cancellationToken = default)
    {
        return LogAccessAsync(new GlbaEventRequest
        {
            AccessedAt = DateTime.UtcNow,
            UserId = userId,
            UserName = userName,
            SubjectId = subjectId,
            AccessType = "View"
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<GlbaEventResponse> LogBulkViewAsync(
        string userId,
        IEnumerable<string> subjectIds,
        string? purpose = null,
        string? userName = null,
        CancellationToken cancellationToken = default)
    {
        var subjectList = subjectIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList()
            ?? throw new ArgumentNullException(nameof(subjectIds));

        if (subjectList.Count == 0)
            throw new ArgumentException("At least one subject ID is required.", nameof(subjectIds));

        return LogAccessAsync(new GlbaEventRequest
        {
            AccessedAt = DateTime.UtcNow,
            UserId = userId,
            UserName = userName,
            SubjectId = subjectList.Count == 1 ? subjectList[0] : "BULK",
            SubjectIds = subjectList,
            AccessType = "View",
            Purpose = purpose
        }, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> sendRequest,
        CancellationToken cancellationToken)
    {
        var retryCount = _options.RetryCount;
        var attempt = 0;
        HttpResponseMessage? response = null;

        while (attempt <= retryCount)
        {
            try
            {
                response = await sendRequest();

                // Don't retry client errors (4xx) - these are not transient
                if (response.IsSuccessStatusCode ||
                    (int)response.StatusCode < 500)
                {
                    return response;
                }

                // Server error (5xx) - might be transient, retry if we have attempts left
                if (attempt < retryCount)
                {
                    response.Dispose();
                    await DelayBeforeRetryAsync(attempt, cancellationToken);
                }
            }
            catch (HttpRequestException) when (attempt < retryCount)
            {
                // Network error - retry
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < retryCount)
            {
                // Timeout - retry
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }

            attempt++;
        }

        return response ?? throw new GlbaException("Failed to send request after all retry attempts.");
    }

    private static async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        // Exponential backoff: 1s, 2s, 4s, 8s, etc.
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        await Task.Delay(delay, cancellationToken);
    }

    private async Task<T> HandleResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Created)
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new GlbaException("Received empty response from server.");
        }

        // Handle specific error status codes
        var statusCode = (int)response.StatusCode;
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                throw new GlbaAuthenticationException();

            case HttpStatusCode.BadRequest:
                throw new GlbaValidationException(
                    TryParseErrorMessage(errorContent) ?? "Request validation failed.");

            case HttpStatusCode.Conflict:
                // For conflict, try to parse the response as GlbaEventResponse to get EventId
                try
                {
                    var conflictResponse = JsonSerializer.Deserialize<GlbaEventResponse>(errorContent, JsonOptions);
                    throw new GlbaDuplicateException(
                        conflictResponse?.Message ?? "Event already exists.",
                        conflictResponse?.EventId);
                }
                catch (JsonException)
                {
                    throw new GlbaDuplicateException();
                }

            default:
                if (_options.ThrowOnError)
                {
                    var errorMessage = TryParseErrorMessage(errorContent) ?? $"Request failed with status {statusCode}.";
                    throw new GlbaException(errorMessage, statusCode);
                }
                // Return default response when not throwing
                return default!;
        }
    }

    private static string? TryParseErrorMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("message", out var message))
                return message.GetString();
            if (doc.RootElement.TryGetProperty("error", out var error))
                return error.GetString();
        }
        catch
        {
            // Ignore parsing errors
        }

        return content.Length > 500 ? content[..500] : content;
    }

    // ============================================================
    // INTERNAL ENDPOINTS (User Auth)
    // ============================================================

    /// <inheritdoc/>
    public async Task<GlbaStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendWithRetryAsync(
            () => SendAuthenticatedRequestAsync(HttpMethod.Get, "api/glba/stats/summary", cancellationToken), 
            cancellationToken);
        return await HandleResponseAsync<GlbaStats>(response, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<AccessEvent>> GetRecentEventsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var url = $"api/glba/events/recent?limit={Math.Min(limit, 100)}";
        var response = await SendWithRetryAsync(
            () => SendAuthenticatedRequestAsync(HttpMethod.Get, url, cancellationToken), 
            cancellationToken);
        return await HandleResponseAsync<List<AccessEvent>>(response, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<AccessEvent>> GetSubjectEventsAsync(string subjectId, int limit = 100, CancellationToken cancellationToken = default)
    {
        var encodedSubjectId = Uri.EscapeDataString(subjectId);
        var url = $"api/glba/subjects/{encodedSubjectId}/events?limit={Math.Min(limit, 500)}";
        var response = await SendWithRetryAsync(
            () => SendAuthenticatedRequestAsync(HttpMethod.Get, url, cancellationToken), 
            cancellationToken);
        return await HandleResponseAsync<List<AccessEvent>>(response, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AccessEvent?> GetEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var url = $"api/glba/events/{eventId}";
        var response = await SendWithRetryAsync(
            () => SendAuthenticatedRequestAsync(HttpMethod.Get, url, cancellationToken), 
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        return await HandleResponseAsync<AccessEvent>(response, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<SourceSystemStatus>> GetSourceStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendWithRetryAsync(
            () => SendAuthenticatedRequestAsync(HttpMethod.Get, "api/glba/sources/status", cancellationToken), 
            cancellationToken);
        return await HandleResponseAsync<List<SourceSystemStatus>>(response, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<AccessorSummary>> GetTopAccessorsAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        var url = $"api/glba/accessors/top?limit={Math.Min(limit, 50)}";
        var response = await SendWithRetryAsync(
            () => SendAuthenticatedRequestAsync(HttpMethod.Get, url, cancellationToken), 
            cancellationToken);
        return await HandleResponseAsync<List<AccessorSummary>>(response, cancellationToken);
    }

    /// <summary>
    /// Sends an authenticated request, creating a new HttpRequestMessage each time.
    /// This allows for retry scenarios since HttpRequestMessage cannot be reused.
    /// </summary>
    private Task<HttpResponseMessage> SendAuthenticatedRequestAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);

        // Use bearer token for internal endpoints if available, otherwise fall back to API key
        if (!string.IsNullOrEmpty(_bearerToken))
        {
            request.Headers.Add("Authorization", $"Bearer {_bearerToken}");
        }
        else
        {
            request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
        }

        request.Headers.Add("Accept", "application/json");
        return _httpClient.SendAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public void SetBearerToken(string bearerToken)
    {
        _bearerToken = bearerToken ?? throw new ArgumentNullException(nameof(bearerToken));
    }

    /// <inheritdoc/>
    public void ClearBearerToken()
    {
        _bearerToken = null;
    }

    private static HttpClient CreateHttpClient(GlbaClientOptions options)
    {
        var client = new HttpClient
        {
            Timeout = options.Timeout
        };
        ConfigureHttpClient(client, options);
        return client;
    }

    private static void ConfigureHttpClient(HttpClient client, GlbaClientOptions options)
    {
        client.BaseAddress = new Uri(options.Endpoint.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
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

    /// <summary>
    /// Disposes of the client and its resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing && _ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
    }
}
