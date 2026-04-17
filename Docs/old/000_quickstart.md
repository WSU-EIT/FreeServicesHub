# 000 — Quickstart: Get Running Locally

> **Document ID:** 000
> **Category:** Quickstart
> **Purpose:** Get a new dev from zero to running locally, plus AI assistant commands.
> **Audience:** Devs, contributors, AI agents.
> **Outcome:** ✅ Working local run + AI ready to assist.

---

# AI AGENT COMMANDS

**Match the user's request:**

| User Says | Action |
|-----------|--------|
| `"sitrep"` / `"status"` | Run SITREP (see below) |
| `"explore"` / `"deep dive"` | Run EXPLORE (see below) |
| `"roleplay [topic]"` | Discussion mode (see doc 001) |
| `"plan [feature/bug]"` | Planning mode (see doc 001) |
| `"build"` / `"test"` | Run command, report results |
| `"menu"` / `"help"` | Show command table |
| *(anything else)* | Run STARTUP first, then address |

---

## AI Startup

**Do this at the start of every conversation:**

1. **READ IN FULL:** `Docs/000_quickstart.md` (this file)
2. **READ IN FULL:** `Docs/001_roleplay.md` (discussion + planning)
3. **READ IN FULL:** `Docs/002_docsguide.md` (standards)
4. **SKIM:** `Docs/003_templates.md` (grab templates as needed)
5. **SCAN:** Any other docs — read headers to understand purpose

**Confirm:**
```
Startup complete.
  Read: 000, 001, 002
  Skimmed: 003 (templates)
  Scanned: [X] other docs
  Ready to: [user's request]
```

### Reading Modes

| Instruction | Meaning |
|-------------|---------|
| **READ IN FULL** | Every line, don't skip |
| **SKIM** | Get the gist: topic, decisions, timeline |
| **SCAN** | Headers only, note what exists |

---

## Sitrep Format

When user says "sitrep" / "status":

```
## Sitrep: FreeServicesHub

**As of:** [date]
**Purpose:** Self-hosted service agent platform built on FreeCRM

**Current:** [from tracker doc or "no active sprint"]
- Task 1: status
- Task 2: status

**Recent:** [last completed work]
**Blocked:** [anything stuck]

Commands: `build` / `test` / `explore` / `plan [thing]`
```

---

## Explore Sequence

When user says "explore" / "deep dive":

1. **READ IN FULL:** All docs in `Docs/` folder
2. **SCAN:** Project files (`.csproj`, `.slnx`)
3. **READ:** `FreeServicesHub/Program.cs` (server entry point)
4. **SAMPLE:** One model, one endpoint, one UI component
5. **READ:** Reference docs in `Examples/FreeTools/FreeExamples/Docs/`
6. **OUTPUT:** Summary of architecture, tech, and current state

---

# HUMAN: START HERE

---

## What is This Project?

**Name:** FreeServicesHub
**One-liner:** Self-hosted service agent platform with API key management, built on FreeCRM
**Stack:** Blazor + C# + .NET 10
**Fork origin:** FreeCRM with `keep:Tags`, renamed to FreeServicesHub

---

## FreeCRM Ecosystem

This project is part of the FreeCRM ecosystem:

| Project | Status | Description |
|---------|--------|-------------|
| **FreeCRM-main** | Public | Base template — authoritative source for all patterns |
| **FreeCICD** | Public | CI/CD pipeline automation — Azure DevOps + YML pipelines |
| **FreeGLBA** | Public | GLBA compliance — API key management, about panels, a11y |
| **FreeServices** | Public | Cross-platform service installer + worker pattern |
| **FreeExamples** | Public | Example extension — houses comprehensive FreeCRM docs |
| **FreeServicesHub** | Public | **This project** — service agent platform |

---

## MANDATORY: File Naming Convention

**All new/custom files MUST use this pattern:**

```
{ProjectName}.App.{Feature}.{OptionalSubFeature}.{Extension}
```

