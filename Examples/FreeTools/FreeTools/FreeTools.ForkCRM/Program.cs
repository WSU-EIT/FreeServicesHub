using System.Diagnostics;
using System.Text;
using LibGit2Sharp;

namespace FreeTools.ForkCRM;

/// <summary>
/// Fork FreeCRM - Clone, remove modules, rename, and output a new project from FreeCRM.
/// Uses LibGit2Sharp for efficient cloning, then runs the official exe tools for transformations.
/// </summary>
class Program
{
    private const string RepoUrl = "https://github.com/WSU-EIT/FreeCRM.git";
    private const string RemoveExeName = "Remove Modules from FreeCRM.exe";
    private const string RenameExeName = "Rename FreeCRM.exe";

    private static readonly string[] ValidModules = ["Tags", "Appointments", "Invoices", "EmailTemplates", "Locations", "Payments", "Services", "all"];

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  FreeTools.ForkCRM - Fork and Rename FreeCRM Projects");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        // Parse arguments
        var options = ParseArguments(args);
        if (options == null)
        {
            ShowUsage();
            return 1;
        }

        // Validate
        if (!ValidateOptions(options))
            return 1;

        // Find the exe tools
        var (removeExe, renameExe) = FindExeTools();
        if (removeExe == null || renameExe == null)
        {
            WriteError("Could not find the required exe tools.");
            Console.WriteLine($"  Looking for: {RemoveExeName}");
            Console.WriteLine($"  Looking for: {RenameExeName}");
            Console.WriteLine();
            Console.WriteLine("  Make sure these files are in one of:");
            Console.WriteLine("    - Same directory as this program");
            Console.WriteLine("    - tools/FreeCRM-utilities/ folder");
            return 1;
        }

        // Show configuration
        Console.WriteLine($"  New Name:          {options.NewName}");
        Console.WriteLine($"  Module Selection:  {options.ModuleSelection}");
        Console.WriteLine($"  Output Directory:  {options.OutputDirectory}");
        Console.WriteLine($"  Branch:            {options.Branch}");
        Console.WriteLine($"  Remove Tool:       {removeExe}");
        Console.WriteLine($"  Rename Tool:       {renameExe}");
        Console.WriteLine();

