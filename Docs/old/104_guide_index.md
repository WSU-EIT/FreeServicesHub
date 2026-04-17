# 104 — The Mandatory Reading List & Guide Index

> **Document ID:** 104  
> **Category:** Index / Onboarding  
> **Purpose:** A curated, prioritized catalog of the `000` through `008` guide documents. Highlights the absolute "must-follow" rules for building within the FreeServicesHub ecosystem.   
> **Audience:** All developers contributing to FreeServicesHub, FreeCICD, or the Agent ecosystem.  
> **Outcome:** 📖 Knowing exactly what to read, in what order, and why it matters to your daily workflow.

---

## 1. The Recommended Reading Order (Start Here)

If you read nothing else, you **must** read these five documents in this exact order before writing your first line of code in `FreeServicesHub`:

```ascii
 ┌─────────────────┐     ┌───────────────┐     ┌────────────────┐
 │ 1. 006_Hooks    │ ──► │ 2. 004_Style  │ ──► │ 3. 007_CRUD    │
 │ (Never edit base)│     │ (File naming) │     │ (The 3 APIs)   │
 └─────────────────┘     └───────────────┘     └────────────────┘
                                                       │
                                                       ▼
                       ┌─────────────────┐     ┌────────────────┐
                       │ 5. 007_SignalR  │ ◄── │ 4. 007_Helpers │
                       │ (Real-time sync)│     │ (No HttpClient)│
                       └─────────────────┘     └────────────────┘
```

1. 🔥 **`006_architecture.extension_hooks.md`** 
   * **Why:** This is the Golden Rule of the repository. It explains the `.App.` hook pattern. If you try to modify a base FreeCRM file (`Program.cs`, `DataController.cs`), your PR will be rejected. This teaches you how to inject code safely.
2. 🏗️ **`004_styleguide.md`** 
   * **Why:** Dictates the **Mandatory File Naming Convention** (`{ProjectName}.App.{Feature}.cs`). If you don't name your files correctly, framework updates will overwrite or break your custom Hub code.
3. 🔌 **`007_patterns.crud_api.md`** 
   * **Why:** We do not use standard REST (`GET`, `POST`, `PUT`, `DELETE`). You must understand the **Three-Endpoint Pattern** (`GetMany`, `SaveMany`, `DeleteMany`) to build Hub APIs.
4. 🛠️ **`007_patterns.helpers.md`** 
   * **Why:** You should rarely use raw `HttpClient` or `NavigationManager`. This doc explains our ubiquitous `Helpers` class (`Helpers.GetOrPost<T>`, `Helpers.NavigateTo`) which automatically handles tenant routing and anti-forgery.
5. ⚡ **`007_patterns.signalr.md`** 
   * **Why:** FreeServicesHub relies heavily on real-time data (e.g., Agent status updates). This explains how to dispatch and consume tenant-aware SignalR messages without leaking data across tenants.

---

## 2. Categorized Guide Index

Below is the complete indexed grouping of the reference materials and how they specifically relate to your work in the FreeServicesHub.

### Category A: Architecture & Hooks (`006_...`)
*How we safely extend the base platform.*

* **`006_architecture.md`**: The master index for architecture.
* **`006_architecture.extension_hooks.md`**: The core primer on the 3-layer extension system.
* **`006_architecture.program.app.md`**: How to inject custom DI services (like our `AgentWorkerService` connections) into the startup pipeline.
* **`006_architecture.datacontroller.app.md`**: Where `FreeServicesHub` registers its custom API endpoints for the Agent to call.
* **`006_architecture.dataaccess.app.md`**: How to map Entity Framework models to DTOs using `GetDataApp` and `SaveDataApp`.
* **`006_architecture.appcomponents.app.md`**: How to inject Hub-specific fields into the standard FreeCRM generic user/settings pages.

### Category B: Coding Patterns & Utilities (`007_...`)
*The standard tools you must use to move data and render state.*

