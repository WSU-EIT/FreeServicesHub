# 305 — Deep Dive: Lineage & Patterns

> **Document ID:** 305  
> **Category:** Reference — Deep Dive  
> **Investigator:** Agent 5 (Architecture & Lineage Specialist)  
> **Scope:** How FreeServicesHub derives from FreeServices, FreeCRM, FreeGLBA, FreeCICD, and FreeExamples  
> **Outcome:** Complete lineage map and pattern catalog showing what was borrowed, adapted, and invented.

---

## Executive Summary

FreeServicesHub is not built from scratch — it's a carefully composed system that draws patterns and code from five predecessor projects. Understanding the lineage is critical for maintaining consistency and knowing where to look for reference implementations.

---

<a id="lineage-map"></a>
## Lineage Map

```
FreeExamples (base template)
    │
    ├──► FreeCRM (renamed/extended — CRM-specific UI, data model)
    │       │
    │       └──► FreeServicesHub (renamed from FreeCRM, agent system added)
    │
    ├──► FreeGLBA (API key/token security model)
    │       │
    │       └──► FreeServicesHub (ApiKeyMiddleware, SHA-256 token pattern)
    │
    ├──► FreeCICD (SignalR real-time monitoring, background polling)
    │       │
    │       └──► FreeServicesHub (AgentMonitorService, poll-detect-broadcast)
    │
    └──► FreeServices (Windows Service + installer pattern)
            │
            └──► FreeServicesHub.Agent + FreeServicesHub.Agent.Installer
```

### What Each Ancestor Contributed

| Ancestor | Contribution to FreeServicesHub |
|----------|-------------------------------|
| **FreeExamples** | Base template: Blazor Server+WASM, SignalR hub, DataAccess/DataObjects/EFModels three-tier, partial class extension pattern, `Program.App.cs` hooks |
| **FreeCRM** | Renamed into FreeServicesHub. Provided: tenant model, user management, RBAC, department/group system, plugin architecture, setup wizard |
| **FreeGLBA** | API key security pattern: SHA-256 hashing, Bearer token middleware, key generation, revocation, prefix-based identification |
| **FreeCICD** | Real-time monitoring: `PipelineMonitorService` → `AgentMonitorService`, SignalR group broadcasting, `ConcurrentDictionary` caching, exponential backoff polling |
| **FreeServices** | Windows Service pattern: `BackgroundService` + `UseWindowsService()`, `sc.exe` installer, system data collection (CPU/memory/disk), `InstallerConfig` model |

---

<a id="pattern-catalog"></a>
## Pattern Catalog

<a id="pattern-poll-detect-broadcast"></a>
### Pattern 1: Poll-Detect-Broadcast (from FreeCICD)

**Origin:** `FreeCICD.App.PipelineMonitorService` — polls Azure DevOps for pipeline status changes.

**Adaptation:** `FreeServicesHub.AgentMonitorService` — polls the local database for agent staleness.

```
while (!cancelled) {
    try {
        Load current state
        Compare against ConcurrentDictionary cache
        Broadcast changes to SignalR group
        Always send heartbeat (so dashboards know service is alive)
        Reset error counter
    } catch {
        Increment error counter
    }
    
    Delay with exponential backoff (base × min(errors+1, maxMultiplier))
}
```

**Key differences in adaptation:**

| Aspect | FreeCICD (Pipeline) | FreeServicesHub (Agent) |
|--------|---------------------|------------------------|
| Data source | Azure DevOps REST API | Local EF database |
| Subscriber gating | Only polls when `PipelineMonitor` group has subscribers | Always polls (agents could go stale even with no viewers) |
| Cache seeding | First poll seeds without broadcasting (avoids flood) | First-time agents always broadcast (dashboard needs initial state) |
| Concurrency | `SemaphoreSlim(5)` for API call throttling | None needed (single DB query) |
| Backoff | 5s base, 12x max = 60s ceiling | Same (5s base, 12x max) |

<a id="pattern-api-key-security"></a>
### Pattern 2: API Key Security (from FreeGLBA)

**Origin:** FreeGLBA's API key management for external integrations.

**Adaptation:** Two-phase token system for agent authentication.

