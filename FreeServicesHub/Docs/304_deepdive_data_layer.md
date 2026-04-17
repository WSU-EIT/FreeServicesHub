# 304 — Deep Dive: Data Layer

> **Document ID:** 304  
> **Category:** Reference — Deep Dive  
> **Investigator:** Agent 4 (Data Layer Specialist)  
> **Scope:** EF Models, DataObjects, and DataAccess for the agent subsystem  
> **Outcome:** Complete understanding of the database schema, DTOs, and data access patterns for agents, heartbeats, registration keys, and API client tokens.

---

## Executive Summary

The agent data layer follows the established FreeCRM/FreeExamples three-tier pattern: **EF Models** (database entities) → **DataObjects** (DTOs/view models) → **DataAccess** (business logic + persistence). Four new entity types support the agent system: `Agent`, `AgentHeartbeat`, `RegistrationKey`, and `ApiClientToken`. The security model uses SHA-256 hashed tokens throughout — plaintext keys are never stored.

---

## Layer Map

```
┌─────────────────────────────────────────────────────────┐
│                   DataObjects (DTOs)                    │
│  FreeServicesHub.DataObjects project                    │
│  ├── Agent                  (+ AgentStatuses constants) │
│  ├── AgentHeartbeat         (+ DiskMetric helper)       │
│  ├── RegistrationKey        (+ NewKeyPlaintext transient)│
│  ├── ApiClientToken         (+ NewTokenPlaintext)       │
│  ├── AgentRegistrationRequest                           │
│  ├── AgentRegistrationResponse                          │
│  └── SignalRUpdateType extensions                       │
├─────────────────────────────────────────────────────────┤
│                   EF Models (Entities)                  │
│  FreeServicesHub.EFModels project                       │
│  ├── Agent                                              │
│  ├── AgentHeartbeat                                     │
│  ├── RegistrationKey                                    │
│  ├── ApiClientToken                                     │
│  └── EFDataModel (DbContext partial)                    │
├─────────────────────────────────────────────────────────┤
│                   DataAccess (Logic)                    │
│  FreeServicesHub.DataAccess project                     │
│  ├── GetAgents / SaveAgents / DeleteAgents              │
│  ├── RegisterAgent                                      │
│  ├── GenerateRegistrationKeys / ValidateRegistrationKey │
│  ├── GenerateApiClientToken / ValidateApiClientToken    │
│  ├── RevokeApiClientToken / GetApiClientTokens          │
│  ├── SaveHeartbeat / GetHeartbeats / PruneHeartbeats    │
│  └── HashKey / GeneratePlaintextKey (crypto helpers)    │
└─────────────────────────────────────────────────────────┘
```

---

<a id="ef-models"></a>
## EF Models (Database Schema)

### Agent

| Column | Type | Constraints |
|--------|------|-------------|
| `AgentId` | `Guid` | PK, `ValueGeneratedNever` |
| `TenantId` | `Guid` | FK to Tenant |
| `Name` | `string` | MaxLength 255, required |
| `Hostname` | `string?` | MaxLength 255 |
| `OperatingSystem` | `string?` | MaxLength 100 |
| `Architecture` | `string?` | MaxLength 50 |
| `AgentVersion` | `string?` | MaxLength 50 |
| `DotNetVersion` | `string?` | MaxLength 50 |
| `Status` | `string?` | MaxLength 50 (Online/Warning/Error/Offline/Stale) |
| `LastHeartbeat` | `DateTime?` | datetime |
| `RegisteredAt` | `DateTime?` | datetime |
| `RegisteredBy` | `string?` | MaxLength 255 |
| `Added` | `DateTime` | datetime |
| `AddedBy` | `string?` | MaxLength 100 |
| `LastModified` | `DateTime` | datetime |
| `LastModifiedBy` | `string?` | MaxLength 100 |
| `Deleted` | `bool` | Soft-delete flag |
| `DeletedAt` | `DateTime?` | datetime |

### AgentHeartbeat

| Column | Type | Constraints |
|--------|------|-------------|
| `HeartbeatId` | `Guid` | PK, `ValueGeneratedNever` |
| `AgentId` | `Guid` | FK to Agent |
| `Timestamp` | `DateTime` | datetime |
| `CpuPercent` | `double` | |
| `MemoryPercent` | `double` | |
| `MemoryUsedGB` | `double` | |
| `MemoryTotalGB` | `double` | |
| `DiskMetricsJson` | `string?` | JSON array of `{Drive, UsedGB, TotalGB, Percent}` |
| `CustomDataJson` | `string?` | Extensible JSON block |

### RegistrationKey

