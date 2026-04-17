# 602 — Phase 1: Data Contracts (Job Queue)

> **Document ID:** 602  
> **Category:** Phase Detail / Implementation Guide  
> **Parent:** [601_action_plan.md](601_action_plan.md)  
> **Purpose:** Define the missing Job Queue data contract, map it through every solution layer, and generate the first EF migration.  
> **Audience:** Developers and AI Agents in Execution Mode.  
> **Outcome:** 📖 A `HubJob` entity flows from EFModels → DataObjects → DataAccess → DataController, matching the existing Agent pattern.

---

## 1. What Already Exists (The "Before")

The Agent monitoring stack is **complete** — these are the implemented contracts that the Job Queue will mirror:

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │                    EXISTING DATA CONTRACT MAP                        │
  │                                                                      │
  │  EFModels/                           DataObjects/                    │
  │  ─────────────────────               ──────────────────────          │
  │  FreeServicesHub.App.Agent.cs  ←──►  FreeServicesHub.App.            │
  │  FreeServicesHub.App.                  DataObjects.Agents.cs         │
  │    AgentHeartbeat.cs           ←──►  (AgentHeartbeat class)          │
  │  FreeServicesHub.App.                FreeServicesHub.App.            │
  │    ApiClientToken.cs           ←──►    DataObjects.ApiKeys.cs       │
  │  FreeServicesHub.App.                FreeServicesHub.App.            │
  │    RegistrationKey.cs          ←──►    DataObjects.ApiKeys.cs       │
  │  FreeServicesHub.App.                                                │
  │    EFDataModel.cs              ──►  DbSet<Agent>, DbSet<Heartbeat>  │
  │                                     DbSet<ApiClientToken>            │
  │                                     DbSet<RegistrationKey>           │
  │                                                                      │
  │  DataAccess/                         Controllers/                    │
  │  ─────────────────────               ──────────────────────          │
  │  FreeServicesHub.App.                FreeServicesHub.App.API.cs      │
  │    DataAccess.Agents.cs        ──►   (Agent endpoints)              │
  │  FreeServicesHub.App.                DataController.App.cs           │
  │    DataAccess.Heartbeats.cs    ──►   (Heartbeat endpoints)          │
  │  FreeServicesHub.App.                                                │
  │    DataAccess.ApiKeys.cs       ──►   (Registration endpoints)       │
  │  FreeServicesHub.App.                                                │
  │    DataAccess.Registration.cs  ──►   (Register Agent endpoint)      │
  │                                                                      │
  │  ✅ Pattern: Each entity has an EF model, a DTO, a DataAccess file, │
  │     and a controller endpoint — all using .App. naming.              │
  └──────────────────────────────────────────────────────────────────────┘
