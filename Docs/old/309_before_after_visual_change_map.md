# 309 — Before & After: Visual Change Map (ASCII Art)

> **Document ID:** 309  
> **Category:** Reference — Visual Change Map  
> **Purpose:** ASCII art showing the BEFORE and AFTER state of every component touched by the 308 implementation plan.  
> **Companion To:** 307 (Gaps), 308 (Plan)  
> **Audience:** Anyone reviewing the PR or wanting a quick visual diff.

---

## 1. Agent → Hub HTTP Communication

### BEFORE (Broken)

```
  ┌──────────────────────────────┐              ┌──────────────────────────────┐
  │  FreeServicesHub.Agent       │              │  FreeServicesHub Hub Server   │
  │                              │              │                              │
  │  HubUrl: localhost:5001  ────┼──── ✘ ──────►│  Listening on :7271 / :5111  │
  │                              │   REFUSED    │                              │
  │  POST /api/agents/register ──┼──── ✘ ──────►│  Route: /api/Data/Register   │
  │                              │   404        │         Agent                │
  │  POST /api/agents/heartbeat ─┼──── ✘ ──────►│  Route: /api/Data/Save       │
  │                              │   404        │         Heartbeat            │
  └──────────────────────────────┘              └──────────────────────────────┘
                 ▲
                 │ Every request fails:
                 │ wrong port, wrong URL
                 └──── NOTHING WORKS
```

### AFTER (Fixed)

```
  ┌──────────────────────────────┐              ┌──────────────────────────────┐
  │  FreeServicesHub.Agent       │              │  FreeServicesHub Hub Server   │
  │                              │              │                              │
  │  HubUrl: localhost:7271  ────┼──── ✔ ──────►│  Listening on :7271 / :5111  │
  │                              │   CONNECTED  │                              │
  │  POST /api/Data/             │              │                              │
  │       RegisterAgent      ────┼──── ✔ ──────►│  Route: /api/Data/Register   │
  │                              │   200 OK     │         Agent                │
  │  POST /api/Data/             │              │                              │
  │       SaveHeartbeat      ────┼──── ✔ ──────►│  Route: /api/Data/Save       │
  │                              │   200 OK     │         Heartbeat            │
  └──────────────────────────────┘              └──────────────────────────────┘
```

**Files Changed:** `AgentWorkerService.cs` (lines 20, 184, 387), `appsettings.json` (HubUrl)

---

## 2. Registration Request/Response Shapes

### BEFORE (Mismatched)

```
  AGENT SENDS:                          SERVER EXPECTS:
  ┌─────────────────────┐               ┌──────────────────────────┐
  │ {                   │               │ AgentRegistrationRequest │
  │   RegistrationKey ──┼──── ✔ match ──┼─► RegistrationKey       │
  │   AgentName     ────┼──── ✘ ───────►│   (no such field)       │
  │   MachineName   ────┼──── ✘ ───────►│   (no such field)       │
  │ }                   │               │                          │
  │                     │    MISSING ──►│   Hostname           ❌  │
  │                     │    MISSING ──►│   OperatingSystem     ❌  │
  │                     │    MISSING ──►│   Architecture        ❌  │
  │                     │    MISSING ──►│   AgentVersion        ❌  │
  │                     │    MISSING ──►│   DotNetVersion       ❌  │
  └─────────────────────┘               └──────────────────────────┘

  SERVER RETURNS:                       AGENT READS:
  ┌──────────────────────────┐          ┌────────────────────────┐
  │ AgentRegistrationResponse│          │ result["token"]    ✘   │
  │   AgentId            ────┼── IGNORED│ result["Token"]    ✘   │
  │   ApiClientToken     ────┼── MISSED │ (never matches)        │
  │   HubUrl             ────┼── IGNORED│                        │
  └──────────────────────────┘          └────────────────────────┘
           Result: Agent thinks registration FAILED even when it SUCCEEDED
```

### AFTER (Aligned)

