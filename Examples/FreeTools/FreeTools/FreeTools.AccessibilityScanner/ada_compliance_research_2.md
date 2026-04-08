



Invite team members
You said:

hey Claude.  I work for Washington state university as a software engineer.   We make web pages using blazor c# .net10.   I have been told we need to make all of our websites ada compliant. what does that even mean.  give me an eli 5. then a series of suggestions on what and how and then examples of actual code for html that is bad and good with like dos and donts of the top 20 most comon things

11:40 AM
Hey! Great question - ADA compliance is super important and honestly not as scary as it sounds once you understand it.

ELI5 Version
ADA (Americans with Disabilities Act) compliance for websites means making sure everyone can use your site, including people who:

Can't see well or are blind (using screen readers)
Can't use a mouse (keyboard-only navigation)
Are colorblind
Are deaf or hard of hearing
Have other disabilities
Think of it like building ramps alongside stairs - you're making sure there are multiple ways for people to access the same content.

What You Need to Know
The Standard: You'll likely be aiming for WCAG 2.1 Level AA compliance (Web Content Accessibility Guidelines). This is the most common standard for government and educational institutions.

Four Core Principles (POUR):

Perceivable - Users can perceive the information
Operable - Users can operate the interface
Understandable - Content and interface are understandable
Robust - Works with assistive technologies
How to Approach This
Audit your current sites - Use tools like WAVE, axe DevTools, or Lighthouse in Chrome
Train your team - Everyone needs to understand the basics
Build accessibility into your workflow - Make it part of code reviews
Test with real assistive tech - Use a screen reader (NVDA is free)
Document your standards - Create a style guide for your team
Top 20 Common Issues - Dos and Don'ts
1. Images Need Alt Text
❌ BAD:

html
<img src="wsu-logo.png">
<img src="campus-map.jpg" alt="image">
✅ GOOD:

html
<img src="wsu-logo.png" alt="Washington State University logo">
<img src="campus-map.jpg" alt="Map showing Pullman campus with library, student center, and parking lots">
<!-- Decorative images should have empty alt -->
<img src="decorative-divider.png" alt="">
2. Proper Heading Hierarchy
❌ BAD:

html
<h1>Welcome to WSU</h1>
<h3>Our Programs</h3>  <!-- Skipped h2 -->
<h2>Computer Science</h2>  <!-- Out of order -->
✅ GOOD:

html
<h1>Welcome to WSU</h1>
<h2>Our Programs</h2>
<h3>Computer Science</h3>
<h3>Engineering</h3>
<h2>Admissions</h2>
3. Form Labels
❌ BAD:

html
<input type="text" placeholder="Enter your name">
Email: <input type="email">
✅ GOOD:

html
<label for="fullName">Full Name:</label>
<input type="text" id="fullName" name="fullName">

<label for="email">Email Address:</label>
<input type="email" id="email" name="email" required>
4. Color Contrast
❌ BAD:

html
<p style="color: #999; background: #fff;">Important notice</p>
<!-- Contrast ratio too low -->
✅ GOOD:

html
<p style="color: #595959; background: #fff;">Important notice</p>
<!-- Contrast ratio 7:1 (AAA level) -->
5. Keyboard Navigation
❌ BAD:

html
<div onclick="submitForm()">Submit</div>
<!-- Can't be accessed with keyboard -->
✅ GOOD:

html
<button type="submit" onclick="submitForm()">Submit</button>
<!-- Or if you must use div: -->
<div role="button" tabindex="0" 
     onclick="submitForm()" 
     onkeypress="if(event.key==='Enter') submitForm()">
  Submit
</div>
6. Link Text
❌ BAD:

html
<a href="apply.html">Click here</a> for admissions
To learn more, <a href="info.html">click here</a>
✅ GOOD:

html
<a href="apply.html">Apply for admission</a>
<a href="info.html">Learn more about our programs</a>
7. Skip Navigation Links
❌ BAD:

html
<header>
  <!-- Long navigation menu -->
</header>
<main>Content...</main>
✅ GOOD:

html
<a href="#main-content" class="skip-link">Skip to main content</a>
<header>
  <!-- Long navigation menu -->
</header>
<main id="main-content">Content...</main>

<style>
.skip-link {
  position: absolute;
  top: -40px;
  left: 0;
}
.skip-link:focus {
  top: 0;
}
</style>
8. ARIA Labels for Icons
❌ BAD:

html
<button><i class="fa fa-search"></i></button>
<a href="mailto:info@wsu.edu"><i class="fa fa-envelope"></i></a>
✅ GOOD:

html
<button aria-label="Search"><i class="fa fa-search" aria-hidden="true"></i></button>
<a href="mailto:info@wsu.edu" aria-label="Email us">
  <i class="fa fa-envelope" aria-hidden="true"></i>
</a>
9. Tables
❌ BAD:

html
<table>
  <tr>
    <td>Name</td><td>Grade</td>
  </tr>
  <tr>
    <td>John</td><td>A</td>
  </tr>
</table>
✅ GOOD:

html
<table>
  <caption>Student Grades - Fall 2024</caption>
  <thead>
    <tr>
      <th scope="col">Name</th>
      <th scope="col">Grade</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>John</td>
      <td>A</td>
    </tr>
  </tbody>
</table>
10. Language Declaration
❌ BAD:

html
<html>
<head>...</head>
✅ GOOD:

html
<html lang="en">
<head>...</head>
11. Video Captions
❌ BAD:

html
<video src="welcome.mp4" controls></video>
✅ GOOD:

html
<video controls>
  <source src="welcome.mp4" type="video/mp4">
  <track kind="captions" src="welcome-captions.vtt" srclang="en" label="English">
</video>
12. Focus Indicators
❌ BAD:

css
/* Don't do this! */
button:focus {
  outline: none;
}
✅ GOOD:

css
button:focus {
  outline: 2px solid #0066cc;
  outline-offset: 2px;
}
13. Semantic HTML
❌ BAD:

html
<div class="header">
  <div class="nav">...</div>
</div>
<div class="content">...</div>
<div class="footer">...</div>
✅ GOOD:

html
<header>
  <nav>...</nav>
</header>
<main>...</main>
<footer>...</footer>
14. Error Messages
❌ BAD:

html
<input type="email" required>
<!-- Browser default error only -->
✅ GOOD:

html
<label for="email">Email:</label>
<input type="email" id="email" 
       aria-describedby="email-error" 
       aria-invalid="true" required>
<span id="email-error" role="alert">
  Please enter a valid email address
</span>
15. Accessible Dropdowns
❌ BAD:

html
<div class="dropdown">
  <div onclick="toggleMenu()">Menu</div>
  <div id="menu">
    <div onclick="navigate()">Item 1</div>
  </div>
</div>
✅ GOOD:

html
<button aria-expanded="false" 
        aria-controls="menu" 
        onclick="toggleMenu()">
  Menu
</button>
<ul id="menu" role="menu" hidden>
  <li role="menuitem">
    <a href="page1.html">Item 1</a>
  </li>
</ul>
16. PDF Links
❌ BAD:

html
<a href="syllabus.pdf">Syllabus</a>
✅ GOOD:

html
<a href="syllabus.pdf">
  Course Syllabus (PDF, 245KB)
