using System.CommandLine;

// =============================================================================
// Command Line Arguments
// =============================================================================
var skipCleanupOption = new Option<bool>(
    name: "--skip-cleanup",
    description: "Skip cleanup of old run folders (keep timestamped backups)",
    getDefaultValue: () => false);

var keepBackupsOption = new Option<int>(
    name: "--keep-backups",
    description: "Number of timestamped backup folders to keep (0 = none, just latest)",
    getDefaultValue: () => 0);

var targetOption = new Option<string>(
    name: "--target",
    description: "Target project to analyze (default: 'FreeExamples')",
    getDefaultValue: () => "FreeExamples");

var rootCommand = new RootCommand("FreeTools AppHost - Run analysis tools against Blazor web projects")
{
    skipCleanupOption,
    keepBackupsOption,
    targetOption
};

rootCommand.SetHandler((skipCleanup, keepBackups, target) =>
{
    AppHostRunner.Run(args, skipCleanup, keepBackups, target);
}, skipCleanupOption, keepBackupsOption, targetOption);

await rootCommand.InvokeAsync(args);

// =============================================================================
// AppHost Runner Class
// =============================================================================
static class AppHostRunner
{
    // Robustness settings
    private const int WebAppStartupDelayMs = 5000;      // Wait for web app to fully start
    private const int ToolStartupDelayMs = 2000;        // Delay between tool launches
    private const int HttpToolDelayMs = 3000;           // Extra delay for HTTP-dependent tools

