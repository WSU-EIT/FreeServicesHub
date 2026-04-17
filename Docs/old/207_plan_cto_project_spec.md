# 207 — Plan: CTO Project Spec

> **Document ID:** 207
> **Category:** Plan
> **Purpose:** Definitive spec — pages, tables, what exists, what's new, CI/CD, Windows agent.
> **Audience:** Full team.
> **Target Platform:** Windows only. No Linux/Mac.

---

## 1. Pages We're Building

### 1A. Agent Dashboard

```
 ┌──────────────────────────────────────────────────────────────────┐
 │  AGENT DASHBOARD                        [Card View] [Table View] │
 │                                                                  │
 │  ┌─ AboutSection ──────────────────────────────────────────────┐ │
 │  │ Real-time monitor for all service agents reporting to hub.  │ │
 │  │ Cards update every 30s. Click a card to see detail + logs.  │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 │                                                                  │
 │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐   SUMMARY BADGES          │
 │  │  3   │ │  0   │ │  1   │ │  1   │                           │
 │  │Online│ │ Warn │ │Error │ │ Off  │                           │
 │  └──────┘ └──────┘ └──────┘ └──────┘                           │
 │                                                                  │
 │  row-cols-1 row-cols-md-2 row-cols-lg-3 row-cols-xl-4 g-3      │
 │  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐            │
 │  │ PROD-01      │ │ PROD-02      │ │ PROD-03      │            │
 │  │ ● Online     │ │ ● Online     │ │ ● Online     │            │
 │  │ CPU: 73% ▲   │ │ CPU: 12% ●   │ │ CPU: 45% ●   │            │
 │  │ MEM: 52% ▲   │ │ MEM: 31% ●   │ │ MEM: 38% ●   │            │
 │  │ DSK: 82% ✕   │ │ DSK: 44% ●   │ │ DSK: 29% ●   │            │
 │  │ border-danger │ │ border-succes│ │ border-succes│            │
 │  └──────────────┘ └──────────────┘ └──────────────┘            │
 │                                                                  │
 │  ┌─ ACTIVITY FEED (last 100) ──────────────────────────────────┐ │
 │  │ ● 02:00:15 PROD-01  Heartbeat           [OK]               │ │
 │  │ ▲ 02:00:14 PROD-01  Disk C: crossed 80% [WARN]             │ │
 │  │ ✕ 01:59:45 STAGING  Connection lost      [ERROR]            │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 └──────────────────────────────────────────────────────────────────┘

 Features:
 - Live SignalR updates (AgentMonitor group)
 - Card border = worst metric status
 - Click card → Agent Detail page
 - Activity feed inserts at top, cap 100
 - Filter by status (Online/Warning/Error/Offline)
```

**File:** `FreeServicesHub.App.AgentDashboard.razor`

### 1B. Agent Detail (with charts)

```
 ┌──────────────────────────────────────────────────────────────────┐
 │  < Back to Dashboard                                             │
 │                                                                  │
 │  SERVER: PROD-01              Status: ● Online                  │
 │  OS: Windows 11 / x64        Uptime: 14d 6h 23m                │
 │  Agent: 1.0.0  .NET: 10.0    Token: ****a7f2 [Revoke]          │
 │                                                                  │
 │  ┌─ CPU (24h) ─────────────────────────────────────────────────┐ │
 │  │ 90% ─────────────────────── ERROR ──────────────────────── │ │
 │  │ 70% ─────────────────────── WARNING ────────────────────── │ │
 │  │      ╱╲    ╱╲                                               │ │
 │  │  ╱╲╱╱  ╲╱╱  ╲╱╲  ╱╲╱╲                                    │ │
 │  │  2AM    6AM   10AM  2PM   6PM   10PM                       │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 │                                                                  │
 │  ┌─ MEMORY ──────┐ ┌─ DISK C: ──────┐ ┌─ DISK D: ──────┐     │
 │  │ 4.2/8 GB 52%  │ │ 164/200GB 82%  │ │ 115/500GB 23%  │     │
 │  │ [mini chart]  │ │ [mini chart]   │ │ [mini chart]   │     │
 │  │ WARNING       │ │ ERROR          │ │ OK             │     │
 │  └───────────────┘ └────────────────┘ └────────────────┘     │
 │                                                                  │
 │  ┌─ RECENT LOGS ───────────────────────────────────────────────┐ │
 │  │ 02:00:15 [INFO] Heartbeat sent                              │ │
 │  │ 02:00:14 [WARN] Disk C: 82%                                │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 └──────────────────────────────────────────────────────────────────┘

 Features:
 - Highcharts Column (24 hourly data points) via CDN chain-load
 - plotLines for warning/error thresholds
 - Revoke token button (with DeleteConfirmation dialog)
 - Live log tail via SignalR
```

