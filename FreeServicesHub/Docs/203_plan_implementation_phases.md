# 203 — Plan: FreeServicesHub.Agent Implementation Phases

> **Document ID:** 203
> **Category:** Meeting
> **Purpose:** Break the agent system into parallelizable phases with small, individually completable tasks.
> **Audience:** Full team.
> **Predicted Outcome:** Actionable implementation plan with clear task boundaries.
> **Actual Outcome:** *(to be updated)*
> **Resolution:** *(to be updated)*

---

## Attendees

- **[Architect]** — Presenting the plan, answering structural questions
- **[Backend]** — Data model, API surface
- **[Frontend]** — UI components, dashboard
- **[AgentDev]** — Agent service and installer
- **[Security]** — API key lifecycle
- **[Quality]** — Testing strategy, docs checklist
- **[Sanity]** — Complexity check
- **[JrDev]** — Learning, asking clarifying questions

---

## Discussion

**[Architect]:** Welcome everyone. [JrDev], this is your first implementation meeting so let me catch you up. We've done deep dives on every reference project in the ecosystem. The CTO briefed us in doc 201, I drew the architecture in doc 202. Now we're turning that into a build plan.

Here's the short version: we're building a service agent platform. A web hub monitors remote agents. Agents connect via SignalR, authenticate with API keys, and stream heartbeats and logs. The whole thing deploys nightly via Azure DevOps pipelines with rotating one-time registration keys.

**[JrDev]:** Got it. So what's the actual build order? Do we start with the agent or the hub?

**[Architect]:** Hub first. The agent has nothing to talk to until the hub exists. And within the hub, we start with the data model because everything depends on it -- the API endpoints need DTOs, the UI needs DTOs, the middleware needs DTOs. Data model is the foundation.

**[Backend]:** I've mapped out the entities based on the 201 decisions. Four core entities:

1. **Agent** — Registered agent instance (name, hostname, OS, status, last heartbeat, tenant)
2. **RegistrationKey** — One-time keys from CI/CD (hash, expiry, used flag, agent it was used by)
3. **ApiClientToken** — Long-lived agent auth tokens (hash, agent ID, active flag, revocable)
4. **AgentHeartbeat** — Time-series snapshots (agent ID, timestamp, CPU, memory, disk, custom JSON)

**[JrDev]:** Why separate RegistrationKey and ApiClientToken? They're both keys.

**[Security]:** Different lifecycles. Registration keys are short-lived, one-time-use, generated in bulk by the pipeline. API client tokens are long-lived, generated one-at-a-time during registration, and revocable from the hub UI. Storing them in the same table would mean mixing two very different concepts with different expiry rules, different validation logic, and different UI management.

**[JrDev]:** Makes sense. And the heartbeat table -- won't that grow huge?

**[Backend]:** Yes. That's deliberate. We'll have a background task that prunes old heartbeats -- keep the last 24 hours of detail, roll up to hourly summaries after that. The `ProcessBackgroundTasksApp` hook runs every 60 seconds, perfect for this.

**[Sanity]:** Before we go further -- how many phases are we looking at? I want to make sure each one delivers something testable. No "build everything then test at the end."

**[Architect]:** Five phases. Each one produces something that works on its own.

---

## Phase 1: Data Foundation

**Goal:** EF entities, DTOs, DataAccess CRUD, and API endpoints exist. You can create/read/update/delete agents and keys via API calls. Nothing visual yet.

**Why first:** Everything else depends on this. The UI needs DTOs. The middleware needs token validation. The agent needs registration endpoints.

### Tasks (all parallelizable)

| Task | File(s) | Owner | Depends On |
|------|---------|-------|------------|
| **1A.** Define DTOs | `FreeServicesHub.App.DataObjects.Agents.cs` | [Backend] | Nothing |
| **1B.** Define EF entities + DbContext extension | `FreeServicesHub.App.Agent.cs`, `FreeServicesHub.App.RegistrationKey.cs`, `FreeServicesHub.App.ApiClientToken.cs`, `FreeServicesHub.App.AgentHeartbeat.cs`, `FreeServicesHub.App.EFDataModel.cs` | [Backend] | Nothing |
| **1C.** Define config properties | `FreeServicesHub.App.Config.cs`, `ConfigurationHelper.App.cs` additions, appsettings.json `"App"` section | [Backend] | Nothing |
| **1D.** Define SignalR update types | `DataObjects.App.cs` additions (partial `SignalRUpdateType`) | [Backend] | Nothing |

