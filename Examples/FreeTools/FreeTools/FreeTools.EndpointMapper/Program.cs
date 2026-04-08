using System.Text;
using System.Text.RegularExpressions;
using FreeTools.Core;

namespace FreeTools.EndpointMapper;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Robustness: Optional startup delay
        var delayEnv = Environment.GetEnvironmentVariable("START_DELAY_MS");
        if (int.TryParse(delayEnv, out var delayMs) && delayMs > 0)
        {
            Console.WriteLine($"Delaying start by {delayMs} ms...");
            await Task.Delay(delayMs);
        }

        var baseDir = AppContext.BaseDirectory;
        var cliArgs = args.ToList();

        var cleanFlag = CliArgs.HasFlag(cliArgs, "--clean");
        var shouldClean = cleanFlag || CliArgs.GetEnvBool("CLEAN_OUTPUT_DIRS");
        var outputDirToClean = Environment.GetEnvironmentVariable("OUTPUT_DIR") ?? "page-snapshots";

        string root;
        if (cliArgs.Count > 0)
        {
            root = Path.GetFullPath(cliArgs[0]);
        }
        else
        {
            root = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        }

        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine("Workspace root not found: " + root);
            return 1;
        }

        string outFile;
        if (cliArgs.Count > 1)
        {
            outFile = Path.GetFullPath(cliArgs[1]);
        }
        else
        {
            outFile = Path.Combine(root, "pages.csv");
        }

        ConsoleOutput.PrintBanner("EndpointMapper (FreeTools)", "2.0");
        ConsoleOutput.PrintConfig("Scanning root", root);
        ConsoleOutput.PrintConfig("Will write CSV to", outFile);
        ConsoleOutput.PrintConfig("Clean mode", shouldClean ? "ENABLED" : "DISABLED");
        if (shouldClean)
        {
            ConsoleOutput.PrintConfig("Will clean directory", outputDirToClean);
        }
        ConsoleOutput.PrintDivider();
        Console.WriteLine();

        if (shouldClean)
        {
            var outputDirFullPath = Path.IsPathRooted(outputDirToClean)
                ? outputDirToClean
                : Path.Combine(root, outputDirToClean);

            Console.WriteLine($"[CLEAN] Checking for output directory: {outputDirFullPath}");

            if (Directory.Exists(outputDirFullPath))
            {
                try
                {
                    Console.WriteLine($"[CLEAN] Deleting directory and all contents...");
                    Directory.Delete(outputDirFullPath, recursive: true);
                    Console.WriteLine($"[CLEAN] Successfully deleted: {outputDirFullPath}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[CLEAN] Error deleting directory: {ex.Message}");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine($"[CLEAN] Directory does not exist (nothing to clean): {outputDirFullPath}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("Scanning for .razor files...");
        var razorFiles = Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("/obj/")
                     && !f.Contains("\\bin\\") && !f.Contains("/bin/")
                     && !f.Contains("\\repo\\") && !f.Contains("/repo/"))  // Exclude /repo/ folder
            .ToList();
        Console.WriteLine($"Found {razorFiles.Count} .razor files.");
        Console.WriteLine();

        var csvLines = new List<string>
        {
            "FilePath,Route,RequiresAuth,Project"
        };

        var pageRegex = new Regex(@"@page\s+""(?<route>[^""]+)""", RegexOptions.Compiled);
        var authorizeRegex = new Regex(@"@attribute\s+\[Authorize", RegexOptions.Compiled);
        var routesFound = 0;
        var filesWithoutRoutes = 0;
        var authRequiredCount = 0;

        foreach (var file in razorFiles)
        {
            var text = await File.ReadAllTextAsync(file);
            var matches = pageRegex.Matches(text);
            
            var requiresAuth = authorizeRegex.IsMatch(text);
            if (requiresAuth) authRequiredCount++;

            var project = DetermineProject(file, root);
            
            // Use relative path instead of absolute path for privacy
            var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (matches.Count == 0)
            {
                var escapedFile = relativePath.Replace("\"", "\\\"");
                csvLines.Add($"\"{escapedFile}\",\"\",{requiresAuth.ToString().ToLower()},\"{project}\"");
                filesWithoutRoutes++;
            }
            else
            {
                foreach (Match m in matches)
                {
                    var route = m.Groups["route"].Value.Replace(',', '?');
                    var escapedFile = relativePath.Replace("\"", "\\\"");
                    csvLines.Add($"\"{escapedFile}\",\"{route}\",{requiresAuth.ToString().ToLower()},\"{project}\"");
                    routesFound++;
                }
            }
        }

        var outDir = Path.GetDirectoryName(outFile);
        if (!string.IsNullOrEmpty(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        await File.WriteAllLinesAsync(outFile, csvLines, Encoding.UTF8);

        ConsoleOutput.PrintDivider("Results");
        Console.WriteLine($"Total .razor files scanned: {razorFiles.Count}");
        Console.WriteLine($"Files with @page directives: {routesFound}");
        Console.WriteLine($"Files without @page directives: {filesWithoutRoutes}");
        Console.WriteLine($"Pages requiring authentication: {authRequiredCount}");
        Console.WriteLine($"Total CSV entries: {csvLines.Count - 1}");
        Console.WriteLine($"CSV written to: {outFile}");
        ConsoleOutput.PrintDivider();

        return 0;
    }

    private static string DetermineProject(string filePath, string root)
    {
        var relativePath = Path.GetRelativePath(root, filePath);
        
        if (relativePath.Contains("FreeTools.Web"))
            return "FreeTools.Web";
        if (relativePath.Contains("Account"))
            return "Identity";
            
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length > 0 ? parts[0] : "Unknown";
    }
}
