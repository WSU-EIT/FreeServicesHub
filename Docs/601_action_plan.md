# 601 — Master Action Plan

> **Document ID:** 601  
> **Category:** Planning / Execution  
> **Purpose:** Top-level action plan consolidating all findings from 106 and the phased roadmap from 107 into a severity-rated, sequenced execution schedule.  
> **Audience:** Developers, AI Agents, PM stakeholders.  
> **Outcome:** 📖 A single source of truth for what to build, in what order, and why.  
> **Phase Docs:** [602](602_phase1_data_contracts.md) · [603](603_phase2_agent_security.md) · [604](604_phase3_dashboard_ui.md)

---

## 1. Project Health Assessment

The codebase exploration (performed by Opus sub-agent against the live workspace) reveals the project is **far more complete than initial documentation suggested**. The following are already production-grade:

| Component | State | Evidence |
|-----------|-------|----------|
| Hub Web Server | ✅ Complete | `Program.cs` — 250+ lines, multi-auth, SignalR, plugins |
| Agent Worker Service | ✅ Complete | `AgentWorkerService.cs` — 300+ lines, heartbeat, snapshots |
| Agent Installer | ✅ Complete | `Program.cs` — 200+ lines, dual-mode CLI/interactive |
| Aspire AppHost | ✅ Complete | `Program.cs` — Hub + Agent wired, ports 7271/5111, WaitFor |
| EF Models | ✅ Complete | Multi-DB: SQL Server, MySQL, PostgreSQL, SQLite, InMemory |
| Data Access Layer | ✅ Complete | 40+ files, cascade deletes, Agent-specific queries |
| Blazor Client | ✅ Complete | MudBlazor, Radzen, Monaco, AgentDashboard pages |
| Hook Files | ✅ Complete | All 7 `.App.cs` exist and are actively populated |
| Integration Tests | ✅ Complete | xUnit, Mvc.Testing, SignalR client tests |
| Test Harness | ✅ Complete | TestMe CLI with 3+ test scenarios |

### Correction to Doc 106

Doc 106 identified four gaps (A–D). Real code inspection shows:

| Gap | 106 Assessment | Actual State | Revised Severity |
|-----|----------------|--------------|------------------|
| **A. Job/Agent DTOs** | Missing | `FreeServicesHub.App.DataObjects.Agents.cs` EXISTS with Agent DTO; `AgentSettings.cs`, `ApiKeys.cs`, `BackgroundServiceLogs.cs` all present | 🟡 Partial — need Job queue DTO only |
| **B. API Auth Boundaries** | Undefined | `ApiKeyMiddleware.cs` EXISTS and is wired in `Program.App.cs` via `UseMiddleware<ApiKeyMiddleware>()` | 🟢 Mostly done — verify coverage |
| **C. AppHost Disconnects** | Not wired | AppHost `Program.cs` already has `AddProject<Agent>` + `WaitFor(hub)` + environment injection | ✅ Resolved — no work needed |
| **D. Playwright E2E** | Not mapped | FreeTools imported but no Hub-specific scripts | 🟡 Needed for CI/CD pipeline |

```ascii
  ╔══════════════════════════════════════════════════════════════╗
  ║              REVISED GAP SEVERITY MATRIX                    ║
  ╠══════════════════════════════════════════════════════════════╣
  ║                                                              ║
  ║  🔴 BLOCKER    ──  (none — no critical blockers found)       ║
  ║                                                              ║
  ║  🟡 HIGH       ──  A. Job Queue DTO (partial)                ║
  ║                     D. Playwright E2E scripts (new)          ║
  ║                                                              ║
  ║  🟢 LOW        ──  B. Auth coverage audit (verify only)      ║
  ║                     SignalR tenant-scope audit                ║
  ║                                                              ║
  ║  ✅ RESOLVED   ──  C. AppHost wiring (already done)          ║
  ║                                                              ║
  ╚══════════════════════════════════════════════════════════════╝
```

---

## 2. Revised Three-Phase Plan

Given the actual codebase state, the original phases from 107 are recalibrated:

```ascii
  ┌─────────────────────────────────────────────────────────────────┐
  │                    EXECUTION TIMELINE                           │
  │                                                                 │
  │  Phase 1          Phase 2               Phase 3                 │
  │  DATA CONTRACTS   AGENT HARDENING       DASHBOARD & E2E        │
  │  ──────────────   ──────────────────    ─────────────────       │
  │  │ Job Queue DTO│  │ Auth audit      │  │ Dashboard polish │    │
  │  │ EF Migration │  │ Agent reconnect │  │ Provisioning wiz │   │
  │  │ CRUD wiring  │  │ Signal scoping  │  │ Playwright E2E   │   │
  │  └──────────────┘  └────────────────-┘  └──────────────────┘   │
  │       ▼                   ▼                      ▼              │
  │  ~1-2 days           ~1-2 days              ~2-3 days           │
  │                                                                 │
  │  DEPENDENCY CHAIN:  Phase 1 ──► Phase 2 ──► Phase 3            │
  │  (Phase 2 needs DTOs from 1; Phase 3 needs auth from 2)        │
  └─────────────────────────────────────────────────────────────────┘
```