**After 1A-1D merge:**

| Task | File(s) | Owner | Depends On |
|------|---------|-------|------------|
| **1E.** DataAccess CRUD for Agents | `FreeServicesHub.App.DataAccess.Agents.cs` | [Backend] | 1A, 1B |
| **1F.** DataAccess for API keys (generate, validate, revoke) | `FreeServicesHub.App.DataAccess.ApiKeys.cs` | [Security] | 1A, 1B |
| **1G.** DataAccess for Registration (register + burn key + issue token) | `FreeServicesHub.App.DataAccess.Registration.cs` | [Security] | 1A, 1B |
| **1H.** API endpoints (three-endpoint CRUD for agents + registration + key management) | `DataController.App.FreeServicesHub.cs`, `FreeServicesHub.App.API.cs` | [Backend] | 1E, 1F, 1G |

**Deliverable:** `dotnet build` succeeds. API endpoints callable via curl/Postman. Agents and keys CRUD works against InMemory database.

---

## Phase 2: Hub Infrastructure

**Goal:** API key middleware protects agent endpoints. AgentMonitorService tracks connected agents. SignalR hub extensions handle agent groups. The hub can receive heartbeats and broadcast status changes.

**Why second:** The hub must be able to authenticate agents and process heartbeats before we build the agent or the UI.

### Tasks (parallelizable within groups)

| Task | File(s) | Owner | Depends On |
|------|---------|-------|------------|
| **2A.** API key middleware | `FreeServicesHub.App.ApiKeyMiddleware.cs` | [Security] | Phase 1 |
| **2B.** Register middleware in startup | `Program.App.cs` (one-line tie-in to `AppModifyStart`), `FreeServicesHub.App.Program.cs` | [Security] | 2A |
| **2C.** AgentMonitorService (BackgroundService) | `FreeServicesHub.App.AgentMonitorService.cs` | [Architect] | Phase 1 |
| **2D.** Register AgentMonitorService in startup | `Program.App.cs` (one-line in `AppModifyBuilderEnd`) | [Architect] | 2C |
| **2E.** SignalR hub extensions (Agent group, heartbeat handler, shutdown broadcast) | `FreeServicesHub.App.SignalR.cs`, `DataController.App.cs` (`SignalRUpdateApp`) | [Backend] | Phase 1 |
| **2F.** Hook into DataAccess.App (deleted records, blazor data model, background tasks) | `DataAccess.App.cs` additions (hook tie-ins) | [Backend] | Phase 1 |
| **2G.** Heartbeat pruning background task | `FreeServicesHub.App.DataAccess.BackgroundTasks.cs` | [Backend] | 1E |

**Deliverable:** A fake agent (curl script or test harness) can register with a one-time key, receive a token, and send heartbeats that the hub processes and stores.

---

## Phase 3: Agent Service

**Goal:** A real cross-platform background service that registers with the hub, sends heartbeats via SignalR, streams logs, and handles shutdown commands. Plus an installer.

**Why third:** The hub is ready to receive agents. Now we build the thing that talks to it.

### Tasks (parallelizable within groups)

| Task | File(s) | Owner | Depends On |
|------|---------|-------|------------|
| **3A.** Agent worker service (heartbeat loop + system snapshots) | `FreeServicesHub.Agent/AgentWorkerService.cs`, `Program.cs` | [AgentDev] | Phase 2 |
| **3B.** Agent SignalR client (connect, auth, reconnect with backoff) | `FreeServicesHub.Agent/HubConnection.cs` or integrated in worker | [AgentDev] | Phase 2 |
| **3C.** Agent registration flow (use reg key, store API token) | `FreeServicesHub.Agent/RegistrationService.cs` | [AgentDev] + [Security] | Phase 2 |
| **3D.** Agent local log buffer (fallback when disconnected) | `FreeServicesHub.Agent/LogBuffer.cs` | [AgentDev] | Nothing |
| **3E.** Agent config and appsettings | `FreeServicesHub.Agent/appsettings.json`, csproj | [AgentDev] | Nothing |

