// FreeServicesHub.Agent.Installer -- Program.cs
// Dual CLI/UI interface for building, deploying, and managing the FreeServicesHub Agent.
// Windows only -- uses sc.exe for service management.
//
// Usage:
//   dotnet run                            -> Interactive menu
//   dotnet run -- <action> [--overrides]  -> CLI mode
//
// Actions: build, configure, remove, install, uninstall, start, stop, status, destroy

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeServicesHub.Agent.Installer;
using Microsoft.Extensions.Configuration;

// ---- Configuration ----

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddCommandLine(args)
    .Build();

var config = new InstallerConfig();
configuration.Bind(config);

// ---- Resolve relative paths ----
ResolveProjectPaths(config);
ResolvePublishOutputPath(config);

// ---- Banner ----
PrintBanner();

// ---- Route: CLI or Interactive ----
var action = args.FirstOrDefault(a => !a.StartsWith("--"));

if (!string.IsNullOrEmpty(action))
{
    // CLI mode: auto-enable noninteractive unless explicitly set to false
    if (!args.Any(a => a.StartsWith("--NonInteractive=", StringComparison.OrdinalIgnoreCase)))
        config.NonInteractive = true;

    return RunAction(action, config);
}

return RunInteractive(config);


// =====================================================================
// RunAction -- central dispatcher. Both CLI and menu call this.
// =====================================================================

static int RunAction(string action, InstallerConfig config)
{
    return action.ToLowerInvariant() switch
    {
        "build" => ActionBuild(config),
        "configure" or "install" => ActionConfigure(config),
        "remove" or "uninstall" => ActionUninstall(config),
        "start" => ActionStart(config),
        "stop" => ActionStop(config),
        "status" => ActionStatus(config),
        "destroy" => ActionDestroy(config),
        _ => ActionUnknown(action),
    };
}


// =====================================================================
// Interactive Menu
// =====================================================================

static int RunInteractive(InstallerConfig config)
{
    while (true)
    {
        PrintMenu(config);

        Console.Write("  Select option: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input)) continue;

        var menuAction = input.ToUpperInvariant() switch
        {
            "1" => "build",
            "2" => "configure",
            "3" => "remove",
            "4" => "start",
            "5" => "stop",
            "6" => "status",
            "7" => "destroy",
            "Q" => (string?)null,
            _ => "unknown:" + input,
        };

        if (menuAction is null)
        {
            Console.WriteLine("\n  Goodbye.\n");
            return 0;
        }

        if (menuAction.StartsWith("unknown:"))
        {
            WriteWarning($"Unknown option: {input}");
            continue;
        }

        Console.WriteLine();
        var result = RunAction(menuAction, config);

        Console.WriteLine();
        if (result == 0)
            WriteSuccess("Done.");
        else
            WriteError($"Failed (exit code {result}).");

        Console.WriteLine("\n  Press Enter to continue...");
        Console.ReadLine();
    }
}

static void PrintMenu(InstallerConfig config)
{
    Console.Clear();
    PrintBanner();

    var configured = IsAlreadyConfigured(config);

    Console.Write("\n      Status: ");
    if (configured)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("CONFIGURED");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("NOT CONFIGURED");
    }
    Console.ResetColor();
    Console.WriteLine("  |  Platform: Windows");

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"""

      CURRENT CONFIGURATION
      Service Name:   {config.Service.Name}
      Display Name:   {config.Service.DisplayName}
      Exe Path:       {config.Service.ExePath}
      Project:        {Path.GetFullPath(config.Publish.ProjectPath)}
      Publish To:     {config.Publish.OutputPath}
      Install To:     {config.Service.InstallPath}
      Runtime:        {config.Publish.Runtime}
    """);
    Console.ResetColor();

    Console.WriteLine("""
      BUILD & DEPLOY
      1. Build (dotnet publish)

      SERVICE MANAGEMENT
      2. Configure (install + set API key)
      3. Remove (stop + uninstall)
      4. Start Service
      5. Stop Service
      6. Query Status

      MAINTENANCE
      7. Destroy (undo everything)

      Q. Quit

    """);
}


// =====================================================================
// Actions
// =====================================================================

