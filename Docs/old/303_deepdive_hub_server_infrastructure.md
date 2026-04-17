# 303 — Deep Dive: Hub Server Infrastructure

> **Document ID:** 303  
> **Category:** Reference — Deep Dive  
> **Investigator:** Agent 3 (Server Infrastructure Specialist)  
> **Scope:** `FreeServicesHub` server project — SignalR hub, API endpoints, middleware, monitor service  
> **Outcome:** Complete understanding of the server-side agent management infrastructure.

---

## Executive Summary

The FreeServicesHub server is a Blazor Server + WebAssembly interactive app built on the FreeCRM/FreeExamples base template. It extends the base with an agent management system consisting of four server-side components: (1) a SignalR hub for real-time agent communication, (2) an API key middleware for Bearer token validation, (3) REST API endpoints for registration/CRUD/heartbeats, and (4) a background monitor service that detects stale agents and broadcasts status changes to dashboards.

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                     FreeServicesHub Server                           │
│                                                                      │
│  ┌─────────────┐   ┌────────────────┐   ┌────────────────────────┐  │
│  │  SignalR Hub │   │ ApiKeyMiddleware│   │  AgentMonitorService  │  │
│  │ /freeservices│   │ /api/agent/*   │   │  (BackgroundService)  │  │
│  │  hubHub      │◄──┤ Bearer → SHA256│   │  poll → detect →      │  │
│  │              │   │ → AgentId      │   │  broadcast            │  │
│  └──────┬───────┘   └────────────────┘   └───────────┬────────────┘  │
│         │                                            │              │
│  ┌──────┴──────────────────────────────────────────────┴────────────┐│
│  │                    DataController (API Endpoints)                ││
│  │  RegisterAgent    SaveHeartbeat    GetAgents    GetHeartbeats    ││
│  │  GenerateKeys     RevokeToken      SaveAgents   DeleteAgents    ││
│  └──────────────────────────────────────────────────────────────────┘│
│         │                                                            │
│  ┌──────┴──────────────────────────────────────────────────────────┐ │
│  │                    DataAccess Layer                              │ │
│  │  (see 304_deepdive_data_layer.md)                               │ │
│  └─────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────┘
```

---

<a id="program-startup"></a>
## Program Startup — Partial Class Pattern

The server uses a **partial class** pattern inherited from FreeCRM/FreeExamples. The main `Program.cs` sets up the generic web host, and `Program.App.cs` provides app-specific hooks:

### `FreeServicesHub.App.Program.cs` (App-Specific)

```csharp
// Builder phase — register AgentMonitorService
private static WebApplicationBuilder MyAppModifyBuilderEnd(WebApplicationBuilder Output)
{
    Output.Services.AddHostedService<AgentMonitorService>();
    return Output;
}

// App phase — register ApiKeyMiddleware
private static WebApplication MyAppModifyStart(WebApplication Output)
{
    Output.UseMiddleware<ApiKeyMiddleware>();
    return Output;
}
```

### `Program.App.cs` (Hook Wiring)

The `AppModifyBuilderEnd` and `AppModifyStart` methods are called by the base `Program.cs` at the right points in the startup sequence. This allows app-specific code to plug into the generic host without modifying the base template.

### Configuration Loading

`MyConfigurationHelpersLoadApp` reads app-specific settings from `appsettings.json`:

| Setting | Default | Purpose |
|---------|---------|---------|
| `App:AgentHeartbeatIntervalSeconds` | 30 | Expected heartbeat frequency |
| `App:AgentStaleThresholdSeconds` | 120 | Seconds before an agent is marked stale |
| `App:RegistrationKeyExpiryHours` | 24 | How long a registration key is valid |
| `App:HeartbeatRetentionHours` | 24 | How long heartbeat data is kept |
| `App:CpuWarningThreshold` | 70 | CPU% to trigger Warning status |
| `App:CpuErrorThreshold` | 90 | CPU% to trigger Error status |
| `App:MemoryWarningThreshold` | 70 | Memory% to trigger Warning |
| `App:MemoryErrorThreshold` | 90 | Memory% to trigger Error |
| `App:DiskWarningThreshold` | 50 | Disk% to trigger Warning |
| `App:DiskErrorThreshold` | 90 | Disk% to trigger Error |

---

<a id="signalr-hub"></a>
## SignalR Hub (`freeserviceshubHub`)

**Path:** `FreeServicesHub/Hubs/signalrHub.cs`

The hub is a strongly-typed hub (`Hub<IsrHub>`) with `[Authorize]` attribute. It provides:

### Interface

```csharp
public partial interface IsrHub
{
    Task SignalRUpdate(DataObjects.SignalRUpdate update);
}
```

### Methods

| Method | Purpose |
|--------|---------|
| `JoinTenantId(string)` | Subscribe to tenant-specific SignalR group (for dashboard viewers) |
| `SignalRUpdate(SignalRUpdate)` | Broadcast an update to a tenant group or all clients |

### Agent-Specific Hub Usage

The hub is extended (via partial interface and class) for agent operations. Agents:
1. Connect with Bearer token auth (`AccessTokenProvider`)
2. Join the `"Agents"` group via `JoinGroup("Agents")`
3. Call `SendHeartbeat(snapshot)` to push telemetry
4. Receive `Shutdown` command from the server

Dashboard clients join the `"AgentMonitor"` group to receive status broadcasts.

### SignalR Update Types (Agent-Specific)

```csharp
public const string AgentHeartbeat = "AgentHeartbeat";
public const string AgentConnected = "AgentConnected";
public const string AgentDisconnected = "AgentDisconnected";
public const string AgentStatusChanged = "AgentStatusChanged";
public const string AgentShutdown = "AgentShutdown";
public const string RegistrationKeyGenerated = "RegistrationKeyGenerated";
```

---

<a id="api-key-middleware"></a>
## API Key Middleware

**Path:** `FreeServicesHub.App.ApiKeyMiddleware.cs`

Intercepts requests to `/api/agent/*` routes and validates Bearer tokens.

### Flow

```
Request → Check path starts with "/api/agent/"
  ├─ No  → pass through (next middleware)
  └─ Yes → Extract Authorization header
        ├─ Missing → 401 { error: "missing_token" }
        ├─ Not Bearer → 401 { error: "invalid_format" }
        └─ Has token → SHA-256 hash → lookup in ApiClientTokens table
              ├─ Not found / revoked / inactive → 401 { error: "invalid_token" }
              └─ Found → Stash AgentId + TenantId in HttpContext.Items → next
```

### Design Notes

- **SHA-256 hashing** — tokens are never stored in plaintext. The middleware hashes the incoming token and compares against `TokenHash` in the database.
- **Scoped DbContext** — creates a service scope to get the EF `EFDataModel`; queries `ApiClientTokens` with `AsNoTracking()`.
- **Context stashing** — downstream controllers read `HttpContext.Items["AgentId"]` and `HttpContext.Items["AgentTenantId"]` without re-validating.
- **Route scoping** — only `/api/agent/*` routes are intercepted. Regular user auth flows are unaffected.

---

<a id="api-endpoints"></a>
## API Endpoints (`FreeServicesHub.App.API.cs`)

All defined as partial methods on `DataController`:

### Agent CRUD

| Route | Auth | Method | Purpose |
|-------|------|--------|---------|
| `POST /api/Data/GetAgents` | Authorize | `GetAgents(List<Guid>?)` | List agents for current tenant |
| `POST /api/Data/SaveAgents` | Admin | `SaveAgents(List<Agent>)` | Create/update agents |
| `POST /api/Data/DeleteAgents` | Admin | `DeleteAgents(List<Guid>)` | Soft-delete agents |

### Registration

| Route | Auth | Method | Purpose |
|-------|------|--------|---------|
| `POST /api/Data/RegisterAgent` | AllowAnonymous | `RegisterAgent(AgentRegistrationRequest)` | One-time agent registration |
| `POST /api/Data/GenerateRegistrationKeys/{count}` | Admin | `GenerateRegistrationKeys(int)` | Create new registration keys |

### Token Management

| Route | Auth | Method | Purpose |
|-------|------|--------|---------|
| `POST /api/Data/GetApiClientTokens` | Admin | `GetApiClientTokens()` | List all tokens for tenant |
| `POST /api/Data/RevokeApiClientToken/{id}` | Admin | `RevokeApiClientToken(Guid)` | Revoke a specific token |

### Heartbeats

| Route | Auth | Method | Purpose |
|-------|------|--------|---------|
| `POST /api/Data/SaveHeartbeat` | Authorize | `SaveHeartbeat(AgentHeartbeat)` | Store heartbeat + update agent status |
| `POST /api/Data/GetHeartbeats/{agentId}` | Admin | `GetHeartbeats(Guid, int)` | Get heartbeat history (default 24h) |

### Registration Flow (End-to-End)

```
Agent                           Server
  │                               │
  │ POST /api/Data/RegisterAgent  │
  │ { RegistrationKey, Hostname } │
  │──────────────────────────────►│
  │                               │ ValidateRegistrationKey()
  │                               │   → SHA-256(key) → lookup in DB
  │                               │   → check: not used, not expired, correct tenant
  │                               │ Create Agent record
  │                               │ Burn registration key (mark Used=true)
  │                               │ GenerateApiClientToken()
  │                               │   → random 32 bytes → Base64
  │                               │   → SHA-256(plaintext) → store hash
  │                               │ SignalRUpdate(AgentConnected)
  │                               │
  │ { AgentId, ApiClientToken }   │
  │◄──────────────────────────────│
  │                               │
  │ Agent persists token to       │
  │ appsettings.json              │
```

---

<a id="agent-monitor-service"></a>
## AgentMonitorService (BackgroundService)

**Path:** `FreeServicesHub.App.AgentMonitorService.cs`

A server-side background service that polls for agent staleness and broadcasts status changes.

### Pattern

Direct adaptation of `FreeCICD.App.PipelineMonitorService`:

```
while (!stoppingToken.IsCancellationRequested)
{
    try {
        PollAndBroadcast();
        _consecutiveErrors = 0;
    } catch {
        _consecutiveErrors++;
    }

    delay = _pollInterval * min(_consecutiveErrors + 1, MaxBackoffMultiplier);
    await Task.Delay(delay);
}
```

### Poll-Detect-Broadcast Cycle

1. **Load all agents** via `da.GetAgents(null, Guid.Empty, null)`
2. **Check staleness** — if `LastHeartbeat < (now - AgentStaleThresholdSeconds)`:
   - Online → Stale
   - Other → Offline
3. **Detect changes** against in-memory `ConcurrentDictionary<Guid, string>` cache
4. **Broadcast changes** to `AgentMonitor` SignalR group:
   - `AgentStatusChanged` update with list of changed agents
5. **Always broadcast heartbeat** to `AgentMonitor` group so dashboards know the service is alive

### Timing

| Parameter | Value |
|-----------|-------|
| Initial delay | 10 seconds (let app finish startup) |
| Poll interval | 5 seconds |
| Max backoff | 60 seconds (5s × 12) |
| Stale threshold | Configurable (default 120s) |

### Differences from FreeCICD PipelineMonitorService

| Aspect | FreeCICD | FreeServicesHub |
|--------|----------|-----------------|
| Data source | Azure DevOps REST API (external) | Local database (internal) |
| Subscriber gating | Only polls when clients subscribed | Always polls |
| Cache seeding | First poll seeds without broadcasting | First-time agents always broadcast |
| Concurrency | SemaphoreSlim(5) for API calls | No concurrency control needed (DB query) |

---

<a id="heartbeat-ingestion"></a>
## Heartbeat Ingestion — Threshold-Based Status

When the server receives a heartbeat (via SignalR or HTTP), `DataAccess.SaveHeartbeat()`:

1. Stores the heartbeat record in `AgentHeartbeats` table
2. Updates the parent `Agent` record:
   - Sets `LastHeartbeat = now`
   - Evaluates CPU and memory against thresholds:
     - `≥ CpuError || ≥ MemError` → Status = `Error`
     - `≥ CpuWarning || ≥ MemWarning` → Status = `Warning`
     - Otherwise → Status = `Online`
3. Broadcasts `AgentHeartbeat` SignalR update to tenant group

This creates a real-time status pipeline: Agent → heartbeat → server evaluates → broadcasts to dashboard.

---

*Prev: [302 — Agent Installer](302_deepdive_agent_installer.md) | Next: [304 — Data Layer](304_deepdive_data_layer.md)*
