using System.Security.Cryptography;
using System.Text;

namespace FreeExamples.Server.Middleware;

// ============================================================================
// API KEY DEMO MIDDLEWARE
// Pattern from: FreeGLBA ApiKeyMiddleware
// Validates Bearer token for demo external API endpoints.
// Only intercepts routes under /api/demo/external/*.
// ============================================================================

public class ApiKeyDemoMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyDemoMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, Services.ApiKeyDemoService keyService)
    {
        var path = context.Request.Path.Value ?? "";

        // Only apply to external demo endpoints
        var requiresApiKey = path.StartsWith("/api/demo/external", StringComparison.OrdinalIgnoreCase);

        if (!requiresApiKey) {
            await _next(context);
            return;
        }

        // Extract API key from Authorization header
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader)) {
            await WriteError(context, 401, "missing_api_key", "Authorization header required");
            return;
        }

        var headerValue = authHeader.ToString();
        if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            await WriteError(context, 401, "invalid_format", "Authorization header must use Bearer scheme");
            return;
        }

        var apiKey = headerValue[7..].Trim();
        if (string.IsNullOrEmpty(apiKey)) {
            await WriteError(context, 401, "empty_api_key", "API key cannot be empty");
            return;
        }

        // Validate API key against our in-memory store
        var keyInfo = keyService.ValidateKey(apiKey);
        if (keyInfo == null) {
            keyService.LogRequest(path, context.Request.Method, null, 401, "Invalid API key");
            await WriteError(context, 401, "invalid_api_key", "API key is invalid or revoked");
            return;
        }

        // Store the validated key info in HttpContext.Items for the controller
        context.Items["ApiKeyInfo"] = keyInfo;

        await _next(context);
    }

    private static async Task WriteError(HttpContext context, int statusCode, string error, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error, message });
    }
}

public static class ApiKeyDemoMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyDemo(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyDemoMiddleware>();
    }
}
