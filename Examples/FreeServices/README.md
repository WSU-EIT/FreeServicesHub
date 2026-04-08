# FreeServices

A cross-platform .NET 10 toolkit for building, deploying, testing, and managing background services through a unified C# CLI/UI interface — with full Azure DevOps pipeline integration.

**Windows** (sc.exe) · **Linux** (systemd) · **macOS** (launchd)

Developed by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

---

## Why This Exists

WSU-EIT runs a growing number of background services across dev, CMS, and production Azure DevOps environments. Today, deploying a background service means:

1. Manually publishing the project
2. RDP/SSH into the target machine
3. Run platform-specific commands by hand (`sc.exe`, `systemctl`, `launchctl`)
4. Hope you remembered the right flags
5. No CI/CD — no pipeline proof that the service even starts

**FreeServices** solves this by making every step programmable and cross-platform:

- A **C# CLI** that can build, install, start, stop, query, and uninstall services — the same operations you'd do manually, but scriptable and repeatable across Windows, Linux, and macOS
- An **interactive menu** that calls the exact same code paths — so a developer can use the UI locally and a pipeline can use the CLI flags, with identical behavior
- **Platform-aware defaults** — the Installer auto-detects your OS and sets the right runtime (`win-x64`, `linux-x64`, `osx-arm64`), install paths, and service management commands
- An **Azure DevOps pipeline** that proves the service works by building it, launching it, verifying heartbeat output, and tearing it down — all on a public hosted agent
- A **test harness** that automates the full lifecycle (clean → build → launch → verify → kill) on any platform