</a>
17. Required Fields
❌ BAD:

html
Name: *<input type="text">
✅ GOOD:

html
<label for="name">
  Name <span aria-label="required">*</span>
</label>
<input type="text" id="name" required aria-required="true">
18. Responsive Text
❌ BAD:

css
p {
  font-size: 10px; /* Too small */
}
✅ GOOD:

css
p {
  font-size: 16px; /* Or 1rem */
  line-height: 1.5;
}
19. Modal Dialogs
❌ BAD:

html
<div class="modal" style="display:block">
  <div>Modal content</div>
</div>
✅ GOOD:

html
<div role="dialog" 
     aria-labelledby="modal-title" 
     aria-modal="true">
  <h2 id="modal-title">Confirmation</h2>
  <p>Are you sure?</p>
  <button>Yes</button>
  <button>No</button>
</div>
<!-- Trap focus inside modal when open -->
20. Accessible Carousels
❌ BAD:

html
<div class="carousel" auto-rotate="true">
  <div>Slide 1</div>
  <div>Slide 2</div>
</div>
✅ GOOD:

html
<section aria-label="Featured announcements" 
         aria-roledescription="carousel">
  <button aria-label="Pause slideshow">Pause</button>
  <div role="group" aria-label="Slide 1 of 3">
    <h3>Announcement</h3>
  </div>
  <button aria-label="Previous slide">Previous</button>
  <button aria-label="Next slide">Next</button>
</section>
Blazor-Specific Tips
Since you're using Blazor, here are some specific considerations:

razor
@* Use proper accessibility attributes in Blazor components *@
<button @onclick="HandleClick" 
        aria-label="@ButtonLabel"
        disabled="@IsProcessing">
    @ButtonText
</button>

@* Announce dynamic content changes *@
<div role="status" aria-live="polite" aria-atomic="true">
    @StatusMessage
</div>
Tools to Use
Browser Extensions: WAVE, axe DevTools
Screen Readers: NVDA (Windows, free), JAWS, VoiceOver (Mac)
Automated Testing: Playwright with axe-core for .NET
Manual Testing: Keyboard-only navigation (unplug your mouse!)
Good luck with making your sites accessible! It's a journey, but it makes a real difference for real people. 🎓

ok so these tools, do they run against a live website? or can i point them at code and have it check that. 

how do they work, identify the best and most commonly used ones

11:41 AM
Great question! The tools work differently depending on what stage you're at. Let me break this down:

How Accessibility Testing Tools Work
Live Website Tools (Browser-Based)
These run against rendered HTML in your browser - so yes, the site needs to be running (localhost is fine).

Why? They analyze the actual DOM, computed styles, color contrast ratios, and how elements are actually displayed to users.

Static Code Analysis
Some tools can scan source code before it's running, but they're more limited because they can't see things like:

Actual color contrast (needs computed CSS)
Dynamic content loaded by JavaScript
Tab order and focus states
How ARIA attributes actually work together
The Best Tools (Ranked by Common Usage)
1. axe DevTools ⭐ MOST POPULAR
Type: Browser extension (Chrome, Firefox, Edge)
How it works:

Right-click → Inspect → "axe DevTools" tab
Click "Scan ALL of my page"
Shows violations with severity levels
Points to exact elements in your code
Pros:

Catches ~57% of WCAG issues automatically (industry leading)
Free version is excellent
Shows code snippets and how to fix
Can test localhost
Cons:

Only tests one page at a time (free version)
Can't test behind login easily
Install: Chrome Web Store → "axe DevTools"
Usage: Navigate to page → F12 → axe DevTools tab → Scan
2. WAVE (WebAIM)
Type: Browser extension or online tool
How it works:

Extension adds icons directly on your page showing issues
Color-codes: red (errors), yellow (alerts), green (features)
Pros:

Visual overlay makes issues super obvious
Great for learning what's wrong WHERE
Free and very user-friendly
Can test localhost with extension
Cons:

Can make complex pages messy with all the icons
Less detailed reporting than axe
Install: Chrome/Firefox → "WAVE Evaluation Tool"
Usage: Navigate to page → Click WAVE icon
3. Lighthouse (Built into Chrome)
Type: Built-in Chrome DevTools
How it works:

Audits performance, SEO, PWA, and accessibility
Generates a score 0-100
Pros:

Already installed in Chrome
Good overview scoring
Tests localhost
Part of CI/CD pipelines
Cons:

Less comprehensive than axe for accessibility
Catches fewer issues (~30-40% of WCAG)
Usage: F12 → "Lighthouse" tab → Check "Accessibility" → Generate report
4. Accessibility Insights for Web (Microsoft)
Type: Browser extension
How it works:

Two modes: FastPass (automated) and Assessment (guided manual testing)
Walks you through manual checks with instructions
Pros:

Great for comprehensive testing (combines auto + manual)
Excellent learning tool with guided tests
Free and thorough
Cons:

More time-consuming (manual testing required for full assessment)
Install: Chrome/Edge → "Accessibility Insights for Web"
5. Pa11y 🔧 FOR CI/CD
Type: Command-line tool (Node.js)
How it works:

Runs headless tests against URLs
Can integrate into build pipelines
Pros:

Automates testing in CI/CD
Can test multiple pages
JSON output for reporting
Cons:

Requires setup
Command-line only
Needs running site
bash
npm install -g pa11y
pa11y https://localhost:5000
6. axe-core for .NET 🔧 FOR AUTOMATED TESTING
Type: NuGet package for integration tests
How it works:

Integrates with Selenium, Playwright, or Puppeteer
Runs accessibility checks in your automated tests
Pros:

Tests as part of your normal test suite
Catches issues before deployment
Same engine as axe DevTools
Example with Playwright:

csharp
using Deque.AxeCore.Playwright;

[Test]
public async Task Homepage_ShouldBeAccessible()
{
    await Page.GotoAsync("https://localhost:5001");
    
    var results = await Page.RunAxe();
    
    Assert.That(results.Violations, Is.Empty, 
        results.Violations.ToString());
}
7. Accessibility Checker (Visual Studio Extension)
Type: Visual Studio extension
How it works:

Analyzes HTML/Razor files in VS
Highlights issues as you code
Pros:

Catches issues during development
Works with static code
Cons:

Limited compared to runtime tools
Can't test dynamic behavior
For Blazor Specifically
Since you're using Blazor, here's my recommended workflow:

Development Phase:
Visual Studio - Install accessibility checker extension
Browser DevTools - Use axe DevTools while developing on localhost
Manual Testing - Use keyboard only (Tab, Enter, Space, Arrow keys)
Testing Phase:
csharp
// Add to your integration tests
using Microsoft.Playwright;
using Deque.AxeCore.Playwright;

public class AccessibilityTests
{
    [Test]
    public async Task AllPages_ShouldMeetWCAGAA()
    {
        var pages = new[] 
        { 
            "/", 
            "/about", 
            "/programs" 
        };
        
        foreach (var page in pages)
        {
            await Page.GotoAsync($"https://localhost:5001{page}");
            var results = await Page.RunAxe();
            
            Assert.That(results.Violations, Is.Empty,
                $"Accessibility violations on {page}:\n" +
                results.Violations.ToString());
        }
    }
}
CI/CD Phase:
Integrate Pa11y or axe-core into your build pipeline
Fail builds if critical violations found
My Recommendation for WSU
Start with this combo:

