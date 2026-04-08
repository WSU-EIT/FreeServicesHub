# Proposed Example Page Categories

> 10 new entity-driven example categories — half are **Jira-like project management features** (hierarchical projects, tickets, boards, sprints, backlogs) and half are **domain-specific workflows** (work orders, budgets, etc.). Each category still serves as a reusable starting-point example with its own data object, CRUD endpoints, and multiple page variants.

---

## What We Already Have

| Category | Pages | Pattern |
|---|---|---|
| Dashboard & Data | SampleItems + V1–V5, Search, Comparison, ItemCards | CRUD table, card grid, split panel, accordion, timeline, stats |
| Files & Media | FileDemo + V1–V6, Gallery, Carousel, Signature + V1–V5 | Upload, drag-drop, preview, digital signatures |
| UI Components | Bootstrap V1–V12, Kanban, Status Board, Pipeline, Wizard, Command Palette, Comments, Chat | Layout patterns, drag-drop, status workflows |
| Charts & Viz | Charts + V1–V5, NetworkGraph + V1–V2 | Bar, line, pie, radar, org chart, dependency map |
| Code & Real-Time | CodeEditor + V1–V5, Playground, SignalR + V1–V5, Timer + V1–V5, Git Browser, API Keys | Monaco, real-time push, countdown/polling |

### What's Missing

The existing KanbanBoard drags SampleItems between status columns — it demonstrates the **drag-and-drop pattern** but doesn't model real project management. There's no concept of:
- Projects that contain sub-projects that contain tickets
- Sprints with start/end dates scoping which tickets are active
- A backlog you groom and pull from
- Ticket types (bug vs. story vs. task) with different workflows
- Assignment, story points, or capacity planning
- Timeline/roadmap views across projects

The existing pages also lack **domain-specific entity examples** — forms someone could copy for facilities work orders, budget approvals, or equipment lending.

---

## Design Philosophy

Categories 1–5 build a **lightweight Jira-like system** — each category is a distinct feature area with its own entity, but they share a common project/ticket data model. Categories 6–10 are **standalone domain workflows** that showcase form patterns the Jira categories don't cover (cascading dropdowns, multi-line-item forms, dynamic form rendering, etc.).

Together they give someone 10 genuinely different starting points to copy from.

---

## Categories 1–5: Project Management (Jira-Like)

### 1. Projects & Hierarchy

**What it is:** The container everything else lives in. A project can have sub-projects, and sub-projects can have sub-sub-projects — unlimited nesting. This is how you break "Website Redesign" into "Frontend," "Backend," "Database," then break "Frontend" into "Navigation," "Forms," "Dashboard."

**Why it's a good example:** Demonstrates **recursive/hierarchical data** — a pattern that comes up constantly (org charts, folder structures, category trees, menu systems) but that SampleItems doesn't touch. The tree view component alone is worth having.

**Data shape:**
- `ProjectId` (Guid), `TenantId` (Guid)
- `ParentProjectId` (Guid? — null = top-level, set = nested under parent)
- `Name` (string), `Description` (string?)
- `ProjectKey` (string — short prefix like "WEB", "HR", "FAC" — used in ticket numbers)
- `LeadName` (string), `LeadEmail` (string?)
- `Department` (string?)
- `Status` (enum: Planning, Active, OnHold, Completed, Archived)
- `StartDate` (DateTime?), `TargetEndDate` (DateTime?)
- `Color` (string? — hex color for board/timeline display)
- `SortOrder` (int — ordering among siblings)
- Audit fields (Added, LastModified, Deleted, etc.)

**Computed/display fields:**
- `Depth` (int — 0 = top-level, 1 = sub-project, etc.)
- `FullPath` (string — "Website Redesign → Frontend → Navigation")
- `TicketCount` (int — total tickets in this project + descendants)
- `OpenTicketCount` (int)
- `CompletionPercentage` (int — closed tickets / total tickets across descendants)

**Page variants:**
- **Projects** — Main list: tree view showing hierarchy with expand/collapse, status badges, ticket count per node, progress bars, drag-to-reorder siblings
- **V1: Project Tree** — Full interactive tree: expand/collapse all, indent levels visually, click node to see details in side panel, breadcrumb trail showing "Home → Web Redesign → Frontend → Nav"
- **V2: Project Form** — Create/edit: name, key, description, parent selector (dropdown showing indented tree), lead assignment, date range, color picker. Changing parent moves the entire subtree
- **V3: Project Cards** — Top-level projects as dashboard cards: name, lead, status badge, progress donut, open ticket count, nested sub-project count. Click to drill in
- **V4: Project Comparison** — Side-by-side: select 2–3 projects, compare ticket velocity, completion %, assignment distribution, timeline overlap