### Phase 1 — Data Contracts (Detail: [602](602_phase1_data_contracts.md))

**Goal:** Define the Job Queue DTO, map it through all layers, run first migration.

| Task | Severity | Parallel? | File(s) |
|------|----------|-----------|---------|
| Define `HubJob` DTO | 🟡 HIGH | Independent | `DataObjects/FreeServicesHub.App.DataObjects.*.cs` |
| Add EF entity + migration | 🟡 HIGH | After DTO | `EFModels/` |
| Wire DataAccess CRUD | 🟡 HIGH | After EF | `DataAccess/FreeServicesHub.App.DataAccess.*.cs` |
| Wire DataController endpoints | 🟡 HIGH | After DA | `Controllers/DataController.App.cs` |
| Unit test DTO round-trip | 🟢 LOW | After DA | `Tests.Integration/` |

### Phase 2 — Agent Hardening & Security (Detail: [603](603_phase2_agent_security.md))

**Goal:** Audit existing auth, harden reconnection, validate SignalR scoping.

| Task | Severity | Parallel? | File(s) |
|------|----------|-----------|---------|
| Audit `ApiKeyMiddleware` | 🟢 LOW | Independent | `FreeServicesHub.App.ApiKeyMiddleware.cs` |
| Audit `Authenticate_App()` | 🟢 LOW | Parallel w/ above | `DataAccess.Authenticate.cs` |
| Validate SignalR tenant scope | 🟡 HIGH | Independent | `DataAccess.SignalR.cs`, `Hubs/` |
| Test Agent reconnection flow | 🟡 HIGH | Independent | `AgentWorkerService.cs` |
| Wire Job fetch into Agent loop | 🟡 HIGH | After Phase 1 | `AgentWorkerService.cs` |

### Phase 3 — Dashboard & E2E (Detail: [604](604_phase3_dashboard_ui.md))

**Goal:** Polish the existing Dashboard, build the provisioning wizard, create Playwright scripts.

| Task | Severity | Parallel? | File(s) |
|------|----------|-----------|---------|
| Dashboard Job Queue panel | 🟡 HIGH | Independent | `Client/Pages/AgentDashboard.razor` |
| Agent Provisioning Wizard | 🟡 HIGH | Independent | `Client/Pages/AgentManagement.razor` |
| SignalR job-complete reactivity | 🟡 HIGH | After dashboard | `Client/Helpers.App.cs` |
| Playwright Hub E2E scripts | 🟡 HIGH | Independent | `FreeTools/` |
| CI/CD pipeline integration | 🟢 LOW | After Playwright | `Pipelines/` |

---

## 3. Project Dependency Chain

This ASCII diagram shows how data flows through the solution layers. Every phase touches multiple projects — this chain dictates the build order within each phase.

```ascii
  ┌─────────────────────────────────────────────────────────────────────┐
  │              SOLUTION PROJECT DEPENDENCY CHAIN                      │
  │                                                                     │
  │  ┌──────────────┐                                                   │
  │  │  EFModels    │  Entity Framework entities + DbContext             │
  │  │  (DB Schema) │  SQL Server / MySQL / PostgreSQL / SQLite         │
  │  └──────┬───────┘                                                   │
  │         │ references                                                │
  │         ▼                                                           │
  │  ┌──────────────┐     ┌──────────────┐                              │
  │  │ DataObjects  │◄────│   Plugins    │  Runtime C# compilation      │
  │  │  (DTOs)      │     │  (Dynamic)   │  plugin system               │
  │  └──────┬───────┘     └──────┬───────┘                              │
  │         │ references          │ references                          │
  │         ▼                     ▼                                     │
  │  ┌─────────────────────────────────┐                                │
  │  │         DataAccess              │  Business logic, queries,      │
  │  │  (EFModels + DataObjects +      │  SignalR broadcast,            │
  │  │   Plugins)                      │  encryption, JWT, LDAP        │
  │  └──────────────┬──────────────────┘                                │
  │                 │ references                                        │
  │         ┌───────┴───────┐                                           │
  │         ▼               ▼                                           │
  │  ┌──────────────┐  ┌──────────────┐                                 │
  │  │   Client     │  │  Hub Server  │  API controllers, SignalR hubs  │
  │  │  (Blazor)    │  │  (ASP.NET)   │  auth middleware, hosted svcs   │
  │  │  DataObjects │  │  DataAccess  │                                 │
  │  └──────────────┘  └──────┬───────┘                                 │
  │                           │                                         │
  │         ┌─────────────────┼──────────────────┐                      │
  │         ▼                 ▼                  ▼                       │
  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐               │
  │  │   AppHost    │  │    Agent     │  │  Tests.Int   │               │
  │  │  (Aspire)    │  │  (Worker)    │  │  (xUnit)     │               │
  │  │  Hub + Agent │  │  HTTP/SignalR│  │  Hub+DataObj  │              │
  │  └──────────────┘  └──────────────┘  └──────────────┘               │
  │                                                                     │
  │  BUILD ORDER (within a phase):                                      │
  │  EFModels ──► DataObjects ──► Plugins ──► DataAccess ──► Client     │
  │                                                    └──► Server      │
  │                                                         └──► All    │
  └─────────────────────────────────────────────────────────────────────┘
```

