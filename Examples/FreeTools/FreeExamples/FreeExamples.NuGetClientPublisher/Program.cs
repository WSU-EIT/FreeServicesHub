using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text.Json;

namespace FreeExamples.NuGetClientPublisher;

/// <summary>
/// Interactive console tool for packing and publishing the FreeExamples.Client NuGet package.
/// Pattern from: FreeGLBA.NuGetClientPublisher
/// </summary>
internal class Program
{
    private static NuGetConfig _config = new();
    private static bool _dryRun = true;
    private static readonly HttpClient _httpClient = new();

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║      FreeExamples.Client NuGet Package Publisher             ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddUserSecrets<Program>(optional: true)
            .AddCommandLine(args)
            .Build();

        configuration.GetSection("NuGet").Bind(_config);

        // Command line version override
        if (args.Length > 1 && args[0] == "--version")
            _config.Version = args[1];

        try {
            var projectPath = ResolveProjectPath();
            if (projectPath == null) return 1;

            ShowConfig();

            // Interactive menu loop
            while (true) {
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.ForegroundColor = _dryRun ? ConsoleColor.Yellow : ConsoleColor.Red;
                Console.WriteLine(_dryRun ? "  MODE: DRY RUN (no changes will be made)" : "  MODE: LIVE (changes WILL be pushed!)");
                Console.ResetColor();
                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.WriteLine("  1. View current configuration");
                Console.WriteLine("  2. Verify project builds successfully");
                Console.WriteLine("  3. Pack NuGet package (build .nupkg)");
                Console.WriteLine("  4. Push to NuGet.org");
                Console.WriteLine("  5. Full publish (Clean → Build → Pack → Push)");
                Console.WriteLine();
                Console.WriteLine("  L. Lookup versions from NuGet.org");
                Console.WriteLine("  V. Change version number");
                Console.WriteLine("  D. Toggle DRY RUN mode");
                Console.WriteLine("  0. Exit");
                Console.WriteLine();
                Console.Write("Select option: ");

                var key = Console.ReadKey();
                Console.WriteLine();
                Console.WriteLine();

                switch (char.ToUpper(key.KeyChar)) {
                    case '1': ShowConfig(); break;
                    case '2': await VerifyBuild(); break;
                    case '3': await PackNuGet(); break;
                    case '4': await PushToNuGet(); break;
                    case '5': await FullPublish(); break;
                    case 'L': await LookupVersions(); break;
                    case 'V': ChangeVersion(); break;
                    case 'D': ToggleDryRun(); break;
                    case '0': Console.WriteLine("Exiting..."); return 0;
                    default: Console.WriteLine("Invalid option."); break;
                }
            }
        } catch (Exception ex) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    #region Menu Actions

