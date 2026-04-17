using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FreeServicesHub.Server.Hubs
{
    public partial interface IsrHub
    {
        Task SignalRUpdate(DataObjects.SignalRUpdate update);
    }

    [Authorize]
    public partial class freeserviceshubHub : Hub<IsrHub>
    {
        private readonly IServiceProvider _serviceProvider;
        private List<string> tenants = new List<string>();

        public freeserviceshubHub(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task JoinTenantId(string TenantId)
        {
            if (!tenants.Contains(TenantId)) {
                tenants.Add(TenantId);
            }

            // Before adding a user to a Tenant group remove them from any groups they were in before.
            if (tenants != null && tenants.Count() > 0) {
                foreach (var tenant in tenants) {
                    try {
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tenant);
                    } catch { }
                }
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, TenantId);
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();

            // Auto-join agents to their individual group so the hub can target them
            var agentIdClaim = Context.User?.FindFirst("AgentId")?.Value;
            if (Guid.TryParse(agentIdClaim, out var agentId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Agent_" + agentId.ToString());
                await Groups.AddToGroupAsync(Context.ConnectionId, "Agents");
            }
        }

        public async Task SendHeartbeat(DataObjects.AgentHeartbeat heartbeat)
        {
            // Extract AgentId from the authenticated connection's claims
            var agentIdClaim = Context.User?.FindFirst("AgentId")?.Value;
            if (Guid.TryParse(agentIdClaim, out var agentId))
            {
                heartbeat.AgentId = agentId;
            }

            using var scope = _serviceProvider.CreateScope();
            var da = scope.ServiceProvider.GetRequiredService<IDataAccess>();
            await da.SaveHeartbeat(heartbeat);
        }

        /// <summary>
        /// Hub → Agent: request current service settings from a specific agent.
        /// The agent listens for "RequestSettings" and responds with ReportAgentSettings.
        /// </summary>
        public async Task RequestAgentSettings(Guid agentId)
        {
            await Clients.Group("Agent_" + agentId.ToString()).SignalRUpdate(new DataObjects.SignalRUpdate
            {
                UpdateType = DataObjects.SignalRUpdateType.AgentSettingsReport,
                Message = "RequestSettings",
                ItemId = agentId,
            });
        }

        /// <summary>
        /// Hub → Agent: push updated settings to a specific agent.
        /// The agent listens for "UpdateSettings" and applies the changes.
        /// </summary>
        public async Task UpdateAgentSettings(DataObjects.AgentSettingsUpdate settings)
        {
            await Clients.Group("Agent_" + settings.AgentId.ToString()).SignalRUpdate(new DataObjects.SignalRUpdate
            {
                UpdateType = DataObjects.SignalRUpdateType.AgentSettingsUpdated,
                Message = "UpdateSettings",
                ItemId = settings.AgentId,
                Object = settings,
            });
        }

        /// <summary>
        /// Agent → Hub: agent reports its current service settings.
        /// Broadcast to the agent's tenant group so dashboard viewers see it.
        /// </summary>
        public async Task ReportAgentSettings(DataObjects.AgentServiceInfo serviceInfo)
        {
            var agentIdClaim = Context.User?.FindFirst("AgentId")?.Value;
            var tenantIdClaim = Context.User?.FindFirst("TenantId")?.Value;
            if (Guid.TryParse(agentIdClaim, out var agentId))
            {
                serviceInfo.AgentId = agentId;
            }

            // Send to the tenant's group (clients join via JoinTenantId)
            string targetGroup = tenantIdClaim ?? "";

            await Clients.Group(targetGroup).SignalRUpdate(new DataObjects.SignalRUpdate
            {
                TenantId = Guid.TryParse(tenantIdClaim, out var tid) ? tid : null,
                UpdateType = DataObjects.SignalRUpdateType.AgentSettingsReport,
                Message = "SettingsReport",
                ItemId = serviceInfo.AgentId,
                Object = serviceInfo,
            });
        }

        public async Task SignalRUpdate(DataObjects.SignalRUpdate update)
        {
            if (update.TenantId.HasValue) {
                await Clients.Group(update.TenantId.Value.ToString()).SignalRUpdate(update);
            } else {
                // This is a non-tenant-specific update.
                await Clients.All.SignalRUpdate(update);
            }
        }
    }
}
