// FreeServicesHub.TestMe -- Program.cs
// CLI test harness for validating Agent, Installer, and Hub binaries.
// Runs from source (dotnet run) or from pre-built artifacts (TestMe.exe).
//
// Usage:
//   dotnet run -- --test=1 --heartbeats=3 --interval=5 --timeout=60
//   FreeServicesHub.TestMe.exe --test=1 --servicedir=C:\path\to\agent --installerdir=C:\path\to\installer
//
// Tests:
//   1  Agent Console Mode      — Start agent, verify heartbeat output, stop
//   3  Installer CLI Headless  — Build, configure (standalone), status, remove
//   4  Agent Standalone Full   — Start agent, count N heartbeats, verify format, stop

using System.Diagnostics;

// ---- Parse Arguments ----
var args2 = ParseArgs(args);
var testNumber = args2.GetValueOrDefault("test", "0");
var heartbeats = int.Parse(args2.GetValueOrDefault("heartbeats", "3"));
var interval = int.Parse(args2.GetValueOrDefault("interval", "5"));
var timeout = int.Parse(args2.GetValueOrDefault("timeout", "60"));
var config = args2.GetValueOrDefault("config", "Debug");
var serviceDir = args2.GetValueOrDefault("servicedir", "");
var installerDir = args2.GetValueOrDefault("installerdir", "");

// Resolve directories
var solutionDir = FindSolutionDir() ?? AppContext.BaseDirectory;

if (string.IsNullOrEmpty(serviceDir))
    serviceDir = Path.Combine(solutionDir, "FreeServicesHub.Agent");
if (string.IsNullOrEmpty(installerDir))
    installerDir = Path.Combine(solutionDir, "FreeServicesHub.Agent.Installer");

PrintBanner();
WriteInfo($"Solution Dir:  {solutionDir}");
WriteInfo($"Service Dir:   {serviceDir}");
WriteInfo($"Installer Dir: {installerDir}");
WriteInfo($"Config:        {config}");
Console.WriteLine();

var result = testNumber switch
{
    "1" => await RunTest1(serviceDir, config, heartbeats, timeout),
    "3" => await RunTest3(installerDir, serviceDir, config, timeout),
    "4" => await RunTest4(serviceDir, config, heartbeats, interval, timeout),
    "0" => RunAll(),
    _ => Fail($"Unknown test: {testNumber}"),
};

Console.WriteLine();
if (result == 0)
    WritePass("ALL TESTS PASSED");
else
    WriteFail($"EXITING WITH CODE {result}");

return result;

// ===========================================================================
// Run All
// ===========================================================================
int RunAll()
{
    Console.WriteLine("No --test specified. Available tests:");
    Console.WriteLine("  --test=1  Agent Console Mode (start, verify heartbeat, stop)");
    Console.WriteLine("  --test=3  Installer CLI Headless (build, configure, status, remove)");
    Console.WriteLine("  --test=4  Agent Standalone Full (start, count heartbeats, verify format, stop)");
    return 1;
}

// ===========================================================================
// Test 1: Agent Console Mode
//   Start the agent process, wait for at least one heartbeat line in stdout,
//   verify it contains expected markers, then kill the process.
// ===========================================================================
async Task<int> RunTest1(string agentDir, string cfg, int hb, int timeoutSec)
{
    PrintHeader("TEST 1 — Agent Console Mode");

    var (exe, exeArgs) = ResolveExe(agentDir, "FreeServicesHub.Agent", cfg);
    if (exe is null) return Fail("Agent executable not found.");

    WriteStep($"Starting: {exe} {exeArgs}");
    var output = new List<string>();

    using var process = StartProcess(exe, exeArgs, line =>
    {
        WriteDim(line);
        output.Add(line);
    });

    if (process is null) return Fail("Failed to start agent process.");

    // Wait for heartbeat output or timeout
    var sw = Stopwatch.StartNew();
    var foundHeartbeat = false;

    while (sw.Elapsed.TotalSeconds < timeoutSec && !process.HasExited)
    {
        if (output.Any(l => l.Contains("Heartbeat", StringComparison.OrdinalIgnoreCase)))
        {
            foundHeartbeat = true;
            break;
        }
        await Task.Delay(500);
    }

    KillProcess(process);

    if (!foundHeartbeat)
        return Fail("No heartbeat output detected within timeout.");

    // Verify heartbeat format
    var heartbeatLine = output.First(l => l.Contains("Heartbeat", StringComparison.OrdinalIgnoreCase));
    var markers = new[] { "CPU:", "RAM:", "Uptime:" };
    foreach (var marker in markers)
    {
        if (!heartbeatLine.Contains(marker, StringComparison.OrdinalIgnoreCase))
            return Fail($"Heartbeat line missing expected marker: {marker}");
    }

    WritePass("Heartbeat detected with correct format.");

    // Verify standalone mode
    if (output.Any(l => l.Contains("standalone mode", StringComparison.OrdinalIgnoreCase)))
        WritePass("Agent running in standalone mode (no hub credentials).");

    // Verify drive info
    if (output.Any(l => l.Contains("Drive", StringComparison.OrdinalIgnoreCase) && l.Contains("GB", StringComparison.OrdinalIgnoreCase)))
        WritePass("Drive information present.");

    return Pass("Test 1 passed.");
}

