using FreeCICD.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System.Collections.Concurrent;

namespace FreeCICD;

/// <summary>
/// Background service that polls Azure DevOps for pipeline status changes
/// and broadcasts diffs to clients subscribed to the PipelineMonitor SignalR group.
/// Only polls when at least one client is subscribed.
/// </summary>
public class PipelineMonitorService : BackgroundService
{
    private readonly IHubContext<freecicdHub, IsrHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PipelineMonitorService> _logger;

    // Cached pipeline statuses keyed by pipeline Id
    private readonly ConcurrentDictionary<int, DataObjects.PipelineStatusSnapshot> _cachedStatuses = new();

    // Tracks whether the cache has been seeded (first poll seeds, doesn't broadcast)
    private bool _cacheSeeded = false;

    // Poll interval — default 5 seconds
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    // Track consecutive errors to back off
    private int _consecutiveErrors = 0;
    private const int MaxBackoffMultiplier = 12; // Max 60 seconds (5s * 12)

    public PipelineMonitorService(
        IHubContext<freecicdHub, IsrHub> hubContext,
        IServiceProvider serviceProvider,
        ILogger<PipelineMonitorService> logger)
    {
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PipelineMonitorService started");

        // Wait a few seconds for the app to fully start before polling
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested) {
            try {
                // Only poll if there are subscribers in the PipelineMonitor group
                int subscriberCount = GetMonitorSubscriberCount();
                if (subscriberCount > 0) {
                    await PollAndBroadcastChanges(stoppingToken);
                    _consecutiveErrors = 0;
                }
            } catch (OperationCanceledException) {
                // Shutdown requested
                break;
            } catch (Exception ex) {
                _consecutiveErrors++;
                _logger.LogWarning(ex, "PipelineMonitorService poll error (attempt {Count})", _consecutiveErrors);
            }

            // Calculate delay with exponential backoff on errors
            int backoffMultiplier = Math.Min(_consecutiveErrors + 1, MaxBackoffMultiplier);
            TimeSpan delay = _pollInterval * backoffMultiplier;

            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("PipelineMonitorService stopped");
    }

    /// <summary>
    /// Checks how many connections are in the PipelineMonitor SignalR group.
    /// </summary>
    private int GetMonitorSubscriberCount()
    {
        return freecicdHub.GetActiveConnectionsList()
            .Count(c => c.Groups.Contains(DataObjects.SignalRUpdateType.PipelineMonitorGroup));
    }

