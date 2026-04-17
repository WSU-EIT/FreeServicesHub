# 604 — Phase 3: Dashboard UI & E2E Testing

> **Document ID:** 604  
> **Category:** Phase Detail / Implementation Guide  
> **Parent:** [601_action_plan.md](601_action_plan.md)  
> **Prerequisites:** [602_phase1_data_contracts.md](602_phase1_data_contracts.md) (HubJob DTO), [603_phase2_agent_security.md](603_phase2_agent_security.md) (Job polling loop, auth verified)  
> **Purpose:** Add a Job Queue panel to the existing Agent Dashboard, build the Agent Provisioning Wizard, wire SignalR job-complete reactivity, and create Playwright E2E scripts.  
> **Audience:** Developers and AI Agents in Execution Mode.  
> **Outcome:** 📖 Users see live job status on the dashboard, can provision agents via wizard, and CI/CD runs Playwright against Hub pages.

---

## 1. What Already Exists (The "Before")

The UI layer is **production-grade**, not a stub. The codebase exploration revealed four fully implemented Blazor pages:

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │              EXISTING UI PAGES — ALL IMPLEMENTED                     │
  │                                                                      │
  │  Pages/App/                                                          │
  │  ──────────                                                          │
  │  FreeServicesHub.App.AgentDashboard.razor      ✅ COMPLETE           │
  │  ├── Summary cards (Online/Warning/Error/Offline counts)             │
  │  ├── Filter & Sort toolbar (name, host, OS, status)                  │
  │  ├── Agent card grid (CPU/MEM/DISK progress bars per card)           │
  │  ├── Real-time SignalR updates (AgentHeartbeat, StatusChanged)       │
  │  ├── Click-to-detail navigation                                      │
  │  └── About section with help text                                    │
  │                                                                      │
  │  FreeServicesHub.App.AgentManagement.razor     ✅ COMPLETE           │
  │  ├── Add New Agent panel (key generation + install instructions)     │
  │  ├── Registration key list with "copy to clipboard"                   │
  │  ├── Agent table (view, soft-delete, API token management)           │
  │  └── Status messages with dismissible alerts                         │
  │                                                                      │
  │  FreeServicesHub.App.AgentSettings.razor       ✅ COMPLETE           │
  │  ├── Filter & search toolbar (name, service, status)                 │
  │  ├── Sortable data table (all Windows Service metadata)              │
  │  ├── Inline editing (heartbeat interval, agent name, hub URL)        │
  │  ├── Pagination (10/25/50/100 per page)                              │
  │  └── Real-time SignalR updates (AgentSettingsReport)                 │
  │                                                                      │
  │  FreeServicesHub.App.BackgroundServices.razor  ✅ COMPLETE           │
  │  ├── Service cards (Agent Monitor, Dev Key Seeder, Bg Processor)     │
  │  ├── Live-scrolling log console with auto-scroll                     │
  │  ├── Log level filter (Info/Warn/Error) + service filter             │
  │  └── Search within log messages                                      │
  │                                                                      │
  │  Client/Helpers.App.cs                         ✅ COMPLETE           │
  │  ├── AppIcons: AgentDashboard, AgentHeartbeat, AgentApiKey, etc.     │
  │  ├── MenuItemsApp: Dashboard(100), Background(110), Settings(120)    │
  │  ├── MenuItemsAdminApp: Agent Management(10, AppAdminOnly)           │
  │  ├── ProcessSignalRUpdateApp: 7 update types handled                 │
  │  └── ReloadModelApp: Hydrates Model.AgentStatuses                    │
  │                                                                      │
  │  Client/DataModel.App.cs                       ✅ COMPLETE           │
  │  ├── AgentStatuses list property with change notification            │
  │  └── PrecompileBlazorPlugins = false                                 │
  └──────────────────────────────────────────────────────────────────────┘
