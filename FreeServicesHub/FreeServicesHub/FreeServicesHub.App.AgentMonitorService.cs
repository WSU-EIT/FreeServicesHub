using System.Collections.Concurrent;
using FreeServicesHub.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FreeServicesHub;

// Background service that polls for agent staleness and broadcasts status
// changes to dashboard viewers in the "AgentMonitor" SignalR group.
// Follows the same poll-detect-broadcast pattern as FreeCICD's PipelineMonitorService.

public class AgentMonitorService : BackgroundService
{
    private readonly IHubContext<freeserviceshubHub, IsrHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentMonitorService> _logger;

    // In-memory cache of last-known agent status, keyed by AgentId
    private readonly ConcurrentDictionary<Guid, string> _cachedStatuses = new();

    // Default poll interval — 5 seconds
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    // Track consecutive errors for exponential backoff
    private int _consecutiveErrors = 0;
    private const int MaxBackoffMultiplier = 12; // 5s * 12 = 60s max

    // The SignalR group name dashboard clients subscribe to
    private const string MonitorGroup = "AgentMonitor";

    public AgentMonitorService(
        IHubContext<freeserviceshubHub, IsrHub> HubContext,
        IServiceProvider ServiceProvider,
        ILogger<AgentMonitorService> Logger)
    {
        _hubContext = HubContext;
        _serviceProvider = ServiceProvider;
        _logger = Logger;
    }

    protected override async Task ExecuteAsync(CancellationToken StoppingToken)
    {
        _logger.LogInformation("AgentMonitorService started");

        // Give the app a moment to finish startup before we start polling
        await Task.Delay(TimeSpan.FromSeconds(10), StoppingToken);

        while (!StoppingToken.IsCancellationRequested) {
            try {
                await PollAndBroadcast(StoppingToken);
                _consecutiveErrors = 0;
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _consecutiveErrors++;
                _logger.LogWarning(ex, "AgentMonitorService poll error (attempt {Count})", _consecutiveErrors);
            }

            int backoffMultiplier = Math.Min(_consecutiveErrors + 1, MaxBackoffMultiplier);
            TimeSpan delay = _pollInterval * backoffMultiplier;
            await Task.Delay(delay, StoppingToken);
        }

        _logger.LogInformation("AgentMonitorService stopped");
    }

    private async Task PollAndBroadcast(CancellationToken StoppingToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IDataAccess da = scope.ServiceProvider.GetRequiredService<IDataAccess>();
        IConfigurationHelper config = scope.ServiceProvider.GetRequiredService<IConfigurationHelper>();

        int staleThreshold = config.AgentStaleThresholdSeconds;
        DateTime staleEdge = DateTime.UtcNow.AddSeconds(-staleThreshold);

        // Load all non-deleted agents via DataAccess
        List<DataObjects.Agent> agents = await da.GetAgents(null, Guid.Empty, null);

        List<DataObjects.Agent> changedAgents = new();

        foreach (DataObjects.Agent agent in agents) {
            string previousStatus = agent.Status ?? DataObjects.AgentStatuses.Offline;

            // See if the agent is stale based on last heartbeat
            string newStatus = previousStatus;
            if (agent.LastHeartbeat.HasValue && agent.LastHeartbeat.Value < staleEdge) {
                newStatus = previousStatus == DataObjects.AgentStatuses.Online
                    ? DataObjects.AgentStatuses.Stale
                    : DataObjects.AgentStatuses.Offline;
            }

            // Detect status change against our in-memory cache
            bool changed = false;
            if (_cachedStatuses.TryGetValue(agent.AgentId, out string? cached)) {
                changed = cached != newStatus;
            } else {
                // First time seeing this agent — treat as a change so dashboard gets initial state
                changed = true;
            }

            _cachedStatuses[agent.AgentId] = newStatus;

            if (changed) {
                agent.Status = newStatus;
                changedAgents.Add(agent);
            }
        }

        // Broadcast status changes to the AgentMonitor group
        if (changedAgents.Count > 0) {
            DataObjects.SignalRUpdate changeUpdate = new() {
                UpdateType = DataObjects.SignalRUpdateType.AgentStatusChanged,
                Message = $"{changedAgents.Count} agent(s) status changed",
                Object = changedAgents,
            };

            await _hubContext.Clients.Group(MonitorGroup).SignalRUpdate(changeUpdate);
        }

        // Always send a heartbeat so dashboard knows the service is alive
        DataObjects.SignalRUpdate heartbeatUpdate = new() {
            UpdateType = DataObjects.SignalRUpdateType.AgentHeartbeat,
            Message = changedAgents.Count > 0
                ? $"{changedAgents.Count} change(s) detected across {agents.Count} agents"
                : $"Checked {agents.Count} agents — no changes",
            Object = agents,
        };

        await _hubContext.Clients.Group(MonitorGroup).SignalRUpdate(heartbeatUpdate);
    }

}
