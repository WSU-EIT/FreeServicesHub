using System.Security.Cryptography;
using System.Text;
using FreeServicesHub.EFModels.EFModels;
using Microsoft.EntityFrameworkCore;

namespace FreeServicesHub;

// Validates Bearer tokens on agent-specific API routes (/api/agent/*).
// Hashes the incoming token with SHA-256 and looks it up in ApiClientTokens.
// On success, stashes AgentId and TenantId in HttpContext.Items so downstream
// controllers don't need to re-validate.

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyMiddleware(RequestDelegate Next)
    {
        _next = Next;
    }

    public async Task InvokeAsync(HttpContext Context)
    {
        string path = Context.Request.Path.Value ?? "";

        // Only intercept agent-specific API routes
        bool requiresAgentAuth = path.StartsWith("/api/agent/", StringComparison.OrdinalIgnoreCase);

        if (!requiresAgentAuth) {
            await _next(Context);
            return;
        }

        // Extract Bearer token from Authorization header
        if (!Context.Request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues authHeader)) {
            await WriteUnauthorized(Context, "missing_token", "Authorization header required");
            return;
        }

        string headerValue = authHeader.ToString();
        if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            await WriteUnauthorized(Context, "invalid_format", "Authorization header must use Bearer scheme");
            return;
        }

        string token = headerValue.Substring(7).Trim();
        if (string.IsNullOrEmpty(token)) {
            await WriteUnauthorized(Context, "empty_token", "Bearer token cannot be empty");
            return;
        }

        // Hash the token and look it up
        string tokenHash = HashToken(token);

        using IServiceScope scope = Context.RequestServices.CreateScope();
        EFDataModel db = scope.ServiceProvider.GetRequiredService<EFDataModel>();

        EFModels.EFModels.ApiClientToken? clientToken = await db.ApiClientTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Active && t.RevokedAt == null);

        if (clientToken == null) {
            await WriteUnauthorized(Context, "invalid_token", "Token is invalid, revoked, or inactive");
            return;
        }

        // Stash agent identity for downstream controllers
        Context.Items["AgentId"] = clientToken.AgentId;
        Context.Items["AgentTenantId"] = clientToken.TenantId;

        await _next(Context);
    }

    private static string HashToken(string Plaintext)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Plaintext));
        return Convert.ToBase64String(bytes);
    }

    private static async Task WriteUnauthorized(HttpContext Context, string Error, string Message)
    {
        Context.Response.StatusCode = 401;
        Context.Response.ContentType = "application/json";
        await Context.Response.WriteAsJsonAsync(new { error = Error, message = Message });
    }
}