static int ActionBuild(InstallerConfig config)
{
    PrintHeader("BUILD");

    var projectPath = Path.GetFullPath(config.Publish.ProjectPath);
    if (!Directory.Exists(projectPath) && !File.Exists(projectPath))
    {
        WriteError($"Project path not found: {projectPath}");
        return 1;
    }

    // Clean publish directory
    var outputPath = config.Publish.OutputPath;
    if (Directory.Exists(outputPath))
    {
        WriteStep($"Cleaning publish directory: {outputPath}");
        try { Directory.Delete(outputPath, recursive: true); }
        catch (Exception ex) { WriteWarning($"Could not clean directory: {ex.Message}"); }
    }

    Directory.CreateDirectory(outputPath);

    var scFlag = config.Publish.SelfContained ? "--self-contained" : "--no-self-contained";
    var buildArgs = $"publish \"{projectPath}\" -c Release -r {config.Publish.Runtime} {scFlag} -o \"{outputPath}\"";

    if (config.Publish.SingleFile)
        buildArgs += " -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true";

    WriteInfo($"Project:    {projectPath}");
    WriteInfo($"Output:     {outputPath}");
    WriteInfo($"Runtime:    {config.Publish.Runtime}");
    WriteInfo($"Contained:  {config.Publish.SelfContained}");
    WriteInfo($"SingleFile: {config.Publish.SingleFile}");
    Console.WriteLine();

    return RunDotnet(buildArgs);
}


static int ActionConfigure(InstallerConfig config)
{
    // Already-configured guard
    if (IsAlreadyConfigured(config))
    {
        WriteError("Cannot configure the agent because it is already configured.");
        WriteError("To reconfigure, run 'remove' first.");
        return 1;
    }

    var nonInteractive = config.NonInteractive;

    Console.WriteLine(">> Configure:\n");

    // Service Name
    if (nonInteractive)
    {
        Console.WriteLine($"  Service name: {config.Service.Name}");
    }
    else
    {
        Console.Write($"  Enter service name (press enter for {config.Service.Name}) > ");
        var nameInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(nameInput))
            config.Service.Name = nameInput;
    }

    // API Key (optional -- if empty, agent runs in standalone mode)
    if (nonInteractive)
    {
        if (!string.IsNullOrEmpty(config.Security.ApiKey))
            Console.WriteLine($"  API key: {new string('*', Math.Min(config.Security.ApiKey.Length, 20))}");
        else
            Console.WriteLine("  API key: (none -- standalone mode)");
    }
    else
    {
        Console.Write("  Enter API key (press enter to skip for standalone mode) > ");
        var apiKeyInput = ReadMaskedInput();
        Console.WriteLine();
        if (!string.IsNullOrWhiteSpace(apiKeyInput))
            config.Security.ApiKey = apiKeyInput;
    }

    if (!string.IsNullOrEmpty(config.Security.ApiKey))
    {
        WriteStep("Validating API key...");
        WriteSuccess("API key validated.");
    }
    else
    {
        WriteInfo("No API key provided. Agent will run in standalone mode (console/file logging only).");
    }
    Console.WriteLine();

    // Source path (publish output)
    if (nonInteractive)
    {
        Console.WriteLine($"  Publish path: {config.Publish.OutputPath}");
    }
    else
    {
        Console.Write($"  Enter source (publish) path (press enter for {config.Publish.OutputPath}) > ");
        var sourceInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(sourceInput))
            config.Publish.OutputPath = sourceInput;
    }

    // Install path
    if (nonInteractive)
    {
        Console.WriteLine($"  Install path: {config.Service.InstallPath}");
    }
    else
    {
        Console.Write($"  Enter install path (press enter for {config.Service.InstallPath}) > ");
        var pathInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(pathInput))
            config.Service.InstallPath = pathInput;
    }

    // Derive ExePath from InstallPath
    config.Service.ExePath = Path.Combine(config.Service.InstallPath, "FreeServicesHub.Agent.exe");

    Console.WriteLine();
    WriteStep("Connecting to server...");

    // Copy published files to install path
    var copyResult = CopyPublishToInstall(config);
    if (copyResult != 0) return copyResult;

    // Write API key to agent's appsettings.json (only if a key was provided)
    if (!string.IsNullOrEmpty(config.Security.ApiKey))
    {
        var writeResult = WriteApiKeyToServiceConfig(config);
        if (writeResult != 0)
        {
            WriteError("Failed to write API key to service configuration.");
            return writeResult;
        }
        WriteSuccess("API key written to service configuration.");
    }
    else
    {
        WriteInfo("No API key -- skipping credential write. Agent will run standalone.");
    }

    // Install the Windows service via sc.exe
    WriteStep("Installing service...");
    Console.WriteLine();
    var installResult = InstallWindows(config);
    if (installResult != 0) return installResult;

    // Write configured marker
    WriteConfiguredMarker(config);

    Console.WriteLine();
    WriteSuccess("Successfully configured the agent.");
    WriteSuccess("Settings Saved.");
    return 0;
}