```
Phase 1: Registration Key (one-time, short-lived)
  Admin generates → SHA-256(plaintext) stored → plaintext shown once
  Agent uses key to register → key is "burned" (Used=true)

Phase 2: API Client Token (long-lived, revocable)
  Server generates during registration → SHA-256(plaintext) stored
  Agent persists plaintext → uses as Bearer token forever
  Admin can revoke (Active=false)
```

**Security properties preserved from FreeGLBA:**
- Never store plaintext — only SHA-256 hashes in database
- `KeyPrefix`/`TokenPrefix` (first 8 chars) stored for admin identification
- `RandomNumberGenerator.Fill()` for cryptographic randomness
- Revocation via `Active` flag + `RevokedAt`/`RevokedBy` audit trail

<a id="pattern-signalr-realtime"></a>
### Pattern 3: SignalR Real-Time Updates (from FreeExamples/FreeCICD)

**Origin:** FreeExamples base template — strongly-typed `Hub<IsrHub>` with `SignalRUpdate` DTO.

**Adaptation:** Agent-specific update types and groups.

```csharp
// Base template types (inherited)
SignalRUpdateType.User, .Setting, .Department, etc.

// FreeServicesHub additions
SignalRUpdateType.AgentHeartbeat
SignalRUpdateType.AgentConnected
SignalRUpdateType.AgentDisconnected
SignalRUpdateType.AgentStatusChanged
SignalRUpdateType.AgentShutdown
SignalRUpdateType.RegistrationKeyGenerated
```

**Groups:**
- `TenantId` groups — inherited from base, scoped by tenant
- `"Agents"` group — agent worker services join this
- `"AgentMonitor"` group — dashboard viewers subscribe to this

**Pattern preserved:** Every data write in `DataAccess` calls `SignalRUpdate()` so all connected clients get instant updates without polling.

<a id="pattern-backgroundservice"></a>
### Pattern 4: Windows Service as BackgroundService (from FreeServices)

**Origin:** `FreeServices.Service.SystemMonitorService` — a simple `BackgroundService` that collects system telemetry.

**Adaptation:** `FreeServicesHub.Agent.AgentWorkerService` — same collection but with network transport.

```
FreeServices.Service:
  while (!cancelled)
    CollectSnapshot()
    FormatReport()
    WriteOutput() → console + log file
    Delay(interval)

FreeServicesHub.Agent:
  RegisterWithHub()          ← NEW
  ConnectToSignalR()         ← NEW
  while (!cancelled)
    CollectSnapshot()        ← SAME pattern
    SendViaSignalR()         ← NEW (replaces local output)
    OR SendViaHttp()         ← NEW (fallback)
    OR BufferLocally()       ← NEW (resilience)
    Delay(interval)
```

**Data collection code is nearly identical** — same `DriveInfo.GetDrives()` loop, same GC memory info, same PowerShell CPU query. The FreeServicesHub.Agent adds `TimestampUtc` and `Uptime` while removing process-specific fields (those move to registration metadata instead).

<a id="pattern-installer"></a>
### Pattern 5: CLI/UI Installer (from FreeServices)

**Origin:** `FreeServices.Installer` — a cross-platform, 12-option interactive installer with full service account management.

**Adaptation:** `FreeServicesHub.Agent.Installer` — Windows-only, 7-option streamlined installer.

```
FreeServices.Installer (12 options):
  build, deploy, configure, remove, start, stop, status,
  config, users, instructions, cleanup, destroy
  + Service Account Manager submenu
  + Docker control
  + Platform detection (Windows/Linux/macOS)

FreeServicesHub.Agent.Installer (7 options):
  build, configure, remove, start, stop, status, destroy
  - No deploy (manual sequence)
  - No service accounts
  - No Docker
  - No cross-platform
  + API key injection into agent appsettings.json
  + Configured marker file (.configured)
```

**Preserved patterns:**
- `InstallerConfig` typed config model with CLI override via `--Section:Property=value`
- `RunAction()` central dispatcher with `switch` expression
- `RunInteractive()` menu loop with `Console.Clear()` + `PrintMenu()` + `Console.ReadLine()`
- `RunProcess()` helper with stdout/stderr capture
- `ResolveProjectPaths()` — walk up from exe to find project
- `ResolvePublishOutputPath()` — walk up to find `.sln`/`.slnx`
- `sc.exe` commands with access-denied guidance

---

<a id="cicd-deployment-model"></a>
## CI/CD Deployment Model

### FreeServices Model (Current)

