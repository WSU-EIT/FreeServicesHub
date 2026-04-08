// FreeServices.Service — SystemMonitorService.cs
// A BackgroundService that collects and reports system information
// on a configurable interval. Works in console mode and as a Windows Service,
// systemd daemon, or launchd agent.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FreeServices.Service;

/// <summary>
/// Configuration section for the service behavior.
/// </summary>
internal sealed class ServiceOptions
{
    public int IntervalSeconds { get; set; } = 10;
    public bool LogToFile { get; set; } = true;
    public string LogFilePath { get; set; } = "service-output.log";
}

/// <summary>
/// Snapshot of collected system information for a single iteration.
/// </summary>
internal sealed record SystemSnapshot
{
    public string MachineName { get; init; } = "";
    public string OsDescription { get; init; } = "";
    public string Architecture { get; init; } = "";
    public string DotNetVersion { get; init; } = "";
    public int ProcessorCount { get; init; }
    public string ProcessorName { get; init; } = "";
    public double CpuUsagePercent { get; init; }
    public long TotalMemoryMb { get; init; }
    public long FreeMemoryMb { get; init; }
    public long UsedMemoryMb { get; init; }
    public double MemoryUsagePercent { get; init; }
    public List<DriveSnapshot> Drives { get; init; } = [];
    public int ProcessId { get; init; }
    public long WorkingSetMb { get; init; }
    public int ThreadCount { get; init; }
    public TimeSpan ServiceUptime { get; init; }
}