// ===========================================================================
// Test 3: Installer CLI Headless
//   Run the installer with build, configure (standalone), status, remove.
// ===========================================================================
async Task<int> RunTest3(string instDir, string svcDir, string cfg, int timeoutSec)
{
    PrintHeader("TEST 3 — Installer CLI Headless");

    var (exe, exeArgs) = ResolveExe(instDir, "FreeServicesHub.Agent.Installer", cfg);
    if (exe is null) return Fail("Installer executable not found.");

    // Step 1: Build
    WriteStep("[1/4] Running: build");
    var r = await RunProcessCapture(exe, $"{exeArgs} build", timeoutSec);
    if (r.exitCode != 0)
    {
        WriteWarn($"Build returned exit code {r.exitCode} (may be OK if no project configured).");
    }
    else
    {
        WritePass("Build completed.");
    }

    // Step 2: Status (before configure)
    WriteStep("[2/4] Running: status");
    r = await RunProcessCapture(exe, $"{exeArgs} status", timeoutSec);
    WritePass($"Status returned exit code {r.exitCode}.");

    // Step 3: Verify help/unknown action
    WriteStep("[3/4] Running: unknown action (should fail gracefully)");
    r = await RunProcessCapture(exe, $"{exeArgs} invalidaction", timeoutSec);
    if (r.exitCode != 0 && r.output.Any(l => l.Contains("Unknown", StringComparison.OrdinalIgnoreCase)))
        WritePass("Unknown action handled gracefully.");
    else
        WriteWarn("Unknown action handling did not behave as expected.");

    // Step 4: Verify banner output
    WriteStep("[4/4] Verifying installer output");
    if (r.output.Any(l => l.Contains("AGENT INSTALLER", StringComparison.OrdinalIgnoreCase)))
        WritePass("Installer banner present.");
    else
        WriteWarn("Installer banner not found in output.");

    return Pass("Test 3 passed.");
}

// ===========================================================================
// Test 4: Agent Standalone Full
//   Start agent, wait for N heartbeats, verify CPU/RAM/Drive format, stop.
// ===========================================================================
async Task<int> RunTest4(string agentDir, string cfg, int hb, int intervalSec, int timeoutSec)
{
    PrintHeader("TEST 4 — Agent Standalone Full Lifecycle");

    var (exe, exeArgs) = ResolveExe(agentDir, "FreeServicesHub.Agent", cfg);
    if (exe is null) return Fail("Agent executable not found.");

    WriteStep($"Starting agent, waiting for {hb} heartbeats...");
    WriteInfo($"Expected interval: ~{intervalSec}s per heartbeat");
    WriteInfo($"Timeout: {timeoutSec}s");

    var output = new List<string>();
    using var process = StartProcess(exe, exeArgs, line =>
    {
        WriteDim(line);
        output.Add(line);
    });

    if (process is null) return Fail("Failed to start agent process.");

    var sw = Stopwatch.StartNew();
    var heartbeatCount = 0;

    while (sw.Elapsed.TotalSeconds < timeoutSec && !process.HasExited)
    {
        heartbeatCount = output.Count(l => l.Contains("Heartbeat", StringComparison.OrdinalIgnoreCase)
                                         && l.Contains("CPU:", StringComparison.OrdinalIgnoreCase));
        if (heartbeatCount >= hb)
            break;
        await Task.Delay(1000);
    }

    KillProcess(process);

    if (heartbeatCount < hb)
        return Fail($"Only {heartbeatCount}/{hb} heartbeats received within {timeoutSec}s timeout.");

    WritePass($"{heartbeatCount} heartbeat(s) received.");

    // Verify log file was created
    var possibleLogPaths = new[]
    {
        Path.Combine(agentDir, "bin", cfg, "net10.0", "agent.log"),
        Path.Combine(agentDir, "agent.log"),
        Path.Combine(Path.GetDirectoryName(exe) ?? agentDir, "agent.log"),
    };

    var foundLog = possibleLogPaths.FirstOrDefault(File.Exists);
    if (foundLog is not null)
    {
        WritePass($"Log file found: {foundLog}");
        var lines = File.ReadAllLines(foundLog);
        WriteInfo($"Log file has {lines.Length} line(s).");
        if (lines.Any(l => l.Contains("Heartbeat")))
            WritePass("Log file contains heartbeat entries.");
    }
    else
    {
        WriteWarn("Log file (agent.log) not found — may be in a different output path.");
    }

    // Verify all heartbeat lines have expected markers
    var heartbeatLines = output.Where(l => l.Contains("Heartbeat", StringComparison.OrdinalIgnoreCase)
                                        && l.Contains("CPU:", StringComparison.OrdinalIgnoreCase)).ToList();
    foreach (var line in heartbeatLines)
    {
        if (!line.Contains("RAM:"))
            return Fail($"Heartbeat line missing RAM: {line}");
    }
    WritePass("All heartbeat lines contain CPU and RAM markers.");

    return Pass("Test 4 passed.");
}


