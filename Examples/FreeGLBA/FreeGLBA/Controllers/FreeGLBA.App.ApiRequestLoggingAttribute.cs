using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace FreeGLBA.Controllers;

// ============================================================================
// API REQUEST LOGGING - Action Filter Attribute
// Logs API requests/responses for compliance and debugging
// ============================================================================

/// <summary>
/// Attribute to enable API request logging on a controller or action.
/// Captures request/response details, timing, and error information.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiRequestLoggingAttribute : ActionFilterAttribute
{
    private const string StopwatchKey = "ApiLogging_Stopwatch";
    private const string RequestDataKey = "ApiLogging_RequestData";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Check for skip attribute
        var skipAttribute = context.ActionDescriptor.EndpointMetadata
            .OfType<SkipApiLoggingAttribute>()
            .FirstOrDefault();
        
        if (skipAttribute != null)
        {
            base.OnActionExecuting(context);
            return;
        }

        // Start stopwatch
        var stopwatch = Stopwatch.StartNew();
        context.HttpContext.Items[StopwatchKey] = stopwatch;

        // Get options from DI
        var options = context.HttpContext.RequestServices
            .GetService<IOptions<DataObjects.ApiLoggingOptions>>()?.Value
            ?? new DataObjects.ApiLoggingOptions();

        // Capture request data
        var requestData = new RequestCaptureData
        {
            RequestedAt = DateTime.UtcNow,
            HttpMethod = context.HttpContext.Request.Method,
            RequestPath = context.HttpContext.Request.Path.ToString(),
            QueryString = context.HttpContext.Request.QueryString.ToString(),
            IpAddress = GetClientIpAddress(context.HttpContext),
            UserAgent = context.HttpContext.Request.Headers.UserAgent.ToString(),
            ForwardedFor = context.HttpContext.Request.Headers["X-Forwarded-For"].ToString(),
            RequestHeaders = CaptureHeaders(context.HttpContext.Request.Headers, options.SensitiveHeaders),
        };

        // Get source system from middleware if available
        if (context.HttpContext.Items["SourceSystem"] is DataObjects.SourceSystem source)
        {
            requestData.SourceSystemId = source.SourceSystemId;
            requestData.SourceSystemName = source.DisplayName;
            requestData.AuthType = "ApiKey";
        }
        else if (context.HttpContext.User?.Identity?.IsAuthenticated == true)
        {
            requestData.UserId = context.HttpContext.User.FindFirst("sub")?.Value ?? string.Empty;
            requestData.UserName = context.HttpContext.User.Identity?.Name ?? string.Empty;
            requestData.AuthType = "JWT";
        }

        // Store for OnActionExecuted
        context.HttpContext.Items[RequestDataKey] = requestData;

        base.OnActionExecuting(context);
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        // Get stopwatch and stop
        if (context.HttpContext.Items[StopwatchKey] is not Stopwatch stopwatch)
        {
            base.OnActionExecuted(context);
            return;
        }
        stopwatch.Stop();

        // Get captured request data
        if (context.HttpContext.Items[RequestDataKey] is not RequestCaptureData requestData)
        {
            base.OnActionExecuted(context);
            return;
        }

        // Build complete log entry
        var logEntry = new EFModels.EFModels.ApiRequestLogItem
        {
            ApiRequestLogId = Guid.NewGuid(),
            SourceSystemId = requestData.SourceSystemId,
            SourceSystemName = requestData.SourceSystemName,
            UserId = requestData.UserId,
            UserName = requestData.UserName,
            HttpMethod = requestData.HttpMethod,
            RequestPath = requestData.RequestPath,
            QueryString = requestData.QueryString,
            RequestHeaders = requestData.RequestHeaders,
            RequestedAt = requestData.RequestedAt,
            RespondedAt = DateTime.UtcNow,
            DurationMs = stopwatch.ElapsedMilliseconds,
            IpAddress = requestData.IpAddress,
            UserAgent = requestData.UserAgent,
            ForwardedFor = requestData.ForwardedFor,
            StatusCode = context.HttpContext.Response.StatusCode,
            IsSuccess = context.HttpContext.Response.StatusCode < 400,
            CorrelationId = context.HttpContext.TraceIdentifier,
            AuthType = requestData.AuthType,
        };

        // Handle exceptions
        if (context.Exception != null)
        {
            logEntry.ErrorMessage = TruncateString(context.Exception.Message, 1000);
            logEntry.ExceptionType = context.Exception.GetType().Name;
            logEntry.IsSuccess = false;
        }

        // Fire and forget - don't block the response
        _ = SaveLogAsync(context.HttpContext.RequestServices, logEntry);

        base.OnActionExecuted(context);
    }

    private static async Task SaveLogAsync(IServiceProvider serviceProvider, EFModels.EFModels.ApiRequestLogItem log)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dataAccess = scope.ServiceProvider.GetRequiredService<IDataAccess>();
            await dataAccess.CreateApiLogAsync(log);
        }
        catch (Exception ex)
        {
            // Fallback to Serilog - never throw from logging
            Log.Error(ex, "Failed to save API request log: {Path}", log.RequestPath);
        }
    }

    private static string GetClientIpAddress(Microsoft.AspNetCore.Http.HttpContext context)
    {
        // Check for forwarded IP first (behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP if there are multiple
            var firstIp = forwardedFor.Split(',')[0].Trim();
            return TruncateString(firstIp, 50);
        }

        // Fall back to direct connection IP
        return TruncateString(context.Connection.RemoteIpAddress?.ToString() ?? string.Empty, 50);
    }

    private static string CaptureHeaders(
        Microsoft.AspNetCore.Http.IHeaderDictionary headers,
        List<string> sensitiveHeaders)
    {
        var filteredHeaders = new Dictionary<string, string>();

        foreach (var header in headers)
        {
            var headerName = header.Key;
            var headerValue = header.Value.ToString();

            // Check if sensitive (case-insensitive)
            var isSensitive = sensitiveHeaders
                .Any(s => s.Equals(headerName, StringComparison.OrdinalIgnoreCase));

            filteredHeaders[headerName] = isSensitive
                ? "[REDACTED]"
                : TruncateString(headerValue, 500);
        }

        return JsonSerializer.Serialize(filteredHeaders);
    }

    private static string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Internal class to hold captured request data between OnActionExecuting and OnActionExecuted.
    /// </summary>
    private class RequestCaptureData
    {
        public DateTime RequestedAt { get; set; }
        public string HttpMethod { get; set; } = string.Empty;
        public string RequestPath { get; set; } = string.Empty;
        public string QueryString { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string ForwardedFor { get; set; } = string.Empty;
        public string RequestHeaders { get; set; } = string.Empty;
        public Guid SourceSystemId { get; set; }
        public string SourceSystemName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string AuthType { get; set; } = string.Empty;
    }
}
