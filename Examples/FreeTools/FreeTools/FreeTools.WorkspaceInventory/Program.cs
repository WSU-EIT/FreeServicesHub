using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using FreeTools.Core;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace FreeTools.WorkspaceInventory;

internal partial class Program
{
    private const string DefaultIncludePatterns = "**/*.cs;**/*.razor;**/*.csproj;**/*.sln;**/*.json;**/*.config;**/*.md;**/*.xml;**/*.yaml;**/*.yml";
    private const string DefaultExcludeDirs = "bin;obj;.git;.vs;node_modules;packages;TestResults;repo";
    private const long DefaultMaxParseSizeBytes = 1024 * 1024;
    private const int DefaultMaxThreads = 10;

    [GeneratedRegex(@"@page\s+""([^""]+)""", RegexOptions.Compiled)]
    private static partial Regex PageDirectiveRegex();

    private static async Task<int> Main(string[] args)
    {
        // Robustness: Optional startup delay
        var delayEnv = Environment.GetEnvironmentVariable("START_DELAY_MS");
        if (int.TryParse(delayEnv, out var delayMs) && delayMs > 0)
        {
            Console.WriteLine($"Delaying start by {delayMs} ms...");
            await Task.Delay(delayMs);
        }

        var argsList = args.ToList();

        var noCounts = CliArgs.HasFlag(argsList, "--noCounts") || CliArgs.GetEnvBool("NO_COUNTS");
        var includeArg = CliArgs.GetOption(argsList, "--include=");
        var excludeDirsArg = CliArgs.GetOption(argsList, "--excludeDirs=");
        
        // Parallel processing configuration
        var maxThreadsEnv = Environment.GetEnvironmentVariable("MAX_THREADS");
        var maxThreads = int.TryParse(maxThreadsEnv, out var parsedThreads) ? Math.Max(1, parsedThreads) : DefaultMaxThreads;

        var maxParseSizeEnv = Environment.GetEnvironmentVariable("MAX_PARSE_SIZE");
        var maxParseSize = long.TryParse(maxParseSizeEnv, out var parsedSize) ? parsedSize : DefaultMaxParseSizeBytes;

        var azdoOrgUrl = Environment.GetEnvironmentVariable("AZDO_ORG_URL")?.TrimEnd('/');
        var azdoProject = Environment.GetEnvironmentVariable("AZDO_PROJECT");
        var azdoRepo = Environment.GetEnvironmentVariable("AZDO_REPO");
        var azdoBranch = Environment.GetEnvironmentVariable("AZDO_BRANCH") ?? "main";
        var azdoEnabled = !string.IsNullOrEmpty(azdoOrgUrl) && !string.IsNullOrEmpty(azdoProject) && !string.IsNullOrEmpty(azdoRepo);

        var root = Environment.GetEnvironmentVariable("ROOT_DIR")
            ?? CliArgs.GetPositional(argsList, 0)
            ?? FindRepoRoot(AppContext.BaseDirectory);

        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            Console.Error.WriteLine($"Root directory not found: {root}");
            return 1;
        }

        root = Path.GetFullPath(root);

        var csvPath = Environment.GetEnvironmentVariable("CSV_PATH")
            ?? CliArgs.GetPositional(argsList, 1)
            ?? Path.Combine(root, "workspace-inventory.csv");

        csvPath = Path.GetFullPath(csvPath);

        var includePatterns = Environment.GetEnvironmentVariable("INCLUDE")
            ?? includeArg
            ?? DefaultIncludePatterns;

        var includeList = includePatterns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var excludeDirs = Environment.GetEnvironmentVariable("EXCLUDE_DIRS")
            ?? excludeDirsArg
            ?? DefaultExcludeDirs;

        var excludeSet = new HashSet<string>(
            excludeDirs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

        ConsoleOutput.PrintBanner("WorkspaceInventory (FreeTools)", "2.0");
        ConsoleOutput.PrintConfig("Root directory", root);
        ConsoleOutput.PrintConfig("Output CSV", csvPath);
        ConsoleOutput.PrintConfig("Include patterns", includePatterns);
        ConsoleOutput.PrintConfig("Exclude dirs", excludeDirs);
        ConsoleOutput.PrintConfig("Max threads", maxThreads.ToString());
        ConsoleOutput.PrintConfig("Count lines/chars", (!noCounts).ToString());
        ConsoleOutput.PrintConfig("Max parse size", PathSanitizer.FormatBytes(maxParseSize));
        ConsoleOutput.PrintConfig("Azure DevOps", azdoEnabled ? "Enabled" : "Disabled");
        ConsoleOutput.PrintDivider();
        Console.WriteLine();

        var csvDir = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(csvDir) && !Directory.Exists(csvDir))
        {
            try
            {
                Directory.CreateDirectory(csvDir);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Cannot create output directory: {ex.Message}");
                return 1;
            }
        }

