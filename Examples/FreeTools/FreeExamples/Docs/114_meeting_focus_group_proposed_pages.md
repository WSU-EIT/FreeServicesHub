# 114 — Meeting: Focus Group Review — Proposed Example Pages & JSON Store

> **Document ID:** 114  
> **Category:** Meeting  
> **Purpose:** Full team roleplay review of ProposedExamplePages.md (10 categories) and doc 113 (JSON Record Store architecture) before implementation begins.  
> **Audience:** CTO, devs, AI agents.  
> **Predicted Outcome:** Identify gaps, overlaps, overengineering, or missing edge cases in both the category plan and the storage layer.  
> **Actual Outcome:** ✅ Design validated with 7 actionable findings — no blockers, 3 refinements, 4 confirmations. See Decisions below.  
> **Resolution:** Proceed to Phase 1 implementation (Projects + Tickets) with the refinements listed in Decisions.

---

## Scope of Review

| Document | What's Being Reviewed |
|----------|-----------------------|
| `ProposedExamplePages.md` | 10 new example page categories (5 Jira-like + 5 domain workflows), data shapes, page variants, build order |
| `Docs/113_decision_json_record_store.md` | JsonRecord envelope, IJsonEntity interface, two-phase parse, generic CRUD, sub-record embedding, version compatibility |

**Review method:** Full discussion per doc 001 — each role examines from their specialty. Mid-check and final-check sanity passes.

---

## Discussion

---

### [Architect] — Framing the Review

We have two artifacts to validate: a **feature plan** (10 categories, ~50 page variants, 10+ entity types) and a **storage architecture** (generic JSON envelope store). They're coupled — the storage design has to actually support every entity and every query the pages need.

I'm looking for three things:

1. **Data model coherence** — Do the entities compose correctly? Are the FK relationships workable in a flat key-value store?
2. **Query feasibility** — Can the generic CRUD actually support the filtering, grouping, and cross-entity joins the page variants need?
3. **Scope proportionality** — Is this the right amount of work for an example/demo project? Are we building a product or a showcase?

Let's hear from each specialist.

---

### [Backend] — Data Shapes & Storage Layer

I've gone through every entity in the proposal and compared it against what doc 113 says the store can do. Here's my analysis:

**✅ What works cleanly:**

| Entity | Why It's Fine |
|--------|---------------|
| Project | Self-referential `ParentProjectId` is just a nullable Guid. Tree reconstruction is LINQ over flat list. |
| Ticket | Largest entity (~20 fields + Comments list). JSON serialization handles this fine. Comments are embedded — correct call since you never query "all comments across all tickets." |
| Sprint | Clean parent container. `PlannedPoints`/`CompletedPoints` are computed by LINQ-joining Tickets where `SprintId` matches. |
| WorkOrder | Standalone entity, no FK complications. Cascading dropdowns are pure UI — the data is just three string fields. |
| BudgetRequest | LineItems embedded as `List<BudgetLineItem>`. `TotalAmount` computed client-side by summing `LineTotal` per row. Perfect sub-record use case. |
| Equipment + Checkouts | Checkouts embedded in Equipment as `List<Checkout>`. `IsAvailable` computed from whether any checkout has `ReturnDate == null`. |
| Evaluation + Questions + Responses | Questions embedded in template. Responses are separate records since you need aggregate queries across all responses per evaluation. |
| Onboarding + ChecklistItems | ChecklistItems embedded. `CompletionPercentage` computed by counting `IsCompleted` items. |

**⚠️ Three things that need refinement:**

**1. Cross-entity queries: Tickets need to query by ProjectId.**

The generic `GetJsonRecords<T>(List<Guid>? ids)` fetches by primary key or returns all. But the Ticket pages need "get all tickets for project X" — a filter by a *foreign key*, not the primary key.

This is why the proposal already has `FilterJsonRecords<T>` and entity-specific filter subclasses like `FilterTickets`. But doc 113 only shows the interface stub for `GetJsonRecordsFiltered<T>()` — it doesn't show the implementation. We need to clarify: does the generic filtered query just load all records of type T into memory and then LINQ-filter? For an example app with ~100 seed records per type, that's fine. For 10,000+ it wouldn't be. But we're an example app, so **load-all-then-filter is acceptable**.

**Recommendation:** Add a one-sentence note to doc 113 confirming that `GetJsonRecordsFiltered` loads all non-deleted records of type T, then applies filter predicates in memory. This is explicitly acceptable because example data volumes are small (25–100 records per type).

