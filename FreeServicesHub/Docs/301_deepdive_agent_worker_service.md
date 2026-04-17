# 301 — Deep Dive: Agent Worker Service

> **Document ID:** 301  
> **Category:** Reference — Deep Dive  
> **Investigator:** Agent 1 (Worker Service Specialist)  
> **Scope:** `FreeServicesHub.Agent` project — the Windows Service executable  
> **Outcome:** Complete understanding of the agent lifecycle, from boot to heartbeat to shutdown.

---

## Executive Summary

`FreeServicesHub.Agent` is a .NET 10 Worker Service that runs as a Windows Service (`sc.exe` managed). It boots with Windows even if no user is logged in, auto-restarts on crash (via `sc.exe failure` recovery config), and runs an infinite heartbeat loop. Each iteration scrapes system telemetry (CPU, memory, disk) and posts it to the FreeServicesHub server via SignalR (primary) or HTTP REST (fallback).

---

## Project Structure

| File | Purpose |
|------|---------|
| `FreeServicesHub.Agent.csproj` | .NET 10 Worker SDK project, references `Microsoft.Extensions.Hosting.WindowsServices` and `Microsoft.AspNetCore.SignalR.Client` |
| `Program.cs` | Host builder — configures Windows Service lifetime, registers `AgentWorkerService` |
| `AgentWorkerService.cs` | The `BackgroundService` — registration, SignalR connection, heartbeat loop, system data collection |

### Dependencies (NuGet)

```
Microsoft.Extensions.Hosting                    10.0.0-preview.3
Microsoft.Extensions.Hosting.WindowsServices    10.0.0-preview.3
Microsoft.AspNetCore.SignalR.Client              10.0.0-preview.3
```

---

## Hosting Model

### Program.cs — Boot Sequence

```
Host.CreateDefaultBuilder(args)
  → UseWindowsService(ServiceName = "FreeServicesHubAgent")
  → ConfigureAppConfiguration(appsettings.json from AppContext.BaseDirectory)
  → ConfigureServices(AddHostedService<AgentWorkerService>)
  → Build()
  → Expose IHostApplicationLifetime (for remote Shutdown command)
  → Run()
```

**Key design decision:** `Program.Lifetime` is stored as a static property so the SignalR `Shutdown` handler inside `AgentWorkerService` can call `lifetime.StopApplication()` to trigger graceful shutdown from the hub.

### Windows Service Behavior

- **Boots with Windows** — installed via `sc.exe create ... start= auto`
- **No login required** — runs under LocalSystem by default
- **Crash recovery** — installer configures `sc.exe failure` with 3-stage restart (5s/5s/5s), reset period 86400s
- **Failure flag** — `sc.exe failureflag 1` ensures non-zero exit codes trigger recovery

---

## AgentWorkerService — The BackgroundService

<a id="lifecycle"></a>
### Lifecycle (ExecuteAsync)

```
┌─────────────────────────────────────────────┐
│ 1. Load AgentOptions from appsettings.json  │
│ 2. Registration (if no ApiClientToken)      │
│    ├─ POST /api/agents/register             │
│    ├─ Receive token                         │
│    └─ Persist token to appsettings.json     │
│ 3. Connect to SignalR hub                   │
│    ├─ Build HubConnection with Bearer auth  │
│    ├─ Register Shutdown handler             │
│    ├─ Register Reconnecting/Reconnected     │
│    └─ Start connection + JoinGroup("Agents")│
│ 4. Heartbeat Loop (while !cancelled)        │
│    ├─ CollectSnapshot() → SystemSnapshot    │
│    ├─ If SignalR connected:                 │
│    │   ├─ FlushBufferedHeartbeats()         │
│    │   └─ InvokeAsync("SendHeartbeat")      │
│    ├─ Else:                                 │
│    │   ├─ HTTP POST /api/agents/heartbeat   │
│    │   ├─ Buffer locally if both fail       │
│    │   └─ TryReconnectSignalR()             │
│    └─ Task.Delay(interval)                  │
│ 5. Shutdown — dispose HubConnection         │
└─────────────────────────────────────────────┘
```

<a id="configuration"></a>
### Configuration (`AgentOptions`)

Bound from `appsettings.json` → `Agent` section:

| Property | Default | Purpose |
|----------|---------|---------|
| `HubUrl` | `https://localhost:5001` | Base URL of the FreeServicesHub server |
| `RegistrationKey` | `""` | One-time registration key (burned after use) |
| `ApiClientToken` | `""` | Long-lived Bearer token (persisted after registration) |
| `HeartbeatIntervalSeconds` | `30` | Delay between heartbeat iterations |
| `AgentName` | `""` (falls back to `Environment.MachineName`) | Display name for the agent |

<a id="registration"></a>
### Registration Flow

1. If `ApiClientToken` is empty and `RegistrationKey` is present:
   - POST to `/api/agents/register` with `{ RegistrationKey, AgentName, MachineName }`
   - Server validates the key (SHA-256 hash lookup), creates an `Agent` record, burns the registration key, generates an `ApiClientToken`
   - Agent receives the plaintext token, persists it to `appsettings.json`
2. If `ApiClientToken` is already present: skip registration
3. If neither is present: log error and exit