**Bootstrap patterns showcased:** Tree view with expand/collapse, breadcrumb drill-down, color picker, nested dropdown selectors, progress donuts, drag-to-reorder

---

### 2. Tickets

**What it is:** The core work item — bugs, stories, tasks, epics. Every ticket belongs to a project, has a type and status, can be assigned to someone, estimated with story points, labeled, and linked to other tickets. This is the Jira "issue" equivalent.

**Why it's a good example:** Demonstrates **complex multi-field forms** with rich enums, markdown descriptions, linked records, and a status workflow that varies by ticket type. The edit form alone has 15+ fields across multiple sections — the most complex form in the entire example suite.

**Data shape:**
- `TicketId` (Guid), `TenantId` (Guid)
- `ProjectId` (Guid — FK to Projects)
- `TicketNumber` (string — auto-generated: "{ProjectKey}-{sequence}" like "WEB-42")
- `Title` (string), `Description` (string? — markdown-enabled)
- `TicketType` (enum: Epic, Story, Task, Bug, Improvement, SubTask)
- `Status` (enum: Backlog, ToDo, InProgress, InReview, Testing, Done, Closed, Wontfix)
- `Priority` (enum: Critical, High, Medium, Low, Trivial)
- `AssignedTo` (string?), `ReporterName` (string)
- `StoryPoints` (int? — 1, 2, 3, 5, 8, 13, 21)
- `Labels` (string? — comma-separated: "frontend,urgent,accessibility")
- `SprintId` (Guid? — null = backlog, set = in a sprint)
- `ParentTicketId` (Guid? — for sub-tasks under a story/epic)
- `DueDate` (DateTime?)
- `StartedDate` (DateTime?), `CompletedDate` (DateTime?)
- `SortOrder` (int — ordering within a column/sprint)
- Audit fields

**Comment sub-record:**
- `CommentId`, `TicketId`, `AuthorName`, `Body` (string — markdown), `IsInternal` (bool), `CreatedDate`, `EditedDate`

**Page variants:**
- **Tickets** — Main CRUD table: project filter, status filter, type filter, priority filter, assignee filter, label filter. Sortable by priority, date, story points. Keyword search across title + description. PagedRecordset with server-side pagination
- **V1: Ticket Form** — Full create/edit form: type selector (icon per type), title, markdown description with preview toggle, project dropdown (shows tree), parent ticket selector (for sub-tasks), priority radio with color indicators, story point fibonacci selector (button group: 1/2/3/5/8/13/21), assignee dropdown, labels as tag input (type to add, x to remove), due date picker, sprint dropdown. Status shown as a step indicator across the top
- **V2: Ticket Detail** — Read view + activity: ticket header (number, type icon, status badge, priority), description rendered as markdown, metadata sidebar (assignee, reporter, sprint, points, labels, dates), linked tickets section, comments thread with markdown support, activity log (status changes, reassignments, edits with timestamps)
- **V3: Quick Create** — Lightweight modal/offcanvas: just title + type + project + priority. Creates ticket in Backlog status. Useful from board views where you want to quickly add without leaving the page
- **V4: Bulk Edit** — Multi-select table: checkbox column, select multiple tickets, bulk assign, bulk change priority, bulk move to sprint, bulk add label. Shows count of selected and preview of changes before applying

**Bootstrap patterns showcased:** Markdown preview toggle, Fibonacci button group selector, tag input for labels, step indicator for status, activity timeline, offcanvas quick-create, bulk select with action bar

---

### 3. Board Views

**What it is:** Kanban and Scrum boards — the visual heart of project management. Tickets shown as cards in columns by status, with drag-and-drop to move between columns. Can be scoped to a sprint or show all work. Swimlanes group cards by assignee, priority, or ticket type.

**Why it's a good example:** The existing KanbanBoard demonstrates drag-and-drop with SampleItems. This takes it further with **configurable columns**, **swimlanes**, **WIP limits**, and **board filtering** — patterns needed anytime you build a workflow visualization. The board configuration form itself is a good example of a settings UI.

