# FreeServices.Service

A cross-platform .NET 10 background service that collects and reports system information on a configurable interval. Runs identically as a console application, a Windows Service (`sc.exe`), a Linux systemd daemon, or a macOS launchd agent — with zero code changes between modes.

**Windows** (sc.exe) · **Linux** (systemd) · **macOS** (launchd)

Developed by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

---

## What This Is

FreeServices.Service is a **reference implementation** of the .NET Generic Host `BackgroundService` pattern configured for cross-platform service hosting. The included `SystemMonitorService` collects system metrics (CPU, memory, disk, OS, process info) and writes them to the console and an optional log file. It exists to demonstrate the hosting pattern — replace it with your own business logic.

The same compiled binary:
- Runs in a terminal via `dotnet run` (console mode)
- Installs and runs as a Windows Service via `sc.exe`
- Installs and runs as a systemd daemon via unit files
- Installs and runs as a macOS launchd agent/daemon via plist files

The host builder in `Program.cs` handles this automatically through platform detection at startup.

---

## Quick Start

### Console Mode (any OS, no elevation)

```bash
dotnet run
```

Output appears every 10 seconds:

```
═══════════════════════════════════════════════════
  FreeServices.Service — System Monitor
  Started:    2026-04-03 20:15:30 UTC
  Machine:    MYWORKSTATION
  Interval:   10s
═══════════════════════════════════════════════════

───── Iteration 1 │ 20:15:30 UTC │ Uptime 0.00:00:00 ─────
  Machine:     MYWORKSTATION
  OS:          Microsoft Windows 10.0.22631
  Arch:        X64
  .NET:        .NET 10.0.0-preview.3
  CPU:         Intel Core i7-13700K (24 logical)
  CPU Usage:   12.3%
  Memory:      18,432 / 32,768 MB (56.3%)
  Drives:
    C:\       120.5 /    476.9 GB free (74.7% used) [NTFS]
  Process:     PID 12345, 85 MB working set, 12 threads
```

Press `Ctrl+C` to stop gracefully.

### Override Interval

```bash
dotnet run -- --Service:IntervalSeconds=2
```

### Deploy as a Platform Service

Use the companion **FreeServices.Installer** project:

```bash
# Windows (Administrator terminal)
dotnet run --project ../FreeServices.Installer -- deploy

# Linux (sudo)
sudo dotnet run --project ../FreeServices.Installer -- deploy

# macOS
dotnet run --project ../FreeServices.Installer -- deploy
```

---

## How It Works

### Program.cs (28 lines)

The entire host builder is 28 lines. Platform detection determines the service lifetime:

```csharp
var builder = Host.CreateApplicationBuilder(args);

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "FreeServicesMonitor";
    });
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Services.AddSystemd();
}
// macOS: default ConsoleLifetime — launchd manages the process directly.

builder.Services.AddHostedService<SystemMonitorService>();

var host = builder.Build();
host.Run();
```

**Key insight:** `AddWindowsService()` and `AddSystemd()` are transparent — they don't change how your `BackgroundService` code runs. They only change how the process communicates its lifecycle to the OS service manager. Your `ExecuteAsync()` code is identical in all modes.

### SystemMonitorService.cs (~490 lines)

The `BackgroundService` implementation follows a simple pattern:

1. **Load configuration** from `appsettings.json` (interval, log settings)
2. **Print a startup banner** with machine name, start time, interval
3. **Loop** until cancellation is requested:
   - Collect a `SystemSnapshot` (CPU, memory, disk, OS, process info)
   - Format a report string
   - Write to console + optional log file
   - Wait for the configured interval
4. **Print a shutdown message** when cancelled

#### Data Collection (Cross-Platform)

| Data | Windows | Linux | macOS |
|------|---------|-------|-------|
| **CPU Usage** | PowerShell `Get-CimInstance Win32_Processor` | `/proc/stat` delta (500ms sample) | `top -l 1` idle% parsing |
| **Memory** | `GC.GetGCMemoryInfo()` total + load | `/proc/meminfo` MemTotal + MemAvailable | `GC.GetGCMemoryInfo()` |
| **Processor Name** | PowerShell `Win32_Processor.Name` | `/proc/cpuinfo` model name | `sysctl -n machdep.cpu.brand_string` |
| **Disk** | `DriveInfo.GetDrives()` | `DriveInfo.GetDrives()` | `DriveInfo.GetDrives()` |
| **OS/Arch/.NET** | `RuntimeInformation` | `RuntimeInformation` | `RuntimeInformation` |
| **Process** | `Process.GetCurrentProcess()` | `Process.GetCurrentProcess()` | `Process.GetCurrentProcess()` |