**Token persistence:** The agent writes the token back into `appsettings.json` by parsing the JSON as a `JsonNode`, updating `Agent.ApiClientToken`, and writing it back. This survives service restarts.

<a id="signalr-connection"></a>
### SignalR Connection

- **URL:** `{HubUrl}/freeserviceshubHub`
- **Auth:** Bearer token via `AccessTokenProvider`
- **Reconnect:** `WithAutomaticReconnect(ExponentialBackoffRetryPolicy)`
- **Group:** Joins `"Agents"` group on connect and reconnect
- **Handlers:**
  - `Shutdown` — calls `Program.Lifetime.StopApplication()` for remote kill
  - `Reconnecting` — logs warning
  - `Reconnected` — re-joins `Agents` group, flushes buffered heartbeats

<a id="heartbeat-loop"></a>
### Heartbeat Loop

The core `while (!ct.IsCancellationRequested)` loop:

1. **Collect snapshot** — CPU, memory, drives, uptime, timestamp
2. **If SignalR connected:**
   - Flush any buffered heartbeats from prior disconnections
   - Send current snapshot via `InvokeAsync("SendHeartbeat", snapshot)`
3. **If SignalR disconnected:**
   - Attempt HTTP fallback: `POST /api/agents/heartbeat` with Bearer auth
   - If HTTP also fails: buffer locally (capped at 100 entries, FIFO eviction)
   - Attempt SignalR reconnection
4. **Delay** by `HeartbeatIntervalSeconds`

<a id="resilience"></a>
### Resilience Design

| Failure Mode | Mitigation |
|-------------|------------|
| SignalR disconnect | Automatic reconnect with exponential backoff + manual reconnect attempts in heartbeat loop |
| Both SignalR + HTTP down | Buffer heartbeats locally (up to 100), flush on reconnect |
| Process crash | Windows Service recovery restarts (3 stages, 5s each) |
| Remote kill needed | Hub sends `Shutdown` command via SignalR |
| Token lost | Registration key allows re-registration (but key is one-time use) |

<a id="system-data-collection"></a>
### System Data Collection (Windows Only)

#### SystemSnapshot Record

| Property | Source |
|----------|--------|
| `MachineName` | `Environment.MachineName` |
| `OsDescription` | `RuntimeInformation.OSDescription` |
| `ProcessorCount` | `Environment.ProcessorCount` |
| `CpuUsagePercent` | PowerShell: `(Get-CimInstance Win32_Processor).LoadPercentage` |
| `TotalMemoryMb` | `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` |
| `FreeMemoryMb` | Total - `GC.GetGCMemoryInfo().MemoryLoadBytes` |
| `UsedMemoryMb` | Derived |
| `MemoryUsagePercent` | Derived, rounded to 1 decimal |
| `Drives` | `DriveInfo.GetDrives()` filtered to `Fixed` + `IsReady` |
| `Uptime` | `DateTime.UtcNow - _startedUtc` |
| `TimestampUtc` | `DateTime.UtcNow` |

#### DriveSnapshot Record

| Property | Source |
|----------|--------|
| `Name` | Drive letter (e.g., `C:\`) |
| `DriveFormat` | e.g., `NTFS` |
| `TotalGb` | `drive.TotalSize` converted |
| `FreeGb` | `drive.AvailableFreeSpace` converted |
| `UsedPercent` | Derived, rounded to 1 decimal |

---

## Relationship to FreeServices.Service

`FreeServicesHub.Agent` is a direct evolution of `Examples/FreeServices/FreeServices.Service/SystemMonitorService.cs`:

| Aspect | FreeServices.Service | FreeServicesHub.Agent |
|--------|---------------------|----------------------|
| Output | Console + local log file | SignalR hub + HTTP API |
| Auth | None | Registration key → API client token |
| Network | None (local only) | SignalR + HTTP fallback |
| Buffering | None | 100-entry local buffer |
| Remote control | None | Shutdown command via SignalR |
| Platform | Cross-platform | Windows only |
| Data collection | Same pattern | Same pattern (CPU/mem/disk) |

The `SystemSnapshot` and `DriveSnapshot` records are nearly identical between the two projects — FreeServicesHub.Agent added `Uptime` and `TimestampUtc` fields while removing `ProcessorName`, `DotNetVersion`, `ProcessId`, `WorkingSetMb`, and `ThreadCount` (those are captured during registration instead).

---

## Key Code Paths

| Scenario | Entry Point | Flow |
|----------|-------------|------|
| First boot (no token) | `ExecuteAsync` | LoadOptions → RegisterWithHub → PersistToken → ConnectToSignalR → RunHeartbeatLoop |
| Normal boot (has token) | `ExecuteAsync` | LoadOptions → ConnectToSignalR → RunHeartbeatLoop |
| Network loss | `RunHeartbeatLoop` | SendHeartbeatViaHttp → buffer → TryReconnectSignalR |
| Reconnect success | `Reconnected` handler | JoinAgentsGroup → FlushBufferedHeartbeats |
| Remote shutdown | `Shutdown` SignalR handler | `Program.Lifetime.StopApplication()` |
| Process crash | OS level | `sc.exe failure` restarts the service after 5s |

---

*Next: [302 — Agent Installer Deep Dive](302_deepdive_agent_installer.md)*