axe DevTools browser extension - For daily development (free, easy, comprehensive)
WAVE - For visual learning and quick checks
Keyboard testing - Manual testing with no mouse
Screen reader - Download NVDA (free) and test weekly
Later: Add axe-core to Playwright/Selenium tests
Why this order? You need to learn what issues look like first (manual tools), then automate catching them (integration tests).

Quick Start Command
bash
# If you want to test right now:
# 1. Install axe DevTools extension
# 2. Navigate to your localhost:5000 (or whatever)
# 3. F12 → axe DevTools → Scan ALL of my page
# 4. Fix red items first, then yellow
The tools complement each other - automated tools catch ~40-60% of issues, but you still need manual testing for things like:

Keyboard navigation flow
Screen reader experience
Logical reading order
Context and clarity
Does this help clarify how they work? Want me to dive deeper into setting up any specific tool?

how many of these can easily be triggered through c#.  ideally with nuget packages.  I want to write a c# program console application i can run using comand line args for like ./myexe --input1 value1 --input2 value2 or wahtever to pass in the url for the root site, and username/password etc for testing

11:42 AM
Perfect! You're thinking like a proper engineer - automate all the things. Let me show you exactly how to build this.

TL;DR: Yes, You Can Do This in C#
The best options are:

Playwright + axe-core (BEST - most comprehensive)
Selenium + axe-core (also good, more mature)
Deque.AxeCore.Playwright or Selenium.Axe NuGet packages
Complete Console App Example
Here's a full working example using Playwright + axe:

1. Create the Project
bash
dotnet new console -n AccessibilityScanner
cd AccessibilityScanner
dotnet add package Microsoft.Playwright
dotnet add package Deque.AxeCore.Playwright
dotnet add package System.CommandLine
2. Install Playwright Browsers
bash
# After adding the package, run this once:
pwsh bin/Debug/net10.0/playwright.ps1 install
# Or if you don't have PowerShell:
playwright install
3. The Complete Code
Program.cs:

csharp
using System.CommandLine;
using Microsoft.Playwright;
using Deque.AxeCore.Playwright;
using Deque.AxeCore.Commons;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootUrlOption = new Option<string>(
            name: "--url",
            description: "Root URL to scan (e.g., https://wsu.edu)")
        { IsRequired = true };

        var usernameOption = new Option<string?>(
            name: "--username",
            description: "Username for authentication (optional)");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "Password for authentication (optional)");

        var pagesOption = new Option<string[]>(
            name: "--pages",
            description: "Additional page paths to scan (e.g., /about /contact)",
            getDefaultValue: () => Array.Empty<string>());

        var outputOption = new Option<string>(
            name: "--output",
            description: "Output file for results (JSON)",
            getDefaultValue: () => "accessibility-results.json");

        var severityOption = new Option<string>(
            name: "--min-severity",
            description: "Minimum severity to report (critical, serious, moderate, minor)",
            getDefaultValue: () => "moderate");

        var rootCommand = new RootCommand("WSU Accessibility Scanner")
        {
            rootUrlOption,
            usernameOption,
            passwordOption,
            pagesOption,
            outputOption,
            severityOption
        };

        rootCommand.SetHandler(async (url, username, password, pages, output, severity) =>
        {
            await RunAccessibilityScan(url, username, password, pages, output, severity);
        }, rootUrlOption, usernameOption, passwordOption, pagesOption, outputOption, severityOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunAccessibilityScan(
        string rootUrl, 
        string? username, 
        string? password, 
        string[] additionalPages,
        string outputFile,
        string minSeverity)
    {
        Console.WriteLine("🔍 WSU Accessibility Scanner Starting...");
        Console.WriteLine($"📍 Root URL: {rootUrl}");
        Console.WriteLine();

        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true // Set to false to watch it run
        });

        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        try
        {
            // Login if credentials provided
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                Console.WriteLine("🔐 Logging in...");
                await LoginToSite(page, rootUrl, username, password);
            }

            // Collect all pages to scan
            var pagesToScan = new List<string> { "/" };
            pagesToScan.AddRange(additionalPages);

            var allResults = new List<PageAccessibilityResult>();

            // Scan each page
            foreach (var pagePath in pagesToScan)
            {
                var fullUrl = new Uri(new Uri(rootUrl), pagePath).ToString();
                Console.WriteLine($"\n📄 Scanning: {fullUrl}");

                await page.GotoAsync(fullUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

                // Run axe scan
                var axeResults = await page.RunAxe();

                var result = new PageAccessibilityResult
                {
                    Url = fullUrl,
                    Violations = axeResults.Violations,
                    Passes = axeResults.Passes.Count,
                    Incomplete = axeResults.Incomplete.Count,
                    Timestamp = DateTime.UtcNow
                };

                allResults.Add(result);

                // Print summary
                PrintPageSummary(result, minSeverity);
            }

            // Save results
            await SaveResults(allResults, outputFile);

            // Print overall summary
            PrintOverallSummary(allResults, minSeverity);

            // Exit code based on violations
            var criticalViolations = allResults.Sum(r => 
                r.Violations.Count(v => v.Impact == "critical" || v.Impact == "serious"));
            
            Environment.Exit(criticalViolations > 0 ? 1 : 0);
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    static async Task LoginToSite(IPage page, string rootUrl, string username, string password)
    {
        // Customize this based on your login page
        await page.GotoAsync($"{rootUrl}/login");
        
        // Example - adjust selectors for your actual login form
        await page.FillAsync("input[name='username']", username);
        await page.FillAsync("input[name='password']", password);
        await page.ClickAsync("button[type='submit']");
        
        // Wait for navigation after login
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        Console.WriteLine("✅ Login successful");
    }

    static void PrintPageSummary(PageAccessibilityResult result, string minSeverity)
    {
        var filteredViolations = FilterBySeverity(result.Violations, minSeverity);

        Console.WriteLine($"  ✓ Passes: {result.Passes}");
        Console.WriteLine($"  ⚠ Violations: {filteredViolations.Count}");
        
        if (filteredViolations.Any())
        {
            var grouped = filteredViolations.GroupBy(v => v.Impact);
            foreach (var group in grouped.OrderByDescending(g => SeverityWeight(g.Key)))
            {
                Console.WriteLine($"    • {group.Key}: {group.Count()}");
            }

            Console.WriteLine("\n  Top Issues:");
            foreach (var violation in filteredViolations.Take(3))
            {
                Console.WriteLine($"    ❌ [{violation.Impact}] {violation.Description}");
                Console.WriteLine($"       Help: {violation.HelpUrl}");
                Console.WriteLine($"       Affected elements: {violation.Nodes.Count()}");
            }
        }
    }

    static void PrintOverallSummary(List<PageAccessibilityResult> results, string minSeverity)
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("📊 OVERALL SUMMARY");
        Console.WriteLine(new string('=', 60));

        var totalViolations = results.Sum(r => r.Violations.Count());
        var criticalCount = results.Sum(r => r.Violations.Count(v => v.Impact == "critical"));
        var seriousCount = results.Sum(r => r.Violations.Count(v => v.Impact == "serious"));
        var moderateCount = results.Sum(r => r.Violations.Count(v => v.Impact == "moderate"));
        var minorCount = results.Sum(r => r.Violations.Count(v => v.Impact == "minor"));

        Console.WriteLine($"Pages Scanned: {results.Count}");
        Console.WriteLine($"Total Violations: {totalViolations}");
        Console.WriteLine($"  🔴 Critical: {criticalCount}");
        Console.WriteLine($"  🟠 Serious: {seriousCount}");
        Console.WriteLine($"  🟡 Moderate: {moderateCount}");
        Console.WriteLine($"  🟢 Minor: {minorCount}");

        if (criticalCount + seriousCount > 0)
        {
            Console.WriteLine("\n⚠️  HIGH PRIORITY: Fix critical and serious issues first!");
        }
        else
        {
            Console.WriteLine("\n✅ No critical or serious violations found!");
        }
    }

    static List<AxeResultItem> FilterBySeverity(IEnumerable<AxeResultItem> violations, string minSeverity)
    {
        var severityLevels = new Dictionary<string, int>
        {
            { "critical", 4 },
            { "serious", 3 },
            { "moderate", 2 },
            { "minor", 1 }
        };

        var minLevel = severityLevels.GetValueOrDefault(minSeverity.ToLower(), 2);

        return violations
            .Where(v => severityLevels.GetValueOrDefault(v.Impact, 0) >= minLevel)
            .ToList();
    }

    static int SeverityWeight(string impact) => impact switch
    {
        "critical" => 4,
        "serious" => 3,
        "moderate" => 2,
        "minor" => 1,
        _ => 0
    };

    static async Task SaveResults(List<PageAccessibilityResult> results, string outputFile)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(outputFile, json);
        Console.WriteLine($"\n💾 Results saved to: {outputFile}");
    }
}