    public static void Run(string[] hostArgs, bool skipCleanup, int keepBackups, string target)
    {
        var builder = DistributedApplication.CreateBuilder(hostArgs);

        // =============================================================================
        // Configuration
        // =============================================================================
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var toolsRoot = GetToolsRoot();
        var docsRoot = Path.Combine(toolsRoot, "Docs");
        var runsRoot = Path.Combine(docsRoot, "runs");

        // Define repo root (FreeExamples is a sibling folder to FreeTools)
        // toolsRoot = .../FreeTools/FreeTools/  -> go up one level to find FreeExamples and .git
        var repoRoot = Path.GetDirectoryName(toolsRoot) ?? toolsRoot;
        var projectRoot = Path.GetFullPath(Path.Combine(repoRoot, target));

        // Get current git branch name for folder naming (search from repoRoot where .git lives)
        var branchName = GetGitBranch(repoRoot) ?? "unknown";
        var safeBranchName = SanitizeFolderName(branchName);

        Console.WriteLine("============================================================");
        Console.WriteLine(" FreeTools.AppHost — Project Analysis");
        Console.WriteLine("============================================================");
        Console.WriteLine($"Tools Root:     {toolsRoot}");
        Console.WriteLine($"Repo Root:      {repoRoot}");
        Console.WriteLine($"Branch:         {branchName}");
        Console.WriteLine($"Target:         {target}");
        Console.WriteLine($"Project Root:   {projectRoot}");
        Console.WriteLine($"Keep Backups:   {(keepBackups == 0 ? "None (latest only)" : keepBackups.ToString())}");
        Console.WriteLine("------------------------------------------------------------");

        // =============================================================================
        // Web App - Run FreeExamples for HTTP testing
        // Pin to the same HTTPS port as launchSettings.json so the database
        // ApplicationURL stays consistent across runs.
        // LocalModeUrl overrides the DB ApplicationURL setting — ensures App.razor
        // builds correct absolute URLs for CSS/JS resources (_content/MudBlazor, etc.)
        // =============================================================================
        var webApp = builder.AddProject<Projects.FreeExamples>("freeexamples-webapp")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("LocalModeUrl", "https://localhost:7271/")
            .WithEndpoint("https", endpoint =>
            {
                endpoint.Port = 7271;
                endpoint.IsProxied = false;
            })
            .WithEndpoint("http", endpoint =>
            {
                endpoint.Port = 5111;
                endpoint.IsProxied = false;
            });

        Console.WriteLine("  [WebApp] FreeExamples - https://localhost:7271 (fixed port)");

        // =============================================================================
        // Static Analysis (no web app dependency)
        // =============================================================================
        var projectConfig = new ProjectConfig(target, projectRoot, safeBranchName, toolsRoot);

        Console.WriteLine($"  {target}:");
        Console.WriteLine($"    Root:   {projectConfig.ProjectRoot}");
        Console.WriteLine($"    Output: {projectConfig.LatestDir}");

        // Backup previous 'latest' if keeping backups
        if (keepBackups > 0 && !skipCleanup)
        {
            BackupLatestFolder(projectConfig.LatestDir, projectConfig.ProjectRunsDir, timestamp, keepBackups);
        }
        else if (!skipCleanup && Directory.Exists(projectConfig.LatestDir))
        {
            // Clean previous run data to avoid displaying stale screenshots
            Console.WriteLine($"  [Cleanup] Deleting previous run: {projectConfig.LatestDir}");
            try
            {
                Directory.Delete(projectConfig.LatestDir, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [Cleanup] Warning: Could not fully clean previous run: {ex.Message}");
            }
        }

        Directory.CreateDirectory(projectConfig.LatestDir);
        Directory.CreateDirectory(projectConfig.SnapshotsDir);

        // Static analysis tools (no web app dependency)
        var endpointMapper = builder.AddProject<Projects.FreeTools_EndpointMapper>("endpoint-mapper")
            .WithArgs(projectConfig.ProjectRoot, projectConfig.PagesCsv)
            .WithEnvironment("START_DELAY_MS", ToolStartupDelayMs.ToString());

        var inventory = builder.AddProject<Projects.FreeTools_WorkspaceInventory>("inventory")
            .WithArgs(projectConfig.ProjectRoot, projectConfig.InventoryCsv)
            .WithEnvironment("MAX_THREADS", "10")
            .WithEnvironment("START_DELAY_MS", ToolStartupDelayMs.ToString());

        // =============================================================================
        // HTTP Tools - Example (requires running web app)
        // =============================================================================
        var poker = builder.AddProject<Projects.FreeTools_EndpointPoker>("poker")
            .WithEnvironment("BASE_URL", webApp.GetEndpoint("https"))
            .WithEnvironment("CSV_PATH", projectConfig.PagesCsv)
            .WithEnvironment("OUTPUT_DIR", projectConfig.SnapshotsDir)
            .WithEnvironment("MAX_THREADS", "10")
            .WithEnvironment("TENANT_CODE", "tenant1")
            .WithEnvironment("START_DELAY_MS", (WebAppStartupDelayMs + HttpToolDelayMs).ToString())
            .WaitFor(webApp)
            .WaitForCompletion(endpointMapper);

        var browser = builder.AddProject<Projects.FreeTools_BrowserSnapshot>("browser")
            .WithEnvironment("BASE_URL", webApp.GetEndpoint("https"))
            .WithEnvironment("CSV_PATH", projectConfig.PagesCsv)
            .WithEnvironment("OUTPUT_DIR", projectConfig.SnapshotsDir)
            .WithEnvironment("SCREENSHOT_BROWSER", "chromium")
            .WithEnvironment("MAX_THREADS", "10")
            .WithEnvironment("TENANT_CODE", "tenant1")
            .WithEnvironment("START_DELAY_MS", (WebAppStartupDelayMs + HttpToolDelayMs).ToString())
            .WaitFor(webApp)
            .WaitForCompletion(endpointMapper);

        // =============================================================================
        // Accessibility Scanner (requires running web app + page list)
        // =============================================================================
        // Extra pages that the EndpointMapper can't auto-discover:
        // - Parameterized routes (EditSampleItem needs an actual ID)
        // - Non-example pages (Settings, Profile, About, etc.)
        // - Auth flow pages (Login, Logout, etc.)
        // The EndpointMapper already finds all @page routes including V1-V12 variants,
        // but routes with {parameters} get skipped. These fill that gap.
        var extraPages = string.Join(";",
            // Parameterized example pages — need concrete IDs
            "/{TenantCode}/Examples/EditSampleItem/New",
            // Non-example pages (app infrastructure)
            "/{TenantCode}/About",
            "/{TenantCode}/Profile",
            "/{TenantCode}/ChangePassword",
            // Settings pages (admin — scanner logs in as admin)
            "/{TenantCode}/Settings/AppSettings",
            "/{TenantCode}/Settings/Users",
            "/{TenantCode}/Settings/AddUser",
            "/{TenantCode}/Settings/UserGroups",
            "/{TenantCode}/Settings/AddUserGroup",
            "/{TenantCode}/Settings/Departments",
            "/{TenantCode}/Settings/AddDepartment",
            "/{TenantCode}/Settings/DepartmentGroups",
            "/{TenantCode}/Settings/AddDepartmentGroup",
            "/{TenantCode}/Settings/Tenants",
            "/{TenantCode}/Settings/AddTenant",
            "/{TenantCode}/Settings/Tags",
            "/{TenantCode}/Settings/AddTag",
            "/{TenantCode}/Settings/Language",
            "/{TenantCode}/Settings/UDF",
            "/{TenantCode}/Settings/Files",
            "/{TenantCode}/Settings/DeletedRecords",
            // Auth / error pages
            "/Login",
            "/not-found",
            "/DatabaseOffline",
            "/ServerUpdated",
            "/Authorization/AccessDenied",
            "/Authorization/InvalidUser",
            "/Authorization/NoLocalAccount"
        );

        var a11y = builder.AddProject<Projects.FreeTools_AccessibilityScanner>("a11y-scanner")
            .WithEnvironment("BASE_URL", webApp.GetEndpoint("https"))
            .WithEnvironment("CSV_PATH", projectConfig.PagesCsv)
            .WithEnvironment("OUTPUT_DIR", projectConfig.SnapshotsDir)
            .WithEnvironment("TENANT_CODE", "tenant1")
            .WithEnvironment("LOGIN_USERNAME", "admin")
            .WithEnvironment("LOGIN_PASSWORD", "admin")
            .WithEnvironment("EXTRA_PAGES", extraPages)
            .WithEnvironment("START_DELAY_MS", (WebAppStartupDelayMs + HttpToolDelayMs).ToString())
            .WaitFor(webApp)
            .WaitForCompletion(endpointMapper);

        Console.WriteLine("  [A11y] AccessibilityScanner - targeting localhost (AppHost mode)");

        // =============================================================================
        // Report Generation
        // =============================================================================
        var reporter = builder.AddProject<Projects.FreeTools_WorkspaceReporter>("reporter")
            .WithEnvironment("REPO_ROOT", projectConfig.ProjectRoot)
            .WithEnvironment("OUTPUT_PATH", projectConfig.ReportPath)
            .WithEnvironment("WORKSPACE_CSV", projectConfig.InventoryCsv)
            .WithEnvironment("PAGES_CSV", projectConfig.PagesCsv)
            .WithEnvironment("SNAPSHOTS_DIR", projectConfig.SnapshotsDir)
            .WithEnvironment("TARGET_PROJECT", target)
            .WithEnvironment("START_DELAY_MS", ToolStartupDelayMs.ToString())
            .WaitForCompletion(inventory)
            .WaitForCompletion(endpointMapper)
            .WaitForCompletion(poker)
            .WaitForCompletion(browser)
            .WaitForCompletion(a11y);

        Console.WriteLine("============================================================");
        Console.WriteLine();
        Console.WriteLine(">> Look for 'Login to the dashboard at' URL in output below <<");
        Console.WriteLine();

        builder.Build().Run();
    }

    // =============================================================================
    // Helper Functions
    // =============================================================================
    
    private static void BackupLatestFolder(string latestDir, string projectRunsDir, string timestamp, int keepBackups)
    {
        if (!Directory.Exists(latestDir)) return;

        try
        {
            // Move 'latest' to timestamped backup
            var backupDir = Path.Combine(projectRunsDir, timestamp);
            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, recursive: true);
            }
            Directory.Move(latestDir, backupDir);
            Console.WriteLine($"  [Backup] Moved previous latest to {timestamp}");

            // Cleanup old backups beyond keepBackups count
            var backupDirs = Directory.GetDirectories(projectRunsDir)
                .Where(d => !Path.GetFileName(d).Equals("latest", StringComparison.OrdinalIgnoreCase))
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d => d.Name)
                .ToList();

