# 202 — Reference: FreeServicesHub Architecture Overview

> **Document ID:** 202
> **Category:** Reference
> **Purpose:** Visual architecture reference for the FreeServicesHub.Agent system -- what exists, what's new, how the pieces connect.
> **Audience:** Full team, new contributors.
> **Outcome:** Complete visual map of the system architecture.

**Notes taken by:** [Quality]
**Presented by:** [Architect]
**Context:** Follow-up to 201 CTO kickoff meeting. Architect distilled the CTO's vision into diagrams and presented to the team.

---

## 1. System Overview — Existing vs New

The hub is built on the FreeCRM framework. Everything in the "existing" layer is inherited from the fork. Everything in the "new" layer uses the `.App.` extension pattern.

```
EXISTING (FreeCRM Base)
  Auth/Login (admin:admin)    Multi-Tenancy       Tags Module (kept)
  SignalR Hub (base)          Plugin System        Background Service
  EF Core (5 DB providers)   DataAccess .App.     DataController .App.

NEW (FreeServicesHub.App.* — Hub Side)
  Agent Management Module
    DataObjects.App     EF Entities        DataAccess.App
    - Agent             - Agent            - CRUD Agents
    - RegKey            - RegKey           - CRUD RegKeys
    - ApiToken          - ApiToken         - CRUD Tokens
    - Heartbeat         - Heartbeat        - Validate Key

  Agent Dashboard         API Key Mgmt UI     AgentMonitorService
  (.App.razor page)       (.App.razor page)   (BackgroundService)

  AboutSection            InfoTip             ApiKey Middleware
  (from FreeExamples)     (from FreeExamples) (from FreeGLBA)

NEW (Separate Projects — Agent Side)
  FreeServicesHub.Agent              FreeServicesHub.Agent.Installer
  - BackgroundService                - Dual interface (menu + CLI)
  - SignalR heartbeat loop           - configure / remove
  - Log streaming                    - .configured marker
  - System snapshots                 - sc.exe / systemd / launchd
  - Reconnect with backoff           - API key injection
  - Local log fallback               - Service account management
```

---

## 2. Two-Key Trust Chain

Two types of keys, two different lifecycles:

| Key Type | Created By | Given To | Stored As | Lifetime | Revocable |
|----------|-----------|----------|-----------|----------|-----------|
| **Registration Key** | Hub (via CI/CD API call) | Pipeline, then Agent | SHA-256 hash in DB | 24 hours, one-time-use | Burns on use |
| **API Client Token** | Hub (during registration) | Agent | SHA-256 hash in DB | Until revoked | Yes, from Hub UI |

### Flow

```
Pipeline                        Hub                         Agent
   |                             |                            |
   |-- GenerateRegistrationKeys ->|                            |
   |<- [Key A, Key B, Key C] ---|                            |
   |                             |  (stores hashes, 24hr exp) |
   |                             |                            |
   |--- deploys Key A to ------>|                            |
   |                             |                   Agent starts
   |                             |<-- RegisterAgent (Key A) --|
   |                             |    validate, burn key      |
   |                             |    generate API token      |
   |                             |--- Token X (plaintext) --->|
   |                             |    (stores hash in DB)     | (stores in config)
   |                             |                            |
   |                             |<-- heartbeat (Token X) ----|
   |                             |<-- heartbeat (Token X) ----|
   |                             |<-- heartbeat (Token X) ----|
   |                             |                            |
   |                             |  Hub revokes Token X       |
   |                             |<-- heartbeat (Token X) ----|
   |                             |--- 401 Unauthorized ------>|
   |                             |                   Agent: disconnected
```

---

## 3. Deployment Lifecycle — 12 Steps

| Step | Action | Actor | Detail |
|------|--------|-------|--------|
| 1 | Generate registration keys | Pipeline calls Hub API | N keys for N servers, one-time-use, 24hr expiry |
| 2 | Shutdown agents | Hub broadcasts via SignalR | "shutdown" command to Agents group |
| 3 | Agents confirm shutdown | Each agent | Graceful stop, confirm via SignalR |
| 4 | Hub reports all offline | Hub | Pipeline waits for confirmation |
| 5 | Shutdown hub | Pipeline | Stop the hub process |
| 6 | Deploy hub | Pipeline | Install new build, start hub |
| 7 | Hub comes online | Hub | Reconnects to existing DB, token hashes persist |
| 8 | Deploy agents | Pipeline | Install new build on each server, inject registration key |
| 9 | Agents start | Each agent | Read registration key from config |
| 10 | Agents register | Each agent calls Hub API | One-time key consumed, API client token returned |
| 11 | Agents store token | Each agent | Save to local config, begin heartbeat |
| 12 | Dashboard shows online | Hub | AgentMonitorService detects connections, pushes to UI |

**Timing:** ~5 minutes total. Scheduled at 2 AM daily. Max 24hr downtime on failure.

