using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FreeServicesHub.EFModels.EFModels;
using Microsoft.EntityFrameworkCore;

namespace FreeServicesHub;

// Validates Bearer tokens on agent-specific API routes (/api/agent/*)
// and SignalR negotiate (/freeserviceshubHub).
// Hashes the incoming token with SHA-256 and looks it up in ApiClientTokens.
// On success, stashes AgentId and TenantId in HttpContext.Items and sets
// HttpContext.User with a ClaimsPrincipal so downstream [Authorize] passes.

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

        bool requiresAgentAuth = path.StartsWith("/api/agent/", StringComparison.OrdinalIgnoreCase);
        bool isSignalR = path.StartsWith("/freeserviceshubHub", StringComparison.OrdinalIgnoreCase);

        if (!requiresAgentAuth && !isSignalR) {
            await _next(Context);
            return;
        }

        // Extract token -- SignalR sends via query string, HTTP API via Authorization header
        string? token = null;

        if (isSignalR) {
            token = Context.Request.Query["access_token"];
        }

        if (string.IsNullOrEmpty(token)) {
            if (!Context.Request.Headers.TryGetValue("Authorization", out var authHeader)) {
                if (requiresAgentAuth) {
                    await WriteUnauthorized(Context, "missing_token", "Authorization header required");
                    return;
                }
                // For SignalR without a token, let it fall through to normal auth (Blazor UI users)
                await _next(Context);
                return;
            }

            string headerValue = authHeader.ToString();
            if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
                if (requiresAgentAuth) {
                    await WriteUnauthorized(Context, "invalid_format", "Authorization header must use Bearer scheme");
                    return;
                }
                await _next(Context);
                return;
            }

            token = headerValue.Substring(7).Trim();
        }

        if (string.IsNullOrEmpty(token)) {
            if (requiresAgentAuth) {
                await WriteUnauthorized(Context, "empty_token", "Bearer token cannot be empty");
                return;
            }
            await _next(Context);
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
            if (requiresAgentAuth) {
                await WriteUnauthorized(Context, "invalid_token", "Token is invalid, revoked, or inactive");
                return;
            }
            // For SignalR, let it fall through -- might be a Blazor UI user with cookie auth
            await _next(Context);
            return;
        }

        // Stash agent identity for downstream controllers
        Context.Items["AgentId"] = clientToken.AgentId;
        Context.Items["AgentTenantId"] = clientToken.TenantId;

        // Create a ClaimsPrincipal so [Authorize] on the hub passes
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, clientToken.AgentId.ToString()),
            new Claim("AgentId", clientToken.AgentId.ToString()),
            new Claim("TenantId", clientToken.TenantId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "AgentToken");
        Context.User = new ClaimsPrincipal(identity);

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