```
  AGENT SENDS:                          SERVER EXPECTS:
  ┌─────────────────────────┐           ┌──────────────────────────┐
  │ {                       │           │ AgentRegistrationRequest │
  │   RegistrationKey   ────┼── ✔ ─────┼─► RegistrationKey       │
  │   Hostname          ────┼── ✔ ─────┼─► Hostname              │
  │   OperatingSystem   ────┼── ✔ ─────┼─► OperatingSystem       │
  │   Architecture      ────┼── ✔ ─────┼─► Architecture          │
  │   AgentVersion      ────┼── ✔ ─────┼─► AgentVersion          │
  │   DotNetVersion     ────┼── ✔ ─────┼─► DotNetVersion         │
  │ }                       │           └──────────────────────────┘
  └─────────────────────────┘

  SERVER RETURNS:                       AGENT READS:
  ┌──────────────────────────┐          ┌──────────────────────────┐
  │ AgentRegistrationResponse│          │                          │
  │   AgentId            ────┼── ✔ ────►│ result["agentId"]    ✔  │
  │   ApiClientToken     ────┼── ✔ ────►│ result["apiClient    ✔  │
  │   HubUrl             ────┼── ✔ ────►│        Token"]          │
  └──────────────────────────┘          └──────────────────────────┘
           Result: Token extracted, persisted, agent continues to SignalR
```

**Files Changed:** `AgentWorkerService.cs` (lines 177-182, 193-200)

---

## 3. SignalR Hub Methods

### BEFORE (Missing Methods)

```
  ┌─────────────────────────────┐       ┌───────────────────────────────────┐
  │  Agent calls:               │       │  Hub has:                         │
  │                             │       │                                   │
  │  InvokeAsync("JoinGroup",  ─┼─ ✘ ──┤  JoinTenantId(string)            │
  │              "Agents")      │  DNE  │  SignalRUpdate(SignalRUpdate)     │
  │                             │       │                                   │
  │  InvokeAsync("SendHeartbeat"┼─ ✘ ──┤  (nothing else)                  │
  │              snapshot)      │  DNE  │                                   │
  └─────────────────────────────┘       └───────────────────────────────────┘
                                              │
         HubException: "Failed to invoke      │
         'JoinGroup' because it does           │
         not exist."                           │
```

### AFTER (Methods Added)

```
  ┌─────────────────────────────┐       ┌───────────────────────────────────┐
  │  Agent calls:               │       │  Hub has:                         │
  │                             │       │                                   │
  │  InvokeAsync("JoinGroup",  ─┼─ ✔ ──┤  JoinTenantId(string)            │
  │              "Agents")      │  OK   │  SignalRUpdate(SignalRUpdate)     │
  │                             │       │  JoinGroup(string)           ★NEW│
  │  InvokeAsync("SendHeartbeat"┼─ ✔ ──┤  SendHeartbeat(AgentHeartbeat)★NEW│
  │              heartbeat)     │  OK   │                                   │
  └─────────────────────────────┘       │  + IServiceProvider DI       ★NEW│
                                        └───────────────────────────────────┘
                                              │
         Hub calls DataAccess.SaveHeartbeat() │
         which persists + evaluates thresholds│
         + broadcasts SignalRUpdate           │
```

**Files Changed:** `signalrHub.cs` (add constructor, JoinGroup, SendHeartbeat)

---

## 4. SignalR Authentication

### BEFORE (Gap — Agent Can't Connect)

```
  Agent                           Middleware                    Hub
  ──────                         ──────────                   ─────
   │                                │                           │
   │ WebSocket negotiate            │                           │
   │ ?access_token=abc123 ─────────►│                           │
   │                                │                           │
   │                          path = /freeserviceshubHub        │
   │                          /api/agent/* ? NO                 │
   │                          ──► SKIP (pass through)           │
   │                                │                           │
   │                                │───────────────────────────►│
   │                                │                    [Authorize]
   │                                │                    User = null
   │                                │                    ──► 401 ✘
   │◄──────────────────── 401 Unauthorized ─────────────────────│
```

