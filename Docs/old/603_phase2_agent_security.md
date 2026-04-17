# 603 — Phase 2: Agent Hardening & Security

> **Document ID:** 603  
> **Category:** Phase Detail / Implementation Guide  
> **Parent:** [601_action_plan.md](601_action_plan.md)  
> **Prerequisite:** [602_phase1_data_contracts.md](602_phase1_data_contracts.md) (HubJob entity must exist)  
> **Purpose:** Audit the existing auth pipeline, wire the Agent's job-fetch loop, validate SignalR tenant scoping, and harden reconnection.  
> **Audience:** Developers and AI Agents in Execution Mode.  
> **Outcome:** 📖 The Agent worker polls for jobs via authenticated HTTP, processes them, and reports results — with verified tenant isolation.

---

## 1. What Already Exists (The "Before")

The security infrastructure is **far more complete than originally assessed**. The codebase exploration revealed:

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │              EXISTING AUTH & AGENT INFRASTRUCTURE                     │
  │                                                                      │
  │  ┌──────────────────────────────────────────────────┐                │
  │  │  ApiKeyMiddleware.cs                   ✅ DONE   │                │
  │  │  ─────────────────────────────────────           │                │
  │  │  • Intercepts /api/agent/* routes                │                │
  │  │  • Intercepts /freeserviceshubHub (SignalR)      │                │
  │  │  • Extracts Bearer token from Authorization hdr  │                │
  │  │  • Extracts access_token from query (SignalR)    │                │
  │  │  • SHA-256 hashes token → looks up ApiClientToken│                │
  │  │  • Sets HttpContext.Items["AgentId"]             │                │
  │  │  • Sets HttpContext.Items["AgentTenantId"]       │                │
  │  │  • Creates ClaimsPrincipal for [Authorize]       │                │
  │  │  • Returns 401 JSON on failure                   │                │
  │  └──────────────────────────────────────────────────┘                │
  │                                                                      │
  │  ┌──────────────────────────────────────────────────┐                │
  │  │  AgentWorkerService.cs                 ✅ DONE   │                │
  │  │  ─────────────────────────────────────           │                │
  │  │  • Windows Service via UseWindowsService()       │                │
  │  │  • Registration workflow (key → token)           │                │
  │  │  • System snapshot: CPU, memory, disk, uptime    │                │
  │  │  • Windows Service metadata collection           │                │
  │  │  • SignalR connection with Bearer token           │                │
  │  │  • Exponential backoff on reconnect              │                │
  │  │  • Heartbeat buffering for offline periods       │                │
  │  └──────────────────────────────────────────────────┘                │
  │                                                                      │
  │  ┌──────────────────────────────────────────────────┐                │
  │  │  AgentMonitorService.cs                ✅ DONE   │                │
  │  │  ─────────────────────────────────────           │                │
  │  │  • Polls agents every 5 sec for staleness        │                │
  │  │  • ConcurrentDictionary status cache             │                │
  │  │  • Broadcasts to "AgentMonitor" SignalR group    │                │
  │  │  • Exponential backoff on poll errors            │                │
  │  └──────────────────────────────────────────────────┘                │
  │                                                                      │
  │  ┌──────────────────────────────────────────────────┐                │
  │  │  Registration Pipeline                 ✅ DONE   │                │
  │  │  ─────────────────────────────────────           │                │
  │  │  • GenerateRegistrationKeys() — SHA-256 hashed   │                │
  │  │  • ValidateRegistrationKey() — expiry + used chk │                │
  │  │  • RegisterAgent() — creates Agent + ApiClientTkn│                │
  │  │  • GenerateApiClientToken() — SHA-256 hashed     │                │
  │  │  • ValidateApiClientToken() — active + not revkd │                │
  │  │  • RevokeApiClientToken() — soft revoke          │                │
  │  │  • DevRegistrationKeySeeder — dev-only seeder    │                │
  │  └──────────────────────────────────────────────────┘                │
  └──────────────────────────────────────────────────────────────────────┘
```

### Key Insight

This phase shifts from **"build from scratch"** to **"audit, wire, and harden"**. The auth infrastructure exists. What's missing is the **job processing loop** in the Agent and **verification** that the existing auth correctly rejects bad actors.

---

## 2. Task Breakdown (Parallelization)

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │              PHASE 2 TASK DEPENDENCY GRAPH                           │
  │                                                                      │
  │  ┌─────────────┐   ┌─────────────┐   ┌──────────────┐              │
  │  │ Task 1      │   │ Task 2      │   │ Task 3       │  ALL THREE   │
  │  │ Auth audit  │   │ SignalR     │   │ Reconnect    │  CAN RUN IN  │
  │  │ (verify)    │   │ tenant chk  │   │ test (verify)│  PARALLEL    │
  │  └──────┬──────┘   └──────┬──────┘   └──────┬───────┘              │
  │         │                 │                  │                       │
  │         └─────────────────┼──────────────────┘                       │
  │                           ▼                                          │
  │                  ┌─────────────────┐                                  │
  │                  │ Task 4          │  DEPENDS ON Phase 1 (HubJob)    │
  │                  │ Agent job       │  + Tasks 1-3 (auth verified)    │
  │                  │ polling loop    │                                  │
  │                  └────────┬────────┘                                  │
  │                           │                                          │
  │                           ▼                                          │
  │                  ┌─────────────────┐                                  │
  │                  │ Task 5          │  DEPENDS ON Task 4              │
  │                  │ Integration     │                                  │
  │                  │ tests           │                                  │
  │                  └─────────────────┘                                  │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## 3. Task Details

### Task 1: Auth Pipeline Audit (Verify Only)

**Files to review:**
- `FreeServicesHub/FreeServicesHub/FreeServicesHub.App.ApiKeyMiddleware.cs`
- `FreeServicesHub/FreeServicesHub/FreeServicesHub.App.Program.cs` (where middleware is wired)
- `FreeServicesHub/FreeServicesHub/Controllers/FreeServicesHub.App.API.cs`

**Parallel with:** Tasks 2 and 3

**Audit Checklist:**

| Check | Expected | Actual (from code review) | Status |
|-------|----------|--------------------------|--------|
| Middleware intercepts `/api/agent/*` | Yes | `path.StartsWith("/api/agent/", OrdinalIgnoreCase)` | ✅ |
| Middleware intercepts SignalR negotiate | Yes | `path.StartsWith("/freeserviceshubHub", OrdinalIgnoreCase)` | ✅ |
| Bearer token extracted from header | Yes | `headerValue.Substring(7).Trim()` | ✅ |
| SignalR token via query string | Yes | `Context.Request.Query["access_token"]` | ✅ |
| Token hashed before DB lookup | SHA-256 | `SHA256.HashData(Encoding.UTF8.GetBytes(Plaintext))` | ✅ |
| Checks `Active` flag | Yes | `t.Active && t.RevokedAt == null` | ✅ |
| Sets AgentId in HttpContext | Yes | `Context.Items["AgentId"] = clientToken.AgentId` | ✅ |
| Sets TenantId in HttpContext | Yes | `Context.Items["AgentTenantId"] = clientToken.TenantId` | ✅ |
| Creates ClaimsPrincipal | Yes | `new ClaimsIdentity(claims, "AgentToken")` | ✅ |
| Returns 401 on invalid token | JSON | `WriteUnauthorized(Context, "invalid_token", ...)` | ✅ |
| Middleware wired in pipeline | Yes | `Output.UseMiddleware<ApiKeyMiddleware>()` in `MyAppModifyStart` | ✅ |

**Result: PASS** — No code changes needed. The auth pipeline is correctly implemented.

**Verification test to write:**
```csharp
[Fact]
public async Task AgentRoute_WithoutToken_Returns401()
{
    var response = await _client.GetAsync("/api/agent/jobs");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("missing_token", body.GetProperty("error").GetString());
}

[Fact]
public async Task AgentRoute_WithInvalidToken_Returns401()
{
    _client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", "totally-invalid-token");
    var response = await _client.GetAsync("/api/agent/jobs");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task AgentRoute_WithRevokedToken_Returns401()
{
    // Revoke the test token, then attempt to use it
    await _da.RevokeApiClientToken(_testTokenId);
    _client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", _testTokenPlaintext);
    var response = await _client.GetAsync("/api/agent/jobs");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

**Pass criteria:**
- All three tests return 401 with correct JSON error bodies
- No token leakage in response headers or bodies

---

### Task 2: SignalR Tenant Scope Validation

**Files to review:**
- `FreeServicesHub/FreeServicesHub.DataAccess/DataAccess.SignalR.cs`
- `FreeServicesHub/FreeServicesHub/Hubs/` (all hub files)
- `FreeServicesHub/FreeServicesHub/FreeServicesHub.App.AgentMonitorService.cs`

**Parallel with:** Tasks 1 and 3

**Concern:** When Agent heartbeats are broadcast, they must only reach users in the same tenant. Cross-tenant leakage is a data breach.

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │              SIGNALR TENANT SCOPING — WHAT TO VERIFY                 │
  │                                                                      │
  │  Tenant A Users ──┐                   ┌── Tenant A Agents            │
  │                    ▼                   ▼                              │
  │              ┌───────────────────────────────┐                       │
  │              │     SignalR Hub               │                       │
  │              │  ┌──────────────────────────┐ │                       │
  │              │  │ Group: "Tenant_{A_Guid}" │ │  ◄─── CORRECT        │
  │              │  │ Group: "AgentMonitor"     │ │  ◄─── ⚠ CHECK THIS  │
  │              │  └──────────────────────────┘ │                       │
  │              └───────────────────────────────┘                       │
  │                    ▲                   ▲                              │
  │  Tenant B Users ──┘                   └── Tenant B Agents            │
  │                                                                      │
  │  RISK: AgentMonitorService broadcasts to "AgentMonitor" group       │
  │  without tenant discrimination. If users from both tenants join     │
  │  the same group name, Tenant B sees Tenant A's agent data.          │
  │                                                                      │
  │  FIX (if needed): Change group to "AgentMonitor_{TenantId}"        │
  └──────────────────────────────────────────────────────────────────────┘
```

**Audit steps:**

1. Check how `SignalRUpdate` in DataAccess broadcasts:
   - Look for `TenantId` filtering in the broadcast method
   - The existing pattern uses `DataObjects.SignalRUpdate.TenantId` — verify the hub method uses this to target the correct group

2. Check `AgentMonitorService.PollAndBroadcast()`:
   - Currently broadcasts to `Clients.Group(MonitorGroup)` where `MonitorGroup = "AgentMonitor"`
   - This is a **global group** — all tenants see all agents
   - **FIX NEEDED:** Partition by tenant

**BEFORE** (actual `AgentMonitorService` code):
```csharp
private const string MonitorGroup = "AgentMonitor";

// In PollAndBroadcast:
List<DataObjects.Agent> agents = await da.GetAgents(null, Guid.Empty, null);

await _hubContext.Clients.Group(MonitorGroup).SignalRUpdate(changeUpdate);
await _hubContext.Clients.Group(MonitorGroup).SignalRUpdate(heartbeatUpdate);
```

**Problems identified:**
1. `GetAgents(null, Guid.Empty, null)` — passes `Guid.Empty` as TenantId. This may return agents from ALL tenants or NONE depending on how the query filters.
2. `Clients.Group("AgentMonitor")` — single global group, no tenant partition.

**AFTER** (tenant-scoped fix):
```csharp
// Remove the global MonitorGroup constant.
// Instead, derive group name per tenant: "AgentMonitor_{TenantId}"

private async Task PollAndBroadcast(CancellationToken StoppingToken)
{
    using IServiceScope scope = _serviceProvider.CreateScope();
    IDataAccess da = scope.ServiceProvider.GetRequiredService<IDataAccess>();
    IConfigurationHelper config = scope.ServiceProvider.GetRequiredService<IConfigurationHelper>();

    int staleThreshold = config.AgentStaleThresholdSeconds;
    DateTime staleEdge = DateTime.UtcNow.AddSeconds(-staleThreshold);

    // Get all tenant IDs that have agents
    // Then process each tenant independently
    List<Guid> tenantIds = await GetTenantIdsWithAgents(da);

    foreach (Guid tenantId in tenantIds) {
        List<DataObjects.Agent> agents = await da.GetAgents(null, tenantId, null);
        List<DataObjects.Agent> changedAgents = new();

        foreach (DataObjects.Agent agent in agents) {
            // ... same staleness detection logic ...
            // (unchanged from current code)
        }

        string tenantGroup = $"AgentMonitor_{tenantId}";

        if (changedAgents.Count > 0) {
            await _hubContext.Clients.Group(tenantGroup)
                .SignalRUpdate(changeUpdate);
        }

        await _hubContext.Clients.Group(tenantGroup)
            .SignalRUpdate(heartbeatUpdate);
    }
}
```

**Also update the client-side group subscription** (in `Helpers.App.cs` or hub connection setup) to join `AgentMonitor_{tenantId}` instead of `AgentMonitor`.

**Pass criteria:**
- Agent data never crosses tenant boundaries
- Integration test: Create agents in two tenants, subscribe to each tenant's group, confirm no cross-leakage
- `GetAgents` never receives `Guid.Empty` as TenantId in production

---

### Task 3: Agent Reconnection Test

**File to test:** `FreeServicesHub.Agent/AgentWorkerService.cs`  
**Parallel with:** Tasks 1 and 2

The Agent already implements exponential backoff reconnection. This task validates it works correctly.

**What exists** (from code exploration):
- SignalR connection with `WithAutomaticReconnect`
- Heartbeat buffering during disconnection
- System snapshot collection continues offline

**Test plan:**
```
1. Start Hub + Agent via AppHost (F5)
2. Verify Agent connects (check dashboard for Online status)
3. Stop the Hub process
4. Wait 30 seconds — Agent should buffer heartbeats locally
5. Restart the Hub
6. Verify Agent reconnects within 60 seconds
7. Verify buffered heartbeats are delivered
```

**Automated test** (in `FreeServicesHub.TestMe`):
```csharp
// TestMe --test=6 -- Reconnection stress test
// 1. Start agent pointing at hub
// 2. Count initial heartbeats (expect 3+)
// 3. Kill hub URL (change to invalid port)
// 4. Wait 15 seconds
// 5. Restore hub URL
// 6. Count resumed heartbeats (expect 3+ more)
// 7. Verify total heartbeat count > initial (buffered beats delivered)
```

**Pass criteria:**
- Agent does not crash when Hub is unavailable
- Agent reconnects within `MaxBackoffMultiplier * PollInterval` seconds
- Buffered heartbeats are delivered after reconnection
- Console logs show backoff progression (5s, 10s, 15s, ...)

---

### Task 4: Agent Job Polling Loop

**File:** `FreeServicesHub.Agent/AgentWorkerService.cs` (MODIFY)  
**Depends on:** Phase 1 (HubJob DTO) + Tasks 1-3 (auth verified)

This is the primary new code in Phase 2 — wiring the Agent to fetch and execute jobs from the Hub.

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │              AGENT JOB PROCESSING LOOP                               │
  │                                                                      │
  │  ┌─────────────────────────────────────────────────────┐             │
  │  │               AgentWorkerService                     │            │
  │  │                                                     │             │
  │  │   ┌──────────────────────────────────────────┐      │             │
  │  │   │  Existing Heartbeat Loop (every 30s)     │      │             │
  │  │   │  1. Collect SystemSnapshot               │      │             │
  │  │   │  2. POST heartbeat to Hub                │      │             │
  │  │   │  3. Update status                        │      │             │
  │  │   └──────────────────────────────────────────┘      │             │
  │  │                                                     │             │
  │  │   ┌──────────────────────────────────────────┐      │             │
  │  │   │  NEW: Job Poll Loop (every 10s)    ◄──── NEW   │             │
  │  │   │  1. GET /api/agent/jobs                  │      │             │
  │  │   │  2. For each Queued job:                 │      │             │
  │  │   │     a. Set status → Running              │      │             │
  │  │   │     b. Execute locally                   │      │             │
  │  │   │     c. Set status → Completed/Failed     │      │             │
  │  │   │     d. POST /api/agent/jobs/complete     │      │             │
  │  │   │  3. Log results                          │      │             │
  │  │   └──────────────────────────────────────────┘      │             │
  │  │                                                     │             │
  │  └─────────────────────────────────────────────────────┘             │
  │                                                                      │
  │  HTTP AUTH:                                                          │
  │  ┌───────────────────────────────────────────────────┐               │
  │  │  GET /api/agent/jobs                              │               │
  │  │  Authorization: Bearer <ApiClientToken>           │               │
  │  │                                                   │               │
  │  │  ──► ApiKeyMiddleware validates token             │               │
  │  │  ──► Sets HttpContext.Items["AgentId"]            │               │
  │  │  ──► Controller returns only THIS agent's jobs    │               │
  │  └───────────────────────────────────────────────────┘               │
  └──────────────────────────────────────────────────────────────────────┘
```

**BEFORE** (AgentWorkerService — simplified structure of what exists):
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // ... registration logic ...
    // ... SignalR connection setup ...

    while (!stoppingToken.IsCancellationRequested)
    {
        // Collect system snapshot
        SystemSnapshot snapshot = CollectSnapshot();

        // Send heartbeat
        await SendHeartbeat(snapshot);

        // Wait for next interval
        await Task.Delay(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds),
            stoppingToken);
    }
}
```

**AFTER** (add job polling as a parallel concern):
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // ... existing registration logic (unchanged) ...
    // ... existing SignalR connection setup (unchanged) ...

    // Run heartbeat loop and job poll loop as parallel tasks
    Task heartbeatTask = HeartbeatLoop(stoppingToken);
    Task jobPollTask = JobPollLoop(stoppingToken);

    await Task.WhenAll(heartbeatTask, jobPollTask);
}

// Existing heartbeat logic extracted into its own method
private async Task HeartbeatLoop(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        SystemSnapshot snapshot = CollectSnapshot();
        await SendHeartbeat(snapshot);
        await Task.Delay(
            TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds),
            stoppingToken);
    }
}

// NEW: Job polling loop
private async Task JobPollLoop(CancellationToken stoppingToken)
{
    int pollIntervalSeconds = 10; // Could be configurable
    int consecutiveErrors = 0;

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            List<DataObjects.HubJob> jobs = await FetchMyJobs();

            foreach (DataObjects.HubJob job in jobs)
            {
                if (stoppingToken.IsCancellationRequested) break;
                await ProcessJob(job);
            }

            consecutiveErrors = 0;
        }
        catch (HttpRequestException ex)
        {
            consecutiveErrors++;
            _logger.LogWarning(ex,
                "Job poll failed (attempt {Count})", consecutiveErrors);
        }

        int backoff = Math.Min(consecutiveErrors + 1, 6);
        await Task.Delay(
            TimeSpan.FromSeconds(pollIntervalSeconds * backoff),
            stoppingToken);
    }
}

private async Task<List<DataObjects.HubJob>> FetchMyJobs()
{
    using HttpClient client = CreateAuthenticatedClient();
    HttpResponseMessage response = await client.GetAsync(
        $"{_options.HubUrl}/api/agent/jobs");
    response.EnsureSuccessStatusCode();
    return await response.Content
        .ReadFromJsonAsync<List<DataObjects.HubJob>>()
        ?? new();
}

private async Task ProcessJob(DataObjects.HubJob job)
{
    _logger.LogInformation(
        "Processing job {JobId} type={Type}", job.HubJobId, job.JobType);

    job.Status = DataObjects.HubJobStatuses.Running;
    job.StartedAt = DateTime.UtcNow;
    await ReportJobStatus(job);

    try
    {
        string result = await ExecuteJobLocally(job);
        job.Status = DataObjects.HubJobStatuses.Completed;
        job.ResultJson = result;
        job.CompletedAt = DateTime.UtcNow;
    }
    catch (Exception ex)
    {
        job.RetryCount++;
        if (job.RetryCount >= job.MaxRetries) {
            job.Status = DataObjects.HubJobStatuses.Failed;
        } else {
            job.Status = DataObjects.HubJobStatuses.Queued; // Re-queue
        }
        job.ErrorMessage = ex.Message;
    }

    await ReportJobStatus(job);
}

private async Task<string> ExecuteJobLocally(DataObjects.HubJob job)
{
    // Dispatch by JobType — extensible switch
    return job.JobType switch
    {
        "CollectLogs" => await CollectLogs(job.JobPayloadJson),
        "RestartService" => await RestartService(job.JobPayloadJson),
        "RunScript" => await RunScript(job.JobPayloadJson),
        _ => throw new NotSupportedException(
            $"Unknown job type: {job.JobType}")
    };
}

private async Task ReportJobStatus(DataObjects.HubJob job)
{
    using HttpClient client = CreateAuthenticatedClient();
    await client.PostAsJsonAsync(
        $"{_options.HubUrl}/api/agent/jobs/complete", job);
}

private HttpClient CreateAuthenticatedClient()
{
    HttpClient client = new();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", _options.ApiClientToken);
    return client;
}
```

**Pass criteria:**
- Agent fetches jobs from Hub using Bearer token auth
- Jobs transition: Queued → Running → Completed/Failed
- Failed jobs retry up to `MaxRetries` then permanently fail
- Job poll loop runs independently from heartbeat loop
- Errors in job processing don't crash the heartbeat loop

---

### Task 5: Integration Tests

**File:** `FreeServicesHub.Tests.Integration/` (NEW test classes)  
**Depends on:** Task 4

```csharp
// Test the full Agent → Hub → Agent round-trip
[Fact]
public async Task Agent_FetchesAndCompletesJob()
{
    // 1. Create a registration key
    var keys = await _da.GenerateRegistrationKeys(1, _testTenantId);
    string regKey = keys[0].NewKeyPlaintext;

    // 2. Register an agent
    var regResponse = await _da.RegisterAgent(new DataObjects.AgentRegistrationRequest {
        RegistrationKey = regKey,
        Hostname = "test-agent",
        OperatingSystem = "Windows",
        Architecture = "x64",
        AgentVersion = "1.0.0",
        DotNetVersion = "10.0.0",
    }, _testTenantId);
    Assert.True(regResponse.ActionResponse.Result);
    string agentToken = regResponse.ApiClientToken;
    Guid agentId = regResponse.AgentId;

    // 3. Queue a job for this agent
    var savedJobs = await _da.SaveJobs(new List<DataObjects.HubJob> {
        new DataObjects.HubJob {
            TenantId = _testTenantId,
            AssignedAgentId = agentId,
            JobType = "CollectLogs",
            Status = DataObjects.HubJobStatuses.Queued,
            Priority = 0,
            MaxRetries = 1,
        }
    });
    Assert.True(savedJobs[0].ActionResponse.Result);

    // 4. Fetch jobs as the agent (using Bearer token)
    _client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", agentToken);
    var jobs = await _client.GetFromJsonAsync<List<DataObjects.HubJob>>(
        "/api/agent/jobs");
    Assert.Single(jobs);
    Assert.Equal("CollectLogs", jobs[0].JobType);

    // 5. Complete the job
    jobs[0].Status = DataObjects.HubJobStatuses.Completed;
    jobs[0].ResultJson = "{\"lines\": 42}";
    jobs[0].CompletedAt = DateTime.UtcNow;
    var completeResponse = await _client.PostAsJsonAsync(
        "/api/agent/jobs/complete", jobs[0]);
    completeResponse.EnsureSuccessStatusCode();

    // 6. Verify job is no longer in the queue
    var remainingJobs = await _client.GetFromJsonAsync<List<DataObjects.HubJob>>(
        "/api/agent/jobs");
    Assert.Empty(remainingJobs);
}

[Fact]
public async Task Agent_CannotSeeOtherTenantsJobs()
{
    // Register agent in Tenant A, create job in Tenant B
    // Verify agent cannot fetch Tenant B's job
    // ... (tenant isolation test)
}
```

---

## 4. Agent ↔ Hub Communication Diagram

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │           COMPLETE AGENT ↔ HUB COMMUNICATION MAP                     │
  │                                                                      │
  │  AGENT WORKER SERVICE                    HUB WEB SERVER              │
  │  ═══════════════════                    ═══════════════              │
  │                                                                      │
  │  ┌─── REGISTRATION (one-time) ───────────────────────────────────┐   │
  │  │                                                               │   │
  │  │  Agent                                         Hub            │   │
  │  │  ─────                                         ───            │   │
  │  │  POST /api/agent/register ──────────────────►  Validate key   │   │
  │  │  { RegistrationKey, Hostname, OS, ... }        Create Agent   │   │
  │  │                                                Generate Token │   │
  │  │  ◄──────────────────────────────────────────   { AgentId,     │   │
  │  │                                                  ApiToken }   │   │
  │  │  Store ApiToken in appsettings.json                           │   │
  │  └───────────────────────────────────────────────────────────────┘   │
  │                                                                      │
  │  ┌─── HEARTBEAT LOOP (every 30s) ───────────────────────────────┐   │
  │  │                                                               │   │
  │  │  Agent                                         Hub            │   │
  │  │  ─────                                         ───            │   │
  │  │  Collect CPU/Memory/Disk snapshot                             │   │
  │  │  POST /api/agent/heartbeat ─────────────────►  SaveHeartbeat  │   │
  │  │  Authorization: Bearer <token>                 Update Status  │   │
  │  │  { CpuPercent, MemoryPercent, ... }            SignalR push   │   │
  │  └───────────────────────────────────────────────────────────────┘   │
  │                                                                      │
  │  ┌─── JOB POLL LOOP (every 10s) — NEW ─────────────────────────┐   │
  │  │                                                               │   │
  │  │  Agent                                         Hub            │   │
  │  │  ─────                                         ───            │   │
  │  │  GET /api/agent/jobs ───────────────────────►  GetJobsForAgent│   │
  │  │  Authorization: Bearer <token>                 Filter by      │   │
  │  │                                                AgentId+Status │   │
  │  │  ◄──────────────────────────────────────────   [ HubJob, ... ]│   │
  │  │                                                               │   │
  │  │  For each job:                                                │   │
  │  │    Set Running → Execute locally                              │   │
  │  │    POST /api/agent/jobs/complete ───────────►  SaveJob        │   │
  │  │    { HubJobId, Status, ResultJson }            SignalR push   │   │
  │  └───────────────────────────────────────────────────────────────┘   │
  │                                                                      │
  │  ┌─── SIGNALR (persistent, bi-directional) ────────────────────┐   │
  │  │                                                               │   │
  │  │  Agent ◄═══════ WSS ═══════► Hub                              │   │
  │  │                                                               │   │
  │  │  Hub → Agent:                                                 │   │
  │  │    AgentSettingsUpdated (new config pushed)                   │   │
  │  │    AgentShutdown (graceful stop command)                      │   │
  │  │                                                               │   │
  │  │  Agent → Hub:                                                 │   │
  │  │    AgentConnected (on connect)                                │   │
  │  │    AgentDisconnected (on disconnect)                          │   │
  │  └───────────────────────────────────────────────────────────────┘   │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## 5. Files Modified Per Project

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │            PHASE 2 — FILES TOUCHED PER PROJECT                       │
  │                                                                      │
  │  FreeServicesHub.Agent (2 files)                                     │
  │  ├── AgentWorkerService.cs .................. MODIFY (job loop)      │
  │  └── appsettings.json ...................... MODIFY (job poll cfg)   │
  │                                                                      │
  │  Hub Server (1 file — audit only)                                    │
  │  └── FreeServicesHub.App.AgentMonitorService.cs  MODIFY (tenant fix)│
  │                                                                      │
  │  Client (1 file — audit only)                                        │
  │  └── Helpers.App.cs ........................ MODIFY (group name)     │
  │                                                                      │
  │  Tests.Integration (2 files)                                         │
  │  ├── Auth_IntegrationTests.cs .............. NEW (auth verification) │
  │  └── JobPoll_IntegrationTests.cs ........... NEW (round-trip test)   │
  │                                                                      │
  │  TOTAL: 2 new files, 4 modified files                                │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## 6. How to Test

| Test | Command | Expected |
|------|---------|----------|
| Auth rejection (no token) | `curl -X GET https://localhost:7271/api/agent/jobs` | 401 `missing_token` |
| Auth rejection (bad token) | `curl -H "Authorization: Bearer fake" https://localhost:7271/api/agent/jobs` | 401 `invalid_token` |
| Auth acceptance (valid token) | `curl -H "Authorization: Bearer <real>" https://localhost:7271/api/agent/jobs` | 200 + JSON |
| Agent reconnection | Start Agent → stop Hub → wait 30s → restart Hub → check agent status | Agent reconnects |
| Job round-trip | `dotnet test --filter "Agent_FetchesAndCompletesJob"` | Green |
| Tenant isolation | `dotnet test --filter "Agent_CannotSeeOtherTenantsJobs"` | Green |
| Full AppHost | `dotnet run --project FreeServicesHub.AppHost` | Hub + Agent both start, Agent heartbeats visible |

---

## 7. Security Considerations

| Risk | Mitigation | Status |
|------|------------|--------|
| Token stored in plaintext in appsettings.json | Acceptable for Windows Service (file system ACL protected); document in deployment guide | ⚠ Known |
| Token replay attack | Tokens are SHA-256 hashed; revocation is immediate via `RevokeApiClientToken` | ✅ Mitigated |
| Cross-tenant data access | Agent can only access its own TenantId (set by middleware from DB lookup) | ✅ Mitigated |
| Job execution as SYSTEM | Agent runs as Windows Service (LocalSystem) — job execution inherits these privileges | ⚠ Document |
| DDoS via rapid polling | Exponential backoff on errors; configurable poll interval | ✅ Mitigated |

---

## 🤖 AI Analysis

> **Agent:** `4/15/2026/opus/4.6`

**The biggest finding in Phase 2** is that the original 106 assessment of "undefined API auth boundaries" was almost entirely wrong. The `ApiKeyMiddleware` is a textbook implementation: SHA-256 token hashing, Bearer scheme extraction, SignalR query-string support, ClaimsPrincipal construction, and proper 401 responses. The registration pipeline (`GenerateRegistrationKeys` → `RegisterAgent` → `GenerateApiClientToken`) is equally complete with one-time-use keys, expiry windows, and token revocation.

**The one real vulnerability** is `AgentMonitorService` broadcasting to a global `"AgentMonitor"` SignalR group without tenant partitioning. This is the only code change with security implications. Everything else in this phase is incremental enhancement (job polling loop) and verification (integration tests).

**Phase 2 is 70% audit, 30% new code.** The job polling loop is the only substantial new feature, and it follows the same HTTP + Bearer pattern already proven by the heartbeat loop. The risk is low.
