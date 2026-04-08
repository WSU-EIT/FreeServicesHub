// ═══════════════════════════════════════════════════════════════════════════════
// FreeTools.AppExtractor
// ═══════════════════════════════════════════════════════════════════════════════
//
// PURPOSE:
//   Scan the FreeExamples project root, find every *.App.* file
//   (the customization layer on top of FreeCRM), and copy them — maintaining
//   folder structure — to an output directory.
//
// WHAT THIS CAPTURES:
//   1. FreeExamples.App.* files (your app code)
//   2. *.App.* files (framework extension points you've customized)
//   3. The Docs folder (planning docs)
//   4. The MigrationTool folder (data migration scripts)
//   5. appsettings.json files (config with your custom sections)
//
// USAGE:
//   dotnet run -- --source "C:\...\FreeExamples" --output "C:\...\extracted"
//   dotnet run -- --source "C:\...\FreeExamples" --output "C:\...\extracted" --dry-run
//
//   Or configure appsettings.json:
//     { "AppExtractor": { "SourceRoot": "...", "OutputRoot": "...", "DryRun": false } }
//
// ═══════════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Configuration;
using System.Reflection;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddCommandLine(args, new Dictionary<string, string> {
        { "--source",  "AppExtractor:SourceRoot" },
        { "--output",  "AppExtractor:OutputRoot" },
        { "--dry-run", "AppExtractor:DryRun" },
    })
    .Build();

string sourceRoot = config["AppExtractor:SourceRoot"] ?? "";
string outputRoot = config["AppExtractor:OutputRoot"] ?? "";
bool dryRun = bool.TryParse(config["AppExtractor:DryRun"], out var d) && d;

// ─────────────────────────────────────────────────────────────────────────────
// VALIDATE INPUTS
// ─────────────────────────────────────────────────────────────────────────────

if (string.IsNullOrWhiteSpace(sourceRoot) || string.IsNullOrWhiteSpace(outputRoot)) {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: --source and --output are required.");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -- --source \"C:\\...\\FreeExamples\" --output \"C:\\...\\extracted\"");
    Console.WriteLine("  dotnet run -- --source \"...\" --output \"...\" --dry-run true");
    Console.WriteLine();
    Console.WriteLine("Or configure appsettings.json:");
    Console.WriteLine("  { \"AppExtractor\": { \"SourceRoot\": \"...\", \"OutputRoot\": \"...\" } }");
    return 1;
}

if (!Directory.Exists(sourceRoot)) {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR: Source directory does not exist: {sourceRoot}");
    Console.ResetColor();
    return 1;
}

// ─────────────────────────────────────────────────────────────────────────────
// CONFIGURATION
// ─────────────────────────────────────────────────────────────────────────────

var section = config.GetSection("AppExtractor");

string filePattern = section["FilePattern"] ?? ".App.";

var skipDirs = new HashSet<string>(
    section.GetSection("SkipDirectories").Get<string[]>() ?? [],
    StringComparer.OrdinalIgnoreCase);

var wholeDirs = new HashSet<string>(
    section.GetSection("WholeDirectories").Get<string[]>() ?? [],
    StringComparer.OrdinalIgnoreCase);

var explicitFiles = new HashSet<string>(
    section.GetSection("ExplicitFiles").Get<string[]>() ?? [],
    StringComparer.OrdinalIgnoreCase);

var rootFileExtensions = new HashSet<string>(
    section.GetSection("RootFileExtensions").Get<string[]>() ?? [],
    StringComparer.OrdinalIgnoreCase);

// ─────────────────────────────────────────────────────────────────────────────
// SCANNING
// ─────────────────────────────────────────────────────────────────────────────

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  FreeTools.AppExtractor                                    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine();
Console.WriteLine($"  Source:  {sourceRoot}");
Console.WriteLine($"  Output:  {outputRoot}");
Console.WriteLine($"  Dry Run: {dryRun}");
Console.WriteLine();

var filesToCopy = new List<(string SourcePath, string RelativePath)>();
var allFiles = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);