**2. BoardConfig and SavedView: Are these IJsonEntity or embedded?**

The proposal lists `BoardConfigId` (board settings per user/project) and `SavedViewId` (saved filter presets). These are small config objects, not major entities. Two options:
- (A) Store as separate `IJsonEntity` records in the JSON store — full CRUD, own RecordType
- (B) Embed as JSON in a user preferences blob or project settings

**Recommendation:** (A) — make them their own `IJsonEntity`. They're small but they need independent CRUD (create a board config, list all boards for a project, delete one). Embedding them would require loading/saving a larger parent every time. The store handles unlimited entity types at zero cost — there's no reason to be stingy.

**3. Ticket auto-numbering: `{ProjectKey}-{sequence}` needs a counter.**

The proposal says TicketNumber is auto-generated as "WEB-42". That implies a per-project auto-incrementing counter. In a SQL database this is a sequence. In our in-memory store, we need an explicit counter somewhere.

**Recommendation:** Add a `NextTicketNumber` (int) field to the `Project` entity. When creating a ticket, read the project's counter, assign it, increment, save both. Race conditions aren't a concern in an example app with one user. If it ever mattered, `ConcurrentDictionary` plus a lock on the project record would handle it.

---

### [Frontend] — Page Variants & UI Patterns

I'm evaluating whether the proposed 50 page variants are **buildable** with the existing component library (Bootstrap 5 + Font Awesome + vanilla Blazor) and whether they're **distinct enough** to each teach something new.

**✅ Confirmed buildable with existing patterns:**

| Pattern | Already Proven In | New Categories Using It |
|---------|-------------------|------------------------|
| CRUD table with filters + pagination | SampleItems (main page) | Tickets, WorkOrders, Budget, Equipment, Evaluations, Onboarding |
| Card grid | SampleItemsV1 | Project Cards (V3), Technician View (WO V3) |
| Split panel | SampleItemsV3 | Grooming View (Backlog V1), Planning View (Sprint V1) |
| Drag-and-drop columns | KanbanBoard | Board Views (all variants), Sprint Planning V1, Backlog V1 |
| Accordion detail | SampleItemsV4 | My Checkouts V2 (history accordion) |
| Timeline | SampleItemsV5 | Ticket Activity (V2), Asset Detail (Equipment V4) |
| Chart integrations | Charts V1–V5 | Sprint Burndown, Velocity Report, Facilities Dashboard, Budget Overview |
| Markdown rendering | CodeEditor pages | Ticket Description (V1, V2) |

**✅ Genuinely new UI patterns that justify new pages:**

| New Pattern | Category | Why It's Worth Having |
|-------------|----------|----------------------|
| **Recursive tree view** with expand/collapse | Projects V1 | Never done before. Applies to org charts, file browsers, menu builders. |
| **Cascading dependent dropdowns** (3 levels) | Work Orders V1 | Building → Floor → Room. Common real-world pattern, no existing example. |
| **Dynamic row add/remove** with calculated totals | Budget V1 | Invoice/order line items. The running total + sticky footer is new. |
| **Inline click-to-edit** cells | Backlog V0 | Click a cell, it becomes an input, blur saves. Different from full form edit. |
| **Floating multi-select action bar** | Backlog V2 | Select checkboxes → bar appears with bulk actions. New interaction. |
| **Dynamic form rendering** from data | Evaluations V1 | Read question type → render matching input. Completely unique pattern. |
| **Fibonacci button group** selector | Tickets V1 | Story point picker (1/2/3/5/8/13/21). Tiny but distinctive. |
| **WIP limit badges** with threshold warnings | Board Views V1 | Column header turns red when over limit. Capacity constraint visualization. |
| **Swimlane horizontal sections** | Board Views V3 | Rows within the kanban board grouped by a field. New layout dimension. |
| **Completion heatmap** (rows × columns grid) | Onboarding V4 | Color-coded matrix. Mini data visualization without a charting library. |
| **Star rating widget** | Evaluations V1 | Interactive star selector. Simple but missing from current examples. |
| **Condition before/after** comparison | Equipment V1/V4 | Side-by-side state comparison. Useful for any audit trail. |

That's **12 genuinely new interaction patterns**. Each one is a copy-paste starting point someone could reuse. This is the value proposition of the example suite.

**⚠️ One concern — page count per category:**

Each category has a base page + 4 variants = 5 pages × 10 categories = **50 new pages**. Plus the existing 74 = **124 total pages**. That's a lot of navigation items.

