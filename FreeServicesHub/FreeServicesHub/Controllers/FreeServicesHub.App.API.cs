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
    public async Task<ActionResult<List<DataObjects.Agent>>> GetAgents(List<Guid>? ids)
    {
        var output = await da.GetAgents(ids, CurrentUser.TenantId, CurrentUser);
        return Ok(output);
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
    public async Task<ActionResult<DataObjects.BooleanResponse>> RevokeApiClientToken(Guid id)
    {
        var output = await da.RevokeApiClientToken(id, CurrentUser);
        return Ok(output);
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
    public async Task<ActionResult<List<DataObjects.AgentHeartbeat>>> GetHeartbeats(Guid agentId, int hours = 24)
    {
        var output = await da.GetHeartbeats(agentId, hours);
        return Ok(output);
    }
}