class PageAccessibilityResult
{
    public string Url { get; set; } = "";
    public IEnumerable<AxeResultItem> Violations { get; set; } = Array.Empty<AxeResultItem>();
    public int Passes { get; set; }
    public int Incomplete { get; set; }
    public DateTime Timestamp { get; set; }
}
4. Usage Examples
bash
# Basic scan
dotnet run -- --url https://wsu.edu

# Scan multiple pages
dotnet run -- --url https://wsu.edu --pages /about /admissions /programs

# With authentication
dotnet run -- --url https://internal.wsu.edu --username myuser --password mypass

# Only show critical/serious issues
dotnet run -- --url https://wsu.edu --min-severity serious

# Custom output file
dotnet run -- --url https://wsu.edu --output reports/scan-results.json

# Full example
dotnet run -- \
  --url https://wsu.edu \
  --username testuser \
  --password testpass \
  --pages /about /contact /programs /admissions \
  --output wsu-accessibility-report.json \
  --min-severity moderate
5. Build as Executable
bash
# Publish as single-file executable
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Then run it:
.\bin\Release\net10.0\win-x64\publish\AccessibilityScanner.exe --url https://wsu.edu
Alternative: Selenium Version
If you prefer Selenium (more mature, wider browser support):

bash
dotnet add package Selenium.WebDriver
dotnet add package Selenium.Axe
dotnet add package WebDriverManager
csharp
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Selenium.Axe;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

class SeleniumScanner
{
    static async Task ScanWithSelenium(string url)
    {
        // Auto-download ChromeDriver
        new DriverManager().SetUpDriver(new ChromeConfig());

        var options = new ChromeOptions();
        options.AddArgument("--headless");
        
        using var driver = new ChromeDriver(options);
        
        driver.Navigate().GoToUrl(url);
        
        // Run axe scan
        var axeResult = driver.Analyze();
        
        Console.WriteLine($"Violations: {axeResult.Violations.Length}");
        
        foreach (var violation in axeResult.Violations)
        {
            Console.WriteLine($"[{violation.Impact}] {violation.Help}");
            Console.WriteLine($"  Elements affected: {violation.Nodes.Length}");
        }
    }
}
Advanced Features You Can Add
1. Sitemap Crawler
csharp
// Automatically discover all pages from sitemap.xml
var sitemap = await FetchSitemap($"{rootUrl}/sitemap.xml");
var allPages = ParseSitemapUrls(sitemap);
2. Screenshot on Failure
csharp
if (violations.Any(v => v.Impact == "critical"))
{
    await page.ScreenshotAsync(new() { Path = $"violation-{DateTime.Now:yyyyMMdd-HHmmss}.png" });
}
3. HTML Report Generation
csharp
// Generate beautiful HTML report
await GenerateHtmlReport(allResults, "accessibility-report.html");
4. CI/CD Integration
yaml
# Azure DevOps pipeline
- task: DotNetCoreCLI@2
  inputs:
    command: 'run'
    arguments: '--url $(WebsiteUrl) --min-severity serious'
  displayName: 'Run Accessibility Scan'
Recommended NuGet Packages Summary
Package	Purpose	Stars
Deque.AxeCore.Playwright	Best overall - modern, fast	⭐⭐⭐⭐⭐
Selenium.Axe	Mature, stable, good docs	⭐⭐⭐⭐
System.CommandLine	Argument parsing	⭐⭐⭐⭐⭐
Microsoft.Playwright	Browser automation	⭐⭐⭐⭐⭐
Which Should You Use?
Use Playwright if:

You want the fastest, most modern approach
You're starting fresh
You want better async support
Use Selenium if:

You already have Selenium tests
You need wider browser support (Safari, IE)
Your team knows Selenium
For WSU, I recommend Playwright - it's faster, more reliable, and has better .NET support.

Want me to add any specific features like HTML report generation, sitemap crawling, or specific login flow handling?

so im also ok executing another executable from inside my program.cs  waht are the like... real officail tools we should be using and how do they work.  can we just execute them with cli args then scrape the results?

11:44 AM
Absolutely! This is actually a really smart approach - use the official tools that organizations trust for compliance, then wrap them in your C# orchestrator.

The "Real" Official Tools
These are the tools that accessibility auditors and legal compliance teams actually use:

1. Pa11y ⭐ THE GOLD STANDARD CLI TOOL
What it is: Node.js command-line tool used by UK government, BBC, financial institutions
Why it's trusted: Open source, actively maintained, based on HTML_CodeSniffer

bash
# Install
npm install -g pa11y

# Basic usage
pa11y https://wsu.edu

# With options
pa11y https://wsu.edu --standard WCAG2AA --reporter json > results.json

# Multiple URLs
pa11y https://wsu.edu https://wsu.edu/about --reporter json

# With authentication
pa11y https://internal.wsu.edu --config config.json
Config file (pa11y-config.json):

json
{
  "standard": "WCAG2AA",
  "timeout": 30000,
  "wait": 1000,
  "chromeLaunchConfig": {
    "args": ["--no-sandbox"]
  },
  "actions": [
    "set field #username to myuser",
    "set field #password to mypass",
    "click element button[type=submit]",
    "wait for url to not be https://internal.wsu.edu/login"
  ]
}
Output (JSON):