static int ActionUninstall(InstallerConfig config)
{
    PrintHeader("REMOVE SERVICE");

    WriteStep("Removing agent from the server");
    Console.WriteLine();

    // Authentication
    var nonInteractive = config.NonInteractive;

    if (nonInteractive)
    {
        Console.WriteLine($"  API key: {new string('*', Math.Min(config.Security.ApiKey.Length, 20))}");
    }
    else
    {
        Console.Write("  Enter API key > ");
        var apiKeyInput = ReadMaskedInput();
        Console.WriteLine();
        if (!string.IsNullOrWhiteSpace(apiKeyInput))
            config.Security.ApiKey = apiKeyInput;
    }

    WriteStep("Connecting to server...");
    Console.WriteLine();

    WriteStep($"Stopping {config.Service.Name}...");
    RunProcess("sc.exe", $"stop {config.Service.Name}");
    Thread.Sleep(2000);

    WriteStep($"Deleting {config.Service.Name}...");
    var result = RunProcess("sc.exe", $"delete {config.Service.Name}");

    if (result != 0 && IsAccessDenied(result))
    {
        PrintAccessDeniedGuidance("remove the Windows service");
        return result;
    }

    RemoveConfiguredMarker(config);
    ClearApiKeyFromServiceConfig(config);

    if (result == 0)
    {
        Console.WriteLine();
        WriteSuccess("Removing agent from the server");
        WriteSuccess("Removing .credentials");
        WriteSuccess("Removing .agent");
    }
    else
    {
        WriteWarning("Delete returned non-zero. Service may not exist.");
    }

    return result;
}


static int ActionStart(InstallerConfig config)
{
    PrintHeader("START SERVICE");
    WriteStep($"Starting {config.Service.Name}...");
    Console.WriteLine();

    var result = RunProcess("sc.exe", $"start {config.Service.Name}");

    if (result == 0)
        WriteSuccess("Service started.");
    else if (IsAccessDenied(result))
        PrintAccessDeniedGuidance("start the service");

    return result;
}


static int ActionStop(InstallerConfig config)
{
    PrintHeader("STOP SERVICE");
    WriteStep($"Stopping {config.Service.Name}...");
    Console.WriteLine();

    var result = RunProcess("sc.exe", $"stop {config.Service.Name}");

    if (result == 0)
        WriteSuccess("Service stopped.");
    else if (IsAccessDenied(result))
        PrintAccessDeniedGuidance("stop the service");

    return result;
}


static int ActionStatus(InstallerConfig config)
{
    PrintHeader("SERVICE STATUS");

    WriteInfo($"Querying {config.Service.Name}...");
    Console.WriteLine();
    RunProcess("sc.exe", $"query {config.Service.Name}");

    Console.WriteLine();

    // Show recent log output if available
    var logPath = Path.Combine(config.Service.InstallPath, "agent.log");
    if (File.Exists(logPath))
    {
        WriteInfo($"Last 10 lines of {logPath}");
        Console.WriteLine();
        var lines = File.ReadAllLines(logPath);
        var start = Math.Max(0, lines.Length - 10);
        for (var i = start; i < lines.Length; i++)
            WriteDim(lines[i]);
        Console.WriteLine();
    }
    else
    {
        WriteWarning("No log file found.");
    }

    return 0;
}