* **`007_patterns.md`**: Master index for data patterns.
* **`007_patterns.crud_api.md`**: The batch-first `GetMany`/`SaveMany` paradigm.
* **`007_patterns.helpers.md`**: The Bible for the Blazor Client. Includes `Helpers.ValidateUrl`, `Helpers.BuildUrl`, and `Helpers.MissingValue` for form validation.
* **`007_patterns.signalr.md`**: Real-time events. Critical for the Hub UI to react immediately when the `FreeServicesHub.Agent` finishes a background job.
* **`007_patterns.filter_pagination.md`**: How to build performant, server-side paginated tables for large Hub datasets.
* **`007_patterns.timers.md`**: How to implement debounce timers and `BackgroundService` polling (extensively used by Web-to-Agent communication).
* **`007_patterns.playwright.md`**: Mandatory reading if you are writing E2E tests or extending the `FreeTools` CLI apps.

### Category C: UI & Frontend Components (`008_...`)
*How things should look and feel.*

* **`008_components.md`**: Master index for the UI.
* **`008_components.razor_templates.md`**: Copy-paste these templates when making a new Page in the Hub. It includes proper tenant-routing (`@page "/{TenantCode}/..."`).
* **`008_components.bootstrap_patterns.md`**: How to properly use Bootstrap 5 Cards, Tabs, and Badges to match the FreeCRM aesthetic.
* **`008_components.wizard.md`**: Used for complex Hub workflows (like configuring a new remote Agent deployment).
* **`008_components.highcharts.md` & `.network_chart.md`**: Libraries used for rendering Hub analytics and service-dependency graphs.

### Category D: Code Style & Documentation (`000` - `005`)
*How we write and maintain the repository.*

* **`002_docsguide.md` & `003_templates.md`**: How to write these very documents.
* **`004_styleguide.md`**: The C# and EditorConfig bible. Enforces explicit typing over `var`, and specific bracing limits.
* **`005_style.comments.md`**: The voice of our code. Comments must be procedural, present-tense, and explain the "why." (e.g., `// First, verify the agent is online...`)

---

## 3. The "Do Not Violate" Cheat Sheet

When reviewing Pull Requests for `FreeServicesHub`, reviewers will explicitly look for violations of these rules:

1. **Did you modify a base file?** (Violates `006_architecture.extension_hooks`) -> *Use a `.App.` partial class!*
2. **Did you build `GetMyDataById`?** (Violates `007_patterns.crud_api`) -> *Use `GetMany([id])`!*
3. **Did you inject `NavigationManager` directly?** (Violates `007_patterns.helpers`) -> *Use `Helpers.NavigateTo()` to preserve tenant scope!*
4. **Did you name your file `Dashboard.razor`?** (Violates `004_styleguide.md`) -> *Name it `FreeServicesHub.App.Dashboard.razor`!*
5. **Did you broadcast a generic SignalR message to `Clients.All`?** (Violates `007_patterns.signalr.md`) -> *Broadcast ONLY to the specific `TenantId` group!*

---

## 🤖 AI Analysis

> **Agent:** `4/15/2026/sonnet/4.6`

**Strengths observed:**
- The reading-order flowchart (ASCII art added in the most recent pass) is highly effective. It makes the dependency chain between the five mandatory docs visually obvious rather than just ordered as a list.
- The "Do Not Violate" cheat sheet at the bottom is the single most useful section for PR reviewers. These five rules cover the majority of architectural mistakes a new developer would make.
- The document correctly separates guides into categories A–D, making it easy to jump to "I need to do X, which doc do I read?"

**Gaps observed:**
- The `10x` docs (`100` through `107`) are not listed in any of the guide categories. A Category E (or a separate callout box) pointing to these deep-dive docs would complete the index.
- `007_patterns.timers.md` is listed in Category B but not explained in detail. Timers and debounce patterns are actually critical for the Hub's Agent polling UI — this entry deserves a line describing *why* it matters to Hub specifically.
- There is no mention of `006_architecture.configurationhelper.app.md`, which is critical when the Agent needs its own `appsettings.json` keys injected into the DI container.

**Recommendation:**
- Add a Section 4: "The `10x` Series" that lists `100` through `107` with one-line descriptions, making this the single master index for both the original `000`–`008` guides and the new deep-dive series.