```

### Existing Agent DTO (reference pattern)

```csharp
// FreeServicesHub.App.DataObjects.Agents.cs — ACTUAL CODE
public class Agent : ActionResponseObject
{
    public Guid AgentId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastHeartbeat { get; set; }
    public DateTime Added { get; set; }
    public string AddedBy { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public string LastModifiedBy { get; set; } = string.Empty;
    public bool Deleted { get; set; }
    // ... 10+ more properties (Hostname, Architecture, etc.)
}
```

### Existing Three-Endpoint CRUD (reference pattern)

```csharp
// FreeServicesHub.App.DataAccess.Agents.cs — ACTUAL CODE
public partial interface IDataAccess
{
    Task<List<DataObjects.Agent>> GetAgents(List<Guid>? Ids, Guid TenantId, DataObjects.User? CurrentUser = null);
    Task<List<DataObjects.Agent>> SaveAgents(List<DataObjects.Agent> Items, DataObjects.User? CurrentUser = null);
    Task<DataObjects.BooleanResponse> DeleteAgents(List<Guid>? Ids, DataObjects.User? CurrentUser = null);
}
```

---

## 2. What Needs to Be Built (The "After")

A `HubJob` represents a discrete unit of work queued for an agent to execute. The Agent polls the Hub for assigned jobs, executes them, and reports results back.

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │                    NEW JOB QUEUE DATA FLOW                           │
  │                                                                      │
  │              Hub Dashboard                Agent Worker               │
  │              ─────────────                ────────────                │
  │                   │                            │                     │
  │           User creates job                     │                     │
  │                   │                            │                     │
  │                   ▼                            │                     │
  │         ┌─────────────────┐                    │                     │
  │         │ POST /api/data/ │                    │                     │
  │         │ SaveMany<HubJob>│                    │                     │
  │         └────────┬────────┘                    │                     │
  │                  │                             │                     │
  │                  ▼                             │                     │
  │         ┌─────────────────┐                    │                     │
  │         │   DataAccess    │                    │                     │
  │         │   SaveJobs()    │──── SignalR ──────►│                     │
  │         └────────┬────────┘  JobQueued event   │                     │
  │                  │                             │                     │
  │                  ▼                             ▼                     │
  │         ┌─────────────────┐         ┌─────────────────┐             │
  │         │    EF Core      │         │ GET /api/agent/ │             │
  │         │  HubJob table   │◄────────│ GetMyJobs()     │             │
  │         └─────────────────┘         └────────┬────────┘             │
  │                                              │                      │
  │                                              ▼                      │
  │                                    ┌─────────────────┐              │
  │                                    │  Agent executes  │             │
  │                                    │  job locally     │             │
  │                                    └────────┬────────┘              │
  │                                             │                       │
  │                                             ▼                       │
  │                                    ┌─────────────────┐              │
  │                                    │ POST /api/agent/ │             │
  │                                    │ CompleteJob()    │             │
  │                                    └─────────────────┘              │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## 3. Task Breakdown (Parallelization)

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │              PHASE 1 TASK DEPENDENCY GRAPH                           │
  │                                                                      │
  │  ┌─────────────┐   ┌─────────────┐                                  │
  │  │ Task 1A     │   │ Task 1B     │   ◄── CAN RUN IN PARALLEL       │
  │  │ EF Entity   │   │ DTO Class   │                                  │
  │  │ (EFModels)  │   │ (DataObjs)  │                                  │
  │  └──────┬──────┘   └──────┬──────┘                                  │
  │         │                 │                                          │
  │         └────────┬────────┘                                          │
  │                  ▼                                                   │
  │         ┌─────────────────┐                                          │
  │         │ Task 2          │   ◄── DEPENDS ON 1A + 1B                │
  │         │ DbSet + Fluent  │                                          │
  │         │ (EFDataModel)   │                                          │
  │         └────────┬────────┘                                          │
  │                  │                                                   │
  │         ┌────────┴────────┐                                          │
  │         ▼                 ▼                                          │
  │  ┌─────────────┐  ┌─────────────┐   ◄── CAN RUN IN PARALLEL        │
  │  │ Task 3      │  │ Task 4      │                                   │
  │  │ DataAccess  │  │ SignalR     │                                   │
  │  │ CRUD methods│  │ update type │                                   │
  │  └──────┬──────┘  └──────┬──────┘                                   │
  │         │                │                                           │
  │         └────────┬───────┘                                           │
  │                  ▼                                                   │
  │         ┌─────────────────┐                                          │
  │         │ Task 5          │   ◄── DEPENDS ON 3 + 4                  │
  │         │ Controller      │                                          │
  │         │ endpoints       │                                          │
  │         └────────┬────────┘                                          │
  │                  │                                                   │
  │                  ▼                                                   │
  │         ┌─────────────────┐                                          │
  │         │ Task 6          │   ◄── DEPENDS ON 5 (final gate)         │
  │         │ Integration     │                                          │
  │         │ test            │                                          │
  │         └─────────────────┘                                          │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## 4. Task Details

### Task 1A: EF Entity — `FreeServicesHub.App.HubJob.cs`

**File:** `FreeServicesHub/FreeServicesHub.EFModels/FreeServicesHub.App.HubJob.cs` (NEW)  
**Parallel with:** Task 1B  
**Naming:** Follows `.App.` convention per [004_styleguide.md](004_styleguide.md)

**BEFORE:** File does not exist.

**AFTER:**
```csharp
using System;

namespace FreeServicesHub.EFModels.EFModels;

public partial class HubJob
{
    public Guid HubJobId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? AssignedAgentId { get; set; }
    public string JobType { get; set; } = null!;       // e.g. "RunScript", "RestartService", "CollectLogs"
    public string? JobPayloadJson { get; set; }         // JSON parameters for the job
    public string Status { get; set; } = null!;         // Queued, Assigned, Running, Completed, Failed, Cancelled
    public int Priority { get; set; }                   // 0 = normal, higher = more urgent
    public DateTime? ScheduledAt { get; set; }          // Null = run immediately
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ResultJson { get; set; }             // JSON output/log from agent execution
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public DateTime Created { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime LastModified { get; set; }
    public string? LastModifiedBy { get; set; }
}
```

**Pass criteria:**
- Compiles as part of `FreeServicesHub.EFModels.csproj`
- Properties mirror the naming style of `FreeServicesHub.App.Agent.cs` (Guid PK, nullable strings, datetime columns)

---

### Task 1B: DTO Class — `FreeServicesHub.App.DataObjects.Jobs.cs`

**File:** `FreeServicesHub/FreeServicesHub.DataObjects/FreeServicesHub.App.DataObjects.Jobs.cs` (NEW)  
**Parallel with:** Task 1A  

**BEFORE:** File does not exist.

**AFTER:**
```csharp
namespace FreeServicesHub;

public partial class DataObjects
{
    /// <summary>
    /// A discrete unit of work queued for an agent.
    /// Follows the same pattern as Agent/AgentHeartbeat DTOs.
    /// </summary>
    public class HubJob : ActionResponseObject
    {
        public Guid HubJobId { get; set; }
        public Guid TenantId { get; set; }
        public Guid? AssignedAgentId { get; set; }
        public string JobType { get; set; } = string.Empty;
        public string JobPayloadJson { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Priority { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ResultJson { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public string LastModifiedBy { get; set; } = string.Empty;

        // Denormalized for display (same pattern as AgentHeartbeat.AgentName)
        public string AssignedAgentName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Constants for job states — same naming pattern as AgentStatuses.
    /// </summary>
    public static class HubJobStatuses
    {
        public const string Queued = "Queued";
        public const string Assigned = "Assigned";
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
    }
}
```

**Pass criteria:**
- Compiles as part of `FreeServicesHub.DataObjects.csproj`
- Extends `ActionResponseObject` (same as `Agent`, `RegistrationKey`, `ApiClientToken`)
- Includes `HubJobStatuses` constants class (mirrors `AgentStatuses`)

---

### Task 2: DbSet + Fluent Config — `FreeServicesHub.App.EFDataModel.cs`

**File:** `FreeServicesHub/FreeServicesHub.EFModels/FreeServicesHub.App.EFDataModel.cs` (MODIFY)  
**Depends on:** Task 1A  

**BEFORE** (actual code):
```csharp
public partial class EFDataModel
{
    public virtual DbSet<Agent> Agents { get; set; }
    public virtual DbSet<RegistrationKey> RegistrationKeys { get; set; }
    public virtual DbSet<ApiClientToken> ApiClientTokens { get; set; }
    public virtual DbSet<AgentHeartbeat> AgentHeartbeats { get; set; }
```

**AFTER** (add one DbSet + fluent mapping):
```csharp
public partial class EFDataModel
{
    public virtual DbSet<Agent> Agents { get; set; }
    public virtual DbSet<RegistrationKey> RegistrationKeys { get; set; }
    public virtual DbSet<ApiClientToken> ApiClientTokens { get; set; }
    public virtual DbSet<AgentHeartbeat> AgentHeartbeats { get; set; }
    public virtual DbSet<HubJob> HubJobs { get; set; }               // ← NEW
```

Add fluent config inside `OnModelCreatingPartial`:
```csharp
    modelBuilder.Entity<HubJob>(entity =>
    {
        entity.Property(e => e.HubJobId).ValueGeneratedNever();
        entity.Property(e => e.JobType).HasMaxLength(100);
        entity.Property(e => e.Status).HasMaxLength(50);
        entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
        entity.Property(e => e.ScheduledAt).HasColumnType("datetime");
        entity.Property(e => e.StartedAt).HasColumnType("datetime");
        entity.Property(e => e.CompletedAt).HasColumnType("datetime");
        entity.Property(e => e.Created).HasColumnType("datetime");
        entity.Property(e => e.CreatedBy).HasMaxLength(255);
        entity.Property(e => e.LastModified).HasColumnType("datetime");
        entity.Property(e => e.LastModifiedBy).HasMaxLength(100);
    });
```

**Pass criteria:**
- `db.HubJobs` resolves at compile time
- `dotnet ef migrations add AddHubJob --project FreeServicesHub.EFModels` succeeds
- Migration SQL produces a `HubJob` table with correct column types

---

### Task 3: DataAccess CRUD — `FreeServicesHub.App.DataAccess.Jobs.cs`

**File:** `FreeServicesHub/FreeServicesHub.DataAccess/FreeServicesHub.App.DataAccess.Jobs.cs` (NEW)  
**Depends on:** Tasks 1A + 1B + 2  
**Parallel with:** Task 4  

**BEFORE:** File does not exist.

**AFTER** (follows exact pattern from `FreeServicesHub.App.DataAccess.Agents.cs`):
```csharp
namespace FreeServicesHub;

public partial interface IDataAccess
{
    Task<List<DataObjects.HubJob>> GetJobs(List<Guid>? Ids, Guid TenantId, DataObjects.User? CurrentUser = null);
    Task<List<DataObjects.HubJob>> SaveJobs(List<DataObjects.HubJob> Items, DataObjects.User? CurrentUser = null);
    Task<DataObjects.BooleanResponse> DeleteJobs(List<Guid>? Ids, DataObjects.User? CurrentUser = null);
    Task<List<DataObjects.HubJob>> GetJobsForAgent(Guid AgentId, Guid TenantId);
}

public partial class DataAccess
{
    public async Task<List<DataObjects.HubJob>> GetJobs(
        List<Guid>? Ids, Guid TenantId, DataObjects.User? CurrentUser = null)
    {
        List<DataObjects.HubJob> output = new();

        IQueryable<EFModels.EFModels.HubJob> query = data.HubJobs
            .Where(x => x.TenantId == TenantId);

        if (Ids != null && Ids.Any()) {
            query = query.Where(x => Ids.Contains(x.HubJobId));
        }

        List<EFModels.EFModels.HubJob> recs = await query
            .OrderByDescending(x => x.Created)
            .ToListAsync();

        // Batch-load agent names for display
        List<Guid> agentIds = recs
            .Where(x => x.AssignedAgentId.HasValue)
            .Select(x => x.AssignedAgentId!.Value)
            .Distinct().ToList();

        List<EFModels.EFModels.Agent> agents = agentIds.Any()
            ? await data.Agents.Where(a => agentIds.Contains(a.AgentId)).ToListAsync()
            : new();

        foreach (EFModels.EFModels.HubJob rec in recs) {
            output.Add(MapJobToDto(rec, agents));
        }

        return output;
    }

    public async Task<List<DataObjects.HubJob>> GetJobsForAgent(Guid AgentId, Guid TenantId)
    {
        // Return only Queued/Assigned jobs for this specific agent
        List<EFModels.EFModels.HubJob> recs = await data.HubJobs
            .Where(x => x.TenantId == TenantId
                && x.AssignedAgentId == AgentId
                && (x.Status == DataObjects.HubJobStatuses.Queued
                    || x.Status == DataObjects.HubJobStatuses.Assigned))
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Created)
            .ToListAsync();

        return recs.Select(r => MapJobToDto(r, null)).ToList();
    }

    public async Task<List<DataObjects.HubJob>> SaveJobs(
        List<DataObjects.HubJob> Items, DataObjects.User? CurrentUser = null)
    {
        List<DataObjects.HubJob> output = new();
        foreach (DataObjects.HubJob item in Items) {
            output.Add(await SaveJob(item, CurrentUser));
        }
        return output;
    }

    private async Task<DataObjects.HubJob> SaveJob(
        DataObjects.HubJob job, DataObjects.User? CurrentUser = null)
    {
        DataObjects.HubJob output = job;
        output.ActionResponse = GetNewActionResponse();

        bool newRecord = false;
        DateTime now = DateTime.UtcNow;

        EFModels.EFModels.HubJob? rec = await data.HubJobs
            .FirstOrDefaultAsync(x => x.HubJobId == output.HubJobId);

        if (rec == null) {
            if (output.HubJobId == Guid.Empty) {
                newRecord = true;
                output.HubJobId = Guid.NewGuid();
                rec = new EFModels.EFModels.HubJob {
                    HubJobId = output.HubJobId,
                    TenantId = output.TenantId,
                    Created = now,
                    CreatedBy = CurrentUserIdString(CurrentUser),
                };
            } else {
                output.ActionResponse.Messages.Add(
                    "Job '" + output.HubJobId.ToString() + "' Not Found");
                return output;
            }
        }

        rec.AssignedAgentId = output.AssignedAgentId;
        rec.JobType = MaxStringLength(output.JobType, 100);
        rec.JobPayloadJson = output.JobPayloadJson;
        rec.Status = output.Status;
        rec.Priority = output.Priority;
        rec.ScheduledAt = output.ScheduledAt;
        rec.StartedAt = output.StartedAt;
        rec.CompletedAt = output.CompletedAt;
        rec.ResultJson = output.ResultJson;
        rec.ErrorMessage = MaxStringLength(output.ErrorMessage, 2000);
        rec.RetryCount = output.RetryCount;
        rec.MaxRetries = output.MaxRetries;
        rec.LastModified = now;
        rec.LastModifiedBy = CurrentUserIdString(CurrentUser);

        try {
            if (newRecord) {
                await data.HubJobs.AddAsync(rec);
            }
            await data.SaveChangesAsync();
            output.ActionResponse.Result = true;

            await SignalRUpdate(new DataObjects.SignalRUpdate {
                TenantId = output.TenantId,
                ItemId = output.HubJobId,
                UpdateType = DataObjects.SignalRUpdateType.JobUpdated,
                Message = newRecord ? "Created" : "Updated",
                UserId = CurrentUserId(CurrentUser),
                Object = output,
            });
        } catch (Exception ex) {
            output.ActionResponse.Messages.Add(
                "Error Saving Job " + output.HubJobId.ToString());
            output.ActionResponse.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }

    public async Task<DataObjects.BooleanResponse> DeleteJobs(
        List<Guid>? Ids, DataObjects.User? CurrentUser = null)
    {
        DataObjects.BooleanResponse output = new();
        if (Ids == null || !Ids.Any()) {
            output.Messages.Add("No Job Ids provided.");
            return output;
        }

        try {
            List<EFModels.EFModels.HubJob> recs = await data.HubJobs
                .Where(x => Ids.Contains(x.HubJobId)).ToListAsync();

            data.HubJobs.RemoveRange(recs);
            await data.SaveChangesAsync();
            output.Result = true;
        } catch (Exception ex) {
            output.Messages.Add("Error Deleting Jobs");
            output.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }

    private static DataObjects.HubJob MapJobToDto(
        EFModels.EFModels.HubJob rec, List<EFModels.EFModels.Agent>? agents)
    {
        string agentName = string.Empty;
        if (rec.AssignedAgentId.HasValue && agents != null) {
            agentName = agents
                .FirstOrDefault(a => a.AgentId == rec.AssignedAgentId.Value)?.Name
                ?? string.Empty;
        }

        return new DataObjects.HubJob {
            ActionResponse = new DataObjects.ActionResponse { Result = true },
            HubJobId = rec.HubJobId,
            TenantId = rec.TenantId,
            AssignedAgentId = rec.AssignedAgentId,
            JobType = rec.JobType ?? string.Empty,
            JobPayloadJson = rec.JobPayloadJson ?? string.Empty,
            Status = rec.Status ?? string.Empty,
            Priority = rec.Priority,
            ScheduledAt = rec.ScheduledAt,
            StartedAt = rec.StartedAt,
            CompletedAt = rec.CompletedAt,
            ResultJson = rec.ResultJson ?? string.Empty,
            ErrorMessage = rec.ErrorMessage ?? string.Empty,
            RetryCount = rec.RetryCount,
            MaxRetries = rec.MaxRetries,
            Created = rec.Created,
            CreatedBy = rec.CreatedBy ?? string.Empty,
            LastModified = rec.LastModified,
            LastModifiedBy = rec.LastModifiedBy ?? string.Empty,
            AssignedAgentName = agentName,
        };
    }
}
```

**Pass criteria:**
- Compiles with `FreeServicesHub.DataAccess.csproj`
- `GetJobs(null, tenantId)` returns all jobs for a tenant
- `GetJobsForAgent(agentId, tenantId)` returns only Queued/Assigned jobs
- `SaveJobs` creates and updates with SignalR broadcast
- `DeleteJobs` hard-deletes (jobs are ephemeral, not soft-deleted like Agents)

---

### Task 4: SignalR Update Type

**File:** `FreeServicesHub/FreeServicesHub.DataObjects/DataObjects.App.cs` (MODIFY)  
**Depends on:** None (independent constant)  
**Parallel with:** Task 3  

**BEFORE** (actual code):
```csharp
public partial class SignalRUpdateType
{
    public const string AgentHeartbeat = "AgentHeartbeat";
    public const string AgentConnected = "AgentConnected";
    public const string AgentDisconnected = "AgentDisconnected";
    public const string AgentStatusChanged = "AgentStatusChanged";
    public const string AgentShutdown = "AgentShutdown";
    public const string RegistrationKeyGenerated = "RegistrationKeyGenerated";
    public const string BackgroundServiceLog = "BackgroundServiceLog";
    public const string AgentSettingsUpdated = "AgentSettingsUpdated";
    public const string AgentSettingsReport = "AgentSettingsReport";
}
```

**AFTER** (add two constants):
```csharp
public partial class SignalRUpdateType
{
    // ... existing constants unchanged ...
    public const string AgentSettingsReport = "AgentSettingsReport";
    public const string JobUpdated = "JobUpdated";         // ← NEW
    public const string JobCompleted = "JobCompleted";     // ← NEW
}
```

**Pass criteria:**
- Constants compile and are accessible from DataAccess and Client projects

---

### Task 5: Controller Endpoints — `DataController.App.cs` / `FreeServicesHub.App.API.cs`

**File:** `FreeServicesHub/FreeServicesHub/Controllers/DataController.App.cs` (MODIFY) and `FreeServicesHub/FreeServicesHub/Controllers/FreeServicesHub.App.API.cs` (MODIFY)  
**Depends on:** Tasks 3 + 4  

Two endpoint groups are needed:
1. **Dashboard endpoints** (authenticated users via DataController) — CRUD for job management
2. **Agent endpoints** (authenticated agents via API key) — fetch and complete jobs

**Dashboard CRUD** (in DataController.App.cs, same pattern as existing GetDataApp/SaveDataApp):
```csharp
// Inside GetDataApp switch/case:
case "jobs":
    output = await da.GetJobs(ids, tenantId, CurrentUser);
    break;

// Inside SaveDataApp switch/case:
case "jobs":
    output = await da.SaveJobs(
        GetItems<DataObjects.HubJob>(body), CurrentUser);
    break;

// Inside DeleteDataApp switch/case:
case "jobs":
    output = await da.DeleteJobs(ids, CurrentUser);
    break;
```

**Agent-facing endpoints** (in FreeServicesHub.App.API.cs):
```csharp
// GET /api/agent/jobs — Agent fetches its assigned jobs
[HttpGet("api/agent/jobs")]
public async Task<IActionResult> GetMyJobs()
{
    Guid agentId = (Guid)HttpContext.Items["AgentId"]!;
    Guid tenantId = (Guid)HttpContext.Items["AgentTenantId"]!;
    var jobs = await da.GetJobsForAgent(agentId, tenantId);
    return Ok(jobs);
}

// POST /api/agent/jobs/complete — Agent reports job completion
[HttpPost("api/agent/jobs/complete")]
public async Task<IActionResult> CompleteJob([FromBody] DataObjects.HubJob job)
{
    Guid agentId = (Guid)HttpContext.Items["AgentId"]!;
    job.AssignedAgentId = agentId;
    var saved = await da.SaveJobs(new List<DataObjects.HubJob> { job });
    return Ok(saved);
}
```

**Pass criteria:**
- `GET /api/data/jobs?tenantId=...` returns job list (cookie auth)
- `POST /api/data/jobs` with JSON body saves jobs (cookie auth)
- `GET /api/agent/jobs` returns agent-specific jobs (Bearer token auth)
- `POST /api/agent/jobs/complete` updates job status (Bearer token auth)
- All agent routes pass through `ApiKeyMiddleware` (already wired)

---

### Task 6: Integration Test

**File:** `FreeServicesHub.Tests.Integration/` (NEW test class)  
**Depends on:** Task 5  

```csharp
[Fact]
public async Task Jobs_RoundTrip_CreateFetchDelete()
{
    // Create a job via SaveMany
    var job = new DataObjects.HubJob {
        TenantId = _testTenantId,
        JobType = "RunScript",
        Status = DataObjects.HubJobStatuses.Queued,
        Priority = 0,
        MaxRetries = 3,
    };

    var saved = await _client.PostAsJsonAsync("/api/data/jobs",
        new List<DataObjects.HubJob> { job });
    saved.EnsureSuccessStatusCode();

    // Fetch it back
    var fetched = await _client.GetFromJsonAsync<List<DataObjects.HubJob>>(
        $"/api/data/jobs?tenantId={_testTenantId}");
    Assert.Single(fetched);
    Assert.Equal("RunScript", fetched[0].JobType);

    // Delete it
    var deleteResponse = await _client.PostAsJsonAsync(
        "/api/data/jobs/delete",
        new List<Guid> { fetched[0].HubJobId });
    deleteResponse.EnsureSuccessStatusCode();
}
```

**Pass criteria:**
- `dotnet test --filter "Jobs_RoundTrip"` passes
- Job is created with a new Guid, fetched with correct properties, and deleted

---

## 5. EF Migration Strategy

```ascii
  ┌────────────────────────────────────────────────────────────────────┐
  │                    MIGRATION SAFETY CHAIN                          │
  │                                                                    │
  │  1. Run on InMemory first (TestMe project)                        │
  │     ────────────────────────────────────                           │
  │     No schema changes; validates EF model compiles                 │
  │                                                                    │
  │  2. Generate migration against SQLite                              │
  │     ──────────────────────────────────                             │
  │     dotnet ef migrations add AddHubJob                             │
  │       --project FreeServicesHub/FreeServicesHub.EFModels           │
  │       --startup-project FreeServicesHub/FreeServicesHub            │
  │     Review the generated Up()/Down() methods                       │
  │                                                                    │
  │  3. Apply to dev database                                          │
  │     ────────────────────────                                       │
  │     dotnet ef database update                                      │
  │       --project FreeServicesHub/FreeServicesHub.EFModels           │
  │       --startup-project FreeServicesHub/FreeServicesHub            │
  │                                                                    │
  │  4. Verify schema                                                  │
  │     ──────────────                                                 │
  │     SELECT * FROM INFORMATION_SCHEMA.COLUMNS                       │
  │       WHERE TABLE_NAME = 'HubJob'                                  │
  │                                                                    │
  │  ⚠ CRITICAL: The EFDataModel uses OnModelCreatingPartial which    │
  │    separates Hub entities from FreeCRM base entities.               │
  │    Migrations will NOT collide with base FreeCRM tables.            │
  └────────────────────────────────────────────────────────────────────┘
```

---

## 6. Cascade Delete Addition

**File:** `FreeServicesHub/FreeServicesHub.DataAccess/DataAccess.App.cs` (MODIFY)  
**Location:** Inside the existing `DeleteRecordApp` method where Agent and Tenant cascade deletes are defined.

**BEFORE** (Agent cascade — actual code):
```csharp
if (Rec is EFModels.EFModels.Agent) {
    data.AgentHeartbeats.RemoveRange(...);
    data.ApiClientTokens.RemoveRange(...);
}
```

**AFTER** (add HubJob to Agent cascade):
```csharp
if (Rec is EFModels.EFModels.Agent) {
    data.AgentHeartbeats.RemoveRange(...);
    data.ApiClientTokens.RemoveRange(...);
    data.HubJobs.RemoveRange(
        data.HubJobs.Where(j => j.AssignedAgentId == agentId));  // ← NEW
}
```

And for Tenant cascade:
```csharp
if (Rec is EFModels.EFModels.Tenant) {
    // ... existing cascade deletes ...
    data.HubJobs.RemoveRange(
        data.HubJobs.Where(j => j.TenantId == tenantId));  // ← NEW
}
```

---

## 7. Project Interaction Diagram

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │            PHASE 1 — FILES TOUCHED PER PROJECT                       │
  │                                                                      │
  │  EFModels (2 files)                                                  │
  │  ├── FreeServicesHub.App.HubJob.cs .............. NEW                │
  │  └── FreeServicesHub.App.EFDataModel.cs ......... MODIFY (DbSet)    │
  │       │                                                              │
  │       ▼                                                              │
  │  DataObjects (2 files)                                               │
  │  ├── FreeServicesHub.App.DataObjects.Jobs.cs .... NEW                │
  │  └── DataObjects.App.cs ........................ MODIFY (SignalR)    │
  │       │                                                              │
  │       ▼                                                              │
  │  DataAccess (2 files)                                                │
  │  ├── FreeServicesHub.App.DataAccess.Jobs.cs ..... NEW                │
  │  └── DataAccess.App.cs ......................... MODIFY (cascade)    │
  │       │                                                              │
  │       ▼                                                              │
  │  Hub Server (2 files)                                                │
  │  ├── Controllers/DataController.App.cs .......... MODIFY (CRUD)      │
  │  └── Controllers/FreeServicesHub.App.API.cs ..... MODIFY (agent)     │
  │       │                                                              │
  │       ▼                                                              │
  │  Tests.Integration (1 file)                                          │
  │  └── Jobs_IntegrationTests.cs .................. NEW                 │
  │                                                                      │
  │  TOTAL: 5 new files, 4 modified files                                │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## 8. How to Test

| Test | Command | Expected |
|------|---------|----------|
| Solution compiles | `dotnet build FreeServicesHub.slnx` | 0 errors |
| EF migration generates | `dotnet ef migrations add AddHubJob ...` | Migration file created |
| InMemory round-trip | `dotnet run --project FreeServicesHub.TestMe -- --test=5` | Job created + fetched |
| Integration test | `dotnet test --filter "Jobs_RoundTrip"` | Green |
| Dashboard API | `curl -X GET https://localhost:7271/api/data/jobs?tenantId=...` | 200 + JSON array |
| Agent API | `curl -H "Authorization: Bearer <token>" https://localhost:7271/api/agent/jobs` | 200 + JSON array |

---

## 🤖 AI Analysis

> **Agent:** `4/15/2026/opus/4.6`

**Design rationale:** The HubJob entity deliberately mirrors the Agent entity's lifecycle pattern (Guid PK, TenantId scope, Created/LastModified audit fields) so developers familiar with one immediately understand the other. The `AssignedAgentId` is nullable because a job may be queued before any agent is assigned — this enables both "push to specific agent" and "next available agent" workflows.

**Naming consistency:** Every new file follows the `FreeServicesHub.App.{Layer}.{Feature}.cs` pattern already established in the workspace. The `HubJob` prefix (not just `Job`) avoids ambiguity with any future FreeCRM base `Job` concept.

**Intentional omissions:** This phase does NOT add the Agent polling loop (that's Phase 2, doc [603](603_phase2_agent_security.md)) or the Dashboard UI for jobs (that's Phase 3, doc [604](604_phase3_dashboard_ui.md)). Phase 1 purely establishes the data plumbing so Phases 2 and 3 have stable contracts to build against.