---

## 4. SignalR Communication

### Hub Groups

| Group | Members | Purpose |
|-------|---------|---------|
| `Agents` | Connected FreeServicesHub.Agent instances | Receive shutdown commands, broadcast messages |
| `AgentMonitor` | Dashboard viewers (browser clients) | Receive live agent status updates |

### Message Types

| Direction | Message | Content |
|-----------|---------|---------|
| Agent -> Hub | Heartbeat | Agent ID, timestamp, CPU, memory, disk, uptime |
| Agent -> Hub | LogBatch | Agent ID, list of log entries since last batch |
| Hub -> Agent | Shutdown | Graceful shutdown command |
| Hub -> Agent | Disconnect | Revoke/force disconnect |
| Hub -> Dashboard | AgentStatusUpdate | Agent ID, status, last heartbeat, snapshot data |
| Hub -> Dashboard | AgentConnected | New agent registered and online |
| Hub -> Dashboard | AgentDisconnected | Agent went offline |

### Reconnection Strategy (Agent Side)

| Attempt | Delay | Action |
|---------|-------|--------|
| 1 | 2s | Reconnect via SignalR |
| 2 | 4s | Reconnect via SignalR |
| 3 | 8s | Reconnect via SignalR |
| 4 | 16s | Reconnect via SignalR |
| 5+ | 30s | Reconnect via SignalR |
| All | — | Buffer logs locally, flush on reconnect |

---

## 5. Source Pattern Map

Every new feature traces back to a reference implementation:

| Feature | Source Project | Key Reference File |
|---------|---------------|-------------------|
| API key generation (SHA-256) | FreeGLBA | `FreeGLBA.App.DataAccess.ApiKey.cs` |
| API key middleware | FreeGLBA | `FreeGLBA.App.ApiKeyMiddleware.cs` |
| Key rotation UI | FreeGLBA | `FreeGLBA.App.EditSourceSystem.razor` |
| API request logging | FreeGLBA | `FreeGLBA.App.ApiRequestLoggingAttribute.cs` |
| Worker loop pattern | FreeServices | `SystemMonitorService.cs` |
| Cross-platform installer | FreeServices | `FreeServices.Installer/Program.cs` |
| .configured marker | FreeServices | `FreeServices.Installer/Program.cs:405` |
| Dual interface (menu+CLI) | FreeServices | `FreeServices.Installer/Program.cs:99` |
| SignalR connection tracking | FreeCICD | `signalrHub.cs` (ConcurrentDictionary) |
| Background monitor service | FreeCICD | `FreeCICD.App.PipelineMonitorService.cs` |
| Progressive dashboard loading | FreeCICD | `FreeCICD.App.DataAccess.DevOps.Dashboard.cs` |
| AboutSection component | FreeExamples | `AboutSection.razor` |
| InfoTip component | FreeExamples | `InfoTip.razor` |
| NuGet client pattern | FreeExamples | `FreeExamplesClient.cs` |
| Base framework (everything) | FreeCRM | Auth, tenants, EF, SignalR, plugins, Tags |

---

## 6. File Naming Preview

New files that will be created, following the mandatory `.App.` pattern:

### Hub Side (FreeServicesHub/)

```
DataObjects:
  FreeServicesHub.App.DataObjects.Agents.cs
  FreeServicesHub.App.DataObjects.ApiKeys.cs

EF Entities:
  FreeServicesHub.App.Agent.cs
  FreeServicesHub.App.RegistrationKey.cs
  FreeServicesHub.App.ApiClientToken.cs
  FreeServicesHub.App.AgentHeartbeat.cs

DataAccess:
  DataAccess.App.FreeServicesHub.cs
  FreeServicesHub.App.DataAccess.Agents.cs
  FreeServicesHub.App.DataAccess.ApiKeys.cs
  FreeServicesHub.App.DataAccess.Registration.cs

Controllers:
  DataController.App.FreeServicesHub.cs

Client Pages:
  FreeServicesHub.App.AgentDashboard.razor
  FreeServicesHub.App.AgentDetail.razor
  FreeServicesHub.App.ApiKeyManager.razor
  FreeServicesHub.App.EditAgent.razor

Shared Components:
  FreeServicesHub.App.AboutSection.razor
  FreeServicesHub.App.InfoTip.razor

Services:
  FreeServicesHub.App.AgentMonitorService.cs
  FreeServicesHub.App.ApiKeyMiddleware.cs
```

### Agent Side (Separate Projects)

```
FreeServicesHub.Agent/
  Program.cs
  AgentWorkerService.cs
  appsettings.json

FreeServicesHub.Agent.Installer/
  Program.cs
  InstallerConfig.cs
  appsettings.json

FreeServicesHub.Agent.TestMe/
  Program.cs
  appsettings.json
```

---

*Created: 2026-04-08*
*Maintained by: [Quality]*