This is the same pattern we use for [FreeCICD](https://github.com/WSU-EIT/FreeCICD) and [FreeTools](https://github.com/WSU-EIT/FreeTools) — open-source, MIT-licensed, built to be forked and adapted.

---

## Background: The Prototype Journey

FreeServices is the production-ready evolution of several rounds of prototype work done in the `PrototypeExploratoryCode/HelloWorld.ServiceManager` directory of the [FreeHub](https://github.com/WSU-EIT/FreeHub) monorepo. That prototype explored:

| Iteration | What was built | What we learned |
|-----------|---------------|-----------------|
| **HelloWorld.Agent + Hub** | Full hub-and-agent architecture with SignalR, heartbeats, 7 read-only collectors, and a Blazor dashboard | The hub-agent pattern works but is more than we need for simple service deployment. The agent/hub split is a separate concern from service management. |
| **HelloWorld.ServiceControl** | CLI orchestrator that publishes, injects config, installs via `sc.exe`, configures crash recovery | The dual CLI/interactive-menu pattern is the right UX. `RunAction()` dispatcher with typed config is the right architecture. |
| **HelloWorld.BuildTool + FreeQEMU** | Cross-compiled Linux agents using QEMU VMs from C# | Proves cross-platform builds work without Docker Desktop. Useful later, but not needed for the Windows-first service deployment story. |
| **HelloWorld.Service + ServiceInstaller + TestMe** | Standalone system-monitor service, unified builder/installer, and integration test harness | This is the pattern we want to productionize. Clean separation: the service does its job, the installer manages lifecycle, the tests prove it works. |

The research is documented in the prototype repo covering Windows Service internals, hub-agent architecture decisions, FreeQEMU integration analysis, and collector design.

**FreeServices takes the best patterns from iteration 4 and builds them into a standalone, publishable project.**

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        FreeServices                                  │
│                                                                      │
│   ┌──────────────────┐   ┌─────────────────────┐   ┌────────────┐   │
│   │  FreeServices     │   │  FreeServices        │   │ FreeServices│   │
│   │  .Service         │   │  .Installer          │   │ .TestMe    │   │
│   │                   │   │                      │   │            │   │
│   │  The background   │   │  Build, deploy, and  │   │ Integration│   │
│   │  service itself   │   │  manage the service  │   │ test runner│   │
│   │                   │   │                      │   │            │   │
│   │  • System monitor │   │  • CLI flags         │   │ • Console  │   │
│   │  • Log to file    │   │  • Interactive menu  │   │   mode test│   │
│   │  • Console + svc  │   │  • Same RunAction()  │   │ • Service  │   │
│   │    hybrid         │   │    for both          │   │   mode test│   │
│   └──────────────────┘   └─────────────────────┘   └────────────┘   │
│                                                                      │
│   ┌──────────────────────────────────────────────────────────────┐   │
│   │  Platform Service Layer (auto-detected)                       │   │
│   │  ┌──────────────┐  ┌───────────────┐  ┌──────────────────┐   │   │
│   │  │ Windows      │  │ Linux         │  │ macOS            │   │   │
│   │  │ sc.exe       │  │ systemd       │  │ launchd          │   │   │
│   │  │ AddWindows   │  │ AddSystemd()  │  │ ConsoleLifetime  │   │   │
│   │  │ Service()    │  │ unit file     │  │ plist file       │   │   │
│   │  └──────────────┘  └───────────────┘  └──────────────────┘   │   │
│   └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│   ┌──────────────────────────────────────────────────────────────┐   │
│   │  azure-pipelines-crossplatform.yml                            │   │
│   │  3 parallel jobs: Windows + Linux + macOS                     │   │
│   │  Build → FileTransform → Test 1 → Verify Installer            │   │
│   └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Projects

| Project | Type | Purpose |
|---------|------|---------|
| **[FreeServices.Service](FreeServices.Service/README.md)** | .NET Worker Service | The background service itself. Collects system info (CPU, memory, disk, OS) on a configurable interval. Outputs to both console and log file. Runs identically as a console app, Windows Service, systemd daemon, or launchd agent. |
| **[FreeServices.Installer](FreeServices.Installer/README.md)** | Console app (CLI + UI) | Builds, deploys, and manages the service on all platforms. Dual interface: interactive numbered menu OR CLI flags — both call the same `RunAction()` dispatcher. Auto-detects OS and routes to `sc.exe` (Windows), `systemctl` (Linux), or `launchctl` (macOS). |
| **[FreeServices.TestMe](FreeServices.TestMe/README.md)** | Console app | Integration test harness. Test 1: console-mode lifecycle (any OS). Test 2: full platform service lifecycle — installs, starts, monitors, stops, and uninstalls using the native service manager. |

## The Dual Interface Pattern

The Installer provides two equal interfaces — **neither is primary, both produce identical output**.

This matters because:
- A **developer** uses the interactive menu to explore options and iterate locally
- A **pipeline** uses the CLI flags to do the same operations headlessly
- The **test harness** uses the CLI flags to automate verification
- **All three call the same `RunAction()` method** with the same string and config

### Interactive Menu

```
dotnet run
```

```
═══ FREESERVICES INSTALLER (Windows) ═══

  CURRENT CONFIGURATION
  Service Name:   FreeServicesMonitor
  Display Name:   FreeServices System Monitor
  Exe Path:       C:\FreeServices\FreeServices.Service.exe
  Project:        C:\repos\FreeServices\FreeServices.Service
  Publish To:     C:\FreeServices
  Runtime:        win-x64
  Self-Contained: Yes

  BUILD & DEPLOY
  1. Build (dotnet publish)
  2. Full Deploy (build → install → start)

  SERVICE MANAGEMENT
  3. Install Service
  4. Uninstall Service
  5. Start Service
  6. Stop Service
  7. Query Status

  OTHER
  8. View Configuration

  Q. Quit

  Select option:
```

### CLI (same operations, same output)

```bash
dotnet run -- build                          # Build/publish the service
dotnet run -- deploy                         # Full pipeline: build → install → start
dotnet run -- install                        # Install as platform service
dotnet run -- uninstall                      # Stop and remove the service
dotnet run -- start                          # Start the service
dotnet run -- stop                           # Stop the service
dotnet run -- status                         # Query status + show recent log
dotnet run -- config                         # View current configuration
```

### Config Overrides (same keys as appsettings.json)

```bash
# Override any setting via CLI args
dotnet run -- deploy --Service:Name=MyMonitor --Publish:OutputPath=D:\svc

# Pipeline usage
dotnet run -- build --Publish:OutputPath=$(Build.ArtifactStagingDirectory)/Service
```

### How It Works Internally

```csharp
// Menu option 2 calls:
RunAction("deploy", config);

// CLI "dotnet run -- deploy" calls:
RunAction("deploy", config);

// Same method. Same config object. Same output.
static int RunAction(string action, InstallerConfig config)
{
    switch (action.ToLowerInvariant())
    {
        case "build":   return BuildService(config);
        case "deploy":  return FullDeploy(config);
        case "install": return InstallService(config);
        // ...
    }
}
```

---

## Quick Start

### 1. Run the Service Manually (Console Mode)

```bash
cd FreeServices.Service
dotnet run
```

You'll see system info printed every 10 seconds:

```
═══════════════════════════════════════════════════
  HelloWorld.Service — System Monitor
  Started:    2026-04-03 20:15:30 UTC
  Machine:    MYWORKSTATION
  Interval:   10s
═══════════════════════════════════════════════════

───── Iteration 1 │ 20:15:30 UTC │ Uptime 0.00:00:00 ─────
  Machine:     MYWORKSTATION
  OS:          Microsoft Windows 10.0.22631
  CPU:         Intel Core i7-13700K (24 logical)
  CPU Usage:   12.3%
  Memory:      18432 / 32768 MB (56.3%)
  Drives:
    C:\    120.5 / 476.9 GB free (74.7% used) [NTFS]
```

Press `Ctrl+C` to stop. The output is identical whether running as a console app or a Windows Service — when running as a service, it also writes to `service-output.log`.

### 2. Run the Integration Tests

```bash
cd FreeServices.TestMe

# Run Test 1 (console mode — works on any OS)
dotnet run -- --test=1 --heartbeats=3 --interval=2

# Run all tests (Test 2 requires Windows + Admin)
dotnet run
```

### 3. Deploy as a Platform Service

**Windows** — open an Administrator terminal:

```bash
cd FreeServices.Installer
dotnet run -- deploy
```

Uses `sc.exe create`, configures crash recovery, starts the service.

**Linux** — run with sudo:

```bash
cd FreeServices.Installer
sudo dotnet run -- deploy
```

Generates a systemd unit file at `/etc/systemd/system/`, enables and starts the service.

**macOS** — run as your user:

```bash
cd FreeServices.Installer
dotnet run -- deploy
```

Generates a launchd plist at `~/Library/LaunchAgents/`, loads and starts the agent.

### 4. Check on it

```bash
dotnet run -- status
```

Shows `sc.exe query` output plus the last 10 lines of the service log.

---

## Configuration

### FreeServices.Service — `appsettings.json`

```json
{
  "Service": {
    "IntervalSeconds": 10,
    "LogToFile": true,
    "LogFilePath": "service-output.log"
  }
}
```

### FreeServices.Installer — `appsettings.json`

```json
{
  "Service": {
    "Name": "FreeServicesMonitor",
    "DisplayName": "FreeServices System Monitor",
    "Description": "Periodically collects and logs system information.",
    "ExePath": ""
  },
  "Publish": {
    "ProjectPath": "../FreeServices.Service",
    "OutputPath": "",
    "Runtime": "",
    "SelfContained": true
  },
  "Recovery": {
    "RestartDelayMs": 5000,
    "ResetPeriodSeconds": 86400
  },
  "Systemd": {
    "UnitFilePath": "/etc/systemd/system/freeservices.service",
    "User": "root",
    "WorkingDirectory": "/opt/freeservices"
  },
  "Launchd": {
    "SystemWide": false,
    "Label": "com.wsu.eit.freeservices",
    "LogPath": "/tmp/freeservices.log",
    "ErrorLogPath": "/tmp/freeservices.error.log"
  }
}
```

> **Note:** Empty strings for `ExePath`, `OutputPath`, and `Runtime` use platform-aware defaults. The Installer auto-detects your OS and architecture at runtime (e.g., `osx-arm64`, `linux-x64`, `win-x64`).

Every setting can be overridden via CLI args using the same dotted key path:

```bash
dotnet run -- deploy --Service:Name=MyService --Publish:Runtime=linux-arm64
dotnet run -- deploy --Systemd:User=appuser --Systemd:WorkingDirectory=/opt/myservice
dotnet run -- deploy --Launchd:SystemWide=true --Launchd:Label=com.example.myservice
```

---

## Azure DevOps Pipelines

Two pipeline definitions are included:

| Pipeline | Purpose |
|----------|---------|
| `azure-pipelines-crossplatform.yml` | **Primary.** Builds and tests on Windows, Linux, and macOS in parallel. Uses `FileTransform@2` to inject `variables.yml` settings into `appsettings.json`. |
| `azure-pipelines-service.yml` | Reference. Windows-only build + publish artifacts. Simpler single-platform starting point. |

### Cross-Platform Pipeline (`azure-pipelines-crossplatform.yml`)

1. **Stage 1 — Hello World Brief:** Environment dump on `windows-latest` (sanity check)
2. **Stage 2 — Build & Test:** Three parallel jobs (Windows, Linux, macOS). Each installs .NET 10, builds the solution, injects test variables via `FileTransform@2`, runs Test 1, and verifies Installer platform detection.
3. **Stage 3 — Summary:** Reports cross-platform results.

Test configuration is centralized in `variables.yml` — change test behavior from one file without editing code.

### Using it

1. Create a new pipeline in your Azure DevOps project
2. Point it at `azure-pipelines-crossplatform.yml`
3. Run it — manual trigger only (no branch trigger by default)

### The deployment roadmap

```
Phase 1 (done):     Build + publish artifacts on windows-latest
                    └─ azure-pipelines-service.yml

Phase 2 (done):     Cross-platform build + test with FileTransform
                    └─ azure-pipelines-crossplatform.yml
                    └─ Test 1 runs on Windows, Linux, and macOS agents
                    └─ Proves the service starts and produces output on all platforms

Phase 3 (target):   Deploy to WSU-EIT environments
                    └─ Use FreeCICD pipeline templates
                    └─ dev.azure.com/wsueit environments: Dev → CMS → Prod
                    └─ Same pattern as FreeCICD deployment
                    └─ Installer CLI runs in the pipeline with --action deploy
```

---

## Relationship to Other WSU-EIT Projects

| Project | Repo | How FreeServices uses it |
|---------|------|------------------------|
| **[FreeCICD](https://github.com/WSU-EIT/FreeCICD)** | github.com/WSU-EIT/FreeCICD | Pipeline templates for Docker-containerized builds, multi-environment deployment patterns (Dev → CMS → Prod), build-info injection |
| **[FreeTools](https://github.com/WSU-EIT/FreeTools)** | github.com/WSU-EIT/FreeTools | CLI patterns (`CliArgs`, `ConsoleOutput`), .NET Aspire orchestration examples, the `ForkCRM` scaffolding model |
| **[FreeQEMU](https://github.com/WSU-EIT/FreeQEMU)** | github.com/WSU-EIT/FreeQEMU | Future: cross-compile Linux services from Windows using QEMU VMs (prototype proven in HelloWorld.BuildTool) |
| **FreeHub** | github.com/WSU-EIT/FreeHub | The monorepo where the prototype iterations live. FreeServices is extracted from `PrototypeExploratoryCode/HelloWorld.ServiceManager`. |

---

## What the Service Collects

The included example service (`FreeServices.Service`) is a system monitor that collects:

| Category | Data | Cross-platform |
|----------|------|:-:|
| **OS** | Machine name, OS description, version, architecture | ✓ |
| **CPU** | Processor name, logical count, usage % | Windows + Linux |
| **Memory** | Total, free, used, usage % | Windows + Linux |
| **Disk** | All fixed drives: total, free, usage %, filesystem | ✓ |
| **Process** | PID, working set, thread count | ✓ |
| **.NET** | Framework description, process architecture | ✓ |
| **Uptime** | System uptime, service uptime, iteration count | ✓ |

This is a **reference service** — replace it with whatever your actual service needs to do. The Installer and TestMe don't care what the service does internally, only that it starts and produces output.

---

## Platform Details

| Platform | Service Hosting | Install Method | Recovery |
|----------|----------------|----------------|----------|
| **Windows** | `AddWindowsService()` — transparent console/service hybrid | `sc.exe create` with auto-start, description, crash recovery | Restart on all failure types via `sc.exe failure` |
| **Linux** | `AddSystemd()` — systemd-notify integration | Unit file at `/etc/systemd/system/`, `systemctl enable` | `Restart=on-failure` with configurable `RestartSec` |
| **macOS** | `ConsoleLifetime` — launchd manages the process | Plist at `~/Library/LaunchAgents/` (user) or `/Library/LaunchDaemons/` (system) | `KeepAlive=true` — launchd restarts automatically |

---

## Project Structure

```
FreeServices/
├── FreeServices.slnx                      # .NET 10 solution (3 projects)
├── Readme.md                              # You are here
├── LICENSE                                # MIT — WSU-EIT
├── .gitignore                             # bin/, obj/, logs, IDE files
│
├── azure-pipelines-crossplatform.yml      # Primary: 3-platform build + test
├── azure-pipelines-service.yml            # Reference: Windows-only build
├── variables.yml                          # Pipeline variables for FileTransform
│
├── FreeServices.Service/                  # The background service
│   ├── FreeServices.Service.csproj        # Worker SDK + 3 hosting packages
│   ├── Program.cs                         # Host builder (Windows/Linux/macOS)
│   ├── SystemMonitorService.cs            # BackgroundService — system monitor
│   ├── appsettings.json                   # Interval, log settings
│   └── README.md                          # Standalone project documentation
│
├── FreeServices.Installer/               # Build, deploy, manage
│   ├── FreeServices.Installer.csproj      # Console app + Configuration packages
│   ├── Program.cs                         # Dual CLI/UI with RunAction() dispatcher
│   ├── InstallerConfig.cs                 # Typed config (platform-aware defaults)
│   ├── appsettings.json                   # Service name, paths, platform settings
│   └── README.md                          # Standalone project documentation
│
└── FreeServices.TestMe/                   # Integration tests
    ├── FreeServices.TestMe.csproj         # Console app + 6 Configuration packages
    ├── Program.cs                         # Test 1: console mode, Test 2: service mode
    ├── appsettings.json                   # Test parameters (heartbeats, interval, etc.)
    └── README.md                          # Standalone project documentation
```

---

## Prerequisites

- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Windows 10/11 or Windows Server** — for Windows Service features
- **Linux with systemd** — for systemd service features (most modern distros)
- **macOS 12+** — for launchd agent/daemon features
- **Elevated privileges** — required for service install/uninstall (Administrator on Windows, sudo on Linux, user-level on macOS by default)

---

## License

[MIT License](LICENSE) — use it however you want.

---

**FreeServices** is developed and maintained by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

📧 Questions or feedback? Visit our [team page](https://em.wsu.edu/eit/meet-our-staff/) or open an issue on [GitHub](https://github.com/WSU-EIT/FreeServices/issues)