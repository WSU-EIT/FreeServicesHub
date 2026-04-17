# 307 — Feasibility Analysis, Integration Gaps & Testing Strategy

> **Document ID:** 307  
> **Category:** Engineering — Feasibility & Test Planning  
> **Scope:** Can the agent system work end-to-end today? What's broken, what options exist for testing, and what's the fastest path to a green pipeline?  
> **Prerequisite Reading:** 301–306 (deep dives)

---

## Executive Summary

**Will it work right now? No.**

The agent, hub, and installer were built in parallel with assumed interfaces that don't match. The data layer (EFModels, DataObjects, DataAccess) is solid and internally consistent, but the **transport layer** — HTTP routes, SignalR hub methods, authentication middleware, payload shapes, and port defaults — has 10 contract mismatches that would prevent registration, heartbeat delivery, and SignalR connectivity.

The installer **does not deploy the web app** — it only builds, copies, and registers the agent as a Windows Service. The hub must be running independently before agents can connect.

None of these gaps are architectural — they're wiring bugs. Once fixed, the end-to-end flow is sound. This doc catalogs every gap, prioritizes fixes, and lays out all testing options from local F5 to Aspire orchestration to full CI/CD pipelines.

---

## Part 1 — Integration Gap Analysis

### Gap Summary Table

| # | Category | What Agent Does | What Server Expects | Severity | File(s) |
|---|----------|----------------|---------------------|----------|---------|
| 1 | **Registration URL** | `POST /api/agents/register` | `POST /api/Data/RegisterAgent` | 🔴 Blocker | `AgentWorkerService.cs:184`, `FreeServicesHub.App.API.cs:59` |
| 2 | **Heartbeat URL** | `POST /api/agents/heartbeat` | `POST /api/Data/SaveHeartbeat` | 🔴 Blocker | `AgentWorkerService.cs:387`, `FreeServicesHub.App.API.cs:96` |
| 3 | **Hub Method: JoinGroup** | `InvokeAsync("JoinGroup", "Agents")` | Hub has no `JoinGroup` method — only `JoinTenantId(string)` | 🔴 Blocker | `AgentWorkerService.cs:307`, `signalrHub.cs` |
| 4 | **Hub Method: SendHeartbeat** | `InvokeAsync("SendHeartbeat", snapshot)` | Hub has no `SendHeartbeat` method — only `SignalRUpdate(SignalRUpdate)` | 🔴 Blocker | `AgentWorkerService.cs:336`, `signalrHub.cs` |
| 5 | **Default HubUrl Port** | Agent defaults to `https://localhost:5001` | Hub listens on `https://localhost:7271` / `http://localhost:5111` | 🔴 Blocker | `FreeServicesHub.Agent/appsettings.json`, `launchSettings.json` |
| 6 | **Registration Request Shape** | Sends `{ RegistrationKey, AgentName, MachineName }` | Expects `AgentRegistrationRequest { RegistrationKey, Hostname, OperatingSystem, Architecture, AgentVersion, DotNetVersion }` | 🟡 Major | `AgentWorkerService.cs:177-182`, `DataObjects.ApiKeys.cs:42-49` |
| 7 | **Registration Response Shape** | Reads `result["token"]` or `result["Token"]` | Returns `AgentRegistrationResponse { AgentId, ApiClientToken, HubUrl }` | 🟡 Major | `AgentWorkerService.cs:194-197`, `DataObjects.ApiKeys.cs:53-57` |
| 8 | **SignalR Auth** | Connects with `Bearer <ApiClientToken>` via `AccessTokenProvider` | Hub has `[Authorize]` using standard ASP.NET auth. `ApiKeyMiddleware` only intercepts `/api/agent/*` HTTP routes — does not cover SignalR WebSocket upgrade at `/freeserviceshubHub` | 🟡 Major | `AgentWorkerService.cs:249-252`, `ApiKeyMiddleware.cs:28-30`, `signalrHub.cs:13` |
| 9 | **No Aspire AppHost** | — | No orchestrated test harness exists for FreeServicesHub (only in FreeTools) | 🟠 Enhancement | N/A |
| 10 | **No CI/CD Pipeline** | — | No YAML pipeline definition exists | 🟠 Enhancement | N/A |

### Gap Details

#### Gap 1 & 2 — Wrong HTTP Routes

The agent hard-codes `/api/agents/register` and `/api/agents/heartbeat`. The server's `DataController` uses the template project's route convention: `/api/Data/RegisterAgent` and `/api/Data/SaveHeartbeat`.

