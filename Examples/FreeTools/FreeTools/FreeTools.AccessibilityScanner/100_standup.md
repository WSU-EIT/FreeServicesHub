# 100 — Meeting: AccessibilityScanner Kickoff Standup

> **Document ID:** 100  
> **Category:** Meeting  
> **Purpose:** Kickoff discussion for FreeTools.AccessibilityScanner — ADA/WCAG compliance scanning integrated into the Aspire pipeline  
> **Attendees:** [CTO], [Lead], [Backend], [Frontend], [Quality], [Sanity]  
> **Date:** 2025-07-24  
> **Predicted Outcome:** Aligned on architecture, tool selection, CSV output format, and reporter integration  
> **Actual Outcome:** *(update after implementation)*  
> **Resolution:** *(link to PR when done)*

---

## Context

**[Lead]:** Alright team, CTO dropped a big one on us. The directive from upper management is clear: **every site we build needs to be fully ADA compliant**. Internal tools, external sites, even if devs are the only users. No exceptions.

**[Lead]:** I've spent time reviewing two research documents we compiled — `ada_compliance_research_1.md` and `ada_compliance_research_2.md`. They're thorough. The short version: "ADA compliance" for websites means meeting **WCAG 2.1 Level AA** (or 2.2 AA for future-proofing). The research identified three primary scanning tools, a unified output format, and two implementation approaches — pure .NET with Playwright+axe, and CLI tool orchestration. Both have merit.

**[Lead]:** The goal from CTO is not just "scan a site." It's: integrate this into FreeTools as a new pipeline step — **after** BrowserSnapshot and EndpointPoker finish, **before** WorkspaceReporter generates the final report. The scanner feeds its results into a CSV. The reporter reads that CSV and builds a cross-tool comparison table showing which issues were found by multiple tools versus just one, then ranks them by consensus severity.

**[Lead]:** I've also reviewed every `.csproj` and `Program.cs` in the FreeTools suite to understand our patterns. Let me bring the team up to speed and let's hash this out.

---

## Discussion

### Architecture & Pipeline Placement

**[Architect]:** Let me frame this. The current Aspire pipeline in `FreeTools.AppHost\Program.cs` runs in four phases:

```
Phase 1 (parallel):  EndpointMapper + WorkspaceInventory  (static analysis)
Phase 2 (waits):     EndpointPoker                        (HTTP testing, needs web app + routes)
Phase 3 (waits):     BrowserSnapshot                      (Playwright screenshots, needs web app + routes)
Phase 4 (waits):     WorkspaceReporter                    (aggregates everything, waits for all above)
```

AccessibilityScanner slots in as a **new Phase 3.5** — or honestly, it can run in parallel with BrowserSnapshot since it also needs the web app running and the routes CSV. Both use Playwright. Both hit the live site. The key constraint is it must complete before WorkspaceReporter starts.

**[Backend]:** Looking at how AppHost wires things up, the pattern is clear. Each tool is a standalone console app, gets environment variables for config, and uses `WaitFor` / `WaitForCompletion` for ordering. So AccessibilityScanner would be:

```csharp
var a11yScanner = builder.AddProject<Projects.FreeTools_AccessibilityScanner>("a11y-scanner")
    .WithEnvironment("BASE_URL", webApp.GetEndpoint("https"))
    .WithEnvironment("CSV_PATH", projectConfig.PagesCsv)
    .WithEnvironment("OUTPUT_DIR", projectConfig.LatestDir)
    .WithEnvironment("START_DELAY_MS", (WebAppStartupDelayMs + HttpToolDelayMs).ToString())
    .WaitFor(webApp)
    .WaitForCompletion(endpointMapper);
```

Then the reporter gets a new `WaitForCompletion(a11yScanner)` added to its chain.

**[Architect]:** Exactly. And the output would be something like `accessibility-issues.csv` in the `latest/` folder alongside `pages.csv` and `workspace-inventory.csv`. Reporter already knows how to read CSVs from that folder.

**[Sanity]:** Mid-check — before we go further. The CTO said the org might pick a specific tool later. So the architecture needs to be **modular**. We can't hard-wire to just axe or just Pa11y. The tool adapters need to be swappable.

**[Lead]:** Correct. That's a core requirement. The research identified three tools that each bring something different:

---

### Tool Selection & What Each Brings

