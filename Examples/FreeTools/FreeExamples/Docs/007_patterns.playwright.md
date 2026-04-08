# FreeCRM: Playwright Browser Automation Guide

> Patterns for headless browser automation, screenshot capture, and page interaction using Microsoft.Playwright in FreeTools CLI tools.

---

## Document Index

| Section | Line | Description |
|---------|------|-------------|
| [Overview](#overview) | ~30 | What Playwright provides and when to use it |
| [Setup & Installation](#setup--installation) | ~55 | Package reference, browser install, csproj |
| [Core Lifecycle](#core-lifecycle) | ~90 | Create → Launch → Context → Page → Dispose |
| [Navigation & Waiting](#navigation--waiting) | ~155 | WaitUntilState strategies for SPAs |
| [Screenshot Capture](#screenshot-capture) | ~200 | Full-page capture, retry logic, file size checks |
| [Page Interaction](#page-interaction) | ~260 | Full element reference: inputs, selects, checkboxes, tables, modals, downloads, keyboard, scrolling |
| [Auth Flow Pattern](#auth-flow-pattern) | ~340 | Multi-step login with screenshots at each stage |
| [Console Error Capture](#console-error-capture) | ~410 | Capturing JavaScript errors during page load |
| [Parallel Execution](#parallel-execution) | ~440 | SemaphoreSlim + ordered output pattern |
| [Metadata Output](#metadata-output) | ~490 | Writing JSON metadata alongside screenshots |
| [Configuration Reference](#configuration-reference) | ~530 | All env vars and defaults |
| [Troubleshooting](#troubleshooting) | ~570 | Common issues and fixes |

**Source:** `FreeTools.BrowserSnapshot/Program.cs`
**Library:** [Microsoft.Playwright](https://playwright.dev/dotnet/) v1.56.0

---

## Overview

FreeTools uses Microsoft.Playwright for headless browser automation. The primary consumer is **BrowserSnapshot**, which captures full-page PNG screenshots of every route discovered by EndpointMapper.

**Key capabilities used:**
- Headless Chromium/Firefox/WebKit launch
- Full-page screenshot capture
- Smart SPA waiting (`NetworkIdle`)
- Form detection and auto-fill (login flows)
- JavaScript console error capture
- Parallel page processing with ordered output

**When to use Playwright in FreeTools:**
- Screenshot capture (BrowserSnapshot)
- Accessibility scanning (AccessibilityScanner — planned)
- Any tool that needs to render and interact with a live web page

---

## Setup & Installation

### Package Reference

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Playwright" Version="1.56.0" />
</ItemGroup>
```

### Browser Auto-Install

Playwright requires browser binaries. FreeTools handles this automatically at startup:

```csharp
private static async Task EnsurePlaywrightBrowsersInstalledAsync(string browserName)
{
    var exitCode = Microsoft.Playwright.Program.Main(["install", browserName]);

    if (exitCode != 0) {
        Console.WriteLine($"Playwright install returned exit code {exitCode}, but continuing anyway...");
    } else {
        Console.WriteLine($"Playwright {browserName} browser ready.");
    }

    await Task.CompletedTask;
}
```

**Call this early** — before `Playwright.CreateAsync()`. It downloads ~100-300MB on first run per browser.

### Manual Install (One-Time)

If running outside the pipeline:

```bash
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

---

## Core Lifecycle

The Playwright object hierarchy follows a strict create → use → dispose pattern:

```
Playwright (singleton)
  └── Browser (one per browser type)
       └── BrowserContext (one per "session" — isolated cookies/storage)
            └── Page (one per tab — navigation, interaction, screenshots)
```

### Full Lifecycle Pattern

```csharp
using Microsoft.Playwright;

// 1. Create Playwright instance
using var playwright = await Playwright.CreateAsync();

// 2. Pick a browser type
var browserType = browserName switch
{
    "firefox" => playwright.Firefox,
    "webkit"  => playwright.Webkit,
    _         => playwright.Chromium
};

// 3. Launch headless browser
await using var browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = true
});

// 4. Create isolated context (new cookies, viewport, etc.)
var contextOptions = new BrowserNewContextOptions();
if (viewportWidth.HasValue && viewportHeight.HasValue) {
    contextOptions.ViewportSize = new ViewportSize
    {
        Width = viewportWidth.Value,
        Height = viewportHeight.Value
    };
}

var context = await browser.NewContextAsync(contextOptions);
try
{
    // 5. Open a page
    var page = await context.NewPageAsync();

    // 6. Navigate, interact, capture
    await page.GotoAsync(url);

    // 7. Take screenshot
    await page.ScreenshotAsync(new PageScreenshotOptions
    {
        Path = outputPath,
        FullPage = true
    });
}
finally
{
    // 8. Always close context (releases resources)
    await context.CloseAsync();
}
```

**Key rules:**
- **One context per route** — Isolates cookies/state so auth flows don't leak between pages
- **Always close context in `finally`** — Prevents browser resource leaks
- **`using`/`await using`** — Playwright and Browser implement `IAsyncDisposable`

---

## Navigation & Waiting

### WaitUntilState Strategies

Blazor SPAs need special wait strategies. The default `Load` event fires before JavaScript renders content.

```csharp
var response = await page.GotoAsync(url, new PageGotoOptions
{
    WaitUntil = WaitUntilState.NetworkIdle,
    Timeout = 60000
});
```

| Strategy | When to Use |
|----------|-------------|
| `Load` | Static HTML pages — fires when HTML + resources loaded |
| `DOMContentLoaded` | Fast — fires when HTML parsed, before images/CSS |
| `NetworkIdle` | **Best for Blazor/SPAs** — waits until network is quiet for 500ms |
| `Commit` | Fastest — fires when first response bytes arrive |

### Post-Navigation Settle Delay

Even after `NetworkIdle`, Blazor may still be rendering. Add a configurable settle delay:

```csharp
// Wait for SPA to fully settle after NetworkIdle
var settleDelay = 3000; // milliseconds
await page.WaitForTimeoutAsync(settleDelay);
```

**Why 3000ms default:** Blazor Server needs time for SignalR connection + initial render. Blazor WASM needs time for assembly download + render. 1500ms caused blank screenshots; 3000ms is reliable.

### Checking Navigation Response

```csharp
var response = await page.GotoAsync(url, new PageGotoOptions
{
    WaitUntil = WaitUntilState.NetworkIdle,
    Timeout = 60000
});

var statusCode = response?.Status ?? 0;

if (statusCode >= 200 && statusCode < 400) {
    // Success — capture screenshot
} else if (statusCode >= 400) {
    // HTTP error — still capture for documentation
}
```

---

## Screenshot Capture

### Basic Full-Page Capture

```csharp
await page.ScreenshotAsync(new PageScreenshotOptions
{
    Path = screenshotPath,
    FullPage = true
});
```

### Smart Retry for Blank Screenshots

Blazor pages sometimes render blank on first capture. Detect by file size and retry:

```csharp
private const int SuspiciousFileSizeThreshold = 10 * 1024; // 10KB
private const int RetryExtraDelayMs = 3000;

// Take initial screenshot
await page.ScreenshotAsync(new PageScreenshotOptions
{
    Path = screenshotPath,
    FullPage = true
});

var fi = new FileInfo(screenshotPath);

// Retry if suspiciously small (likely blank)
if (fi.Length < SuspiciousFileSizeThreshold) {
    result.RetryAttempted = true;
    await page.WaitForTimeoutAsync(RetryExtraDelayMs);

    await page.ScreenshotAsync(new PageScreenshotOptions
    {
        Path = screenshotPath,
        FullPage = true
    });

    fi = new FileInfo(screenshotPath);
}

result.FileSize = fi.Length;
result.IsSuspiciouslySmall = fi.Length < SuspiciousFileSizeThreshold;
```

**Why 10KB threshold:** A real rendered page is typically 50KB-2MB. A blank white page is ~3-8KB. 10KB catches blanks without false positives.

---

## Page Interaction

### Locator Basics

Playwright uses Locators for element selection. Always use `.First` and check visibility before interacting:

```csharp
var locator = page.Locator(selector).First;
if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync()) {
    // Element exists and is visible — safe to interact
    await locator.FillAsync("value");
}
```

### Selector Quick Reference

| Strategy | Syntax | Best For |
|----------|--------|----------|
| By ID | `page.Locator("#my-id")` | Elements with known IDs |
| By name | `page.Locator("input[name='Email']")` | Form fields |
| By type | `page.Locator("input[type='email']")` | Input type matching |
| By placeholder | `page.Locator("input[placeholder*='search' i]")` | Fuzzy text match (case-insensitive) |
| By text | `page.Locator("button:has-text('Save')")` | Buttons and links by visible text |
| By exact text | `page.GetByText("Save", new() { Exact = true })` | Exact text match |
| By label | `page.GetByLabel("Email")` | Inputs associated with `<label>` |
| By role | `page.GetByRole(AriaRole.Button, new() { Name = "Save" })` | Accessible role + name |
| By test ID | `page.GetByTestId("submit-btn")` | Elements with `data-testid` attribute |
| CSS class | `page.Locator(".btn-primary")` | Elements by CSS class |
| Descendant | `page.Locator("form >> input[type='text']")` | Scoped within a parent |
| Nth child | `page.Locator("table tr").Nth(2)` | Specific row/item by index |

### Blazor Identity Selector Gotcha

Blazor Identity generates IDs with dots (e.g. `Input.Email`). CSS interprets dots as class selectors:

```csharp
// WRONG — CSS sees id="Input" with class="Email"
page.Locator("#Input.Email");

// CORRECT — attribute selector matches the full id value
page.Locator("input[id='Input.Email']");
page.Locator("input[name='Input.Email']");
```

---

### Text Inputs

#### Standard text input

```csharp
// By id
var nameField = page.Locator("#edit-item-Name");
await nameField.FillAsync("My Item Name");

// By name attribute
var emailField = page.Locator("input[name='Email']");
await emailField.FillAsync("user@example.com");

// By label text (finds the input associated with a <label>)
var descField = page.GetByLabel("Description");
await descField.FillAsync("A detailed description");
```

#### Fill vs Type

```csharp
// FillAsync — clears field first, sets value instantly (preferred)
await page.Locator("#search").FillAsync("search term");

// TypeAsync — simulates keystrokes one at a time (triggers key events)
await page.Locator("#search").TypeAsync("search term", new() { Delay = 50 });
```

**Use `FillAsync`** for most cases. Use `TypeAsync` when you need to trigger `oninput`/`onkeyup` event handlers that drive behavior (autocomplete, live search, Blazor `@bind:event="oninput"`).

#### Clear a field

```csharp
await page.Locator("#search").FillAsync("");
```

#### Read current value

```csharp
string currentValue = await page.Locator("#edit-item-Name").InputValueAsync();
```

---

### Number Inputs

```csharp
// Set a numeric value (still a string in the DOM)
await page.Locator("input[type='number']#edit-item-SortOrder").FillAsync("42");
await page.Locator("input[type='number']#edit-item-Amount").FillAsync("99.95");
```

---

### Password Inputs

```csharp
var passwordField = page.Locator("input[type='password']").First;
await passwordField.FillAsync("my-secret-password");
```

---

### Textareas

```csharp
// Fill a multiline textarea
var notesField = page.Locator("textarea#edit-item-Notes");
await notesField.FillAsync("Line 1\nLine 2\nLine 3");

// Read current content
string notes = await notesField.InputValueAsync();
```

---

### Checkboxes

```csharp
// Check (set to true)
await page.Locator("#edit-item-Enabled").CheckAsync();

// Uncheck (set to false)
await page.Locator("#edit-item-Enabled").UncheckAsync();

// Set to a specific state
await page.Locator("#edit-item-Enabled").SetCheckedAsync(true);

// Read current state
bool isChecked = await page.Locator("#edit-item-Enabled").IsCheckedAsync();
```

#### Toggle Switches (Bootstrap form-switch)

Bootstrap toggle switches are just styled checkboxes — same API:

```csharp
// Toggle switches use the same check/uncheck pattern
// HTML: <input type="checkbox" role="switch" class="form-check-input" id="edit-item-Enabled" />
await page.Locator("#edit-item-Enabled").SetCheckedAsync(true);
```

---

### Radio Buttons

```csharp
// Select a specific radio button by value
await page.Locator("input[name='priority'][value='high']").CheckAsync();

// Select by label text
await page.GetByLabel("High Priority").CheckAsync();

// Read which radio is selected
bool isHigh = await page.Locator("input[name='priority'][value='high']").IsCheckedAsync();
```

---

### Select Dropdowns (`<select>`)

#### Single select

```csharp
// Select by value attribute
await page.Locator("#edit-item-Status").SelectOptionAsync("Active");

// Select by visible text (label)
await page.Locator("#edit-item-Status").SelectOptionAsync(new SelectOptionValue { Label = "Active" });

// Select by index (0-based)
await page.Locator("#edit-item-Status").SelectOptionAsync(new SelectOptionValue { Index = 2 });

// Select by value attribute (explicit)
await page.Locator("#edit-item-CategoryId").SelectOptionAsync(new SelectOptionValue
{
    Value = "00000000-0000-0000-0000-000000000001"
});
```

#### Multi-select (`<select multiple>`)

```csharp
// Select multiple values at once
await page.Locator("#edit-item-Tags").SelectOptionAsync(new[] { "tag-1", "tag-2", "tag-3" });

// Select by label text
await page.Locator("#edit-item-Tags").SelectOptionAsync(new[]
{
    new SelectOptionValue { Label = "Important" },
    new SelectOptionValue { Label = "Urgent" },
});

// Clear all selections then select new ones
await page.Locator("#edit-item-Tags").SelectOptionAsync(Array.Empty<string>());
await page.Locator("#edit-item-Tags").SelectOptionAsync(new[] { "tag-1" });
```

#### Read selected value(s)

```csharp
// Single select — get current value
string selectedValue = await page.Locator("#edit-item-Status").InputValueAsync();

// Multi-select — get all selected option values
var selectedValues = await page.Locator("#edit-item-Tags option:checked")
    .AllAsync();
var values = new List<string>();
foreach (var option in selectedValues) {
    values.Add(await option.GetAttributeAsync("value") ?? "");
}
```

#### Count options in a select

```csharp
int optionCount = await page.Locator("#edit-item-CategoryId option").CountAsync();
```

---

### Date and Time Inputs

```csharp
// Date input (<input type="date">)
await page.Locator("#edit-item-EffectiveDate").FillAsync("2025-07-25");

// DateTime-local input (<input type="datetime-local">)
await page.Locator("#edit-item-StartDate").FillAsync("2025-07-25T14:30");

// Time input (<input type="time">)
await page.Locator("#edit-item-ScheduledTime").FillAsync("14:30");
```

---

### Buttons

#### Click a button

```csharp
// By text content
await page.Locator("button:has-text('Save')").ClickAsync();

// By role + name (accessible way)
await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

// By CSS selector
await page.Locator("button.btn-success").First.ClickAsync();

// By id
await page.Locator("#submit-button").ClickAsync();
```

#### Double-click

```csharp
await page.Locator("#my-element").DblClickAsync();
```

#### Right-click (context menu)

```csharp
await page.Locator("#my-element").ClickAsync(new() { Button = MouseButton.Right });
```

#### Click with modifier keys

```csharp
// Ctrl+Click (open in new tab behavior)
await page.Locator("a.my-link").ClickAsync(new() { Modifiers = new[] { KeyboardModifier.Control } });
```

#### Disabled button check

```csharp
bool isDisabled = await page.Locator("#submit-button").IsDisabledAsync();
bool isEnabled = await page.Locator("#submit-button").IsEnabledAsync();
```

---

### Links

```csharp
// Click a link by text
await page.Locator("a:has-text('Settings')").ClickAsync();

// Click by role
await page.GetByRole(AriaRole.Link, new() { Name = "Settings" }).ClickAsync();

// Get the href value
string? href = await page.Locator("a:has-text('Settings')").GetAttributeAsync("href");
```

---

### Tables

#### Read cell content

```csharp
// Get text from a specific cell (row 2, column 3 — both 0-based via Nth)
string cellText = await page.Locator("table.table tbody tr").Nth(1)
    .Locator("td").Nth(2)
    .InnerTextAsync();
```

#### Count rows

```csharp
int rowCount = await page.Locator("table.table tbody tr").CountAsync();
```

#### Click edit button in a specific row

```csharp
// Find row containing "My Item" and click its Edit button
var row = page.Locator("table.table tbody tr", new() { HasText = "My Item" });
await row.Locator("button:has-text('Edit')").ClickAsync();
```

#### Iterate all rows

```csharp
var rows = page.Locator("table.table tbody tr");
int count = await rows.CountAsync();

for (int i = 0; i < count; i++) {
    var row = rows.Nth(i);
    string name = await row.Locator("td").Nth(1).InnerTextAsync();
    string status = await row.Locator("td").Nth(2).InnerTextAsync();
    Console.WriteLine($"Row {i}: {name} — {status}");
}
```

---

### Tab Navigation (Bootstrap Tabs)

```csharp
// Click a tab by its text
await page.Locator("button.nav-link:has-text('Details')").ClickAsync();
await page.WaitForTimeoutAsync(300); // Let tab content render

// Verify tab content is visible
bool isActive = await page.Locator("#tabDetails").IsVisibleAsync();
```

---

### Modal Dialogs

```csharp
// Wait for modal to appear
await page.Locator(".modal.show").WaitForAsync();

// Interact with content inside the modal
await page.Locator(".modal.show input[name='ConfirmText']").FillAsync("DELETE");

// Click modal button
await page.Locator(".modal.show button:has-text('Confirm')").ClickAsync();

// Wait for modal to close
await page.Locator(".modal.show").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
```

---

### File Uploads

```csharp
// Single file
await page.Locator("input[type='file']").SetInputFilesAsync("path/to/file.pdf");

// Multiple files
await page.Locator("input[type='file']").SetInputFilesAsync(new[]
{
    "path/to/file1.pdf",
    "path/to/file2.png"
});

// Clear file input
await page.Locator("input[type='file']").SetInputFilesAsync(Array.Empty<string>());
```

---

### Reading Element State & Content

```csharp
// Get visible text content
string text = await page.Locator("h1.page-title").InnerTextAsync();

// Get inner HTML
string html = await page.Locator(".alert").InnerHTMLAsync();

// Get an attribute value
string? className = await page.Locator("#my-element").GetAttributeAsync("class");
string? dataValue = await page.Locator("#my-element").GetAttributeAsync("data-item-id");

// Check visibility
bool isVisible = await page.Locator("#advanced-section").IsVisibleAsync();
bool isHidden = await page.Locator("#loading-spinner").IsHiddenAsync();

// Check if element exists in DOM at all
int count = await page.Locator("#optional-section").CountAsync();
bool exists = count > 0;
```

---

### Keyboard Actions

```csharp
// Press a single key
await page.Keyboard.PressAsync("Enter");
await page.Keyboard.PressAsync("Escape");
await page.Keyboard.PressAsync("Tab");

// Key combo
await page.Keyboard.PressAsync("Control+a");  // Select all
await page.Keyboard.PressAsync("Control+c");  // Copy

// Press key on a specific element
await page.Locator("#search").PressAsync("Enter");

// Hold and release (for drag or modifier keys)
await page.Keyboard.DownAsync("Shift");
await page.Locator("#item-1").ClickAsync();
await page.Locator("#item-5").ClickAsync();
await page.Keyboard.UpAsync("Shift");
```

---

### Scrolling

```csharp
// Scroll element into view
await page.Locator("#bottom-section").ScrollIntoViewIfNeededAsync();

// Scroll the page by pixels
await page.EvaluateAsync("window.scrollBy(0, 500)");

// Scroll to bottom of page
await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");

// Scroll to top
await page.EvaluateAsync("window.scrollTo(0, 0)");
```

---

### Hover

```csharp
// Hover over an element (triggers tooltips, dropdown menus)
await page.Locator(".dropdown-toggle").HoverAsync();
await page.WaitForTimeoutAsync(300); // Let dropdown render

// Then click a dropdown item
await page.Locator(".dropdown-menu a:has-text('Delete')").ClickAsync();
```

---

### Focus

```csharp
// Set focus to an element
await page.Locator("#edit-item-Name").FocusAsync();

// Blur (remove focus) via JavaScript
await page.Locator("#edit-item-Name").BlurAsync();
```

---

### Waiting for Elements

```csharp
// Wait for element to appear
await page.Locator("#results-table").WaitForAsync();

// Wait for element to disappear (loading spinner)
await page.Locator(".loading-spinner").WaitForAsync(new() { State = WaitForSelectorState.Hidden });

// Wait for element to be attached to DOM (but maybe not visible)
await page.Locator("#lazy-section").WaitForAsync(new() { State = WaitForSelectorState.Attached });

// Wait with timeout
await page.Locator("#slow-content").WaitForAsync(new() { Timeout = 10000 });
```

---

### Running JavaScript on the Page

```csharp
// Execute JS and get a return value
string title = await page.EvaluateAsync<string>("document.title");
int scrollY = await page.EvaluateAsync<int>("window.scrollY");

// Execute JS with no return
await page.EvaluateAsync("document.querySelector('#my-id').style.border = '2px solid red'");

// Execute JS on a specific element
string tagName = await page.Locator("#my-element").EvaluateAsync<string>("el => el.tagName");

// Call a global JS function
await page.EvaluateAsync("window.myApp.resetForm()");
```

---

### Multi-Selector Fallback Pattern

For resilient form detection, try multiple selectors in order:

```csharp
var usernameSelectors = new[]
{
    "input[name='username']",
    "input[name='Email']",
    "input[name='Input.Email']",        // Blazor Identity
    "input[type='email']",
    "input[autocomplete='username']",
    "input[autocomplete='username webauthn']",  // Blazor Identity with passkey
    "input[placeholder*='user' i]",
    "input[placeholder*='email' i]"
};

ILocator? usernameField = null;

foreach (var selector in usernameSelectors) {
    try {
        var locator = page.Locator(selector).First;
        if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync()) {
            usernameField = locator;
            break;
        }
    } catch { /* Continue to next selector */ }
}
```

**Why try/catch per selector:** Some selectors may throw if the page structure is unexpected. Swallowing errors per selector lets the fallback chain continue.

---

### Form Submission

Try multiple submit strategies:

```csharp
var submitSelectors = new[]
{
    "button[type='submit']",
    "input[type='submit']",
    "button:has-text('Log in')",
    "button:has-text('Login')",
    "button:has-text('Sign in')",
    "#login-submit",
};

foreach (var selector in submitSelectors) {
    try {
        var locator = page.Locator(selector).First;
        if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync()) {
            await locator.ClickAsync();
            return;
        }
    } catch { /* Continue to next selector */ }
}

// Fallback: Press Enter on the password field
var passwordField = page.Locator("input[type='password']").First;
if (await passwordField.CountAsync() > 0) {
    await passwordField.PressAsync("Enter");
}
```

---

### Detecting Login Forms

Quick check if the current page has a login form (useful for detecting unexpected redirects):

```csharp
private static async Task<bool> HasLoginFormAsync(IPage page)
{
    try {
        var passwordField = page.Locator("input[type='password']").First;
        if (await passwordField.CountAsync() > 0 && await passwordField.IsVisibleAsync()) {
            // Also verify there's a username/email field
            var emailField = page.Locator("input[type='email']").First;
            if (await emailField.CountAsync() > 0 && await emailField.IsVisibleAsync()) {
                return true;
            }
        }
    } catch { /* Ignore errors */ }

    return false;
}
```

---

### File Downloads

Playwright intercepts browser downloads so you can save them, read their contents, and validate what was exported.

#### Basic download — click a link/button that triggers a file download

```csharp
// Start waiting for the download BEFORE clicking
var downloadTask = page.WaitForDownloadAsync();
await page.Locator("a:has-text('Export CSV')").ClickAsync();
var download = await downloadTask;

// Get the suggested filename from the server (Content-Disposition header)
string suggestedName = download.SuggestedFilename;
Console.WriteLine($"Downloaded: {suggestedName}");

// Save to a specific path on disk
string savePath = Path.Combine(outputDir, suggestedName);
await download.SaveAsAsync(savePath);

Console.WriteLine($"Saved to: {savePath}");
Console.WriteLine($"File size: {new FileInfo(savePath).Length} bytes");
```

#### Download and read CSV contents into objects

```csharp
// Trigger the download
var downloadTask = page.WaitForDownloadAsync();
await page.Locator("#export-csv-button").ClickAsync();
var download = await downloadTask;

// Save to a temp file, read it, then clean up
string tempPath = Path.Combine(Path.GetTempPath(), download.SuggestedFilename);
await download.SaveAsAsync(tempPath);

// Read CSV lines into objects
var lines = await File.ReadAllLinesAsync(tempPath);
Console.WriteLine($"CSV has {lines.Length} lines (including header)");

// Parse CSV into a list (skip header row)
List<CsvRow> rows = new();
for (int i = 1; i < lines.Length; i++) {
    var parts = lines[i].Split(',');
    if (parts.Length >= 3) {
        rows.Add(new CsvRow
        {
            Name = parts[0].Trim('"'),
            Status = parts[1].Trim('"'),
            Count = int.TryParse(parts[2].Trim('"'), out var c) ? c : 0
        });
    }
}

Console.WriteLine($"Parsed {rows.Count} data rows from CSV");

// Clean up temp file
File.Delete(tempPath);
```

```csharp
// Simple CSV row class
internal class CsvRow
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public int Count { get; set; }
}
```

#### Read file contents without saving to disk

```csharp
// Use the download stream directly (no temp file needed)
var downloadTask = page.WaitForDownloadAsync();
await page.Locator("button:has-text('Download Report')").ClickAsync();
var download = await downloadTask;

// Read straight from the stream
await using var stream = await download.CreateReadStreamAsync();
using var reader = new StreamReader(stream);
string content = await reader.ReadToEndAsync();

Console.WriteLine($"Downloaded {content.Length} characters");
Console.WriteLine($"First 200 chars: {content[..Math.Min(200, content.Length)]}");
```

#### Download a JSON file and deserialize it

```csharp
var downloadTask = page.WaitForDownloadAsync();
await page.Locator("a:has-text('Export JSON')").ClickAsync();
var download = await downloadTask;

await using var stream = await download.CreateReadStreamAsync();
var report = await JsonSerializer.DeserializeAsync<ExportReport>(stream, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
});

if (report != null) {
    Console.WriteLine($"Report: {report.Title}");
    Console.WriteLine($"Items: {report.Items.Count}");
}
```

#### Download triggered by navigation (e.g. clicking a direct file URL)

```csharp
// Some links navigate to a URL that returns a file instead of a page
// Use page.ExpectDownloadAsync for this pattern
var downloadTask = page.WaitForDownloadAsync();
await page.GotoAsync("https://localhost:5001/api/Data/ExportAll");
var download = await downloadTask;

string savePath = Path.Combine(outputDir, download.SuggestedFilename);
await download.SaveAsAsync(savePath);
```

#### Check for download errors

```csharp
var downloadTask = page.WaitForDownloadAsync();
await page.Locator("#download-btn").ClickAsync();
var download = await downloadTask;

// Check if download failed (server error, cancelled, etc.)
string? failure = await download.FailureAsync();
if (failure != null) {
    Console.WriteLine($"Download failed: {failure}");
} else {
    Console.WriteLine($"Download OK: {download.SuggestedFilename}");
    string savePath = Path.Combine(outputDir, download.SuggestedFilename);
    await download.SaveAsAsync(savePath);
}
```

#### Multiple downloads from one action

```csharp
// Some buttons trigger multiple file downloads
// Use a loop to capture them
var downloads = new List<IDownload>();

page.Download += (_, download) => downloads.Add(download);

await page.Locator("#export-all-button").ClickAsync();

// Wait a moment for all downloads to start
await page.WaitForTimeoutAsync(2000);

Console.WriteLine($"Captured {downloads.Count} downloads");

foreach (var dl in downloads) {
    string savePath = Path.Combine(outputDir, dl.SuggestedFilename);
    await dl.SaveAsAsync(savePath);
    Console.WriteLine($"  Saved: {dl.SuggestedFilename} ({new FileInfo(savePath).Length} bytes)");
}
```

---

### Element Interaction Quick Reference

| Element | Find It | Interact |
|---------|---------|----------|
| Text input | `page.Locator("#my-id")` | `.FillAsync("value")` |
| Password | `page.Locator("input[type='password']")` | `.FillAsync("secret")` |
| Textarea | `page.Locator("textarea#notes")` | `.FillAsync("multiline\ntext")` |
| Number | `page.Locator("input[type='number']")` | `.FillAsync("42")` |
| Date | `page.Locator("input[type='date']")` | `.FillAsync("2025-07-25")` |
| Checkbox | `page.Locator("#enabled")` | `.SetCheckedAsync(true)` |
| Radio | `page.Locator("input[value='high']")` | `.CheckAsync()` |
| Select | `page.Locator("select#status")` | `.SelectOptionAsync("Active")` |
| Multi-select | `page.Locator("select#tags")` | `.SelectOptionAsync(new[] {"a","b"})` |
| Button | `page.GetByRole(AriaRole.Button, ...)` | `.ClickAsync()` |
| Link | `page.GetByRole(AriaRole.Link, ...)` | `.ClickAsync()` |
| File upload | `page.Locator("input[type='file']")` | `.SetInputFilesAsync("path")` |
| File download | `page.WaitForDownloadAsync()` | `.SaveAsAsync("path")` / `.CreateReadStreamAsync()` |
| Any element | `page.Locator(".my-class")` | `.InnerTextAsync()` / `.GetAttributeAsync("href")` |

---

## Auth Flow Pattern

For pages requiring `[Authorize]`, BrowserSnapshot captures a 3-step auth flow:

```
Step 1: initial page (login form or redirect)  → 1-initial.png
Step 2: form filled with credentials            → 2-filled.png
Step 3: result after login submit               → 3-result.png
```

### Implementation

```csharp
private static async Task CaptureAuthFlowAsync(
    IPage page, string route, string outputDir, int settleDelay,
    string username, string password, ScreenshotResult result)
{
    // Step 1: Screenshot the initial page
    var step1Path = PathSanitizer.GetOutputFilePath(outputDir, route, "1-initial.png");
    PathSanitizer.EnsureDirectoryExists(step1Path);
    await page.ScreenshotAsync(new PageScreenshotOptions { Path = step1Path, FullPage = true });

    // Try to find and fill login form
    var loginFormFound = await TryFillLoginFormAsync(page, username, password);

    if (loginFormFound) {
        // Step 2: Screenshot with form filled
        await page.WaitForTimeoutAsync(500);
        var step2Path = PathSanitizer.GetOutputFilePath(outputDir, route, "2-filled.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = step2Path, FullPage = true });

        // Submit and wait
        await SubmitLoginFormAsync(page);
        await page.WaitForTimeoutAsync(settleDelay);

        // Step 3: Screenshot the result
        var step3Path = PathSanitizer.GetOutputFilePath(outputDir, route, "3-result.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = step3Path, FullPage = true });

        result.AuthFlowCompleted = true;
    } else {
        // No login form found — save as default
        var defaultPath = PathSanitizer.GetOutputFilePath(outputDir, route, "default.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = defaultPath, FullPage = true });
        result.AuthFlowNote = "No login form detected";
    }
}
```

### Decision Logic

```
Route requires auth?
├── YES → Always run auth flow
│   ├── Login form found → 3-step capture (1-initial, 2-filled, 3-result)
│   └── No login form → Capture as default.png + note
└── NO → Check for unexpected redirect
    ├── Has login form → Run auth flow (note: "Redirected to login (unexpected)")
    └── No login form → Standard single screenshot (default.png)
```

---

## Console Error Capture

Subscribe to JavaScript console errors before navigation:

```csharp
List<string> consoleErrors = [];

page.Console += (_, msg) =>
{
    if (msg.Type == "error")
        consoleErrors.Add(msg.Text);
};

// Navigate and capture...

result.ConsoleErrors = consoleErrors;
```

This captures any `console.error()` calls during page load, useful for detecting broken JS, failed API calls, or missing resources.

---

## Parallel Execution

### SemaphoreSlim + ConcurrentDictionary Pattern

Process routes in parallel but output results in order:

```csharp
var results = new ConcurrentDictionary<int, ScreenshotResult>();
var nextIndexToWrite = 0;
var writeLock = new object();

var semaphore = new SemaphoreSlim(maxThreads);

var tasks = routeInfos.Select((routeInfo, index) => Task.Run(async () =>
{
    await semaphore.WaitAsync();
    try {
        var result = await CaptureScreenshotAsync(browser, routeInfo, index, ...);
        results[index] = result;
        WriteResultsInOrder(results, ref nextIndexToWrite, writeLock);
    } finally {
        semaphore.Release();
    }
})).ToArray();

await Task.WhenAll(tasks);

// Flush any remaining
WriteResultsInOrder(results, ref nextIndexToWrite, writeLock, flush: true);
```

### Ordered Writer

```csharp
private static void WriteResultsInOrder(
    ConcurrentDictionary<int, ScreenshotResult> results,
    ref int nextIndexToWrite, object writeLock, bool flush = false)
{
    lock (writeLock) {
        while (results.TryGetValue(nextIndexToWrite, out var result)) {
            WriteResult(result);
            results.TryRemove(nextIndexToWrite, out _);
            nextIndexToWrite++;
        }

        if (flush && results.Count > 0) {
            foreach (var kvp in results.OrderBy(k => k.Key)) {
                WriteResult(kvp.Value);
            }
            results.Clear();
        }
    }
}
```

**Why ordered output:** Without ordering, parallel capture writes `[3/10]` before `[1/10]`, making console output unreadable. The `ConcurrentDictionary` + index pattern keeps output sequential.

---

## Metadata Output

Write a JSON file alongside each screenshot for downstream tools (WorkspaceReporter):

```csharp
var metadata = new ScreenshotMetadata
{
    Route = result.Route,
    Url = result.Url,
    StatusCode = result.StatusCode,
    FileSize = result.FileSize,
    IsSuspiciouslySmall = result.IsSuspiciouslySmall,
    RetryAttempted = result.RetryAttempted,
    ConsoleErrors = result.ConsoleErrors,
    CapturedAt = result.CapturedAt,
    IsSuccess = result.IsSuccess,
    IsHttpError = result.IsHttpError,
    IsError = result.IsError,
    ErrorMessage = result.ErrorMessage,
    RequiresAuth = result.RequiresAuth,
    AuthFlowCompleted = result.AuthFlowCompleted,
    AuthFlowNote = result.AuthFlowNote
};

var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
{
    WriteIndented = true
});

await File.WriteAllTextAsync(metadataPath, json);
```

---

## Configuration Reference

| Variable | Default | Description |
|----------|---------|-------------|
| `BASE_URL` | `https://localhost:5001` | Target web app URL |
| `CSV_PATH` | `pages.csv` | Route CSV from EndpointMapper |
| `OUTPUT_DIR` | `page-snapshots` | Screenshot output directory |
| `SCREENSHOT_BROWSER` | `chromium` | Browser engine: chromium, firefox, webkit |
| `SCREENSHOT_VIEWPORT` | Playwright default | Viewport size as `WIDTHxHEIGHT` (e.g. `1920x1080`) |
| `MAX_THREADS` | `10` | Parallel capture workers |
| `PAGE_SETTLE_DELAY_MS` | `3000` | Post-NetworkIdle wait before capture (ms) |
| `LOGIN_USERNAME` | `admin` | Username for auth flow |
| `LOGIN_PASSWORD` | `admin` | Password for auth flow |
| `START_DELAY_MS` | `5000` | Startup delay for server warmup (ms) |

### Viewport Parsing

```csharp
private static (int? width, int? height) ParseViewport(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return (null, null);

    var parts = value.Split('x', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 2 &&
        int.TryParse(parts[0], out var w) &&
        int.TryParse(parts[1], out var h)) {
        return (w, h);
    }

    return (null, null);
}
```

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| Blank screenshots | Blazor not rendered yet | Increase `PAGE_SETTLE_DELAY_MS` (try 5000) |
| Screenshots < 10KB | SPA render timing | Auto-retry handles this; if persistent, increase settle delay |
| `Playwright install` fails | Missing system deps | Run `pwsh playwright.ps1 install-deps` for OS libraries |
| Timeout on navigation | Server not ready | Increase `START_DELAY_MS` or use Aspire `WaitFor` |
| Login flow fails | Selectors don't match | Add your app's selectors to the fallback arrays |
| Firefox/WebKit crashes | Browser compatibility | Stick with chromium (most stable) |
| Memory issues with many routes | Too many parallel contexts | Reduce `MAX_THREADS` to 3-5 |

### Blazor Identity Selector Gotcha

Blazor Identity generates IDs with dots (e.g. `Input.Email`). CSS selectors need attribute-style matching:

```csharp
// DON'T: CSS interprets the dot as a class selector
"#Input.Email"  // Looks for id="Input" with class="Email"

// DO: Use attribute selectors
"input[name='Input.Email']"  // Matches name attribute exactly
"input[id='Input.Email']"    // Matches id attribute exactly
```

---

## Reusing Playwright in New Tools

When creating a new FreeTools tool that needs Playwright (e.g. AccessibilityScanner):

1. Add the package reference to your `.csproj`:
   ```xml
   <PackageReference Include="Microsoft.Playwright" Version="1.56.0" />
   ```

2. Add a project reference to `FreeTools.Core` for shared utilities:
   ```xml
   <ProjectReference Include="..\FreeTools.Core\FreeTools.Core.csproj" />
   ```

3. Follow the core lifecycle pattern from this guide
4. Call `EnsurePlaywrightBrowsersInstalledAsync()` before `Playwright.CreateAsync()`
5. Use `NetworkIdle` + settle delay for Blazor pages
6. Create one `BrowserContext` per route for isolation
7. Always close contexts in `finally` blocks

---

*Category: 007_patterns*
*Last Updated: 2025-07-25*
*Source: FreeTools.BrowserSnapshot/Program.cs*
