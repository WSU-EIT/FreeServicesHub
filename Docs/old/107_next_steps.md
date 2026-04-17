# 107 — Next Steps & Execution Recommendations

> **Document ID:** 107  
> **Category:** Planning  
> **Purpose:** Actionable roadmap for advancing the FreeServicesHub based on the analysis in Doc 106.  
> **Audience:** Developers and AI Agents (acting in Execution Mode).  
> **Outcome:** 📖 A prioritized, chronological build sequence.

---

## Phase 1: Define the Data Contract

Before we build UI or Worker logic, we must define how they talk.

```ascii
     [HUB API]                      [AGENT]
  ┌─────────────┐                ┌───────────┐
  │ SaveMany()  │◄── Job DTO ────┤ Execute() │
  │ GetMany()   ├── Status DTO ─►│ Heartbeat │
  └─────────────┘                └───────────┘
```

1. **Create `DataObjects.App.cs` definitions:**
   * `HubAgent`: Tracks registered agents.
   * `HubJob`: A work item queued for an agent.
2. **Setup EF Models:** Map these to `FreeServicesHub.EFModels` and run the initial migration.
3. **Map `DataAccess.App.cs`:** Wire up `GetDataApp` and `SaveDataApp` for the new models.

---

## Phase 2: Agent Wire-up & Security

1. **Implement `DataController.App.cs` Auth Hook:**
   * Override `Authenticate_App()` to accept an `Agent-Api-Key` HTTP Header so the Worker Service can talk to the Hub safely.
2. **Build the Polling Loop:**
   * Update `AgentWorkerService.cs` to use `Helpers.GetOrPost<T>` to fetch assigned `HubJob`s every 10 seconds.
3. **Update `.AppHost`:** Wire `FreeServicesHub.Agent` into the Aspire AppHost for local F5 debugging.

---

## Phase 3: Hub Dashboard UI

1. **Create `FreeServicesHub.App.Dashboard.razor`:**
   * Build a Bootstrap 5 `Card` layout showing Connected Agents and Active Jobs.
2. **Implement SignalR Reactivity:**
   * Hook into `ProcessSignalRUpdateApp` so when an Agent completes a job, the Dashboard card turns green instantly.
3. **Agent Provisioning Wizard:**
   * Use the `Wizard` pattern from `008_components.wizard.md` to let a user generate a new Agent installation key.

---

## AI Agent Execution Plan

When ready to begin, use the following commands:
* `plan [Phase 1: Data Contracts]`
* `plan [Phase 2: Agent Auth Hook]` 
This will instruct the AI to build out the exact files required!

---

## 🤖 AI Analysis

> **Agent:** `4/15/2026/sonnet/4.6`

**Strengths observed:**
- The three-phase sequencing is correct. Data Contracts first, then Security, then UI is the only safe order. Building the UI or Agent polling loop before the DTOs exist would require painful rewrites.
- The ASCII diagram showing Hub API ↔ Agent interaction (Job DTOs flowing in, Status DTOs flowing out) is the clearest articulation of the Agent communication contract anywhere in the docs.
- Recommending the `plan [Phase...]` commands at the bottom closes the loop with `105_ai_roleplay_methodology.md` — it shows the developer exactly how to engage the AI for each phase.

**Gaps observed:**
- Phase 1 says "run the initial migration" but doesn't specify the EF Core CLI command, which project to run it against, or how to avoid colliding with the base FreeCRM migration history. This is non-trivial and should be spelled out.
- Phase 2 says "implement `Authenticate_App()`" but doesn't describe how an API key gets provisioned for a new Agent — i.e., who generates the key, where it's stored (database vs. config), and how the Agent knows to include it in the `Agent-Api-Key` header.
- Phase 3 is the most underspecified. "Bootstrap 5 Card layout" and "SignalR Reactivity" are named but not designed. At minimum, Phase 3 should reference `008_components.bootstrap_patterns.md` and `007_patterns.signalr.md` as the implementation guides.

**Recommendation:**
- Before beginning Phase 1, run a `roleplay [Agent Data Contract Design]` session to formally spec the `HubAgent` and `HubJob` DTOs with the virtual team. This will surface edge cases (e.g., what happens when an Agent goes offline mid-job?) before any code is written.