**File:** `FreeServicesHub.App.AgentDetail.razor`

### 1C. API Key Manager

```
 ┌──────────────────────────────────────────────────────────────────┐
 │  API KEY MANAGER                                                 │
 │                                                                  │
 │  ┌─ AboutSection ──────────────────────────────────────────────┐ │
 │  │ Manage API client tokens for registered agents. Each agent  │ │
 │  │ gets one token at registration. Revoke here to disconnect.  │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 │                                                                  │
 │  ┌─ ACTIVE TOKENS ─────────────────────────────────────────────┐ │
 │  │ Agent        │ Prefix   │ Created    │ Status  │ Action     │ │
 │  │─────────────────────────────────────────────────────────────│ │
 │  │ PROD-01      │ a7f2...  │ 2026-04-07 │ Active  │ [Revoke]  │ │
 │  │ PROD-02      │ b3e1...  │ 2026-04-07 │ Active  │ [Revoke]  │ │
 │  │ STAGING-01   │ c9d4...  │ 2026-04-06 │ Revoked │ —         │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 └──────────────────────────────────────────────────────────────────┘

 Features:
 - Table of all tokens (active + revoked)
 - Revoke with two-step DeleteConfirmation
 - Token prefix shown (never full hash)
 - Filter: Active / Revoked / All
```

**File:** `FreeServicesHub.App.ApiKeyManager.razor`

### 1D. Registration Key Manager

```
 ┌──────────────────────────────────────────────────────────────────┐
 │  REGISTRATION KEY MANAGER                                        │
 │                                                                  │
 │  ┌─ AboutSection ──────────────────────────────────────────────┐ │
 │  │ Generate one-time registration keys for CI/CD pipelines.    │ │
 │  │ Keys expire in 24 hours. Each key registers one agent.      │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 │                                                                  │
 │  Generate: [  3  ] keys  [Generate]                             │
 │                                                                  │
 │  ┌─ GENERATED KEYS (copy now — shown once!) ───────────────────┐ │
 │  │ ⚠ These keys will NOT be shown again after you leave.       │ │
 │  │                                                              │ │
 │  │ Key 1: Rk9yZXN0R3VtcC4uLg==...  [Copy]                    │ │
 │  │ Key 2: QW5vdGhlcktleS4uLg==...  [Copy]                    │ │
 │  │ Key 3: VGhpcmRLZXkuLi4=.......  [Copy]                    │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 │                                                                  │
 │  ┌─ KEY HISTORY ───────────────────────────────────────────────┐ │
 │  │ Prefix   │ Created    │ Expires    │ Status     │ Used By   │ │
 │  │────────────────────────────────────────────────────────────│  │
 │  │ Rk9y...  │ 2026-04-08 │ 2026-04-09 │ Used       │ PROD-01  │ │
 │  │ QW5v...  │ 2026-04-08 │ 2026-04-09 │ Used       │ PROD-02  │ │
 │  │ VGhp...  │ 2026-04-08 │ 2026-04-09 │ Unused     │ —        │ │
 │  │ oLD2...  │ 2026-04-07 │ 2026-04-08 │ Expired    │ —        │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 └──────────────────────────────────────────────────────────────────┘

 Features:
 - Bulk generate N keys (admin-only endpoint)
 - Plaintext shown once in alert box with copy buttons
 - History table shows prefix, status (Used/Unused/Expired), which agent used it
```

**File:** `FreeServicesHub.App.RegistrationKeys.razor`

---

## 2. Database Tables

### Already Implemented (Phase 1A-1D, merged to main)

#### Agents
| Column | Type | Notes |
|--------|------|-------|
| AgentId | GUID PK | ValueGeneratedNever |
| TenantId | GUID | FK to Tenants |
| Name | nvarchar(255) | Required |
| Hostname | nvarchar(255) | |
| OperatingSystem | nvarchar(100) | |
| Architecture | nvarchar(50) | x64, ARM64 |
| AgentVersion | nvarchar(50) | |
| DotNetVersion | nvarchar(50) | |
| Status | nvarchar(50) | Online/Warning/Error/Offline/Stale |
| LastHeartbeat | datetime | |
| RegisteredAt | datetime | |
| RegisteredBy | nvarchar(255) | |
| Added | datetime | |
| AddedBy | nvarchar(100) | |
| LastModified | datetime | |
| LastModifiedBy | nvarchar(100) | |
| Deleted | bit | Soft delete |
| DeletedAt | datetime | |

