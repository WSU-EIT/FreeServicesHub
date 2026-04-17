# 101 — Workspace Summary & Core Concepts Index

> **Document ID:** 101  
> **Category:** Guide / Deep Dive Summary  
> **Purpose:** A high-level summary and index of the most important architectural pieces in the FreeServicesHub ecosystem and *why* they are designed this way.  
> **Audience:** Developers new to the repository needing a conceptual roadmap.  
> **Outcome:** 📖 Understand the "why" behind the structure before writing code.

---

## Document Index

| Section | Description |
|---------|-------------|
| [1. The FreeCRM Base & App Hooks](#1-the-freecrm-base--app-hooks-isolation--upgradability) | Why we use the `.App.` partial class extension pattern. |
| [2. The Three-Endpoint CRUD Pattern](#2-the-three-endpoint-crud-pattern-api-simplicity) | Why we don't use standard REST (GET, PUT, POST, DELETE). |
| [3. Global Helpers & Utilities](#3-global-helpers--utilities-dry-principle) | Why we wrap .NET primitives and navigation. |
| [4. Background Workers & Agents](#4-background-workers--agents-decoupling-work) | Why `FreeServicesHub.Agent` exists separately from the Web UI. |
| [5. FreeTools Automations](#5-freetools-automations-confidence-at-scale) | Why we use custom Playwright CLI tools instead of just unit tests. |

---

## 1. The FreeCRM Base & App Hooks: Isolation & Upgradability

**What it is:** The codebase is rigidly split into the "Base Framework" (FreeCRM) and "Custom Projects" (FreeServicesHub, FreeCICD, etc.). You **never** modify base FreeCRM files directly. Instead, you use "Hook" files containing `.App.` in the name (e.g., `Program.App.cs`, `DataController.App.cs`).

**Why it exists:** 
* **Effortless Upgrades:** If you modify `Program.cs` directly, every time the core FreeCRM framework receives an update, you will face complex Git merge conflicts.
* **Clear Boundaries:** By using partial classes and `.App.` injection methods, custom app logic is strictly quarantined. We can swap out the underlying CRM engine without rewriting the custom business logic.

**How it looks in practice:**
```csharp
// Framework File: Program.cs (DO NOT TOUCH)
var builder = WebApplication.CreateBuilder(args);
builder = AppModifyBuilderStart(builder); // Calls the hook!

// Hook File: Program.App.cs (SHIPPED WITH CRM)
public static WebApplicationBuilder AppModifyBuilderStart(WebApplicationBuilder builder) {
    // Add one line to call YOUR custom code
    return FreeServicesHub.App.Program.Init(builder);
}

// Custom File: FreeServicesHub.App.Program.cs (YOUR CODE)
public static WebApplicationBuilder Init(WebApplicationBuilder builder) {
    builder.Services.AddSingleton<MyAgentCoordinator>();
    return builder;
}
```

*Reference:* Read `006_architecture.extension_hooks.md` for the master rulebook on this pattern.

---

## 2. The Three-Endpoint CRUD Pattern: API Simplicity

**What it is:** Instead of building 6+ classic REST endpoints for every data entity (e.g., `GetAll`, `GetById`, `Post`, `Put`, `Delete`), every entity gets exactly three endpoints:
1. `GetMany(List<Guid>? ids)`
2. `SaveMany(List<T> items)`
3. `DeleteMany(List<Guid> ids)`

**Why it exists:**
* **Batch Processing by Default:** Minimizes network chatter. If the UI needs to save 5 rows, it's one API call, not 5.
* **Unified Insert/Update:** `SaveMany` handles both logic paths. If the item has an empty/new Guid, it inserts. If it matches an existing Guid, it updates.
* **Simplified Client Logic:** The Blazor components don't need to decide whether to call a PUT or POST endpoint depending on whether the entity is new or existing.

**Implementation Example:**
```csharp
// Single API POST for getting multiple rows efficiently. 
[HttpPost]
[Authorize]
[Route("~/api/Data/GetSampleItems")]
public async Task<ActionResult<List<DataObjects.ExampleItem>>> GetSampleItems(List<Guid>? ids)
{
    // Return all if ids == null, else filter nicely...
}
```

*Reference:* Read `007_patterns.crud_api.md` to see exactly how to structure your DataAccess methods and Controller actions to match this convention.

---

## 3. Global Helpers & Utilities: DRY Principle

**What it is:** A global static `Helpers` class is initialized in the `MainLayout` and is used pervasively for API calls (`GetOrPost<T>`), UI navigation (`NavigateTo`), Language translations (`Text()`), and data formatting.

**Why it exists:**
* **Tenant Awareness:** In a multi-tenant system, URLs change (`/tenant1/Users` vs `/tenant2/Users`). `Helpers.NavigateTo` automatically ensures the user stays within their correct tenant's URL route securely.
* **Boilerplate Reduction:** `GetOrPost` automatically handles JSON serialization, authentication token injection, and error trapping.
* **Consistency:** It enforces a single way to do common tasks across thousands of UI lines.

```csharp
// BAD: Manual HTTP Client calls and raw navigation
var client = ClientFactory.CreateClient();
client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
var response = await client.GetFromJsonAsync<MyThing>($"/api/GetThing/{id}");
NavigationManager.NavigateTo($"/myTenant/Thing/{id}");

// GOOD: FreeCRM Helpers
var thing = await Helpers.GetOrPost<MyThing>("api/Data/GetThing", new { id });
Helpers.NavigateTo($"Thing/{thing.Id}"); // Auto-injects tenant prefix
```

*Reference:* Read `007_patterns.helpers.md` for the full method catalog.

---

## 4. Background Workers & Agents: Decoupling Work

**What it is:** The workspace contains standard web projects (`FreeServicesHub.Client`, `FreeServicesHub/`) alongside distributed worker projects (`FreeServicesHub.Agent`, `FreeServicesHub.Agent.Installer`).

**Why it exists:**
* **Non-Blocking UI:** Web servers (IIS/Kestrel) are designed for fast request/response cycles. Heavy tasks (polling APIs, generating large exports, or executing CLI tools) are pushed to the background `Agent` to keep the UI snappy.
* **Resilience:** The Agent runs as a persistent service. If the Web UI gracefully restarts, the Agent continues processing its queues uninterrupted.

```ascii
  ┌─────────────────┐       SignalR / HTTP      ┌──────────────────┐
  │                 │ ◄───────────────────────► │                  │
  │  FreeServices   │                           │ FreeServicesHub  │
  │  Web UI + API   │ ◄────── Data Sync ──────► │ .Agent (Worker)  │
  │                 │                           │                  │
  └────────┬────────┘                           └──────────────────┘
           │
           ▼
     SQL Database
```

*Reference:* Deep dive into how these interact in `103_freeserviceshub_unique.md`.

---

## 5. FreeTools Automations: Confidence at Scale

**What it is:** A suite of CLI programs (e.g., `BrowserSnapshot`, `EndpointMapper`, `AccessibilityScanner`) built using `Microsoft.Playwright`.

**Why it exists:**
* **Visual Regression & Auditing:** `BrowserSnapshot` crawls the entire application to take network-idle screenshots. This is crucial for verifying that base framework CSS/UI updates didn't accidentally break custom app screens.
* **Security & Reliability:** `EndpointPoker` automatically discovers API endpoints via reflection and tests them to ensure proper authorization constraints (401/403) and 200 OKs are responding correctly, acting as an automated QA engineer.

*Reference:* View the setup guides in `007_patterns.playwright.md` for automated crawler scripting.

---

## 🤖 AI Analysis

> **Agent:** `4/15/2026/sonnet/4.6`

**Strengths observed:**
- This document does the best job in the `10x` series of explaining *why* each pattern exists rather than just describing *what* it is. The "BAD vs GOOD" code comparison for `Helpers` is particularly effective for onboarding.
- The ASCII architecture diagram for the Agent/Web split makes the distributed topology immediately clear without requiring a diagram tool.

**Gaps observed:**
- Sections 1–5 are well written but vary significantly in depth. Section 4 (Agents) and Section 5 (FreeTools) are noticeably shorter than Sections 1–3. Future passes should expand them equally.
- This document does not yet reference `106_findings.md` (which identifies what is still missing) or `107_next_steps.md` (what to build next). Adding those cross-references would make it a more complete orientation guide.

**Recommendation:**
- Consider adding a "What's Not Here Yet" callout box near the top that links to `106_findings.md`. Developers reading `101` as their orientation should know upfront that the Agent DTOs and auth pipeline are stubs awaiting implementation.