| Column | Type | Constraints |
|--------|------|-------------|
| `RegistrationKeyId` | `Guid` | PK, `ValueGeneratedNever` |
| `TenantId` | `Guid` | FK to Tenant |
| `KeyHash` | `string` | MaxLength 100, required (SHA-256) |
| `KeyPrefix` | `string?` | MaxLength 20 (first 8 chars for display) |
| `ExpiresAt` | `DateTime` | datetime |
| `Used` | `bool` | One-time-use flag |
| `UsedByAgentId` | `Guid?` | FK to Agent (set when burned) |
| `UsedAt` | `DateTime?` | datetime |
| `Created` | `DateTime` | datetime |
| `CreatedBy` | `string?` | MaxLength 255 |

### ApiClientToken

| Column | Type | Constraints |
|--------|------|-------------|
| `ApiClientTokenId` | `Guid` | PK, `ValueGeneratedNever` |
| `AgentId` | `Guid` | FK to Agent |
| `TenantId` | `Guid` | FK to Tenant |
| `TokenHash` | `string` | MaxLength 100, required (SHA-256) |
| `TokenPrefix` | `string?` | MaxLength 20 (first 8 chars for display) |
| `Active` | `bool` | Revocation flag |
| `Created` | `DateTime` | datetime |
| `RevokedAt` | `DateTime?` | datetime |
| `RevokedBy` | `string?` | MaxLength 255 |

### DbContext Registration

All four entities are registered in `EFDataModel` partial class with Fluent API configuration:

```csharp
public virtual DbSet<Agent> Agents { get; set; }
public virtual DbSet<RegistrationKey> RegistrationKeys { get; set; }
public virtual DbSet<ApiClientToken> ApiClientTokens { get; set; }
public virtual DbSet<AgentHeartbeat> AgentHeartbeats { get; set; }
```

---

<a id="data-objects"></a>
## DataObjects (DTOs)

### Agent DTO

Extends `ActionResponseObject` (inherits `ActionResponse` with `Result` bool and `Messages` list). Mirrors EF model fields exactly, adding:
- Inherited `ActionResponse` for success/failure tracking

### AgentHeartbeat DTO

Adds `AgentName` (denormalized from Agent table for display convenience).

### DiskMetric

Helper class for deserializing `DiskMetricsJson`:

```csharp
public class DiskMetric
{
    public string Drive { get; set; }
    public double UsedGB { get; set; }
    public double TotalGB { get; set; }
    public double Percent { get; set; }
}
```

### AgentStatuses Constants

```csharp
public static class AgentStatuses
{
    public const string Online = "Online";
    public const string Warning = "Warning";
    public const string Error = "Error";
    public const string Offline = "Offline";
    public const string Stale = "Stale";
}
```

### Registration Request/Response

```csharp
// Request (agent → server)
public class AgentRegistrationRequest
{
    public string RegistrationKey { get; set; }    // Plaintext key
    public string Hostname { get; set; }
    public string OperatingSystem { get; set; }
    public string Architecture { get; set; }
    public string AgentVersion { get; set; }
    public string DotNetVersion { get; set; }
}

// Response (server → agent)
public class AgentRegistrationResponse : ActionResponseObject
{
    public Guid AgentId { get; set; }
    public string ApiClientToken { get; set; }     // Plaintext token (shown ONCE)
    public string HubUrl { get; set; }
}
```

### RegistrationKey DTO

Includes `NewKeyPlaintext` — a transient field populated only at generation time, never stored.

### ApiClientToken DTO

Includes `NewTokenPlaintext` — same transient pattern.

---

<a id="security-model"></a>
## Security Model — SHA-256 Token Hashing

### Key Generation

```csharp
private static string GeneratePlaintextKey()
{
    byte[] bytes = new byte[32];        // 256 bits of entropy
    RandomNumberGenerator.Fill(bytes);  // Cryptographically secure
    return Convert.ToBase64String(bytes);
}
```

### Hashing

```csharp
private static string HashKey(string plaintext)
{
    byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
    return Convert.ToBase64String(hash);
}
```

### Token Lifecycle

```
Generate → plaintext returned to caller ONCE → SHA-256(plaintext) stored in DB
              │
              ▼
Validate → SHA-256(incoming) → compare against stored hash
              │
              ▼
Revoke → set Active=false, RevokedAt, RevokedBy
```

The plaintext is shown exactly once (on generation) and never stored. The `KeyPrefix`/`TokenPrefix` (first 8 chars of plaintext) is stored for display/identification purposes.

---

<a id="data-access-operations"></a>
## DataAccess Operations

### Agent CRUD

| Method | Behavior |
|--------|----------|
| `GetAgents(ids, tenantId, user)` | Tenant-scoped query. Admins see soft-deleted records; non-admins don't. |
| `SaveAgents(items, user)` | Iterates and calls `SaveAgent()` for each. Broadcasts `AgentStatusChanged` on save. |
| `DeleteAgents(ids, user)` | Soft-delete: sets `Deleted=true`, `DeletedAt=now`. Broadcasts per agent. |