### AFTER (Bridge — Middleware Covers SignalR)

```
  Agent                           Middleware                    Hub
  ──────                         ──────────                   ─────
   │                                │                           │
   │ WebSocket negotiate            │                           │
   │ ?access_token=abc123 ─────────►│                           │
   │                                │                           │
   │                          path = /freeserviceshubHub        │
   │                          SignalR path? YES ★               │
   │                          Extract access_token from query   │
   │                          SHA-256 hash → lookup DB          │
   │                          Found! AgentId=X, TenantId=Y      │
   │                          ──► Create ClaimsPrincipal        │
   │                          ──► Set Context.User              │
   │                          ──► Set Context.Items             │
   │                                │                           │
   │                                │───────────────────────────►│
   │                                │                    [Authorize]
   │                                │                    User = ✔
   │                                │                    ──► PASS ✔
   │◄──────────────────── 101 Switching Protocols ──────────────│
   │                                                            │
   │◄═══════════════ WebSocket Connected ═══════════════════════│
```

**Files Changed:** `FreeServicesHub.App.ApiKeyMiddleware.cs` (expand path check, handle query token, create ClaimsPrincipal)

---

## 5. Heartbeat Data Flow

### BEFORE (Shape Mismatch)

```
  Agent SystemSnapshot               Server AgentHeartbeat
  ─────────────────────              ─────────────────────
  CpuUsagePercent ─── ✘ ──────────── CpuPercent
  MemoryUsagePercent ─ ✘ ──────────── MemoryPercent
  UsedMemoryMb ─────── ✘ (units!) ── MemoryUsedGB
  TotalMemoryMb ────── ✘ (units!) ── MemoryTotalGB
  Drives[] ─────────── ✘ (type!) ─── DiskMetricsJson (string)
  (none) ──────────── ✘ missing ──── AgentName
  (none) ──────────── ✘ missing ──── AgentId

  Result: Deserialization fails or produces zeroes everywhere
```

### AFTER (Converted)

```
  Agent SystemSnapshot     ConvertToHeartbeat()      Server AgentHeartbeat
  ─────────────────────   ─────────────────────     ─────────────────────
  CpuUsagePercent ────────► CpuPercent ─────── ✔ ── CpuPercent
  MemoryUsagePercent ─────► MemoryPercent ──── ✔ ── MemoryPercent
  UsedMemoryMb ──── /1024 ► MemoryUsedGB ──── ✔ ── MemoryUsedGB
  TotalMemoryMb ─── /1024 ► MemoryTotalGB ─── ✔ ── MemoryTotalGB
  Drives[] ── Serialize() ► DiskMetricsJson ── ✔ ── DiskMetricsJson
  _options.AgentName ─────► AgentName ──────── ✔ ── AgentName
  (from auth context) ────► AgentId ─────────── ✔ ── AgentId

  Result: Heartbeat persisted with correct values, thresholds evaluated
```

**Files Changed:** `AgentWorkerService.cs` (add ConvertToHeartbeat method, update all send calls)

---

## 6. End-to-End Architecture

### BEFORE (Disconnected)

```
  ┌──────────┐     ┌───────────┐     ┌──────────────┐
  │ Pipeline │     │ Installer │     │   Agent      │
  │ (none)   │     │ (works    │     │ (broken      │
  │          │     │  locally) │     │  wiring)     │
  └──────────┘     └───────────┘     └──────────────┘
       │                │                   │
       │                │                   │    ┌──────────────┐
       ✘ no pipeline    ✔ builds/installs   ✘───►│  Hub Server  │
       ✘ no key gen     ✔ writes API key    │    │ (running but │
       ✘ no deploy      ✘ wrong port in cfg │    │  unreachable │
                                            │    │  from agent) │
                                            │    └──────────────┘
                                            │
                              ┌─────────────┘
                              │ Wrong URLs
                              │ Wrong shapes
                              │ Missing hub methods
                              │ No SignalR auth
                              └──── ALL FAIL
```

