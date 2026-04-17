using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreeServicesHub.Server.Controllers;

public partial class DataController
{
    // ========================================================================
    // Agent CRUD Endpoints
    // ========================================================================

    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetAgents")]
    public async Task<ActionResult<List<DataObjects.Agent>>> GetAgents(DataObjects.GetAgentsRequest? request)
    {
        var output = await da.GetAgents(request?.Ids, CurrentUser.TenantId, CurrentUser);
        return Ok(output);
    }

    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetAgent/{id}")]
    public async Task<ActionResult<DataObjects.Agent>> GetAgent(Guid id)
    {
        var agents = await da.GetAgents(new List<Guid> { id }, CurrentUser.TenantId, CurrentUser);
        var agent = agents.FirstOrDefault();
        if (agent == null) return NotFound();
        return Ok(agent);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/SaveAgents")]
    public async Task<ActionResult<List<DataObjects.Agent>>> SaveAgents(List<DataObjects.Agent> items)
    {
        var output = await da.SaveAgents(items, CurrentUser);
        return Ok(output);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/DeleteAgents")]
    public async Task<ActionResult<DataObjects.BooleanResponse>> DeleteAgents(List<Guid> ids)
    {
        var output = await da.DeleteAgents(ids, CurrentUser);
        return Ok(output);
    }

    // ========================================================================
    // Registration Key Endpoints
    // ========================================================================

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/GetRegistrationKeys")]
    public async Task<ActionResult<List<DataObjects.RegistrationKey>>> GetRegistrationKeys()
    {
        var output = await da.GetRegistrationKeys(CurrentUser.TenantId);
        return Ok(output);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/GenerateRegistrationKeys/{count}")]
    public async Task<ActionResult<List<DataObjects.RegistrationKey>>> GenerateRegistrationKeys(int count)
    {
        var output = await da.GenerateRegistrationKeys(count, CurrentUser.TenantId, CurrentUser);
        return Ok(output);
    }

    // ========================================================================
    // Agent Registration Endpoint
    // ========================================================================

    [HttpPost]
    [AllowAnonymous]
    [Route("~/api/Data/RegisterAgent")]
    public async Task<ActionResult<DataObjects.AgentRegistrationResponse>> RegisterAgent(DataObjects.AgentRegistrationRequest request)
    {
        var output = await da.RegisterAgent(request, TenantId);
        return Ok(output);
    }

    // ========================================================================
    // API Client Token Endpoints
    // ========================================================================

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/GetApiClientTokens")]
    public async Task<ActionResult<List<DataObjects.ApiClientToken>>> GetApiClientTokens()
    {
        var output = await da.GetApiClientTokens(CurrentUser.TenantId);
        return Ok(output);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/RevokeApiClientToken/{id}")]
    [Route("~/api/Data/RevokeToken/{id}")]
    public async Task<ActionResult<DataObjects.BooleanResponse>> RevokeApiClientToken(Guid id)
    {
        var output = await da.RevokeApiClientToken(id, CurrentUser);
        return Ok(output);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/GetAgentToken/{agentId}")]
    public async Task<ActionResult<DataObjects.ApiClientToken>> GetAgentToken(Guid agentId)
    {
        var tokens = await da.GetApiClientTokens(CurrentUser.TenantId);
        var token = tokens.Where(x => x.AgentId == agentId && x.Active).OrderByDescending(x => x.Created).FirstOrDefault();
        if (token == null) return NotFound();
        return Ok(token);
    }

    // ========================================================================
    // CI/CD Pipeline Registration Endpoint
    // Authenticated by X-CiCd-Token header (matches App:CiCdAdminToken).
    // Called by the pipeline after web app deploy to pre-generate a
    // registration key for an agent slug. Returns the plaintext key
    // so the pipeline can inject it into the agent's appsettings.json.
    // ========================================================================

    [HttpPost]
    [AllowAnonymous]
    [Route("~/api/Data/CiCdRegisterAgent/{agentSlug}")]
    public async Task<ActionResult<DataObjects.CiCdRegistrationResponse>> CiCdRegisterAgent(string agentSlug)
    {
        // Validate the CI/CD admin token from request header
        string? cicdToken = null;
        if (HttpContext.Request.Headers.TryGetValue("X-CiCd-Token", out var header)) {
            cicdToken = header.ToString().Trim();
        }

        string expectedToken = configurationHelper.CiCdAdminToken;

        if (string.IsNullOrWhiteSpace(expectedToken)
            || string.IsNullOrWhiteSpace(cicdToken)
            || !string.Equals(cicdToken, expectedToken, StringComparison.Ordinal)) {
            return Unauthorized(new { error = "invalid_cicd_token", message = "CI/CD admin token is missing, empty, or does not match." });
        }

        if (string.IsNullOrWhiteSpace(agentSlug)) {
            return BadRequest(new { error = "missing_slug", message = "Agent slug (friendly name) is required." });
        }

        // Consume one use of this token (auto-creates tracking record on first use)
        bool consumed = await da.ConsumeCiCdTokenUse(cicdToken, TenantId);
        if (!consumed) {
            return StatusCode(403, new { error = "token_exhausted", message = "CiCd token has been fully consumed or has expired. No remaining uses." });
        }

        // Resolve the default tenant so the registration key (and ultimately the agent)
        // lands in the correct tenant rather than Guid.Empty.
        Guid keyTenantId = new Guid("00000000-0000-0000-0000-000000000001");
        string defaultTenantCode = da.DefaultTenantCode;
        if (!string.IsNullOrWhiteSpace(defaultTenantCode)) {
            Guid resolvedId = da.GetTenantIdFromCode(defaultTenantCode);
            if (resolvedId != Guid.Empty) {
                keyTenantId = resolvedId;
            }
        }

        // Generate a single registration key for this agent slug
        var keys = await da.GenerateRegistrationKeys(1, keyTenantId);

        if (keys == null || keys.Count == 0 || string.IsNullOrWhiteSpace(keys[0].NewKeyPlaintext)) {
            return StatusCode(500, new { error = "key_generation_failed", message = "Failed to generate registration key." });
        }

        var output = new DataObjects.CiCdRegistrationResponse {
            AgentSlug = agentSlug,
            RegistrationKey = keys[0].NewKeyPlaintext,
            ExpiresAt = keys[0].ExpiresAt,
            Message = $"Registration key generated for agent '{agentSlug}'. Inject into agent appsettings.json."
        };

        return Ok(output);
    }

    // ========================================================================
    // CI/CD Token Invalidation Endpoint
    // Called at end of pipeline to burn the CiCd token immediately.
    // ========================================================================

    [HttpPost]
    [AllowAnonymous]
    [Route("~/api/Data/InvalidateCiCdToken")]
    public async Task<ActionResult> InvalidateCiCdToken()
    {
        string? cicdToken = null;
        if (HttpContext.Request.Headers.TryGetValue("X-CiCd-Token", out var header)) {
            cicdToken = header.ToString().Trim();
        }

        string expectedToken = configurationHelper.CiCdAdminToken;

        // Must match the current config token to invalidate
        if (string.IsNullOrWhiteSpace(expectedToken)
            || string.IsNullOrWhiteSpace(cicdToken)
            || !string.Equals(cicdToken, expectedToken, StringComparison.Ordinal)) {
            return Unauthorized(new { error = "invalid_cicd_token", message = "CI/CD admin token is missing, empty, or does not match." });
        }

        bool invalidated = await da.InvalidateCiCdToken(cicdToken, TenantId);
        if (invalidated) {
            return Ok(new { message = "CiCd token invalidated. No further agent registrations possible with this token." });
        }

        return Ok(new { message = "No active CiCd token session found (may have already been invalidated or was never used)." });
    }

    // ========================================================================
    // Heartbeat Endpoints
    // ========================================================================

    [HttpPost]
    [Authorize]
    [Route("~/api/Data/SaveHeartbeat")]
    public async Task<ActionResult<DataObjects.BooleanResponse>> SaveHeartbeat(DataObjects.AgentHeartbeat heartbeat)
    {
        var output = await da.SaveHeartbeat(heartbeat);
        return Ok(output);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/GetHeartbeats/{agentId}")]
    [Route("~/api/Data/GetAgentHeartbeats/{agentId}")]
    public async Task<ActionResult<List<DataObjects.AgentHeartbeat>>> GetHeartbeats(Guid agentId, int hours = 24)
    {
        var output = await da.GetHeartbeats(agentId, hours);
        return Ok(output);
    }

    // ========================================================================
    // Background Service Log Endpoints
    // ========================================================================

    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetBackgroundServices")]
    public ActionResult<List<DataObjects.BackgroundServiceInfo>> GetBackgroundServices()
    {
        var sink = HttpContext.RequestServices.GetRequiredService<BackgroundServiceLogSink>();
        return Ok(sink.GetServiceInfos());
    }

    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetBackgroundServiceLogs")]
    public ActionResult<List<DataObjects.BackgroundServiceLogEntry>> GetBackgroundServiceLogs(int count = 200)
    {
        var sink = HttpContext.RequestServices.GetRequiredService<BackgroundServiceLogSink>();
        return Ok(sink.GetRecentEntries(count));
    }

    // ========================================================================
    // Latest Heartbeats Endpoint
    // ========================================================================

    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetLatestHeartbeats")]
    public async Task<ActionResult<List<DataObjects.AgentHeartbeat>>> GetLatestHeartbeats()
    {
        var agents = await da.GetAgents(null, CurrentUser.TenantId, CurrentUser);
        var heartbeats = new List<DataObjects.AgentHeartbeat>();
        foreach (var agent in agents)
        {
            var hb = await da.GetHeartbeats(agent.AgentId, 1);
            if (hb.Any())
            {
                heartbeats.Add(hb.First());
            }
        }
        return Ok(heartbeats);
    }

    // ========================================================================
    // Job Queue Endpoints
    // ========================================================================

    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetHubJobs")]
    public async Task<ActionResult<List<DataObjects.HubJob>>> GetHubJobs(DataObjects.GetHubJobsRequest? request)
    {
        var output = await da.GetHubJobs(request?.Ids, CurrentUser.TenantId, CurrentUser);
        return Ok(output);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/SaveHubJobs")]
    public async Task<ActionResult<List<DataObjects.HubJob>>> SaveHubJobs(List<DataObjects.HubJob> items)
    {
        var output = await da.SaveHubJobs(items, CurrentUser);
        return Ok(output);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/DeleteHubJobs")]
    public async Task<ActionResult<DataObjects.BooleanResponse>> DeleteHubJobs(List<Guid> ids)
    {
        var output = await da.DeleteHubJobs(ids, CurrentUser);
        return Ok(output);
    }

    // ========================================================================
    // Agent Job Polling Endpoint (Bearer-authenticated, not cookie-authenticated)
    // ========================================================================

    [HttpPost]
    [AllowAnonymous]
    [Route("~/api/agent/jobs")]
    public async Task<ActionResult<List<DataObjects.HubJob>>> GetAgentJobs()
    {
        if (HttpContext.Items["AgentId"] is not Guid agentId ||
            HttpContext.Items["AgentTenantId"] is not Guid tenantId) {
            return Unauthorized(new { error = "not_authenticated", message = "Valid agent token required" });
        }

        var output = await da.GetJobsForAgent(agentId, tenantId);
        return Ok(output);
    }

    [HttpPost]
    [AllowAnonymous]
    [Route("~/api/agent/jobs/update")]
    public async Task<ActionResult<List<DataObjects.HubJob>>> UpdateAgentJob(List<DataObjects.HubJob> items)
    {
        if (HttpContext.Items["AgentId"] is not Guid agentId ||
            HttpContext.Items["AgentTenantId"] is not Guid tenantId) {
            return Unauthorized(new { error = "not_authenticated", message = "Valid agent token required" });
        }

        foreach (var item in items) {
            item.AgentId = agentId;
            item.TenantId = tenantId;
        }

        var output = await da.SaveHubJobs(items);
        return Ok(output);
    }

    // ========================================================================
    // Agent Settings Endpoints
    // ========================================================================

    /// <summary>
    /// Returns cached service info from the latest heartbeat for all agents.
    /// Deserialized from AgentHeartbeat.ServiceInfoJson.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetAgentServiceInfos")]
    public async Task<ActionResult<List<DataObjects.AgentServiceInfo>>> GetAgentServiceInfos()
    {
        var agents = await da.GetAgents(null, CurrentUser.TenantId, CurrentUser);
        var result = new List<DataObjects.AgentServiceInfo>();

        foreach (var agent in agents)
        {
            var hbs = await da.GetHeartbeats(agent.AgentId, 1);
            if (hbs.Any() && !string.IsNullOrWhiteSpace(hbs.First().ServiceInfoJson))
            {
                try
                {
                    var info = System.Text.Json.JsonSerializer.Deserialize<DataObjects.AgentServiceInfo>(
                        hbs.First().ServiceInfoJson,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (info != null)
                    {
                        info.AgentId = agent.AgentId;
                        result.Add(info);
                    }
                }
                catch { /* skip unparseable */ }
            }
            else
            {
                // Return a stub so the UI knows the agent exists but has no settings yet
                result.Add(new DataObjects.AgentServiceInfo
                {
                    AgentId = agent.AgentId,
                    AgentName = agent.Name,
                    ServiceStatus = agent.Status,
                });
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// Pushes a settings update to a specific agent via SignalR.
    /// The agent will apply the changes and report back.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/UpdateAgentSettings")]
    public async Task<ActionResult<DataObjects.BooleanResponse>> UpdateAgentSettings(DataObjects.AgentSettingsUpdate settings)
    {
        try
        {
            // Send to the agent's individual SignalR group
            var update = new DataObjects.SignalRUpdate
            {
                UpdateType = DataObjects.SignalRUpdateType.AgentSettingsUpdated,
                Message = "UpdateSettings",
                ItemId = settings.AgentId,
                Object = settings,
            };

            // Serialize the object so it's available on the wire
            update.ObjectAsString = System.Text.Json.JsonSerializer.Serialize(settings);

            await _signalR.Clients.Group("Agent_" + settings.AgentId.ToString()).SignalRUpdate(update);

            return Ok(new DataObjects.BooleanResponse { Result = true, Messages = ["Settings update sent to agent."] });
        }
        catch (Exception ex)
        {
            return Ok(new DataObjects.BooleanResponse { Result = false, Messages = [$"Failed: {ex.Message}"] });
        }
    }

    /// <summary>
    /// Requests a specific agent to report its current settings immediately via SignalR.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/RequestAgentSettings/{agentId}")]
    public async Task<ActionResult<DataObjects.BooleanResponse>> RequestAgentSettings(Guid agentId)
    {
        try
        {
            var update = new DataObjects.SignalRUpdate
            {
                UpdateType = DataObjects.SignalRUpdateType.AgentSettingsReport,
                Message = "RequestSettings",
                ItemId = agentId,
            };

            await _signalR.Clients.Group("Agent_" + agentId.ToString()).SignalRUpdate(update);

            return Ok(new DataObjects.BooleanResponse { Result = true, Messages = ["Settings request sent to agent."] });
        }
        catch (Exception ex)
        {
            return Ok(new DataObjects.BooleanResponse { Result = false, Messages = [$"Failed: {ex.Message}"] });
        }
    }
}