| Type | Example |
|------|---------|
| New page | `FreeServicesHub.App.AgentManager.razor` |
| Partial file | `FreeServicesHub.App.AgentManager.State.cs` |
| New entity | `FreeServicesHub.App.Agent.cs` |
| New DTOs | `FreeServicesHub.App.DataObjects.Agents.cs` |
| Base extension | `DataController.App.FreeServicesHub.cs` |

**Why this matters:**
- Find all your code instantly: `find . -name "FreeServicesHub.App.*"`
- Safe during FreeCRM framework updates
- Clear separation of base vs custom

**Blazor components:** Dots become underscores in class names:
- File: `FreeServicesHub.App.AgentManager.razor`
- Class: `FreeServicesHub_App_AgentManager`
- Usage: `<FreeServicesHub_App_AgentManager />`

**Full details:** See `004_styleguide.md` (from FreeExamples Docs)

---

## Project Structure

```
FreeServicesHub/
  FreeServicesHub/                    Server (API, auth, SignalR hub)
  FreeServicesHub.Client/             Blazor WASM client
  FreeServicesHub.DataAccess/         Data access layer (EF Core)
  FreeServicesHub.DataObjects/        DTOs and data contracts
  FreeServicesHub.EFModels/           Entity Framework models
  FreeServicesHub.Plugins/            Plugin subsystem
  Docs/                               Project documentation (this folder)
  FreeServicesHub.slnx                Solution file
```

### Companion Projects (Planned)

| Project | Purpose |
|---------|---------|
| **FreeServicesHub.Agent** | Cross-platform service agent (installed on remote machines) |
| **FreeServicesHub.Agent.Installer** | Agent installer CLI (configure/remove/manage) |
| **FreeServicesHub.Agent.TestMe** | Agent integration tests |

---

## Prerequisites

| Required | Notes |
|----------|-------|
| .NET 10 SDK | `dotnet --version` should show 10.x |
| Git | Latest |
| IDE | VS / Rider / VS Code |

---

## Setup

```bash
cd FreeServicesHub
dotnet restore FreeServicesHub.slnx
dotnet build FreeServicesHub.slnx
```

## Running Locally

```bash
dotnet run --project FreeServicesHub/FreeServicesHub
```

Default: InMemory database, admin:admin login, http://localhost:5000

### Smoke Check

- [ ] App loads in browser
- [ ] Login with admin:admin
- [ ] Tags settings page accessible

---

## Default Credentials

| Username | Password | Role | Tenant |
|----------|----------|------|--------|
| `admin` | `admin` | Admin | admin (all tenants) |
| `test` | `test` | User | tenant1 |

---

## Key Conventions

### API Endpoint Pattern

Three endpoints per entity — not individual GET/POST/PUT/DELETE:

| Endpoint | Accepts | Behavior |
|----------|---------|----------|
| **GetMany** | `List<Guid>?` | `null`/empty = all; IDs = filtered |
| **SaveMany** | `List<T>` | PK exists = update; empty/new PK = insert |
| **DeleteMany** | `List<Guid>` | Must provide IDs; `null`/empty = error |

### Extension Hooks

All customization goes in `.App.` files:

| Base File | Extension | Purpose |
|-----------|-----------|---------|
| `DataAccess.cs` | `DataAccess.App.cs` | Custom data methods |
| `DataController.cs` | `DataController.App.cs` | Custom API endpoints |
| `DataObjects.cs` | `DataObjects.App.cs` | Custom DTOs |
| `Program.cs` | `Program.App.cs` | Custom DI/middleware |
| `MainLayout.razor` | `MainLayout.App.razor` | Custom layout |

---

## Next Steps

1. **Read** docs 001-002 for team patterns and standards
2. **Run** `sitrep` to see current state
3. **Use** `plan [feature]` before starting work

---

*Created: 2026-04-08*
*Maintained by: [Quality]*