static int ActionDestroy(InstallerConfig config)
{
    PrintHeader("DESTROY -- UNDO EVERYTHING");

    var nonInteractive = config.NonInteractive;
    var serviceName = config.Service.Name;
    var installPath = config.Service.InstallPath;

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("""
    +============================================================+
    |                    DESTROY                                  |
    |                                                             |
    |  This will permanently remove EVERYTHING the installer has  |
    |  created using the current configuration defaults.          |
    +============================================================+
    """);
    Console.ResetColor();

    Console.WriteLine($"    Service:          {serviceName}");
    Console.WriteLine($"    Install Path:     {installPath}");
    Console.WriteLine($"    Publish Folders:  publish/*");
    Console.WriteLine($"    Config Marker:    .configured");
    Console.WriteLine();

    // Confirmation
    if (!nonInteractive)
    {
        WriteWarning("This cannot be undone.");
        Console.Write("  Type DESTROY to confirm > ");
        var answer = Console.ReadLine()?.Trim();
        if (answer != "DESTROY")
        {
            WriteWarning("Cancelled -- confirmation did not match.");
            return 0;
        }
    }

    Console.WriteLine();
    int errors = 0;

    // 1. Stop & remove the service
    WriteStep($"[1/4] Stopping service '{serviceName}'...");
    RunProcess("sc.exe", $"stop {serviceName}");
    Thread.Sleep(2000);
    WriteStep($"       Deleting service '{serviceName}'...");
    var r = RunProcess("sc.exe", $"delete {serviceName}");
    if (r != 0 && IsAccessDenied(r))
    {
        PrintAccessDeniedGuidance("delete the Windows service");
        errors++;
    }

    // 2. Remove installed files
    WriteStep($"[2/4] Removing install directory: {installPath}");
    if (Directory.Exists(installPath))
    {
        try { Directory.Delete(installPath, recursive: true); WriteSuccess("Install directory removed."); }
        catch (Exception ex) { WriteWarning($"Could not remove: {ex.Message}"); errors++; }
    }
    else
    {
        WriteDim("Directory does not exist -- skipping.");
    }

    // 3. Remove publish output
    WriteStep("[3/4] Removing publish directories...");
    var publishBase = Path.GetDirectoryName(config.Publish.OutputPath);
    if (!string.IsNullOrEmpty(publishBase) && Directory.Exists(publishBase))
    {
        try { Directory.Delete(publishBase, recursive: true); WriteSuccess("Publish directory removed."); }
        catch (Exception ex) { WriteWarning($"Could not remove: {ex.Message}"); errors++; }
    }
    else
    {
        WriteDim("Publish directory does not exist -- skipping.");
    }

    // 4. Remove configured marker
    WriteStep("[4/4] Removing configured marker...");
    RemoveConfiguredMarker(config);
    WriteSuccess("Marker removed.");

    Console.WriteLine();
    if (errors == 0)
        WriteSuccess("Destroy complete. Everything has been removed.");
    else
        WriteWarning($"Destroy completed with {errors} warning(s). Review output above.");

    return errors > 0 ? 1 : 0;
}


static int ActionUnknown(string action)
{
    WriteError($"Unknown action: {action}");
    Console.WriteLine();
    Console.WriteLine("  Available actions: build, configure, remove, start, stop, status, destroy");
    return 1;
}


// =====================================================================
// Windows Service Install
// =====================================================================

static int InstallWindows(InstallerConfig config)
{
    var exePath = config.Service.ExePath;
    if (!File.Exists(exePath))
    {
        WriteWarning($"Exe not found at: {exePath}");
        WriteWarning("Run 'build' first, or check Publish:OutputPath");
        Console.WriteLine();
    }

    var createArgs = $"create {config.Service.Name} binPath= \"{exePath}\" start= auto DisplayName= \"{config.Service.DisplayName}\"";
    var result = RunProcess("sc.exe", createArgs);

    if (result != 0)
    {
        if (IsAccessDenied(result))
            PrintAccessDeniedGuidance("install the Windows service");
        return result;
    }

    // Set description
    RunProcess("sc.exe", $"description {config.Service.Name} \"{config.Service.Description}\"");

    // Configure crash recovery: restart on failure
    RunProcess("sc.exe", $"failure {config.Service.Name} reset= 86400 actions= restart/5000/restart/5000/restart/5000");
    RunProcess("sc.exe", $"failureflag {config.Service.Name} 1");

    Console.WriteLine();
    WriteSuccess("Windows Service installed with recovery configuration.");
    return 0;
}


// =====================================================================
// File Copy
// =====================================================================