#### RegistrationKeys
| Column | Type | Notes |
|--------|------|-------|
| RegistrationKeyId | GUID PK | ValueGeneratedNever |
| TenantId | GUID | |
| KeyHash | nvarchar(100) | SHA-256 hash |
| KeyPrefix | nvarchar(20) | First 8 chars for display |
| ExpiresAt | datetime | 24-hour default |
| Used | bit | One-time-use flag |
| UsedByAgentId | GUID? | Which agent consumed it |
| UsedAt | datetime? | |
| Created | datetime | |
| CreatedBy | nvarchar(255) | |

#### ApiClientTokens
| Column | Type | Notes |
|--------|------|-------|
| ApiClientTokenId | GUID PK | ValueGeneratedNever |
| AgentId | GUID | FK to Agents |
| TenantId | GUID | |
| TokenHash | nvarchar(100) | SHA-256 hash |
| TokenPrefix | nvarchar(20) | Last 4 chars for display |
| Active | bit | Revocable |
| Created | datetime | |
| RevokedAt | datetime? | |
| RevokedBy | nvarchar(255) | |

#### AgentHeartbeats
| Column | Type | Notes |
|--------|------|-------|
| HeartbeatId | GUID PK | ValueGeneratedNever |
| AgentId | GUID | FK to Agents |
| Timestamp | datetime | |
| CpuPercent | float | |
| MemoryPercent | float | |
| MemoryUsedGB | float | |
| MemoryTotalGB | float | |
| DiskMetricsJson | nvarchar(max) | JSON array of {Drive, UsedGB, TotalGB, Percent} |
| CustomDataJson | nvarchar(max) | Extensible |

---

## 3. What Already Exists (Don't Rebuild)

The FreeCRM fork gives us everything below for free:

| Feature | Where | Notes |
|---------|-------|-------|
| **Auth/Login** | Built-in | admin:admin default, JWT, OAuth providers |
| **Multi-Tenancy** | `DataAccess.Tenants.cs` | TenantId on all queries, URL-based routing |
| **SignalR Hub** | `signalrHub.cs` | `[Authorize]`, JoinTenantId, SignalRUpdate |
| **EF Core** | `EFDataModel.cs` | 5 providers: InMemory, MySQL, PostgreSQL, SQLite, SQL Server |
| **Tags Module** | `DataAccess.Tags.cs` | Kept from fork (`keep:Tags`) |
| **Background Service** | `Program.cs` | 60s interval, `ProcessBackgroundTasksApp` hook |
| **DataAccess .App.** | `DataAccess.App.cs` | All CRUD hooks, delete hooks, background task hook |
| **DataController .App.** | `DataController.App.cs` | Custom endpoints, `Authenticate_App`, `SignalRUpdateApp` |
| **Program .App.** | `Program.App.cs` | Builder/app modification hooks, config loading |
| **Helpers** | `Helpers.App.cs` | Navigation, HTTP, serialization, menus, icons, SignalR processing |
| **DataModel** | `DataModel.App.cs` | Client-side data model extensions |
| **UI Components** | All `.App.razor` files | 15+ component extension points |
| **CSS/JS** | wwwroot | Bootstrap 5, Font Awesome 6, jQuery 3.7, SortableJS |
| **Localization** | `<Language Tag="..." />` | Full i18n system |
| **File Storage** | `DataAccess.FileStorage.cs` | Upload, encryption, serve |
| **Plugins** | `DataAccess.Plugins.cs` | Dynamic C# compilation |
| **Settings** | `DataAccess.Settings.cs` | Key-value store per tenant |
| **Config** | `ConfigurationHelper` | Partial interface pattern for app properties |

---

## 4. CI/CD Pipeline — API Key Injection

### Pattern: FileTransform@2

From `Examples/FreeServices/azure-pipelines-crossplatform.yml`:

```yaml
# The FileTransform task replaces JSON values in appsettings.json
# with pipeline variables using dot-notation matching.
# Variable "App.AgentApiKey" → replaces { "App": { "AgentApiKey": "..." } }

- task: FileTransform@2
  displayName: 'Inject config into appsettings.json'
  inputs:
    folderPath: '$(Build.ArtifactStagingDirectory)'
    jsonTargetFiles: '**/appsettings.json'
```

