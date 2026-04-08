# ADA / WCAG Web Accessibility for Blazor (.NET 10) — Practical Guide + Tooling + Automation

> **Audience:** Software engineers building web apps (Blazor + C#/.NET 10) at a university or other public institution.  
> **Goal:** Understand what “ADA compliant” means for websites, how to test it, and how to automate checks with C# and/or official CLI tools.

---

## Why this document exists (context recap)

You asked for:
- An **ELI5** explanation of “ADA compliance” for websites.
- A **series of actionable suggestions** on *what to do and how* to do it.
- **Top 20+ common accessibility mistakes** with “bad vs good” HTML examples (dos & don’ts).
- A rundown of **which tools** scan a live site vs source code, and **which are most common**.
- A plan to run checks **from C#** (NuGet packages preferred), including a **console app** with CLI args (`--url`, `--username`, `--password`, etc.).
- A way to optionally **execute external CLI tools** (axe CLI / Pa11y / Lighthouse) and **unify all results** into one output format.

This markdown is designed to be **self-contained** so you can paste it into a new chat without needing the original conversation.

---

## ELI5: What does “ADA compliant website” mean?

**ADA compliance for a website** means: people with disabilities should be able to **use your website to get the same information and complete the same tasks** as everyone else.

Think of it like:
- **Ramps + stairs** (multiple ways to access the same place)
- **Elevator buttons with Braille** (works for more people)
- **Captions on videos** (helps people who can’t hear the audio)

On the web, that usually means your site works for people who:
- Use **screen readers** because they can’t see well
- Use **keyboard-only** because they can’t use a mouse
- Need **high contrast** or larger text
- Are **colorblind**
- Are **deaf/hard of hearing** (captions/transcripts)
- Have cognitive or motor disabilities (clear structure, no tricky interactions)

---

## “ADA” vs “WCAG” vs “Section 508” (what you actually build to)

### ADA
ADA is a **civil rights law**, not a technical spec. It says people with disabilities must have equal access. For websites, the ADA doesn’t (historically) spell out exact HTML rules—so organizations use WCAG as the measurable yardstick.

The U.S. DOJ has published web accessibility guidance under the ADA, and in recent years has also issued rules/guidance for public-sector accessibility. citeturn0search1turn0search5

### WCAG
**WCAG (Web Content Accessibility Guidelines)** is the main technical standard.  
Most institutions target **WCAG Level AA** (commonly “2.1 AA” today, sometimes “2.2 AA” for forward-compatibility). WCAG 2.2 is a W3C Recommendation and W3C advises using the most current version when updating policies. citeturn0search0

### Section 508
For U.S. federal procurement/accessibility, **Section 508** matters. The revised 508 standards incorporate **WCAG 2.0 AA** by reference. citeturn0search2

**Practical takeaway:**  
Most “ADA compliance” website work is essentially: **meet WCAG Level AA**, plus do some manual validation (keyboard/screen reader) because automation can’t catch everything.

---

## The 4 principles (POUR) you will keep bumping into

- **Perceivable** – users can perceive content (text alternatives, captions, contrast)
- **Operable** – users can operate UI (keyboard support, focus, no traps)
- **Understandable** – content/interaction is clear (labels, errors, predictable behavior)
- **Robust** – compatible with assistive tech (semantic HTML, correct ARIA)

---

## Reality check: what tools can and can’t do

### Automated tools can find:
- Missing labels/alt text
- Color contrast failures (computed styles)
- Obvious ARIA misuse
- Some heading/link/landmark issues
- Some form and table semantics issues

### Automated tools cannot reliably determine:
- Whether **alt text is meaningful**
- Whether the **reading order** makes sense
- Whether **link text is clear in context**
- Whether keyboard navigation is **pleasant** (vs “technically possible”)
- Whether captions/transcripts are **accurate**
- Whether content is **understandable**

**Rule of thumb:** automation catches maybe **30–60%** of issues. The rest is manual checks + good design habits.

---

# Tooling: what’s “official” and commonly used?

There isn’t one single government-run “ADA scanner.” In practice, the tools most widely used in audits and engineering teams are:

## 1) axe (Deque) — engine + browser plugin + CLI + libraries
- Deque’s **axe-core** is a major industry-standard rules engine. citeturn0search15turn0search3  
- You’ll see it as:
  - **axe DevTools** browser extension (day-to-day dev)
  - **@axe-core/cli** (command line) citeturn0search7
  - **axe-core libraries** integrated into Selenium/Playwright tests

## 2) Lighthouse (Google / Chrome)
- Built into Chrome DevTools; also available as a CLI (`lighthouse` or `lhci`).
- Good as a **high-level gate** (score + some issues), but typically less deep than axe on pure accessibility checks.

## 3) Pa11y (CLI)
- Popular CLI runner often used in CI pipelines. It loads pages and runs accessibility rules, outputting machine-readable results.

## 4) WAVE (WebAIM)
- Great for visual overlays and education.
- Has an API (paid) for automation in some orgs.

---

## Live site vs “scan the code”

### “Live / running site” scanners (most useful)
These tools inspect the **rendered DOM**, computed CSS, and JS behavior:
- axe extension / axe CLI
- Lighthouse / LHCI
- Pa11y
- Playwright/Selenium-based tests

They can run against:
- production URLs
- a staging URL
- **localhost** while you’re developing (still “live”)

### Static code scanners (limited)
Linters can catch some problems (e.g., missing `alt`, invalid ARIA attributes), but cannot reliably compute:
- contrast ratios (computed CSS)
- focus/keyboard behavior
- DOM changes after JS/Blazor rendering

**Best practice:** use both, but trust live scanning more.

---

# Recommended workflow for a Blazor team

## Daily development (fast feedback)
1. Run the app locally
2. Use **axe DevTools** and/or **WAVE** extension per page
3. Do quick manual checks:
   - Tab through the page
   - Zoom to 200%
   - Check color contrast for key components

## Pre-merge (automation)
- Playwright + axe in an integration test suite (NuGet-based)
- Fail PR if new **critical/serious** violations appear

## Pre-release (audit-grade reports)
- Run CLI tools (axe CLI / Lighthouse / Pa11y) against staging
- Export JSON and archive results for evidence

## Periodically (manual validation)
- Screen reader checks (NVDA on Windows; VoiceOver on macOS)
- Keyboard-only “happy path” completion tests for core tasks
- Mobile checks (touch targets, zoom, orientation)

---

# Top 25 accessibility dos and don’ts (with bad vs good HTML)

> These are the repeat offenders you’ll see in audits.

## 1) Images need correct alt text
**Don’t**
```html
<img src="logo.png">
<img src="map.png" alt="image">
```
**Do**
```html
<img src="logo.png" alt="Washington State University logo">
<img src="map.png" alt="Map showing the Pullman campus with the library, student center, and parking areas.">
<img src="divider.png" alt=""> <!-- decorative -->
```

## 2) Use real headings in order (don’t skip levels)
**Don’t**
```html
<h1>Page</h1>
<h3>Section</h3>
<h2>Another Section</h2>
```
**Do**
```html
<h1>Page</h1>
<h2>Section</h2>
<h3>Subsection</h3>
<h2>Another Section</h2>
```

## 3) Forms must have labels (placeholder is not a label)
**Don’t**
```html
<input type="text" placeholder="Name">
```
**Do**
```html
<label for="name">Full name</label>
<input id="name" name="name" type="text" autocomplete="name">
```

## 4) Every input needs an accessible name
**Don’t**
```html
<button><svg><!-- icon --></svg></button>
```
**Do**
```html
<button aria-label="Search">
  <svg aria-hidden="true"><!-- icon --></svg>
</button>
```

## 5) Don’t use color alone to convey meaning
**Don’t**
```html
<span style="color:red">Overdue</span>
```
**Do**
```html
<span class="status status--overdue">
  Overdue <span class="sr-only">(action required)</span>
</span>
```

## 6) Ensure sufficient color contrast
**Don’t**
```html
<p style="color:#999;background:#fff">Important notice</p>
```
**Do**
```html
<p style="color:#444;background:#fff">Important notice</p>
```
(Use a contrast checker; aim for WCAG AA.)

## 7) Make everything keyboard-operable (no “div buttons”)
**Don’t**
```html
<div onclick="save()">Save</div>
```
**Do**
```html
<button type="button" onclick="save()">Save</button>
```

## 8) Don’t remove focus outlines
**Don’t**
```css
*:focus { outline: none; }
```
**Do**
```css
*:focus { outline: 2px solid currentColor; outline-offset: 2px; }
```

## 9) Use semantic landmarks
**Don’t**
```html
<div class="header">...</div>
<div class="content">...</div>
<div class="footer">...</div>
```
**Do**
```html
<header>...</header>
<nav aria-label="Primary">...</nav>
<main id="main">...</main>
<footer>...</footer>
```

## 10) Provide a skip link
**Don’t**
```html
<!-- no skip link -->
```
**Do**
```html
<a class="skip-link" href="#main">Skip to main content</a>
<main id="main">...</main>
```

## 11) Link text must be meaningful out of context
**Don’t**
```html
<a href="/apply">Click here</a>
```
**Do**
```html
<a href="/apply">Apply for admission</a>
```

## 12) Buttons and links are not interchangeable
**Don’t**
```html
<a href="#" onclick="openModal()">Open</a>
```
**Do**
```html
<button type="button" onclick="openModal()">Open</button>
```

## 13) Identify page language
**Don’t**
```html
<html>
```
**Do**
```html
<html lang="en">
```

## 14) Tables must be real tables (with headers)
**Don’t**
```html
<div class="row"><div>Name</div><div>Grade</div></div>
```
**Do**
```html
<table>
  <caption>Grades</caption>
  <thead>
    <tr>
      <th scope="col">Name</th>
      <th scope="col">Grade</th>
    </tr>
  </thead>
  <tbody>
    <tr><td>Ana</td><td>A</td></tr>
  </tbody>
</table>
```

## 15) Don’t use tables for layout
**Don’t**
```html
<table><tr><td>Sidebar</td><td>Main</td></tr></table>
```
**Do**
```html
<div class="layout">
  <aside>Sidebar</aside>
  <main>Main</main>
</div>
```

## 16) Errors must be described in text, not just color
**Don’t**
```html
<input aria-invalid="true">
<span class="error" style="color:red">*</span>
```
**Do**
```html
<label for="email">Email</label>
<input id="email" type="email" aria-describedby="email-error" aria-invalid="true">
<p id="email-error" role="alert">Enter a valid email address.</p>
```

## 17) Required fields must be announced
**Don’t**
```html
<label>Name *</label>
<input>
```
**Do**
```html
<label for="name">Name <span aria-hidden="true">*</span></label>
<input id="name" required aria-required="true">
```

## 18) Don’t trap focus (modals/menus must manage focus)
**Don’t**
```html
<div class="modal" style="display:block">...</div>
```
**Do**
```html
<div role="dialog" aria-modal="true" aria-labelledby="m-title">
  <h2 id="m-title">Confirm</h2>
  <button type="button">Cancel</button>
  <button type="button">OK</button>
</div>
```
(Also implement focus trap and restore focus on close.)

## 19) Dropdowns/menus need correct keyboard interaction
**Don’t**
```html
<div onclick="toggle()">Menu</div>
```
**Do**
```html
<button type="button" aria-expanded="false" aria-controls="menu">Menu</button>
<ul id="menu" hidden>
  <li><a href="/a">Item A</a></li>
</ul>
```

## 20) Provide captions/transcripts for media
**Don’t**
```html
<video src="welcome.mp4" controls></video>
```
**Do**
```html
<video controls>
  <source src="welcome.mp4" type="video/mp4">
  <track kind="captions" src="welcome.en.vtt" srclang="en" label="English captions">
</video>
```

## 21) Respect reduced motion
**Don’t**
```css
.spinner { animation: spin 1s linear infinite; }
```
**Do**
```css
@media (prefers-reduced-motion: reduce) {
  .spinner { animation: none; }
}
```

## 22) Don’t auto-play audio/video
**Don’t**
```html
<video autoplay></video>
```
**Do**
```html
<video controls></video>
```

## 23) Touch targets should be large enough (mobile)
**Don’t**
```html
<button style="padding:2px">X</button>
```
**Do**
```html
<button style="padding:12px">Close</button>
```

## 24) Don’t misuse ARIA (prefer semantic HTML first)
**Don’t**
```html
<div role="button">Save</div>
```
**Do**
```html
<button type="button">Save</button>
```
If you must use ARIA, do it correctly.

## 25) Dynamic updates should be announced when needed
**Don’t**
```html
<div id="status">Saved!</div> <!-- changes silently -->
```
**Do**
```html
<div id="status" role="status" aria-live="polite" aria-atomic="true">Saved!</div>
```

---

# Blazor-specific guidance (practical patterns)

## Prefer semantic HTML in Razor components
```razor
<button class="btn" @onclick="SaveAsync" disabled="@IsSaving">
  Save
</button>
```

## Accessible icon-only buttons
```razor
<button class="icon-btn" aria-label="Open search" @onclick="OpenSearch">
  <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24">...</svg>
</button>
```

## Announce status updates
```razor
<div role="status" aria-live="polite" aria-atomic="true">
  @StatusMessage
</div>
```

## Forms: use `<label for>` and stable `id`s
```razor
<label for="email">Email</label>
<input id="email" @bind="Model.Email" type="email" autocomplete="email" />
```

## Validation messages: ensure they are associated
```razor
<input id="email" @bind="Model.Email" aria-describedby="email-error" aria-invalid="@HasEmailError" />
@if (HasEmailError)
{
  <p id="email-error" role="alert">@EmailError</p>
}
```

---

# Testing suites: “official” CLI tools + C# integrated tests

## Option A (pure .NET): Playwright + axe (recommended for CI)
**Why:** Type-safe, debuggable, fast, runs against localhost or staging.

NuGet packages:
- `Microsoft.Playwright`
- (axe integration package; commonly from Deque community ecosystem)

Conceptual approach:
1. Launch headless browser
2. Navigate to pages
3. Run axe-core against DOM
4. Fail test if serious/critical violations exist

Example (sketch; package APIs vary by wrapper):
```csharp
using Microsoft.Playwright;
// using <YourAxeWrapper>;

public sealed class A11yTestRunner
{
    public async Task<A11yScanResult> ScanAsync(string url)
    {
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });

        // PSEUDOCODE: call axe and return results
        // var axe = await page.RunAxeAsync();
        // return MapAxeToUnified(axe, url);

        return new A11yScanResult { Url = url };
    }
}
```

## Option B (use “official” CLIs): axe CLI + Lighthouse + Pa11y
**Why:** These are widely used in audit pipelines and produce durable “report artifacts.”

### axe CLI (`@axe-core/cli`)
Install:
```bash
npm i -g @axe-core/cli
```
Run:
```bash
axe https://example.edu --save axe.json
```
`@axe-core/cli` is the npm package for axe CLI. citeturn0search7

### Lighthouse (Google)
Install:
```bash
npm i -g lighthouse
```
Run accessibility-only:
```bash
lighthouse https://example.edu \
  --only-categories=accessibility \
  --output json --output-path lighthouse.json \
  --chrome-flags="--headless"
```

### Pa11y
Install:
```bash
npm i -g pa11y
```
Run JSON report:
```bash
pa11y https://example.edu --reporter json > pa11y.json
```

> **Authentication note:** CLIs typically support scripted actions, cookies, or a launch config. For complex SSO, you may prefer Playwright (log in) and then scan using in-process checks.

---

# A unified results format (so any tool can plug in)

You want a single system that can ingest axe results, Lighthouse audits, Pa11y output, and Playwright+axe output.

## Unified data model (JSON)
Design goals:
- consistent severity levels
- tool-agnostic issue shape
- include page URL + CSS selector(s) + snippet + help URL
- preserve raw tool payload for debugging

### Proposed schema
```json
{
  "runId": "2026-02-12T21:30:00Z__staging",
  "timestampUtc": "2026-02-12T21:30:00Z",
  "target": {
    "rootUrl": "https://staging.example.edu",
    "pages": ["/", "/about", "/apply"]
  },
  "environment": {
    "machine": "CI",
    "headless": true
  },
  "toolRuns": [
    {
      "tool": "axe-cli",
      "toolVersion": "4.x",
      "status": "success",
      "artifacts": ["axe.json"]
    },
    {
      "tool": "lighthouse",
      "toolVersion": "11.x",
      "status": "success",
      "artifacts": ["lighthouse.json"]
    }
  ],
  "issues": [
    {
      "id": "axe.color-contrast",
      "tool": "axe-cli",
      "ruleId": "color-contrast",
      "severity": "serious",
      "wcag": ["1.4.3"],
      "url": "https://staging.example.edu/apply",
      "selector": "#main .cta",
      "snippet": "<a class=\"cta\">Apply</a>",
      "message": "Elements must have sufficient color contrast",
      "helpUrl": "https://dequeuniversity.com/rules/axe/4.11/color-contrast",
      "tags": ["contrast", "visual"],
      "raw": { }
    }
  ],
  "summary": {
    "pagesScanned": 3,
    "issueCounts": {
      "critical": 1,
      "serious": 5,
      "moderate": 10,
      "minor": 2
    }
  }
}
```

### Severity mapping recommendation
Normalize severities to: `critical | serious | moderate | minor | info`  
Mappings:
- axe: impact already uses `critical/serious/moderate/minor`
- pa11y: map `error -> serious` (or `critical` for specific codes), `warning -> moderate`, `notice -> minor/info`
- lighthouse: treat failing audits as `moderate` unless you decide certain audits should be `serious`

---

# Example C# console app orchestrator (runs CLIs + unifies output)

Below is a practical, “single binary” orchestrator pattern:
- Accepts args (`--url`, `--pages`, `--username`, `--password`, `--tools`, `--out`)
- Runs each external CLI via `Process`
- Reads the JSON outputs
- Maps to unified schema
- Writes a single combined report JSON

> This is intentionally “realistic,” but you’ll still need to customize auth flows and selectors for your environment.

## `dotnet new console` + packages
```bash
dotnet new console -n AdaScanOrchestrator
cd AdaScanOrchestrator
dotnet add package System.CommandLine
```

## Program.cs (single file)
```csharp
using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var urlOpt = new Option<string>("--url", "Root URL to scan (e.g., https://staging.example.edu)") { IsRequired = true };
        var pagesOpt = new Option<string[]>("--pages", () => Array.Empty<string>(), "Page paths to scan (e.g., / /about /apply)");
        var toolsOpt = new Option<string[]>("--tools", () => new[] { "axe", "lighthouse", "pa11y" }, "Tools to run: axe | lighthouse | pa11y | all");
        var outOpt = new Option<string>("--out", () => "unified-a11y-report.json", "Unified output JSON file");

        // auth placeholders (use Playwright for real SSO)
        var usernameOpt = new Option<string?>("--username", "Username (optional; best handled via Playwright/SAML workflows)");
        var passwordOpt = new Option<string?>("--password", "Password (optional)");

        var root = new RootCommand("ADA/WCAG scan orchestrator (wraps official CLIs and outputs unified JSON)")
        {
            urlOpt, pagesOpt, toolsOpt, outOpt, usernameOpt, passwordOpt
        };

        root.SetHandler(async (url, pages, tools, outFile, username, password) =>
        {
            var run = await RunAsync(url, pages, tools, outFile);
            var json = JsonSerializer.Serialize(run, UnifiedJson.Options);
            await File.WriteAllTextAsync(outFile, json);
            Console.WriteLine($"Wrote: {outFile}");
            Console.WriteLine($"Issues: {run.Summary.TotalIssues} (critical={run.Summary.Critical}, serious={run.Summary.Serious}, moderate={run.Summary.Moderate}, minor={run.Summary.Minor})");

            // Non-zero exit if critical/serious issues exist (CI-friendly)
            Environment.Exit((run.Summary.Critical + run.Summary.Serious) > 0 ? 1 : 0);
        }, urlOpt, pagesOpt, toolsOpt, outOpt, usernameOpt, passwordOpt);

        return await root.InvokeAsync(args);
    }

    private static async Task<UnifiedRun> RunAsync(string rootUrl, string[] pages, string[] tools, string outFile)
    {
        var runId = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var pagesToScan = (pages.Length == 0 ? new[] { "/" } : pages)
            .Select(p => p.StartsWith("/") ? p : "/" + p)
            .Distinct()
            .ToArray();

        var toolList = tools.Select(t => t.ToLowerInvariant()).ToArray();
        if (toolList.Contains("all"))
            toolList = new[] { "axe", "lighthouse", "pa11y" };

        var unified = new UnifiedRun
        {
            RunId = runId,
            TimestampUtc = DateTime.UtcNow,
            Target = new TargetInfo { RootUrl = rootUrl, Pages = pagesToScan.ToList() },
            ToolRuns = new List<ToolRun>(),
            Issues = new List<UnifiedIssue>()
        };

        // 1) Run each tool per page
        foreach (var pagePath in pagesToScan)
        {
            var fullUrl = new Uri(new Uri(rootUrl), pagePath).ToString();
            Console.WriteLine($"Scanning: {fullUrl}");

            if (toolList.Contains("axe"))
            {
                var axeOut = $"{SanitizeFile(pagePath)}__axe.json";
                var tr = await RunProcessAsync("axe", $"{Quote(fullUrl)} --save {Quote(axeOut)}");
                unified.ToolRuns.Add(tr with { Tool = "axe-cli", TargetUrl = fullUrl, Artifacts = new() { axeOut } });

                if (File.Exists(axeOut))
                {
                    var axeJson = await File.ReadAllTextAsync(axeOut);
                    var axe = JsonSerializer.Deserialize<AxeCliPayload>(axeJson, UnifiedJson.Options);
                    if (axe is not null)
                        unified.Issues.AddRange(MapAxe(fullUrl, axe));
                }
            }

            if (toolList.Contains("lighthouse"))
            {
                var lhOut = $"{SanitizeFile(pagePath)}__lighthouse.json";
                var args = $"{Quote(fullUrl)} --only-categories=accessibility --output json --output-path {Quote(lhOut)} --chrome-flags=\"--headless\"";
                var tr = await RunProcessAsync("lighthouse", args);
                unified.ToolRuns.Add(tr with { Tool = "lighthouse", TargetUrl = fullUrl, Artifacts = new() { lhOut } });

                if (File.Exists(lhOut))
                {
                    var lhJson = await File.ReadAllTextAsync(lhOut);
                    var lh = JsonSerializer.Deserialize<LighthousePayload>(lhJson, UnifiedJson.Options);
                    if (lh is not null)
                        unified.Issues.AddRange(MapLighthouse(fullUrl, lh));
                }
            }

            if (toolList.Contains("pa11y"))
            {
                // pa11y writes JSON to stdout easily
                var tr = await RunProcessAsync("pa11y", $"{Quote(fullUrl)} --reporter json");
                unified.ToolRuns.Add(tr with { Tool = "pa11y", TargetUrl = fullUrl });

                if (tr.Status == "success" && !string.IsNullOrWhiteSpace(tr.Stdout))
                {
                    var pa = JsonSerializer.Deserialize<List<Pa11yItem>>(tr.Stdout, UnifiedJson.Options) ?? new();
                    unified.Issues.AddRange(MapPa11y(fullUrl, pa));
                }
            }
        }

        unified.Summary = UnifiedSummary.From(unified.Issues);
        return unified;
    }

    // ---------- tool execution ----------

    private static async Task<ToolRun> RunProcessAsync(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p is null) return new ToolRun(exe, "failed", "Could not start process", "", "", 1, "", new());

            var stdout = await p.StandardOutput.ReadToEndAsync();
            var stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();

            return new ToolRun(exe, p.ExitCode == 0 ? "success" : "failed", "", stdout, stderr, p.ExitCode, "", new());
        }
        catch (Exception ex)
        {
            return new ToolRun(exe, "failed", ex.Message, "", "", 1, "", new());
        }
    }

    private static string Quote(string s) => s.Contains(' ') ? $"\"{s}\"" : s;
    private static string SanitizeFile(string pagePath)
    {
        var s = pagePath.Trim('/');
        if (string.IsNullOrEmpty(s)) s = "root";
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        s = s.Replace('/', '_').Replace('\\', '_');
        return s;
    }

    // ---------- mapping (tool payload -> unified issues) ----------

    private static IEnumerable<UnifiedIssue> MapAxe(string url, AxeCliPayload axe)
    {
        if (axe.Violations is null) yield break;

        foreach (var v in axe.Violations)
        {
            var impact = v.Impact ?? "moderate";
            var severity = NormalizeSeverity(impact);

            if (v.Nodes is null || v.Nodes.Count == 0)
            {
                yield return new UnifiedIssue
                {
                    Id = $"axe.{v.Id}",
                    Tool = "axe-cli",
                    RuleId = v.Id ?? "",
                    Severity = severity,
                    Url = url,
                    Message = v.Help ?? v.Description ?? "Violation",
                    HelpUrl = v.HelpUrl,
                    Raw = JsonDocument.Parse(JsonSerializer.Serialize(v, UnifiedJson.Options)).RootElement
                };
                continue;
            }

            foreach (var n in v.Nodes)
            {
                var selector = (n.Target is { Count: > 0 }) ? string.Join(", ", n.Target) : null;
                yield return new UnifiedIssue
                {
                    Id = $"axe.{v.Id}",
                    Tool = "axe-cli",
                    RuleId = v.Id ?? "",
                    Severity = severity,
                    Url = url,
                    Selector = selector,
                    Snippet = n.Html,
                    Message = v.Help ?? v.Description ?? "Violation",
                    HelpUrl = v.HelpUrl,
                    Raw = JsonDocument.Parse(JsonSerializer.Serialize(new { violation = v, node = n }, UnifiedJson.Options)).RootElement
                };
            }
        }
    }

    private static IEnumerable<UnifiedIssue> MapPa11y(string url, List<Pa11yItem> items)
    {
        foreach (var i in items)
        {
            var severity = i.Type switch
            {
                "error" => "serious",
                "warning" => "moderate",
                "notice" => "minor",
                _ => "info"
            };

            yield return new UnifiedIssue
            {
                Id = $"pa11y.{i.Code}",
                Tool = "pa11y",
                RuleId = i.Code ?? "",
                Severity = severity,
                Url = url,
                Selector = i.Selector,
                Snippet = i.Context,
                Message = i.Message ?? "Issue",
                Raw = JsonDocument.Parse(JsonSerializer.Serialize(i, UnifiedJson.Options)).RootElement
            };
        }
    }

    private static IEnumerable<UnifiedIssue> MapLighthouse(string url, LighthousePayload lh)
    {
        // Lighthouse accessibility findings are embedded in audits; this mapper is intentionally conservative:
        // - Report audits with score == 0 that contain items/snippets when available.
        if (lh.Audits is null) yield break;

        foreach (var kv in lh.Audits)
        {
            var auditId = kv.Key;
            var a = kv.Value;
            if (a is null) continue;

            if (a.Score is double score && score == 0)
            {
                // treat failing audits as moderate by default
                var issue = new UnifiedIssue
                {
                    Id = $"lighthouse.{auditId}",
                    Tool = "lighthouse",
                    RuleId = auditId,
                    Severity = "moderate",
                    Url = url,
                    Message = a.Title ?? "Lighthouse audit failed",
                    HelpUrl = a.HelpUrl,
                    Raw = JsonDocument.Parse(JsonSerializer.Serialize(a, UnifiedJson.Options)).RootElement
                };
                yield return issue;
            }
        }
    }

    private static string NormalizeSeverity(string s)
    {
        s = s.ToLowerInvariant();
        return s switch
        {
            "critical" => "critical",
            "serious" => "serious",
            "moderate" => "moderate",
            "minor" => "minor",
            _ => "info"
        };
    }
}

// ---------- Unified schema types ----------

public sealed class UnifiedRun
{
    public string RunId { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
    public TargetInfo Target { get; set; } = new();
    public List<ToolRun> ToolRuns { get; set; } = new();
    public List<UnifiedIssue> Issues { get; set; } = new();
    public UnifiedSummary Summary { get; set; } = new();
}

public sealed class TargetInfo
{
    public string RootUrl { get; set; } = "";
    public List<string> Pages { get; set; } = new();
}

public record ToolRun(
    string Tool,
    string Status,
    string Error,
    string Stdout,
    string Stderr,
    int ExitCode,
    string TargetUrl,
    List<string> Artifacts
);

public sealed class UnifiedIssue
{
    public string Id { get; set; } = "";
    public string Tool { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string Severity { get; set; } = "moderate";
    public string Url { get; set; } = "";
    public string? Selector { get; set; }
    public string? Snippet { get; set; }
    public string Message { get; set; } = "";
    public string? HelpUrl { get; set; }
    public JsonElement Raw { get; set; }
}

public sealed class UnifiedSummary
{
    public int Critical { get; set; }
    public int Serious { get; set; }
    public int Moderate { get; set; }
    public int Minor { get; set; }
    public int Info { get; set; }
    public int TotalIssues => Critical + Serious + Moderate + Minor + Info;

    public static UnifiedSummary From(IEnumerable<UnifiedIssue> issues)
    {
        var s = new UnifiedSummary();
        foreach (var i in issues)
        {
            switch (i.Severity)
            {
                case "critical": s.Critical++; break;
                case "serious": s.Serious++; break;
                case "moderate": s.Moderate++; break;
                case "minor": s.Minor++; break;
                default: s.Info++; break;
            }
        }
        return s;
    }
}

// ---------- Tool payload models (minimal) ----------

public static class UnifiedJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

// axe CLI payload shape (subset)
public sealed class AxeCliPayload
{
    [JsonPropertyName("violations")]
    public List<AxeViolation>? Violations { get; set; }
}

public sealed class AxeViolation
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("impact")]
    public string? Impact { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("help")]
    public string? Help { get; set; }

    [JsonPropertyName("helpUrl")]
    public string? HelpUrl { get; set; }

    [JsonPropertyName("nodes")]
    public List<AxeNode>? Nodes { get; set; }
}

public sealed class AxeNode
{
    [JsonPropertyName("html")]
    public string? Html { get; set; }

    [JsonPropertyName("target")]
    public List<string>? Target { get; set; }
}

// Pa11y payload shape (subset)
public sealed class Pa11yItem
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("context")]
    public string? Context { get; set; }

    [JsonPropertyName("selector")]
    public string? Selector { get; set; }
}

// Lighthouse payload shape (subset)
public sealed class LighthousePayload
{
    [JsonPropertyName("audits")]
    public Dictionary<string, LighthouseAudit?>? Audits { get; set; }
}

public sealed class LighthouseAudit
{
    [JsonPropertyName("score")]
    public double? Score { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("helpUrl")]
    public string? HelpUrl { get; set; }
}
```

## Example usage
```bash
# Run all tools against a few pages
dotnet run -- \
  --url https://staging.example.edu \
  --pages / /about /apply \
  --tools all \
  --out unified-a11y-report.json

# Run only axe + lighthouse
dotnet run -- --url https://staging.example.edu --pages / /apply --tools axe lighthouse
```

### Notes on reliability
- If a tool is not installed (e.g., `axe` not in PATH), the orchestrator will mark that tool run as failed and continue.
- For internal sites requiring complex auth, use **Playwright** to login and either:
  - run in-process axe checks, OR
  - export session cookies for CLIs (more work), OR
  - run the CLIs against an authenticated staging environment.

---

# Suggestions and upgrades beyond what we discussed

## 1) Target standard: WCAG 2.1 AA minimum, consider WCAG 2.2 AA for future-proofing
WCAG 2.2 is current and W3C encourages using the most current version when updating policies. citeturn0search0

## 2) Create an accessibility “definition of done”
Example checklist for PR review:
- All images have meaningful alt (or `alt=""` if decorative)
- All controls reachable by keyboard; visible focus indicator
- Inputs have labels; validation messages announced
- Semantic landmarks (`header/nav/main/footer`) present
- No “click here” links
- Contrast meets AA
- Reduced motion supported
- Modals trap focus and restore focus

## 3) Establish severity gates for CI
Common CI policy:
- Block merge on **critical/serious**
- Allow moderate/minor but track them
- Require accessibility owner sign-off for exceptions

## 4) Store JSON artifacts from scans
Keep reports for:
- regression tracking
- procurement / audit evidence
- verifying fixes

## 5) Add a sitemap crawler (optional)
If your site has a `sitemap.xml`, you can discover pages automatically and scan them. (Be cautious of scanning huge sites—limit scope, avoid infinite loops, respect robots rules for non-prod environments.)

## 6) Use manual testing for “core user journeys”
Pick 3–5 critical flows:
- log in
- search
- apply/register
- submit a form
- download course materials

Test them with:
- keyboard only
- screen reader
- 200% zoom

---

# Quick reference: what to install

## Browser extensions (devs)
- axe DevTools
- WAVE
- Chrome Lighthouse (built-in)

## CLI tools (pipeline)
- `@axe-core/cli` citeturn0search7
- `lighthouse`
- `pa11y`

## C# / .NET
- `Microsoft.Playwright` (if you go the pure .NET route)
- `System.CommandLine` (for robust args parsing)

---

# Appendix: Tool outputs (what to expect)

## axe JSON shape (high level)
- `violations[]` each with:
  - `id` (rule id)
  - `impact` (critical/serious/moderate/minor)
  - `helpUrl`
  - `nodes[]` (each affected element, with `html` snippet + `target` selectors)

## Pa11y JSON shape (high level)
Each item includes:
- `type` (error/warning/notice)
- `message`
- `selector`
- `context` snippet

## Lighthouse JSON shape (high level)
- Contains `audits` keyed by audit id
- `score` indicates pass/fail (not always strictly 0/1)
- More of a “report” than a “list of nodes”

---

# Message to a future AI (intent of this file)

You are reading this markdown because the user (a Blazor/.NET engineer) needs a **self-contained playbook** for web accessibility compliance commonly described as “ADA compliance.” The goal is to:
1) explain the concept in simple terms,  
2) list concrete dos/don’ts with code examples,  
3) recommend practical tooling, and  
4) provide an automation approach with a C# console orchestrator that can run multiple scanners and normalize results into one schema.

When extending this document, prioritize:
- actionable engineering steps
- reliable automation patterns (CI-friendly exit codes, artifact outputs)
- accessibility best practices that reduce legal risk and improve usability
- keeping examples concrete and minimal, avoiding ARIA where semantic HTML works