**[Lead]:** From the research, here are the three tools and why each matters:

| Tool | What It Does | Strengths | How It Runs |
|------|-------------|-----------|-------------|
| **Deque.AxeCore.Playwright** | In-process axe-core via Playwright | Industry standard engine, ~57% automated catch rate, NuGet native, type-safe | C# in-process — we already have Playwright from BrowserSnapshot |
| **Pa11y** (`pa11y`) | HTML_CodeSniffer-based CLI | Used by UK gov, BBC. Different rule engine than axe so catches different things | Node.js CLI — execute via `Process`, parse JSON stdout |
| **Google Lighthouse** (`lighthouse`) | Chrome's accessibility audit | High-level scoring, good for "overall grade", catches some unique issues | Node.js CLI — execute via `Process`, parse JSON file |

**[Backend]:** So two of the three are external CLI tools requiring Node.js, and one is pure .NET via NuGet. That's fine. The research doc `ada_compliance_research_1.md` has a full orchestrator pattern showing how to shell out to CLI tools, capture stdout/stderr, and parse the JSON output. It's solid.

**[Frontend]:** The value of running multiple tools is the cross-reference. The research called it the "consensus" approach. An issue found by all three tools is almost certainly real and important. An issue found by only one tool might be a false positive or a lower priority. That maps perfectly to what CTO asked for — **rank by how many tools found it**.

**[Quality]:** I want to flag something from the research. Automated tools catch maybe **30-60% of real issues**. The rest is manual — keyboard testing, screen reader validation, meaningful alt text review. Our tool can't solve that, but we should make sure the report is clear about what it covers and what it doesn't. A disclaimer section.

**[Sanity]:** Good point. We're building an automated scanner, not claiming full compliance certification. The report should say that explicitly.

---

### Modular Tool Adapter Design

**[Architect]:** Here's what I'm thinking for the modular architecture. Each scanning tool is an **adapter** that implements a common interface. The main Program.cs orchestrates them — runs all adapters against each route, collects results into a unified format, then writes the CSV.

**[Backend]:** Looking at how our other tools work — every one of them is a single `Program.cs` with static methods, uses `FreeTools.Core` for CLI args and console output, follows the startup delay pattern, and reads env vars. No DI, no interfaces in the traditional sense. This is a CLI tool suite, not an enterprise app.

**[Architect]:** Right. So let's not over-engineer it. Instead of formal interfaces, we can use a simpler pattern that matches the codebase style:

```csharp
// Each tool adapter is a static class with a ScanAsync method
// Returns a list of unified issues
public static class AxeAdapter
{
    public static async Task<List<AccessibilityIssue>> ScanAsync(string url, IPage page) { ... }
}

public static class Pa11yAdapter
{
    public static async Task<List<AccessibilityIssue>> ScanAsync(string url) { ... }
}

public static class LighthouseAdapter
{
    public static async Task<List<AccessibilityIssue>> ScanAsync(string url) { ... }
}
```

**[Backend]:** Each adapter maps its tool-specific output to a common `AccessibilityIssue` record. Then the orchestrator deduplicates and counts how many tools flagged each issue. That count becomes the priority signal.

**[Frontend]:** The `AccessibilityIssue` record needs to capture enough info for the reporter to build a useful table. At minimum:

| Field | Purpose |
|-------|---------|
| `RuleId` | The WCAG rule or tool-specific rule ID (e.g., `color-contrast`) |
| `Severity` | Normalized: critical / serious / moderate / minor |
| `Url` | Which page had the issue |
| `Selector` | CSS selector of the offending element |
| `Snippet` | The HTML snippet |
| `Message` | Human-readable description |
| `HelpUrl` | Link to fix documentation |
| `ToolName` | Which tool found this |
| `WcagCriteria` | WCAG success criterion (e.g., `1.4.3`) |

**[Sanity]:** That's clean. The CSV can have all those columns. Reporter reads it and groups by RuleId+Url+Selector to figure out which issues were found by multiple tools.

---

### CSV Output Format

**[Lead]:** Let's nail down the CSV format. Every other FreeTools tool writes a CSV that the reporter consumes. This needs to follow the same pattern.

**[Backend]:** Looking at how `pages.csv` and `workspace-inventory.csv` work — they use simple CSV with quoted strings, one row per item. The reporter reads them with `File.ReadAllLinesAsync` and splits on commas. Nothing fancy.

