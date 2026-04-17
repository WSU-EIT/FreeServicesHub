// FreeServicesHub.Agent -- AgentWorkerService.cs
// BackgroundService that registers with the hub, maintains a SignalR connection,
// and sends periodic heartbeats with system snapshots.
// Windows only -- uses PowerShell/GC for CPU/memory, DriveInfo for disk.

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.ServiceProcess;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR.Client;

namespace FreeServicesHub.Agent;

/// <summary>
/// Configuration section bound from appsettings.json "Agent" key.
/// </summary>
internal sealed class AgentOptions
{
    public string HubUrl { get; set; } = "https://localhost:7271";
    public string RegistrationKey { get; set; } = "";
    public string ApiClientToken { get; set; } = "";
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public string AgentName { get; set; } = "";
}

/// <summary>
/// Snapshot of system information sent with each heartbeat.
/// </summary>
internal sealed record SystemSnapshot
{
    public string MachineName { get; init; } = "";
    public string OsDescription { get; init; } = "";
    public int ProcessorCount { get; init; }
    public double CpuUsagePercent { get; init; }
    public long TotalMemoryMb { get; init; }
    public long FreeMemoryMb { get; init; }
    public long UsedMemoryMb { get; init; }
    public double MemoryUsagePercent { get; init; }
    public List<DriveSnapshot> Drives { get; init; } = [];
    public TimeSpan Uptime { get; init; }
    public DateTime TimestampUtc { get; init; }
}

/// <summary>
/// Snapshot of a single fixed drive.
/// </summary>
internal sealed record DriveSnapshot
{
    public string Name { get; init; } = "";
    public string DriveFormat { get; init; } = "";
    public double TotalGb { get; init; }
    public double FreeGb { get; init; }
    public double UsedPercent { get; init; }
}

/// <summary>
/// Locally-collected Windows Service metadata.
/// </summary>
internal sealed record WindowsServiceSnapshot
{
    public string ServiceName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Status { get; init; } = "";
    public string StartupType { get; init; } = "";
    public string LogOnAccount { get; init; } = "";
    public int ProcessId { get; init; }
    public string Description { get; init; } = "";
}