### AFTER (Connected)

```
  ┌──────────────────┐
  │  Azure DevOps    │
  │  Pipeline (YAML) │
  │                  │
  │ 1. Build         │
  │ 2. Test          │
  │ 3. Deploy Hub ───┼──────────────────────────────────────────────┐
  │ 4. Gen Key ──────┼──── POST /api/Data/GenerateRegistrationKeys │
  │ 5. Deploy Agent ─┼──── Installer --Security:ApiKey=<key>       │
  │ 6. Smoke Test    │                                              │
  └──────────────────┘                                              │
                                                                    ▼
  ┌──────────┐  build   ┌───────────┐  configure  ┌──────────────────────┐
  │ Installer├─────────►│ Published ├────────────►│  Agent (Windows Svc) │
  │          │  publish  │  Files    │  sc.exe     │                      │
  └──────────┘          └───────────┘  create     │ 1. Register ─────────┤
                                                   │    POST RegisterAgent│
                                                   │    ──► get token     │
                                                   │                      │
                                                   │ 2. Connect SignalR ──┤
                                                   │    WebSocket + auth  │
                                                   │    ──► JoinGroup     │
                                                   │                      │
                                                   │ 3. Heartbeat Loop ──┤
                                                   │    ──► SendHeartbeat │
                                                   │    every 30s         │
                                                   └──────────┬───────────┘
                                                              │
                                              ┌───────────────▼───────────────┐
                                              │  FreeServicesHub Server       │
                                              │                               │
                                              │  /api/Data/RegisterAgent  ✔   │
                                              │  /api/Data/SaveHeartbeat  ✔   │
                                              │  /freeserviceshubHub      ✔   │
                                              │    ├── JoinGroup          ✔   │
                                              │    ├── SendHeartbeat      ✔   │
                                              │    └── Auth (middleware)  ✔   │
                                              │                               │
                                              │  AgentMonitorService          │
                                              │    poll → detect → broadcast  │
                                              │                               │
                                              │  Blazor Dashboard             │
                                              │    real-time agent status     │
                                              └───────────────────────────────┘
```

---

## 7. Local Dev (Aspire) — NEW

### BEFORE (Does Not Exist)

```
  ┌─────────────────────────────────────────────────┐
  │                                                 │
  │              (nothing here)                     │
  │                                                 │
  │   No Aspire AppHost for FreeServicesHub         │
  │   Manual: open 2 terminals, start separately    │
  │   Hope ports match, hope order is right         │
  │                                                 │
  └─────────────────────────────────────────────────┘
```

### AFTER (Orchestrated)

```
  ┌──────────────────────────────────────────────────────────────────┐
  │  FreeServicesHub.AppHost (Aspire)                                │
  │                                                                  │
  │  ┌──────────────────────┐        ┌──────────────────────┐       │
  │  │  hub                 │        │  agent               │       │
  │  │  FreeServicesHub     │        │  FreeServicesHub.Agent│       │
  │  │  :7271 (https)       │◄──────►│  HubUrl=:7271        │       │
  │  │  :5111 (http)        │ auto   │  RegKey=<seeded>     │       │
  │  │  InMemory DB         │ wired  │  WaitFor(hub)        │       │
  │  └──────────────────────┘        └──────────────────────┘       │
  │                                                                  │
  │  ┌──────────────────────────────────────────────────────┐       │
  │  │  Aspire Dashboard  https://localhost:15888            │       │
  │  │  ┌─────────┬─────────┬─────────┬─────────────────┐  │       │
  │  │  │ Project │ Status  │  Logs   │ Traces/Metrics  │  │       │
  │  │  ├─────────┼─────────┼─────────┼─────────────────┤  │       │
  │  │  │ hub     │ ●green  │ [view]  │ [view]          │  │       │
  │  │  │ agent   │ ●green  │ [view]  │ [view]          │  │       │
  │  │  └─────────┴─────────┴─────────┴─────────────────┘  │       │
  │  └──────────────────────────────────────────────────────┘       │
  │                                                                  │
  │  $ dotnet run  ←── ONE COMMAND, EVERYTHING STARTS               │
  └──────────────────────────────────────────────────────────────────┘
```