**[Architect]:** Proposed CSV — `accessibility-issues.csv`:

```csv
"RuleId","Severity","Url","Route","Selector","Snippet","Message","HelpUrl","ToolName","WcagCriteria"
"color-contrast","serious","/Account/Login","/Account/Login","#main .btn","<button class=""btn"">Login</button>","Elements must have sufficient color contrast","https://dequeuniversity.com/rules/axe/4.11/color-contrast","axe","1.4.3"
"color-contrast","serious","/Account/Login","/Account/Login","#main .btn","<button class=""btn"">Login</button>","Ensure the contrast ratio...","https://...","pa11y","1.4.3"
```

**[Frontend]:** So when the same issue is found by both axe and Pa11y, there are two rows with different `ToolName` values but the same `RuleId` + `Url` + `Selector`. The reporter groups on those three fields and counts distinct `ToolName` values. Issues found by 3 tools rank highest, then 2, then 1.

**[Quality]:** We should also write a summary CSV or at least include enough data for the reporter to generate summary stats — total issues by severity, total pages scanned, tool availability (did all three tools actually run or did one fail because Node.js wasn't installed).

**[Backend]:** Good call. We could write a second small file — `accessibility-summary.json` — with metadata:

```json
{
  "timestamp": "2025-07-24T10:30:00Z",
  "pagesScanned": 12,
  "toolsRun": ["axe", "pa11y", "lighthouse"],
  "toolsFailed": [],
  "totalIssues": 47,
  "bySeverity": { "critical": 2, "serious": 8, "moderate": 25, "minor": 12 }
}
```

**[Sanity]:** That keeps it simple. CSV for the row-level data, JSON for the metadata. Both are patterns the reporter can handle easily.

---

### Implementation Approach — What Goes Where

**[Architect]:** Let me lay out the file plan for `FreeTools.AccessibilityScanner`:

```
FreeTools.AccessibilityScanner/
├── Program.cs                          # Main orchestrator
├── AccessibilityIssue.cs               # Unified issue record
├── AxeAdapter.cs                       # Playwright + axe-core (in-process)
├── Pa11yAdapter.cs                     # Shells out to pa11y CLI
├── LighthouseAdapter.cs                # Shells out to lighthouse CLI
├── CliToolRunner.cs                    # Shared helper for Process execution
├── CsvWriter.cs                        # Writes accessibility-issues.csv
├── FreeTools.AccessibilityScanner.csproj
├── ada_compliance_research_1.md        # (existing) Research doc
├── ada_compliance_research_2.md        # (existing) Research doc
└── 100_standup.md                      # (this doc)
```

**[Backend]:** The `.csproj` needs updating. Right now it's bare — just `net10.0` and no references. We need:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Playwright" Version="1.56.0" />
  <PackageReference Include="Deque.AxeCore.Playwright" Version="..." />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\FreeTools.Core\FreeTools.Core.csproj" />
</ItemGroup>
```

**[Frontend]:** And the AppHost `.csproj` needs a new `ProjectReference` to AccessibilityScanner so Aspire can orchestrate it.

**[Sanity]:** Check — does `Deque.AxeCore.Playwright` actually exist as a NuGet package for .NET 10? We should verify before committing to it. The research mentions it but package availability can change.

**[Backend]:** Good flag. If the official Deque package doesn't support .NET 10 yet, the fallback from the research is to inject `axe-core.min.js` directly into the page via Playwright's `EvaluateAsync` and parse the JSON result ourselves. The research doc `ada_compliance_research_1.md` has a sketch of this approach. Either way works — the adapter pattern means we can swap the implementation without changing anything else.

**[Quality]:** We should also handle the case where Node.js isn't installed. Pa11y and Lighthouse won't work without it. The scanner should gracefully skip those adapters and log a warning, not crash. The research doc covers this — check for the tool with `Process.Start("node", "--version")`, and if it fails, mark that tool as unavailable.

---

### Reporter Integration

**[Lead]:** The last piece is the WorkspaceReporter. It needs a new section in the generated report.

**[Frontend]:** Based on what CTO described, the report section should have:

1. **Summary stats** — Pages scanned, total issues, tools used
2. **Cross-tool comparison table** — For each unique issue (grouped by RuleId + Route), show which tools found it with checkmarks
3. **Priority-ranked list** — Sorted by: (a) number of tools that found it (descending), then (b) severity (critical > serious > moderate > minor), then (c) page count
4. **Per-page breakdown** — Expandable `<details>` sections per route showing all issues on that page

**[Architect]:** The comparison table is the key deliverable. Something like:

```markdown
| Priority | Rule | Severity | Route | axe | Pa11y | Lighthouse | Consensus |
|----------|------|----------|-------|-----|-------|------------|-----------|
| 1 | color-contrast | serious | /Login | ✅ | ✅ | ✅ | 3/3 |
| 2 | image-alt | serious | /Home | ✅ | ✅ | ❌ | 2/3 |
| 3 | link-name | moderate | /About | ✅ | ❌ | ❌ | 1/3 |
```

**[Frontend]:** And the priority ranking:
- **🔴 High confidence** — Found by 3/3 tools. Fix these first.
- **🟠 Medium confidence** — Found by 2/3 tools. Likely real issues.
- **🟡 Low confidence** — Found by 1/3 tools. Could be false positives, review manually.

**[Quality]:** The report should also include the disclaimer we discussed — automated tools catch 30-60% of real issues. Manual testing (keyboard, screen reader) is still required for full compliance.

**[Backend]:** For the reporter implementation, it reads `accessibility-issues.csv` and `accessibility-summary.json`, builds the grouped/ranked data in memory, then appends a new markdown section using the same `StringBuilder` pattern all the other sections use.

---

### Execution Flow Inside the Scanner

**[Architect]:** Let me sketch the flow for `Program.cs`:

```
1. Startup delay (standard pattern)
2. Read env vars: BASE_URL, CSV_PATH, OUTPUT_DIR
3. Parse routes from pages.csv (reuse RouteParser from Core)
4. Check tool availability:
   - axe: always available (in-process NuGet)
   - pa11y: check `npx pa11y --version` or `pa11y --version`
   - lighthouse: check `npx lighthouse --version` or `lighthouse --version`
5. Launch Playwright browser (headless Chromium)
6. For each route:
   a. Navigate to page, wait for NetworkIdle
   b. Run axe in-process → collect issues
   c. Run pa11y CLI (if available) → parse JSON → collect issues
   d. Run lighthouse CLI (if available) → parse JSON → collect issues
   e. Normalize all issues to AccessibilityIssue records
7. Write accessibility-issues.csv
8. Write accessibility-summary.json
9. Print console summary
10. Exit 0 (don't fail the pipeline on violations — just report them)
```

**[Backend]:** Step 6 is the interesting one. For pa11y and lighthouse, we can either run them per-page (slower but more granular) or batch them. Per-page matches how our other tools work — EndpointPoker hits each route individually, BrowserSnapshot screenshots each route individually. Consistency matters.

**[Sanity]:** One concern — running three tools against every route could be slow. If we have 30 routes and each tool takes 3-5 seconds per page, that's 270-450 seconds (4.5-7.5 minutes) in serial. We should parallelize across routes like EndpointPoker does with `SemaphoreSlim`.

**[Backend]:** Agreed. But the CLI tools (pa11y, lighthouse) each spin up their own browser instance. Running 10 of those in parallel could be brutal on memory. Maybe parallel for axe (since it shares a Playwright browser context) but serial or low-concurrency for the CLI tools?

**[Architect]:** Reasonable. Or we batch differently: run axe against all routes in parallel first (fast, in-process), then run pa11y against all routes with moderate concurrency, then lighthouse. Three sequential phases, parallel within each phase. That also makes the output ordering cleaner.

**[Frontend]:** That also lets us show progress nicely — "Phase 1/3: axe (12/12 routes done)" etc. Matches the `ConsoleOutput` pattern from other tools.

**[Quality]:** What about authenticated pages? BrowserSnapshot already handles login — it navigates to the login page, fills credentials, and gets a session cookie. Our scanner needs the same capability since the directive says "every site, even internal tools." Some routes will be behind `[Authorize]`.

**[Backend]:** Good catch. BrowserSnapshot uses `LOGIN_USERNAME` and `LOGIN_PASSWORD` env vars with a login flow. We should reuse the same approach. The Playwright browser context will hold the auth cookie for axe scanning. For CLI tools, we'd need to either pass cookies or accept that they only scan unauthenticated routes.

**[Architect]:** For v1, let's handle auth for axe (since it shares the browser context) and let pa11y/lighthouse scan only the unauthenticated routes. The report can note which tools scanned which routes. We can add cookie forwarding to CLI tools later.

**[Sanity]:** Final check — are we overcomplicating this? Let me summarize what we're actually building:

1. A console app that reads `pages.csv` and hits each route with up to three accessibility scanners
2. It writes a CSV of all issues found
3. The reporter reads that CSV and builds a prioritized table

That's it. It follows the exact same pattern as every other tool in the suite. The "modular adapter" thing is just separate static classes — not a plugin system, not an interface hierarchy. Static classes with `ScanAsync` methods.

**[Lead]:** Exactly right. Keep it simple.

---

### Open Questions

**[Quality]:** A few things we should resolve before coding:

1. **Deque NuGet package** — Does `Deque.AxeCore.Playwright` support .NET 10 / Playwright 1.56? If not, we inject axe-core.min.js manually.
2. **Node.js dependency** — Should we document that Pa11y and Lighthouse are optional? If Node isn't installed, axe still runs and we get value.
3. **CI/CD** — Do we want the scanner to return exit code 1 on critical/serious violations? CTO said "log it" not "block the build" — but we should confirm.
4. **Severity normalization** — axe uses critical/serious/moderate/minor natively. Pa11y uses error/warning/notice. Lighthouse uses pass/fail scores. We need a mapping table.
5. **Deduplication** — Two tools might flag the same element with slightly different selectors (e.g., `#main > div > button` vs `.btn.primary`). How fuzzy do we match?

**[Lead]:** Good list. My answers for now:

1. Verify the NuGet package. If it doesn't work, fall back to manual JS injection — the research has the pattern.
2. Yes, document it. Pa11y and Lighthouse are "recommended but optional." Axe is the baseline.
3. Exit code 0 always for now. We report, we don't block. CTO can change this later.
4. Use the mapping from `ada_compliance_research_1.md`: Pa11y error→serious, warning→moderate, notice→minor. Lighthouse failing audit→moderate.
5. For v1, match on `RuleId + Route` only (not selector). Same rule on same page from multiple tools = consensus. We can get more granular later.

---

## Decisions

- AccessibilityScanner slots into the Aspire pipeline after EndpointPoker/BrowserSnapshot, before WorkspaceReporter
- It can run in parallel with BrowserSnapshot (both need web app + routes)
- Three tool adapters: axe (in-process, required), Pa11y (CLI, optional), Lighthouse (CLI, optional)
- Output: `accessibility-issues.csv` + `accessibility-summary.json` in the `latest/` folder
- Reporter gets a new section: cross-tool comparison table ranked by consensus count then severity
- Exit code always 0 — report only, don't block the pipeline
- Deduplication/consensus matching on `RuleId + Route` (not selector) for v1
- Auth handling: axe gets full auth via shared Playwright context; CLI tools scan unauthenticated routes only for v1
- Follow existing FreeTools patterns: single Program.cs entry point, FreeTools.Core reference, env var config, startup delay, ConsoleOutput formatting

## Next Steps

| Action | Owner | Priority |
|--------|-------|----------|
| Verify `Deque.AxeCore.Playwright` NuGet compatibility with .NET 10 | [Backend] | P1 |
| Update `.csproj` with dependencies (Playwright, axe, Core reference) | [Backend] | P1 |
| Implement `AccessibilityIssue` record and `CsvWriter` | [Backend] | P1 |
| Implement `AxeAdapter` (in-process Playwright+axe) | [Backend] | P1 |
| Implement `Pa11yAdapter` and `LighthouseAdapter` (CLI shelling) | [Backend] | P2 |
| Implement `Program.cs` orchestrator with route iteration | [Backend] | P1 |
| Add `AccessibilityScanner` to `AppHost\Program.cs` pipeline | [Architect] | P2 |
| Add new report section to `WorkspaceReporter\Program.cs` | [Frontend] | P2 |
| Add reporter cross-tool comparison table and priority ranking | [Frontend] | P2 |
| Document Node.js as optional dependency in project README | [Quality] | P3 |
| Test with BlazorApp1 sample project end-to-end | [Quality] | P2 |
| Review report disclaimer language for automated vs manual testing | [Quality] | P3 |

---

*Created: 2025-07-24*  
*Maintained by: [Quality]*