/// <summary>
/// Background service that:
/// 1. Registers with the hub (or skips if already has a token).
/// 2. Connects to the SignalR hub with Bearer token auth.
/// 3. Sends heartbeats with system snapshots on a configurable interval.
/// 4. Listens for a "Shutdown" command from the hub.
/// 5. Reconnects with exponential backoff when disconnected.
/// </summary>
public sealed class AgentWorkerService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AgentWorkerService> _logger;
    private readonly DateTime _startedUtc = DateTime.UtcNow;
    private readonly List<SystemSnapshot> _bufferedHeartbeats = [];

    /// <summary>Windows Service name used to query SCM. Set from Program.cs.</summary>
    internal static string WindowsServiceName { get; set; } = "FreeServicesHubAgent";

    private AgentOptions _options = new();
    private HubConnection? _hubConnection;

    public AgentWorkerService(IConfiguration config, ILogger<AgentWorkerService> logger)
    {
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options = LoadOptions();

        _logger.LogInformation("FreeServicesHub Agent starting on {Machine}", Environment.MachineName);
        _logger.LogInformation("Heartbeat interval: {Interval}s", _options.HeartbeatIntervalSeconds);

        // Determine mode: standalone (no hub credentials) or connected (hub + SignalR)
        var hasHubCredentials = !string.IsNullOrEmpty(_options.ApiClientToken)
                             || !string.IsNullOrEmpty(_options.RegistrationKey);

        if (!hasHubCredentials)
        {
            _logger.LogInformation("No hub credentials configured. Running in standalone mode (console output only).");
            await RunStandaloneLoop(stoppingToken);
            return;
        }

        // ---- Connected mode ----
        _logger.LogInformation("Hub URL: {HubUrl}", _options.HubUrl);

        // Step 1: Registration
        if (string.IsNullOrEmpty(_options.ApiClientToken))
        {
            _logger.LogInformation("No API client token found. Registering with hub...");
            var token = await RegisterWithHub(stoppingToken);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Registration failed. Falling back to standalone mode.");
                await RunStandaloneLoop(stoppingToken);
                return;
            }

            _options.ApiClientToken = token;
            PersistToken(token);
            _logger.LogInformation("Registration successful. Token stored.");
        }
        else
        {
            _logger.LogInformation("API client token found. Skipping registration.");
        }

        // Step 2: Connect to SignalR
        await ConnectToSignalR(stoppingToken);

        // Step 2b: Write boot-status file so the CI/CD pipeline can detect successful startup
        WriteBootStatus();

        // Step 3: Heartbeat loop (connected mode -- sends to hub)
        await RunHeartbeatLoop(stoppingToken);

        // Shutdown
        _logger.LogInformation("Agent shutting down.");
        if (_hubConnection is not null)
        {
            try
            {
                await _hubConnection.DisposeAsync();
            }
            catch { /* best effort */ }
        }
    }

    // ======================================================================
    // Boot Status File -- written after successful registration + SignalR connect
    // Allows the CI/CD pipeline to detect the agent is alive and connected.
    // ======================================================================

    private void WriteBootStatus()
    {
        try
        {
            var exeDir = AppContext.BaseDirectory;
            var statusFile = Path.Combine(exeDir, ".boot-status");
            var signalRState = _hubConnection?.State.ToString() ?? "null";
            var lines = new[]
            {
                $"agent={_options.AgentName}",
                $"hub={_options.HubUrl}",
                $"signalr={signalRState}",
                $"registered={!string.IsNullOrEmpty(_options.ApiClientToken)}",
                $"timestamp={DateTime.UtcNow:O}",
            };
            File.WriteAllLines(statusFile, lines);
            _logger.LogInformation("Boot status written to {Path}", statusFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write boot status file.");
        }
    }

    // ======================================================================
    // Standalone Loop -- no hub, just collect and log to console
    // ======================================================================

    private async Task RunStandaloneLoop(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var snapshot = await CollectSnapshot(ct);
                LogSnapshotToConsole(snapshot);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting system snapshot.");
            }

            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("Standalone mode shutting down.");
    }

    private void LogSnapshotToConsole(SystemSnapshot snapshot)
    {
        _logger.LogInformation(
            "── Heartbeat ── {Time:HH:mm:ss} | CPU: {Cpu}% | RAM: {MemUsed}/{MemTotal} MB ({MemPct}%) | Uptime: {Up}",
            snapshot.TimestampUtc,
            snapshot.CpuUsagePercent,
            snapshot.UsedMemoryMb,
            snapshot.TotalMemoryMb,
            snapshot.MemoryUsagePercent,
            snapshot.Uptime.ToString(@"d\.hh\:mm\:ss"));

        foreach (var drive in snapshot.Drives)
        {
            _logger.LogInformation(
                "   Drive {Name}: {Used:F1}/{Total:F1} GB ({Pct:F1}% used)",
                drive.Name,
                drive.TotalGb - drive.FreeGb,
                drive.TotalGb,
                drive.UsedPercent);
        }
    }

    // ======================================================================
    // Configuration
    // ======================================================================

    private AgentOptions LoadOptions()
    {
        var options = new AgentOptions();
        var section = _config.GetSection("Agent");

        var hubUrl = section["HubUrl"];
        if (!string.IsNullOrWhiteSpace(hubUrl))
            options.HubUrl = hubUrl;

        var regKey = section["RegistrationKey"];
        if (!string.IsNullOrWhiteSpace(regKey))
            options.RegistrationKey = regKey;

        var token = section["ApiClientToken"];
        if (!string.IsNullOrWhiteSpace(token))
            options.ApiClientToken = token;

        if (int.TryParse(section["HeartbeatIntervalSeconds"], out var interval) && interval > 0)
            options.HeartbeatIntervalSeconds = interval;

        var agentName = section["AgentName"];
        if (!string.IsNullOrWhiteSpace(agentName))
            options.AgentName = agentName;
        else
            options.AgentName = Environment.MachineName;

        return options;
    }

    // ======================================================================
    // Registration
    // ======================================================================

    private async Task<string?> RegisterWithHub(CancellationToken ct)
    {
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(_options.HubUrl.TrimEnd('/') + "/") };
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                RegistrationKey = _options.RegistrationKey,
                Hostname = _options.AgentName,
                OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
                AgentVersion = typeof(AgentWorkerService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                DotNetVersion = Environment.Version.ToString(),
            };

            var response = await httpClient.PostAsJsonAsync("api/Data/RegisterAgent", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Registration failed: {Status} - {Body}", response.StatusCode, body);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (result.TryGetProperty("apiClientToken", out var tokenElement))
                return tokenElement.GetString();
            if (result.TryGetProperty("ApiClientToken", out var tokenElement2))
                return tokenElement2.GetString();

            _logger.LogError("Registration response did not contain an ApiClientToken.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration request failed.");
            return null;
        }
    }

    /// <summary>
    /// Persist the API client token back to appsettings.json so it survives restarts.
    /// </summary>
    private void PersistToken(string token)
    {
        try
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(settingsPath)) return;

            var json = File.ReadAllText(settingsPath);
            var doc = JsonNode.Parse(json) ?? new JsonObject();

            var agentSection = doc["Agent"]?.AsObject();
            if (agentSection is null)
            {
                agentSection = new JsonObject();
                doc["Agent"] = agentSection;
            }

            agentSection["ApiClientToken"] = token;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(settingsPath, doc.ToJsonString(options));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist token to appsettings.json.");
        }
    }

    // ======================================================================
    // SignalR Connection
    // ======================================================================

    private async Task ConnectToSignalR(CancellationToken ct)
    {
        var hubUrl = $"{_options.HubUrl.TrimEnd('/')}/freeserviceshubHub";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_options.ApiClientToken);
            })
            .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())
            .Build();

        // Listen for SignalRUpdate messages that target this agent
        _hubConnection.On<JsonElement>("SignalRUpdate", async (updateJson) =>
        {
            try
            {
                var updateType = "";
                var message = "";
                var objectAsString = "";

                if (updateJson.TryGetProperty("updateType", out var utProp))
                    updateType = utProp.GetString() ?? "";
                else if (updateJson.TryGetProperty("UpdateType", out var utProp2))
                    updateType = utProp2.GetString() ?? "";

                if (updateJson.TryGetProperty("message", out var msgProp))
                    message = msgProp.GetString() ?? "";
                else if (updateJson.TryGetProperty("Message", out var msgProp2))
                    message = msgProp2.GetString() ?? "";

                if (updateJson.TryGetProperty("objectAsString", out var objProp))
                    objectAsString = objProp.GetString() ?? "";
                else if (updateJson.TryGetProperty("ObjectAsString", out var objProp2))
                    objectAsString = objProp2.GetString() ?? "";

                // Hub requesting our current settings
                if (updateType == "AgentSettingsReport" && message == "RequestSettings")
                {
                    _logger.LogInformation("Hub requested agent settings. Reporting...");
                    var serviceInfo = CollectServiceInfo();
                    await _hubConnection.InvokeAsync("ReportAgentSettings", serviceInfo);
                }
                // Hub pushing updated settings
                else if (updateType == "AgentSettingsUpdated" && message == "UpdateSettings")
                {
                    _logger.LogInformation("Received settings update from hub.");
                    if (!string.IsNullOrWhiteSpace(objectAsString))
                    {
                        var settings = JsonSerializer.Deserialize<AgentSettingsUpdatePayload>(objectAsString,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (settings != null)
                        {
                            ApplySettingsUpdate(settings);
                        }
                    }
                    else
                    {
                        // Try to read from the "object" property directly
                        if (updateJson.TryGetProperty("object", out var objDirect) || updateJson.TryGetProperty("Object", out objDirect))
                        {
                            var settings = JsonSerializer.Deserialize<AgentSettingsUpdatePayload>(objDirect.GetRawText(),
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (settings != null)
                            {
                                ApplySettingsUpdate(settings);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing SignalRUpdate.");
            }
        });

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning("SignalR reconnecting: {Error}", error?.Message ?? "unknown");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += async connectionId =>
        {
            _logger.LogInformation("SignalR reconnected (id: {ConnectionId}). Joining Agents group.", connectionId);
            await JoinAgentsGroup();
            await FlushBufferedHeartbeats();
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning("SignalR connection closed: {Error}", error?.Message ?? "clean close");
            return Task.CompletedTask;
        };

        await StartSignalRConnection(ct);
    }

    private async Task StartSignalRConnection(CancellationToken ct)
    {
        try
        {
            await _hubConnection!.StartAsync(ct);
            _logger.LogInformation("SignalR connected.");
            await JoinAgentsGroup();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Initial SignalR connection failed. Will retry during heartbeat loop.");
        }
    }

    private async Task JoinAgentsGroup()
    {
        try
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("JoinGroup", "Agents");
                _logger.LogInformation("Joined 'Agents' group.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to join Agents group.");
        }
    }

    // ======================================================================
    // Heartbeat Loop
    // ======================================================================

    private async Task RunHeartbeatLoop(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var snapshot = await CollectSnapshot(ct);

                if (_hubConnection?.State == HubConnectionState.Connected)
                {
                    // Flush any buffered heartbeats first
                    await FlushBufferedHeartbeats();

                    await _hubConnection.InvokeAsync("SendHeartbeat", ConvertToHeartbeat(snapshot), ct);
                    _logger.LogDebug("Heartbeat sent via SignalR.");

                    // Poll for and execute pending jobs
                    await PollAndExecuteJobs(ct);
                }
                else
                {
                    // SignalR disconnected -- try HTTP fallback
                    var sent = await SendHeartbeatViaHttp(snapshot, ct);
                    if (!sent)
                    {
                        _logger.LogWarning("Heartbeat failed. Buffering locally.");
                        lock (_bufferedHeartbeats)
                        {
                            _bufferedHeartbeats.Add(snapshot);
                            // Cap buffer at 100 entries
                            if (_bufferedHeartbeats.Count > 100)
                                _bufferedHeartbeats.RemoveAt(0);
                        }
                    }

                    // Try to reconnect SignalR
                    await TryReconnectSignalR(ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in heartbeat loop.");
            }

            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> SendHeartbeatViaHttp(SystemSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(_options.HubUrl.TrimEnd('/') + "/") };
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiClientToken);

            var heartbeat = ConvertToHeartbeat(snapshot);
            var response = await httpClient.PostAsJsonAsync("api/Data/SaveHeartbeat", heartbeat, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HTTP heartbeat fallback failed.");
            return false;
        }
    }

    private async Task FlushBufferedHeartbeats()
    {
        List<SystemSnapshot> toFlush;
        lock (_bufferedHeartbeats)
        {
            if (_bufferedHeartbeats.Count == 0) return;
            toFlush = [.. _bufferedHeartbeats];
            _bufferedHeartbeats.Clear();
        }

        _logger.LogInformation("Flushing {Count} buffered heartbeat(s).", toFlush.Count);

        foreach (var snapshot in toFlush)
        {
            try
            {
                if (_hubConnection?.State == HubConnectionState.Connected)
                    await _hubConnection.InvokeAsync("SendHeartbeat", ConvertToHeartbeat(snapshot));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to flush buffered heartbeat.");
            }
        }
    }

    private async Task TryReconnectSignalR(CancellationToken ct)
    {
        if (_hubConnection is null) return;
        if (_hubConnection.State == HubConnectionState.Connected) return;
        if (_hubConnection.State == HubConnectionState.Connecting) return;

        try
        {
            _logger.LogInformation("Attempting SignalR reconnection...");
            await _hubConnection.StartAsync(ct);
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                _logger.LogInformation("SignalR reconnected.");
                await JoinAgentsGroup();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SignalR reconnection attempt failed.");
        }
    }

    // ======================================================================
    // Heartbeat Conversion
    // ======================================================================

    /// <summary>
    /// Converts the agent's internal SystemSnapshot to the shape expected by
    /// DataObjects.AgentHeartbeat on the server. Keeps the agent self-contained
    /// without referencing server assemblies.
    /// </summary>
    private object ConvertToHeartbeat(SystemSnapshot snapshot)
    {
        var diskMetrics = snapshot.Drives.Select(d => new
        {
            Drive = d.Name,
            UsedGB = Math.Round(d.TotalGb - d.FreeGb, 2),
            TotalGB = d.TotalGb,
            Percent = d.UsedPercent,
        }).ToList();

        return new
        {
            HeartbeatId = Guid.Empty,
            AgentId = Guid.Empty,
            Timestamp = snapshot.TimestampUtc,
            CpuPercent = snapshot.CpuUsagePercent,
            MemoryPercent = snapshot.MemoryUsagePercent,
            MemoryUsedGB = Math.Round(snapshot.UsedMemoryMb / 1024.0, 2),
            MemoryTotalGB = Math.Round(snapshot.TotalMemoryMb / 1024.0, 2),
            DiskMetricsJson = JsonSerializer.Serialize(diskMetrics),
            CustomDataJson = "",
            AgentName = _options.AgentName,
            ServiceInfoJson = JsonSerializer.Serialize(CollectServiceInfo()),
        };
    }

    // ======================================================================
    // Job Polling & Execution
    // ======================================================================

    private async Task PollAndExecuteJobs(CancellationToken ct)
    {
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(_options.HubUrl.TrimEnd('/') + "/") };
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiClientToken);

            var response = await httpClient.PostAsync("api/agent/jobs", null, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Job poll returned {Status}", response.StatusCode);
                return;
            }

            var jobs = await response.Content.ReadFromJsonAsync<List<JsonElement>>(ct);
            if (jobs == null || jobs.Count == 0) return;

            _logger.LogInformation("Received {Count} job(s) from hub.", jobs.Count);

            foreach (var jobJson in jobs)
            {
                var jobId = jobJson.GetProperty("hubJobId").GetGuid();
                var jobType = jobJson.GetProperty("jobType").GetString() ?? "";
                var status = jobJson.GetProperty("status").GetString() ?? "";
                var payload = "";
                if (jobJson.TryGetProperty("payload", out var payloadProp) && payloadProp.ValueKind == JsonValueKind.String)
                    payload = payloadProp.GetString() ?? "";

                // Skip jobs already running or completed
                if (status != "Queued" && status != "Assigned") continue;

                _logger.LogInformation("Executing job {JobId} ({JobType})", jobId, jobType);

                // Report Running status
                await ReportJobStatus(httpClient, jobId, "Running", null, null, ct);

                // Execute the job
                string? result = null;
                string? error = null;
                try
                {
                    result = await ExecuteJob(jobType, payload, ct);
                    await ReportJobStatus(httpClient, jobId, "Completed", result, null, ct);
                    _logger.LogInformation("Job {JobId} completed.", jobId);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    await ReportJobStatus(httpClient, jobId, "Failed", null, error, ct);
                    _logger.LogWarning(ex, "Job {JobId} failed.", jobId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Job polling failed.");
        }
    }

    private async Task ReportJobStatus(HttpClient httpClient, Guid jobId, string status,
        string? result, string? errorMessage, CancellationToken ct)
    {
        try
        {
            var update = new[]
            {
                new
                {
                    HubJobId = jobId,
                    Status = status,
                    Result = result ?? "",
                    ErrorMessage = errorMessage ?? "",
                    StartedAt = status == "Running" ? DateTime.UtcNow : (DateTime?)null,
                    CompletedAt = (status == "Completed" || status == "Failed") ? DateTime.UtcNow : (DateTime?)null,
                }
            };

            await httpClient.PostAsJsonAsync("api/agent/jobs/update", update, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to report job status for {JobId}", jobId);
        }
    }

    private async Task<string> ExecuteJob(string jobType, string payload, CancellationToken ct)
    {
        // Read-only job handlers only — no service restarts, no script execution,
        // no shell commands. The agent is a passive reporter.
        return jobType switch
        {
            "CollectStats" => await ExecuteCollectStats(ct),
            "Ping" => ExecutePing(),
            _ => JsonSerializer.Serialize(new { success = false, message = $"Unsupported job type '{jobType}'" }),
        };
    }

    private async Task<string> ExecuteCollectStats(CancellationToken ct)
    {
        var snapshot = await CollectSnapshot(ct);
        return JsonSerializer.Serialize(new
        {
            success = true,
            machineName = snapshot.MachineName,
            osDescription = snapshot.OsDescription,
            processorCount = snapshot.ProcessorCount,
            cpuUsagePercent = snapshot.CpuUsagePercent,
            totalMemoryMb = snapshot.TotalMemoryMb,
            freeMemoryMb = snapshot.FreeMemoryMb,
            usedMemoryMb = snapshot.UsedMemoryMb,
            memoryUsagePercent = snapshot.MemoryUsagePercent,
            drives = snapshot.Drives,
            uptime = snapshot.Uptime.ToString(@"d\.hh\:mm\:ss"),
            timestampUtc = snapshot.TimestampUtc,
        });
    }

    private static string ExecutePing()
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = "pong",
            timestampUtc = DateTime.UtcNow,
        });
    }

    // ======================================================================
    // System Data Collection (Windows only)
    // ======================================================================

    private async Task<SystemSnapshot> CollectSnapshot(CancellationToken ct)
    {
        var snapshot = new SystemSnapshot
        {
            MachineName = Environment.MachineName,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ProcessorCount = Environment.ProcessorCount,
            CpuUsagePercent = await MeasureCpuWindows(ct),
            Drives = GetDriveSnapshots(),
            Uptime = DateTime.UtcNow - _startedUtc,
            TimestampUtc = DateTime.UtcNow,
        };

        var (totalMb, freeMb) = GetMemoryWindows();
        var usedMb = totalMb - freeMb;
        var usagePct = totalMb > 0 ? (double)usedMb / totalMb * 100.0 : 0.0;

        return snapshot with
        {
            TotalMemoryMb = totalMb,
            FreeMemoryMb = freeMb,
            UsedMemoryMb = usedMb,
            MemoryUsagePercent = Math.Round(usagePct, 1),
        };
    }

    // ---- CPU (Windows) ----

    private static async Task<double> MeasureCpuWindows(CancellationToken ct)
    {
        try
        {
            var output = await RunCommand(
                "powershell",
                "-NoProfile -Command \"(Get-CimInstance Win32_Processor).LoadPercentage\"",
                ct);
            return double.TryParse(output?.Trim(), out var pct) ? pct : -1.0;
        }
        catch
        {
            return -1.0;
        }
    }

    // ---- Memory (Windows) ----

    private static (long totalMb, long freeMb) GetMemoryWindows()
    {
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            var totalMb = gcInfo.TotalAvailableMemoryBytes / (1024 * 1024);
            var committedMb = gcInfo.MemoryLoadBytes / (1024 * 1024);
            var freeMb = totalMb - committedMb;
            return (totalMb, Math.Max(freeMb, 0));
        }
        catch
        {
            return (0, 0);
        }
    }

    // ---- Drives ----

    private static List<DriveSnapshot> GetDriveSnapshots()
    {
        var snapshots = new List<DriveSnapshot>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType != DriveType.Fixed) continue;

                var totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                var usedPct = totalGb > 0 ? (1.0 - freeGb / totalGb) * 100.0 : 0.0;

                snapshots.Add(new DriveSnapshot
                {
                    Name = drive.Name,
                    DriveFormat = drive.DriveFormat,
                    TotalGb = Math.Round(totalGb, 1),
                    FreeGb = Math.Round(freeGb, 1),
                    UsedPercent = Math.Round(usedPct, 1),
                });
            }
        }
        catch { /* best effort */ }

        return snapshots;
    }

    // ---- Process Helper ----

    private static async Task<string?> RunCommand(string fileName, string arguments, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return output;
        }
        catch
        {
            return null;
        }
    }

    // ======================================================================
    // Windows Service Metadata Collection
    // ======================================================================

    /// <summary>
    /// Collects the current agent service info including Windows Service metadata
    /// and editable settings. Returns an anonymous object matching the hub's
    /// AgentServiceInfo shape.
    /// </summary>
    private object CollectServiceInfo()
    {
        var svcSnapshot = GetWindowsServiceSnapshot();

        return new
        {
            AgentId = Guid.Empty, // Hub will fill from claims
            ServiceName = svcSnapshot.ServiceName,
            DisplayName = svcSnapshot.DisplayName,
            ServiceStatus = svcSnapshot.Status,
            StartupType = svcSnapshot.StartupType,
            LogOnAccount = svcSnapshot.LogOnAccount,
            ProcessId = svcSnapshot.ProcessId,
            ServiceDescription = svcSnapshot.Description,
            HubUrl = _options.HubUrl,
            HeartbeatIntervalSeconds = _options.HeartbeatIntervalSeconds,
            AgentName = _options.AgentName,
            AgentVersion = typeof(AgentWorkerService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            DotNetVersion = Environment.Version.ToString(),
            LastBootTime = (DateTime?)(_startedUtc),
            Uptime = DateTime.UtcNow - _startedUtc,
            TimestampUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Queries the Windows Service Control Manager for metadata about this service.
    /// Falls back gracefully when running as a console app.
    /// </summary>
    private static WindowsServiceSnapshot GetWindowsServiceSnapshot()
    {
        try
        {
            using var sc = new ServiceController(WindowsServiceName);
            var status = sc.Status.ToString();
            var startType = sc.StartType.ToString();
            var displayName = sc.DisplayName;

            // Get the service's PID from WMI via sc query
            int pid = 0;
            try
            {
                pid = Environment.ProcessId;
            }
            catch { /* best effort */ }

            // Get log-on account via sc qc (requires parsing)
            var logOnAccount = GetServiceLogOnAccount(WindowsServiceName);
            var description = GetServiceDescription(WindowsServiceName);

            return new WindowsServiceSnapshot
            {
                ServiceName = WindowsServiceName,
                DisplayName = displayName,
                Status = status,
                StartupType = startType,
                LogOnAccount = logOnAccount,
                ProcessId = pid,
                Description = description,
            };
        }
        catch
        {
            // Not running as a service or service not found
            return new WindowsServiceSnapshot
            {
                ServiceName = WindowsServiceName,
                DisplayName = WindowsServiceName,
                Status = "Console",
                StartupType = "N/A",
                LogOnAccount = Environment.UserName,
                ProcessId = Environment.ProcessId,
                Description = "Running as console application",
            };
        }
    }

    /// <summary>
    /// Queries the service log-on account using sc.exe qc.
    /// </summary>
    private static string GetServiceLogOnAccount(string serviceName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"qc \"{serviceName}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return "Unknown";

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            // Parse SERVICE_START_NAME line
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("SERVICE_START_NAME", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                        return parts[1].Trim();
                }
            }
        }
        catch { /* best effort */ }
        return "Unknown";
    }

    /// <summary>
    /// Queries the service description using sc.exe qdescription.
    /// </summary>
    private static string GetServiceDescription(string serviceName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"qdescription \"{serviceName}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return "";

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            // Parse DESCRIPTION line
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("DESCRIPTION", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        var desc = parts[1].Trim();
                        if (!string.IsNullOrWhiteSpace(desc))
                            return desc;
                    }
                }
            }
        }
        catch { /* best effort */ }
        return "";
    }

    // ======================================================================
    // Settings Update (from Hub)
    // ======================================================================

    /// <summary>
    /// Applies settings pushed from the hub. Updates appsettings.json and
    /// optionally changes the Windows Service startup type via sc.exe.
    /// </summary>
    private void ApplySettingsUpdate(AgentSettingsUpdatePayload settings)
    {
        bool changed = false;

        if (!string.IsNullOrWhiteSpace(settings.HubUrl) && settings.HubUrl != _options.HubUrl)
        {
            _logger.LogInformation("Updating HubUrl: {Old} → {New}", _options.HubUrl, settings.HubUrl);
            _options.HubUrl = settings.HubUrl;
            changed = true;
        }

        if (settings.HeartbeatIntervalSeconds.HasValue && settings.HeartbeatIntervalSeconds.Value > 0
            && settings.HeartbeatIntervalSeconds.Value != _options.HeartbeatIntervalSeconds)
        {
            _logger.LogInformation("Updating HeartbeatIntervalSeconds: {Old} → {New}",
                _options.HeartbeatIntervalSeconds, settings.HeartbeatIntervalSeconds.Value);
            _options.HeartbeatIntervalSeconds = settings.HeartbeatIntervalSeconds.Value;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(settings.AgentName) && settings.AgentName != _options.AgentName)
        {
            _logger.LogInformation("Updating AgentName: {Old} → {New}", _options.AgentName, settings.AgentName);
            _options.AgentName = settings.AgentName;
            changed = true;
        }

        // Persist changes to appsettings.json
        if (changed)
        {
            PersistSettings();
        }

        // StartupType changes via sc.exe are intentionally not supported.
        // The agent is a passive reporter — no service mutations.

        // Report back the updated settings to the hub
        _ = Task.Run(async () =>
        {
            try
            {
                if (_hubConnection?.State == HubConnectionState.Connected)
                {
                    var info = CollectServiceInfo();
                    await _hubConnection.InvokeAsync("ReportAgentSettings", info);
                    _logger.LogInformation("Reported updated settings to hub.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to report settings update to hub.");
            }
        });
    }

    /// <summary>
    /// Persists current options to appsettings.json.
    /// </summary>
    private void PersistSettings()
    {
        try
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(settingsPath)) return;

            var json = File.ReadAllText(settingsPath);
            var doc = JsonNode.Parse(json) ?? new JsonObject();

            var agentSection = doc["Agent"]?.AsObject();
            if (agentSection is null)
            {
                agentSection = new JsonObject();
                doc["Agent"] = agentSection;
            }

            agentSection["HubUrl"] = _options.HubUrl;
            agentSection["HeartbeatIntervalSeconds"] = _options.HeartbeatIntervalSeconds;
            agentSection["AgentName"] = _options.AgentName;
            agentSection["ApiClientToken"] = _options.ApiClientToken;

            var writeOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(settingsPath, doc.ToJsonString(writeOptions));
            _logger.LogInformation("Settings persisted to appsettings.json.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist settings to appsettings.json.");
        }
    }

}

/// <summary>
/// Exponential backoff retry policy for SignalR reconnection.
/// Delays: 2s, 4s, 8s, 16s, then caps at 30s.
/// </summary>
internal sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var delaySeconds = Math.Pow(2, retryContext.PreviousRetryCount + 1);
        var delay = TimeSpan.FromSeconds(Math.Min(delaySeconds, MaxDelay.TotalSeconds));
        return delay;
    }
}

/// <summary>
/// Deserialization target for settings pushed from the hub.
/// Mirrors DataObjects.AgentSettingsUpdate but lives in the agent assembly.
/// </summary>
internal sealed class AgentSettingsUpdatePayload
{
    public Guid AgentId { get; set; }
    public string? HubUrl { get; set; }
    public int? HeartbeatIntervalSeconds { get; set; }
    public string? AgentName { get; set; }
    public string? StartupType { get; set; }
}

/// <summary>
/// Static accessor for the application lifetime, used by the Shutdown SignalR handler.
/// </summary>
internal static class Program
{
    internal static IHostApplicationLifetime? Lifetime { get; set; }
}
