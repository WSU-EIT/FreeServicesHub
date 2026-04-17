# 306 — Deep Dive: Master Index

> **Document ID:** 306  
> **Category:** Reference — Deep Dive Index  
> **Author:** Master Compiler (synthesized from Agents 1–5)  
> **Scope:** Consolidated findings from the 300-series deep dive into the FreeServicesHub Agent system  
> **Outcome:** A single entry point into all research with briefs and direct links.

---

## What This Is

Five parallel deep-dive investigations were conducted across the FreeServicesHub agent subsystem. Each focused on a distinct layer of the architecture. This document compiles the key findings into an index with brief summaries and links back to the detailed research.

---

## System at a Glance

FreeServicesHub is a Blazor Server + WebAssembly application (evolved from FreeCRM/FreeExamples) that manages a fleet of remote Windows agents. Each agent is a .NET 10 Worker Service that boots with Windows, survives crashes via `sc.exe` failure recovery, and runs an infinite heartbeat loop posting system telemetry (CPU, memory, disk) to the hub via SignalR. The hub stores heartbeats, evaluates threshold-based health status, detects stale agents, and broadcasts everything to Blazor dashboards in real time.

### The End-to-End Flow

```
┌─────────────────────────┐          ┌──────────────────────────────────┐
│  CI/CD Pipeline         │          │  FreeServicesHub Server          │
│  1. Build agent         │          │                                  │
│  2. Generate reg key ───┼──────────┤► POST /api/Data/GenerateKeys     │
│  3. Deploy files        │          │                                  │
│  4. Run installer ──────┼──────────┤► Headless: configure + start     │
└─────────────────────────┘          │                                  │
                                     │  Registration:                   │
┌─────────────────────────┐          │  ► Validate key (SHA-256)        │
│  Agent (Windows Service)│          │  ► Create Agent record           │
│  1. Register ───────────┼──────────┤► Burn key, issue ApiClientToken  │
│  2. Connect SignalR ────┼──────────┤► Join "Agents" group             │
│  3. Heartbeat loop ─────┼──────────┤► SendHeartbeat (every 30s)       │
│     ├─ CPU/mem/disk     │          │  ► Store in DB                   │
│     ├─ SignalR primary  │          │  ► Evaluate thresholds           │
│     └─ HTTP fallback    │          │  ► Broadcast to tenant group     │
└─────────────────────────┘          │                                  │
                                     │  AgentMonitorService:            │
┌─────────────────────────┐          │  ► Poll every 5s                 │
│  Blazor Dashboard       │          │  ► Detect stale agents           │
│  ► AgentMonitor group ──┼──────────┤► Broadcast status changes        │
│  ► Real-time updates    │          │                                  │
└─────────────────────────┘          └──────────────────────────────────┘
```

---

## Research Index

### [301 — Agent Worker Service](301_deepdive_agent_worker_service.md)

**Investigator:** Agent 1 — Worker Service Specialist

**Brief:** The `FreeServicesHub.Agent` is a .NET 10 `BackgroundService` hosted as a Windows Service. It registers with the hub using a one-time registration key, receives a long-lived API client token, connects to SignalR with Bearer auth, and enters an infinite heartbeat loop. System telemetry (CPU via PowerShell, memory via GC, drives via `DriveInfo`) is sent every 30 seconds. Resilience includes: SignalR automatic reconnect with exponential backoff, HTTP REST fallback, local buffering (100 entries), and OS-level crash recovery via `sc.exe failure`.