    /// <summary>
    /// Polls Azure DevOps for the latest build status of each pipeline,
    /// compares with cached state, and broadcasts results to subscribers.
    /// Always sends a heartbeat so clients know the service is alive.
    /// </summary>
    private async Task PollAndBroadcastChanges(CancellationToken stoppingToken)
    {
        // Get config via a scoped service
        using var scope = _serviceProvider.CreateScope();
        var configHelper = scope.ServiceProvider.GetRequiredService<IConfigurationHelper>();

        string pat = configHelper.PAT ?? "";
        string orgName = configHelper.OrgName ?? "";
        string projectId = configHelper.ProjectId ?? "";

        if (string.IsNullOrWhiteSpace(pat) || string.IsNullOrWhiteSpace(orgName) || string.IsNullOrWhiteSpace(projectId)) {
            return; // Not configured
        }

        var collectionUri = new Uri($"https://dev.azure.com/{orgName}");
        var credentials = new VssBasicCredential(string.Empty, pat);
        using var connection = new VssConnection(collectionUri, credentials);
        var buildClient = connection.GetClient<BuildHttpClient>();

        // Get all pipeline definitions
        var definitions = await buildClient.GetDefinitionsAsync(project: projectId, cancellationToken: stoppingToken);

        // Collect all snapshots and detect changes
        var allSnapshots = new List<DataObjects.PipelineStatusSnapshot>();
        var changedPipelines = new List<DataObjects.PipelineStatusSnapshot>();

        // Use SemaphoreSlim to limit concurrent API calls
        using var semaphore = new SemaphoreSlim(5);
        var tasks = definitions.Select(async defRef => {
            await semaphore.WaitAsync(stoppingToken);
            try {
                var snapshot = await GetPipelineSnapshot(buildClient, projectId, defRef, stoppingToken);
                if (snapshot != null) {
                    lock (allSnapshots) {
                        allSnapshots.Add(snapshot);
                    }

                    // Only report changes after the cache has been seeded
                    if (_cacheSeeded && HasChanged(snapshot)) {
                        lock (changedPipelines) {
                            changedPipelines.Add(snapshot);
                        }
                    }

                    // Always update cache
                    _cachedStatuses[snapshot.Id] = snapshot;
                }
            } finally {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Mark cache as seeded after first complete poll
        _cacheSeeded = true;

        // Count running pipelines
        int runningCount = allSnapshots.Count(s => {
            var status = s.LastRunStatus?.ToLower() ?? "";
            return status == "inprogress" || status == "notstarted";
        });

        // Always send a heartbeat — even when nothing changed
        var liveUpdate = new DataObjects.PipelineLiveUpdate {
            ChangedPipelines = changedPipelines,
            Timestamp = DateTime.UtcNow,
            PipelinesChecked = allSnapshots.Count,
            RunningCount = runningCount
        };

        string message = changedPipelines.Count > 0
            ? $"{changedPipelines.Count} pipeline(s) updated"
            : $"Checked {allSnapshots.Count} pipelines — no changes";

        var signalRUpdate = new DataObjects.SignalRUpdate {
            UpdateType = DataObjects.SignalRUpdateType.PipelineLiveStatusUpdate,
            Message = message,
            Object = liveUpdate
        };

        await _hubContext.Clients
            .Group(DataObjects.SignalRUpdateType.PipelineMonitorGroup)
            .SignalRUpdate(signalRUpdate);
    }

    /// <summary>
    /// Gets a lightweight status snapshot for a single pipeline.
    /// </summary>
    private async Task<DataObjects.PipelineStatusSnapshot?> GetPipelineSnapshot(
        BuildHttpClient buildClient,
        string projectId,
        Microsoft.TeamFoundation.Build.WebApi.BuildDefinitionReference defRef,
        CancellationToken stoppingToken)
    {
        try {
            // Query with queueTime ordering to ensure in-progress builds appear first
            // (default ordering by finishTime would put in-progress builds with null finish time last)
            var builds = await buildClient.GetBuildsAsync(
                projectId,
                definitions: [defRef.Id],
                top: 1,
                queryOrder: BuildQueryOrder.QueueTimeDescending,
                cancellationToken: stoppingToken);

            var snapshot = new DataObjects.PipelineStatusSnapshot {
                Id = defRef.Id,
                Name = defRef.Name ?? ""
            };

            if (builds.Count > 0) {
                var latest = builds[0];
                snapshot.LastRunStatus = latest.Status?.ToString() ?? "";
                snapshot.LastRunResult = latest.Result?.ToString() ?? "";
                snapshot.LastRunTime = latest.FinishTime ?? latest.StartTime ?? latest.QueueTime;
                snapshot.LastRunBuildId = latest.Id;
                snapshot.LastRunBuildNumber = latest.BuildNumber;
                snapshot.TriggerBranch = latest.SourceBranch;
                snapshot.TriggerReason = latest.Reason.ToString();

                if (latest.RequestedFor != null) {
                    snapshot.TriggeredByUser = latest.RequestedFor.DisplayName;
                    snapshot.TriggeredByAvatarUrl = latest.RequestedFor.ImageUrl;
                } else if (latest.RequestedBy != null) {
                    snapshot.TriggeredByUser = latest.RequestedBy.DisplayName;
                    snapshot.TriggeredByAvatarUrl = latest.RequestedBy.ImageUrl;
                }

                // Get stage bubbles from timeline
                try {
                    var timeline = await buildClient.GetBuildTimelineAsync(
                        projectId, latest.Id, cancellationToken: stoppingToken);
                    if (timeline?.Records != null) {
                        snapshot.Stages = timeline.Records
                            .Where(r => r.RecordType == "Stage" && r.Name?.StartsWith("__") != true)
                            .OrderBy(r => r.Order)
                            .Select(r => new DataObjects.StageBubble {
                                Name = r.Name ?? "",
                                State = r.State?.ToString()?.ToLower() ?? "pending",
                                Result = r.Result?.ToString()?.ToLower(),
                                Order = r.Order ?? 0
                            }).ToList();
                    }
                } catch { }
            }

            return snapshot;
        } catch {
            // Skip this pipeline on error
            return null;
        }
    }

    /// <summary>
    /// Checks if a pipeline's status has changed compared to the cached version.
    /// </summary>
    private bool HasChanged(DataObjects.PipelineStatusSnapshot snapshot)
    {
        if (!_cachedStatuses.TryGetValue(snapshot.Id, out DataObjects.PipelineStatusSnapshot? cached)) {
            return true; // New pipeline — first time seeing it
        }
        return snapshot.ChangeKey != cached.ChangeKey;
    }
}