foreach (var file in allFiles) {
    var relativePath = Path.GetRelativePath(sourceRoot, file);
    var parts = relativePath.Split(Path.DirectorySeparatorChar);

    // Skip build artifacts and IDE folders.
    if (parts.Any(p => skipDirs.Contains(p))) continue;

    // Check if this file is inside a "copy wholesale" directory.
    bool inWholeDir = wholeDirs.Any(wd =>
        relativePath.StartsWith(wd + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        relativePath.Equals(wd, StringComparison.OrdinalIgnoreCase));

    if (inWholeDir) {
        filesToCopy.Add((file, relativePath));
        continue;
    }

    var fileName = Path.GetFileName(file);

    // Match the file pattern (default: *.App.*) — the core extraction rule.
    if (fileName.Contains(filePattern, StringComparison.OrdinalIgnoreCase)) {
        filesToCopy.Add((file, relativePath));
        continue;
    }

    // Explicit files (appsettings.json in project roots).
    if (explicitFiles.Contains(fileName)) {
        // Only include appsettings from project directories (1 level deep).
        if (parts.Length == 2) {
            filesToCopy.Add((file, relativePath));
            continue;
        }
    }

    // Solution/root files at root level.
    if (parts.Length == 1 && rootFileExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) {
        filesToCopy.Add((file, relativePath));
        continue;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// REPORT
// ─────────────────────────────────────────────────────────────────────────────

// Group by top-level directory for display.
var groups = filesToCopy
    .GroupBy(f => f.RelativePath.Contains(Path.DirectorySeparatorChar)
        ? f.RelativePath.Split(Path.DirectorySeparatorChar)[0]
        : "(root)")
    .OrderBy(g => g.Key);

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"  Found {filesToCopy.Count} files to extract:");
Console.ResetColor();
Console.WriteLine();

foreach (var group in groups) {
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write($"  📁 {group.Key}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($" ({group.Count()} files)");
    Console.ResetColor();

    foreach (var file in group.OrderBy(f => f.RelativePath)) {
        var fileName = Path.GetFileName(file.RelativePath);
        var isAppFile = fileName.Contains(".App.", StringComparison.OrdinalIgnoreCase);
        Console.ForegroundColor = isAppFile ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"     {file.RelativePath}");
    }
    Console.ResetColor();
    Console.WriteLine();
}

// ─────────────────────────────────────────────────────────────────────────────
// COPY
// ─────────────────────────────────────────────────────────────────────────────

if (dryRun) {
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  ⚠ DRY RUN \u2014 no files were copied.");
    Console.ResetColor();
    Console.WriteLine("  Remove --dry-run to actually copy files.");
    return 0;
}

int copied = 0;
int errors = 0;

foreach (var (sourcePath, relativePath) in filesToCopy) {
    var destPath = Path.Combine(outputRoot, relativePath);
    var destDir = Path.GetDirectoryName(destPath)!;

    try {
        if (!Directory.Exists(destDir)) {
            Directory.CreateDirectory(destDir);
        }
        File.Copy(sourcePath, destPath, overwrite: true);
        copied++;
    } catch (Exception ex) {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ERROR copying {relativePath}: {ex.Message}");
        Console.ResetColor();
        errors++;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SUMMARY
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.ResetColor();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"  ✅ Extracted {copied} files to: {outputRoot}");
Console.ResetColor();

if (errors > 0) {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ❌ {errors} errors occurred.");
    Console.ResetColor();
}

// Calculate size.
long totalBytes = 0;
foreach (var (sourcePath, _) in filesToCopy) {
    try { totalBytes += new FileInfo(sourcePath).Length; } catch { }
}
double totalKB = totalBytes / 1024.0;
double totalMB = totalKB / 1024.0;
Console.WriteLine($"  📦 Total size: {(totalMB >= 1 ? $"{totalMB:F1} MB" : $"{totalKB:F0} KB")}");

// Show what percentage of the full project this represents.
var allSourceFiles = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories)
    .Where(f => {
        var p = Path.GetRelativePath(sourceRoot, f).Split(Path.DirectorySeparatorChar);
        return !p.Any(x => skipDirs.Contains(x));
    })
    .Count();
double pct = allSourceFiles > 0 ? (filesToCopy.Count / (double)allSourceFiles) * 100 : 0;
Console.WriteLine($"  📊 {filesToCopy.Count} of {allSourceFiles} project files ({pct:F1}% of codebase is your customization)");

Console.WriteLine();
return errors > 0 ? 1 : 0;