        try
        {
            await RunForkAsync(options, removeExe, renameExe);
            return 0;
        }
        catch (Exception ex)
        {
            WriteError($"Fork failed: {ex.Message}");
            if (ex.InnerException != null)
                WriteError($"  Inner: {ex.InnerException.Message}");
            return 1;
        }
    }

    private static async Task RunForkAsync(ForkOptions options, string removeExe, string renameExe)
    {
        // Create a temp working directory
        var workDir = Path.Combine(Path.GetTempPath(), $"forkcrm-{Guid.NewGuid():N}");

        try
        {
            // Step 1: Clone repository using LibGit2Sharp
            WriteStep("Cloning FreeCRM repository...");
            Directory.CreateDirectory(workDir);

            var cloneOptions = new CloneOptions
            {
                BranchName = options.Branch,
                RecurseSubmodules = false
            };

            Repository.Clone(RepoUrl, workDir, cloneOptions);
            WriteSuccess("Repository cloned");

            // Step 2: Copy exe tools to work directory
            WriteStep("Preparing tools...");
            var removeExeDst = Path.Combine(workDir, RemoveExeName);
            var renameExeDst = Path.Combine(workDir, RenameExeName);
            File.Copy(removeExe, removeExeDst, overwrite: true);
            File.Copy(renameExe, renameExeDst, overwrite: true);
            WriteSuccess("Tools ready");

            // Step 3: Run module removal
            WriteStep($"Running module removal: {options.ModuleSelection}");
            var removeResult = await RunExeAsync(removeExeDst, $"\"{options.ModuleSelection}\"", workDir);
            if (removeResult != 0)
            {
                WriteWarning($"Module removal returned exit code {removeResult} (may be expected)");
            }
            else
            {
                WriteSuccess("Module removal complete");
            }

            // Step 4: Run rename
            WriteStep($"Renaming project to: {options.NewName}");
            var renameResult = await RunExeAsync(renameExeDst, $"\"{options.NewName}\"", workDir);
            if (renameResult != 0)
            {
                WriteWarning($"Rename returned exit code {renameResult} (may be expected)");
            }
            else
            {
                WriteSuccess("Rename complete");
            }

            // Step 5: Clean up - remove exe tools and unwanted folders
            WriteStep("Cleaning up work directory...");
            File.Delete(removeExeDst);
            File.Delete(renameExeDst);

            var gitDir = Path.Combine(workDir, ".git");
            var githubDir = Path.Combine(workDir, ".github");
            var artifactsDir = Path.Combine(workDir, "artifacts");

            if (Directory.Exists(gitDir))
            {
                SetAttributesNormal(gitDir);
                Directory.Delete(gitDir, recursive: true);
            }
            if (Directory.Exists(githubDir))
                Directory.Delete(githubDir, recursive: true);
            if (Directory.Exists(artifactsDir))
                Directory.Delete(artifactsDir, recursive: true);

            WriteSuccess("Cleanup complete");

            // Step 6: Read all files into memory
            WriteStep("Reading transformed files into memory...");
            var files = new Dictionary<string, byte[]>();
            foreach (var file in Directory.GetFiles(workDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(workDir, file);
                files[relativePath] = File.ReadAllBytes(file);
            }
            WriteSuccess($"Read {files.Count} files into memory");

            // Step 7: Write to output directory
            WriteStep($"Writing {files.Count} files to output directory...");
            var outputDir = Path.GetFullPath(options.OutputDirectory);
            WriteFilesToDisk(files, outputDir);
            WriteSuccess($"Files written to {outputDir}");

            // Done!
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            WriteSuccess("Fork Complete!");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"  Project Name: {options.NewName}");
            Console.WriteLine($"  Location:     {outputDir}");
            Console.WriteLine($"  Files:        {files.Count}");
            Console.WriteLine();
            Console.WriteLine("  Next Steps:");
            Console.WriteLine($"    1. cd \"{outputDir}\"");
            Console.WriteLine("    2. dotnet restore");
            Console.WriteLine("    3. dotnet build");
            Console.WriteLine();
        }
        finally
        {
            // Cleanup temp directory
            if (Directory.Exists(workDir))
            {
                WriteStep("Cleaning up temp files...");
                try
                {
                    SetAttributesNormal(workDir);
                    Directory.Delete(workDir, recursive: true);
                    WriteSuccess("Temp files cleaned up");
                }
                catch (Exception ex)
                {
                    WriteWarning($"Could not fully clean up: {ex.Message}");
                }
            }
        }
    }

    private static async Task<int> RunExeAsync(string exePath, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
                Console.WriteLine($"    {e.Data}");
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);
                Console.WriteLine($"    [ERR] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static (string? removeExe, string? renameExe) FindExeTools()
    {
        var searchPaths = new List<string>
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
        };

        // Add parent directories to search for FreeCRM-utilities
        var current = AppContext.BaseDirectory;
        for (int i = 0; i < 5; i++)
        {
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;

            searchPaths.Add(Path.Combine(current, "FreeCRM-utilities"));
            searchPaths.Add(current);
        }

        string? removeExe = null;
        string? renameExe = null;

        foreach (var dir in searchPaths)
        {
            if (!Directory.Exists(dir)) continue;

            var removePath = Path.Combine(dir, RemoveExeName);
            var renamePath = Path.Combine(dir, RenameExeName);

            if (removeExe == null && File.Exists(removePath))
                removeExe = removePath;

            if (renameExe == null && File.Exists(renamePath))
                renameExe = renamePath;

            if (removeExe != null && renameExe != null)
                break;
        }

        return (removeExe, renameExe);
    }

    private static void SetAttributesNormal(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* Best effort */ }
        }
    }

    private static void WriteFilesToDisk(Dictionary<string, byte[]> files, string outputDir)
    {
        // Clear output directory (preserve .git if exists)
        if (Directory.Exists(outputDir))
        {
            foreach (var entry in Directory.GetFileSystemEntries(outputDir))
            {
                var name = Path.GetFileName(entry);
                if (name == ".git") continue;

                if (Directory.Exists(entry))
                    Directory.Delete(entry, recursive: true);
                else
                    File.Delete(entry);
            }
        }
        else
        {
            Directory.CreateDirectory(outputDir);
        }

        // Write all files
        foreach (var kvp in files)
        {
            var fullPath = Path.Combine(outputDir, kvp.Key);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(fullPath, kvp.Value);
        }
    }

    private static ForkOptions? ParseArguments(string[] args)
    {
        var options = new ForkOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "-h" or "--help" or "-?")
                return null;

            if (arg is "-n" or "--name" && i + 1 < args.Length)
                options.NewName = args[++i];
            else if (arg is "-m" or "--modules" && i + 1 < args.Length)
                options.ModuleSelection = args[++i];
            else if (arg is "-o" or "--output" && i + 1 < args.Length)
                options.OutputDirectory = args[++i];
            else if (arg is "-b" or "--branch" && i + 1 < args.Length)
                options.Branch = args[++i];
            else if (!arg.StartsWith('-'))
            {
                // Positional arguments
                if (string.IsNullOrEmpty(options.NewName))
                    options.NewName = arg;
                else if (string.IsNullOrEmpty(options.ModuleSelection))
                    options.ModuleSelection = arg;
                else if (string.IsNullOrEmpty(options.OutputDirectory))
                    options.OutputDirectory = arg;
            }
        }

        if (string.IsNullOrEmpty(options.NewName) ||
            string.IsNullOrEmpty(options.ModuleSelection) ||
            string.IsNullOrEmpty(options.OutputDirectory))
            return null;

        return options;
    }

    private static bool ValidateOptions(ForkOptions options)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(options.NewName, @"^[A-Za-z][A-Za-z0-9]*$"))
        {
            WriteError($"Invalid project name: '{options.NewName}'");
            Console.WriteLine("  Name must start with a letter and contain only letters and numbers.");
            return false;
        }

        var selMatch = System.Text.RegularExpressions.Regex.Match(
            options.ModuleSelection,
            @"^(keep|remove):(.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!selMatch.Success)
        {
            WriteError($"Invalid module selection: '{options.ModuleSelection}'");
            return false;
        }

        var module = selMatch.Groups[2].Value;
        if (!ValidModules.Contains(module, StringComparer.OrdinalIgnoreCase))
        {
            WriteError($"Invalid module: '{module}'");
            Console.WriteLine($"  Valid modules: {string.Join(", ", ValidModules)}");
            return false;
        }

        return true;
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: FreeTools.ForkCRM <NewName> <ModuleSelection> <OutputDirectory> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  NewName           New project name (letters/numbers, starts with letter)");
        Console.WriteLine("  ModuleSelection   What modules to keep/remove (see below)");
        Console.WriteLine("  OutputDirectory   Where to place the forked project");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -n, --name <name>      New project name");
        Console.WriteLine("  -m, --modules <sel>    Module selection");
        Console.WriteLine("  -o, --output <dir>     Output directory");
        Console.WriteLine("  -b, --branch <branch>  Git branch to clone (default: main)");
        Console.WriteLine("  -h, --help             Show this help");
        Console.WriteLine();
        Console.WriteLine("Module Selections:");
        Console.WriteLine("  remove:all          Remove ALL optional modules (minimal project)");
        Console.WriteLine("  keep:Tags           Keep only the Tags module");
        Console.WriteLine("  keep:Appointments   Keep only the Appointments module");
        Console.WriteLine("  keep:Invoices       Keep only the Invoices module");
        Console.WriteLine("  keep:EmailTemplates Keep only the EmailTemplates module");
        Console.WriteLine("  keep:Locations      Keep only the Locations module");
        Console.WriteLine("  keep:Payments       Keep only the Payments module");
        Console.WriteLine("  keep:Services       Keep only the Services module");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  FreeTools.ForkCRM FreeManager \"keep:Tags\" ./output");
        Console.WriteLine("  FreeTools.ForkCRM MyApp \"remove:all\" C:\\Projects\\MyApp");
        Console.WriteLine();
        Console.WriteLine("Note: Requires Windows to run the exe tools, or Wine on Linux/Mac.");
    }

    private static void WriteStep(string msg) => Console.WriteLine($"[STEP] {msg}");
    private static void WriteSuccess(string msg) => Console.WriteLine($"[OK] {msg}");
    private static void WriteWarning(string msg) => Console.WriteLine($"[WARN] {msg}");
    private static void WriteError(string msg) => Console.WriteLine($"[ERROR] {msg}");
}

class ForkOptions
{
    public string NewName { get; set; } = "";
    public string ModuleSelection { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public string Branch { get; set; } = "main";
}