```
Azure DevOps YAML Pipeline
  ├── Stage 1: Build
  │   └── dotnet publish → artifact
  ├── Stage 2: Deploy to DEV
  │   └── Drop files to target server
  ├── Stage 3: Deploy to TEST
  │   └── Drop files to target server
  └── Stage 4: Deploy to PROD
      └── Drop files to target server
      
  ⚠ Pipeline does NOT install the service
  ⚠ Sysadmin manually runs the installer exe
```

### FreeServicesHub.Agent Model (Target)

```
Azure DevOps YAML Pipeline
  ├── Stage 1: Build
  │   └── dotnet publish → artifact
  ├── Stage 2: Deploy + Install
  │   ├── Drop files to target server
  │   ├── Generate registration key (API call to hub)
  │   └── Run installer headlessly:
  │       installer.exe configure --Security:ApiKey=$(REG_KEY)
  │       installer.exe start
  ├── (Repeat for TEST, PROD)
  └── Agent auto-registers with hub on first heartbeat
```

The key innovation: the installer's non-interactive mode (`--Security:ApiKey=...`) allows full CI/CD automation without sysadmin intervention. The registration key is a one-time credential that can be generated by the pipeline, passed to the installer, and burned after use.

---

<a id="partial-class-extension"></a>
## Partial Class Extension Model (from FreeExamples)

The entire codebase uses a **partial class** pattern that allows app-specific code to extend the base template without modifying shared files:

```
Program.cs (base template — shared across all Free* projects)
Program.App.cs (app-specific hooks — unique to FreeServicesHub)

DataAccess.cs (base CRUD — shared)
FreeServicesHub.App.DataAccess.Agents.cs (agent-specific — unique)

DataObjects.cs (base DTOs — shared)
FreeServicesHub.App.DataObjects.Agents.cs (agent-specific — unique)

EFDataModel.cs (base DbContext — shared)
FreeServicesHub.App.EFDataModel.cs (agent DbSets — unique)
```

**File naming convention:** `FreeServicesHub.App.*.cs` — the `App` suffix indicates app-specific extensions to the base template.

---

<a id="what-was-invented"></a>
## What's New (Not From Ancestors)

| Feature | Description |
|---------|-------------|
| **Agent registration flow** | Two-phase key→token handshake is new. FreeGLBA had single-phase API keys. |
| **Heartbeat buffering** | 100-entry local buffer with flush-on-reconnect is new resilience mechanism. |
| **Threshold-based status** | CPU/memory thresholds driving Online→Warning→Error status transitions. |
| **AgentMonitorService stale detection** | Server-side detection of agents that stopped heartbeating. |
| **Remote Shutdown command** | Hub can remotely kill agents via SignalR. |
| **`.configured` marker** | Idempotency guard preventing double-installs. |
| **SignalR + HTTP dual transport** | Agent tries SignalR first, falls back to HTTP REST. |
| **Token persistence** | Agent writes its API token back to its own `appsettings.json`. |

---

<a id="solution-layout"></a>
## Solution Layout — Core vs. Examples

```
FreeServicesHub.slnx
├── FreeServicesHub/                    ← THE APPLICATION
│   ├── FreeServicesHub/               (Blazor Server host)
│   ├── FreeServicesHub.Client/        (Blazor WASM client)
│   ├── FreeServicesHub.DataAccess/    (Business logic)
│   ├── FreeServicesHub.DataObjects/   (DTOs)
│   ├── FreeServicesHub.EFModels/      (EF Core entities)
│   ├── FreeServicesHub.Plugins/       (Plugin system)
│   └── Docs/                         (This documentation)
│
├── FreeServicesHub.Agent/             ← AGENT (Windows Service)
├── FreeServicesHub.Agent.Installer/   ← INSTALLER (CLI/UI)
│
└── Examples/                          ← ANCESTORS (read-only reference)
    ├── FreeCRM/                       (Base CRM → renamed into hub)
    ├── FreeCICD/                      (Monitor pattern source)
    ├── FreeGLBA/                      (API key pattern source)
    ├── FreeServices/                  (Service + installer pattern source)
    └── FreeTools/FreeExamples/        (Base template source)
```

---

*Prev: [304 — Data Layer](304_deepdive_data_layer.md) | Next: [306 — Master Index](306_deepdive_master_index.md)*