**Files Created:** `FreeServicesHub.AppHost/Program.cs`, `FreeServicesHub.AppHost.csproj`

---

## 8. File Change Summary

```
  FILES MODIFIED                          CHANGES
  ──────────────────────────────────────  ────────────────────────────────────
  AgentWorkerService.cs                   Port default 5001 → 7271
                                          URL /api/agents/register → RegisterAgent
                                          URL /api/agents/heartbeat → SaveHeartbeat
                                          Registration payload shape fix
                                          Registration response parsing fix
                                          Add ConvertToHeartbeat() method
                                          Update all heartbeat send calls

  appsettings.json (Agent)                HubUrl: 5001 → 7271

  signalrHub.cs                           Add IServiceProvider constructor
                                          Add JoinGroup(string) method
                                          Add SendHeartbeat(AgentHeartbeat) method

  ApiKeyMiddleware.cs                     Expand path check for SignalR
                                          Handle ?access_token query param
                                          Create ClaimsPrincipal for agents

  FILES CREATED                           PURPOSE
  ──────────────────────────────────────  ────────────────────────────────────
  FreeServicesHub.AppHost.csproj          Aspire orchestration project
  FreeServicesHub.AppHost/Program.cs      Hub + Agent wiring with WaitFor
  .azure-pipelines/.../ci.yml             Build → Deploy → Test pipeline
  Tests.Integration/ (future)             WebApplicationFactory-based tests
```

---

## 9. The Vision — What This Becomes

```
  ┌─────────────────────────────────────────────────────────────────┐
  │                                                                 │
  │    NOT Azure DevOps Agents.  NOT GitHub Runners.                │
  │    YOUR OWN THING.                                              │
  │                                                                 │
  │    ┌─────────────────────────────────────────────────────┐      │
  │    │  FreeServicesHub (Blazor Dashboard)                 │      │
  │    │                                                     │      │
  │    │  ┌─────────┬──────────┬────────┬──────────┐        │      │
  │    │  │ Agent   │ Status   │ CPU    │ Memory   │        │      │
  │    │  ├─────────┼──────────┼────────┼──────────┤        │      │
  │    │  │ DEV-01  │ ● Online │ 23%    │ 4.2 GB   │        │      │
  │    │  │ CMS-01  │ ● Online │ 45%    │ 8.1 GB   │        │      │
  │    │  │ PROD-01 │ ⚠ Warn   │ 78%    │ 14.2 GB  │        │      │
  │    │  │ PROD-02 │ ● Online │ 12%    │ 6.3 GB   │        │      │
  │    │  └─────────┴──────────┴────────┴──────────┘        │      │
  │    │                                                     │      │
  │    │  Real-time via SignalR • API key auth               │      │
  │    │  Threshold alerts • Stale detection                 │      │
  │    └─────────────────────────────────────────────────────┘      │
  │                          ▲                                      │
  │              SignalR +   │  + HTTP fallback                     │
  │              heartbeats  │                                      │
  │    ┌─────────┬──────────┬┴─────────┬──────────┐                │
  │    │ DEV-01  │ CMS-01   │ PROD-01  │ PROD-02  │                │
  │    │ Agent   │ Agent    │ Agent    │ Agent    │                │
  │    │ Win Svc │ Win Svc  │ Win Svc  │ Win Svc  │                │
  │    └─────────┴──────────┴──────────┴──────────┘                │
  │                                                                 │
  │    Deployed via Azure DevOps pipeline                           │
  │    using the Installer in headless mode                         │
  │    with per-environment registration keys                       │
  │                                                                 │
  └─────────────────────────────────────────────────────────────────┘
```

---

*End of 309 — Before & After Visual Change Map*