**Fix:** Update `AgentWorkerService.cs` to use the correct routes:
```
/api/agents/register   →  /api/Data/RegisterAgent
/api/agents/heartbeat  →  /api/Data/SaveHeartbeat
```

#### Gap 3 & 4 — Missing SignalR Hub Methods

The hub (`signalrHub.cs`) has exactly two methods:
- `JoinTenantId(string TenantId)` — joins a tenant-scoped group
- `SignalRUpdate(DataObjects.SignalRUpdate update)` — broadcasts to a tenant group

The agent calls `JoinGroup("Agents")` and `SendHeartbeat(snapshot)` — neither exists.

**Fix (two options):**

**Option A — Add agent-specific methods to the hub:**
```csharp
public async Task JoinGroup(string groupName)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
}

public async Task SendHeartbeat(DataObjects.AgentHeartbeat heartbeat)
{
    // Save to DB + broadcast via SignalRUpdate
    // Reuse DataAccess.SaveHeartbeat() here
}
```

**Option B — Make the agent use existing methods:**
- Replace `JoinGroup("Agents")` with `JoinTenantId(tenantId)` (requires agent to know its `TenantId` from registration)
- Replace `SendHeartbeat(snapshot)` with `SignalRUpdate(new SignalRUpdate { ... })` wrapping the heartbeat in the existing `SignalRUpdate` envelope

Option A is cleaner for separation of concerns. Option B reuses existing code but mixes tenant UI updates with agent telemetry.

#### Gap 5 — Port Mismatch

- `FreeServicesHub.Agent/appsettings.json` → `"HubUrl": "https://localhost:5001"`
- `launchSettings.json` → hub runs on `https://localhost:7271` (https profile) or `http://localhost:5201` (http profile)

**Fix:** Update agent's `appsettings.json`:
```json
"HubUrl": "https://localhost:7271"
```

#### Gap 6 — Registration Request Payload

Agent sends:
```json
{ "RegistrationKey": "...", "AgentName": "...", "MachineName": "..." }
```

Server's `AgentRegistrationRequest` expects:
```json
{ "RegistrationKey": "...", "Hostname": "...", "OperatingSystem": "...",
  "Architecture": "...", "AgentVersion": "...", "DotNetVersion": "..." }
```

**Fix:** Update `AgentWorkerService.RegisterWithHub()` to construct a proper `AgentRegistrationRequest` or an anonymous object matching the expected properties. System information is readily available:
```csharp
var payload = new {
    RegistrationKey = _options.RegistrationKey,
    Hostname = Environment.MachineName,
    OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
    AgentVersion = typeof(AgentWorkerService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    DotNetVersion = Environment.Version.ToString()
};
```

#### Gap 7 — Registration Response Property

Agent looks for `result["token"]` or `result["Token"]`. Server returns `AgentRegistrationResponse`:
```csharp
public class AgentRegistrationResponse : ActionResponseObject
{
    public Guid AgentId { get; set; }
    public string ApiClientToken { get; set; } = string.Empty;
    public string HubUrl { get; set; } = string.Empty;
}
```

**Fix:** Read `ApiClientToken` instead of `token`:
```csharp
if (result.TryGetProperty("apiClientToken", out var tokenEl))
    return tokenEl.GetString();
if (result.TryGetProperty("ApiClientToken", out var tokenEl2))
    return tokenEl2.GetString();
```

Also: the response includes `AgentId` and `HubUrl` — the agent should persist these as well.

#### Gap 8 — SignalR Authentication

The hub class has `[Authorize]`, which requires a valid `ClaimsPrincipal`. The agent provides a Bearer token via `AccessTokenProvider`, but:

- `ApiKeyMiddleware` only activates on paths starting with `/api/agent/` — it does **not** intercept the SignalR negotiate/connect at `/freeserviceshubHub`
- Standard ASP.NET Bearer auth (`AddAuthentication().AddJwtBearer()`) is not configured to validate agent API client tokens

**Fix:** Add JWT Bearer events or a custom authentication handler that validates agent tokens on SignalR connections. The SignalR negotiate request sends the token as a query string parameter (`?access_token=...`). The fix should:

1. Register a custom auth scheme or extend the existing one:
```csharp
services.AddAuthentication()
    .AddScheme<AgentTokenAuthOptions, AgentTokenAuthHandler>("AgentToken", ...);
```

