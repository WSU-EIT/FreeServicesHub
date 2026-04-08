// FreeServicesHub.Agent -- AgentWorkerService.cs
// BackgroundService that registers with the hub, maintains a SignalR connection,
// and sends periodic heartbeats with system snapshots.
// Windows only -- uses PowerShell/GC for CPU/memory, DriveInfo for disk.

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR.Client;

namespace FreeServicesHub.Agent;

/// <summary>
/// Configuration section bound from appsettings.json "Agent" key.
/// </summary>
internal sealed class AgentOptions
{
    public string HubUrl { get; set; } = "https://localhost:5001";
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
        _logger.LogInformation("Hub URL: {HubUrl}", _options.HubUrl);
        _logger.LogInformation("Heartbeat interval: {Interval}s", _options.HeartbeatIntervalSeconds);

        // ---- Step 1: Registration ----
        if (string.IsNullOrEmpty(_options.ApiClientToken))
        {
            if (string.IsNullOrEmpty(_options.RegistrationKey))
            {
                _logger.LogError("No ApiClientToken or RegistrationKey configured. Cannot start.");
                return;
            }

            _logger.LogInformation("No API client token found. Registering with hub...");
            var token = await RegisterWithHub(stoppingToken);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Registration failed. Agent cannot start.");
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

        // ---- Step 2: Connect to SignalR ----
        await ConnectToSignalR(stoppingToken);

        // ---- Step 3: Heartbeat loop ----
        await RunHeartbeatLoop(stoppingToken);

        // ---- Shutdown ----
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
            using var httpClient = new HttpClient { BaseAddress = new Uri(_options.HubUrl) };
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                RegistrationKey = _options.RegistrationKey,
                AgentName = _options.AgentName,
                MachineName = Environment.MachineName,
            };

            var response = await httpClient.PostAsJsonAsync("/api/agents/register", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Registration failed: {Status} - {Body}", response.StatusCode, body);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (result.TryGetProperty("token", out var tokenElement))
                return tokenElement.GetString();
            if (result.TryGetProperty("Token", out var tokenElement2))
                return tokenElement2.GetString();

            _logger.LogError("Registration response did not contain a token.");
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

        // Listen for Shutdown command
        _hubConnection.On("Shutdown", async () =>
        {
            _logger.LogWarning("Received Shutdown command from hub. Stopping agent...");
            // Trigger graceful shutdown via the application lifetime
            var lifetime = Program.Lifetime;
            lifetime?.StopApplication();
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

                    await _hubConnection.InvokeAsync("SendHeartbeat", snapshot, ct);
                    _logger.LogDebug("Heartbeat sent via SignalR.");
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
            using var httpClient = new HttpClient { BaseAddress = new Uri(_options.HubUrl) };
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiClientToken);

            var response = await httpClient.PostAsJsonAsync("/api/agents/heartbeat", snapshot, ct);
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
                    await _hubConnection.InvokeAsync("SendHeartbeat", snapshot);
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
/// Static accessor for the application lifetime, used by the Shutdown SignalR handler.
/// </summary>
internal static class Program
{
    internal static IHostApplicationLifetime? Lifetime { get; set; }
}