    private static void ShowConfig()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("                       CONFIGURATION                           ");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Package ID:    {_config.PackageId}");
        Console.WriteLine($"  Version:       {_config.Version}");
        Console.WriteLine($"  Configuration: {_config.Configuration}");
        Console.WriteLine($"  Source:        {_config.Source}");
        Console.WriteLine($"  Project:       {_config.ProjectPath}");
        Console.WriteLine($"  API Key:       {(string.IsNullOrWhiteSpace(_config.ApiKey) ? "❌ NOT CONFIGURED" : "✓ Configured (hidden)")}");
    }

    private static async Task VerifyBuild()
    {
        Console.WriteLine("Verifying project builds...");
        var projectPath = ResolveProjectPath();
        if (projectPath == null) return;

        var (success, _) = await RunCommandAsync("dotnet", $"build \"{projectPath}\" -c {_config.Configuration}", Path.GetDirectoryName(projectPath)!);
        Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(success ? "✓ Build succeeded" : "✗ Build failed");
        Console.ResetColor();
    }

    private static async Task PackNuGet()
    {
        Console.WriteLine("Packing NuGet package...");
        var projectPath = ResolveProjectPath();
        if (projectPath == null) return;

        if (_dryRun) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("DRY RUN — would run: dotnet pack");
            Console.ResetColor();
            return;
        }

        var args = $"pack \"{projectPath}\" -c {_config.Configuration} /p:Version={_config.Version}";
        if (_config.IncludeSymbols) args += " --include-symbols -p:SymbolPackageFormat=snupkg";

        var (success, output) = await RunCommandAsync("dotnet", args, Path.GetDirectoryName(projectPath)!);
        Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(success ? "✓ Pack succeeded" : "✗ Pack failed");
        Console.ResetColor();

        if (success) {
            var nupkg = FindNupkg(projectPath);
            if (nupkg != null) Console.WriteLine($"  Package: {nupkg}");
        }
    }

    private static async Task PushToNuGet()
    {
        if (string.IsNullOrWhiteSpace(_config.ApiKey)) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ NuGet API key not configured. Run: dotnet user-secrets set \"NuGet:ApiKey\" \"your-key\"");
            Console.ResetColor();
            return;
        }

        var projectPath = ResolveProjectPath();
        if (projectPath == null) return;

        var nupkg = FindNupkg(projectPath);
        if (nupkg == null) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ No .nupkg found. Run Pack first.");
            Console.ResetColor();
            return;
        }

        if (_dryRun) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"DRY RUN — would push: {Path.GetFileName(nupkg)}");
            Console.ResetColor();
            return;
        }

        var skipDup = _config.SkipDuplicate ? " --skip-duplicate" : "";
        var args = $"nuget push \"{nupkg}\" --api-key {_config.ApiKey} --source {_config.Source}{skipDup}";

        var (success, _) = await RunCommandAsync("dotnet", args, Path.GetDirectoryName(nupkg)!, hideOutput: false);
        Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(success ? "✓ Push succeeded" : "✗ Push failed");
        Console.ResetColor();
    }

    private static async Task FullPublish()
    {
        Console.WriteLine("Full publish: Clean → Build → Pack → Push");
        Console.WriteLine();

        if (_dryRun) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("DRY RUN — no changes will be made. Toggle with D.");
            Console.ResetColor();
            return;
        }

        Console.Write("Are you sure? (y/N): ");
        var confirm = Console.ReadKey();
        Console.WriteLine();
        if (char.ToUpper(confirm.KeyChar) != 'Y') {
            Console.WriteLine("Cancelled.");
            return;
        }

        var projectPath = ResolveProjectPath();
        if (projectPath == null) return;
        var dir = Path.GetDirectoryName(projectPath)!;

        // Clean
        Console.WriteLine("\n[1/4] Cleaning...");
        var (cleanOk, _) = await RunCommandAsync("dotnet", $"clean \"{projectPath}\" -c {_config.Configuration}", dir);
        if (!cleanOk) { Console.WriteLine("Clean failed. Aborting."); return; }

        // Build
        Console.WriteLine("\n[2/4] Building...");
        var (buildOk, _) = await RunCommandAsync("dotnet", $"build \"{projectPath}\" -c {_config.Configuration}", dir);
        if (!buildOk) { Console.WriteLine("Build failed. Aborting."); return; }

        // Pack
        Console.WriteLine("\n[3/4] Packing...");
        await PackNuGet();

        // Push
        Console.WriteLine("\n[4/4] Pushing...");
        await PushToNuGet();
    }

    private static async Task LookupVersions()
    {
        Console.WriteLine($"Looking up {_config.PackageId} on NuGet.org...");
        try {
            var url = $"https://api.nuget.org/v3-flatcontainer/{_config.PackageId.ToLower()}/index.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) {
                Console.WriteLine(response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "  Package not found on NuGet.org (not published yet)."
                    : $"  Error: {response.StatusCode}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var versions = doc.RootElement.GetProperty("versions");

            Console.WriteLine($"  Found {versions.GetArrayLength()} version(s):");
            foreach (var v in versions.EnumerateArray())
                Console.WriteLine($"    {v.GetString()}");
        } catch (Exception ex) {
            Console.WriteLine($"  Error: {ex.Message}");
        }
    }

    private static void ChangeVersion()
    {
        Console.Write($"Current version: {_config.Version}. New version: ");
        var newVersion = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(newVersion)) {
            _config.Version = newVersion;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Version updated to {_config.Version}");
            Console.ResetColor();
        }
    }

    private static void ToggleDryRun()
    {
        _dryRun = !_dryRun;
        Console.ForegroundColor = _dryRun ? ConsoleColor.Yellow : ConsoleColor.Red;
        Console.WriteLine(_dryRun ? "DRY RUN mode ENABLED" : "LIVE mode ENABLED — operations will make real changes!");
        Console.ResetColor();
    }

    #endregion

    #region Helpers

    private static string? ResolveProjectPath()
    {
        // Try SolutionRoot + ProjectPath
        if (!string.IsNullOrWhiteSpace(_config.SolutionRoot)) {
            var root = Environment.ExpandEnvironmentVariables(_config.SolutionRoot);
            var full = Path.Combine(root, _config.ProjectPath);
            if (File.Exists(full)) return Path.GetFullPath(full);
        }

        // Try relative to current directory
        if (File.Exists(_config.ProjectPath))
            return Path.GetFullPath(_config.ProjectPath);

        // Walk up from current directory looking for the project
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 5; i++) {
            var candidate = Path.Combine(dir, _config.ProjectPath);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Cannot find project: {_config.ProjectPath}");
        Console.WriteLine("   Set \"SolutionRoot\" in appsettings.json or run from the solution directory.");
        Console.ResetColor();
        return null;
    }

    private static string? FindNupkg(string projectPath)
    {
        var dir = Path.GetDirectoryName(projectPath)!;
        var binDir = Path.Combine(dir, "bin", _config.Configuration);
        if (Directory.Exists(binDir)) {
            var files = Directory.GetFiles(binDir, $"*.{_config.Version}.nupkg", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".snupkg"))
                .ToArray();
            if (files.Length > 0) return files[0];
        }
        return null;
    }

    private static async Task<(bool Success, string Output)> RunCommandAsync(
        string command, string arguments, string workingDirectory, bool hideOutput = true)
    {
        try {
            var psi = new ProcessStartInfo {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    Failed to start process: {command}");
                Console.ResetColor();
                return (false, "Failed to start process");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;
            var combined = (output + Environment.NewLine + error).Trim();

            bool showOutput = !hideOutput || process.ExitCode != 0;

            if (showOutput && !string.IsNullOrWhiteSpace(output)) {
                foreach (var line in output.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l))) {
                    if (line.Contains("error", StringComparison.OrdinalIgnoreCase)) {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"    {line.TrimEnd()}");
                        Console.ResetColor();
                    } else {
                        Console.WriteLine($"    {line.TrimEnd()}");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(error)) {
                Console.ForegroundColor = ConsoleColor.Red;
                foreach (var line in error.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                    Console.WriteLine($"    {line.TrimEnd()}");
                Console.ResetColor();
            }

            return (process.ExitCode == 0, combined);
        } catch (Exception ex) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"    Error: {ex.Message}");
            Console.ResetColor();
            return (false, ex.Message);
        }
    }

    #endregion
}

/// <summary>
/// Configuration for NuGet publishing.
/// </summary>
public class NuGetConfig
{
    public string ApiKey { get; set; } = "";
    public string Source { get; set; } = "https://api.nuget.org/v3/index.json";
    public string PackageId { get; set; } = "FreeExamples.Client";
    public string Version { get; set; } = "1.0.0";
    public string SolutionRoot { get; set; } = "";
    public string ProjectPath { get; set; } = "FreeExamples.NuGetClient\\FreeExamples.NuGetClient.csproj";
    public string Configuration { get; set; } = "Release";
    public bool SkipDuplicate { get; set; } = true;
    public bool IncludeSymbols { get; set; } = true;
}