2. Or use `JwtBearerEvents.OnMessageReceived` to intercept and validate:
```csharp
options.Events = new JwtBearerEvents {
    OnMessageReceived = context => {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/freeserviceshubHub")) {
            // Hash + validate against ApiClientTokens table
        }
        return Task.CompletedTask;
    }
};
```

---

## Part 2 — What the Installer Does (and Doesn't Do)

### What It Does

| Action | Description |
|--------|-------------|
| `build` | Runs `dotnet publish` on the Agent project → output to `publish/` directory |
| `configure` | Copies published files to install path, writes API key to `appsettings.json`, runs `sc.exe create` + recovery config |
| `start` / `stop` | Runs `sc.exe start/stop` |
| `status` | Runs `sc.exe query` + tails last 10 log lines |
| `remove` | Runs `sc.exe stop` + `sc.exe delete`, clears API key |
| `destroy` | Full teardown: stop + delete service, delete install dir, delete publish dir, remove marker |

### What It Does NOT Do

- **Does not deploy the hub web app.** The hub must be deployed separately (IIS, Azure App Service, container, or running locally via `dotnet run`).
- **Does not generate registration keys.** Keys must be generated via the hub's API (`POST /api/Data/GenerateRegistrationKeys/{count}`) before running the installer.
- **Does not validate connectivity.** The installer writes the API key and installs the service, but does not verify the agent can actually reach the hub.

### CI/CD Pipeline Usage

The installer supports fully non-interactive (headless) mode via command-line arguments:
```bash
dotnet run -- configure --Security:ApiKey=<key> --Service:Name=FSHAgent-Prod \
    --Service:InstallPath=C:\Agents\FSHAgent --Publish:OutputPath=.\publish\win-x64
```

This means a pipeline can: build → generate key → run installer headlessly → start service.

---

## Part 3 — Testing Options

### Option A: Manual Multi-Terminal (Fastest to Start)

**What:** Two terminals, F5 or `dotnet run`.

**Steps:**
1. Terminal 1 — Start the hub:
   ```
   cd FreeServicesHub\FreeServicesHub
   dotnet run --launch-profile https
   ```
   Hub is live at `https://localhost:7271`

2. Generate a registration key (via Swagger or curl):
   ```
   curl -X POST https://localhost:7271/api/Data/GenerateRegistrationKeys/1 \
        -H "Authorization: Bearer <admin-token>"
   ```

3. Terminal 2 — Start the agent:
   ```
   cd FreeServicesHub.Agent
   dotnet run -- --Agent:HubUrl=https://localhost:7271 --Agent:RegistrationKey=<key>
   ```

**Pros:** Zero setup, works immediately after gap fixes.  
**Cons:** Manual, no orchestration, can't easily reset state between runs.

**Pre-requisites:** Fix gaps 1–8 first. Hub needs InMemory database (default) so no DB setup needed.

---

### Option B: Aspire AppHost (Recommended for Dev Inner Loop)

**What:** A new `FreeServicesHub.AppHost` project that orchestrates the hub, agent, and optionally a database — all wired together with automatic port discovery and environment variable injection.

**Architecture:**
```
FreeServicesHub.AppHost/
├── Program.cs
├── FreeServicesHub.AppHost.csproj
```

**`Program.cs` sketch:**
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Hub server
var hub = builder.AddProject<Projects.FreeServicesHub>("hub")
    .WithHttpsEndpoint(port: 7271, name: "https");

// Agent (as a project, not a service — for dev/test)
builder.AddProject<Projects.FreeServicesHub_Agent>("agent")
    .WithEnvironment("Agent__HubUrl", hub.GetEndpoint("https"))
    .WithEnvironment("Agent__RegistrationKey", "<dev-test-key>")
    .WaitFor(hub);

