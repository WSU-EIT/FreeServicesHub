# FreeCRM: Timer & Countdown Patterns

> Client-side timers, debounce, countdown displays, and server-side BackgroundService patterns.

**Source:** SSO (TOTP countdown), TrusselBuilder (countdown timers), FreeCICD (PipelineMonitorService), all projects (debounce)

---

## Pattern 1: Debounce Timer (Ubiquitous)

Prevents rapid-fire API calls when users type or change filters.

```csharp
@implements IDisposable

@code {
    private System.Timers.Timer _debounceTimer = new();

    protected override void OnInitialized()
    {
        _debounceTimer.Interval = 500;  // ms
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += async (s, e) => {
            await InvokeAsync(async () => {
                await LoadData();
                StateHasChanged();
            });
        };
    }

    private void OnSearchChanged(ChangeEventArgs e)
    {
        _searchText = e.Value?.ToString() ?? "";
        _debounceTimer.Stop();
        _debounceTimer.Start();  // Resets the timer
    }

    public void Dispose()
    {
        _debounceTimer.Stop();
        _debounceTimer.Dispose();
    }
}
```

**Used in:** Monaco editor content sync (1000ms), filter inputs (500ms), search boxes (300ms).

---

## Pattern 2: Countdown Timer with Progress Bar (SSO)

Live countdown that refreshes a TOTP code every 60 seconds:

```razor
@implements IDisposable

@if (!string.IsNullOrWhiteSpace(_totpCode)) {
    @{
        var percentage = ((double)_secondsRemaining / 59.0) * 100;
        var barWidth = percentage.ToString() + "%";
        string barClass = "progress-bar overflow-visible text-white"
            + (_secondsRemaining <= 10 ? " bg-danger" : "");
    }

    <div class="mb-1 totp-code" style="font-size:4em; font-family:monospace;">@_totpCode</div>

    <div class="progress" style="height:30px; background-color:#555;">
        <div class="@barClass" style="width: @barWidth">
            <span class="ms-2">Seconds Remaining: @_secondsRemaining</span>
        </div>
    </div>
}

@code {
    private string _totpCode = "";
    private int _secondsRemaining = -1;
    private System.Timers.Timer _timer = new();

    protected override void OnInitialized()
    {
        _timer.Interval = 1000;
        _timer.Elapsed += UpdateCountdown;
        _timer.AutoReset = true;
        _timer.Start();
    }

    private void UpdateCountdown(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _secondsRemaining = 59 - DateTime.UtcNow.Second;

        if (_secondsRemaining <= 0) {
            // Generate new code at the top of each minute
            GenerateNewCode();
        }

        InvokeAsync(StateHasChanged);
    }

    private void GenerateNewCode()
    {
        // Your code generation logic here
        _totpCode = "123456";
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
```

**Source:** `_Repos/SSO/SSO.Client/Pages/SlateTOTP.razor`

---

## Pattern 3: Auto-Refresh with Polling

Refresh data every N seconds (e.g., dashboard stats):

```csharp
@implements IDisposable

@code {
    private System.Timers.Timer _refreshTimer = new();

    protected override void OnInitialized()
    {
        _refreshTimer.Interval = 30_000;  // 30 seconds
        _refreshTimer.AutoReset = true;
        _refreshTimer.Elapsed += async (s, e) => {
            await InvokeAsync(async () => {
                await RefreshDashboard();
                StateHasChanged();
            });
        };
        _refreshTimer.Start();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
    }
}
```

---

## Pattern 4: BackgroundService + SignalR + Exponential Backoff (FreeCICD)

Server-side polling that broadcasts only diffs, with smart backoff on errors:

```csharp
public class PipelineMonitorService : BackgroundService
{
    private readonly IHubContext<MyHub, IMyHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    private int _consecutiveErrors = 0;
    private const int MaxBackoffMultiplier = 12;  // Max 60s

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);  // Let app start

        while (!stoppingToken.IsCancellationRequested) {
            try {
                int subscribers = GetSubscriberCount();
                if (subscribers > 0) {
                    await PollAndBroadcastChanges(stoppingToken);
                    _consecutiveErrors = 0;  // Reset on success
                }
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _consecutiveErrors++;
                _logger.LogWarning(ex, "Poll error (attempt {Count})", _consecutiveErrors);
            }

            // Exponential backoff: 5s → 10s → 15s → ... → 60s max
            int multiplier = Math.Min(_consecutiveErrors + 1, MaxBackoffMultiplier);
            await Task.Delay(_pollInterval * multiplier, stoppingToken);
        }
    }
}
```

**Key patterns:**
- **Only polls when clients are listening** (check SignalR group subscriber count)
- **Exponential backoff** on consecutive errors (5s → 60s max)
- **Initial delay** (10s) to let the app fully start
- **Diff broadcasting** — only send changes, not full state

**Register in `Program.App.cs`:**
```csharp
public static WebApplicationBuilder AppModifyBuilderEnd(WebApplicationBuilder builder)
{
    var output = builder;
    output.Services.AddHostedService<PipelineMonitorService>();
    return output;
}
```

**Source:** `ReferenceProjects/FreeCICD-main/FreeCICD/Services/FreeCICD.App.PipelineMonitorService.cs`

---

## Pattern 5: Iteration-Based Background Tasks

The built-in `ProcessBackgroundTasksApp` hook uses iteration counting for interval control:

```csharp
public async Task<DataObjects.BooleanResponse> ProcessBackgroundTasksApp(Guid TenantId, long Iteration)
{
    var output = new DataObjects.BooleanResponse();

    // Runs every tick (default: 10 seconds from appsettings.json)

    // Every 60 seconds (6 × 10s ticks)
    if (Iteration % 6 == 0) {
        await SyncExternalData(TenantId);
    }

    // Every 5 minutes (30 × 10s ticks)
    if (Iteration % 30 == 0) {
        await CleanupExpiredSessions(TenantId);
    }

    // Time-based alternative using settings storage
    var lastRun = GetSetting<DateTime>("CleanupLastRun", DataObjects.SettingType.DateTime);
    if (lastRun == default || lastRun < DateTime.UtcNow.AddMinutes(-10)) {
        await RunCleanup(TenantId);
        SaveSetting("CleanupLastRun", DataObjects.SettingType.DateTime, DateTime.UtcNow);
    }

    output.Result = true;
    return output;
}
```

---

*Category: 007_patterns*
*Source: SSO, TrusselBuilder, FreeCICD, FreeCRM base template*