```

### Key Insight

The Dashboard, Agent Management, Agent Settings, and Background Services pages **already exist and are feature-rich**. Phase 3 is about **extending** these pages with Job Queue visibility and adding E2E test coverage — not building from scratch.

---

## 2. Task Breakdown (Parallelization)

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │              PHASE 3 TASK DEPENDENCY GRAPH                           │
  │                                                                      │
  │  ┌─────────────┐   ┌──────────────┐   ┌──────────────┐             │
  │  │ Task 1      │   │ Task 2       │   │ Task 3       │  ALL THREE  │
  │  │ Job Queue   │   │ Provisioning │   │ Playwright   │  CAN RUN IN │
  │  │ panel       │   │ Wizard       │   │ E2E scripts  │  PARALLEL   │
  │  └──────┬──────┘   └──────┬───────┘   └──────┬───────┘             │
  │         │                 │                   │                      │
  │         └────────┬────────┘                   │                      │
  │                  ▼                            │                      │
  │         ┌─────────────────┐                   │                      │
  │         │ Task 4          │   DEPENDS ON 1+2  │                      │
  │         │ SignalR job     │                   │                      │
  │         │ reactivity     │                    │                      │
  │         └────────┬────────┘                   │                      │
  │                  │                            │                      │
  │                  └──────────┬─────────────────┘                      │
  │                             ▼                                        │
  │                    ┌─────────────────┐                               │
  │                    │ Task 5          │   DEPENDS ON 1-4              │
  │                    │ CI/CD pipeline  │                               │
  │                    │ integration     │                               │
  │                    └─────────────────┘                               │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## 3. Task Details

### Task 1: Job Queue Panel on Agent Dashboard

**File:** `FreeServicesHub/FreeServicesHub.Client/Pages/App/FreeServicesHub.App.AgentDashboard.razor` (MODIFY)  
**Parallel with:** Tasks 2 and 3

The existing dashboard has summary cards (Online/Warning/Error/Offline) at the top, then agent cards. We add a **Job Queue section** between the summary cards and the agent grid.

**BEFORE** (dashboard structure — actual):
```
┌─────────────────────────────────────────────────┐
│  Summary Cards (Online | Warning | Error | OFF) │
├─────────────────────────────────────────────────┤
│  Filter & Sort Toolbar                          │
├─────────────────────────────────────────────────┤
│  Agent Card Grid (CPU/MEM/DISK per card)        │
└─────────────────────────────────────────────────┘
```

**AFTER** (with Job Queue panel):
```
┌─────────────────────────────────────────────────┐
│  Summary Cards (Online | Warning | Error | OFF) │
├─────────────────────────────────────────────────┤
│  Job Queue Summary (NEW)                        │
│  ┌──────┬──────┬──────┬──────┬──────┬──────┐   │
│  │Queued│Assign│Runnin│Compl │Failed│Cancel│   │
│  │  12  │  3   │  2   │  47  │  1   │  0   │   │
│  └──────┴──────┴──────┴──────┴──────┴──────┘   │
├─────────────────────────────────────────────────┤
│  Recent Jobs Table (NEW — last 10 active)       │
│  JobType | Agent | Status | Created | Duration  │
├─────────────────────────────────────────────────┤
│  Filter & Sort Toolbar (existing)               │
├─────────────────────────────────────────────────┤
│  Agent Card Grid (existing, unchanged)          │
└─────────────────────────────────────────────────┘
```

**Pseudo code for the new Job Queue section** (Razor):
```razor
@* ═══════════ Job Queue Summary ═══════════ *@
<section aria-label="Job queue summary" class="mb-4">
    <h5><i class="fa-solid fa-list-check me-1" aria-hidden="true"></i> Job Queue</h5>
    <div class="row g-3 mb-3">
        @foreach (var status in _jobStatusCounts) {
            var color = status.Key switch {
                "Queued" => "info",
                "Assigned" => "primary",
                "Running" => "warning",
                "Completed" => "success",
                "Failed" => "danger",
                "Cancelled" => "secondary",
                _ => "light"
            };
            <div class="col-4 col-md-2">
                <div class="card text-center border-@color">
                    <div class="card-body py-2">
                        <h4 class="text-@color mb-0">@status.Value</h4>
                        <small class="text-muted">@status.Key</small>
                    </div>
                </div>
            </div>
        }
    </div>

    @* Recent active jobs table *@
    @if (_recentJobs.Any()) {
        <div class="table-responsive">
            <table class="table table-sm table-hover" aria-label="Recent jobs">
                <thead class="table-light">
                    <tr>
                        <th>Type</th>
                        <th>Agent</th>
                        <th>Status</th>
                        <th>Created</th>
                        <th>Duration</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var job in _recentJobs) {
                        <tr class="@GetJobRowClass(job.Status)">
                            <td>@job.JobType</td>
                            <td>@job.AssignedAgentName</td>
                            <td><span class="badge @GetJobStatusBadge(job.Status)">@job.Status</span></td>
                            <td><time datetime="@job.Created.ToString("o")">@job.Created.ToString("g")</time></td>
                            <td>@GetJobDuration(job)</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
</section>
```

**Code-behind additions** (in the `@code` block):
```csharp
private List<DataObjects.HubJob> _recentJobs = new();
private Dictionary<string, int> _jobStatusCounts = new();

private async Task LoadJobData()
{
    var jobs = await Http.GetFromJsonAsync<List<DataObjects.HubJob>>(
        $"api/data/jobs?tenantId={Model.TenantId}");

    _recentJobs = (jobs ?? new())
        .Where(j => j.Status != DataObjects.HubJobStatuses.Completed
                  && j.Status != DataObjects.HubJobStatuses.Cancelled)
        .OrderByDescending(j => j.Created)
        .Take(10)
        .ToList();

    _jobStatusCounts = (jobs ?? new())
        .GroupBy(j => j.Status)
        .ToDictionary(g => g.Key, g => g.Count());
}

private string GetJobDuration(DataObjects.HubJob job)
{
    if (job.StartedAt == null) return "—";
    DateTime end = job.CompletedAt ?? DateTime.UtcNow;
    TimeSpan dur = end - job.StartedAt.Value;
    return dur.TotalMinutes < 1 ? $"{dur.Seconds}s" : $"{dur.TotalMinutes:F0}m {dur.Seconds}s";
}
```

**Pass criteria:**
- Job counts render in the summary cards section
- Recent jobs table shows the 10 most recent non-completed jobs
- Page compiles and loads without JS errors
- Existing agent cards are unaffected

---

### Task 2: Agent Provisioning Wizard Enhancement

**File:** `FreeServicesHub/FreeServicesHub.Client/Pages/App/FreeServicesHub.App.AgentManagement.razor` (MODIFY)  
**Parallel with:** Tasks 1 and 3  
**Reference:** [008_components.wizard.md](008_components.wizard.md)

The existing Agent Management page already has a "Register a New Agent" panel with step-by-step key generation and install instructions. The enhancement adds a **multi-step wizard** using the `Wizard` component pattern from [008_components.wizard.md](008_components.wizard.md).

**BEFORE** (actual — simplified):
```
┌─────────────────────────────────────────────┐
│  [Generate Keys] button                     │
│  ↓                                          │
│  Keys displayed + install instructions      │
│  (all in one panel, not stepped)            │
└─────────────────────────────────────────────┘
```

**AFTER** (wizard flow):
```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │              AGENT PROVISIONING WIZARD — 4 STEPS                     │
  │                                                                      │
  │  Step 1: Generate          Step 2: Configure                        │
  │  ────────────────          ──────────────────                       │
  │  [How many keys?]          [Copy key shown]                         │
  │  [Generate] button         [Show appsettings.json template]         │
  │                            [Verify Hub URL is correct]              │
  │        ▼                          ▼                                 │
  │  Step 3: Install           Step 4: Verify                          │
  │  ──────────────            ────────────────                         │
  │  [Show sc create cmd]     [Auto-poll for agent connection]         │
  │  [Show console mode cmd]  [✅ "Agent connected!" on success]       │
  │  [Link to Installer docs] [⚠ "Not yet connected" + retry]         │
  │                                                                      │
  │  ◄─── [Back]     [Next] ───►     [Finish]                          │
  └──────────────────────────────────────────────────────────────────────┘
```

**Pseudo code for Step 4 (verification):**
```csharp
// New method in AgentManagement.razor @code block
private bool _wizardVerifying = false;
private bool _wizardVerified = false;

private async Task VerifyAgentConnection()
{
    _wizardVerifying = true;
    StateHasChanged();

    // Poll for 60 seconds to see if the agent registered
    int maxAttempts = 12;
    for (int i = 0; i < maxAttempts; i++)
    {
        var agents = await Http.GetFromJsonAsync<List<DataObjects.Agent>>(
            $"api/data/agents?tenantId={Model.TenantId}");

        // Check if any agent was registered using our key prefix
        var match = agents?.FirstOrDefault(a =>
            a.RegisteredBy?.Contains(_selectedKeyPrefix) == true
            && a.RegisteredAt > _keyGeneratedAt);

        if (match != null)
        {
            _wizardVerified = true;
            _wizardVerifying = false;
            _statusMessage = $"Agent '{match.Name}' connected successfully!";
            _statusAlertClass = "alert-success";
            StateHasChanged();
            return;
        }

        await Task.Delay(5000); // 5-second intervals
    }

    _wizardVerifying = false;
    _statusMessage = "Agent has not connected yet. It may still be starting up.";
    _statusAlertClass = "alert-warning";
    StateHasChanged();
}
```

**Pass criteria:**
- Wizard renders 4 steps with Back/Next/Finish navigation
- Step 1 generates keys (reuses existing `GenerateKeys()`)
- Step 2 shows copyable key + appsettings template
- Step 3 shows install commands (reuses existing instructions)
- Step 4 auto-polls for agent connection for 60s
- Existing functionality (key generation, agent table) is preserved

---

### Task 3: Playwright E2E Scripts

**File:** `FreeTools/` or `FreeServicesHub.Tests.Integration/` (NEW)  
**Parallel with:** Tasks 1 and 2  
**Reference:** [007_patterns.playwright.md](007_patterns.playwright.md)

These scripts navigate the Hub's custom pages, ensuring the UI renders correctly and responds to user actions.

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │              PLAYWRIGHT TEST COVERAGE MAP                            │
  │                                                                      │
  │  Test Script                    Pages Covered        Actions         │
  │  ────────────────────────────   ────────────────     ───────────     │
  │  HubDashboard_E2E.cs           /AgentDashboard      Load, Filter,   │
  │                                                      Sort, Click     │
  │                                                      agent card      │
  │                                                                      │
  │  HubManagement_E2E.cs          /AgentManagement      Generate key,  │
  │                                                      Copy, Close     │
  │                                                      panel           │
  │                                                                      │
  │  HubSettings_E2E.cs            /AgentSettings        Load, Filter,  │
  │                                                      Sort, Paginate │
  │                                                                      │
  │  HubBackgroundServices_E2E.cs  /BackgroundServices   Load, Filter   │
  │                                                      logs, Toggle   │
  │                                                      auto-scroll    │
  │                                                                      │
  │  HubJobQueue_E2E.cs (NEW)      /AgentDashboard       Job counts,   │
  │                                                      Job table,     │
  │                                                      Status badges  │
  └──────────────────────────────────────────────────────────────────────┘
```

**Pseudo code for `HubDashboard_E2E.cs`:**
```csharp
using Microsoft.Playwright;

[TestClass]
public class HubDashboard_E2E
{
    private IPlaywright _playwright;
    private IBrowser _browser;
    private IPage _page;

    [TestInitialize]
    public async Task Setup()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        _page = await _browser.NewPageAsync();
    }

    [TestMethod]
    public async Task Dashboard_LoadsWithSummaryCards()
    {
        await _page.GotoAsync("https://localhost:7271/AgentDashboard");
        await _page.WaitForSelectorAsync("[aria-label='Agent status summary']");

        // Verify 4 summary cards are present
        var cards = await _page.QuerySelectorAllAsync(
            "section[aria-label='Agent status summary'] .card");
        Assert.AreEqual(4, cards.Count);
    }

    [TestMethod]
    public async Task Dashboard_FilterByStatus()
    {
        await _page.GotoAsync("https://localhost:7271/AgentDashboard");
        await _page.WaitForSelectorAsync("#agentDashStatusFilter");

        await _page.SelectOptionAsync("#agentDashStatusFilter",
            new SelectOptionValue { Value = "Online" });

        // Verify filtered count updates
        var countText = await _page.TextContentAsync(
            "[aria-live='polite']");
        Assert.IsTrue(countText.Contains("of"));
    }

    [TestMethod]
    public async Task Dashboard_ClickAgentCard_Navigates()
    {
        await _page.GotoAsync("https://localhost:7271/AgentDashboard");
        await _page.WaitForSelectorAsync(".agent-card");

        var firstCard = await _page.QuerySelectorAsync(".agent-card");
        if (firstCard != null) {
            await firstCard.ClickAsync();
            // Should navigate to agent detail view
            await _page.WaitForURLAsync("**/AgentDashboard/**");
        }
    }

    [TestMethod]
    public async Task Dashboard_JobQueueSection_Renders()
    {
        await _page.GotoAsync("https://localhost:7271/AgentDashboard");
        await _page.WaitForSelectorAsync(
            "section[aria-label='Job queue summary']");

        // Verify job status count cards exist
        var jobCards = await _page.QuerySelectorAllAsync(
            "section[aria-label='Job queue summary'] .card");
        Assert.IsTrue(jobCards.Count >= 1); // At least one status shown
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }
}
```

**Pseudo code for `HubManagement_E2E.cs`:**
```csharp
[TestMethod]
public async Task Management_GenerateRegistrationKey()
{
    await _page.GotoAsync("https://localhost:7271/AgentManagement");
    await _page.WaitForSelectorAsync("[aria-label='Page actions']");

    // Click "Add New Agent" button
    await _page.ClickAsync("text=Add New Agent");
    await _page.WaitForSelectorAsync("#addAgentPanel");

    // Click "Generate Key" — exact button depends on implementation
    await _page.ClickAsync("text=Generate Key");

    // Wait for key to appear
    await _page.WaitForSelectorAsync("code.font-monospace");

    var keyText = await _page.TextContentAsync("code.font-monospace");
    Assert.IsFalse(string.IsNullOrWhiteSpace(keyText));
    Assert.IsTrue(keyText.Length > 20); // Base64 key should be long
}
```

**Pass criteria:**
- All E2E tests pass in headless Chromium
- Tests use accessible selectors (aria-label, role, id) not fragile CSS
- Tests are idempotent (no leftover state between runs)
- Each test completes in under 30 seconds

---

### Task 4: SignalR Job Reactivity

**Files:**
- `FreeServicesHub/FreeServicesHub.Client/Helpers.App.cs` (MODIFY)
- `FreeServicesHub/FreeServicesHub.Client/DataModel.App.cs` (MODIFY)

**Depends on:** Tasks 1 and 2 (dashboard needs the job panel to exist)

**BEFORE** (Helpers.App.cs — actual `ProcessSignalRUpdateApp`):
```csharp
// Handles: AgentHeartbeat, AgentConnected, AgentDisconnected,
//          AgentStatusChanged, BackgroundServiceLog,
//          AgentSettingsReport, AgentSettingsUpdated
```

**AFTER** (add JobUpdated and JobCompleted):
```csharp
case DataObjects.SignalRUpdateType.JobUpdated:
case DataObjects.SignalRUpdateType.JobCompleted:
    // Refresh job data on the dashboard
    if (Model.View == "AgentDashboard")
    {
        // Trigger a reload of the job section only
        Model.NotifyJobQueueChanged();
    }
    break;
```

**BEFORE** (DataModel.App.cs — actual properties):
```csharp
List<DataObjects.Agent> _AgentStatuses
List<string> _MyValues
```

**AFTER** (add job queue cache):
```csharp
List<DataObjects.Agent> _AgentStatuses
List<string> _MyValues
List<DataObjects.HubJob> _ActiveJobs    // ← NEW

public List<DataObjects.HubJob> ActiveJobs {
    get { return _ActiveJobs; }
    set {
        if (!ObjectsAreEqual(_ActiveJobs, value)) {
            _ActiveJobs = value;
            NotifyDataChanged();
        }
    }
}

// Event for targeted job queue refresh (avoids full page reload)
public event Action? OnJobQueueChanged;
public void NotifyJobQueueChanged() => OnJobQueueChanged?.Invoke();
```

**Pass criteria:**
- When a job status changes, the dashboard updates within 2 seconds (SignalR push)
- No full page reload — only the job section re-renders
- Background Services page is unaffected

---

### Task 5: CI/CD Pipeline Integration

**File:** `Pipelines/` (MODIFY — add Playwright step)  
**Depends on:** Tasks 1-4  

**Pseudo code for pipeline step** (YAML):
```yaml
# Add to existing CI/CD pipeline
- task: DotNetCoreCLI@2
  displayName: 'Install Playwright browsers'
  inputs:
    command: 'custom'
    custom: 'tool'
    arguments: 'run playwright install chromium'

- task: DotNetCoreCLI@2
  displayName: 'Run Playwright E2E tests'
  inputs:
    command: 'test'
    projects: '**/FreeServicesHub.Tests.Integration.csproj'
    arguments: '--filter "Category=E2E" --logger trx'
  env:
    HUB_URL: 'https://localhost:7271'
    PLAYWRIGHT_BROWSERS_PATH: '$(Agent.TempDirectory)/playwright-browsers'

- task: PublishTestResults@2
  displayName: 'Publish E2E test results'
  inputs:
    testResultsFormat: 'VSTest'
    testResultsFiles: '**/*.trx'
  condition: always()
```

**Pass criteria:**
- Pipeline installs Playwright browsers
- E2E tests run against a deployed Hub instance
- Test results published to pipeline artifacts
- Pipeline fails if any E2E test fails

---

## 4. Full Page Interaction Diagram

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │          PHASE 3 — UI INTERACTION MAP (All Pages)                    │
  │                                                                      │
  │  ┌─────────────────────────────────────────────────────────────┐     │
  │  │                    Blazor Client                            │     │
  │  │                                                             │     │
  │  │  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐   │     │
  │  │  │ AgentDash    │  │ AgentMgmt    │  │ BackgroundSvcs  │   │     │
  │  │  │ ──────────── │  │ ──────────── │  │ ─────────────── │   │     │
  │  │  │ Agent cards  │  │ Key gen      │  │ Service cards   │   │     │
  │  │  │ Job counts☆  │  │ Wizard☆      │  │ Log console     │   │     │
  │  │  │ Job table☆   │  │ Agent table  │  │                 │   │     │
  │  │  └──────┬───────┘  └──────┬───────┘  └────────┬────────┘   │     │
  │  │         │                 │                    │            │     │
  │  │         └────────┬────────┘────────────────────┘            │     │
  │  │                  ▼                                          │     │
  │  │  ┌──────────────────────────────────────────────────────┐   │     │
  │  │  │              Helpers.App.cs                           │   │     │
  │  │  │  ProcessSignalRUpdateApp()                           │   │     │
  │  │  │  ├── AgentHeartbeat → update agent cards             │   │     │
  │  │  │  ├── AgentStatusChanged → update status badges       │   │     │
  │  │  │  ├── JobUpdated☆ → refresh job panel                 │   │     │
  │  │  │  ├── JobCompleted☆ → flash green on job row          │   │     │
  │  │  │  └── BackgroundServiceLog → append to log console    │   │     │
  │  │  └────────────────────────┬─────────────────────────────┘   │     │
  │  │                           │                                 │     │
  │  │  ┌────────────────────────┴─────────────────────────────┐   │     │
  │  │  │              DataModel.App.cs                         │   │     │
  │  │  │  AgentStatuses: List<Agent>                          │   │     │
  │  │  │  ActiveJobs☆: List<HubJob>                           │   │     │
  │  │  │  OnJobQueueChanged☆: event Action                    │   │     │
  │  │  └─────────────────────────────────────────────────────-┘   │     │
  │  └─────────────────────────────────────────────────────────────┘     │
  │         │ HTTP / SignalR                                             │
  │         ▼                                                            │
  │  ┌─────────────────────────────────────────────────────────────┐     │
  │  │                    Hub Server                               │     │
  │  │  Controllers/                                               │     │
  │  │  ├── DataController.App.cs → GetJobs, SaveJobs, DeleteJobs │     │
  │  │  └── FreeServicesHub.App.API.cs → Agent job fetch/complete │     │
  │  │                                                             │     │
  │  │  Hubs/signalrHub.cs → broadcast JobUpdated/JobCompleted    │     │
  │  │                                                             │     │
  │  │  AgentMonitorService.cs → poll & broadcast status changes  │     │
  │  └─────────────────────────────────────────────────────────────┘     │
  │                                                                      │
  │  ☆ = NEW in Phase 3                                                 │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## 5. Files Modified Per Project

```ascii
  ┌──────────────────────────────────────────────────────────────────────┐
  │            PHASE 3 — FILES TOUCHED PER PROJECT                       │
  │                                                                      │
  │  Client (4 files)                                                    │
  │  ├── Pages/App/FreeServicesHub.App.AgentDashboard.razor  MODIFY     │
  │  ├── Pages/App/FreeServicesHub.App.AgentManagement.razor MODIFY     │
  │  ├── Helpers.App.cs .............................. MODIFY (SignalR)  │
  │  └── DataModel.App.cs ........................... MODIFY (ActiveJobs)│
  │                                                                      │
  │  Tests.Integration (5 files)                                         │
  │  ├── HubDashboard_E2E.cs ........................ NEW               │
  │  ├── HubManagement_E2E.cs ....................... NEW               │
  │  ├── HubSettings_E2E.cs ......................... NEW               │
  │  ├── HubBackgroundServices_E2E.cs ............... NEW               │
  │  └── HubJobQueue_E2E.cs ......................... NEW               │
  │                                                                      │
  │  Pipelines (1 file)                                                  │
  │  └── ci-cd.yml (or equivalent) .................. MODIFY            │
  │                                                                      │
  │  TOTAL: 5 new files, 5 modified files                                │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## 6. How to Test

| Test | Command | Expected |
|------|---------|----------|
| Dashboard loads with jobs | Navigate to `/AgentDashboard` | Job count cards + recent jobs table visible |
| Job counts update live | Create a job via API, watch dashboard | Count increments within 2s |
| Wizard Step 4 verifies | Generate key → run agent → watch wizard | "Connected!" within 60s |
| Playwright dashboard | `dotnet test --filter "HubDashboard_E2E"` | All tests green |
| Playwright management | `dotnet test --filter "HubManagement_E2E"` | Key generated, copied |
| Playwright settings | `dotnet test --filter "HubSettings_E2E"` | Table renders, sorts |
| Playwright bg services | `dotnet test --filter "HubBackgroundServices_E2E"` | Logs stream, filter works |
| Full pipeline | Push to CI/CD branch | E2E step passes, results published |

---

## 7. Accessibility Notes

The existing pages already demonstrate excellent accessibility patterns. All new code must maintain:

| Pattern | Existing Example | New Code Must... |
|---------|-----------------|------------------|
| `aria-label` on sections | `<section aria-label="Agent status summary">` | Add `aria-label="Job queue summary"` |
| `aria-live="polite"` | Status message regions | Add to job count updates |
| `role="progressbar"` | CPU/MEM bars | N/A (no new progress bars) |
| Keyboard navigation | `@onkeydown` on agent cards | Add to wizard step buttons |
| `<time datetime>` | Heartbeat timestamps | Use for job Created/Completed |
| Semantic headings | `<h1>` → `<h5>` hierarchy | Job section uses `<h5>` |

---

## 🤖 AI Analysis

> **Agent:** `4/15/2026/opus/4.6`

**The UI is the most mature part of this project.** The AgentDashboard alone has summary cards, filter/sort toolbars, agent card grids with live CPU/MEM/DISK progress bars, SignalR real-time updates, click-to-detail navigation, accessible markup (aria-labels, roles, keyboard handlers), and an About section. Agent Management has a complete key generation flow with copy-to-clipboard and installation instructions. These are not stubs — they are production Blazor pages.

**Phase 3 is the lightest implementation phase.** The dashboard job panel is the only structural change — it adds a new `<section>` before the existing agent grid. The wizard enhancement wraps existing key-generation logic in stepped navigation. The Playwright scripts are new files but test existing functionality.

**The real value of Phase 3 is E2E testability.** The Hub has zero automated UI tests today. Adding Playwright coverage catches regressions in the existing agent dashboard, management, settings, and background services pages — not just the new job panel. This is the highest ROI task in the entire action plan.