All collection methods use best-effort error handling — if a metric can't be read, it returns a fallback value rather than crashing.

---

## Configuration

### appsettings.json

```json
{
  "Service": {
    "IntervalSeconds": 10,
    "LogToFile": true,
    "LogFilePath": "service-output.log"
  }
}
```

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `IntervalSeconds` | int | `10` | Seconds between heartbeat iterations |
| `LogToFile` | bool | `true` | Write output to a file in addition to console |
| `LogFilePath` | string | `service-output.log` | File path for log output (relative to working directory) |

### Override via CLI

The Generic Host's `IConfiguration` supports CLI arg overrides using colon-delimited paths:

```bash
dotnet run -- --Service:IntervalSeconds=2 --Service:LogToFile=false
```

### Override via Environment Variables

```bash
Service__IntervalSeconds=5 dotnet run
```

---

## Files

| File | Lines | Purpose |
|------|------:|---------|
| `FreeServices.Service.csproj` | — | Worker SDK project. Targets `net10.0`. References `Microsoft.Extensions.Hosting` (generic host), `Microsoft.Extensions.Hosting.WindowsServices` (Windows Service lifetime), and `Microsoft.Extensions.Hosting.Systemd` (systemd notify/watchdog). |
| `Program.cs` | 28 | Host builder. Platform detection → service lifetime registration → register `SystemMonitorService` → build → run. |
| `SystemMonitorService.cs` | ~490 | `BackgroundService` implementation. Configuration loading, system snapshot collection (CPU, memory, disk, OS, process), report formatting, thread-safe file + console output. |
| `appsettings.json` | — | Runtime configuration: interval, log toggle, log path. Copied to output directory at build time. |

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Extensions.Hosting` | 10.0.0-preview.3 | Generic Host (`CreateApplicationBuilder`, `IHostedService`) |
| `Microsoft.Extensions.Hosting.WindowsServices` | 10.0.0-preview.3 | `AddWindowsService()` — Windows SCM integration |
| `Microsoft.Extensions.Hosting.Systemd` | 10.0.0-preview.3 | `AddSystemd()` — systemd notify + watchdog integration |

---

## Platform Details

| Platform | Lifetime Method | What It Does |
|----------|----------------|--------------|
| **Windows** | `AddWindowsService()` | Registers a `WindowsServiceLifetime` that communicates with the Windows Service Control Manager (SCM). The service reports `SERVICE_RUNNING` on start and handles `SERVICE_CONTROL_STOP`. The `ServiceName` option sets the name the SCM knows. |
| **Linux** | `AddSystemd()` | Registers a `SystemdLifetime` that sends `sd_notify(READY=1)` when the host starts, enabling `Type=notify` in the unit file. Supports watchdog integration. |
| **macOS** | Default (`ConsoleLifetime`) | No special registration needed. launchd manages the process lifecycle directly — `KeepAlive` in the plist handles restarts. The `ConsoleLifetime` still handles `Ctrl+C` gracefully in console mode. |

---

## Repurposing for Your Own Service

1. **Replace `SystemMonitorService`** with your own `BackgroundService` class
2. **Update `Program.cs`** — change `AddHostedService<SystemMonitorService>()` to your class
3. **Update `appsettings.json`** — replace `Service` section with your own configuration
4. **Keep the platform detection** in `Program.cs` — that's the cross-platform magic

The Installer and TestMe projects don't care about the service's internal logic. They only care that the service binary starts and runs. As long as your service is a valid .NET Generic Host application, everything else works unchanged.

---

## Prerequisites

- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/10.0)

---

## Related Projects

| Project | Purpose |
|---------|---------|
| **FreeServices.Installer** | Builds, installs, starts, stops, and manages this service on all platforms |
| **FreeServices.TestMe** | Automated integration tests (4 tests) that verify the full service and installer lifecycle |

---

## License

[MIT License](../LICENSE) — Washington State University, Enrollment Information Technology.