/// <summary>
/// Snapshot of a single drive's usage.
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
/// Background service that periodically collects system information
/// and writes it to the console and optionally to a log file.
/// </summary>
public sealed class SystemMonitorService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SystemMonitorService> _logger;
    private readonly DateTime _startedUtc = DateTime.UtcNow;

    public SystemMonitorService(IConfiguration config, ILogger<SystemMonitorService> logger)
    {
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = LoadOptions();
        int iteration = 0;

        PrintBanner(options);

        while (!stoppingToken.IsCancellationRequested)
        {
            iteration++;

            var snapshot = await CollectSnapshot(stoppingToken);
            snapshot = snapshot with { ServiceUptime = DateTime.UtcNow - _startedUtc };

            var report = FormatReport(snapshot, iteration);
            WriteOutput(report, options);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var shutdownMsg = $"\n  Service stopped at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n";
        WriteOutput(shutdownMsg, options);
    }

    // ──── Configuration ────

    private ServiceOptions LoadOptions()
    {
        var options = new ServiceOptions();
        var section = _config.GetSection("Service");

        if (int.TryParse(section["IntervalSeconds"], out var interval) && interval > 0)
            options.IntervalSeconds = interval;

        if (bool.TryParse(section["LogToFile"], out var logToFile))
            options.LogToFile = logToFile;

        var logPath = section["LogFilePath"];
        if (!string.IsNullOrWhiteSpace(logPath))
            options.LogFilePath = logPath;

        return options;
    }

    // ──── Banner ────

    private void PrintBanner(ServiceOptions options)
    {
        var banner = $"""

        ═══════════════════════════════════════════════════
          FreeServices.Service — System Monitor
          Started:    {_startedUtc:yyyy-MM-dd HH:mm:ss} UTC
          Machine:    {Environment.MachineName}
          Interval:   {options.IntervalSeconds}s
        ═══════════════════════════════════════════════════
        """;

        WriteOutput(banner, options);
    }

    // ──── Data Collection ────

    private async Task<SystemSnapshot> CollectSnapshot(CancellationToken ct)
    {
        var snapshot = new SystemSnapshot
        {
            MachineName = Environment.MachineName,
            OsDescription = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            DotNetVersion = RuntimeInformation.FrameworkDescription,
            ProcessorCount = Environment.ProcessorCount,
            ProcessorName = GetProcessorName(),
            CpuUsagePercent = await MeasureCpuUsage(ct),
            ProcessId = Environment.ProcessId,
            WorkingSetMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024),
            ThreadCount = Process.GetCurrentProcess().Threads.Count,
            Drives = GetDriveSnapshots(),
        };

        var (totalMb, freeMb) = GetMemoryInfo();
        var usedMb = totalMb - freeMb;
        var usagePct = totalMb > 0 ? (double)usedMb / totalMb * 100.0 : 0.0;

        return snapshot with
        {
            TotalMemoryMb = totalMb,
            FreeMemoryMb = freeMb,
            UsedMemoryMb = usedMb,
            MemoryUsagePercent = usagePct,
        };
    }

    // ──── CPU ────

    private static async Task<double> MeasureCpuUsage(CancellationToken ct)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return await MeasureCpuWindows(ct);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return await MeasureCpuLinux(ct);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return await MeasureCpuMacOS(ct);
        }
        catch { /* best effort */ }

        return -1.0;
    }

    private static async Task<double> MeasureCpuWindows(CancellationToken ct)
    {
        // Use PowerShell to read the processor performance counter
        var output = await RunCommand("powershell", "-NoProfile -Command \"(Get-CimInstance Win32_Processor).LoadPercentage\"", ct);
        return double.TryParse(output?.Trim(), out var pct) ? pct : -1.0;
    }

    private static async Task<double> MeasureCpuLinux(CancellationToken ct)
    {
        // Read two snapshots from /proc/stat and compute delta
        var read1 = await File.ReadAllLinesAsync("/proc/stat", ct);
        await Task.Delay(500, ct);
        var read2 = await File.ReadAllLinesAsync("/proc/stat", ct);

        var vals1 = ParseProcStatLine(read1.FirstOrDefault(l => l.StartsWith("cpu ")));
        var vals2 = ParseProcStatLine(read2.FirstOrDefault(l => l.StartsWith("cpu ")));

        if (vals1 is null || vals2 is null) return -1.0;

        var totalDelta = vals2.Value.total - vals1.Value.total;
        var idleDelta = vals2.Value.idle - vals1.Value.idle;

        return totalDelta > 0
            ? Math.Round((1.0 - (double)idleDelta / totalDelta) * 100.0, 1)
            : -1.0;
    }

    private static (long total, long idle)? ParseProcStatLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5) return null;

        // cpu user nice system idle iowait irq softirq ...
        var values = parts.Skip(1).Select(p => long.TryParse(p, out var v) ? v : 0).ToArray();
        var total = values.Sum();
        var idle = values.Length >= 4 ? values[3] : 0;
        return (total, idle);
    }

    private static async Task<double> MeasureCpuMacOS(CancellationToken ct)
    {
        // Use top to get a quick CPU snapshot on macOS
        var output = await RunCommand("top", "-l 1 -n 0 -stats cpu", ct);
        if (output is null) return -1.0;

        // Look for "CPU usage:" line
        foreach (var line in output.Split('\n'))
        {
            if (!line.Contains("CPU usage", StringComparison.OrdinalIgnoreCase)) continue;

            // Parse "CPU usage: 5.26% user, 10.52% sys, 84.21% idle"
            var idleIdx = line.IndexOf("idle", StringComparison.OrdinalIgnoreCase);
            if (idleIdx < 0) continue;

            // Walk backwards to find the idle percentage
            var segment = line[..idleIdx].TrimEnd().TrimEnd('%');
            var lastComma = segment.LastIndexOf(',');
            var lastSpace = segment.LastIndexOf(' ');
            var start = Math.Max(lastComma, lastSpace) + 1;
            var idleStr = segment[start..].Trim();

            if (double.TryParse(idleStr, out var idle))
                return Math.Round(100.0 - idle, 1);
        }

        return -1.0;
    }

    // ──── Memory ────

    private static (long totalMb, long freeMb) GetMemoryInfo()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetMemoryWindows();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return GetMemoryLinux();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMemoryMacOS();
        }
        catch { /* best effort */ }

        return (0, 0);
    }

    private static (long totalMb, long freeMb) GetMemoryWindows()
    {
        // Use GC and Environment info as a fallback; WMI is more accurate but heavier
        var gcInfo = GC.GetGCMemoryInfo();
        var totalBytes = gcInfo.TotalAvailableMemoryBytes;
        var totalMb = totalBytes / (1024 * 1024);

        // Approximate free from GC info
        var committedMb = gcInfo.MemoryLoadBytes / (1024 * 1024);
        var freeMb = totalMb - committedMb;

        return (totalMb, Math.Max(freeMb, 0));
    }

    private static (long totalMb, long freeMb) GetMemoryLinux()
    {
        // Parse /proc/meminfo
        long totalKb = 0, availableKb = 0;

        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            if (line.StartsWith("MemTotal:"))
                totalKb = ParseMemInfoValue(line);
            else if (line.StartsWith("MemAvailable:"))
                availableKb = ParseMemInfoValue(line);

            if (totalKb > 0 && availableKb > 0) break;
        }

        return (totalKb / 1024, availableKb / 1024);
    }

    private static long ParseMemInfoValue(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[1], out var kb) ? kb : 0;
    }

    private static (long totalMb, long freeMb) GetMemoryMacOS()
    {
        // Use GC info on macOS — same approach as Windows
        var gcInfo = GC.GetGCMemoryInfo();
        var totalMb = gcInfo.TotalAvailableMemoryBytes / (1024 * 1024);
        var committedMb = gcInfo.MemoryLoadBytes / (1024 * 1024);
        var freeMb = totalMb - committedMb;
        return (totalMb, Math.Max(freeMb, 0));
    }

    // ──── Processor Name ────

    private static string GetProcessorName()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var output = RunCommand("powershell", "-NoProfile -Command \"(Get-CimInstance Win32_Processor).Name\"", CancellationToken.None).GetAwaiter().GetResult();
                return output?.Trim() ?? "Unknown";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var lines = File.ReadAllLines("/proc/cpuinfo");
                var modelLine = lines.FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
                return modelLine?.Split(':').ElementAtOrDefault(1)?.Trim() ?? "Unknown";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var output = RunCommand("sysctl", "-n machdep.cpu.brand_string", CancellationToken.None).GetAwaiter().GetResult();
                return output?.Trim() ?? "Unknown";
            }
        }
        catch { /* best effort */ }

        return "Unknown";
    }

    // ──── Drives ────

    private static List<DriveSnapshot> GetDriveSnapshots()
    {
        var snapshots = new List<DriveSnapshot>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;

                // On macOS/Linux, skip pseudo-filesystems
                if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Network)
                    continue;

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

    // ──── Report Formatting ────

    private static string FormatReport(SystemSnapshot snap, int iteration)
    {
        var now = DateTime.UtcNow;
        var uptime = snap.ServiceUptime;

        var lines = new List<string>
        {
            "",
            $"───── Iteration {iteration} │ {now:HH:mm:ss} UTC │ Uptime {uptime.Days}.{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2} ─────",
            $"  Machine:     {snap.MachineName}",
            $"  OS:          {snap.OsDescription}",
            $"  Arch:        {snap.Architecture}",
            $"  .NET:        {snap.DotNetVersion}",
            $"  CPU:         {snap.ProcessorName} ({snap.ProcessorCount} logical)",
        };

        if (snap.CpuUsagePercent >= 0)
            lines.Add($"  CPU Usage:   {snap.CpuUsagePercent:F1}%");
        else
            lines.Add("  CPU Usage:   (unavailable)");

        if (snap.TotalMemoryMb > 0)
        {
            lines.Add($"  Memory:      {snap.UsedMemoryMb:N0} / {snap.TotalMemoryMb:N0} MB ({snap.MemoryUsagePercent:F1}%)");
        }
        else
        {
            lines.Add("  Memory:      (unavailable)");
        }

        if (snap.Drives.Count > 0)
        {
            lines.Add("  Drives:");
            foreach (var d in snap.Drives)
            {
                lines.Add($"    {d.Name,-8} {d.FreeGb,8:F1} / {d.TotalGb,8:F1} GB free ({d.UsedPercent:F1}% used) [{d.DriveFormat}]");
            }
        }

        lines.Add($"  Process:     PID {snap.ProcessId}, {snap.WorkingSetMb} MB working set, {snap.ThreadCount} threads");
        lines.Add("");

        return string.Join(Environment.NewLine, lines);
    }

    // ──── Output ────

    private static readonly object _writeLock = new();

    private static void WriteOutput(string text, ServiceOptions options)
    {
        lock (_writeLock)
        {
            Console.Write(text);

            if (options.LogToFile)
            {
                try
                {
                    File.AppendAllText(options.LogFilePath, text);
                }
                catch { /* best effort */ }
            }
        }
    }

    // ──── Process Helpers ────

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
