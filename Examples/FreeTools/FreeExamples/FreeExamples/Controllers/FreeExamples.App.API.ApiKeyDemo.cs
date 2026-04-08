using FreeExamples.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreeExamples.Server.Controllers;

/// <summary>
/// API endpoints for the API Key Middleware demo.
/// Pattern from: FreeGLBA GlbaController (external API key auth) + DataController (internal user auth).
/// 
/// Two categories of endpoints:
/// 1. Internal (user auth via [Authorize]) — manage keys, view logs
/// 2. External (API key auth via middleware) — the protected endpoint
/// </summary>
public partial class DataController
{
    // ================================================================
    // INTERNAL ENDPOINTS — user-authenticated, for managing demo keys
    // ================================================================

    /// <summary>
    /// GetMany for API keys. Returns all registered keys.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetApiKeys")]
    public ActionResult<List<DataObjects.ApiKeyInfo>> GetApiKeys(
        [FromServices] ApiKeyDemoService keyService)
    {
        return Ok(keyService.GetKeys());
    }

    /// <summary>
    /// Generate a new API key. Returns the key info AND the plaintext key (shown once).
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GenerateApiKey")]
    public ActionResult<object> GenerateApiKey(
        [FromBody] DataObjects.ApiKeyTestRequest request,
        [FromServices] ApiKeyDemoService keyService)
    {
        var name = !string.IsNullOrWhiteSpace(request.Message) ? request.Message : "Demo Key";
        var (keyInfo, plaintextKey) = keyService.GenerateKey(name);

        return Ok(new {
            keyInfo,
            plaintextKey,
        });
    }

    /// <summary>
    /// Revoke (deactivate) an API key.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/RevokeApiKey")]
    public ActionResult<DataObjects.BooleanResponse> RevokeApiKey(
        [FromBody] Guid apiKeyId,
        [FromServices] ApiKeyDemoService keyService)
    {
        var result = keyService.RevokeKey(apiKeyId);
        return Ok(new DataObjects.BooleanResponse { Result = result });
    }

    /// <summary>
    /// Get the middleware request log.
    /// </summary>
    [HttpGet]
    [Authorize]
    [Route("~/api/Data/GetApiKeyLogs")]
    public ActionResult<List<DataObjects.ApiKeyRequestLog>> GetApiKeyLogs(
        [FromServices] ApiKeyDemoService keyService)
    {
        return Ok(keyService.GetLogs());
    }
}

// ================================================================
// EXTERNAL CONTROLLER — protected by ApiKeyDemoMiddleware
// Separate controller so it uses a different route prefix.
// Pattern from: FreeGLBA GlbaController
// ================================================================

[ApiController]
[Route("api/demo/external")]
public class ExternalDemoController : ControllerBase
{
    /// <summary>
    /// A protected endpoint that requires a valid API key via Bearer token.
    /// The middleware validates the key before this code runs.
    /// </summary>
    [HttpPost("data")]
    public ActionResult<DataObjects.ApiKeyTestResponse> PostData(
        [FromBody] DataObjects.ApiKeyTestRequest request,
        [FromServices] ApiKeyDemoService keyService)
    {
        // Get the validated key info set by the middleware
        var keyInfo = HttpContext.Items["ApiKeyInfo"] as DataObjects.ApiKeyInfo;
        if (keyInfo == null) {
            return Unauthorized(new { error = "invalid_api_key", message = "API key validation failed" });
        }

        // Log the successful request
        keyService.LogRequest(
            HttpContext.Request.Path.Value ?? "",
            HttpContext.Request.Method,
            keyInfo.Name,
            200,
            request.Message);

        return Ok(new DataObjects.ApiKeyTestResponse {
            Success = true,
            Message = $"Received: {request.Message ?? "(empty)"}",
            AuthenticatedAs = keyInfo.Name,
            Timestamp = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Simple GET endpoint that just returns status (also protected by middleware).
    /// </summary>
    [HttpGet("ping")]
    public ActionResult<DataObjects.ApiKeyTestResponse> Ping(
        [FromServices] ApiKeyDemoService keyService)
    {
        var keyInfo = HttpContext.Items["ApiKeyInfo"] as DataObjects.ApiKeyInfo;

        keyService.LogRequest(
            HttpContext.Request.Path.Value ?? "",
            HttpContext.Request.Method,
            keyInfo?.Name,
            200,
            "ping");

        return Ok(new DataObjects.ApiKeyTestResponse {
            Success = true,
            Message = "pong",
            AuthenticatedAs = keyInfo?.Name ?? "Unknown",
            Timestamp = DateTime.UtcNow,
        });
    }
}