**Recommendation:** The `ExampleNav` component already groups pages by category with parent/child breadcrumbs. We'll need a new category group for "Project Management" (categories 1–5) and "Domain Workflows" (categories 6–10) in the nav. The Dashboard page will need a new section or tab. This is manageable but we should do the nav update early (Phase 1) so every subsequent page slots in cleanly.

**⚠️ One simplification — consider merging Sprint and Backlog:**

Sprint Planning V1 ("drag from backlog to sprint") and Backlog V1 ("grooming view with detail panel") are very similar split-panel layouts. The difference is which side is the source and which is the target.

**Recommendation:** Keep them separate — they serve different *personas* (scrum master planning a sprint vs. product owner grooming the backlog). The data model separation (Sprint is a container entity; Backlog is a view over Tickets) is correct. But share the split-panel and drag-drop code between them as Blazor component partials or shared helper methods to avoid duplication.

---

### [Quality] — Testability, Edge Cases, Docs

**Testing strategy for the JSON store:**

The generic CRUD is the foundation everything else sits on. It needs to be tested *once*, thoroughly, and then all 10 entity types get that coverage for free. Key test cases:

| Test | What It Validates |
|------|-------------------|
| Save new entity → Get by ID → fields match | Round-trip serialization |
| Save with `RecordId == Guid.Empty` → gets assigned a new Guid | Auto-ID generation |
| Save existing entity → Modified timestamp updates, Created doesn't | Update semantics |
| Delete by ID → re-get returns empty (or only with explicit ID) | Soft delete |
| GetAll with mixed RecordTypes → only returns requested type | RecordType discriminator |
| Deserialize blob with missing fields → defaults to null/0/false | Schema backward compat |
| Deserialize blob with SchemaVersion > Current → returns null | Schema forward compat (skip) |
| Save entity with embedded sub-records → sub-records round-trip | Nested list serialization |
| Concurrent Save + Get → no data corruption | ConcurrentDictionary thread safety |

**Recommendation:** When we build `FreeExamples.App.DataAccess.JsonStore.cs`, write one test class `JsonStoreTests` that covers the generic CRUD with a simple test entity. This gives us confidence before wiring up 10 entity types.

**Edge cases in the proposal I want flagged:**

| Edge Case | Category | Risk | Mitigation |
|-----------|----------|------|------------|
| Circular parent reference (Project A → Project B → Project A) | Projects | Infinite loop in tree rendering | Depth-limit the tree traversal (max 10 levels). Check on save that ParentProjectId doesn't create a cycle. |
| Ticket in a deleted sprint | Tickets/Sprints | Orphan reference | When displaying SprintId, check if sprint exists. If not, show "Sprint removed" badge. Don't cascade-delete tickets when deleting a sprint — just null out their SprintId. |
| Empty evaluation (0 questions) | Evaluations | Broken dynamic form | Template Builder should require at least 1 question. Take Evaluation page should show "No questions configured" message if questions list is empty. |
| Budget line item with 0 quantity or negative price | Budget | Calculation errors | Client-side validation: quantity ≥ 1, unit price ≥ 0. Compute `LineTotal` as `Quantity * UnitPrice` (never allow manual override). |
| Equipment checked out to someone who already has 5 items | Equipment | No limit enforcement | Not required for an example app, but mention in the UI that a "max checkouts" rule could be added. Keep it simple. |
| Checklist with 0 items | Onboarding | Division by zero in CompletionPercentage | If `totalItems == 0`, return 0% (not divide-by-zero). Guard this in the computed property. |

**Documentation checklist:**

| Doc | Action Needed |
|-----|---------------|
| ProposedExamplePages.md | Add Backend's three refinements (FilterJsonRecords note, BoardConfig as IJsonEntity, NextTicketNumber counter) |
| Doc 113 | Add one-sentence note about in-memory filter strategy. Add NextTicketNumber example to Project entity. |
| Doc 112 | No change needed — it already references 113 and the proposal. |
| ExampleNav | Plan for new category groups ("Project Management", "Domain Workflows") |
| Per-page AboutSection | Each of the 50 new pages needs its own AboutSection explaining the pattern — plan these as part of each page, not as a separate pass. |

---

### [Sanity] — Mid-Check

*Okay, stop. Let's zoom out.*

We're proposing **50 new example pages**, **10+ new entity types**, a **new generic storage layer**, and **12 new UI patterns**. For an example/demo project.

**Is this too much?**