            if (backupDirs.Count > keepBackups)
            {
                foreach (var dir in backupDirs.Skip(keepBackups))
                {
                    try
                    {
                        Directory.Delete(dir.FullName, recursive: true);
                        Console.WriteLine($"  [Cleanup] Deleted old backup: {dir.Name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [Cleanup] Failed to delete {dir.Name}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [Backup] Failed: {ex.Message}");
        }
    }

    public static string GetToolsRoot()
    {
        // Try environment variable first
        var envRoot = Environment.GetEnvironmentVariable("FREETOOLS_ROOT");
        if (!string.IsNullOrEmpty(envRoot) && Directory.Exists(envRoot))
            return envRoot;

        // Walk up from base directory
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "FreeTools.Core"))) return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // Last resort: relative path from typical bin location
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static string? GetGitBranch(string repoPath)
    {
        try
        {
            var headPath = Path.Combine(repoPath, ".git", "HEAD");
            if (File.Exists(headPath))
            {
                var headContent = File.ReadAllText(headPath).Trim();
                if (headContent.StartsWith("ref: refs/heads/"))
                {
                    return headContent.Substring("ref: refs/heads/".Length);
                }
                // Detached HEAD - return short hash
                return headContent.Length > 7 ? headContent[..7] : headContent;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    private static string SanitizeFolderName(string name)
    {
        // Replace invalid characters with underscores
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            name = name.Replace(c, '_');
        }
        // Also replace slashes (for branch names like feature/xyz)
        name = name.Replace('/', '_').Replace('\\', '_');
        return name;
    }
}

// =============================================================================
// Project Configuration Record
// =============================================================================
record ProjectConfig(string Name, string ProjectRoot, string Branch, string ToolsRoot)
{
    // Output folder: Docs/runs/{ProjectName}/{Branch}/latest
    public string ProjectRunsDir { get; } = Path.Combine(ToolsRoot, "Docs", "runs", Name, Branch);
    public string LatestDir => Path.Combine(ProjectRunsDir, "latest");
    public string PagesCsv => Path.Combine(LatestDir, "pages.csv");
    public string InventoryCsv => Path.Combine(LatestDir, "workspace-inventory.csv");
    public string SnapshotsDir => Path.Combine(LatestDir, "snapshots");
    public string ReportPath => Path.Combine(LatestDir, $"{Name}-Report.md");
}