builder.Build().Run();
```

**What Aspire Gives You:**
- **Port wiring:** The agent automatically gets the hub's URL via environment variable — no hard-coded ports
- **Startup ordering:** `WaitFor(hub)` ensures the hub is healthy before the agent starts
- **Dashboard:** Built-in Aspire dashboard shows logs, traces, metrics for both projects side by side
- **Database option:** Can add `.AddSqlServer()` or use InMemory via environment variable override
- **Reproducible:** `dotnet run` in AppHost starts everything

**What Aspire Does NOT Solve:**
- The registration key flow — in dev mode, you'd either pre-seed a key in the InMemory database or add a dev-mode bypass
- Gap fixes still required (URLs, shapes, hub methods, auth)

**Reference:** The existing `FreeTools.AppHost` uses Aspire SDK 9.2.0 (`Aspire.AppHost.Sdk`) and orchestrates 7 projects with `WithEndpoint`, `WithEnvironment`, etc. Same pattern applies here.

**Effort:** ~1 hour to create the AppHost project after gap fixes are in place.

---

### Option C: Integration Test Project (Best for CI/CD)

**What:** An xUnit/NUnit test project that spins up the hub using `WebApplicationFactory`, then runs the agent registration and heartbeat flow programmatically.

**Architecture:**
```
FreeServicesHub.Tests.Integration/
├── HubFixture.cs              -- WebApplicationFactory<Program> wrapper
├── AgentRegistrationTests.cs  -- Register, get token, verify DB
├── HeartbeatTests.cs          -- Send heartbeat, verify saved
├── SignalRTests.cs            -- Connect, join group, send/receive
```

**Key Pattern:**
```csharp
public class HubFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory;
    public HttpClient Client { get; private set; }
    public string HubUrl { get; private set; }

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => {
                builder.ConfigureServices(services => {
                    // Use InMemory database
                    // Disable auth for testing OR seed a test token
                });
            });
        Client = _factory.CreateClient();
        HubUrl = _factory.Server.BaseAddress.ToString();
    }
}
```

**Pros:** Fast, deterministic, runs in pipeline without deploying anything. Tests the actual server code.  
**Cons:** Doesn't test the real Windows Service lifecycle, sc.exe, or cross-machine networking.

---

### Option D: Full CI/CD Pipeline (Azure DevOps)

**What:** A YAML pipeline that builds everything, deploys the hub, generates keys, deploys agents, and runs smoke tests.

**Pipeline Stages:**

```
Stage 1: Build
  - dotnet restore
  - dotnet build
  - dotnet publish (hub)
  - dotnet publish (agent)
  - dotnet test (unit + integration)

Stage 2: Deploy Hub
  - Deploy hub to Azure App Service / IIS / VM
  - Run DB migrations (if not InMemory)
  - Health check: GET /health

Stage 3: Deploy Agent(s)
  - For each target environment:
    - Generate registration key: POST /api/Data/GenerateRegistrationKeys/1
    - Copy agent files to target machine
    - Run installer headlessly:
      dotnet FreeServicesHub.Agent.Installer.dll configure \
        --Security:ApiKey=<generated-key> \
        --Agent:HubUrl=<hub-url> \
        --Service:Name=FSHAgent-<env>
    - Start service: sc.exe start FSHAgent-<env>

Stage 4: Smoke Test
  - Wait 60s for heartbeat cycle
  - GET /api/Data/GetAgents — verify agent registered
  - GET /api/Data/GetHeartbeats/<agentId> — verify heartbeats flowing
  - Check SignalR dashboard connectivity
```

**Key Variables to Parameterize:**
```yaml
variables:
  hubUrl: 'https://freeserviceshub-$(environment).azurewebsites.net'
  agentInstallPath: 'C:\Agents\FSHAgent-$(environment)'
  serviceName: 'FSHAgent-$(environment)'