        Console.WriteLine("Scanning...");
        var matcher = new Matcher();
        foreach (var pattern in includeList)
        {
            matcher.AddInclude(pattern);
        }

        foreach (var excludeDir in excludeSet)
        {
            matcher.AddExclude($"**/{excludeDir}/**");
        }

        PatternMatchingResult matchResult;
        try
        {
            matchResult = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error scanning directory: {ex.Message}");
            return 1;
        }

        var matchedFiles = matchResult.Files
            .Where(f => !ContainsExcludedDir(f.Path, excludeSet))
            .ToList();

        Console.WriteLine($"  Found {matchedFiles.Count} files matching patterns with {maxThreads} parallel workers.");

        if (matchedFiles.Count == 0)
        {
            Console.WriteLine("  No files to process.");
            await WriteEmptyCsvAsync(csvPath);
            Console.WriteLine($"  CSV written to: {csvPath}");
            return 0;
        }

        Console.WriteLine("  Processing files...");
        
        var totalCount = matchedFiles.Count;
        var processedCount = 0;
        var unreadableCount = 0;
        var skippedLargeCount = 0;
        long totalSize = 0;
        long totalLines = 0;
        long totalChars = 0;

        var kindCounts = new ConcurrentDictionary<string, int>();
        var results = new ConcurrentDictionary<int, FileInventoryItem>();
        var nextIndexToWrite = 0;
        var writeLock = new object();

        var semaphore = new SemaphoreSlim(maxThreads);