static int CopyPublishToInstall(InstallerConfig config)
{
    var source = config.Publish.OutputPath;
    var dest = config.Service.InstallPath;

    if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
    {
        WriteInfo("Publish and install paths are the same -- skipping copy.");
        return 0;
    }

    if (!Directory.Exists(source))
    {
        WriteWarning($"Publish directory not found: {source}");
        WriteWarning("Run 'build' first to publish the agent.");
        WriteWarning("Skipping copy -- install will use whatever is already at the install path.");
        return 0;
    }

    WriteStep("Copying files:");
    WriteDim($"From: {source}");
    WriteDim($"To:   {dest}");

    try
    {
        Directory.CreateDirectory(dest);

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(source, file);
            var destFile = Path.Combine(dest, relativePath);
            var destDir = Path.GetDirectoryName(destFile)!;
            Directory.CreateDirectory(destDir);
            File.Copy(file, destFile, overwrite: true);
        }

        var fileCount = Directory.GetFiles(source, "*", SearchOption.AllDirectories).Length;
        WriteSuccess($"Copied {fileCount} file(s) to install path.");
        return 0;
    }
    catch (Exception ex)
    {
        WriteError($"Copying files: {ex.Message}");
        return 1;
    }
}


// =====================================================================
// API Key -- write to agent appsettings.json
// =====================================================================

static int WriteApiKeyToServiceConfig(InstallerConfig config)
{
    try
    {
        var candidates = new[]
        {
            Path.Combine(config.Service.InstallPath, "appsettings.json"),
            Path.Combine(config.Publish.OutputPath, "appsettings.json"),
            Path.Combine(Path.GetFullPath(config.Publish.ProjectPath), "appsettings.json"),
        };

        foreach (var settingsPath in candidates)
        {
            if (!File.Exists(settingsPath)) continue;

            var json = File.ReadAllText(settingsPath);
            var doc = JsonNode.Parse(json) ?? new JsonObject();

            // Write into Agent.RegistrationKey
            var agentSection = doc["Agent"]?.AsObject();
            if (agentSection is null)
            {
                agentSection = new JsonObject();
                doc["Agent"] = agentSection;
            }

            agentSection["RegistrationKey"] = config.Security.ApiKey;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(settingsPath, doc.ToJsonString(options));
        }

        return 0;
    }
    catch (Exception ex)
    {
        WriteError($"Writing API key: {ex.Message}");
        return 1;
    }
}


static void ClearApiKeyFromServiceConfig(InstallerConfig config)
{
    try
    {
        var candidates = new[]
        {
            Path.Combine(config.Service.InstallPath, "appsettings.json"),
            Path.Combine(config.Publish.OutputPath, "appsettings.json"),
            Path.Combine(Path.GetFullPath(config.Publish.ProjectPath), "appsettings.json"),
        };

        foreach (var settingsPath in candidates)
        {
            if (!File.Exists(settingsPath)) continue;

            var json = File.ReadAllText(settingsPath);
            var doc = JsonNode.Parse(json) ?? new JsonObject();

            var agentSection = doc["Agent"]?.AsObject();
            if (agentSection is not null)
            {
                agentSection["RegistrationKey"] = "";
                agentSection["ApiClientToken"] = "";
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(settingsPath, doc.ToJsonString(options));
            }
        }
    }
    catch { /* best effort */ }
}


// =====================================================================
// Configured Marker
// =====================================================================

static string GetConfiguredMarkerPath(InstallerConfig config)
{
    return Path.Combine(config.Service.InstallPath, ".configured");
}

static bool IsAlreadyConfigured(InstallerConfig config)
{
    return File.Exists(GetConfiguredMarkerPath(config));
}

static void WriteConfiguredMarker(InstallerConfig config)
{
    try
    {
        Directory.CreateDirectory(config.Service.InstallPath);
        var marker = new
        {
            ConfiguredAt = DateTime.UtcNow.ToString("o"),
            ServiceName = config.Service.Name,
            Platform = "Windows",
        };
        File.WriteAllText(
            GetConfiguredMarkerPath(config),
            JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true }));
    }
    catch { /* best effort */ }
}

static void RemoveConfiguredMarker(InstallerConfig config)
{
    try
    {
        var path = GetConfiguredMarkerPath(config);
        if (File.Exists(path))
            File.Delete(path);
    }
    catch { /* best effort */ }
}