### Pattern: Variables File

From `Examples/FreeServices/variables.yml`:

```yaml
variables:
  buildConfiguration: 'Release'
  dotnetVersion: '10.0.x'

  # These get injected into appsettings.json by FileTransform@2
  # Dot-notation maps to JSON paths:
  #   "App.RegistrationKey" → { "App": { "RegistrationKey": "value" } }
  App.RegistrationKey: ''          # Set at runtime by key generation step
  App.HubUrl: 'https://hub.example.com'
  App.AgentHeartbeatIntervalSeconds: 30
```

### Full Pipeline Flow for Agent Deployment

```yaml
stages:
- stage: PrepareKeys
  jobs:
  - job: GenerateKeys
    steps:
    # First, call the hub API to generate registration keys while hub is still up
    - script: |
        KEYS=$(curl -s -X POST "$(HubUrl)/api/Data/GenerateRegistrationKeys/" \
          -H "Authorization: Bearer $(AdminToken)" \
          -H "Content-Type: application/json" \
          -d '{"Count": 3}')
        echo "##vso[task.setvariable variable=RegKey1;isOutput=true]$(echo $KEYS | jq -r '.[0]')"
        echo "##vso[task.setvariable variable=RegKey2;isOutput=true]$(echo $KEYS | jq -r '.[1]')"
        echo "##vso[task.setvariable variable=RegKey3;isOutput=true]$(echo $KEYS | jq -r '.[2]')"
      displayName: 'Generate registration keys from hub API'
      name: keys

- stage: DeployAgents
  dependsOn: PrepareKeys
  jobs:
  - deployment: DeployAgent1
    environment: 'prod-server-01'
    variables:
      App.RegistrationKey: $[stageDependencies.PrepareKeys.GenerateKeys.outputs['keys.RegKey1']]
      App.HubUrl: '$(HubUrl)'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: FileTransform@2
            displayName: 'Inject registration key into appsettings.json'
            inputs:
              folderPath: '$(Pipeline.Workspace)/drop'
              jsonTargetFiles: '**/appsettings.json'

          # Now, install and start the Windows service
          - script: |
              sc.exe stop FreeServicesHub.Agent
              sc.exe delete FreeServicesHub.Agent
              xcopy /E /Y "$(Pipeline.Workspace)\drop\*" "C:\Services\FreeServicesHub.Agent\"
              sc.exe create FreeServicesHub.Agent binPath="C:\Services\FreeServicesHub.Agent\FreeServicesHub.Agent.exe" start=delayed-auto
              sc.exe start FreeServicesHub.Agent
            displayName: 'Install and start agent Windows service'
```

---

## 5. Windows Agent Service Spec

**Target: Windows only.** No Linux/Mac. Uses `sc.exe` for service management.

### Agent Project Structure

```
FreeServicesHub.Agent/
  Program.cs                    // Host builder, AddWindowsService()
  AgentWorkerService.cs         // BackgroundService — heartbeat loop
  AgentSignalRClient.cs         // HubConnection, reconnect with backoff
  AgentRegistrationService.cs   // One-time registration flow
  AgentLogBuffer.cs             // Local log buffer for offline fallback
  appsettings.json              // HubUrl, RegistrationKey, ApiClientToken
```

### Worker Loop

```
ON START:
  1. Read appsettings.json
  2. If no ApiClientToken → run registration flow
  3. Connect to SignalR hub with Bearer token
  4. Enter heartbeat loop

HEARTBEAT LOOP (every 30s):
  1. Collect: CPU%, Memory (used/total), Disk (per drive)
  2. Collect: buffered logs since last heartbeat
  3. Send via SignalR: InvokeAsync("AgentHeartbeat", payload)
  4. If send fails → buffer locally, enter reconnect

RECONNECT (exponential backoff):
  Attempt 1: wait 2s
  Attempt 2: wait 4s
  Attempt 3: wait 8s
  Attempt 4: wait 16s
  Attempt 5+: wait 30s
  On reconnect: flush local buffer

ON SHUTDOWN COMMAND (from hub via SignalR):
  1. Stop heartbeat loop
  2. Flush remaining logs
  3. Confirm shutdown to hub
  4. Exit gracefully
```

### Registration Flow

