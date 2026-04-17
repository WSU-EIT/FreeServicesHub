// FreeServicesHub.Agent -- FileLoggerProvider.cs
// Lightweight file logger that mirrors console output to a rolling log file.
// No external packages required. Writes to {BaseDirectory}/agent.log.

using System.Collections.Concurrent;

namespace FreeServicesHub.Agent;

/// <summary>
/// Simple file-based <see cref="ILoggerProvider"/> that appends log entries to a file.
/// Rolls the log when it exceeds <see cref="MaxFileSizeBytes"/>.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logPath;
    private readonly long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly Lock _writeLock = new();

    public FileLoggerProvider(string logPath)
    {
        _logPath = logPath;
        var dir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));
    }

    internal void WriteEntry(string category, LogLevel level, string message)
    {
        lock (_writeLock)
        {
            try
            {
                RollIfNeeded();
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var levelTag = level switch
                {
                    LogLevel.Trace => "trce",
                    LogLevel.Debug => "dbug",
                    LogLevel.Information => "info",
                    LogLevel.Warning => "warn",
                    LogLevel.Error => "fail",
                    LogLevel.Critical => "crit",
                    _ => "none",
                };
                var line = $"{timestamp} [{levelTag}] {category}: {message}{Environment.NewLine}";
                File.AppendAllText(_logPath, line);
            }
            catch { /* best effort -- don't crash the service over a log write */ }
        }
    }

    private void RollIfNeeded()
    {
        try
        {
            if (!File.Exists(_logPath)) return;
            var info = new FileInfo(_logPath);
            if (info.Length < MaxFileSizeBytes) return;

            var rolled = _logPath + ".1";
            if (File.Exists(rolled))
                File.Delete(rolled);
            File.Move(_logPath, rolled);
        }
        catch { /* best effort */ }
    }

    public void Dispose() { }
}

/// <summary>
/// Logger instance that forwards entries to <see cref="FileLoggerProvider"/>.
/// </summary>
internal sealed class FileLogger(string categoryName, FileLoggerProvider provider) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (exception is not null)
            message += Environment.NewLine + exception;

        provider.WriteEntry(categoryName, logLevel, message);
    }
}
