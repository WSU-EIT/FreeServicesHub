using System.Collections.Concurrent;
using FreeServicesHub.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FreeServicesHub;

/// <summary>
/// In-memory ring buffer that captures log entries from hub-side background services
/// and broadcasts each entry to the "BackgroundServiceLogs" SignalR group in real time.
/// Registered as a singleton <see cref="ILoggerProvider"/> so it automatically receives
/// all ILogger output from the monitored categories.
/// </summary>
public sealed class BackgroundServiceLogSink : ILoggerProvider
{
    private const int MaxEntries = 500;
    private const string SignalRGroup = "BackgroundServiceLogs";

    // Categories whose output we capture.  Keyed by logger category → display name.
    private static readonly Dictionary<string, string> MonitoredServices = new(StringComparer.OrdinalIgnoreCase)
    {
        { "FreeServicesHub.AgentMonitorService", "Agent Monitor" },
        { "FreeServicesHub.DevRegistrationKeySeeder", "Dev Registration Key Seeder" },
        { "FreeServicesHub.BackgroundProcessor", "Background Processor" },
    };

    private readonly ConcurrentQueue<DataObjects.BackgroundServiceLogEntry> _entries = new();
    private readonly IServiceProvider _serviceProvider;

    // Track last-activity time per service for the service list endpoint.
    private readonly ConcurrentDictionary<string, DateTime> _lastActivity = new(StringComparer.OrdinalIgnoreCase);

    public BackgroundServiceLogSink(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> most recent log entries (newest first).
    /// </summary>
    public List<DataObjects.BackgroundServiceLogEntry> GetRecentEntries(int count = 200)
    {
        return _entries.Reverse().Take(count).ToList();
    }

    /// <summary>
    /// Returns descriptors for every monitored background service.
    /// </summary>
    public List<DataObjects.BackgroundServiceInfo> GetServiceInfos()
    {
        return MonitoredServices.Select(kv => new DataObjects.BackgroundServiceInfo
        {
            ServiceName = kv.Value,
            Description = kv.Key,
            Status = "Running",
            LastActivity = _lastActivity.TryGetValue(kv.Key, out var ts) ? ts : null,
        }).ToList();
    }

    // ── ILoggerProvider ──────────────────────────────────────────────────────

    public ILogger CreateLogger(string categoryName)
    {
        if (MonitoredServices.TryGetValue(categoryName, out var displayName))
        {
            return new SinkLogger(this, categoryName, displayName);
        }

        return NullLogger.Instance;
    }

    public void Dispose() { }

    // ── Internal plumbing ────────────────────────────────────────────────────

    private void AddEntry(DataObjects.BackgroundServiceLogEntry entry)
    {
        _entries.Enqueue(entry);

        // Trim the ring buffer.
        while (_entries.Count > MaxEntries)
        {
            _entries.TryDequeue(out _);
        }

        _lastActivity[entry.ServiceName] = entry.Timestamp;

        // Fire-and-forget SignalR broadcast.
        _ = BroadcastAsync(entry);
    }

    private async Task BroadcastAsync(DataObjects.BackgroundServiceLogEntry entry)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var hubContext = scope.ServiceProvider.GetService<IHubContext<freeserviceshubHub, IsrHub>>();
            if (hubContext != null)
            {
                var update = new DataObjects.SignalRUpdate
                {
                    UpdateType = DataObjects.SignalRUpdateType.BackgroundServiceLog,
                    Message = entry.Message,
                    Object = entry,
                };
                await hubContext.Clients.Group(SignalRGroup).SignalRUpdate(update);
            }
        }
        catch
        {
            // Swallow – logging infra should never crash the host.
        }
    }

    // ── Nested logger that writes into the ring buffer ───────────────────────

    private sealed class SinkLogger(BackgroundServiceLogSink sink, string category, string displayName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (exception != null)
            {
                message += Environment.NewLine + exception.ToString();
            }

            sink.AddEntry(new DataObjects.BackgroundServiceLogEntry
            {
                LogId = Guid.NewGuid(),
                ServiceName = displayName,
                LogLevel = logLevel.ToString(),
                Message = message,
                Timestamp = DateTime.UtcNow,
            });
        }
    }

    /// <summary>No-op logger for categories we don't monitor.</summary>
    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