**After 3A-3E merge:**

| Task | File(s) | Owner | Depends On |
|------|---------|-------|------------|
| **3F.** Agent installer (cross-platform, dual interface) | `FreeServicesHub.Agent.Installer/Program.cs`, `InstallerConfig.cs` | [AgentDev] | 3A-3E |
| **3G.** Agent integration tests | `FreeServicesHub.Agent.TestMe/Program.cs` | [Quality] | 3A-3E |

**Deliverable:** Agent installs as an OS service, registers with the hub, appears in the database, sends heartbeats every 30 seconds. Installer works on Windows and Linux.

---

## Phase 4: Dashboard UI

**Goal:** Web pages showing agent status in real time. Cards with color-coded thresholds. API key management UI. About/info panels on every page.

**Why fourth:** The data flows exist. Now we visualize them.

### Tasks (all parallelizable)

| Task | File(s) | Owner | Depends On |
|------|---------|-------|------------|
| **4A.** Agent Dashboard page (card grid, live updates via SignalR) | `FreeServicesHub.App.AgentDashboard.razor` | [Frontend] | Phase 2 |
| **4B.** Agent Detail page (heartbeat history, charts, logs) | `FreeServicesHub.App.AgentDetail.razor` | [Frontend] | Phase 2 |
| **4C.** API Key Management page (generate, revoke, one-time display) | `FreeServicesHub.App.ApiKeyManager.razor` | [Frontend] + [Security] | Phase 2 |
| **4D.** Registration Key Management page (generate bulk, view status) | `FreeServicesHub.App.RegistrationKeys.razor` | [Frontend] | Phase 2 |
| **4E.** AboutSection + InfoTip components (port from FreeExamples) | `FreeServicesHub.App.AboutSection.razor`, `FreeServicesHub.App.InfoTip.razor` | [Frontend] | Nothing |
| **4F.** Navigation menu items + icons | `Helpers.App.cs` additions (`MenuItemsApp`, `AppIcons`) | [Frontend] | Nothing |
| **4G.** Index.App.razor tie-in (embed dashboard on home page) | `Index.App.razor`, `FreeServicesHub.App.HomePage.razor` | [Frontend] | 4A |
| **4H.** Custom CSS (status cards, thresholds, charts) | `site.App.css` | [Frontend] | Nothing |

### Dashboard Card Theming

```
Agent Card Color Logic:
  Status: Online     -> border-success (green)
  Status: Warning    -> border-warning (yellow)  -- e.g., disk > 50%
  Status: Error      -> border-danger (red)      -- e.g., disk > 90%
  Status: Offline    -> border-secondary (gray)
  Status: Stale      -> border-warning (yellow)  -- no heartbeat > 2 min

Metric Thresholds (configurable per tenant):
  CPU:    Warning > 70%,  Error > 90%
  Memory: Warning > 70%,  Error > 90%
  Disk:   Warning > 50%,  Error > 90%
```

**Deliverable:** Dashboard shows agents as color-coded cards updating in real time. Click a card to see detail with heartbeat history charts. API keys manageable from UI. Every page has AboutSection explaining what it does.

---

## Phase 5: CI/CD Integration

**Goal:** Azure DevOps pipeline templates that automate the full deployment lifecycle (generate keys, shutdown, deploy, register).

**Why last:** Everything else must work manually before we automate it.

### Tasks (parallelizable)