**Data shape (board configuration — persisted per user/project):**
- `BoardConfigId` (Guid), `TenantId` (Guid)
- `ProjectId` (Guid — which project this board is for)
- `BoardName` (string — e.g. "Dev Board", "QA Board")
- `BoardType` (enum: Kanban, Sprint)
- `ColumnConfig` (string — JSON: which statuses map to which columns, column order)
- `SwimlaneField` (string? — null = no swimlanes, "assignee" / "priority" / "type")
- `WipLimits` (string? — JSON: max cards per column, e.g. `{"InProgress": 5, "InReview": 3}`)
- `FilterPreset` (string? — JSON: default filters applied to this board)
- `CreatedBy` (string)
- Audit fields

**Page variants:**
- **Board Views** — Default Kanban board for selected project: columns by status, ticket cards showing type icon + number + title + assignee avatar + priority dot + story points badge. Drag card to change status. Header shows project name + sprint selector (if sprint board) + filter bar
- **V1: Kanban Board** — Full kanban: columns for each status, cards with condensed info, drag between columns saves status immediately, WIP limit warnings (column header turns red when over limit), collapse/expand columns, card count per column
- **V2: Sprint Board** — Same column layout but scoped to a single sprint. Sprint selector dropdown in header. "Backlog" column on the left for unplanned items that can be pulled in. Sprint goal displayed at top. Burndown mini-chart in corner
- **V3: Swimlane Board** — Board with horizontal swimlanes: toggle between grouping by assignee (see each person's cards across statuses), by priority (Critical lane at top), or by ticket type (Bugs vs Stories vs Tasks). Collapse individual swimlanes
- **V4: Board Settings** — Configuration form: select which statuses become columns and in what order, set WIP limits per column, choose default swimlane grouping, set default filters, choose card display density (compact/normal/detailed), save as named board configuration

**Bootstrap patterns showcased:** Drag-and-drop columns with persistence, WIP limit badges, swimlane horizontal sections, collapsible lanes, board configuration form with sortable column list, density toggle (compact/normal)

---

### 4. Sprint Planning

**What it is:** Sprint lifecycle management — create a sprint, set dates, drag tickets from backlog into the sprint, track capacity, start the sprint, then complete it (moving unfinished tickets to the next sprint or back to backlog). This is the planning ceremony in a UI.

**Why it's a good example:** Demonstrates **time-boxed container management** — a parent entity (sprint) that temporarily "owns" child entities (tickets) for a duration. Also shows **capacity planning** (how much work can the team handle) and **velocity tracking** (how much did they actually finish). These patterns apply to any time-boxed process (semesters, fiscal quarters, event planning phases).

**Data shape:**
- `SprintId` (Guid), `TenantId` (Guid)
- `ProjectId` (Guid — FK to Projects)
- `Name` (string — e.g. "Sprint 14", "June 2025")
- `Goal` (string? — one-sentence sprint objective)
- `StartDate` (DateTime?), `EndDate` (DateTime?)
- `Status` (enum: Planning, Active, Completed, Cancelled)
- `CapacityPoints` (int? — team's estimated capacity in story points)
- Audit fields

**Computed/display fields:**
- `PlannedPoints` (int — sum of story points for tickets in this sprint)
- `CompletedPoints` (int — sum of points for Done/Closed tickets)
- `RemainingPoints` (int — planned minus completed)
- `TicketCount` (int), `CompletedTicketCount` (int)
- `DaysRemaining` (int — countdown from today to EndDate)
- `Velocity` (decimal — historical average completed points per sprint)

**Page variants:**
- **Sprint Planning** — Sprint list for a project: current/upcoming/past tabs, name, date range, goal, planned vs. capacity gauge, completion %, status badge. Create new sprint button
- **V1: Planning View** — Split panel: left = backlog (tickets not in any sprint, ordered by priority), right = current sprint. Drag tickets from backlog to sprint. Running total of story points vs. capacity. Warning when over capacity. Quick-estimate: click a ticket's story points to change inline
- **V2: Active Sprint** — Dashboard for the in-progress sprint: sprint goal at top, days remaining countdown, burndown chart (ideal line vs. actual), ticket status breakdown (pie: backlog/in-progress/done), list of committed tickets with status badges, "Complete Sprint" button
- **V3: Sprint Retrospective** — Completed sprint summary: goal achieved? (yes/no), planned vs. delivered points, velocity trend (line chart of last 5–10 sprints), carried-over tickets list, "what went well / what didn't / action items" simple form
- **V4: Velocity Report** — Cross-sprint analytics: velocity trend chart, average/median/best/worst sprints, points committed vs. delivered per sprint (grouped bar chart), scope change tracking (tickets added mid-sprint), team-level velocity comparison

**Bootstrap patterns showcased:** Split-panel drag-to-assign, capacity gauge with warning threshold, burndown chart, countdown badges, velocity trend lines, sprint comparison grouped bars

---

### 5. Backlog & Grooming

**What it is:** The prioritized queue of all work not yet in a sprint. Drag to reorder priority, inline-edit fields without opening the full form, bulk-select and assign/label/move, save filter views. This is where the team grooms and refines upcoming work.

**Why it's a good example:** Demonstrates **sortable/reorderable lists** with persistence, **inline editing** (click a cell to edit without navigating away), and **bulk operations** with a floating action bar. These patterns apply anywhere you have a prioritized queue (support ticket triage, application review, content publishing pipelines).

**Data shape:**
This category doesn't have its own entity — it's a **view layer** over Tickets, filtered to `SprintId == null` (not in any sprint). The "data" it manages is:
- Ticket sort order in the backlog (`SortOrder` field on Ticket)
- Saved filter/view configurations (could be in-memory or persisted)

**Saved View (optional persistence):**
- `SavedViewId` (Guid), `TenantId` (Guid)
- `Name` (string — e.g. "My High-Priority Bugs", "Unestimated Stories")
- `ProjectId` (Guid?)
- `FilterJson` (string — serialized filter criteria)
- `SortJson` (string — serialized sort rules)
- `GroupByField` (string? — "type", "priority", "assignee", "label")
- `CreatedBy` (string)

**Page variants:**
- **Backlog** — Full prioritized list: tickets ordered by SortOrder, drag handle to reorder, columns for type icon, ticket number, title, priority, story points, assignee, labels. Inline-click-to-edit on priority, points, and assignee. Filter bar across top
- **V1: Grooming View** — Split panel: left = backlog list with condensed rows, right = selected ticket detail panel (full description, comments, linked tickets). Click a row on the left to see detail on the right. Add/update story points from the detail panel. Mark as "refined" (ready for sprint)
- **V2: Bulk Operations** — Checkbox column for multi-select, floating action bar appears when ≥1 selected: "Assign to..." dropdown, "Set Priority..." dropdown, "Add Label..." input, "Move to Sprint..." dropdown, "Delete" with confirmation. Shows count selected. Select-all checkbox in header
- **V3: Grouped Backlog** — Same ticket data but grouped: toggle between group-by-type (Epics with their stories nested underneath), group-by-priority (Critical section at top), group-by-assignee (unassigned section highlighted), group-by-label. Collapse/expand groups, ticket count per group
- **V4: Saved Views** — Manage saved filters: create a filter (type = Bug AND priority ≥ High AND assignee = unassigned), save with a name, see list of saved views as tabs across the top. Click a tab to apply that filter. Edit/delete saved views. "Share with team" toggle

**Bootstrap patterns showcased:** Drag-to-reorder with handle icons, inline click-to-edit cells, floating multi-select action bar, split-panel grooming, collapsible group headers, saved filter tabs

---

## Categories 6–10: Domain Workflows

### 6. Work Orders (Facilities Maintenance)

**What it is:** "The toilet on floor 3 is broken" → tracked, assigned, completed. Every university has a facilities team processing these. This is a domain-specific ticket type with its own workflow, distinct from project management tickets.

**Why it's a good example:** Demonstrates **cascading dependent dropdowns** (Building → Floor → Room), a **multi-party status workflow** (requester → dispatcher → technician → closer), and **location-based data** — patterns that don't appear in the Jira categories. Also shows how to build a domain-specific system that could optionally integrate with the project management tools.

**Data shape:**
- `WorkOrderId` (Guid), `TenantId` (Guid)
- `Title` (string), `Description` (string, multiline)
- `Building` (string — dropdown), `Floor` (string — dependent on Building), `RoomNumber` (string — dependent on Floor)
- `Category` (enum: Plumbing, Electrical, HVAC, Custodial, Grounds, Locksmith, Other)
- `Urgency` (enum: Low, Normal, High, Emergency)
- `Status` (enum: Submitted → Assigned → InProgress → OnHold → Completed → Closed)
- `AssignedTo` (string?), `AssignedTeam` (string?)
- `RequestedBy` (string), `RequestedByEmail` (string), `RequestedDate` (DateTime)
- `CompletedDate` (DateTime?), `CompletionNotes` (string?)
- `EstimatedHours` (decimal?), `ActualHours` (decimal?)
- Audit fields

**Page variants:**
- **Work Orders** — Main CRUD table: status/urgency/building/category filters, sortable columns, SLA-style urgency color coding
- **V1: Submit Request** — Public-facing form: building dropdown → floor cascading dropdown → room cascading dropdown, category selector with icons, urgency with color-coded descriptions ("Emergency: safety hazard, response within 1 hour"), description textarea, optional photo upload
- **V2: Dispatch Board** — Dispatcher view: Kanban columns by status, cards show building/room/category/urgency, drag to assign (drop on "Assigned" prompts worker selection modal), unassigned queue highlighted, urgency badges
- **V3: Technician View** — Mobile-friendly card list filtered to "my assignments": current jobs with tap-to-update-status, hours timer (start/stop), completion notes form, photo of completed work, tap to mark complete
- **V4: Facilities Dashboard** — Metrics: avg time-to-completion by category, open vs. closed trend line, busiest buildings bar chart, overdue count with drill-down, worker utilization (assigned hours vs. capacity)

**Bootstrap patterns showcased:** Cascading dependent dropdowns (3 levels), urgency color coding, mobile-optimized card actions, start/stop timer, completion photo capture

---

### 7. Budget & Approvals

**What it is:** Purchase requests with line items, calculated totals, and a multi-step approval chain. "I need to buy 10 laptops for the new lab" → line items with quantities and prices → supervisor approves → finance approves → purchase order issued.

**Why it's a good example:** Demonstrates **dynamic row add/remove** inside a form (line items), **auto-calculated fields** (line totals, grand total), and a **multi-step approval workflow** with different roles at each step. None of the Jira categories or other examples show a form where you add/remove child rows with running calculations.

**Data shape:**
- `BudgetRequestId` (Guid), `TenantId` (Guid)
- `Title` (string), `Justification` (string, multiline)
- `Department` (string), `FiscalYear` (string — "FY2025")
- `ProjectId` (Guid? — optional link to a Project from category 1)
- `RequestedBy` (string), `RequestedDate` (DateTime)
- `Status` (enum: Draft, Submitted, SupervisorApproved, FinanceReview, Approved, Denied, Completed)
- `TotalAmount` (decimal — computed sum of line items)
- `ApprovedAmount` (decimal?), `DenialReason` (string?)
- `SupervisorName` (string?), `SupervisorDate` (DateTime?)
- `FinanceReviewerName` (string?), `FinanceDate` (DateTime?)
- `AccountCode` (string — GL account)
- Audit fields

**Line item sub-record:**
- `LineItemId`, `BudgetRequestId`, `Description` (string), `Vendor` (string?), `Quantity` (int), `UnitPrice` (decimal), `LineTotal` (decimal — computed), `Category` (enum: Supplies, Equipment, Software, Travel, Services, Other), `Notes` (string?)

**Page variants:**
- **Budget Requests** — Main list: fiscal year filter, status filter, department filter, total column with currency formatting, approval status step indicator per row
- **V1: Request Builder** — Multi-line item form: add row button, remove row button per line, description + vendor + quantity + unit price fields per row, auto-calculated line total, running grand total in sticky footer. Justification textarea, GL code picker, submit for approval
- **V2: Approval Queue** — Supervisor/finance view: pending requests sorted by date, expand row to see line items inline, approve/deny buttons with comment modal, batch approve with checkboxes, "approved X of Y" counter
- **V3: Budget Overview** — Department dashboard: requested vs. approved vs. spent (stacked bar chart), by-category breakdown, fiscal year comparison, burn rate gauge, remaining budget
- **V4: Request Detail** — Print-friendly full view: header with request info, line item table, approval timeline (who approved when with comments), attached receipts, total with approved amount comparison

**Bootstrap patterns showcased:** Dynamic row add/remove, auto-calculated fields, currency formatting, sticky footer totals, approval step indicator, expandable inline detail, print-friendly layout

---

### 8. Equipment Checkout

**What it is:** Library/lab equipment lending — laptops, projectors, cameras. Check out, track due dates, check back in, track condition. "Is this projector available right now?" requires looking at the current checkout state.

**Why it's a good example:** Demonstrates a **transaction log pattern** — the same equipment record accumulates checkout/return transactions over time, and current availability is derived from the latest transaction. Also shows **condition tracking** (before/after comparison) and **due date countdown** patterns. This is fundamentally different from the project management categories where records have a single status.

**Data shape:**
- `EquipmentId` (Guid), `TenantId` (Guid)
- `Name` (string), `Description` (string?)
- `Category` (enum: Laptop, Projector, Camera, Microphone, Tablet, Hotspot, Adapter, Other)
- `SerialNumber` (string?), `AssetTag` (string — "EQ-2025-0042")
- `Location` (string — home location when not checked out)
- `Condition` (enum: New, Good, Fair, NeedsRepair, Retired)
- `PurchaseDate` (DateTime?), `PurchasePrice` (decimal?)
- `IsAvailable` (bool — computed: true if no open checkout exists)
- Audit fields

**Checkout sub-record:**
- `CheckoutId`, `EquipmentId`, `BorrowerName`, `BorrowerEmail`, `BorrowerDepartment`, `CheckoutDate` (DateTime), `DueDate` (DateTime), `ReturnDate` (DateTime?), `ConditionAtCheckout` (enum), `ConditionAtReturn` (enum?), `Notes` (string?)

**Page variants:**
- **Equipment Checkout** — Main inventory table: availability filter (Available/Checked Out/In Repair/Retired), category filter, sortable columns, availability badge per row
- **V1: Checkout Form** — Borrower autocomplete (name/email), equipment selector with real-time availability indicator, due date picker, condition assessment radio group, terms acknowledgment checkbox, signature capture (reusing existing Signature component)
- **V2: My Checkouts** — Borrower's view: current checkouts with due-date countdown badges (green >3 days, yellow 1–3 days, red overdue), return button, checkout history accordion per item
- **V3: Overdue Report** — Filtered to overdue only: days overdue calculated column (sorted most overdue first), borrower contact info, "send reminder" button per row, bulk reminder, escalation indicator (>7 days, >14 days, >30 days)
- **V4: Asset Detail** — Single equipment page: photo placeholder, specs sidebar, full checkout history as timeline (who, when, condition before/after), current status hero badge, maintenance notes log

**Bootstrap patterns showcased:** Availability badges (green/red), due-date countdown (color-coded), condition radio groups, before/after comparison, escalation tier indicators, timeline history

---

### 9. Course Evaluations (Dynamic Surveys)

**What it is:** End-of-semester student evaluations — but the form is different for every course because the questions are stored as data. An admin defines a question template (5 Likert questions + 2 free text), attaches it to a course, students fill it out, results are aggregated into averages and distributions.

**Why it's a good example:** Demonstrates **dynamic form rendering** — the page reads question definitions from the database and renders the appropriate input for each (radio buttons for Likert, textarea for free text, star widget for ratings). This "form from data" pattern is completely unique among the examples. Also demonstrates **aggregate computation** (individual responses → per-question averages) and **anonymous submission** patterns.

**Data shape:**
- `EvaluationId` (Guid), `TenantId` (Guid)
- `Title` (string — "Fall 2025 — CS 101 — Dr. Smith")
- `CourseCode` (string), `CourseName` (string), `InstructorName` (string)
- `Term` (string), `Department` (string)
- `OpenDate` (DateTime), `CloseDate` (DateTime)
- `IsAnonymous` (bool), `IsOpen` (bool — computed from dates)
- `ResponseCount` (int), `EnrollmentCount` (int)
- `TemplateId` (Guid)
- Audit fields

**Question sub-record:**
- `QuestionId`, `TemplateId`, `QuestionText`, `QuestionType` (enum: Likert5, Likert7, MultipleChoice, YesNo, FreeText, Rating10), `IsRequired` (bool), `DisplayOrder` (int), `Options` (string? — JSON for MC choices)

**Response sub-record:**
- `ResponseId`, `EvaluationId`, `SubmittedDate`, `Answers` (list of `{ QuestionId, Value }`)

**Page variants:**
- **Course Evaluations** — Admin list: term/department filter, response rate progress bar per row, open/closed badge, bulk open/close actions
- **V1: Take Evaluation** — Student-facing: dynamically renders each question by type (radio group for Likert, textarea for free text, star rating for Rating10, checkbox group for MultipleChoice), progress bar showing "question 3 of 8", submit with confirmation
- **V2: Results Summary** — Instructor/admin view per evaluation: per-question average with horizontal bar, distribution histogram (how many chose 1/2/3/4/5), free-text comments listed, response rate gauge, comparison to department average where applicable
- **V3: Template Builder** — Create/edit question templates: add question (type selector + text), drag-and-drop reorder, delete question, preview how the rendered form will look, save template with name
- **V4: Department Report** — Cross-evaluation analytics: average scores by instructor, by course, by term. Trend lines over semesters, response rate trends, lowest-rated areas flagged

**Bootstrap patterns showcased:** Dynamic form rendering by type, star rating widget, Likert radio group styling, horizontal result bars, drag-to-reorder question builder, response rate gauges

---

### 10. Employee Onboarding (Checklist Tracking)

**What it is:** New hire → checklist of tasks across multiple departments (HR files paperwork, IT provisions laptop, Facilities assigns office, Training schedules orientation). Each task is independently completable by different people. "How far along is this new hire?" = percentage of checklist completed.

**Why it's a good example:** Demonstrates **checklist/task-completion tracking** with multi-party responsibility — a pattern that applies to any process with distributed ownership (compliance audits, software releases, event planning, accreditation reviews). The **progress aggregation** ("65% complete, blocked on IT") is a different computation from anything in the Jira categories.

**Data shape:**
- `OnboardingId` (Guid), `TenantId` (Guid)
- `EmployeeName` (string), `EmployeeEmail` (string), `EmployeeTitle` (string)
- `Department` (string), `HireDate` (DateTime), `StartDate` (DateTime)
- `SupervisorName` (string), `MentorName` (string?)
- `EmploymentType` (enum: FullTime, PartTime, Temporary, GradAssistant, StudentWorker)
- `Status` (enum: Pending, InProgress, Completed, Withdrawn)
- `CompletionPercentage` (int — computed)
- `Notes` (string?)
- Audit fields

**Checklist item sub-record:**
- `ChecklistItemId`, `OnboardingId`, `TaskName`, `Description` (string?)
- `Category` (enum: HR, IT, Facilities, Department, Training, Compliance)
- `AssignedTo` (string — who's responsible)
- `DueDate` (DateTime?), `CompletedDate` (DateTime?)
- `IsRequired` (bool), `IsCompleted` (bool)
- `CompletedBy` (string?), `Notes` (string?), `DisplayOrder` (int)

**Page variants:**
- **Employee Onboarding** — Main list: department filter, status filter, completion % progress bar per row, overdue task count badge, start date sorting
- **V1: Onboarding Setup** — HR creates onboarding: employee info form → select checklist template (standard set of tasks) → customize (add/remove/reorder tasks) → assign due dates → assign responsible parties → activate
- **V2: Task Tracker** — Single-employee checklist: grouped by category (HR / IT / Facilities / Training), checkbox per task, overdue items highlighted, overall progress bar at top, responsible party shown per task, notes field per task
- **V3: My Onboarding** — New employee's self-service: personal checklist filtered to their tasks only, document upload slots (I-9, W-4, direct deposit, emergency contact), training module links with completion checkmarks, mentor contact card, "what to bring on Day 1" section
- **V4: Department Dashboard** — Manager/HR overview: all active onboardings, completion heatmap (rows = employees, columns = categories, cells = green/yellow/red), bottleneck identification (which category has the most overdue tasks), average days-to-complete trend

**Bootstrap patterns showcased:** Checklist with grouped categories, progress bar aggregation, completion heatmap, document upload slots, category-based grouping with collapse, mentor/contact cards

---

## Summary Grid

| # | Category | Type | Core Data Pattern | Key UI Innovation | Unique Bootstrap Feature |
|---|---|---|---|---|---|
| 1 | Projects & Hierarchy | 🔧 Jira | Recursive parent-child tree | Tree view with drill-down | Nested expand/collapse + breadcrumbs |
| 2 | Tickets | 🔧 Jira | Complex multi-field entity | 15+ field multi-section form | Fibonacci selector + tag input + markdown |
| 3 | Board Views | 🔧 Jira | Configurable view over tickets | Kanban + swimlanes + WIP | Configurable columns + density toggle |
| 4 | Sprint Planning | 🔧 Jira | Time-boxed container | Drag-from-backlog + burndown | Capacity gauge + velocity trend |
| 5 | Backlog & Grooming | 🔧 Jira | Prioritized queue | Inline edit + bulk ops | Floating action bar + saved filter tabs |
| 6 | Work Orders | 🏢 Domain | Status workflow + assignment | Cascading 3-level dropdowns | Urgency color coding + dispatch board |
| 7 | Budget & Approvals | 🏢 Domain | Parent + N line items + totals | Dynamic row add/remove | Sticky footer totals + approval steps |
| 8 | Equipment Checkout | 🏢 Domain | Transaction log + availability | Due-date countdown badges | Condition before/after + escalation tiers |
| 9 | Course Evaluations | 🏢 Domain | Dynamic form from question data | Form renderer by question type | Star rating + Likert bars + drag builder |
| 10 | Employee Onboarding | 🏢 Domain | Checklist with multi-party owners | Progress aggregation + heatmap | Category-grouped checklist + upload slots |

---

## How They Connect

```
┌─────────────────────────────────────────────────────┐
│                 JIRA-LIKE SYSTEM                     │
│                                                      │
│  Projects ──┬── Tickets ──── Board Views             │
│     └── Sub-Projects   │                             │
│                        ├── Sprint Planning            │
│                        └── Backlog & Grooming         │
│                                                      │
│  Optional links:                                     │
│  - Budget Request → Project (fund a project)         │
│  - Work Order → Ticket (escalate to dev team)        │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│              STANDALONE WORKFLOWS                     │
│                                                      │
│  Work Orders       (facilities — their own workflow) │
│  Budget & Approvals (finance — line items + chain)   │
│  Equipment Checkout (lending — transaction history)  │
│  Course Evaluations (surveys — dynamic forms)        │
│  Employee Onboarding (checklists — multi-party)      │
└─────────────────────────────────────────────────────┘
```

Categories 1–5 share the Project and Ticket data model. Categories 6–10 are independent entities. Optionally, Budget Requests can link to a Project (funding the work), and Work Orders can generate a Ticket (when facilities needs dev work).

---

## Implementation Notes

### Storage: Generic JSON Record Store (doc 113)

**No EF migrations. No new database tables.** All entities use the generic `JsonRecord` envelope store — a single in-memory `ConcurrentDictionary<Guid, JsonRecord>` shared by every entity type.

Each entity is a strongly typed C# class implementing `IJsonEntity`. When saved, it's JSON-serialized into the `Contents` field of a `JsonRecord` envelope that carries metadata (`RecordType`, `SchemaVersion`, `Format`). When read, metadata is checked first (two-phase parse), then `Contents` is deserialized into the typed entity.

**Schema changes are free:** Update the C# class, bump `CurrentSchemaVersion`. Old blobs with missing fields just default to null/0/false. Blobs from a future version are skipped gracefully.

**Sub-records (comments, line items, checklist items) are embedded** in the parent entity's JSON — not stored as separate records. One store read = one complete entity with all its children.

**Full design:** See [113_decision_json_record_store.md](Docs/113_decision_json_record_store.md)

### File Naming Convention

Each category follows the established patterns:
- **DataObjects:** `FreeExamples.App.DataObjects.{Category}.cs` — entity class implementing `IJsonEntity`, enums, filter DTO
- **DataAccess:** `FreeExamples.App.DataAccess.JsonStore.cs` — one file for all generic CRUD (shared)
- **DataAccess Seed:** `FreeExamples.App.DataAccess.JsonStore.Seed.cs` — seed methods for all entity types
- **API Endpoints:** `FreeExamples.App.API.JsonStore.cs` — three endpoints per entity (thin wrappers over generic CRUD)
- **Pages:** `FreeExamples.App.Pages.{Category}.razor` + V1–V4 variants, each with `ExampleNav`, `InfoTip`, `AboutSection`
- **No external dependencies:** Bootstrap 5 + Font Awesome + vanilla Blazor

### Build Order Recommendation

**Phase 1 — Foundation:** Projects (#1) + Tickets (#2) — the core data model everything else references
**Phase 2 — Visualization:** Board Views (#3) + Backlog (#5) — the most-requested Jira features
**Phase 3 — Planning:** Sprint Planning (#4) — completes the Jira-like system
**Phase 4 — Domain:** Work Orders (#6) + Budget (#7) — highest-value standalone workflows
**Phase 5 — Specialty:** Equipment (#8) + Evaluations (#9) + Onboarding (#10) — unique form patterns