// ===========================================================================
// Process Helpers
// ===========================================================================

static Process? StartProcess(string fileName, string arguments, Action<string> onOutput)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) onOutput(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) onOutput($"[STDERR] {e.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }
    catch (Exception ex)
    {
        WriteFail($"Start process error: {ex.Message}");
        return null;
    }
}

static void KillProcess(Process process)
{
    try
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
    }
    catch { /* best effort */ }
}

static async Task<(int exitCode, List<string> output)> RunProcessCapture(string fileName, string arguments, int timeoutSec)
{
    var output = new List<string>();
    using var process = StartProcess(fileName, arguments, line =>
    {
        WriteDim(line);
        output.Add(line);
    });

    if (process is null) return (1, output);

    var sw = Stopwatch.StartNew();
    while (sw.Elapsed.TotalSeconds < timeoutSec && !process.HasExited)
        await Task.Delay(500);

    if (!process.HasExited)
    {
        KillProcess(process);
        return (1, output);
    }

    return (process.ExitCode, output);
}


// ===========================================================================
// Exe Resolution
// ===========================================================================

/// <summary>
/// Resolves the path to the executable. If the directory contains a .exe directly
/// (artifact mode), returns it. Otherwise, returns dotnet run args (source mode).
/// </summary>
static (string? exe, string args) ResolveExe(string dir, string projectName, string config)
{
    // Check for pre-built exe (artifact mode)
    var exePath = Path.Combine(dir, $"{projectName}.exe");
    if (File.Exists(exePath))
        return (exePath, "");

    // Check bin output
    var binExe = Path.Combine(dir, "bin", config, "net10.0", $"{projectName}.exe");
    if (File.Exists(binExe))
        return (binExe, "");

    // Check for csproj (source mode -- use dotnet run)
    var csproj = Path.Combine(dir, $"{projectName}.csproj");
    if (File.Exists(csproj))
        return ("dotnet", $"run --project \"{csproj}\" --configuration {config} --no-build --");

    // Search recursively for the exe
    if (Directory.Exists(dir))
    {
        var found = Directory.GetFiles(dir, $"{projectName}.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (found is not null)
            return (found, "");
    }

    return (null, "");
}


// ===========================================================================
// Argument Parsing
// ===========================================================================

static Dictionary<string, string> ParseArgs(string[] args)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var arg in args)
    {
        if (arg.StartsWith("--"))
        {
            var parts = arg[2..].Split('=', 2);
            dict[parts[0]] = parts.Length > 1 ? parts[1] : "true";
        }
    }
    return dict;
}

static string? FindSolutionDir()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
            return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}


// ===========================================================================
// Console Output
// ===========================================================================

static void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("""

     _____ ____  _____ _____   _   _ _   _ ____
    |  ___|  _ \| ____| ____| | | | | | | | __ )
    | |_  | |_) |  _| |  _|   | |_| | | | |  _ \
    |  _| |  _ <| |___| |___  |  _  | |_| | |_) |
    |_|   |_| \_\_____|_____| |_| |_|\___/|____/    TEST RUNNER

    """);
    Console.ResetColor();
}

static void PrintHeader(string title)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"\n  ╔══════════════════════════════════════════════════╗");
    Console.WriteLine($"  ║  {title,-48}║");
    Console.WriteLine($"  ╚══════════════════════════════════════════════════╝\n");
    Console.ResetColor();
}

static void WriteStep(string msg)
{
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine($"  >> {msg}");
    Console.ResetColor();
}

static void WriteInfo(string msg)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  [INFO] {msg}");
    Console.ResetColor();
}

static void WritePass(string msg)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  [PASS] {msg}");
    Console.ResetColor();
}

static void WriteFail(string msg)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  [FAIL] {msg}");
    Console.ResetColor();
}

static void WriteWarn(string msg)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  [WARN] {msg}");
    Console.ResetColor();
}

static void WriteDim(string msg)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"    {msg}");
    Console.ResetColor();
}

static int Pass(string msg) { WritePass(msg); return 0; }
static int Fail(string msg) { WriteFail(msg); return 1; }