Let me check the proportionality:
- The existing 74 pages already demonstrate a lot — but they all use one entity (SampleItem) in different layouts. The *data modeling* examples are thin.
- The Jira-like categories (1–5) are **genuinely interconnected** — you can't show a sprint board without tickets, and tickets live in projects. This isn't feature creep; it's the minimum viable domain model.
- The domain categories (6–10) are **standalone** — each teaches a form pattern the Jira categories don't cover. Cascading dropdowns, dynamic form rendering, line-item math, checklist tracking — these are the patterns people actually Google for.
- The storage layer (doc 113) is **one file of generic CRUD** that all 10 entities share. It's not 10× the work — it's 1× the work plus 10 entity class definitions.

**My verdict:** The scope is **appropriate** because:
1. The phased build order means we don't commit to all 50 pages at once
2. Each phase delivers usable examples independently
3. The generic store means entity #10 is almost as cheap as entity #1
4. The 12 new UI patterns are the actual product — the entities are just vehicles

**What would be too much:**
- Adding authentication/authorization to the Jira features (role-based board permissions, etc.)
- Building real multi-tenant data isolation (every entity already has TenantId but we don't enforce it in the example)
- Adding real-time SignalR sync to the board views (nice to have, but save for a V5 if people ask)

**Recommendation:** Stay disciplined on "example-quality" — seed data, in-memory store, no auth, no SignalR (unless reusing existing pattern). If a page variant starts needing infrastructure the example suite doesn't have, cut it or simplify it.

---

### [Frontend] — Response to Sanity Check

Agreed. One specific simplification: the proposal mentions **"photo upload"** in Work Orders V1 and V3, and **"photo placeholder"** in Equipment V4. We already have the FileDemo pages for upload patterns. For the new categories, use a **placeholder image** (gray box with camera icon) instead of wiring up actual file upload. This keeps the focus on the form pattern, not the file handling.

Same for **"signature capture"** in Equipment V1 — the proposal says "reusing existing Signature component" which is correct. Just reference it, don't rebuild it.

### [Backend] — Response to Sanity Check

Agreed on scope. One more simplification: the **Velocity Report** (Sprint V4) and **Department Report** (Evaluations V4) require historical data across multiple completed sprints/evaluations. With seed data, we can fake this — seed 5–8 completed sprints with varying point totals so the velocity chart has data. But we should **seed the historical data up front** during `SeedSprints()`, not expect the user to complete sprints manually to see the charts.

### [JrDev] — Questions

Couple of things I want to make sure I understand:

**Q1:** "Sub-records are embedded in the parent's JSON." So when I save a Ticket, the Comments go with it. But what if two people add a comment at the same time? Don't they overwrite each other's entire Ticket blob?

**[Backend]:** Yes — in theory this is a last-write-wins problem. In practice, this is an **example app** with one user. We're not building Jira. If this ever became a production concern, you'd move Comments to their own `IJsonEntity` (doc 113 already has the escape hatch: "If a sub-record type needs to be queried independently, it gets its own IJsonEntity"). For the example, embedded is fine.

**Q2:** The `IJsonEntity` interface uses `static abstract` members. Does that mean we need .NET 7+? What if someone clones this on .NET 6?

**[Backend]:** The project targets .NET 10. Static abstract interface members have been stable since .NET 7. Anyone cloning this is using .NET 8+ at minimum (the current LTS). Not a concern.

**Q3:** The proposal says "computed fields" like `CompletionPercentage`, `TicketCount`, `FullPath`. Where do those live? Are they stored in JSON or calculated on read?

**[Backend]:** **Calculated on read.** These are `[JsonIgnore]` properties or computed in the page's `@code` block after loading. They're never persisted. The proposal lists them under "Computed/display fields" to show what the UI will *display*, not what the entity *stores*. We should add `[JsonIgnore]` annotations in the actual entity classes and a comment explaining they're computed.

**Q4:** How does the ExampleNav know about the new pages? Do I just add entries to the list?

**[Frontend]:** Yes — `ExampleNav.razor` has a flat `List<ExPage>` with category, title, route. You add entries. We'll need two new category strings: `"Project Management"` and `"Domain Workflows"`. The breadcrumb, prev/next, and dashboard grouping all derive from that list automatically.

---

### [Sanity] — Final Check

Let me walk through the entire chain one more time:

**Data flow for a Ticket (most complex entity):**

```
1. User creates Ticket on Tickets V1 (form page)
2. Blazor @code serializes to Ticket object
3. HTTP POST to /api/Data/SaveTickets with List<Ticket>
4. Controller calls DataAccess.SaveJsonRecords<Ticket>(list)
5. Store serializes Ticket → JSON → wraps in JsonRecord envelope
6. Stores in ConcurrentDictionary<Guid, JsonRecord>
7. Returns saved Ticket (with assigned RecordId if new)
8. Page refreshes ticket list
```

**Data flow for Board View (cross-entity query):**

```
1. Board Views page loads
2. Calls GetJsonRecords<Ticket>(null) → all tickets
3. Calls GetJsonRecords<Project>(projectIds) → current project
4. Calls GetJsonRecords<Sprint>(null) → sprints for project
5. Client-side LINQ: group tickets by Status for columns
6. Client-side LINQ: filter to current sprint if sprint board
7. Render columns with ticket cards
8. Drag card → update Ticket.Status → SaveJsonRecords<Ticket>
```

This works. Multiple HTTP calls are fine — they're all in-memory lookups, sub-millisecond. No N+1 problem because there's no database.

**Seed data completeness check:**

| Category | Needs Seed Data | Estimated Records |
|----------|-----------------|-------------------|
| Projects | 5–8 projects with 2–3 nesting levels | ~15 |
| Tickets | 30–50 tickets across projects, mixed types/statuses/sprints | ~40 |
| Sprints | 5–8 sprints (2 completed, 1 active, 2 planning) per project | ~15 |
| BoardConfig | 2–3 saved board configurations | ~3 |
| SavedView | 3–4 saved filter presets | ~4 |
| WorkOrders | 15–20 across buildings, mixed statuses/urgency | ~18 |
| BudgetRequests | 5–8 with 3–5 line items each | ~7 |
| Equipment | 10–15 items with 2–3 checkout history each | ~12 |
| Evaluations | 3–5 with templates, some with responses | ~5 |
| Onboarding | 5–8 employees with checklists at various completion | ~6 |
| **Total** | | **~125 records** |

125 records in a `ConcurrentDictionary` is trivial. Memory footprint is negligible. Seed time is < 50ms.

**Anything missing?**

One thing: the **connection between categories**. The proposal says Budget Requests can optionally link to a Project (`ProjectId` FK), and Work Orders can escalate to a Ticket. These cross-domain links are nice for showing how entities compose, but they're **optional**. Don't block any category on another. Each category should be usable standalone with its own seed data, even if the linked record doesn't exist.

**Recommendation:** Make all cross-category FK fields nullable and handle missing references gracefully in the UI (show "No project linked" instead of crashing).

---

### [Architect] — Summary

I've heard from everyone. Let me consolidate.

**The design is sound.** The JSON envelope store (doc 113) is well-architected for this use case — it provides the right abstraction (typed entities over generic blobs), the right escape hatches (version compatibility, sub-record promotion), and the right simplicity (one dictionary, three generic methods). The 10 categories cover genuinely different data patterns and UI interactions without excessive overlap.

**No blockers. Three refinements. Four confirmations.**

---

## Decisions

### Refinements (Action Required)

| # | Finding | Decision | Owner | Priority |
|---|---------|----------|-------|----------|
| R1 | `GetJsonRecordsFiltered` implementation not shown in doc 113 | Add note: "loads all non-deleted records of type T into memory, applies filter predicates via LINQ. Acceptable because example data volumes are 25–100 records per type." | [Backend] | P2 — before Phase 1 |
| R2 | BoardConfig and SavedView storage strategy unclear | Make them their own `IJsonEntity` with full CRUD. They're small but need independent create/list/delete. | [Backend] | P2 — before Phase 2 (Board Views) |
| R3 | Ticket auto-numbering needs a counter | Add `NextTicketNumber` (int) field to Project entity. Increment on ticket creation. | [Backend] | P1 — Phase 1 (Tickets) |

### Confirmations (No Change Needed)

| # | Concern Raised | Resolution |
|---|----------------|------------|
| C1 | 50 pages too many? | No — phased build order, generic store makes each entity cheap, 12 genuinely new UI patterns justify the scope. |
| C2 | Embedded sub-records vs. separate? | Embedded is correct for Comments, LineItems, Checkouts, ChecklistItems. Promote to own IJsonEntity only if independent querying is needed (see R2). |
| C3 | Computed fields storage | Never persisted. `[JsonIgnore]` on entity class, computed in page `@code` or DataAccess helper. |
| C4 | Cross-category FKs | All nullable. Handle missing references gracefully. No category blocks another. |

### Simplifications (Agreed)

| # | What | Simplification |
|---|------|----------------|
| S1 | Photo upload in Work Orders / Equipment | Use placeholder image (gray box + camera icon). Don't wire up actual FileUpload — that pattern is already covered in FileDemo pages. |
| S2 | Velocity/Department reports | Seed historical data (5–8 completed sprints, past evaluations) so charts render on first load. Don't require manual data entry to see visualizations. |
| S3 | Real-time sync | No SignalR for board views in Phase 1–3. The existing KanbanBoard already demonstrates SignalR + drag-drop. New boards focus on the *configuration* and *swimlane* patterns. |
| S4 | Auth/role enforcement | Not in scope. TenantId exists on every entity but is not enforced. Example app assumes single user. |

---

## Edge Cases Registry

Flagged by [Quality] — each must be handled during implementation:

| # | Edge Case | Category | Required Guard |
|---|-----------|----------|----------------|
| E1 | Circular parent reference | Projects | Depth-limit tree traversal (max 10). Validate on save that ParentProjectId doesn't create a cycle. |
| E2 | Ticket in deleted sprint | Tickets/Sprints | Show "Sprint removed" badge. Don't cascade-delete — null out SprintId. |
| E3 | Empty evaluation template | Evaluations | Require ≥1 question in Template Builder. Show "No questions" message in Take Evaluation. |
| E4 | Zero quantity / negative price | Budget | Client-side: quantity ≥ 1, price ≥ 0. Compute LineTotal server-side (Quantity × UnitPrice). |
| E5 | 0 checklist items | Onboarding | Guard: if totalItems == 0, CompletionPercentage = 0 (not divide-by-zero). |
| E6 | Missing cross-category FK target | All | Nullable FKs. UI shows "Not linked" or "Removed" instead of crashing. |

---

## Build Order (Confirmed)

No changes to the proposed build order from ProposedExamplePages.md:

| Phase | Categories | Delivers | Depends On |
|-------|-----------|----------|------------|
| **0** | JSON Store infrastructure | Generic CRUD, IJsonEntity, JsonRecord, seed orchestrator | Nothing — do this first |
| **1** | Projects (#1) + Tickets (#2) | Core data model, tree view, complex form, CRUD tables | Phase 0 |
| **2** | Board Views (#3) + Backlog (#5) | Kanban/swimlane boards, inline edit, bulk ops | Phase 1 |
| **3** | Sprint Planning (#4) | Sprint lifecycle, burndown, velocity | Phase 1 |
| **4** | Work Orders (#6) + Budget (#7) | Cascading dropdowns, line-item math, approval workflow | Phase 0 only |
| **5** | Equipment (#8) + Evaluations (#9) + Onboarding (#10) | Transaction log, dynamic forms, checklists | Phase 0 only |

**Note:** Added Phase 0 for the JSON store infrastructure itself. Phases 4–5 only depend on Phase 0 (the generic store), not on Phases 1–3. They can be built in any order after the store exists.

---

## Next Steps

| Action | Owner | Priority | Phase |
|--------|-------|----------|-------|
| Update doc 113 with R1 (filter implementation note) | [Backend] | P2 | Pre-Phase 1 |
| Add `NextTicketNumber` to Project entity spec | [Backend] | P1 | Phase 1 |
| Note BoardConfig/SavedView as own IJsonEntity | [Backend] | P2 | Pre-Phase 2 |
| Update ExampleNav with new category groups | [Frontend] | P1 | Phase 1 |
| Build `FreeExamples.App.DataObjects.JsonStore.cs` | [Backend] | P1 | Phase 0 |
| Build `FreeExamples.App.DataAccess.JsonStore.cs` | [Backend] | P1 | Phase 0 |
| Write `JsonStoreTests` covering generic CRUD | [Quality] | P1 | Phase 0 |
| Begin Project + Ticket entities and pages | [Full Team] | P1 | Phase 1 |

---

⏸️ **CTO Input Needed**

**Question:** Ready to proceed to Phase 0 (JSON store infrastructure)?

**Options:**
1. **Go** — Build the store, write tests, then start Phase 1 (Projects + Tickets)
2. **Revise** — Address specific concerns before implementation
3. **Scope down** — Start with fewer categories (e.g., just 1–5, defer 6–10)

@CTO — Which way?

---

*Created: 2025-07-14*  
*Participants: [Architect], [Backend], [Frontend], [Quality], [Sanity], [JrDev]*  
*Maintained by: [Quality]*