| Task | File(s) | Owner | Depends On |
|------|---------|-------|------------|
| **5A.** Pipeline template for hub deployment | `Templates/deploy-hub-template.yml` | [Pipeline] | Phases 1-4 |
| **5B.** Pipeline template for agent deployment | `Templates/deploy-agent-template.yml` | [Pipeline] | Phase 3 |
| **5C.** Pipeline template for key generation + shutdown orchestration | `Templates/orchestrate-deploy-template.yml` | [Pipeline] + [Security] | Phases 1-2 |
| **5D.** Variable groups for agent config | Azure DevOps config | [Pipeline] | Nothing |
| **5E.** Nightly schedule trigger | Main pipeline YAML | [Pipeline] | 5A-5C |

**Deliverable:** Push to main triggers the full 12-step deployment lifecycle automatically. Nightly 2 AM schedule keeps everything fresh.

---

## Open Questions Resolved

Based on our research:

| Question (from 201) | Resolution | Rationale |
|---------------------|------------|-----------|
| What EF entities? | Agent, RegistrationKey, ApiClientToken, AgentHeartbeat | [Backend] mapped in Phase 1 |
| Agent local storage? | appsettings.json only, no local DB | [AgentDev] — FreeServices pattern, simpler. Token in config, logs in flat file as fallback |
| Progressive loading or polling? | Progressive loading (FreeCICD pattern) | [Architect] — proven in production, handles many agents well |
| CI/CD pipeline auth? | Admin-authenticated endpoint generates reg keys while hub is still up | [Security] — pipeline calls hub API with admin bearer token before shutdown |
| TestMe from day one? | Phase 3, after agent works | [Quality] — test what exists, not what's planned |

---

## Summary

**[Quality]:** Let me read back what we've agreed on.

Five phases, each produces something testable:

1. **Data Foundation** — 8 tasks, DTOs + EF + DataAccess + API. All four DTO/entity tasks run in parallel, then CRUD tasks in parallel, then endpoints.
2. **Hub Infrastructure** — 7 tasks, middleware + monitor service + SignalR + hooks. Middleware and monitor service run in parallel.
3. **Agent Service** — 7 tasks, worker + SignalR client + registration + installer + tests. Worker components in parallel, then installer and tests.
4. **Dashboard UI** — 8 tasks, all pages + components. Fully parallelizable — every page is independent.
5. **CI/CD Integration** — 5 tasks, pipeline templates. Parallelizable after all manual flows work.

**Total: 35 tasks across 5 phases.**

Phase 1 has the most dependencies (later tasks need earlier ones). Phases 4 tasks are the most parallelizable (8 independent pages/components).

**[Sanity]:** Phases 1-3 are the critical path. Phase 4 can start as soon as Phase 2 is done -- we don't need a real agent to build the dashboard, just the data model and some seed data. Phase 5 is nice-to-have until everything else works manually.

**[Architect]:** Agreed. And note: we estimated the file list in doc 202. That list maps directly to these tasks. Nothing here is invented beyond what we already planned.

**[JrDev]:** So I could pick up task 1A or 4E right now since they have no dependencies?

**[Architect]:** Exactly. 1A, 1B, 1C, 1D, 3D, 3E, 4E, 4F, 4H, and 5D all have zero dependencies. That's 10 tasks that can start immediately.

---

## Decisions

1. Five phases: Data Foundation, Hub Infrastructure, Agent Service, Dashboard UI, CI/CD Integration.
2. Hub before agent. Data model before everything.
3. Agent uses appsettings.json for token storage, not a local database.
4. Dashboard uses FreeCICD progressive loading pattern.
5. Pipeline authenticates with admin bearer token to generate registration keys.
6. TestMe project comes in Phase 3, after the agent works.
7. Heartbeat pruning keeps 24 hours of detail, rolls up to hourly after that.

## Next Steps

| Action | Owner | Priority |
|--------|-------|----------|
| Start Phase 1A-1D in parallel | [Backend] | P0 |
| Start Phase 4E-4F-4H (zero-dependency UI prep) | [Frontend] | P0 |
| Review this plan with CTO | [Architect] | P0 |
| Create Phase 1 feature branch | [Quality] | P0 |

---

*Created: 2026-04-08*
*Maintained by: [Quality]*