json
[
  {
    "code": "WCAG2AA.Principle1.Guideline1_1.1_1_1.H37",
    "type": "error",
    "typeCode": 1,
    "message": "Img element missing an alt attribute",
    "context": "<img src=\"logo.png\">",
    "selector": "#header > img",
    "runner": "htmlcs"
  }
]
2. axe-cli (by Deque)
What it is: Official Deque axe command-line tool
Why it's trusted: Deque is THE accessibility company - they literally wrote the standards

bash
# Install
npm install -g @axe-core/cli

# Basic usage
axe https://wsu.edu

# JSON output
axe https://wsu.edu --save results.json

# Multiple URLs from file
axe --show-errors https://wsu.edu https://wsu.edu/about

# With custom rules
axe https://wsu.edu --rules color-contrast,image-alt
Output structure:

json
{
  "violations": [
    {
      "id": "color-contrast",
      "impact": "serious",
      "description": "Elements must have sufficient color contrast",
      "help": "Ensure the contrast ratio...",
      "helpUrl": "https://dequeuniversity.com/rules/axe/4.4/color-contrast",
      "nodes": [
        {
          "html": "<p style=\"color: #999\">Text</p>",
          "target": ["p"],
          "failureSummary": "Fix..."
        }
      ]
    }
  ],
  "passes": [...],
  "incomplete": [...]
}
3. Lighthouse CI (Google)
What it is: Google's official accessibility auditing tool
Why it's trusted: Used by Google, part of Chrome DevTools

bash
# Install
npm install -g @lhci/cli

# Run audit
lhci autorun --collect.url=https://wsu.edu

# Or using lighthouse directly
npm install -g lighthouse
lighthouse https://wsu.edu --output json --output-path=./report.json --only-categories=accessibility
Output (simplified):

json
{
  "categories": {
    "accessibility": {
      "score": 0.85,
      "title": "Accessibility"
    }
  },
  "audits": {
    "image-alt": {
      "score": 0,
      "details": {
        "items": [
          {
            "node": {
              "snippet": "<img src=\"logo.png\">",
              "path": "1,HTML,1,BODY,0,DIV,0,IMG"
            }
          }
        ]
      }
    }
  }
}
4. WAVE API (WebAIM)
What it is: WebAIM's official API (paid, but authoritative)
Why it's trusted: WebAIM = Web Accessibility In Mind, education/gov standard

bash
# API call (requires key)
curl "https://wave.webaim.org/api/request?key=YOUR_KEY&url=https://wsu.edu" > results.json
You can execute this from C# too.

C# Wrapper Implementation
Here's how to execute these and parse results:

csharp
using System.Diagnostics;
using System.Text.Json;

class AccessibilityScanner
{
    // Execute Pa11y
    static async Task<Pa11yResult[]> RunPa11y(string url, string configPath = null)
    {
        var args = $"{url} --reporter json";
        if (configPath != null)
            args += $" --config {configPath}";

        var result = await ExecuteNodeCommand("pa11y", args);
        return JsonSerializer.Deserialize<Pa11yResult[]>(result.Output);
    }

    // Execute axe-cli
    static async Task<AxeCliResult> RunAxe(string url)
    {
        var tempFile = Path.GetTempFileName() + ".json";
        await ExecuteNodeCommand("axe", $"{url} --save {tempFile}");
        
        var json = await File.ReadAllTextAsync(tempFile);
        File.Delete(tempFile);
        
        return JsonSerializer.Deserialize<AxeCliResult>(json);
    }

    // Execute Lighthouse
    static async Task<LighthouseResult> RunLighthouse(string url)
    {
        var tempFile = Path.GetTempFileName() + ".json";
        var args = $"{url} --output json --output-path={tempFile} --only-categories=accessibility --chrome-flags=\"--headless\"";
        
        await ExecuteNodeCommand("lighthouse", args);
        
        var json = await File.ReadAllTextAsync(tempFile);
        File.Delete(tempFile);
        
        return JsonSerializer.Deserialize<LighthouseResult>(json);
    }

    // Generic Node command executor
    static async Task<(string Output, int ExitCode)> ExecuteNodeCommand(string command, string arguments)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe", // or "bash" on Linux/Mac
            Arguments = $"/c {command} {arguments}", // or $"-c \"{command} {arguments}\""
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };
        
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null) error.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            Console.WriteLine($"Error running {command}: {error}");
        }

        return (output.ToString(), process.ExitCode);
    }
}

// Result models
class Pa11yResult
{
    public string code { get; set; }
    public string type { get; set; }
    public int typeCode { get; set; }
    public string message { get; set; }
    public string context { get; set; }
    public string selector { get; set; }
}

class AxeCliResult
{
    public AxeViolation[] violations { get; set; }
    public object[] passes { get; set; }
    public object[] incomplete { get; set; }
}

class AxeViolation
{
    public string id { get; set; }
    public string impact { get; set; }
    public string description { get; set; }
    public string help { get; set; }
    public string helpUrl { get; set; }
    public AxeNode[] nodes { get; set; }
}

class AxeNode
{
    public string html { get; set; }
    public string[] target { get; set; }
    public string failureSummary { get; set; }
}

class LighthouseResult
{
    public LighthouseCategories categories { get; set; }
    public Dictionary<string, LighthouseAudit> audits { get; set; }
}

class LighthouseCategories
{
    public LighthouseCategory accessibility { get; set; }
}

class LighthouseCategory
{
    public double score { get; set; }
    public string title { get; set; }
}

