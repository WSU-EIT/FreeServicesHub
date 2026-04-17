# 105 — AI Operations & Roleplay Methodology

> **Document ID:** 105  
> **Category:** Process / Team Operations  
> **Purpose:** To define how AI agents (like GitHub Copilot) interact with the developer (CTO) using the established Roleplay and Planning modes defined in Docs `000` through `003`.  
> **Audience:** Developers and AI Agents working in this workspace.  
> **Outcome:** 📖 A clear understanding of the AI personas, interaction modes, and how to execute robust planning sessions.

---

## 1. The Core AI Directives (`000_quickstart`)

The FreeServicesHub repository utilizes built-in AI directives to standardize how agents interact with the codebase. When an AI agent boots up in this workspace, it is expected to:

1. **Read `000`, `001`, and `002`** fully to understand the persona and documentation rules.
2. **Recognize Command Keywords:**
   * `"sitrep"`: Provide a status report of the current workspace state.
   * `"explore"`: Scan the project files, read the entry points, and summarize the architecture.
   * `"roleplay [topic]"`: Initiate a multi-persona design discussion.
   * `"plan [task]"`: Build an execution checklist for a specific feature.

By adhering to these commands, the AI acts not just as an autocomplete engine, but as an integrated technical team member.

---

## 2. The Two Modes of AI Engagement (`001_roleplay`)

When approaching a task in FreeServicesHub, the work size dictates the AI mode used.

| Task Size | Definition | Recommended Mode |
|-----------|------------|------------------|
| **Tiny** | Typo fixes, comment updates. | Just generate the code. |
| **Small** | Adding a single field to a UI component or EF model. | Direct instruction (Standard Copilot interaction). |
| **Medium** | Creating a new API endpoint cluster or a new full-page Blazor component. | **Planning Mode** (`"plan [task]"`) |
| **Large** | Designing the `AgentWorkerService` architecture or planning a new database schema. | **Discussion Mode** (`"roleplay [topic]"`) |

---

## 3. Discussion Mode (The "Roleplay")

When the human developer (acting as the **CTO**) types `"roleplay [topic]"`, the AI agent splits its persona into a virtual focus group to examine the problem from multiple architectural angles before writing code.

### The Standard Virtual Team
* **[Architect]**: Focuses on system design, boundaries, and how the change affects the `.App.` hook pattern (as defined in `101` and `006`).
* **[Backend]**: Analyzes the Database (EF Core) impact, the Three-Endpoint CRUD API requirements (`007`), and data contracts.
* **[Frontend]**: Focuses on the Blazor UI, tenant-routing, and integration with `Helpers.js`.
* **[Quality]**: Asks how this will be tested (e.g., suggesting a `FreeTools` Playwright test).
* **[Sanity / JrDev]**: Acts as the grounding force, asking "Are we overcomplicating this?" or "Why don't we just use the existing `SaveMany` endpoint?"

### The Flow
1. The **Architect** frames the problem.
2. **Specialists** debate the approach.
3. The AI hits a decision point and **Pauses for the CTO**.

```ascii
 ┌─────────────┐        ┌──────────────┐         ┌────────────┐
 │ AI Personas │        │ Conflict /   │         │ Output &   │
 │ (Architect, ├───────►│ Options Gen. ├────────►│ PAUSE      │
 │  Backend)   │        │              │         │            │
 └─────────────┘        └──────────────┘         └─────┬──────┘
                                                       │
                                                       ▼
 ┌─────────────┐        ┌──────────────┐         ┌────────────┐
 │ AI Resumes  │        │ Human CTO    │         │ CTO Reads  │
 │ & Finalizes │◄───────┤ Selects Path │◄────────┤ Options 1/2│
 │ Document    │        │              │         │            │
 └─────────────┘        └──────────────┘         └────────────┘
```

4. The AI outputs:
   ```markdown
   ⏸️ **CTO Input Needed**
   Options:
   1. Build a new SignalR Hub.
   2. Piggyback on the existing Tenant Hub.
   @CTO - Which way?
   ```
5. The human replies, and the AI finalizes the design document.

---

## 4. Planning Mode (The "Execution Checklist")

Once a design is settled (or if the task is a "Medium" sized known entity), the developer uses `"plan [task]"`.

Instead of a debate, the AI pulls from `003_templates.md` and generates a rigid execution checklist tailored to the FreeCRM architecture.

A good FreeServicesHub plan always includes:
* **UI/Blazor Updates**: Which `{Project}.App.{Feature}.razor` files are changing?
* **API/Controller Updates**: Are we adding DTOs to `DataObjects.App.cs`? Are we mapping them in `DataAccess.App.cs`?
* **Testing Requirements**: Does `BrowserSnapshot` need to be updated? 

By using these modes, the developer ensures that the AI respects the strict architectural boundaries (`101` & `104`) of the FreeCRM ecosystem rather than hallucinating standard, non-compliant .NET boilerplate.

---

## 🤖 AI Analysis

> **Agent:** `4/15/2026/sonnet/4.6`

**Strengths observed:**
- The two-mode framework (Discussion vs. Planning) is the right abstraction. It correctly mirrors how real engineering teams operate: explore first, execute second.
- The `⏸️ CTO Input Needed` template block is excellent process design. It forces the AI to make its reasoning transparent and gives the human a clear decision point rather than having the AI make silent assumptions.
- The task-size table is highly practical. Many AI-assisted sessions go wrong because the developer picks the wrong mode (trying to Plan something that should be Discussed, or Discussing something that should just be done).

**Gaps observed:**
- The document describes the AI's roles (Architect, Backend, etc.) but doesn't say how many rounds of discussion typically happen before a pause. Without a cadence, discussions can run indefinitely. Adding a rule like "maximum 2 rounds before mandatory CTO pause" would improve efficiency.
- There is no mention of what happens when the AI produces a design document at the end of a roleplay. Where does it get saved? What naming convention should it use? (`003_templates.md` has the answer, but this doc should reference it explicitly.)
- The "Planning Mode" section doesn't specify where the output checklist should be stored. A planning session should produce a new doc (e.g., `108_plan_agent_auth.md`) following the `002_docsguide.md` numbering convention.

**Recommendation:**
- Add a Section 5 that defines the output artifacts for each mode: Discussion → new `NNN_meeting_*.md` doc; Planning → new `NNN_plan_*.md` doc. This closes the loop between the AI's work product and the repository's documentation system.