// =====================================================================
// Path Resolution
// =====================================================================

static void ResolveProjectPaths(InstallerConfig config)
{
    if (Path.IsPathRooted(config.Publish.ProjectPath)
        && (Directory.Exists(config.Publish.ProjectPath) || File.Exists(config.Publish.ProjectPath)))
    {
        return;
    }

    var targetName = Path.GetFileName(config.Publish.ProjectPath);
    var dir = new DirectoryInfo(AppContext.BaseDirectory);

    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, targetName);
        if (Directory.Exists(candidate) || File.Exists(candidate))
        {
            config.Publish.ProjectPath = candidate;
            return;
        }
        dir = dir.Parent;
    }
}

static void ResolvePublishOutputPath(InstallerConfig config)
{
    if (!string.IsNullOrEmpty(config.Publish.OutputPath))
        return;

    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
        {
            config.Publish.OutputPath = Path.Combine(dir.FullName, "publish", config.Publish.Runtime);
            return;
        }
        dir = dir.Parent;
    }

    config.Publish.OutputPath = Path.Combine(AppContext.BaseDirectory, "publish", config.Publish.Runtime);
}


// =====================================================================
// Process Helpers
// =====================================================================

static int RunDotnet(string arguments)
{
    return RunProcess("dotnet", arguments);
}

static int RunProcess(string fileName, string arguments)
{
    WriteDim($"> {fileName} {arguments}");
    Console.WriteLine();

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

        using var process = Process.Start(psi);
        if (process is null)
        {
            WriteError("Failed to start process.");
            return 1;
        }

        while (!process.StandardOutput.EndOfStream)
        {
            var line = process.StandardOutput.ReadLine();
            if (line is not null)
                WriteDim(line);
        }

        var stderr = process.StandardError.ReadToEnd();
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            WriteWarning("STDERR:");
            foreach (var line in stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                WriteWarning(line);
        }

        process.WaitForExit();
        return process.ExitCode;
    }
    catch (Exception ex)
    {
        WriteError(ex.Message);
        return 1;
    }
}


// =====================================================================
// Console Output Helpers
// =====================================================================

static void PrintHeader(string title)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"  -- {title} --");
    Console.ResetColor();
    Console.WriteLine();
}

static void WriteSuccess(string message)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  [OK] {message}");
    Console.ResetColor();
}

static void WriteError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  [ERROR] {message}");
    Console.ResetColor();
}

static void WriteWarning(string message)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  [WARN] {message}");
    Console.ResetColor();
}

static void WriteInfo(string message)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  [INFO] {message}");
    Console.ResetColor();
}

static void WriteStep(string message)
{
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine($"  >> {message}");
    Console.ResetColor();
}

static void WriteDim(string message)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"    {message}");
    Console.ResetColor();
}

static bool IsAccessDenied(int exitCode)
{
    return exitCode == 5;
}

static void PrintAccessDeniedGuidance(string operation)
{
    Console.WriteLine();
    WriteError($"Access denied while trying to {operation}.");
    Console.WriteLine();
    WriteInfo("Run this installer as Administrator:");
    WriteDim("Right-click the terminal and select 'Run as administrator',");
    WriteDim("or from an elevated prompt run the installer again.");
    Console.WriteLine();
}

static void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("""

     _____ ____  _____ _____   _   _ _   _ ____
    |  ___|  _ \| ____| ____| | | | | | | | __ )
    | |_  | |_) |  _| |  _|   | |_| | | | |  _ \
    |  _| |  _ <| |___| |___  |  _  | |_| | |_) |
    |_|   |_| \_\_____|_____| |_| |_|\___/|____/    AGENT INSTALLER

    """);
    Console.ResetColor();
}

static string ReadMaskedInput()
{
    var input = new System.Text.StringBuilder();
    while (true)
    {
        var keyInfo = Console.ReadKey(intercept: true);
        if (keyInfo.Key == ConsoleKey.Enter)
            break;
        if (keyInfo.Key == ConsoleKey.Backspace)
        {
            if (input.Length > 0)
            {
                input.Remove(input.Length - 1, 1);
                Console.Write("\b \b");
            }
        }
        else if (!char.IsControl(keyInfo.KeyChar))
        {
            input.Append(keyInfo.KeyChar);
            Console.Write('*');
        }
    }
    return input.ToString();
}