### Registration

| Method | Behavior |
|--------|----------|
| `RegisterAgent(request, tenantId)` | Validates key → creates Agent → burns key → generates ApiClientToken → broadcasts `AgentConnected` |
| `ValidateRegistrationKey(plaintext, tenantId)` | SHA-256 hash lookup. Checks: correct tenant, not used, not expired. |
| `GenerateRegistrationKeys(count, tenantId, user)` | Batch creates keys. Stores hash + prefix. Returns plaintext once. Broadcasts `RegistrationKeyGenerated`. |

### Token Management

| Method | Behavior |
|--------|----------|
| `GenerateApiClientToken(agentId, tenantId)` | Generates random 32-byte token, stores SHA-256 hash. Returns plaintext once. |
| `ValidateApiClientToken(plaintext)` | SHA-256 hash lookup. Checks `Active=true`. |
| `RevokeApiClientToken(tokenId, user)` | Sets `Active=false`, `RevokedAt=now`, `RevokedBy`. |
| `GetApiClientTokens(tenantId)` | Lists all tokens for tenant (admin view). |

### Heartbeat Management

| Method | Behavior |
|--------|----------|
| `SaveHeartbeat(heartbeat)` | Stores heartbeat record. Updates parent Agent status based on CPU/memory thresholds. Broadcasts `AgentHeartbeat`. |
| `GetHeartbeats(agentId, hours)` | Returns heartbeats within time window, ordered by timestamp descending. Denormalizes agent name. |
| `PruneHeartbeats(retentionHours)` | Deletes heartbeats older than retention window. |

### Threshold Evaluation (in SaveHeartbeat)

```
CPU ≥ CpuError(90) OR Memory ≥ MemError(90)     → Status = "Error"
CPU ≥ CpuWarning(70) OR Memory ≥ MemWarning(70) → Status = "Warning"
Otherwise                                        → Status = "Online"
```

---

<a id="signalr-integration"></a>
## SignalR Integration

Every write operation broadcasts a `SignalRUpdate` to the tenant group:

| Operation | UpdateType | Message |
|-----------|-----------|---------|
| Save agent | `AgentStatusChanged` | "Saved" |
| Delete agent | `AgentStatusChanged` | "Deleted" |
| Register agent | `AgentConnected` | "Agent Registered: {hostname}" |
| Save heartbeat | `AgentHeartbeat` | Agent status string |
| Generate keys | `RegistrationKeyGenerated` | "Generated N registration key(s)" |

This ensures the Blazor dashboard gets real-time updates for every data change without polling.

---

<a id="configuration"></a>
## Configuration (`FreeServicesHub.App.Config.cs`)

Three-part pattern: `IConfigurationHelper` (interface) → `ConfigurationHelper` (implementation) → `ConfigurationHelperLoader` (populated at startup).

| Property | Default | Usage |
|----------|---------|-------|
| `AgentHeartbeatIntervalSeconds` | 30 | Not used server-side (agent-side config) |
| `AgentStaleThresholdSeconds` | 120 | AgentMonitorService stale detection |
| `RegistrationKeyExpiryHours` | 24 | Key generation expiry |
| `HeartbeatRetentionHours` | 24 | Heartbeat pruning |
| `CpuWarningThreshold` | 70 | Heartbeat status evaluation |
| `CpuErrorThreshold` | 90 | Heartbeat status evaluation |
| `MemoryWarningThreshold` | 70 | Heartbeat status evaluation |
| `MemoryErrorThreshold` | 90 | Heartbeat status evaluation |
| `DiskWarningThreshold` | 50 | Available for future use |
| `DiskErrorThreshold` | 90 | Available for future use |

---

<a id="entity-relationship"></a>
## Entity Relationship Diagram

```
┌──────────────┐     1:N     ┌─────────────────┐
│    Tenant    │─────────────│     Agent        │
└──────────────┘             └────────┬─────────┘
                                      │
                              ┌───────┼────────┐
                              │       │        │
                         1:N  │  1:N  │   1:1  │
                              ▼       ▼        ▼
                     ┌─────────┐ ┌─────────┐ ┌──────────────┐
                     │Heartbeat│ │ApiClient│ │Registration  │
                     │         │ │Token    │ │Key (burned)  │
                     └─────────┘ └─────────┘ └──────────────┘

Tenant → RegistrationKey  (1:N, keys are tenant-scoped)
RegistrationKey → Agent   (1:1 via UsedByAgentId, set on registration)
Agent → ApiClientToken    (1:N, but typically 1:1 active)
Agent → AgentHeartbeat    (1:N, time-series data)
```

---

*Prev: [303 — Hub Server Infrastructure](303_deepdive_hub_server_infrastructure.md) | Next: [305 — Lineage & Patterns](305_deepdive_lineage_and_patterns.md)*