---

## 4. Cross-Reference Index

| Phase Doc | Primary Concern | Key Files Modified | Tests Required |
|-----------|----------------|-------------------|----------------|
| [602](602_phase1_data_contracts.md) | Job Queue DTO + EF + CRUD | DataObjects, EFModels, DataAccess, DataController | DTO serialization, DB round-trip |
| [603](603_phase2_agent_security.md) | Auth audit + Agent job fetch | ApiKeyMiddleware, AgentWorkerService, SignalR | Auth header rejection, tenant isolation |
| [604](604_phase3_dashboard_ui.md) | UI polish + Wizard + E2E | AgentDashboard.razor, Helpers.App.cs, FreeTools | Visual regression, SignalR push |

### Upstream Documentation

| Doc | Relevance |
|-----|-----------|
| [004_styleguide.md](004_styleguide.md) | `.App.` file naming + never modify base files |
| [006_architecture.dataobjects.app.md](006_architecture.dataobjects.app.md) | DTO hook pattern |
| [006_architecture.dataaccess.app.md](006_architecture.dataaccess.app.md) | DataAccess hook pattern |
| [006_architecture.datacontroller.app.md](006_architecture.datacontroller.app.md) | API endpoint wiring |
| [007_patterns.crud_api.md](007_patterns.crud_api.md) | Three-Endpoint CRUD pattern |
| [007_patterns.signalr.md](007_patterns.signalr.md) | SignalR tenant-scoped messaging |
| [008_components.wizard.md](008_components.wizard.md) | Multi-step wizard pattern |
| [008_components.bootstrap_patterns.md](008_components.bootstrap_patterns.md) | Card/Grid layout |

---

## 5. Pass / Fail Criteria (Global)

The entire action plan is considered **PASS** when:

- [ ] `HubJob` DTO exists in DataObjects and serializes correctly
- [ ] EF migration runs without error on at least one DB provider
- [ ] `GetMany`/`SaveMany`/`DeleteMany` endpoints work for `HubJob`
- [ ] `ApiKeyMiddleware` rejects requests without a valid key (returns 401)
- [ ] SignalR updates are scoped to TenantId (no cross-tenant leakage)
- [ ] Agent worker fetches and acknowledges jobs via HTTP
- [ ] Dashboard displays Job Queue with live SignalR updates
- [ ] Provisioning Wizard generates a valid Agent registration key
- [ ] At least one Playwright E2E script navigates the Hub dashboard
- [ ] All integration tests pass (`dotnet test`)

---

## 6. Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| EF migration conflicts with FreeCRM base | DB schema corruption | Use separate migration history table; test on InMemory first |
| Agent polling overloads Hub API | Performance degradation | Configurable interval (default 30s); Agent-side backoff |
| SignalR tenant leak | Security / data breach | Explicit `TenantId` filter in every hub method; integration test |
| Playwright flaky on CI | False build failures | Retry policy + headless browser pinned version |
| Plugin system interference | Unexpected behavior | Isolate plugin tests; don't load plugins during Agent E2E |

---

## 🤖 AI Analysis

> **Agent:** `4/15/2026/opus/4.6`

**Key Correction:** The most significant finding from the live codebase exploration is that **Gap C (AppHost Disconnects) is already resolved** — `FreeServicesHub.AppHost/Program.cs` has `AddProject<Agent>`, `WaitFor(hub)`, and environment variable injection for `Agent__HubUrl` and `Agent__RegistrationKey`. Additionally, **Gap B (API Auth) is mostly implemented** via `ApiKeyMiddleware` already wired into the request pipeline. The original 106 analysis was based on documentation review, not code inspection.

**Revised priorities:** With blockers eliminated, the remaining work is incremental enhancement rather than foundational construction. Phase 1 (Job Queue DTO) is the only genuinely missing data contract. Phases 2 and 3 shift from "build from scratch" to "audit, harden, and polish."

**Parallel opportunity:** Within Phase 1, the DTO definition and EF entity can be developed simultaneously by different developers (or AI agents) since they converge only at the DataAccess layer. Within Phase 3, the Playwright scripts and Dashboard UI are fully independent work streams.