```

**Per-Environment API Key Flow:**
1. Pipeline generates a one-time registration key via the hub API
2. Key is passed to the installer as `--Security:ApiKey=<key>`
3. Agent registers on first boot, receives permanent `ApiClientToken`
4. Registration key is consumed (one-time use) — cannot be replayed

---

### Option E: Hybrid (Recommended Strategy)

Run **local Aspire (Option B)** and **pipeline (Option D)** simultaneously:

```
┌─────────────────────────────────┐    ┌───────────────────────────┐
│  LOCAL DEV (your machine)       │    │  CI/CD PIPELINE           │
│                                 │    │                           │
│  Aspire AppHost                 │    │  Build + Unit Tests       │
│  ├── Hub (InMemory DB)          │    │  Integration Tests        │
│  ├── Agent (env-injected)       │    │  Deploy Hub → Azure       │
│  └── Dashboard (auto)           │    │  Generate Key             │
│                                 │    │  Deploy Agent → VM        │
│  You: Fix code, see results     │    │  Smoke Tests              │
│  instantly in Aspire dashboard  │    │  Report ✅ or ❌           │
└─────────────────────────────────┘    └───────────────────────────┘
```

You iterate locally with Aspire while the pipeline validates the same code in an isolated deployed environment.

---

## Part 4 — Prioritized Fix List

Fixes ordered by dependency — each step unlocks the next.

### Priority 1 — Must Fix (Agent Won't Connect Without These)

| Order | Gap | Fix | File | LOC |
|-------|-----|-----|------|-----|
| 1 | Port mismatch (#5) | Change `HubUrl` to `https://localhost:7271` | `FreeServicesHub.Agent/appsettings.json` | 1 |
| 2 | Registration URL (#1) | Change `/api/agents/register` → `/api/Data/RegisterAgent` | `AgentWorkerService.cs:184` | 1 |
| 3 | Heartbeat URL (#2) | Change `/api/agents/heartbeat` → `/api/Data/SaveHeartbeat` | `AgentWorkerService.cs:387` | 1 |
| 4 | Request shape (#6) | Replace `{ AgentName, MachineName }` with `{ Hostname, OperatingSystem, Architecture, AgentVersion, DotNetVersion }` | `AgentWorkerService.cs:177-182` | ~8 |
| 5 | Response shape (#7) | Read `ApiClientToken` instead of `token`/`Token`; also capture `AgentId` | `AgentWorkerService.cs:193-197` | ~5 |

### Priority 2 — Must Fix (SignalR Won't Work Without These)

| Order | Gap | Fix | File | LOC |
|-------|-----|-----|------|-----|
| 6 | Hub methods (#3, #4) | Add `JoinGroup()` and `SendHeartbeat()` to hub — OR — change agent to use `JoinTenantId()` + `SignalRUpdate()` | `signalrHub.cs` or `AgentWorkerService.cs` | ~20 |
| 7 | SignalR auth (#8) | Add custom auth handler or JWT event to validate agent tokens on WebSocket upgrade | Hub `Program.cs` + new handler class | ~40 |

### Priority 3 — Enhancement (Testing Infrastructure)

| Order | Gap | Fix | Effort |
|-------|-----|-----|--------|
| 8 | No Aspire AppHost (#9) | Create `FreeServicesHub.AppHost` project | ~1 hour |
| 9 | No CI/CD pipeline (#10) | Create YAML pipeline definition | ~2 hours |
| 10 | No integration tests | Create test project with `WebApplicationFactory` | ~4 hours |

### Estimated Total Effort

| Category | Effort |
|----------|--------|
| Priority 1 fixes (agent HTTP wiring) | ~30 min |
| Priority 2 fixes (SignalR hub + auth) | ~2 hours |
| Aspire AppHost | ~1 hour |
| CI/CD pipeline | ~2 hours |
| Integration tests | ~4 hours |
| **Total to fully operational + tested** | **~10 hours** |

---

## Part 5 — Quick-Start Checklist

After reading this doc, here's the action plan in order:

- [ ] **Fix gaps 1–7** (agent wiring) — ~30 min
- [ ] **Fix gap 8** (SignalR auth) — ~2 hours
- [ ] **Test manually** (Option A) — verify registration + heartbeat flow works
- [ ] **Create Aspire AppHost** (Option B) — orchestrated local dev
- [ ] **Check in all changes**
- [ ] **Create CI/CD pipeline** (Option D) — automated deployment + smoke tests
- [ ] **Run pipeline** while continuing local Aspire iteration (Option E)
- [ ] **Add integration tests** (Option C) — long-term regression safety

---

## Appendix — Key File Quick Reference

| File | Purpose |
|------|---------|
| `FreeServicesHub.Agent/AgentWorkerService.cs` | All agent logic — registration, SignalR, heartbeat, system collection |
| `FreeServicesHub.Agent/appsettings.json` | Agent config — HubUrl, keys, intervals |
| `FreeServicesHub/Hubs/signalrHub.cs` | SignalR hub — needs agent methods |
| `FreeServicesHub/Controllers/FreeServicesHub.App.API.cs` | Server REST endpoints — the correct routes |
| `FreeServicesHub/FreeServicesHub.App.ApiKeyMiddleware.cs` | Bearer token validation — HTTP only, not SignalR |
| `FreeServicesHub.DataObjects/FreeServicesHub.App.DataObjects.ApiKeys.cs` | Request/response contracts |
| `FreeServicesHub.Agent.Installer/Program.cs` | Installer — build, configure, manage Windows Service |
| `FreeServicesHub/Properties/launchSettings.json` | Hub ports (7271, 5111, 5201) |

---

*End of 307 — Feasibility Analysis, Integration Gaps & Testing Strategy*