**Key sections:**
- [Lifecycle flow](301_deepdive_agent_worker_service.md#lifecycle)
- [Configuration (AgentOptions)](301_deepdive_agent_worker_service.md#configuration)
- [Registration flow](301_deepdive_agent_worker_service.md#registration)
- [SignalR connection](301_deepdive_agent_worker_service.md#signalr-connection)
- [Heartbeat loop](301_deepdive_agent_worker_service.md#heartbeat-loop)
- [Resilience design](301_deepdive_agent_worker_service.md#resilience)
- [System data collection](301_deepdive_agent_worker_service.md#system-data-collection)

---

### [302 — Agent Installer](302_deepdive_agent_installer.md)

**Investigator:** Agent 2 — Installer & Deployment Specialist

**Brief:** The `FreeServicesHub.Agent.Installer` is a dual CLI/UI console app for building, deploying, and managing the agent Windows Service. It supports fully headless operation (all prompts skippable via `--Security:ApiKey=...` CLI arg) for CI/CD integration. Core workflow: `dotnet publish` → copy files to install path → inject registration key into `appsettings.json` → `sc.exe create` with auto-start and failure recovery → write `.configured` marker. Stripped down from the cross-platform FreeServices.Installer to Windows-only with 7 menu options.

**Key sections:**
- [Configuration model](302_deepdive_agent_installer.md#configuration-model)
- [Dual-mode interface](302_deepdive_agent_installer.md#dual-mode)
- [Action reference](302_deepdive_agent_installer.md#actions)
- [Windows Service installation detail](302_deepdive_agent_installer.md#windows-service-install)
- [API key injection](302_deepdive_agent_installer.md#api-key-injection)
- [Configured marker](302_deepdive_agent_installer.md#configured-marker)
- [Path resolution](302_deepdive_agent_installer.md#path-resolution)
- [Comparison to FreeServices.Installer](302_deepdive_agent_installer.md#comparison-to-freeservices)
- [CI/CD integration model](302_deepdive_agent_installer.md#cicd-integration)

---

### [303 — Hub Server Infrastructure](303_deepdive_hub_server_infrastructure.md)

**Investigator:** Agent 3 — Server Infrastructure Specialist

**Brief:** The server side consists of four agent-specific components plugged into the base Blazor app via the partial class pattern. `ApiKeyMiddleware` intercepts `/api/agent/*` routes and validates SHA-256-hashed Bearer tokens. The `DataController` partial exposes REST endpoints for registration, CRUD, heartbeats, and token management. `AgentMonitorService` is a `BackgroundService` that polls every 5 seconds, detects stale agents (>120s since last heartbeat), and broadcasts changes to the `AgentMonitor` SignalR group. The SignalR hub handles real-time agent communication with tenant-scoped groups.

**Key sections:**
- [Program startup (partial class hooks)](303_deepdive_hub_server_infrastructure.md#program-startup)
- [SignalR hub](303_deepdive_hub_server_infrastructure.md#signalr-hub)
- [API key middleware](303_deepdive_hub_server_infrastructure.md#api-key-middleware)
- [API endpoints](303_deepdive_hub_server_infrastructure.md#api-endpoints)
- [AgentMonitorService](303_deepdive_hub_server_infrastructure.md#agent-monitor-service)
- [Heartbeat ingestion & thresholds](303_deepdive_hub_server_infrastructure.md#heartbeat-ingestion)

---

### [304 — Data Layer](304_deepdive_data_layer.md)

**Investigator:** Agent 4 — Data Layer Specialist

**Brief:** Four EF Core entities (`Agent`, `AgentHeartbeat`, `RegistrationKey`, `ApiClientToken`) with corresponding DTOs and DataAccess methods. The security model uses SHA-256 hashing throughout — plaintext keys/tokens are returned once on generation and never stored. Registration keys are one-time-use with expiry. API client tokens are long-lived and revocable. Heartbeats are time-series data with CPU/memory thresholds driving agent status transitions (Online → Warning → Error). All write operations broadcast SignalR updates to tenant groups.

**Key sections:**
- [EF Models (database schema)](304_deepdive_data_layer.md#ef-models)
- [DataObjects (DTOs)](304_deepdive_data_layer.md#data-objects)
- [Security model (SHA-256)](304_deepdive_data_layer.md#security-model)
- [DataAccess operations](304_deepdive_data_layer.md#data-access-operations)
- [SignalR integration](304_deepdive_data_layer.md#signalr-integration)
- [Configuration](304_deepdive_data_layer.md#configuration)
- [Entity relationship diagram](304_deepdive_data_layer.md#entity-relationship)

---

### [305 — Lineage & Patterns](305_deepdive_lineage_and_patterns.md)

**Investigator:** Agent 5 — Architecture & Lineage Specialist

**Brief:** FreeServicesHub is composed from five ancestors: FreeExamples (base template), FreeCRM (renamed into hub), FreeGLBA (API key security), FreeCICD (real-time monitoring), and FreeServices (Windows Service + installer). Five key patterns were identified: (1) poll-detect-broadcast from FreeCICD, (2) SHA-256 API key security from FreeGLBA, (3) SignalR real-time updates from FreeExamples, (4) BackgroundService Windows hosting from FreeServices, (5) CLI/UI installer from FreeServices. New inventions include: two-phase registration, heartbeat buffering, threshold-based status, stale detection, remote shutdown, and `.configured` idempotency.

**Key sections:**
- [Lineage map](305_deepdive_lineage_and_patterns.md#lineage-map)
- [Pattern 1: Poll-Detect-Broadcast](305_deepdive_lineage_and_patterns.md#pattern-poll-detect-broadcast)
- [Pattern 2: API Key Security](305_deepdive_lineage_and_patterns.md#pattern-api-key-security)
- [Pattern 3: SignalR Real-Time](305_deepdive_lineage_and_patterns.md#pattern-signalr-realtime)
- [Pattern 4: BackgroundService](305_deepdive_lineage_and_patterns.md#pattern-backgroundservice)
- [Pattern 5: CLI/UI Installer](305_deepdive_lineage_and_patterns.md#pattern-installer)
- [CI/CD deployment model](305_deepdive_lineage_and_patterns.md#cicd-deployment-model)
- [Partial class extension model](305_deepdive_lineage_and_patterns.md#partial-class-extension)
- [What's new (not from ancestors)](305_deepdive_lineage_and_patterns.md#what-was-invented)
- [Solution layout](305_deepdive_lineage_and_patterns.md#solution-layout)

---

## Cross-Cutting Findings

### Security Chain

```
Admin generates RegistrationKey → plaintext shown ONCE
  ↓
CI/CD pipeline passes key to installer → written to agent appsettings.json
  ↓
Agent sends key to /api/Data/RegisterAgent → server validates SHA-256 hash
  ↓
Server burns key → generates ApiClientToken → plaintext returned ONCE
  ↓
Agent persists token → uses as Bearer auth for all subsequent calls
  ↓
ApiKeyMiddleware validates SHA-256(token) on every /api/agent/* request
  ↓
Admin can revoke token at any time via /api/Data/RevokeApiClientToken
```

### Resilience Chain

```
Layer 1: OS-level         sc.exe failure recovery (restart on crash, 3 attempts)
Layer 2: Transport        SignalR auto-reconnect + manual reconnect attempts
Layer 3: Fallback         HTTP REST when SignalR is down
Layer 4: Buffering        100-entry local buffer, flush on reconnect
Layer 5: Detection        Server-side AgentMonitorService marks stale agents
Layer 6: Remote control   Hub can send Shutdown command via SignalR
```

### Data Flow

```
Agent CollectSnapshot() → SystemSnapshot
  → SignalR InvokeAsync("SendHeartbeat") or HTTP POST /api/agents/heartbeat
    → Server DataAccess.SaveHeartbeat()
      → Store AgentHeartbeat record
      → Evaluate CPU/memory thresholds → update Agent.Status
      → SignalRUpdate(AgentHeartbeat) to tenant group
        → Blazor dashboard renders in real time
          
AgentMonitorService (every 5s)
  → Load all agents from DB
  → Check LastHeartbeat vs stale threshold (120s)
  → Compare against ConcurrentDictionary cache
  → Broadcast changes to AgentMonitor group
```

---

## Quick Reference — File Map

| File | Project | Role |
|------|---------|------|
| `FreeServicesHub.Agent/Program.cs` | Agent | Host builder, Windows Service |
| `FreeServicesHub.Agent/AgentWorkerService.cs` | Agent | BackgroundService — registration, SignalR, heartbeat |
| `FreeServicesHub.Agent.Installer/Program.cs` | Installer | CLI/UI, sc.exe, file copy, key injection |
| `FreeServicesHub.Agent.Installer/InstallerConfig.cs` | Installer | Typed config model |
| `FreeServicesHub/Hubs/signalrHub.cs` | Server | SignalR hub (strongly-typed) |
| `FreeServicesHub/FreeServicesHub.App.ApiKeyMiddleware.cs` | Server | Bearer token middleware |
| `FreeServicesHub/FreeServicesHub.App.AgentMonitorService.cs` | Server | Stale agent detection |
| `FreeServicesHub/FreeServicesHub.App.Program.cs` | Server | App-specific startup hooks |
| `FreeServicesHub/Controllers/FreeServicesHub.App.API.cs` | Server | REST endpoints |
| `FreeServicesHub.DataObjects/FreeServicesHub.App.DataObjects.Agents.cs` | DataObjects | Agent DTOs |
| `FreeServicesHub.DataObjects/FreeServicesHub.App.DataObjects.ApiKeys.cs` | DataObjects | Key/Token DTOs |
| `FreeServicesHub.DataObjects/FreeServicesHub.App.Config.cs` | DataObjects | Threshold config |
| `FreeServicesHub.DataObjects/DataObjects.App.cs` | DataObjects | SignalR update types |
| `FreeServicesHub.DataAccess/FreeServicesHub.App.DataAccess.Agents.cs` | DataAccess | Agent CRUD |
| `FreeServicesHub.DataAccess/FreeServicesHub.App.DataAccess.Registration.cs` | DataAccess | Registration flow |
| `FreeServicesHub.DataAccess/FreeServicesHub.App.DataAccess.ApiKeys.cs` | DataAccess | Key/token management |
| `FreeServicesHub.DataAccess/FreeServicesHub.App.DataAccess.Heartbeats.cs` | DataAccess | Heartbeat CRUD + pruning |
| `FreeServicesHub.EFModels/EFModels/FreeServicesHub.App.Agent.cs` | EFModels | Agent entity |
| `FreeServicesHub.EFModels/EFModels/FreeServicesHub.App.AgentHeartbeat.cs` | EFModels | Heartbeat entity |
| `FreeServicesHub.EFModels/EFModels/FreeServicesHub.App.RegistrationKey.cs` | EFModels | Registration key entity |
| `FreeServicesHub.EFModels/EFModels/FreeServicesHub.App.ApiClientToken.cs` | EFModels | API token entity |
| `FreeServicesHub.EFModels/EFModels/FreeServicesHub.App.EFDataModel.cs` | EFModels | DbContext partial |

---

*This is document 306 of the 300-series deep dive. See individual docs (301–305) for detailed analysis.*