```
1. Read RegistrationKey from appsettings.json
2. POST to hub: /api/Data/RegisterAgent/
   Body: { RegistrationKey, Hostname, OS, Architecture, AgentVersion, DotNetVersion }
3. Hub validates key (hash match, not expired, not used)
4. Hub burns key (Used=true), creates Agent record, generates ApiClientToken
5. Hub returns: { AgentId, ApiClientToken (plaintext), HubUrl }
6. Agent writes ApiClientToken back to appsettings.json
7. Agent clears RegistrationKey from appsettings.json
8. Agent connects to SignalR with new token
```

### System Snapshot Collection (Windows)

```csharp
// CPU — via System.Diagnostics.PerformanceCounter
PerformanceCounter cpuCounter = new("Processor", "% Processor Time", "_Total");
double cpu = cpuCounter.NextValue();

// Memory — via GlobalMemoryStatusEx or GC info
double memUsed = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

// Disk — via DriveInfo
foreach (DriveInfo drive in DriveInfo.GetDrives())
{
    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
    {
        double used = (drive.TotalSize - drive.TotalFreeSpace) / 1_073_741_824.0;
        double total = drive.TotalSize / 1_073_741_824.0;
        double percent = (used / total) * 100;
    }
}
```

### Agent Installer

```
FreeServicesHub.Agent.Installer/
  Program.cs          // Dual interface: interactive menu OR CLI flags
  InstallerConfig.cs  // Config model
```

**Interactive Menu:**
```
FreeServicesHub Agent Installer
================================
1. Install and Configure
2. Remove Service
3. Check Status
4. Exit

Choice: 1

Hub URL: https://hub.example.com
Registration Key: [paste key]
Service Account [LocalSystem]:

Installing...
  Writing appsettings.json... done
  sc.exe create... done
  sc.exe start... done
  Waiting for registration... done
  Agent registered as PROD-01 (token: ****a7f2)

Writing .configured marker... done
```

**CLI Mode (for CI/CD):**
```
FreeServicesHub.Agent.Installer.exe install ^
  --hub-url https://hub.example.com ^
  --registration-key Rk9yZXN0R3VtcC4uLg== ^
  --service-name FreeServicesHub.Agent ^
  --service-account LocalSystem
```

---

## 6. Implementation Status

| Phase | Task | Status | Notes |
|-------|------|--------|-------|
| 1 | 1A DTOs | **Done** | DataObjects.Agents.cs, DataObjects.ApiKeys.cs |
| 1 | 1B EF entities | **Done** | 4 entity files + EFDataModel partial |
| 1 | 1C Config | **Done** | Config.cs, App.Program.cs, appsettings.json |
| 1 | 1D SignalR types | **Done** | 6 types in DataObjects.App.cs |
| 1 | 1E DataAccess Agents | **Done** | GetMany/SaveMany/DeleteMany in DataAccess.Agents.cs |
| 1 | 1F DataAccess API Keys | **Done** | Generate, validate, revoke (SHA-256) in DataAccess.ApiKeys.cs |
| 1 | 1G DataAccess Registration | **Done** | Register + burn key + issue token in DataAccess.Registration.cs |
| 1 | 1H API Endpoints | **Done** | 10 endpoints in FreeServicesHub.App.API.cs |
| 2 | 2A API Key Middleware | **Done** | FreeServicesHub.App.ApiKeyMiddleware.cs |
| 2 | 2C AgentMonitorService | **Done** | FreeServicesHub.App.AgentMonitorService.cs |
| 2 | 2B/2D Program hooks | **Done** | Middleware + hosted service registered |
| 2 | 2E-2G DataAccess/Helpers/Model hooks | **Done** | Background prune, menu items, SignalR handlers |
| 3 | Agent Service | **Done** | FreeServicesHub.Agent/ (worker + SignalR heartbeat) |
| 3 | Agent Installer | **Done** | FreeServicesHub.Agent.Installer/ (Windows, sc.exe) |
| 4 | Agent Dashboard | **Done** | Card grid with threshold colors, activity feed |
| 4 | Agent Detail | **Done** | Metrics, heartbeat history, logs, revoke |
| 4 | API Key Manager | **Done** | Generate/revoke, one-time display |
| 4 | AboutSection + CSS | **Done** | Ported component, agent card styles |
| 5 | CI/CD Pipeline | **Done** | Pipelines/deploy-freeserviceshub.yml (6 stages) |

---

*Created: 2026-04-08*
*Maintained by: [CTO]*