        var tasks = matchedFiles.Select((file, index) => Task.Run(async () =>
        {
            await semaphore.WaitAsync();
            try
            {
                var relativePath = file.Path;
                var absolutePath = Path.Combine(root, relativePath);

                var item = new FileInventoryItem
                {
                    FilePath = relativePath.Replace('\\', '/'),  // Use relative path, not absolute
                    RelativePath = relativePath.Replace('\\', '/'),
                    Extension = Path.GetExtension(absolutePath).ToLowerInvariant()
                };

                try
                {
                    var fileInfo = new FileInfo(absolutePath);
                    item.SizeBytes = fileInfo.Length;
                    item.CreatedUtc = fileInfo.CreationTimeUtc;
                    item.ModifiedUtc = fileInfo.LastWriteTimeUtc;
                    Interlocked.Add(ref totalSize, item.SizeBytes);

                    if (item.SizeBytes > maxParseSize)
                    {
                        item.ReadError = "FileTooLarge";
                        item.Kind = ClassifyByExtension(item.Extension);
                        Interlocked.Increment(ref skippedLargeCount);
                    }
                    else
                    {
                        string? content = null;
                        try
                        {
                            content = await File.ReadAllTextAsync(absolutePath);
                        }
                        catch (Exception ex)
                        {
                            item.ReadError = ex.GetType().Name;
                            Interlocked.Increment(ref unreadableCount);
                        }

                        if (content is not null)
                        {
                            if (!noCounts)
                            {
                                var (lines, chars) = CountLinesAndChars(content);
                                item.LineCount = lines;
                                item.CharCount = chars;
                                Interlocked.Add(ref totalLines, lines);
                                Interlocked.Add(ref totalChars, chars);
                            }

                            ClassifyAndExtractMetadata(item, content);
                        }
                        else
                        {
                            item.Kind = ClassifyByExtension(item.Extension);
                        }
                    }

                    if (azdoEnabled)
                    {
                        item.AzureDevOpsUrl = BuildAzureDevOpsUrl(azdoOrgUrl!, azdoProject!, azdoRepo!, azdoBranch, item.RelativePath);
                    }

                    if (!string.IsNullOrEmpty(item.Kind))
                    {
                        kindCounts.AddOrUpdate(item.Kind, 1, (_, count) => count + 1);
                    }
                }
                catch (Exception ex)
                {
                    item.ReadError = ex.GetType().Name;
                    item.Kind = ClassifyByExtension(item.Extension);
                    Interlocked.Increment(ref unreadableCount);
                }

                // Store result and try to write in order
                results[index] = item;
                Interlocked.Increment(ref processedCount);

                // Write results in order
                lock (writeLock)
                {
                    while (results.TryGetValue(nextIndexToWrite, out var resultItem))
                    {
                        WriteProgress(nextIndexToWrite + 1, totalCount, resultItem.RelativePath, 
                            resultItem.SizeBytes, resultItem.LineCount, resultItem.Kind, resultItem.ReadError);
                        nextIndexToWrite++;
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Write any remaining results
        lock (writeLock)
        {
            while (results.TryGetValue(nextIndexToWrite, out var resultItem))
            {
                WriteProgress(nextIndexToWrite + 1, totalCount, resultItem.RelativePath,
                    resultItem.SizeBytes, resultItem.LineCount, resultItem.Kind, resultItem.ReadError);
                nextIndexToWrite++;
            }
        }

        // Collect all results in order
        var inventory = Enumerable.Range(0, matchedFiles.Count)
            .Select(i => results.TryGetValue(i, out var item) ? item : null!)
            .Where(item => item != null)
            .ToList();

        Console.WriteLine();

        try
        {
            await WriteCsvAsync(csvPath, inventory);
            
            var csFiles = inventory
                .Where(i => i.Extension == ".cs")
                .OrderByDescending(i => i.LineCount ?? 0)
                .ToList();
            
            var razorFiles = inventory
                .Where(i => i.Extension == ".razor")
                .OrderByDescending(i => i.LineCount ?? 0)
                .ToList();
            
            var csPath = Path.Combine(Path.GetDirectoryName(csvPath)!, 
                Path.GetFileNameWithoutExtension(csvPath) + "-csharp.csv");
            var razorPath = Path.Combine(Path.GetDirectoryName(csvPath)!, 
                Path.GetFileNameWithoutExtension(csvPath) + "-razor.csv");
            
            await WriteCsvAsync(csPath, csFiles);
            await WriteCsvAsync(razorPath, razorFiles);
            
            Console.WriteLine($"  C# files CSV ({csFiles.Count} files, sorted by lines): {csPath}");
            Console.WriteLine($"  Razor files CSV ({razorFiles.Count} files, sorted by lines): {razorPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error writing CSV: {ex.Message}");
            return 1;
        }

        ConsoleOutput.PrintDivider("SUMMARY");
        Console.WriteLine($"  Files matched:     {matchedFiles.Count}");
        Console.WriteLine($"  Files scanned:     {processedCount}");
        Console.WriteLine($"  Files unreadable:  {unreadableCount}");
        Console.WriteLine($"  Files too large:   {skippedLargeCount}");
        Console.WriteLine($"  Total size:        {PathSanitizer.FormatBytes(totalSize)}");
        if (!noCounts)
        {
            Console.WriteLine($"  Total lines:       {totalLines:N0}");
            Console.WriteLine($"  Total chars:       {totalChars:N0}");
        }
        Console.WriteLine();
        Console.WriteLine("  By Kind:");
        foreach (var kvp in kindCounts.OrderByDescending(k => k.Value))
        {
            Console.WriteLine($"    {kvp.Key,-20} {kvp.Value,5}");
        }
        Console.WriteLine();
        Console.WriteLine($"  CSV written to: {csvPath}");
        ConsoleOutput.PrintDivider();

        return 0;
    }

    private static void ClassifyAndExtractMetadata(FileInventoryItem item, string content)
    {
        switch (item.Extension)
        {
            case ".razor":
                ExtractRazorMetadata(item, content);
                break;
            case ".cs":
                item.Kind = "CSharpSource";
                ExtractCSharpMetadata(item, content);
                break;
            case ".csproj":
                item.Kind = "ProjectFile";
                break;
            case ".sln":
                item.Kind = "SolutionFile";
                break;
            case ".json" or ".config" or ".xml" or ".yaml" or ".yml":
                item.Kind = "Config";
                break;
            case ".md":
                item.Kind = "Markdown";
                break;
            default:
                item.Kind = "Other";
                break;
        }
    }

    private static string ClassifyByExtension(string extension) => extension switch
    {
        ".razor" => "RazorComponent",
        ".cs" => "CSharpSource",
        ".csproj" => "ProjectFile",
        ".sln" => "SolutionFile",
        ".json" or ".config" or ".xml" or ".yaml" or ".yml" => "Config",
        ".md" => "Markdown",
        _ => "Other"
    };

    private static void ExtractRazorMetadata(FileInventoryItem item, string content)
    {
        var pageMatches = PageDirectiveRegex().Matches(content);
        if (pageMatches.Count > 0)
        {
            item.Kind = "RazorPage";
            item.Routes = string.Join(";", pageMatches.Select(m => m.Groups[1].Value));
        }
        else
        {
            item.Kind = "RazorComponent";
        }

        item.RequiresAuth = content.Contains("@attribute [Authorize", StringComparison.Ordinal);
    }

    private static void ExtractCSharpMetadata(FileInventoryItem item, string content)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetRoot();

            var namespaces = root.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Select(n => n.Name.ToString())
                .Distinct()
                .ToList();

            if (namespaces.Count > 0)
            {
                item.Namespaces = string.Join(";", namespaces);
            }

            var types = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Select(t => t.Identifier.Text)
                .Concat(root.DescendantNodes()
                    .OfType<EnumDeclarationSyntax>()
                    .Select(e => e.Identifier.Text))
                .Distinct()
                .ToList();

            if (types.Count > 0)
            {
                item.DeclaredTypes = string.Join(";", types);
            }
        }
        catch
        {
        }
    }

    private static string BuildAzureDevOpsUrl(string orgUrl, string project, string repo, string branch, string relativePath)
    {
        var encodedPath = Uri.EscapeDataString("/" + relativePath);
        return $"{orgUrl}/{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(repo)}?path={encodedPath}&version=GB{Uri.EscapeDataString(branch)}";
    }

    private static (long lines, long chars) CountLinesAndChars(string content)
    {
        if (string.IsNullOrEmpty(content))
            return (0, 0);

        long lineCount = 0;
        foreach (var c in content)
        {
            if (c == '\n')
                lineCount++;
        }

        if (content.Length > 0 && content[^1] != '\n')
            lineCount++;

        return (lineCount, content.Length);
    }

    private static async Task WriteCsvAsync(string csvPath, List<FileInventoryItem> inventory)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FilePath,RelativePath,Extension,SizeBytes,LineCount,CharCount,CreatedUtc,ModifiedUtc,ReadError,Kind,Namespaces,DeclaredTypes,Routes,RequiresAuth,AzureDevOpsUrl");

        foreach (var item in inventory)
        {
            sb.Append(CsvEscape(item.FilePath));
            sb.Append(',');
            sb.Append(CsvEscape(item.RelativePath));
            sb.Append(',');
            sb.Append(CsvEscape(item.Extension));
            sb.Append(',');
            sb.Append(item.SizeBytes);
            sb.Append(',');
            sb.Append(item.LineCount?.ToString() ?? "");
            sb.Append(',');
            sb.Append(item.CharCount?.ToString() ?? "");
            sb.Append(',');
            sb.Append(item.CreatedUtc?.ToString("o") ?? "");
            sb.Append(',');
            sb.Append(item.ModifiedUtc?.ToString("o") ?? "");
            sb.Append(',');
            sb.Append(CsvEscape(item.ReadError ?? ""));
            sb.Append(',');
            sb.Append(CsvEscape(item.Kind ?? ""));
            sb.Append(',');
            sb.Append(CsvEscape(item.Namespaces ?? ""));
            sb.Append(',');
            sb.Append(CsvEscape(item.DeclaredTypes ?? ""));
            sb.Append(',');
            sb.Append(CsvEscape(item.Routes ?? ""));
            sb.Append(',');
            sb.Append(item.RequiresAuth?.ToString().ToLowerInvariant() ?? "");
            sb.Append(',');
            sb.Append(CsvEscape(item.AzureDevOpsUrl ?? ""));
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(csvPath, sb.ToString(), Encoding.UTF8);
    }

    private static async Task WriteEmptyCsvAsync(string csvPath)
    {
        await File.WriteAllTextAsync(csvPath, "FilePath,RelativePath,Extension,SizeBytes,LineCount,CharCount,CreatedUtc,ModifiedUtc,ReadError,Kind,Namespaces,DeclaredTypes,Routes,RequiresAuth,AzureDevOpsUrl\n", Encoding.UTF8);
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static bool ContainsExcludedDir(string relativePath, HashSet<string> excludeSet)
    {
        var segments = relativePath.Split('/', '\\');
        return segments.Any(s => excludeSet.Contains(s));
    }

    private static string FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                dir.GetFiles("*.sln").Length > 0)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(startPath, "..", "..", "..", "..", ".."));
    }

    private static void WriteProgress(int current, int total, string relativePath, long sizeBytes, long? lineCount, string? kind, string? error = null)
    {
        if (error is not null)
        {
            ConsoleOutput.WriteLine($"  [{current}/{total}] {relativePath} - ERROR: {error}", isError: true);
        }
        else if (lineCount.HasValue)
        {
            ConsoleOutput.WriteLine($"  [{current}/{total}] {relativePath} ({sizeBytes:N0} bytes, {lineCount:N0} lines) [{kind}]");
        }
        else
        {
            ConsoleOutput.WriteLine($"  [{current}/{total}] {relativePath} ({sizeBytes:N0} bytes) [{kind}]");
        }
    }

    private sealed class FileInventoryItem
    {
        public string FilePath { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string Extension { get; set; } = "";
        public long SizeBytes { get; set; }
        public long? LineCount { get; set; }
        public long? CharCount { get; set; }
        public DateTime? CreatedUtc { get; set; }
        public DateTime? ModifiedUtc { get; set; }
        public string? ReadError { get; set; }
        public string? Kind { get; set; }
        public string? Namespaces { get; set; }
        public string? DeclaredTypes { get; set; }
        public string? Routes { get; set; }
        public bool? RequiresAuth { get; set; }
        public string? AzureDevOpsUrl { get; set; }
    }
}