class LighthouseAudit
{
    public double? score { get; set; }
    public string title { get; set; }
    public string description { get; set; }
}
Complete Multi-Tool Scanner
csharp
using System.CommandLine;
using System.Text.Json;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var urlOption = new Option<string>("--url", "URL to scan") { IsRequired = true };
        var toolsOption = new Option<string[]>("--tools", 
            getDefaultValue: () => new[] { "pa11y", "axe", "lighthouse" },
            description: "Tools to run (pa11y, axe, lighthouse, all)");
        var outputOption = new Option<string>("--output", 
            getDefaultValue: () => "accessibility-report.json",
            description: "Output file");

        var rootCommand = new RootCommand("Multi-Tool Accessibility Scanner")
        {
            urlOption,
            toolsOption,
            outputOption
        };

        rootCommand.SetHandler(async (url, tools, output) =>
        {
            await RunScan(url, tools, output);
        }, urlOption, toolsOption, outputOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunScan(string url, string[] tools, string outputFile)
    {
        Console.WriteLine($"🔍 Scanning {url} with tools: {string.Join(", ", tools)}\n");

        var results = new ScanResults
        {
            Url = url,
            Timestamp = DateTime.UtcNow,
            Tools = new Dictionary<string, object>()
        };

        // Check Node.js is installed
        if (!await IsNodeInstalled())
        {
            Console.WriteLine("❌ Node.js not found. Please install Node.js first.");
            return;
        }

        // Run each tool
        if (tools.Contains("pa11y") || tools.Contains("all"))
        {
            Console.WriteLine("🔧 Running Pa11y...");
            try
            {
                var pa11yResults = await RunPa11y(url);
                results.Tools["pa11y"] = pa11yResults;
                Console.WriteLine($"  ✓ Found {pa11yResults.Length} issues");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Pa11y failed: {ex.Message}");
                Console.WriteLine("    Install with: npm install -g pa11y");
            }
        }

        if (tools.Contains("axe") || tools.Contains("all"))
        {
            Console.WriteLine("🔧 Running axe-cli...");
            try
            {
                var axeResults = await RunAxe(url);
                results.Tools["axe"] = axeResults;
                Console.WriteLine($"  ✓ Found {axeResults.violations.Length} violations");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ axe-cli failed: {ex.Message}");
                Console.WriteLine("    Install with: npm install -g @axe-core/cli");
            }
        }

        if (tools.Contains("lighthouse") || tools.Contains("all"))
        {
            Console.WriteLine("🔧 Running Lighthouse...");
            try
            {
                var lighthouseResults = await RunLighthouse(url);
                results.Tools["lighthouse"] = lighthouseResults;
                Console.WriteLine($"  ✓ Accessibility score: {lighthouseResults.categories.accessibility.score * 100}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Lighthouse failed: {ex.Message}");
                Console.WriteLine("    Install with: npm install -g lighthouse");
            }
        }

        // Save combined results
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(outputFile, json);

        Console.WriteLine($"\n💾 Results saved to {outputFile}");
        PrintSummary(results);
    }

    static void PrintSummary(ScanResults results)
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("📊 SUMMARY");
        Console.WriteLine(new string('=', 60));

        if (results.Tools.ContainsKey("pa11y"))
        {
            var pa11y = JsonSerializer.Deserialize<Pa11yResult[]>(
                JsonSerializer.Serialize(results.Tools["pa11y"]));
            var errors = pa11y.Count(r => r.type == "error");
            Console.WriteLine($"Pa11y: {errors} errors, {pa11y.Length - errors} warnings");
        }

        if (results.Tools.ContainsKey("axe"))
        {
            var axe = JsonSerializer.Deserialize<AxeCliResult>(
                JsonSerializer.Serialize(results.Tools["axe"]));
            var critical = axe.violations.Count(v => v.impact == "critical");
            var serious = axe.violations.Count(v => v.impact == "serious");
            Console.WriteLine($"axe: {critical} critical, {serious} serious");
        }

        if (results.Tools.ContainsKey("lighthouse"))
        {
            var lh = JsonSerializer.Deserialize<LighthouseResult>(
                JsonSerializer.Serialize(results.Tools["lighthouse"]));
            var score = lh.categories.accessibility.score * 100;
            Console.WriteLine($"Lighthouse: {score:F0}% accessibility score");
        }
    }

    static async Task<bool> IsNodeInstalled()
    {
        try
        {
            var result = await ExecuteCommand("node", "--version");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    // ... (include ExecuteCommand and other helper methods from above)
}

class ScanResults
{
    public string Url { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Tools { get; set; }
}
Installation Script for Your Team
Create setup.ps1:

powershell
# Install Node.js tools
Write-Host "Installing accessibility tools..." -ForegroundColor Green

npm install -g pa11y
npm install -g @axe-core/cli
npm install -g lighthouse

Write-Host "✓ All tools installed!" -ForegroundColor Green
Write-Host "Run: dotnet run -- --url https://wsu.edu"
Usage Examples
bash
# Single tool
dotnet run -- --url https://wsu.edu --tools pa11y

# Multiple tools
dotnet run -- --url https://wsu.edu --tools pa11y axe lighthouse

# All tools
dotnet run -- --url https://wsu.edu --tools all

# Custom output
dotnet run -- --url https://wsu.edu --output reports/wsu-scan.json
Which Approach is Better?
Approach	Pros	Cons
NuGet (Playwright + axe)	✓ Pure .NET, no dependencies
✓ Better debugging
✓ Type safety	✗ Limited to axe rules
✗ Less official
CLI Tools (Pa11y, axe-cli, Lighthouse)	✓ Industry standard
✓ More comprehensive
✓ Used by auditors	✗ Requires Node.js
✗ Harder to debug
My Recommendation for WSU
Use BOTH:

Daily development: Use Playwright + axe (fast, integrated)
Pre-deployment: Run CLI tools for compliance reports (official, comprehensive)
csharp
// Best of both worlds
if (args.Contains("--quick"))
{
    // Fast: Use Playwright
    await RunPlaywrightScan(url);
}
else if (args.Contains("--official"))
{
    // Thorough: Use CLI tools
    await RunPa11y(url);
    await RunAxe(url);
    await RunLighthouse(url);
}
This gives you speed during development and confidence before releases. Want me to build out the full hybrid version?

come up with a writeup .md doc i can download that summarizes all this. make it long, make it complete, make it have examples of each test suite you are suggesting along with the 20+ dos and donts.  show the example c# program and ways to format the results into a single unified system that any ada scanning tool can do.   im going to take what we have done from that document and provide it to a new chat, so make it complete as we wont have any other context from this chat.  include a summary at the top of my user requests that lead to the creation of the document and a brief at the end that is a message to the ai about the intent of the file and why / where / how to use it

1:27 PM
I'll create a comprehensive markdown document that captures everything we've discussed. Let me build this out for you.


Creating a comprehensive ADA compliance guide with all tools, examples, and code

Continue

1:40 PM
The file is quite large, so let me continue adding the rest of the content:


Continue adding the CLI tools section and remaining content


Want to be notified when Claude responds?


Claude is AI and can make mistakes. Please double-check responses.





hey i was working with claude on this and it ran out of tokens... can you also do this task?  reivew the work done, add any suggestions you might have then dump me out a .md file comprehnsive?

ChatGPT said:

Thought for 13s

Analyzed
Always show details

import textwrap, os, datetime, json, re, pathlib

md = r"""# ADA / WCAG Web Accessibility for Blazor (.NET 10) — Practical Guide + Tooling + Automation

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
Do

Always show details

<img src="logo.png" alt="Washington State University logo">
<img src="map.png" alt="Map showing the Pullman campus with the library, student center, and parking areas.">
<img src="divider.png" alt=""> <!-- decorative -->
2) Use real headings in order (don’t skip levels)
Don’t

Always show details

<h1>Page</h1>
<h3>Section</h3>
<h2>Another Section</h2>
Do

Always show details

<h1>Page</h1>
<h2>Section</h2>
<h3>Subsection</h3>
<h2>Another Section</h2>
3) Forms must have labels (placeholder is not a label)
Don’t

Always show details

<input type="text" placeholder="Name">
Do

Always show details

<label for="name">Full name</label>
<input id="name" name="name" type="text" autocomplete="name">
4) Every input needs an accessible name
Don’t

Always show details

<button><svg><!-- icon --></svg></button>
Do

Always show details

<button aria-label="Search">
  <svg aria-hidden="true"><!-- icon --></svg>
</button>
5) Don’t use color alone to convey meaning
Don’t

Always show details

<span style="color:red">Overdue</span>
Do

Always show details

<span class="status status--overdue">
  Overdue <span class="sr-only">(action required)</span>
</span>
6) Ensure sufficient color contrast
Don’t

Always show details

<p style="color:#999;background:#fff">Important notice</p>
Do

Always show details

<p style="color:#444;background:#fff">Important notice</p>
(Use a contrast checker; aim for WCAG AA.)

7) Make everything keyboard-operable (no “div buttons”)
Don’t

Always show details

<div onclick="save()">Save</div>
Do

Always show details

<button type="button" onclick="save()">Save</button>
8) Don’t remove focus outlines
Don’t

Always show details

*:focus { outline: none; }
Do

Always show details

*:focus { outline: 2px solid currentColor; outline-offset: 2px; }
9) Use semantic landmarks
Don’t

Always show details

<div class="header">...</div>
<div class="content">...</div>
<div class="footer">...</div>
Do

Always show details

<header>...</header>
<nav aria-label="Primary">...</nav>
<main id="main">...</main>
<footer>...</footer>
10) Provide a skip link
Don’t

Always show details

<!-- no skip link -->
Do

Always show details

<a class="skip-link" href="#main">Skip to main content</a>
<main id="main">...</main>
11) Link text must be meaningful out of context
Don’t

Always show details

<a href="/apply">Click here</a>
Do

Always show details

<a href="/apply">Apply for admission</a>
12) Buttons and links are not interchangeable
Don’t

Always show details

<a href="#" onclick="openModal()">Open</a>
Do

Always show details

<button type="button" onclick="openModal()">Open</button>
13) Identify page language
Don’t

Always show details

<html>
Do

Always show details

<html lang="en">
14) Tables must be real tables (with headers)
Don’t

Always show details

<div class="row"><div>Name</div><div>Grade</div></div>
Do

Always show details

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
15) Don’t use tables for layout
Don’t

Always show details

<table><tr><td>Sidebar</td><td>Main</td></tr></table>
Do

Always show details

<div class="layout">
  <aside>Sidebar</aside>
  <main>Main</main>
</div>
16) Errors must be described in text, not just color
Don’t

Always show details

<input aria-invalid="true">
<span class="error" style="color:red">*</span>
Do

Always show details

<label for="email">Email</label>
<input id="email" type="email" aria-describedby="email-error" aria-invalid="true">
<p id="email-error" role="alert">Enter a valid email address.</p>
17) Required fields must be announced
Don’t

Always show details

<label>Name *</label>
<input>
Do

Always show details

<label for="name">Name <span aria-hidden="true">*</span></label>
<input id="name" required aria-required="true">
18) Don’t trap focus (modals/menus must manage focus)
Don’t

Always show details

<div class="modal" style="display:block">...</div>
Do

Always show details

<div role="dialog" aria-modal="true" aria-labelledby="m-title">
  <h2 id="m-title">Confirm</h2>
  <button type="button">Cancel</button>
  <button type="button">OK</button>
</div>
(Also implement focus trap and restore focus on close.)

19) Dropdowns/menus need correct keyboard interaction
Don’t

Always show details

<div onclick="toggle()">Menu</div>
Do

Always show details

<button type="button" aria-expanded="false" aria-controls="menu">Menu</button>
<ul id="menu" hidden>
  <li><a href="/a">Item A</a></li>
</ul>
20) Provide captions/transcripts for media
Don’t

Always show details

<video src="welcome.mp4" controls></video>
Do

Always show details

<video controls>
  <source src="welcome.mp4" type="video/mp4">
  <track kind="captions" src="welcome.en.vtt" srclang="en" label="English captions">
</video>
21) Respect reduced motion
Don’t

Always show details

.spinner { animation: spin 1s linear infinite; }
Do

Always show details

@media (prefers-reduced-motion: reduce) {
  .spinner { animation: none; }
}
22) Don’t auto-play audio/video
Don’t

Always show details

<video autoplay></video>
Do

Always show details

<video controls></video>
23) Touch targets should be large enough (mobile)
Don’t

Always show details

<button style="padding:2px">X</button>
Do

Always show details

<button style="padding:12px">Close</button>
24) Don’t misuse ARIA (prefer semantic HTML first)
Don’t

Always show details

<div role="button">Save</div>
Do

Always show details

<button type="button">Save</button>
If you must use ARIA, do it correctly.

25) Dynamic updates should be announced when needed
Don’t

Always show details

<div id="status">Saved!</div> <!-- changes silently -->
Do

Always show details

<div id="status" role="status" aria-live="polite" aria-atomic="true">Saved!</div>
Blazor-specific guidance (practical patterns)
Prefer semantic HTML in Razor components
Always show details

<button class="btn" @onclick="SaveAsync" disabled="@IsSaving">
  Save
</button>
Accessible icon-only buttons
Always show details

<button class="icon-btn" aria-label="Open search" @onclick="OpenSearch">
  <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24">...</svg>
</button>
Announce status updates
Always show details

<div role="status" aria-live="polite" aria-atomic="true">
  @StatusMessage
</div>
Forms: use <label for> and stable ids
Always show details

<label for="email">Email</label>
<input id="email" @bind="Model.Email" type="email" autocomplete="email" />
Validation messages: ensure they are associated
Always show details

<input id="email" @bind="Model.Email" aria-describedby="email-error" aria-invalid="@HasEmailError" />
@if (HasEmailError)
{
  <p id="email-error" role="alert">@EmailError</p>
}
Testing suites: “official” CLI tools + C# integrated tests
Option A (pure .NET): Playwright + axe (recommended for CI)
Why: Type-safe, debuggable, fast, runs against localhost or staging.

NuGet packages:

Microsoft.Playwright

(axe integration package; commonly from Deque community ecosystem)

Conceptual approach:

Launch headless browser

Navigate to pages

Run axe-core against DOM

Fail test if serious/critical violations exist

Example (sketch; package APIs vary by wrapper):

Always show details

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
Option B (use “official” CLIs): axe CLI + Lighthouse + Pa11y
Why: These are widely used in audit pipelines and produce durable “report artifacts.”

axe CLI (@axe-core/cli)
Install:

Always show details

npm i -g @axe-core/cli
Run:

Always show details

axe https://example.edu --save axe.json
@axe-core/cli is the npm package for axe CLI. citeturn0search7

Lighthouse (Google)
Install:

Always show details

npm i -g lighthouse
Run accessibility-only:

Always show details

lighthouse https://example.edu \
  --only-categories=accessibility \
  --output json --output-path lighthouse.json \
  --chrome-flags="--headless"
Pa11y
Install:

Always show details

npm i -g pa11y
Run JSON report:

Always show details

pa11y https://example.edu --reporter json > pa11y.json
Authentication note: CLIs typically support scripted actions, cookies, or a launch config. For complex SSO, you may prefer Playwright (log in) and then scan using in-process checks.

A unified results format (so any tool can plug in)
You want a single system that can ingest axe results, Lighthouse audits, Pa11y output, and Playwright+axe output.

Unified data model (JSON)
Design goals:

consistent severity levels

tool-agnostic issue shape

include page URL + CSS selector(s) + snippet + help URL

preserve raw tool payload for debugging

Proposed schema
Always show details

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
Severity mapping recommendation
Normalize severities to: critical | serious | moderate | minor | info
Mappings:

axe: impact already uses critical/serious/moderate/minor

pa11y: map error -> serious (or critical for specific codes), warning -> moderate, notice -> minor/info

lighthouse: treat failing audits as moderate unless you decide certain audits should be serious

Example C# console app orchestrator (runs CLIs + unifies output)
Below is a practical, “single binary” orchestrator pattern:

Accepts args (--url, --pages, --username, --password, --tools, --out)

Runs each external CLI via Process

Reads the JSON outputs

Maps to unified schema

Writes a single combined report JSON

This is intentionally “realistic,” but you’ll still need to customize auth flows and selectors for your environment.

dotnet new console + packages
Always show details

dotnet new console -n AdaScanOrchestrator
cd AdaScanOrchestrator
dotnet add package System.CommandLine
Program.cs (single file)
Always show details

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
Example usage
Always show details

# Run all tools against a few pages
dotnet run -- \
  --url https://staging.example.edu \
  --pages / /about /apply \
  --tools all \
  --out unified-a11y-report.json

# Run only axe + lighthouse
dotnet run -- --url https://staging.example.edu --pages / /apply --tools axe lighthouse
Notes on reliability
If a tool is not installed (e.g., axe not in PATH), the orchestrator will mark that tool run as failed and continue.

For internal sites requiring complex auth, use Playwright to login and either:

run in-process axe checks, OR

export session cookies for CLIs (more work), OR

run the CLIs against an authenticated staging environment.

Suggestions and upgrades beyond what we discussed
1) Target standard: WCAG 2.1 AA minimum, consider WCAG 2.2 AA for future-proofing
WCAG 2.2 is current and W3C encourages using the most current version when updating policies. citeturn0search0

2) Create an accessibility “definition of done”
Example checklist for PR review:

All images have meaningful alt (or alt="" if decorative)

All controls reachable by keyboard; visible focus indicator

Inputs have labels; validation messages announced

Semantic landmarks (header/nav/main/footer) present

No “click here” links

Contrast meets AA

Reduced motion supported

Modals trap focus and restore focus

3) Establish severity gates for CI
Common CI policy:

Block merge on critical/serious

Allow moderate/minor but track them

Require accessibility owner sign-off for exceptions

4) Store JSON artifacts from scans
Keep reports for:

regression tracking

procurement / audit evidence

verifying fixes

5) Add a sitemap crawler (optional)
If your site has a sitemap.xml, you can discover pages automatically and scan them. (Be cautious of scanning huge sites—limit scope, avoid infinite loops, respect robots rules for non-prod environments.)

6) Use manual testing for “core user journeys”
Pick 3–5 critical flows:

log in

search

apply/register

submit a form

download course materials

Test them with:

keyboard only

screen reader

200% zoom

Quick reference: what to install
Browser extensions (devs)
axe DevTools

WAVE

Chrome Lighthouse (built-in)

CLI tools (pipeline)
@axe-core/cli citeturn0search7

lighthouse

pa11y

C# / .NET
Microsoft.Playwright (if you go the pure .NET route)

System.CommandLine (for robust args parsing)

Appendix: Tool outputs (what to expect)
axe JSON shape (high level)
violations[] each with:

id (rule id)

impact (critical/serious/moderate/minor)

helpUrl

nodes[] (each affected element, with html snippet + target selectors)

Pa11y JSON shape (high level)
Each item includes:

type (error/warning/notice)

message

selector

context snippet

Lighthouse JSON shape (high level)
Contains audits keyed by audit id

score indicates pass/fail (not always strictly 0/1)

More of a “report” than a “list of nodes”

Message to a future AI (intent of this file)
You are reading this markdown because the user (a Blazor/.NET engineer) needs a self-contained playbook for web accessibility compliance commonly described as “ADA compliance.” The goal is to:

explain the concept in simple terms,

list concrete dos/don’ts with code examples,

recommend practical tooling, and

provide an automation approach with a C# console orchestrator that can run multiple scanners and normalize results into one schema.

When extending this document, prioritize:

actionable engineering steps

reliable automation patterns (CI-friendly exit codes, artifact outputs)

accessibility best practices that reduce legal risk and improve usability

keeping examples concrete and minimal, avoiding ARIA where semantic HTML works

"""

out_path = "/mnt/data/ada_wcag_blazor_accessibility_guide.md"
with open(out_path, "w", encoding="utf-8") as f:
f.write(md)

out_path

Always show details

Result
'/mnt/data/ada_wcag_blazor_accessibility_guide.md'
Done — here’s the complete markdown doc you can download:

Download the comprehensive ADA/WCAG + Blazor/.NET10 accessibility guide (.md)

 


You said:
ok i need a name for a c# project two words   ElegantName  type thing

ChatGPT said:
Here are some solid two-word options in that style (PascalCase-ready):

A11yGuardian

ComplianceKit

AccessAudit

A11yHarbor

A11yBeacon

A11ySentinel

A11yCompass

A11yForge

A11yLens

A11ySignal

A11yMeter

A11yScanner

A11yReport

A11yPipeline

A11yPortal

PolicyCheck

StandardScan

GuidelineGate

WCAGWizard

AuditRunner

ScanSuite

ReportSmith

RuleRunner

QualityGate

CivicAccess

If you want something a bit more “university/public sector” themed:

CampusAccess

PublicAccess

CivicCompliance

InstitutionCheck

AccessibilityHub

If you tell me whether you want it to sound more “security tool” vs “reporting tool,” I’ll narrow it to the best 5.


You said:
whats A11y?

ChatGPT said:
A11y is shorthand for “accessibility.”

It’s a numeronym:

A + 11 letters + y

accessibility = a c c e s s i b i l i t y
(there are 11 letters between the first a and last y)

Same pattern as:

i18n = internationalization

l10n = localization


You said:
thats stuipd... where does that come from?

ChatGPT said:
Yeah, it’s pretty nerdy shorthand.

It comes from a style of abbreviation called a numeronym that started showing up in tech (and earlier in standards/docs) to make long, frequently-used words faster to type in emails, commits, ticket titles, and file names.

The pattern is:

first letter + number of letters in the middle + last letter

So:

accessibility → a11y (11 letters between a and y)

internationalization → i18n

localization → l10n

Why it stuck:

Developers were writing these words constantly (especially in UI/platform work).

It’s short, unambiguous inside that community.

It fits well in identifiers (branch names, packages, tags) without being a mile long.

Outside tech circles it looks like nonsense, and honestly if your team hates it, don’t use it. “AccessibilityScanner” is perfectly fine and clearer.


You said:
how do i dotnet a new console application called accessibility .net10 and have it be a regular int main args console rather than the flat

ChatGPT said:
To create it:

dotnet new console -n Accessibility -f net10.0
cd Accessibility
By default, new console templates use top-level statements (“flat”). To get a classic int Main(string[] args) style, edit Program.cs to this:

using System;

namespace Accessibility;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Hello accessibility!");
        // args is available here
        return 0;
    }
}
That’s obvious, works everywhere, and keeps it “regular” (no top-level statements).


You said:
AccessibilityScanner

ChatGPT said:
AccessibilityScanner is a solid name—clear, boring in the best way, and instantly understandable.

Quick setup:

dotnet new console -n AccessibilityScanner -f net10.0
cd AccessibilityScanner
Then use classic Main by replacing Program.cs with:

using System;

namespace AccessibilityScanner;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("AccessibilityScanner starting...");
        return 0;
    }
}




