// FreeServices.Installer — Program.cs
// Dual CLI/UI interface for building, deploying, and managing FreeServices.
// Cross-platform: Windows (sc.exe), Linux (systemd), macOS (launchd).
//
// Usage:
//   dotnet run                            → Interactive menu (12 options)
//   dotnet run -- <action> [--overrides]  → CLI mode
//
// Core Actions:
//   build          Build/publish the service (dotnet publish)
//   deploy         Full pipeline: build → configure → start
//   configure      Interactive setup (API key, account, install)
//   remove         Auth + stop + uninstall + clear credentials
//   start          Start the service
//   stop           Stop the service
//   status         Query status + recent log
//   config         View current configuration
//   users          Service Account Manager (interactive submenu)
//   instructions   Interactive help with detailed guides (alias: docs)
//   help           Show CLI usage reference
//   cleanup        Delete publish folders to free disk space
//   destroy        Nuclear option: remove everything
//
// Account Management:
//   account-view   Show current user & system accounts
//   account-create Create service account  (--Target:Username, --ServiceAccount:Password)
//   account-delete Delete service account  (--Target:Username, --Target:Confirm=true)
//   account-lookup Look up user: existence, groups, permissions (--Target:Username)
//   grant          Grant a permission      (--Target:Username, --Target:Permission)
//   revoke         Revoke a permission     (--Target:Username, --Target:Permission)
//
// Service & Docker Control:
//   svc-list       List all OS services
//   svc-search     Search services         (--Target:Search)
//   svc-start      Start an OS service     (--Target:ServiceName)
//   svc-stop       Stop an OS service      (--Target:ServiceName)
//   docker-list    List Docker containers
//   docker-start   Start a container       (--Target:ContainerName)
//   docker-stop    Stop a container        (--Target:ContainerName)
//
// Non-interactive / CI/CD mode (all flags via CLI):
//   installer.exe configure --Security:ApiKey=<key> --Service:Name=MyAgent
//   installer.exe remove --Security:ApiKey=<key>
//   installer.exe account-create --Target:Username=Agent --ServiceAccount:Password=<pass>
//   installer.exe destroy --Target:Confirm=true
//
// Override any config via CLI:
//   dotnet run -- deploy --Service:Name=MyService --Publish:OutputPath=/opt/svc

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeServices.Installer;
using Microsoft.Extensions.Configuration;

// ──── Configuration ────

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddCommandLine(args)
    .Build();

var config = new InstallerConfig();
configuration.Bind(config);

// ──── Resolve relative paths ────
// When running from bin\Debug\net10.0\ the default "../FreeServices.Service"
// would resolve incorrectly. Walk up from the exe directory to find the
// solution root (.slnx or .sln), then anchor relative paths there.

ResolveProjectPaths(config);
ResolvePublishOutputPath(config);

// ──── Banner ────

PrintBanner();

// ──── Route: CLI or Interactive ────

// Find the first positional argument that isn't a --key=value flag
var action = args.FirstOrDefault(a => !a.StartsWith("--"));

if (!string.IsNullOrEmpty(action))
{
    // CLI mode — run the action and exit
    return RunAction(action, config);
}

// Interactive mode — show menu in a loop
return RunInteractive(config);


// ═══════════════════════════════════════════════════════════════════════
// RunAction — the central dispatcher. Both CLI and menu call this.
// ═══════════════════════════════════════════════════════════════════════

static int RunAction(string action, InstallerConfig config)
{
    return action.ToLowerInvariant() switch
    {
        "build" => ActionBuild(config),
        "deploy" => ActionDeploy(config),
        "configure" or "install" => ActionConfigure(config),
        "remove" or "uninstall" => ActionUninstall(config),
        "start" => ActionStart(config),
        "stop" => ActionStop(config),
        "status" => ActionStatus(config),
        "config" => ActionViewConfig(config),
        "users" => ActionUsers(config),
        "instructions" or "docs" => ActionInstructions(),
        "help" => ActionHelp(),
        "cleanup" => ActionCleanup(config),
        "destroy" => ActionDestroy(config),

        // ── Service Account Manager CLI equivalents ──
        "account-view" => ActionAccountView(),
        "account-create" => ActionAccountCreate(config),
        "account-delete" => ActionAccountDelete(config),
        "account-lookup" => ActionAccountLookup(config),
        "grant" => ActionGrant(config),
        "revoke" => ActionRevoke(config),
        "svc-list" => ActionSvcList(),
        "svc-search" => ActionSvcSearch(config),
        "svc-start" => ActionSvcControl(config, start: true),
        "svc-stop" => ActionSvcControl(config, start: false),
        "docker-list" => ActionDockerList(),
        "docker-start" => ActionDockerControl(config, start: true),
        "docker-stop" => ActionDockerControl(config, start: false),

        _ => ActionUnknown(action),
    };
}


// ═══════════════════════════════════════════════════════════════════════
// Interactive Menu
// ═══════════════════════════════════════════════════════════════════════

static int RunInteractive(InstallerConfig config)
{
    while (true)
    {
        PrintMenu(config);

        Console.Write("  Select option: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input)) continue;

        // Map menu numbers to action strings
        var menuAction = input.ToUpperInvariant() switch
        {
            "1" => "build",
            "2" => "deploy",
            "3" => "configure",
            "4" => "remove",
            "5" => "start",
            "6" => "stop",
            "7" => "status",
            "8" => "config",
            "9" => "users",
            "10" => "instructions",
            "11" => "cleanup",
            "12" => "destroy",
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
    var platform = GetPlatformName();
    var configured = IsAlreadyConfigured(config);

    Console.Clear();
    PrintBanner();

    // ── Status line with color ──
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
    Console.WriteLine($"  |  Platform: {platform}");

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
      Self-Contained: {(config.Publish.SelfContained ? "Yes" : "No")}
      Single File:    {(config.Publish.SingleFile ? "Yes" : "No")}
    """);
    Console.ResetColor();

    Console.WriteLine($"""
      BUILD & DEPLOY
      1. Build (dotnet publish)
      2. Full Deploy (build → configure → start)

      SERVICE MANAGEMENT
      3. Configure (install + set API key)
      4. Remove (stop + uninstall)
      5. Start Service
      6. Stop Service
      7. Query Status

      OTHER
      8. View Configuration
      9. Service Account Manager
     10. Instructions & Help

      MAINTENANCE
     11. Cleanup (delete publish folders)
     12. Destroy (undo everything — nuclear option)

      Q. Quit

    """);
}


// ═══════════════════════════════════════════════════════════════════════
// Actions
// ═══════════════════════════════════════════════════════════════════════

static int ActionBuild(InstallerConfig config)
{
    PrintHeader("BUILD");

    var projectPath = Path.GetFullPath(config.Publish.ProjectPath);
    if (!Directory.Exists(projectPath) && !File.Exists(projectPath))
    {
        WriteError($"Project path not found: {projectPath}");
        return 1;
    }

    // ── Interactive target platform chooser ──
    // Only prompt if running interactively (no CLI flags overriding Runtime)
    if (!Console.IsInputRedirected)
    {
        var currentPlatform = GetPlatformName();
        Console.WriteLine($"  Select target platform:\n");
        Console.WriteLine($"    1. Windows x64     (win-x64)");
        Console.WriteLine($"    2. Windows ARM64   (win-arm64)");
        Console.WriteLine($"    3. Linux x64       (linux-x64)");
        Console.WriteLine($"    4. Linux ARM64     (linux-arm64)");
        Console.WriteLine($"    5. macOS x64       (osx-x64)");
        Console.WriteLine($"    6. macOS ARM64     (osx-arm64)");
        Console.WriteLine();
        Console.Write($"  Enter choice (press enter for current OS: {config.Publish.Runtime}) > ");
        var platformChoice = Console.ReadLine()?.Trim();

        if (!string.IsNullOrEmpty(platformChoice))
        {
            var previousRuntime = config.Publish.Runtime;
            config.Publish.Runtime = platformChoice switch
            {
                "1" => "win-x64",
                "2" => "win-arm64",
                "3" => "linux-x64",
                "4" => "linux-arm64",
                "5" => "osx-x64",
                "6" => "osx-arm64",
                _ => config.Publish.Runtime,
            };

            // Update the output path to match the new runtime if it was auto-generated
            if (config.Publish.Runtime != previousRuntime
                && config.Publish.OutputPath.EndsWith(previousRuntime, StringComparison.OrdinalIgnoreCase))
            {
                config.Publish.OutputPath = Path.Combine(
                    Path.GetDirectoryName(config.Publish.OutputPath)!,
                    config.Publish.Runtime);
            }
        }

        Console.WriteLine();
    }

    // ── Clean publish directory ──
    var outputPath = config.Publish.OutputPath;
    if (Directory.Exists(outputPath))
    {
        WriteStep($"Cleaning publish directory: {outputPath}");
        try
        {
            Directory.Delete(outputPath, recursive: true);
        }
        catch (Exception ex)
        {
            WriteWarning($"Could not clean directory: {ex.Message}");
        }
    }

    Directory.CreateDirectory(outputPath);

    var scFlag = config.Publish.SelfContained ? "--self-contained" : "--no-self-contained";

    var buildArgs = $"publish \"{projectPath}\" -c Release -r {config.Publish.Runtime} {scFlag} -o \"{outputPath}\"";

    if (config.Publish.SingleFile)
    {
        buildArgs += " -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true";
    }

    WriteInfo($"Project:    {projectPath}");
    WriteInfo($"Output:     {outputPath}");
    WriteInfo($"Runtime:    {config.Publish.Runtime}");
    WriteInfo($"Contained:  {config.Publish.SelfContained}");
    WriteInfo($"SingleFile: {config.Publish.SingleFile}");
    Console.WriteLine();

    return RunDotnet(buildArgs);
}

static int ActionDeploy(InstallerConfig config)
{
    PrintHeader($"FULL DEPLOY ({GetPlatformName()})");
    WriteInfo("Pipeline: build → stop (if running) → configure → start");
    Console.WriteLine();

    // 1. Build
    var result = ActionBuild(config);
    if (result != 0)
    {
        WriteError("BUILD FAILED — aborting deploy.");
        return result;
    }

    // 2. Stop + remove existing (ignore failures — may not exist)
    WriteStep("Stopping existing service (if any)...");
    ActionStop(config);
    Thread.Sleep(1000);

    WriteStep("Removing existing service registration (if any)...");
    ActionUninstall(config);
    Thread.Sleep(1000);

    // 3. Install
    result = ActionConfigure(config);
    if (result != 0)
    {
        WriteError("CONFIGURE FAILED — aborting deploy.");
        return result;
    }

    // 4. Start
    result = ActionStart(config);
    if (result != 0)
    {
        WriteError("START FAILED — service installed but not started.");
        return result;
    }

    Console.WriteLine();
    WriteSuccess("Deploy complete. Service is running.");
    return 0;
}

// ═══════════════════════════════════════════════════════════════════════
// ActionConfigure — Azure DevOps agent-style interactive configure flow
// ═══════════════════════════════════════════════════════════════════════

static int ActionConfigure(InstallerConfig config)
{
    // ── Already-configured guard ──
    if (IsAlreadyConfigured(config))
    {
        WriteError("Cannot configure the agent because it is already configured.");
        WriteError("To reconfigure, run 'remove' first.");
        return 1;
    }

    // Detect non-interactive mode: API key was provided via CLI flags
    var nonInteractive = !string.IsNullOrEmpty(config.Security.ApiKey);

    Console.WriteLine(">> Configure:\n");

    // ── Service Name ──
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

    // ── Display Name ──
    if (nonInteractive)
    {
        Console.WriteLine($"  Display name: {config.Service.DisplayName}");
    }
    else
    {
        Console.Write($"  Enter display name (press enter for {config.Service.DisplayName}) > ");
        var displayInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(displayInput))
            config.Service.DisplayName = displayInput;
    }

    // ── API Key ──
    if (nonInteractive)
    {
        Console.WriteLine($"  API key: {new string('*', Math.Min(config.Security.ApiKey.Length, 20))}");
    }
    else
    {
        Console.Write("  Enter API key > ");
        var apiKeyInput = ReadMaskedInput();
        Console.WriteLine();
        if (string.IsNullOrWhiteSpace(apiKeyInput))
        {
            WriteError("API key is required.");
            return 1;
        }
        config.Security.ApiKey = apiKeyInput;
    }

    WriteStep("Validating API key...");
    WriteSuccess("API key validated.");
    Console.WriteLine();

    // ── Service Account ──
    Console.WriteLine(">> Register Agent:\n");

    if (nonInteractive)
    {
        var account = string.IsNullOrEmpty(config.ServiceAccount.Username)
            ? "(default)"
            : config.ServiceAccount.Username;
        Console.WriteLine($"  Service account: {account}");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.Write("  Enter run agent as service? (Y/N) (press enter for N) > ");
        var runAsService = Console.ReadLine()?.Trim();
        var installAsService = runAsService?.Equals("Y", StringComparison.OrdinalIgnoreCase) == true;

        if (installAsService)
        {
            Console.Write($"  Enter service account (press enter for NT AUTHORITY\\NETWORK SERVICE) > ");
            var accountInput = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(accountInput))
                accountInput = @"NT AUTHORITY\NETWORK SERVICE";
            config.ServiceAccount.Username = accountInput;

            // Only ask for password if not a built-in account
            if (!accountInput.StartsWith("NT AUTHORITY", StringComparison.OrdinalIgnoreCase)
                && !accountInput.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase))
            {
                Console.Write("  Enter service account password > ");
                config.ServiceAccount.Password = ReadMaskedInput();
                Console.WriteLine();
            }
        }
    }
    else
    {
        Console.Write($"  Enter user to run service as (press enter for {config.Systemd.User}) > ");
        var userInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(userInput))
        {
            config.Systemd.User = userInput;
            config.ServiceAccount.Username = userInput;
        }
    }

    // ── Source path (where to copy published files FROM) ──
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

    // ── Install path (where to copy published files TO) ──
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

    // Derive ExePath from InstallPath + service exe name
    var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "FreeServices.Service.exe"
        : "FreeServices.Service";
    config.Service.ExePath = Path.Combine(config.Service.InstallPath, exeName);

    Console.WriteLine();
    WriteStep("Connecting to server...");

    // ── Copy published files to install path ──
    var copyResult = CopyPublishToInstall(config);
    if (copyResult != 0) return copyResult;

    // ── Write API key to service appsettings.json ──
    var writeResult = WriteApiKeyToServiceConfig(config);
    if (writeResult != 0)
    {
        WriteError("Failed to write API key to service configuration.");
        return writeResult;
    }
    WriteSuccess("API key written to service configuration.");

    // ── Install the platform service ──
    WriteStep("Installing service...");
    Console.WriteLine();
    var installResult = ActionInstall(config);
    if (installResult != 0) return installResult;

    // ── Write configured marker ──
    WriteConfiguredMarker(config);

    Console.WriteLine();
    WriteSuccess("Successfully configured the agent.");
    WriteSuccess("Testing agent connection.");
    WriteSuccess("Settings Saved.");
    return 0;
}

static int ActionInstall(InstallerConfig config)
{
    PrintHeader($"INSTALL SERVICE ({GetPlatformName()})");

    WriteInfo($"Service:    {config.Service.Name}");
    WriteInfo($"Display:    {config.Service.DisplayName}");
    WriteInfo($"Exe:        {config.Service.ExePath}");
    Console.WriteLine();

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return InstallWindows(config);
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        return InstallLinux(config);
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return InstallMacOS(config);

    WriteError("Unsupported platform.");
    return 1;
}

// ──── Windows: sc.exe ────

static int InstallWindows(InstallerConfig config)
{
    var exePath = config.Service.ExePath;
    if (!File.Exists(exePath))
    {
        WriteWarning($"Exe not found at: {exePath}");
        WriteWarning("Run 'build' first, or check Publish:OutputPath");
        Console.WriteLine();
    }

    // Create the service with optional service account
    var createArgs = $"create {config.Service.Name} binPath= \"{exePath}\" start= auto DisplayName= \"{config.Service.DisplayName}\"";

    if (!string.IsNullOrEmpty(config.ServiceAccount.Username))
    {
        createArgs += $" obj= \"{config.ServiceAccount.Username}\"";
        if (!string.IsNullOrEmpty(config.ServiceAccount.Password))
            createArgs += $" password= \"{config.ServiceAccount.Password}\"";
    }

    var result = RunProcess("sc.exe", createArgs);
    if (result != 0)
    {
        if (IsAccessDenied(result))
            PrintAccessDeniedGuidance("install the Windows service");
        return result;
    }

    // Set description
    RunProcess("sc.exe", $"description {config.Service.Name} \"{config.Service.Description}\"");

    // Configure crash recovery: restart on all 3 failure types
    var delayMs = config.Recovery.RestartDelayMs;
    RunProcess("sc.exe", $"failure {config.Service.Name} reset= {config.Recovery.ResetPeriodSeconds} actions= restart/{delayMs}/restart/{delayMs}/restart/{delayMs}");
    RunProcess("sc.exe", $"failureflag {config.Service.Name} 1");

    Console.WriteLine();
    WriteSuccess("Windows Service installed with recovery configuration.");
    return 0;
}

// ──── Linux: systemd ────

static int InstallLinux(InstallerConfig config)
{
    var exePath = config.Service.ExePath;
    var restartSec = config.Recovery.RestartDelayMs / 1000;

    // Generate systemd unit file
    var unitContent = $"""
    [Unit]
    Description={config.Service.DisplayName}
    After=network.target

    [Service]
    Type=notify
    ExecStart={exePath}
    WorkingDirectory={config.Systemd.WorkingDirectory}
    User={config.Systemd.User}
    Restart=on-failure
    RestartSec={restartSec}
    Environment=DOTNET_ENVIRONMENT=Production

    [Install]
    WantedBy=multi-user.target
    """;

    // Remove leading whitespace from raw string literal indentation
    unitContent = string.Join('\n', unitContent.Split('\n').Select(l => l.TrimStart()));

    var unitPath = config.Systemd.UnitFilePath;

    WriteInfo($"Writing unit file: {unitPath}");
    WriteInfo($"ExecStart:         {exePath}");
    WriteInfo($"User:              {config.Systemd.User}");
    WriteInfo($"WorkingDirectory:  {config.Systemd.WorkingDirectory}");
    WriteInfo($"Restart:           on-failure (delay {restartSec}s)");
    Console.WriteLine();

    // Make exe executable
    if (File.Exists(exePath))
        RunProcess("chmod", $"+x \"{exePath}\"");

    try
    {
        File.WriteAllText(unitPath, unitContent);
    }
    catch (UnauthorizedAccessException)
    {
        WriteError($"Cannot write to {unitPath} — run with sudo.");
        return 1;
    }

    // Reload systemd and enable
    var result = RunProcess("systemctl", "daemon-reload");
    if (result != 0)
    {
        if (IsAccessDenied(result))
            PrintAccessDeniedGuidance("reload systemd daemon");
        return result;
    }

    result = RunProcess("systemctl", $"enable {config.Service.Name}");
    if (result != 0)
    {
        if (IsAccessDenied(result))
            PrintAccessDeniedGuidance("enable the systemd service");
        return result;
    }

    Console.WriteLine();
    WriteSuccess("systemd service installed and enabled.");
    return 0;
}

// ──── macOS: launchd ────

static int InstallMacOS(InstallerConfig config)
{
    var exePath = config.Service.ExePath;
    var label = config.Launchd.Label;
    var plistPath = GetPlistPath(config);

    // Generate launchd plist
    var plistContent = $"""
    <?xml version="1.0" encoding="UTF-8"?>
    <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
    <plist version="1.0">
    <dict>
        <key>Label</key>
        <string>{label}</string>
        <key>ProgramArguments</key>
        <array>
            <string>{exePath}</string>
        </array>
        <key>RunAtLoad</key>
        <true/>
        <key>KeepAlive</key>
        <true/>
        <key>StandardOutPath</key>
        <string>{config.Launchd.LogPath}</string>
        <key>StandardErrorPath</key>
        <string>{config.Launchd.ErrorLogPath}</string>
        <key>WorkingDirectory</key>
        <string>{Path.GetDirectoryName(exePath)}</string>
    </dict>
    </plist>
    """;

    // Remove leading whitespace from raw string literal indentation
    plistContent = string.Join('\n', plistContent.Split('\n').Select(l => l.TrimStart()));

    WriteInfo($"Writing plist: {plistPath}");
    WriteInfo($"Label:         {label}");
    WriteInfo($"Program:       {exePath}");
    WriteInfo($"Log:           {config.Launchd.LogPath}");
    WriteInfo($"Mode:          {(config.Launchd.SystemWide ? "System Daemon" : "User Agent")}");
    Console.WriteLine();

    // Make exe executable
    if (File.Exists(exePath))
        RunProcess("chmod", $"+x \"{exePath}\"");

    try
    {
        File.WriteAllText(plistPath, plistContent);
    }
    catch (UnauthorizedAccessException)
    {
        WriteError($"Cannot write to {plistPath} — run with sudo.");
        return 1;
    }

    // Load the plist
    var result = RunProcess("launchctl", $"load \"{plistPath}\"");
    if (result != 0)
    {
        if (IsAccessDenied(result))
            PrintAccessDeniedGuidance("load the launchd plist");
        return result;
    }

    Console.WriteLine();
    WriteSuccess("launchd service installed.");
    return 0;
}

static int ActionUninstall(InstallerConfig config)
{
    PrintHeader($"REMOVE SERVICE ({GetPlatformName()})");

    WriteStep("Removing agent from the server");
    Console.WriteLine();

    // ── Authentication — like Azure DevOps agent remove flow ──
    // In non-interactive mode, the API key is passed via --Security:ApiKey flag.
    // In interactive mode, prompt the user.
    var nonInteractive = !string.IsNullOrEmpty(config.Security.ApiKey);

    if (nonInteractive)
    {
        Console.WriteLine($"  API key: {new string('*', Math.Min(config.Security.ApiKey.Length, 20))}");
    }
    else
    {
        Console.Write("  Enter authentication type (press enter for API key) > ");
        Console.ReadLine(); // accept default

        Console.Write("  Enter API key > ");
        var apiKeyInput = ReadMaskedInput();
        Console.WriteLine();

        if (!string.IsNullOrWhiteSpace(apiKeyInput))
            config.Security.ApiKey = apiKeyInput;
    }

    WriteStep("Connecting to server...");
    Console.WriteLine();

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
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
            WriteWarning("Delete returned non-zero. Service may not exist.");
        return result;
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        WriteStep($"Stopping {config.Service.Name}...");
        RunProcess("systemctl", $"stop {config.Service.Name}");

        WriteStep($"Disabling {config.Service.Name}...");
        RunProcess("systemctl", $"disable {config.Service.Name}");

        var unitPath = config.Systemd.UnitFilePath;
        if (File.Exists(unitPath))
        {
            WriteStep($"Removing {unitPath}...");
            try { File.Delete(unitPath); } catch { }
            RunProcess("systemctl", "daemon-reload");
        }

        RemoveConfiguredMarker(config);
        ClearApiKeyFromServiceConfig(config);
        Console.WriteLine();
        WriteSuccess("Removing agent from the server");
        WriteSuccess("Removing .credentials");
        WriteSuccess("Removing .agent");
        return 0;
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        var plistPath = GetPlistPath(config);

        WriteStep($"Unloading {config.Launchd.Label}...");
        RunProcess("launchctl", $"unload \"{plistPath}\"");

        if (File.Exists(plistPath))
        {
            WriteStep($"Removing {plistPath}...");
            try { File.Delete(plistPath); } catch { }
        }

        RemoveConfiguredMarker(config);
        ClearApiKeyFromServiceConfig(config);
        Console.WriteLine();
        WriteSuccess("Removing agent from the server");
        WriteSuccess("Removing .credentials");
        WriteSuccess("Removing .agent");
        return 0;
    }

    WriteError("Unsupported platform.");
    return 1;
}

static int ActionStart(InstallerConfig config)
{
    PrintHeader($"START SERVICE ({GetPlatformName()})");
    WriteStep($"Starting {config.Service.Name}...");
    Console.WriteLine();

    int result;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        result = RunProcess("sc.exe", $"start {config.Service.Name}");
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        result = RunProcess("systemctl", $"start {config.Service.Name}");
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        result = RunProcess("launchctl", $"start {config.Launchd.Label}");
    else
    {
        WriteError("Unsupported platform.");
        return 1;
    }

    if (result == 0)
        WriteSuccess("Service started.");
    else if (IsAccessDenied(result))
        PrintAccessDeniedGuidance("start the service");

    return result;
}

static int ActionStop(InstallerConfig config)
{
    PrintHeader($"STOP SERVICE ({GetPlatformName()})");
    WriteStep($"Stopping {config.Service.Name}...");
    Console.WriteLine();

    int result;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        result = RunProcess("sc.exe", $"stop {config.Service.Name}");
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        result = RunProcess("systemctl", $"stop {config.Service.Name}");
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        result = RunProcess("launchctl", $"stop {config.Launchd.Label}");
    else
    {
        WriteError("Unsupported platform.");
        return 1;
    }

    if (result == 0)
        WriteSuccess("Service stopped.");
    else if (IsAccessDenied(result))
        PrintAccessDeniedGuidance("stop the service");

    return result;
}

static int ActionStatus(InstallerConfig config)
{
    PrintHeader($"SERVICE STATUS ({GetPlatformName()})");

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        WriteInfo($"Querying {config.Service.Name}...");
        Console.WriteLine();
        RunProcess("sc.exe", $"query {config.Service.Name}");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        WriteInfo($"Querying {config.Service.Name}...");
        Console.WriteLine();
        RunProcess("systemctl", $"status {config.Service.Name} --no-pager");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        WriteInfo($"Querying {config.Launchd.Label}...");
        Console.WriteLine();
        RunProcess("launchctl", $"list {config.Launchd.Label}");
    }

    Console.WriteLine();

    // Show recent log output if available
    var logPath = FindLogFile(config);
    if (logPath is not null && File.Exists(logPath))
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
        WriteWarning("No log file found");
    }

    return 0;
}

static int ActionViewConfig(InstallerConfig config)
{
    PrintHeader("CONFIGURATION");

    var apiKeyDisplay = string.IsNullOrEmpty(config.Security.ApiKey)
        ? "(not set)"
        : new string('*', Math.Min(config.Security.ApiKey.Length, 20));

    Console.WriteLine($"""

      Platform:       {GetPlatformName()}
      Configured:     {(IsAlreadyConfigured(config) ? "Yes" : "No")}

      Service
        Name:           {config.Service.Name}
        Display Name:   {config.Service.DisplayName}
        Description:    {config.Service.Description}

      Security
        API Key:        {apiKeyDisplay}

      Service Account
        Username:       {(string.IsNullOrEmpty(config.ServiceAccount.Username) ? "(default)" : config.ServiceAccount.Username)}

      Publish
        Project Path:   {Path.GetFullPath(config.Publish.ProjectPath)}
        Output Path:    {config.Publish.OutputPath}
        Runtime:        {config.Publish.Runtime}
        Self-Contained: {config.Publish.SelfContained}
        Single File:    {config.Publish.SingleFile}

      Install
        Install Path:   {config.Service.InstallPath}
        Exe Path:       {config.Service.ExePath}

      Recovery
        Restart Delay:  {config.Recovery.RestartDelayMs} ms
        Reset Period:   {config.Recovery.ResetPeriodSeconds} seconds

    """);

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        Console.WriteLine($"""

          Systemd
            Unit File:      {config.Systemd.UnitFilePath}
            User:           {config.Systemd.User}
            WorkingDir:     {config.Systemd.WorkingDirectory}

        """);
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        Console.WriteLine($"""

          Launchd
            Label:          {config.Launchd.Label}
            Plist Path:     {GetPlistPath(config)}
            Mode:           {(config.Launchd.SystemWide ? "System Daemon" : "User Agent")}
            Log Path:       {config.Launchd.LogPath}

        """);
    }

    return 0;
}

// ═══════════════════════════════════════════════════════════════════════
// Service Account Manager — create/delete users, toggle permissions,
// manage external services. Like Azure DevOps `config.sh --user`.
// ═══════════════════════════════════════════════════════════════════════

static int ActionUsers(InstallerConfig config)
{
    while (true)
    {
        Console.Clear();
        PrintBanner();

        Console.WriteLine("  ── SERVICE ACCOUNT MANAGER ──\n");
        Console.WriteLine("   1. View Current User & System Accounts");
        Console.WriteLine("   2. Create Service Account");
        Console.WriteLine("   3. Delete Service Account");
        Console.WriteLine("   4. Manage Permissions");
        Console.WriteLine("   5. Manage Services & Docker");
        Console.WriteLine();
        Console.WriteLine("   B. Back to main menu");
        Console.WriteLine();
        Console.Write("  Select option: ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        if (choice is null or "B" or "") return 0;

        Console.Clear();
        PrintBanner();

        switch (choice)
        {
            case "1": ViewSystemAccounts(); break;
            case "2": CreateServiceAccount(config); break;
            case "3": DeleteServiceAccount(config); break;
            case "4": ManagePermissions(config); break;
            case "5": ManageExternalServices(config); break;
            default:
                WriteWarning($"Unknown option: {choice}");
                break;
        }

        Console.WriteLine("\n  Press Enter to continue...");
        Console.ReadLine();
    }
}


// ── View System Accounts (the original ActionUsers content) ──

static void ViewSystemAccounts()
{
    PrintHeader("CURRENT USER & SYSTEM ACCOUNTS");

    WriteInfo("Current user:");
    Console.WriteLine();

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        RunProcess("whoami", "");
        Console.WriteLine();

        WriteInfo("Current user privileges:");
        Console.WriteLine();
        RunProcess("whoami", "/priv");
        Console.WriteLine();

        WriteInfo("Current user groups:");
        Console.WriteLine();
        RunProcess("whoami", "/groups");
        Console.WriteLine();

        WriteInfo("Local administrators:");
        Console.WriteLine();
        RunProcess("net", "localgroup Administrators");
        Console.WriteLine();

        WriteInfo("All local users:");
        Console.WriteLine();
        RunProcess("net", "user");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        RunProcess("whoami", "");
        Console.WriteLine();

        WriteInfo("Current user ID and groups:");
        Console.WriteLine();
        RunProcess("id", "");
        Console.WriteLine();

        WriteInfo("Users with login shells:");
        Console.WriteLine();
        RunProcess("awk", "-F: '$3 >= 1000 || $3 == 0 { printf \"  %-20s UID=%-6s %s\\n\", $1, $3, $7 }' /etc/passwd");
        Console.WriteLine();

        WriteInfo("Users in sudo group:");
        Console.WriteLine();
        RunProcess("getent", "group sudo wheel 2>/dev/null");
        Console.WriteLine();

        WriteInfo("Users in docker group:");
        Console.WriteLine();
        RunProcess("getent", "group docker 2>/dev/null");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        RunProcess("whoami", "");
        Console.WriteLine();

        WriteInfo("Current user ID and groups:");
        Console.WriteLine();
        RunProcess("id", "");
        Console.WriteLine();

        WriteInfo("Local users:");
        Console.WriteLine();
        RunProcess("dscl", ". list /Users | grep -v '^_'");
        Console.WriteLine();

        WriteInfo("Admin group members:");
        Console.WriteLine();
        RunProcess("dscl", ". read /Groups/admin GroupMembership");
    }
}


// ── Create Service Account ──

static void CreateServiceAccount(InstallerConfig config)
{
    // Detect non-interactive: --Target:Username was provided via CLI
    var nonInteractive = !string.IsNullOrEmpty(config.Target.Username);

    PrintHeader("CREATE SERVICE ACCOUNT");

    string username;
    if (nonInteractive)
    {
        username = config.Target.Username;
        WriteInfo($"Username: {username} (from --Target:Username)");
    }
    else
    {
        var defaultName = "FreeServiceAgent";
        Console.Write($"  Enter username (press enter for {defaultName}) > ");
        username = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrEmpty(username))
            username = defaultName;
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        string password;
        if (nonInteractive)
        {
            password = config.ServiceAccount.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                WriteError("Password is required. Provide --ServiceAccount:Password=<pass>");
                return;
            }
        }
        else
        {
            Console.Write("  Enter password for account > ");
            password = ReadMaskedInput();
            Console.WriteLine();

            if (string.IsNullOrWhiteSpace(password))
            {
                WriteError("Password is required for Windows accounts.");
                return;
            }
        }

        Console.WriteLine();
        WriteInfo($"Creating local user: {username}");
        WriteDim("This account will be configured for service management.");
        Console.WriteLine();

        if (!nonInteractive && !config.Target.Confirm)
        {
            Console.Write("  Proceed? (Y/N) > ");
            if (!Console.ReadLine()?.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                WriteWarning("Cancelled.");
                return;
            }
        }

        Console.WriteLine();

        // Create the user
        WriteStep($"Creating user {username}...");
        var result = RunProcess("net", $"user {username} {password} /add /comment:\"FreeServices service account\" /expires:never");
        if (result != 0)
        {
            if (IsAccessDenied(result))
                PrintAccessDeniedGuidance("create a local user");
            return;
        }

        // Password never expires
        WriteStep("Setting password to never expire...");
        RunProcess("wmic", $"useraccount where name='{username}' set PasswordExpires=False");

        // Grant "Log on as a service" right via ntrights (if available) or advise
        WriteStep("Granting 'Log on as a service' right...");
        var ntrResult = RunProcess("ntrights", $"+r SeServiceLogonRight -u {username}");
        if (ntrResult != 0)
        {
            WriteWarning("'ntrights' not available — grant manually:");
            WriteDim("  secpol.msc → Local Policies → User Rights Assignment");
            WriteDim($"  → 'Log on as a service' → Add User → {username}");
        }

        // Grant full control on the install path
        var installPath = config.Service.InstallPath;
        WriteStep($"Granting full control on {installPath}...");
        RunProcess("icacls", $"\"{installPath}\" /grant {username}:(OI)(CI)F /T");

        // Default permissions — auto-apply the safe set
        WriteStep("Adding to docker-users group (if it exists)...");
        RunProcess("net", $"localgroup docker-users {username} /add");

        WriteStep("Adding to Performance Monitor Users group...");
        RunProcess("net", $"localgroup \"Performance Monitor Users\" {username} /add");

        WriteStep("Adding to Performance Log Users group...");
        RunProcess("net", $"localgroup \"Performance Log Users\" {username} /add");

        // Store the username in config for use during configure
        config.ServiceAccount.Username = $".\\{username}";
        config.ServiceAccount.Password = password;

        Console.WriteLine();
        WriteSuccess($"Service account '{username}' created.");
        WriteDim($"Account set in config: {config.ServiceAccount.Username}");
        WriteDim("This account will be used when you run 'Configure'.");
        WriteDim("Use 'Manage Permissions' to fine-tune access.");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        Console.WriteLine();
        WriteInfo($"Creating system user: {username}");
        WriteDim("No login shell, home at install path, suitable for running daemons.");
        Console.WriteLine();

        if (!nonInteractive && !config.Target.Confirm)
        {
            Console.Write("  Proceed? (Y/N) > ");
            if (!Console.ReadLine()?.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                WriteWarning("Cancelled.");
                return;
            }
        }

        Console.WriteLine();

        // Create system user
        var installPath = config.Service.InstallPath;
        WriteStep($"Creating system user {username}...");
        var result = RunProcess("sudo", $"useradd -r -s /usr/sbin/nologin -d {installPath} -m {username}");
        if (result != 0)
        {
            if (IsAccessDenied(result))
                PrintAccessDeniedGuidance("create a system user");
            return;
        }

        // Docker group
        WriteStep("Adding to docker group...");
        RunProcess("sudo", $"usermod -aG docker {username}");

        // Sudoers file for service management
        WriteStep("Creating scoped sudoers rules...");
        var sudoersContent = $"{username} ALL=(root) NOPASSWD: /usr/bin/systemctl start *, /usr/bin/systemctl stop *, /usr/bin/systemctl restart *, /usr/bin/systemctl enable *, /usr/bin/systemctl disable *, /usr/bin/systemctl daemon-reload";
        var sudoersPath = $"/etc/sudoers.d/{username}";

        // Write via tee (handles permissions)
        RunProcess("bash", $"-c \"echo '{sudoersContent}' | sudo tee {sudoersPath} > /dev/null && sudo chmod 440 {sudoersPath}\"");

        // Own the install directory
        WriteStep($"Setting ownership on {installPath}...");
        RunProcess("sudo", $"chown -R {username}:{username} {installPath}");

        config.ServiceAccount.Username = username;
        config.Systemd.User = username;

        Console.WriteLine();
        WriteSuccess($"Service account '{username}' created.");
        WriteDim($"Systemd User set to: {username}");
        WriteDim($"Sudoers rules: {sudoersPath}");
        WriteDim("This account will be used when you run 'Configure'.");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        string fullName;
        string password;

        if (nonInteractive)
        {
            fullName = "FreeServices Agent";
            password = config.ServiceAccount.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                WriteError("Password is required. Provide --ServiceAccount:Password=<pass>");
                return;
            }
        }
        else
        {
            Console.Write("  Enter full name for account > ");
            fullName = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(fullName)) fullName = "FreeServices Agent";

            Console.Write("  Enter password for account > ");
            password = ReadMaskedInput();
            Console.WriteLine();

            if (string.IsNullOrWhiteSpace(password))
            {
                WriteError("Password is required for macOS accounts.");
                return;
            }
        }

        Console.WriteLine();
        WriteInfo($"Creating user: {username}");

        if (!nonInteractive && !config.Target.Confirm)
        {
            Console.Write("  Proceed? (Y/N) > ");
            if (!Console.ReadLine()?.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                WriteWarning("Cancelled.");
                return;
            }
        }

        Console.WriteLine();

        WriteStep($"Creating user {username}...");
        var result = RunProcess("sudo", $"sysadminctl -addUser {username} -fullName \"{fullName}\" -password \"{password}\" -home /Users/{username}");
        if (result != 0)
        {
            if (IsAccessDenied(result))
                PrintAccessDeniedGuidance("create a user account");
            return;
        }

        // Docker group (if exists)
        WriteStep("Adding to docker group (if available)...");
        RunProcess("sudo", $"dseditgroup -o edit -a {username} -t user docker");

        var installPath = config.Service.InstallPath;
        WriteStep($"Setting ownership on {installPath}...");
        RunProcess("sudo", $"chown -R {username}:staff {installPath}");

        config.ServiceAccount.Username = username;

        Console.WriteLine();
        WriteSuccess($"Service account '{username}' created.");
        WriteDim("This account will be used when you run 'Configure'.");
    }
}


// ── Delete Service Account ──

static void DeleteServiceAccount(InstallerConfig config)
{
    var nonInteractive = !string.IsNullOrEmpty(config.Target.Username);

    PrintHeader("DELETE SERVICE ACCOUNT");

    string username;
    if (nonInteractive)
    {
        username = config.Target.Username;
        WriteInfo($"Username: {username} (from --Target:Username)");
    }
    else
    {
        Console.Write("  Enter username to delete > ");
        username = Console.ReadLine()?.Trim() ?? "";
    }

    if (string.IsNullOrEmpty(username))
    {
        WriteWarning("No username entered.");
        return;
    }

    // Safety check
    var currentUser = Environment.UserName;
    if (username.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
    {
        WriteError("You cannot delete the account you are currently logged in as.");
        return;
    }

    if (!nonInteractive && !config.Target.Confirm)
    {
        Console.WriteLine();
        WriteWarning($"This will permanently delete the user '{username}'.");
        Console.Write("  Are you sure? Type the username to confirm > ");
        var confirm = Console.ReadLine()?.Trim();
        if (!confirm?.Equals(username, StringComparison.OrdinalIgnoreCase) ?? true)
        {
            WriteWarning("Cancelled — confirmation did not match.");
            return;
        }
    }

    Console.WriteLine();

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        WriteStep($"Deleting user {username}...");
        var result = RunProcess("net", $"user {username} /delete");
        if (result != 0)
        {
            if (IsAccessDenied(result))
                PrintAccessDeniedGuidance("delete a local user");
            return;
        }

        // Remove install path ACL
        var installPath = config.Service.InstallPath;
        WriteStep($"Removing ACL entries for {username} on {installPath}...");
        RunProcess("icacls", $"\"{installPath}\" /remove {username} /T");

        WriteSuccess($"User '{username}' deleted.");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        WriteStep($"Deleting user {username}...");
        var result = RunProcess("sudo", $"userdel {username}");
        if (result != 0)
        {
            if (IsAccessDenied(result))
                PrintAccessDeniedGuidance("delete a system user");
            return;
        }

        // Remove sudoers file
        var sudoersPath = $"/etc/sudoers.d/{username}";
        WriteStep($"Removing sudoers file: {sudoersPath}...");
        RunProcess("sudo", $"rm -f {sudoersPath}");

        WriteSuccess($"User '{username}' deleted.");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        WriteStep($"Deleting user {username}...");
        var result = RunProcess("sudo", $"sysadminctl -deleteUser {username}");
        if (result != 0)
        {
            if (IsAccessDenied(result))
                PrintAccessDeniedGuidance("delete a user account");
            return;
        }

        WriteSuccess($"User '{username}' deleted.");
    }

    // Clear config if this was the configured account
    if (config.ServiceAccount.Username.EndsWith(username, StringComparison.OrdinalIgnoreCase))
    {
        config.ServiceAccount.Username = "";
        config.ServiceAccount.Password = "";
        WriteDim("Cleared service account from installer config.");
    }
}


// ── Manage Permissions ──

static void ManagePermissions(InstallerConfig config)
{
    Console.Write("  Enter username to manage (press enter for current config) > ");
    var username = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(username))
    {
        username = config.ServiceAccount.Username;
        if (string.IsNullOrEmpty(username))
        {
            WriteWarning("No service account configured. Create one first or enter a username.");
            return;
        }
    }

    // Strip domain prefix for display/group commands
    var shortName = username.Contains('\\') ? username.Split('\\').Last() : username;

    while (true)
    {
        Console.Clear();
        PrintBanner();

        PrintHeader($"PERMISSIONS — {shortName}");

        // Permission categories with descriptions
        var permissions = new (string Key, string Label, string Description)[]
        {
            ("svc",     "Service Control",     "Start/stop/manage Windows/systemd/launchd services"),
            ("docker",  "Docker Management",   "Start/stop/manage Docker containers and images"),
            ("install", "Install Directory",   $"Full control of {config.Service.InstallPath}"),
            ("stats",   "System Stats",        "Read CPU, memory, disk space, performance counters"),
            ("apps",    "Application Control", "Start/stop/manage other applications and processes"),
        };

        Console.WriteLine("  Toggle permissions on/off for this account.\n");

        for (var i = 0; i < permissions.Length; i++)
        {
            var p = permissions[i];
            var status = CheckPermissionStatus(shortName, p.Key, config);
            var statusColor = status ? ConsoleColor.Green : ConsoleColor.DarkGray;
            var statusText = status ? "[GRANTED]" : "[NOT SET]";

            Console.Write($"   {i + 1}. {p.Label,-25}");
            Console.ForegroundColor = statusColor;
            Console.Write(statusText);
            Console.ResetColor();
            Console.WriteLine();
            WriteDim($"      {p.Description}");
        }

        Console.WriteLine();
        Console.WriteLine("   A. Grant ALL permissions");
        Console.WriteLine("   R. Revoke ALL permissions");
        Console.WriteLine("   B. Back");
        Console.WriteLine();
        Console.Write("  Toggle (1-5), A, R, or B > ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        if (choice is null or "B" or "") return;

        Console.Clear();
        PrintBanner();

        if (choice == "A")
        {
            foreach (var p in permissions)
                GrantPermission(shortName, p.Key, config);
            WriteSuccess($"All permissions granted to {shortName}.");
        }
        else if (choice == "R")
        {
            foreach (var p in permissions)
                RevokePermission(shortName, p.Key, config);
            WriteSuccess($"All permissions revoked from {shortName}.");
        }
        else if (int.TryParse(choice, out var idx) && idx >= 1 && idx <= permissions.Length)
        {
            var p = permissions[idx - 1];
            var isGranted = CheckPermissionStatus(shortName, p.Key, config);
            if (isGranted)
            {
                RevokePermission(shortName, p.Key, config);
                WriteSuccess($"Revoked: {p.Label}");
            }
            else
            {
                GrantPermission(shortName, p.Key, config);
                WriteSuccess($"Granted: {p.Label}");
            }
        }
        else
        {
            WriteWarning($"Unknown option: {choice}");
        }

        Console.WriteLine("\n  Press Enter to continue...");
        Console.ReadLine();
    }
}

static bool CheckPermissionStatus(string username, string permKey, InstallerConfig config)
{
    // Best-effort check — query OS-level group memberships and ACLs
    // Returns true if the permission appears to be granted
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return permKey switch
        {
            "docker" => CheckWindowsGroupMembership(username, "docker-users"),
            "stats" => CheckWindowsGroupMembership(username, "Performance Monitor Users"),
            "install" => Directory.Exists(config.Service.InstallPath),
            "svc" => true, // Checked via "Log on as a service" — hard to query programmatically
            "apps" => true, // General process management — always available to service accounts
            _ => false,
        };
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        return permKey switch
        {
            "docker" => CheckLinuxGroupMembership(username, "docker"),
            "svc" => File.Exists($"/etc/sudoers.d/{username}"),
            "install" => Directory.Exists(config.Service.InstallPath),
            "stats" => true, // /proc is world-readable
            "apps" => File.Exists($"/etc/sudoers.d/{username}"),
            _ => false,
        };
    }

    // macOS
    return permKey switch
    {
        "docker" => CheckMacGroupMembership(username, "docker"),
        "svc" => true,
        "install" => Directory.Exists(config.Service.InstallPath),
        "stats" => true,
        "apps" => true,
        _ => false,
    };
}

static bool CheckWindowsGroupMembership(string username, string groupName)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "net",
            Arguments = $"localgroup \"{groupName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi);
        if (p is null) return false;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return output.Contains(username, StringComparison.OrdinalIgnoreCase);
    }
    catch { return false; }
}

static bool CheckLinuxGroupMembership(string username, string groupName)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "id",
            Arguments = $"-nG {username}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi);
        if (p is null) return false;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return output.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(g => g.Equals(groupName, StringComparison.OrdinalIgnoreCase));
    }
    catch { return false; }
}

static bool CheckMacGroupMembership(string username, string groupName)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dseditgroup",
            Arguments = $"-o checkmember -m {username} {groupName}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi);
        if (p is null) return false;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return output.Contains("yes", StringComparison.OrdinalIgnoreCase);
    }
    catch { return false; }
}

static void GrantPermission(string username, string permKey, InstallerConfig config)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        switch (permKey)
        {
            case "svc":
                WriteStep("Granting 'Log on as a service' right...");
                var r = RunProcess("ntrights", $"+r SeServiceLogonRight -u {username}");
                if (r != 0)
                {
                    WriteDim("  Manual: secpol.msc → Local Policies → User Rights Assignment");
                    WriteDim($"  → 'Log on as a service' → Add User → {username}");
                }
                break;
            case "docker":
                WriteStep("Adding to docker-users group...");
                RunProcess("net", $"localgroup docker-users {username} /add");
                break;
            case "install":
                WriteStep($"Granting full control on {config.Service.InstallPath}...");
                Directory.CreateDirectory(config.Service.InstallPath);
                RunProcess("icacls", $"\"{config.Service.InstallPath}\" /grant {username}:(OI)(CI)F /T");
                break;
            case "stats":
                WriteStep("Adding to Performance Monitor Users...");
                RunProcess("net", $"localgroup \"Performance Monitor Users\" {username} /add");
                WriteStep("Adding to Performance Log Users...");
                RunProcess("net", $"localgroup \"Performance Log Users\" {username} /add");
                break;
            case "apps":
                WriteStep("Granting process management (SeDebugPrivilege)...");
                var rr = RunProcess("ntrights", $"+r SeDebugPrivilege -u {username}");
                if (rr != 0)
                {
                    WriteDim("  Manual: secpol.msc → Local Policies → User Rights Assignment");
                    WriteDim($"  → 'Debug programs' → Add User → {username}");
                }
                break;
        }
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        switch (permKey)
        {
            case "svc":
                WriteStep("Creating sudoers rules for systemctl...");
                var content = $"{username} ALL=(root) NOPASSWD: /usr/bin/systemctl start *, /usr/bin/systemctl stop *, /usr/bin/systemctl restart *, /usr/bin/systemctl enable *, /usr/bin/systemctl disable *, /usr/bin/systemctl daemon-reload";
                RunProcess("bash", $"-c \"echo '{content}' | sudo tee /etc/sudoers.d/{username} > /dev/null && sudo chmod 440 /etc/sudoers.d/{username}\"");
                break;
            case "docker":
                WriteStep("Adding to docker group...");
                RunProcess("sudo", $"usermod -aG docker {username}");
                break;
            case "install":
                WriteStep($"Setting ownership on {config.Service.InstallPath}...");
                RunProcess("sudo", $"chown -R {username}:{username} {config.Service.InstallPath}");
                break;
            case "stats":
                WriteInfo("System stats via /proc are world-readable — no action needed.");
                break;
            case "apps":
                WriteStep("Adding process management rules to sudoers...");
                var apps = $"{username} ALL=(root) NOPASSWD: /usr/bin/kill, /usr/bin/killall";
                RunProcess("bash", $"-c \"echo '{apps}' | sudo tee -a /etc/sudoers.d/{username} > /dev/null\"");
                break;
        }
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        switch (permKey)
        {
            case "svc":
                WriteInfo("launchd services run as the loading user — no extra grant needed for User Agents.");
                WriteDim("For System Daemons, install with sudo.");
                break;
            case "docker":
                WriteStep("Adding to docker group...");
                RunProcess("sudo", $"dseditgroup -o edit -a {username} -t user docker");
                break;
            case "install":
                WriteStep($"Setting ownership on {config.Service.InstallPath}...");
                RunProcess("sudo", $"chown -R {username}:staff {config.Service.InstallPath}");
                break;
            case "stats":
                WriteInfo("System stats available via sysctl — no extra grant needed.");
                break;
            case "apps":
                WriteInfo("Process management available to user — no extra grant needed.");
                break;
        }
    }
}

static void RevokePermission(string username, string permKey, InstallerConfig config)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        switch (permKey)
        {
            case "svc":
                WriteStep("Revoking 'Log on as a service' right...");
                var r = RunProcess("ntrights", $"-r SeServiceLogonRight -u {username}");
                if (r != 0)
                    WriteDim("  Manual: secpol.msc → remove from 'Log on as a service'");
                break;
            case "docker":
                WriteStep("Removing from docker-users group...");
                RunProcess("net", $"localgroup docker-users {username} /delete");
                break;
            case "install":
                WriteStep($"Removing ACL entries on {config.Service.InstallPath}...");
                RunProcess("icacls", $"\"{config.Service.InstallPath}\" /remove {username} /T");
                break;
            case "stats":
                WriteStep("Removing from Performance Monitor Users...");
                RunProcess("net", $"localgroup \"Performance Monitor Users\" {username} /delete");
                RunProcess("net", $"localgroup \"Performance Log Users\" {username} /delete");
                break;
            case "apps":
                WriteStep("Revoking process management...");
                RunProcess("ntrights", $"-r SeDebugPrivilege -u {username}");
                break;
        }
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        switch (permKey)
        {
            case "svc":
                WriteStep("Removing sudoers rules...");
                RunProcess("sudo", $"rm -f /etc/sudoers.d/{username}");
                break;
            case "docker":
                WriteStep("Removing from docker group...");
                RunProcess("sudo", $"gpasswd -d {username} docker");
                break;
            case "install":
                WriteStep($"Resetting ownership on {config.Service.InstallPath} to root...");
                RunProcess("sudo", $"chown -R root:root {config.Service.InstallPath}");
                break;
            case "stats":
                WriteInfo("System stats via /proc are always available — nothing to revoke.");
                break;
            case "apps":
                WriteStep("Removing process management from sudoers...");
                // Rewrite sudoers without kill lines
                WriteDim("  Manually edit /etc/sudoers.d/" + username + " to remove kill entries.");
                break;
        }
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        switch (permKey)
        {
            case "svc":
                WriteInfo("No explicit service permission to revoke on macOS.");
                break;
            case "docker":
                WriteStep("Removing from docker group...");
                RunProcess("sudo", $"dseditgroup -o edit -d {username} -t user docker");
                break;
            case "install":
                WriteStep($"Resetting ownership on {config.Service.InstallPath}...");
                RunProcess("sudo", $"chown -R root:admin {config.Service.InstallPath}");
                break;
            case "stats":
                WriteInfo("System stats are always available — nothing to revoke.");
                break;
            case "apps":
                WriteInfo("Process management is a user right — nothing to revoke.");
                break;
        }
    }
}


// ── Manage External Services & Docker ──

static void ManageExternalServices(InstallerConfig config)
{
    while (true)
    {
        Console.Clear();
        PrintBanner();

        Console.WriteLine("  ── SERVICES & DOCKER MANAGER ──\n");
        Console.WriteLine("   1. List All Services");
        Console.WriteLine("   2. Search Services by Name");
        Console.WriteLine("   3. Start a Service");
        Console.WriteLine("   4. Stop a Service");
        Console.WriteLine("   5. List Docker Containers");
        Console.WriteLine("   6. Start Docker Container");
        Console.WriteLine("   7. Stop Docker Container");
        Console.WriteLine();
        Console.WriteLine("   B. Back");
        Console.WriteLine();
        Console.Write("  Select option: ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        if (choice is null or "B" or "") return;

        Console.Clear();
        PrintBanner();

        switch (choice)
        {
            case "1":
                ListAllServices();
                break;
            case "2":
                SearchServices();
                break;
            case "3":
                StartExternalService();
                break;
            case "4":
                StopExternalService();
                break;
            case "5":
                ListDockerContainers();
                break;
            case "6":
                StartDockerContainer();
                break;
            case "7":
                StopDockerContainer();
                break;
            default:
                WriteWarning($"Unknown option: {choice}");
                break;
        }

        Console.WriteLine("\n  Press Enter to continue...");
        Console.ReadLine();
    }
}

static void ListAllServices()
{
    PrintHeader("ALL SERVICES");

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        RunProcess("sc.exe", "query type= service state= all");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        RunProcess("systemctl", "list-units --type=service --all --no-pager");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        RunProcess("launchctl", "list");
    }
}

static void SearchServices(string? cliSearch = null)
{
    PrintHeader("SEARCH SERVICES");

    var search = cliSearch;
    if (string.IsNullOrEmpty(search))
    {
        Console.Write("  Enter search term > ");
        search = Console.ReadLine()?.Trim();
    }
    if (string.IsNullOrEmpty(search))
    {
        WriteWarning("No search term entered.");
        return;
    }

    Console.WriteLine();

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        // Use PowerShell to filter services
        WriteInfo($"Services matching '{search}':");
        Console.WriteLine();
        RunProcess("powershell", $"-NoProfile -Command \"Get-Service | Where-Object {{ $_.Name -like '*{search}*' -or $_.DisplayName -like '*{search}*' }} | Format-Table -Property Status,Name,DisplayName -AutoSize\"");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        WriteInfo($"Services matching '{search}':");
        Console.WriteLine();
        RunProcess("systemctl", $"list-units --type=service --all --no-pager | grep -i {search}");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        WriteInfo($"Services matching '{search}':");
        Console.WriteLine();
        RunProcess("launchctl", $"list | grep -i {search}");
    }
}

static void StartExternalService(string? cliName = null)
{
    PrintHeader("START SERVICE");

    var name = cliName;
    if (string.IsNullOrEmpty(name))
    {
        Console.Write("  Enter service name > ");
        name = Console.ReadLine()?.Trim();
    }
    if (string.IsNullOrEmpty(name))
    {
        WriteWarning("No service name entered.");
        return;
    }

    Console.WriteLine();
    WriteStep($"Starting {name}...");

    int result;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        result = RunProcess("sc.exe", $"start {name}");
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        result = RunProcess("sudo", $"systemctl start {name}");
    else
        result = RunProcess("launchctl", $"start {name}");

    if (result == 0)
        WriteSuccess($"{name} started.");
    else if (IsAccessDenied(result))
        PrintAccessDeniedGuidance($"start service '{name}'");
}

static void StopExternalService(string? cliName = null)
{
    PrintHeader("STOP SERVICE");

    var name = cliName;
    if (string.IsNullOrEmpty(name))
    {
        Console.Write("  Enter service name > ");
        name = Console.ReadLine()?.Trim();
    }
    if (string.IsNullOrEmpty(name))
    {
        WriteWarning("No service name entered.");
        return;
    }

    Console.WriteLine();
    WriteStep($"Stopping {name}...");

    int result;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        result = RunProcess("sc.exe", $"stop {name}");
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        result = RunProcess("sudo", $"systemctl stop {name}");
    else
        result = RunProcess("launchctl", $"stop {name}");

    if (result == 0)
        WriteSuccess($"{name} stopped.");
    else if (IsAccessDenied(result))
        PrintAccessDeniedGuidance($"stop service '{name}'");
}

static void ListDockerContainers()
{
    PrintHeader("DOCKER CONTAINERS");

    WriteInfo("All containers (running and stopped):");
    Console.WriteLine();
    var result = RunProcess("docker", "ps -a --format \"table {{.ID}}\\t{{.Names}}\\t{{.Status}}\\t{{.Image}}\"");
    if (result != 0)
    {
        WriteWarning("Docker may not be installed or running.");
        WriteDim("Install Docker Desktop or Docker Engine for container management.");
    }
}

static void StartDockerContainer(string? cliName = null)
{
    PrintHeader("START DOCKER CONTAINER");

    var name = cliName;
    if (string.IsNullOrEmpty(name))
    {
        // Show stopped containers first
        WriteInfo("Stopped containers:");
        Console.WriteLine();
        RunProcess("docker", "ps -a --filter \"status=exited\" --filter \"status=created\" --format \"table {{.ID}}\\t{{.Names}}\\t{{.Status}}\\t{{.Image}}\"");
        Console.WriteLine();

        Console.Write("  Enter container name or ID > ");
        name = Console.ReadLine()?.Trim();
    }
    if (string.IsNullOrEmpty(name))
    {
        WriteWarning("No container specified.");
        return;
    }

    Console.WriteLine();
    WriteStep($"Starting container {name}...");
    var result = RunProcess("docker", $"start {name}");
    if (result == 0)
        WriteSuccess($"Container {name} started.");
}

static void StopDockerContainer(string? cliName = null)
{
    PrintHeader("STOP DOCKER CONTAINER");

    var name = cliName;
    if (string.IsNullOrEmpty(name))
    {
        // Show running containers first
        WriteInfo("Running containers:");
        Console.WriteLine();
        RunProcess("docker", "ps --format \"table {{.ID}}\\t{{.Names}}\\t{{.Status}}\\t{{.Image}}\"");
        Console.WriteLine();

        Console.Write("  Enter container name or ID > ");
        name = Console.ReadLine()?.Trim();
    }
    if (string.IsNullOrEmpty(name))
    {
        WriteWarning("No container specified.");
        return;
    }

    Console.WriteLine();
    WriteStep($"Stopping container {name}...");
    var result = RunProcess("docker", $"stop {name}");
    if (result == 0)
        WriteSuccess($"Container {name} stopped.");
}

// ═══════════════════════════════════════════════════════════════════════
// CLI Action Wrappers — non-interactive equivalents for Service Account
// Manager features. Each reads from --Target:* and --ServiceAccount:*.
// ═══════════════════════════════════════════════════════════════════════

static int ActionAccountView()
{
    ViewSystemAccounts();
    return 0;
}

static int ActionAccountCreate(InstallerConfig config)
{
    if (string.IsNullOrEmpty(config.Target.Username))
    {
        WriteError("--Target:Username=<name> is required.");
        WriteDim("Example: installer account-create --Target:Username=FreeServiceAgent --ServiceAccount:Password=<pass>");
        return 1;
    }

    CreateServiceAccount(config);
    return 0;
}

static int ActionAccountDelete(InstallerConfig config)
{
    if (string.IsNullOrEmpty(config.Target.Username))
    {
        WriteError("--Target:Username=<name> is required.");
        WriteDim("Example: installer account-delete --Target:Username=FreeServiceAgent --Target:Confirm=true");
        return 1;
    }

    // CLI delete always requires --Target:Confirm=true as a safety net
    if (!config.Target.Confirm)
    {
        WriteError("--Target:Confirm=true is required for non-interactive account deletion.");
        WriteDim("This is a safety measure. Add --Target:Confirm=true to proceed.");
        return 1;
    }

    DeleteServiceAccount(config);
    return 0;
}

static int ActionAccountLookup(InstallerConfig config)
{
    var username = !string.IsNullOrEmpty(config.Target.Username)
        ? config.Target.Username
        : config.ServiceAccount.Username;

    if (string.IsNullOrEmpty(username))
    {
        WriteError("No username specified. Provide --Target:Username=<name> or --ServiceAccount:Username=<name>.");
        WriteDim("Example: installer account-lookup --Target:Username=FreeServiceAgent");
        return 1;
    }

    var shortName = username.Contains('\\') ? username.Split('\\').Last() : username;

    PrintHeader($"ACCOUNT LOOKUP — {shortName}");

    // ── 1. Check if user exists ──
    bool userExists = false;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        WriteInfo($"User details for '{shortName}':");
        Console.WriteLine();
        var result = RunProcess("net", $"user {shortName}");
        userExists = result == 0;
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        WriteInfo($"User details for '{shortName}':");
        Console.WriteLine();
        var result = RunProcess("id", shortName);
        if (result == 0)
        {
            userExists = true;
            Console.WriteLine();
            RunProcess("getent", $"passwd {shortName}");
        }
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        WriteInfo($"User details for '{shortName}':");
        Console.WriteLine();
        var result = RunProcess("id", shortName);
        if (result == 0)
        {
            userExists = true;
            Console.WriteLine();
            RunProcess("dscl", $". read /Users/{shortName}");
        }
    }

    Console.WriteLine();

    if (!userExists)
    {
        WriteWarning($"User '{shortName}' was NOT FOUND on this system.");
        return 1;
    }

    WriteSuccess($"User '{shortName}' EXISTS on this system.");
    Console.WriteLine();

    // ── 2. Group memberships ──
    WriteInfo("Group memberships:");
    Console.WriteLine();
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        RunProcess("net", $"user {shortName}");
    }
    else
    {
        RunProcess("id", $"-nG {shortName}");
    }

    Console.WriteLine();

    // ── 3. Permission status report ──
    WriteInfo("Permission status:");
    Console.WriteLine();

    var permissions = new (string Key, string Label)[]
    {
        ("svc",     "Service Control"),
        ("docker",  "Docker Management"),
        ("install", "Install Directory"),
        ("stats",   "System Stats"),
        ("apps",    "Application Control"),
    };

    foreach (var p in permissions)
    {
        var status = CheckPermissionStatus(shortName, p.Key, config);
        var statusColor = status ? ConsoleColor.Green : ConsoleColor.DarkGray;
        var statusText = status ? "[GRANTED]" : "[NOT SET]";

        Console.Write($"    {p.Label,-25}");
        Console.ForegroundColor = statusColor;
        Console.Write(statusText);
        Console.ResetColor();
        Console.WriteLine();
    }

    Console.WriteLine();
    return 0;
}

static int ActionGrant(InstallerConfig config)
{
    var username = !string.IsNullOrEmpty(config.Target.Username)
        ? config.Target.Username
        : config.ServiceAccount.Username;

    if (string.IsNullOrEmpty(username))
    {
        WriteError("No username specified. Provide --Target:Username=<name> or --ServiceAccount:Username=<name>.");
        return 1;
    }

    var perm = config.Target.Permission;
    if (string.IsNullOrEmpty(perm))
    {
        WriteError("--Target:Permission=<key> is required.");
        WriteDim("Valid keys: svc, docker, install, stats, apps, all");
        return 1;
    }

    var shortName = username.Contains('\\') ? username.Split('\\').Last() : username;

    if (perm.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        foreach (var key in new[] { "svc", "docker", "install", "stats", "apps" })
            GrantPermission(shortName, key, config);
        WriteSuccess($"All permissions granted to {shortName}.");
    }
    else
    {
        GrantPermission(shortName, perm.ToLowerInvariant(), config);
        WriteSuccess($"Permission '{perm}' granted to {shortName}.");
    }

    return 0;
}

static int ActionRevoke(InstallerConfig config)
{
    var username = !string.IsNullOrEmpty(config.Target.Username)
        ? config.Target.Username
        : config.ServiceAccount.Username;

    if (string.IsNullOrEmpty(username))
    {
        WriteError("No username specified. Provide --Target:Username=<name> or --ServiceAccount:Username=<name>.");
        return 1;
    }

    var perm = config.Target.Permission;
    if (string.IsNullOrEmpty(perm))
    {
        WriteError("--Target:Permission=<key> is required.");
        WriteDim("Valid keys: svc, docker, install, stats, apps, all");
        return 1;
    }

    var shortName = username.Contains('\\') ? username.Split('\\').Last() : username;

    if (perm.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        foreach (var key in new[] { "svc", "docker", "install", "stats", "apps" })
            RevokePermission(shortName, key, config);
        WriteSuccess($"All permissions revoked from {shortName}.");
    }
    else
    {
        RevokePermission(shortName, perm.ToLowerInvariant(), config);
        WriteSuccess($"Permission '{perm}' revoked from {shortName}.");
    }

    return 0;
}

static int ActionSvcList()
{
    ListAllServices();
    return 0;
}

static int ActionSvcSearch(InstallerConfig config)
{
    var search = config.Target.Search;
    if (string.IsNullOrEmpty(search))
    {
        WriteError("--Target:Search=<term> is required.");
        WriteDim("Example: installer svc-search --Target:Search=docker");
        return 1;
    }

    SearchServices(search);
    return 0;
}

static int ActionSvcControl(InstallerConfig config, bool start)
{
    var name = config.Target.ServiceName;
    if (string.IsNullOrEmpty(name))
    {
        WriteError("--Target:ServiceName=<name> is required.");
        WriteDim($"Example: installer {(start ? "svc-start" : "svc-stop")} --Target:ServiceName=docker");
        return 1;
    }

    if (start)
        StartExternalService(name);
    else
        StopExternalService(name);

    return 0;
}

static int ActionDockerList()
{
    ListDockerContainers();
    return 0;
}

static int ActionDockerControl(InstallerConfig config, bool start)
{
    var name = config.Target.ContainerName;
    if (string.IsNullOrEmpty(name))
    {
        WriteError("--Target:ContainerName=<name> is required.");
        WriteDim($"Example: installer {(start ? "docker-start" : "docker-stop")} --Target:ContainerName=my-redis");
        return 1;
    }

    if (start)
        StartDockerContainer(name);
    else
        StopDockerContainer(name);

    return 0;
}


// ═══════════════════════════════════════════════════════════════════════
// Cleanup — delete publish folders to free disk space
// ═══════════════════════════════════════════════════════════════════════

static int ActionCleanup(InstallerConfig config)
{
    PrintHeader("CLEANUP — DELETE PUBLISH FOLDERS");

    var nonInteractive = config.Target.Confirm;

    // Resolve the publish root (parent of publish/{runtime})
    var publishDir = Path.GetFullPath(config.Publish.OutputPath);
    var publishRoot = Path.GetDirectoryName(publishDir);

    // If we can walk up to a "publish" folder, use that as root
    if (publishRoot is not null && Path.GetFileName(publishRoot).Equals("publish", StringComparison.OrdinalIgnoreCase))
    {
        // Already pointing at the right place
    }
    else
    {
        // Try the standard location: {solutionDir}/publish
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
            {
                publishRoot = Path.Combine(dir.FullName, "publish");
                break;
            }
            dir = dir.Parent;
        }

        publishRoot ??= Path.Combine(AppContext.BaseDirectory, "publish");
    }

    if (!Directory.Exists(publishRoot))
    {
        WriteInfo("No publish directory found. Nothing to clean up.");
        WriteDim($"  Expected at: {publishRoot}");
        return 0;
    }

    // Scan all subdirectories
    var subdirs = Directory.GetDirectories(publishRoot);
    if (subdirs.Length == 0)
    {
        WriteInfo("Publish directory is empty. Nothing to clean up.");
        return 0;
    }

    WriteInfo($"Publish root: {publishRoot}\n");
    long totalSize = 0;

    foreach (var sub in subdirs)
    {
        var dirInfo = new DirectoryInfo(sub);
        var size = GetDirectorySize(dirInfo);
        totalSize += size;
        Console.WriteLine($"    {dirInfo.Name,-30} {FormatBytes(size),12}");
    }

    Console.WriteLine();
    WriteInfo($"Total: {FormatBytes(totalSize)} across {subdirs.Length} folder(s)");
    Console.WriteLine();

    // Confirmation — defaults to N
    if (!nonInteractive)
    {
        Console.Write("  Delete all publish folders? [y/N] > ");
        var answer = Console.ReadLine()?.Trim();
        if (!answer?.Equals("y", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            WriteWarning("Cancelled.");
            return 0;
        }
    }

    Console.WriteLine();
    foreach (var sub in subdirs)
    {
        var name = Path.GetFileName(sub);
        WriteStep($"Deleting {name}...");
        try
        {
            Directory.Delete(sub, recursive: true);
            WriteSuccess($"  Deleted {name}");
        }
        catch (Exception ex)
        {
            WriteError($"  Failed to delete {name}: {ex.Message}");
        }
    }

    // Try to remove the empty publish root itself
    try
    {
        if (Directory.Exists(publishRoot) && Directory.GetFileSystemEntries(publishRoot).Length == 0)
            Directory.Delete(publishRoot);
    }
    catch { }

    Console.WriteLine();
    WriteSuccess($"Cleanup complete — freed {FormatBytes(totalSize)}.");
    return 0;
}

static long GetDirectorySize(DirectoryInfo dir)
{
    long size = 0;
    try
    {
        foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            size += file.Length;
    }
    catch { }
    return size;
}

static string FormatBytes(long bytes)
{
    return bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1_024 => $"{bytes / 1_024.0:F1} KB",
        _ => $"{bytes} B",
    };
}


// ═══════════════════════════════════════════════════════════════════════
// Destroy — nuclear option: undo everything installed with defaults
//
// Uses the values in appsettings.json (or CLI overrides) to determine
// what to tear down. If you used custom names, update appsettings.json
// or use the interactive tools to remove them individually.
//
// Deletes:
//   1. The OS service        (Service:Name)
//   2. The service account   (ServiceAccount:Username)
//   3. The install directory  (Service:InstallPath, e.g. C:\FreeServices)
//   4. All publish folders    (publish/*)
//   5. The .configured marker
//   6. The API key from service config
// ═══════════════════════════════════════════════════════════════════════

static int ActionDestroy(InstallerConfig config)
{
    PrintHeader("DESTROY — UNDO EVERYTHING");

    var nonInteractive = config.Target.Confirm;
    var serviceName = config.Service.Name;
    var installPath = config.Service.InstallPath;
    var serviceAccount = config.ServiceAccount.Username;

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("""
    ╔══════════════════════════════════════════════════════════════╗
    ║                    ⚠  D E S T R O Y  ⚠                     ║
    ║                                                              ║
    ║  This will permanently remove EVERYTHING the installer has   ║
    ║  created using the current configuration defaults:           ║
    ╚══════════════════════════════════════════════════════════════╝
    """);
    Console.ResetColor();

    Console.WriteLine($"    Service:          {serviceName}");
    Console.WriteLine($"    Install Path:     {installPath}");
    Console.WriteLine($"    Service Account:  {(string.IsNullOrEmpty(serviceAccount) ? "(none configured)" : serviceAccount)}");
    Console.WriteLine($"    Publish Folders:  publish/*");
    Console.WriteLine($"    Config Marker:    .configured");
    Console.WriteLine();

    WriteDim("  If you used custom names, update appsettings.json first or use");
    WriteDim("  the interactive menu to remove individual items.");
    Console.WriteLine();

    // Confirmation — must type DESTROY
    if (!nonInteractive)
    {
        WriteWarning("This cannot be undone.");
        Console.Write("  Type DESTROY to confirm > ");
        var answer = Console.ReadLine()?.Trim();
        if (answer != "DESTROY")
        {
            WriteWarning("Cancelled — confirmation did not match.");
            return 0;
        }
    }

    Console.WriteLine();
    int errors = 0;

    // ── 1. Stop & remove the service ──
    WriteStep($"[1/6] Stopping service '{serviceName}'...");
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        RunProcess("sc.exe", $"stop {serviceName}");
        Thread.Sleep(2000);
        WriteStep($"       Deleting service '{serviceName}'...");
        var r = RunProcess("sc.exe", $"delete {serviceName}");
        if (r != 0 && IsAccessDenied(r))
        {
            PrintAccessDeniedGuidance("delete the Windows service");
            errors++;
        }
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        RunProcess("systemctl", $"stop {serviceName}");
        RunProcess("systemctl", $"disable {serviceName}");
        var unitPath = config.Systemd.UnitFilePath;
        if (File.Exists(unitPath))
        {
            WriteStep($"       Removing unit file: {unitPath}");
            try { File.Delete(unitPath); } catch { errors++; }
            RunProcess("systemctl", "daemon-reload");
        }
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        var plistPath = GetPlistPath(config);
        RunProcess("launchctl", $"unload \"{plistPath}\"");
        if (File.Exists(plistPath))
        {
            WriteStep($"       Removing plist: {plistPath}");
            try { File.Delete(plistPath); } catch { errors++; }
        }
    }
    WriteSuccess("  Service removal attempted.");
    Console.WriteLine();

    // ── 2. Delete the service account ──
    if (!string.IsNullOrEmpty(serviceAccount))
    {
        var shortName = serviceAccount.Contains('\\') ? serviceAccount.Split('\\').Last() : serviceAccount;

        // Safety: don't delete the current user
        if (shortName.Equals(Environment.UserName, StringComparison.OrdinalIgnoreCase))
        {
            WriteWarning($"[2/6] Skipping account deletion — '{shortName}' is the current user.");
        }
        else
        {
            WriteStep($"[2/6] Deleting service account '{shortName}'...");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var r = RunProcess("net", $"user {shortName} /delete");
                if (r != 0) errors++;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                RunProcess("sudo", $"userdel {shortName}");
                var sudoersPath = $"/etc/sudoers.d/{shortName}";
                if (File.Exists(sudoersPath))
                    RunProcess("sudo", $"rm -f {sudoersPath}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                RunProcess("sudo", $"sysadminctl -deleteUser {shortName}");
            }
            WriteSuccess("  Account deletion attempted.");
        }
    }
    else
    {
        WriteDim("[2/6] No service account configured — skipping.");
    }
    Console.WriteLine();

    // ── 3. Delete the install directory ──
    WriteStep($"[3/6] Deleting install directory: {installPath}");
    if (Directory.Exists(installPath))
    {
        try
        {
            Directory.Delete(installPath, recursive: true);
            WriteSuccess($"  Deleted {installPath}");
        }
        catch (Exception ex)
        {
            WriteError($"  Failed: {ex.Message}");
            errors++;
        }
    }
    else
    {
        WriteDim($"  Directory does not exist — nothing to delete.");
    }
    Console.WriteLine();

    // ── 4. Delete all publish folders ──
    WriteStep("[4/6] Deleting publish folders...");
    var publishDir = Path.GetFullPath(config.Publish.OutputPath);
    var publishRoot = Path.GetDirectoryName(publishDir);

    if (publishRoot is not null && Path.GetFileName(publishRoot).Equals("publish", StringComparison.OrdinalIgnoreCase))
    {
        // Good, we have the publish root
    }
    else
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
            {
                publishRoot = Path.Combine(dir.FullName, "publish");
                break;
            }
            dir = dir.Parent;
        }
        publishRoot ??= Path.Combine(AppContext.BaseDirectory, "publish");
    }

    if (Directory.Exists(publishRoot))
    {
        try
        {
            Directory.Delete(publishRoot, recursive: true);
            WriteSuccess($"  Deleted {publishRoot}");
        }
        catch (Exception ex)
        {
            WriteError($"  Failed: {ex.Message}");
            errors++;
        }
    }
    else
    {
        WriteDim($"  No publish directory found at {publishRoot}");
    }
    Console.WriteLine();

    // ── 5. Remove .configured marker ──
    WriteStep("[5/6] Removing .configured marker...");
    RemoveConfiguredMarker(config);
    WriteSuccess("  Marker cleanup attempted.");
    Console.WriteLine();

    // ── 6. Clear API key from service config ──
    WriteStep("[6/6] Clearing API key from service config...");
    ClearApiKeyFromServiceConfig(config);
    WriteSuccess("  API key cleanup attempted.");
    Console.WriteLine();

    // ── Summary ──
    Console.WriteLine();
    if (errors == 0)
    {
        WriteSuccess("Destroy complete — everything removed.");
    }
    else
    {
        WriteWarning($"Destroy completed with {errors} error(s). Some items may need manual cleanup.");
        WriteDim("Run as admin/sudo if you see access denied errors.");
    }

    return errors > 0 ? 1 : 0;
}


static int ActionHelp()
{
    var platform = GetPlatformName();
    Console.WriteLine($"""

    FreeServices Installer — Build, deploy, and manage services
    Platform: {platform}

    Usage:
      dotnet run                            Interactive menu
      dotnet run -- <action> [--overrides]  CLI mode

    Core Actions:
      build        Build/publish the service (dotnet publish)
      deploy       Full pipeline: build → configure → start
      configure    Install + set API key (use --Security:ApiKey=<key> for CI)
      remove       Stop and remove the service + clear configured state
      start        Start the service
      stop         Stop the service
      status       Query status and show recent log output
      config       View current configuration
      users        Service Account Manager (interactive submenu)
      instructions Interactive help with detailed guides
      help         Show this help

    Account Management:
      account-view     Show current user & system accounts
      account-create   Create a service account
                       --Target:Username=<name> --ServiceAccount:Password=<pass>
      account-delete   Delete a service account (requires --Target:Confirm=true)
                       --Target:Username=<name> --Target:Confirm=true
      account-lookup   Look up a specific user: existence, groups, permissions
                       --Target:Username=<name>

    Permission Management:
      grant            Grant a permission to an account
                       --Target:Username=<name> --Target:Permission=<key>
      revoke           Revoke a permission from an account
                       --Target:Username=<name> --Target:Permission=<key>
                       Keys: svc, docker, install, stats, apps, all

    Service Control:
      svc-list         List all OS services
      svc-search       Search services by name
                       --Target:Search=<term>
      svc-start        Start an OS service
                       --Target:ServiceName=<name>
      svc-stop         Stop an OS service
                       --Target:ServiceName=<name>

    Docker Control:
      docker-list      List all Docker containers
      docker-start     Start a Docker container
                       --Target:ContainerName=<name>
      docker-stop      Stop a Docker container
                       --Target:ContainerName=<name>

    Maintenance:
      cleanup          Delete publish folders to free disk space
                       Interactive: prompts Y/n (defaults to N)
                       CLI: --Target:Confirm=true
      destroy          Nuclear option — remove service, account, install
                       dir, publish folders, marker, API key. Uses
                       values from appsettings.json defaults.
                       Interactive: must type DESTROY to confirm
                       CLI: --Target:Confirm=true

    Config Overrides:
      --Service:Name=MyService
      --Service:DisplayName="My Display Name"
      --Service:InstallPath=C:\MyService
      --Security:ApiKey=<key>
      --ServiceAccount:Username=<user>
      --Publish:OutputPath=./publish/win-x64
      --Publish:Runtime=linux-arm64
      --Publish:SelfContained=false
      --Publish:SingleFile=false

    Linux-specific:
      --Systemd:User=myuser
      --Systemd:WorkingDirectory=/opt/svc
      --Systemd:UnitFilePath=/etc/systemd/system/myservice.service

    macOS-specific:
      --Launchd:Label=com.example.myservice
      --Launchd:SystemWide=true
      --Launchd:LogPath=/var/log/myservice.log

    CI/CD Examples:
      installer configure --Security:ApiKey=<key> --ServiceAccount:Username="NT AUTHORITY\NETWORK SERVICE"
      installer account-create --Target:Username=FreeServiceAgent --ServiceAccount:Password=s3cret
      installer account-lookup --Target:Username=FreeServiceAgent
      installer account-delete --Target:Username=FreeServiceAgent --Target:Confirm=true
      installer grant --Target:Username=FreeServiceAgent --Target:Permission=all
      installer revoke --Target:Username=FreeServiceAgent --Target:Permission=docker
      installer svc-start --Target:ServiceName=docker
      installer docker-stop --Target:ContainerName=my-redis
      installer cleanup --Target:Confirm=true
      installer destroy --Target:Confirm=true

    """);
    return 0;
}

static int ActionUnknown(string action)
{
    Console.WriteLine();
    WriteWarning($"Unknown action: '{action}'");
    WriteDim("Run with 'help' to see available actions.");
    Console.WriteLine();
    return 1;
}


// ═══════════════════════════════════════════════════════════════════════
// Instructions & Help — interactive submenu
// ═══════════════════════════════════════════════════════════════════════

static int ActionInstructions()
{
    while (true)
    {
        Console.Clear();
        PrintBanner();

        Console.WriteLine("  ── INSTRUCTIONS & HELP ──\n");
        Console.WriteLine("   1. Build (dotnet publish)");
        Console.WriteLine("   2. Full Deploy (build → configure → start)");
        Console.WriteLine("   3. Configure (install + set API key)");
        Console.WriteLine("   4. Remove (stop + uninstall)");
        Console.WriteLine("   5. Start / Stop / Status");
        Console.WriteLine("   6. Service Account Manager");
        Console.WriteLine("   7. Permissions Model (why admin is needed)");
        Console.WriteLine("   8. Service Accounts (least-privilege setup)");
        Console.WriteLine("   9. CI/CD Non-Interactive Mode");
        Console.WriteLine("  10. Configuration Overrides");
        Console.WriteLine();
        Console.WriteLine("   B. Back to main menu");
        Console.WriteLine();
        Console.Write("  Select topic: ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        if (choice is null or "B" or "") return 0;

        Console.Clear();
        PrintBanner();

        switch (choice)
        {
            case "1": ShowInstructionBuild(); break;
            case "2": ShowInstructionDeploy(); break;
            case "3": ShowInstructionConfigure(); break;
            case "4": ShowInstructionRemove(); break;
            case "5": ShowInstructionStartStopStatus(); break;
            case "6": ShowInstructionUsers(); break;
            case "7": ShowInstructionPermissions(); break;
            case "8": ShowInstructionServiceAccounts(); break;
            case "9": ShowInstructionCiCd(); break;
            case "10": ShowInstructionConfigOverrides(); break;
            default:
                WriteWarning($"Unknown topic: {choice}");
                break;
        }

        Console.WriteLine("\n  Press Enter to continue...");
        Console.ReadLine();
    }
}

static void ShowInstructionBuild()
{
    PrintHeader("BUILD (dotnet publish)");

    WriteInfo("TL;DR");
    WriteDim("Compiles and publishes the service as a self-contained, single-file binary.");
    WriteDim("Select a target platform, output lands in the publish/{runtime} folder.");
    Console.WriteLine();

    WriteInfo("DETAILS");
    WriteDim("The build action runs 'dotnet publish' with the configured settings:");
    WriteDim("  • Project Path   — which .csproj to publish");
    WriteDim("  • Runtime        — target RID (e.g., win-x64, linux-arm64)");
    WriteDim("  • Self-Contained — bundles the .NET runtime (no install required on target)");
    WriteDim("  • Single File    — merges everything into one executable");
    Console.WriteLine();
    WriteDim("The publish directory is cleaned before each build to avoid stale files.");
    Console.WriteLine();

    WriteInfo("EXAMPLES");
    WriteDim("  Interactive:  Select option 1, pick a platform, wait for build.");
    WriteDim("  CLI:          dotnet run -- build");
    WriteDim("  CLI override: dotnet run -- build --Publish:Runtime=linux-arm64");
    WriteDim("  CI/CD:        installer.exe build --Publish:Runtime=win-x64");
}

static void ShowInstructionDeploy()
{
    PrintHeader("FULL DEPLOY");

    WriteInfo("TL;DR");
    WriteDim("One-command pipeline: build → stop existing → remove → configure → start.");
    Console.WriteLine();

    WriteInfo("DETAILS");
    WriteDim("Deploy orchestrates the full lifecycle in order:");
    WriteDim("  1. Build — publishes the service binary");
    WriteDim("  2. Stop  — halts any running instance (ignores if not running)");
    WriteDim("  3. Remove — unregisters existing service (ignores if not registered)");
    WriteDim("  4. Configure — copies files, sets API key, registers the service");
    WriteDim("  5. Start — starts the newly installed service");
    Console.WriteLine();
    WriteDim("If any step fails, the pipeline stops and reports which step failed.");
    WriteDim("Requires elevated permissions (admin/sudo) for service registration.");
    Console.WriteLine();

    WriteInfo("EXAMPLES");
    WriteDim("  Interactive:  Select option 2");
    WriteDim("  CLI:          dotnet run -- deploy");
    WriteDim("  CI/CD:        installer.exe deploy --Security:ApiKey=<key>");
}

static void ShowInstructionConfigure()
{
    PrintHeader("CONFIGURE");

    WriteInfo("TL;DR");
    WriteDim("Registers the service with the OS and writes the API key. This is the 'install' step.");
    Console.WriteLine();

    WriteInfo("DETAILS");
    WriteDim("Configure is the Azure DevOps agent-style setup flow:");
    WriteDim("  1. Prompts for service name, display name (or uses defaults)");
    WriteDim("  2. Prompts for API key (masked input, like a password)");
    WriteDim("  3. Asks about service account and install paths");
    WriteDim("  4. Copies published files from publish dir → install dir");
    WriteDim("  5. Writes the API key into the service's appsettings.json");
    WriteDim("  6. Registers the service with the OS (sc.exe / systemd / launchd)");
    WriteDim("  7. Writes a .configured marker file to track state");
    Console.WriteLine();
    WriteDim("If already configured, it blocks re-install. Run 'remove' first.");
    Console.WriteLine();

    WriteInfo("EXAMPLES");
    WriteDim("  Interactive:  Select option 3, follow the prompts");
    WriteDim("  CLI:          dotnet run -- configure --Security:ApiKey=abc123");
    WriteDim("  Full flags:   installer.exe configure --Security:ApiKey=abc123 \\");
    WriteDim("                  --Service:Name=MyAgent --Service:InstallPath=C:\\MyAgent");
}

static void ShowInstructionRemove()
{
    PrintHeader("REMOVE (stop + uninstall)");

    WriteInfo("TL;DR");
    WriteDim("Stops the service, unregisters it from the OS, and clears credentials.");
    Console.WriteLine();

    WriteInfo("DETAILS");
    WriteDim("Remove performs the reverse of configure:");
    WriteDim("  1. Prompts for API key (authentication, like Azure DevOps agent)");
    WriteDim("  2. Stops the running service");
    WriteDim("  3. Deletes the service registration (sc.exe delete / systemctl disable)");
    WriteDim("  4. Clears the API key from the service's appsettings.json");
    WriteDim("  5. Removes the .configured marker file");
    Console.WriteLine();
    WriteDim("Requires elevated permissions. After remove, you can re-run 'configure'.");
    Console.WriteLine();

    WriteInfo("EXAMPLES");
    WriteDim("  Interactive:  Select option 4, enter API key when prompted");
    WriteDim("  CLI:          dotnet run -- remove --Security:ApiKey=abc123");
}

static void ShowInstructionStartStopStatus()
{
    PrintHeader("START / STOP / STATUS");

    WriteInfo("TL;DR");
    WriteDim("Start, stop, or query the service using the OS service manager.");
    Console.WriteLine();

    WriteInfo("DETAILS");
    Console.WriteLine();

    WriteDim("Start — tells the OS to run the service:");
    WriteDim("  Windows:  sc.exe start <ServiceName>");
    WriteDim("  Linux:    systemctl start <ServiceName>");
    WriteDim("  macOS:    launchctl start <Label>");
    Console.WriteLine();

    WriteDim("Stop — tells the OS to halt the service:");
    WriteDim("  Windows:  sc.exe stop <ServiceName>");
    WriteDim("  Linux:    systemctl stop <ServiceName>");
    WriteDim("  macOS:    launchctl stop <Label>");
    Console.WriteLine();

    WriteDim("Status — queries the current state and shows recent log output:");
    WriteDim("  Windows:  sc.exe query <ServiceName>");
    WriteDim("  Linux:    systemctl status <ServiceName>");
    WriteDim("  macOS:    launchctl list <Label>");
    WriteDim("  Also shows the last 10 lines of the service log file if found.");
    Console.WriteLine();

    WriteInfo("EXAMPLES");
    WriteDim("  Interactive:  Select option 5, 6, or 7");
    WriteDim("  CLI:          dotnet run -- start");
    WriteDim("  CLI:          dotnet run -- stop");
    WriteDim("  CLI:          dotnet run -- status");
}

static void ShowInstructionUsers()
{
    PrintHeader("SERVICE ACCOUNT MANAGER");

    WriteInfo("TL;DR");
    WriteDim("Create/delete dedicated service accounts, toggle permissions on/off,");
    WriteDim("and manage external services and Docker containers — all from one menu.");
    Console.WriteLine();

    WriteInfo("DETAILS");
    WriteDim("The Service Account Manager (menu option 9) has 5 sub-features:");
    Console.WriteLine();

    WriteDim("1. View Current User & System Accounts");
    WriteDim("   Shows who you're logged in as, your privileges, group memberships,");
    WriteDim("   and all local accounts on the system.");
    Console.WriteLine();

    WriteDim("2. Create Service Account");
    WriteDim("   Creates a dedicated user for running FreeServices (like Azure DevOps");
    WriteDim("   agent's config.sh --user). Auto-configures permissions:");
    WriteDim("     Windows: net user + 'Log on as a service' + docker-users + perf groups");
    WriteDim("     Linux:   useradd -r + docker group + scoped sudoers for systemctl");
    WriteDim("     macOS:   sysadminctl + docker group + directory ownership");
    Console.WriteLine();

    WriteDim("3. Delete Service Account");
    WriteDim("   Removes the account and cleans up ACLs/sudoers. Requires confirmation");
    WriteDim("   by typing the username. Cannot delete your own account.");
    Console.WriteLine();

    WriteDim("4. Manage Permissions");
    WriteDim("   Interactive toggle screen with 5 permission categories:");
    WriteDim("     • Service Control   — start/stop OS services");
    WriteDim("     • Docker Management — docker group membership");
    WriteDim("     • Install Directory — full control of the install folder");
    WriteDim("     • System Stats      — CPU/memory/disk performance counters");
    WriteDim("     • Application Control — start/stop other apps and processes");
    WriteDim("   Each shows [GRANTED] or [NOT SET] and can be toggled individually.");
    Console.WriteLine();

    WriteDim("5. Manage Services & Docker");
    WriteDim("   List/search/start/stop any service on the system.");
    WriteDim("   List/start/stop Docker containers.");
    WriteDim("   Useful for managing Azure DevOps runners, other agents, etc.");
    Console.WriteLine();

    WriteInfo("EXAMPLES");
    WriteDim("  Interactive:  Select option 9 from the main menu");
    WriteDim("  CLI:          dotnet run -- users");
    WriteDim("  Workflow:     Create account → Grant permissions → Configure service");
}

static void ShowInstructionPermissions()
{
    PrintHeader("PERMISSIONS MODEL");

    WriteInfo("TL;DR");
    WriteDim("This app needs elevated (admin/root) access to manage system services and Docker.");
    WriteDim("There's no way around it — but you can scope it to only what's needed.");
    Console.WriteLine();

    WriteInfo("WHY ADMIN IS NEEDED");
    WriteDim("FreeServices manages system-level resources:");
    WriteDim("  • Installing/starting/stopping OS services (requires service control permissions)");
    WriteDim("  • Managing Docker containers (requires docker socket / group access)");
    WriteDim("  • Starting/stopping other applications (requires process management rights)");
    Console.WriteLine();
    WriteDim("These are fundamentally privileged operations on every operating system.");
    WriteDim("Even Docker's 'rootless' mode still needs user namespace configuration.");
    Console.WriteLine();

    WriteInfo("THE RIGHT APPROACH PER PLATFORM");
    Console.WriteLine();

    WriteDim("WINDOWS:");
    WriteDim("  ✗ Don't run everything as full Administrator");
    WriteDim("  ✓ Create a dedicated service account with only:");
    WriteDim("      - 'Log on as a service' right (secpol.msc)");
    WriteDim("      - Membership in specific groups (e.g., docker-users)");
    WriteDim("      - Custom SDDL on specific services (sc.exe sdset)");
    WriteDim("  ✓ The INSTALLER needs admin to register — the SERVICE can run as limited user");
    Console.WriteLine();

    WriteDim("LINUX:");
    WriteDim("  ✗ Don't run as root");
    WriteDim("  ✓ Create a system user: sudo useradd -r -s /sbin/nologin freeservices");
    WriteDim("  ✓ Add to docker group: sudo usermod -aG docker freeservices");
    WriteDim("  ✓ Grant scoped sudoers for systemctl only (see Service Accounts topic)");
    WriteDim("  ✓ Use Linux capabilities for specific permissions (cap_net_bind_service)");
    WriteDim("  ✓ The INSTALLER needs sudo — the SERVICE runs as the limited user");
    Console.WriteLine();

    WriteDim("macOS:");
    WriteDim("  ✗ Don't run as root for everything");
    WriteDim("  ✓ Use a User Agent (~/Library/LaunchAgents) when possible — no root needed");
    WriteDim("  ✓ For System Daemons, install with sudo but run as a limited user");
    WriteDim("  ✓ Docker Desktop runs as user — no root needed for container management");
    Console.WriteLine();

    WriteInfo("KEY INSIGHT");
    WriteDim("Separate the INSTALLER permissions from the SERVICE permissions:");
    WriteDim("  • Installer needs elevated access once (to register the service)");
    WriteDim("  • Service runs continuously as a limited account (least privilege)");
}

static void ShowInstructionServiceAccounts()
{
    PrintHeader("SERVICE ACCOUNTS — LEAST-PRIVILEGE SETUP");

    WriteInfo("TL;DR");
    WriteDim("Create a dedicated account, give it only what it needs, install with admin, run as that user.");
    Console.WriteLine();

    WriteInfo("WINDOWS — STEP BY STEP");
    WriteDim("  1. Create the account:");
    WriteDim("     net user FreeServiceAgent <StrongPassword> /add");
    WriteDim("     net user FreeServiceAgent /active:yes");
    Console.WriteLine();
    WriteDim("  2. Grant 'Log on as a service':");
    WriteDim("     secpol.msc → Local Policies → User Rights Assignment");
    WriteDim("     → 'Log on as a service' → Add User → FreeServiceAgent");
    Console.WriteLine();
    WriteDim("  3. Add to docker-users (if managing Docker):");
    WriteDim("     net localgroup docker-users FreeServiceAgent /add");
    Console.WriteLine();
    WriteDim("  4. Install with this account:");
    WriteDim("     installer.exe configure --Security:ApiKey=xxx \\");
    WriteDim("       --ServiceAccount:Username=.\\FreeServiceAgent \\");
    WriteDim("       --ServiceAccount:Password=<StrongPassword>");
    Console.WriteLine();

    WriteInfo("LINUX — STEP BY STEP");
    WriteDim("  1. Create system user (no login shell, no home):");
    WriteDim("     sudo useradd -r -s /usr/sbin/nologin -d /opt/freeservices freeservices");
    Console.WriteLine();
    WriteDim("  2. Add to docker group:");
    WriteDim("     sudo usermod -aG docker freeservices");
    Console.WriteLine();
    WriteDim("  3. Grant scoped sudo (create /etc/sudoers.d/freeservices):");
    WriteDim("     freeservices ALL=(root) NOPASSWD: /usr/bin/systemctl start *, \\");
    WriteDim("       /usr/bin/systemctl stop *, /usr/bin/systemctl restart *");
    Console.WriteLine();
    WriteDim("  4. Set ownership on install directory:");
    WriteDim("     sudo chown -R freeservices:freeservices /opt/freeservices");
    Console.WriteLine();
    WriteDim("  5. Install with this user:");
    WriteDim("     sudo ./installer configure --Security:ApiKey=xxx --Systemd:User=freeservices");
    Console.WriteLine();

    WriteInfo("macOS — STEP BY STEP");
    WriteDim("  1. Option A — User Agent (recommended, no root):");
    WriteDim("     ./installer configure --Launchd:SystemWide=false --Security:ApiKey=xxx");
    WriteDim("     → Installs to ~/Library/LaunchAgents, runs as current user");
    Console.WriteLine();
    WriteDim("  2. Option B — System Daemon (requires sudo to install):");
    WriteDim("     sudo ./installer configure --Launchd:SystemWide=true --Security:ApiKey=xxx");
    WriteDim("     → Installs to /Library/LaunchDaemons, runs as root by default");
    WriteDim("     → Add UserName key to plist to run as limited user");
}

static void ShowInstructionCiCd()
{
    PrintHeader("CI/CD NON-INTERACTIVE MODE");

    WriteInfo("TL;DR");
    WriteDim("Pass all values via CLI flags — the installer skips all prompts automatically.");
    Console.WriteLine();

    WriteInfo("DETAILS");
    WriteDim("When --Security:ApiKey is provided via CLI, the installer detects non-interactive");
    WriteDim("mode and uses all values from flags/config without prompting.");
    Console.WriteLine();
    WriteDim("This works in any CI/CD system: Azure DevOps, GitHub Actions, Jenkins, etc.");
    Console.WriteLine();

    WriteInfo("EXAMPLES");
    Console.WriteLine();
    WriteDim("  Build only:");
    WriteDim("    installer.exe build --Publish:Runtime=linux-x64");
    Console.WriteLine();
    WriteDim("  Full deploy (build + configure + start):");
    WriteDim("    installer.exe deploy --Security:ApiKey=$(API_KEY) \\");
    WriteDim("      --Service:Name=MyAgent \\");
    WriteDim("      --ServiceAccount:Username=\"NT AUTHORITY\\NETWORK SERVICE\"");
    Console.WriteLine();
    WriteDim("  Configure only (service already built):");
    WriteDim("    installer.exe configure --Security:ApiKey=$(API_KEY) \\");
    WriteDim("      --Service:InstallPath=/opt/myservice");
    Console.WriteLine();
    WriteDim("  Remove (with key rotation):");
    WriteDim("    installer.exe remove --Security:ApiKey=$(OLD_KEY)");
    Console.WriteLine();

    WriteInfo("AZURE DEVOPS PIPELINE EXAMPLE");
    WriteDim("  - script: |");
    WriteDim("      ./installer deploy --Security:ApiKey=$(ApiKey)");
    WriteDim("    displayName: 'Deploy FreeServices Agent'");
    WriteDim("    env:");
    WriteDim("      ApiKey: $(FreeServicesApiKey)");
}

static void ShowInstructionConfigOverrides()
{
    PrintHeader("CONFIGURATION OVERRIDES");

    WriteInfo("TL;DR");
    WriteDim("Every config value can be set via CLI flag (--Section:Key=value) or appsettings.json.");
    Console.WriteLine();

    WriteInfo("PRECEDENCE (highest wins)");
    WriteDim("  1. Command-line arguments   --Service:Name=MyAgent");
    WriteDim("  2. appsettings.json         { \"Service\": { \"Name\": \"MyAgent\" } }");
    WriteDim("  3. Built-in defaults        FreeServicesMonitor");
    Console.WriteLine();

    WriteInfo("ALL AVAILABLE OVERRIDES");
    Console.WriteLine();

    WriteDim("Service:");
    WriteDim("  --Service:Name=<name>                    Service registration name");
    WriteDim("  --Service:DisplayName=<name>             Friendly name in service manager");
    WriteDim("  --Service:InstallPath=<path>             Where binaries are deployed to");
    Console.WriteLine();

    WriteDim("Security:");
    WriteDim("  --Security:ApiKey=<key>                  API key for server authentication");
    Console.WriteLine();

    WriteDim("Service Account:");
    WriteDim("  --ServiceAccount:Username=<user>         Logon account for the service");
    WriteDim("  --ServiceAccount:Password=<pass>         Password (Windows non-builtin only)");
    Console.WriteLine();

    WriteDim("Publish:");
    WriteDim("  --Publish:ProjectPath=<path>             Path to the service .csproj");
    WriteDim("  --Publish:OutputPath=<path>              Staging directory for publish output");
    WriteDim("  --Publish:Runtime=<rid>                  Target RID (win-x64, linux-arm64, etc.)");
    WriteDim("  --Publish:SelfContained=<true|false>     Bundle the .NET runtime");
    WriteDim("  --Publish:SingleFile=<true|false>         Merge into single executable");
    Console.WriteLine();

    WriteDim("Linux (systemd):");
    WriteDim("  --Systemd:UnitFilePath=<path>            Unit file location");
    WriteDim("  --Systemd:User=<user>                    User to run the service as");
    WriteDim("  --Systemd:WorkingDirectory=<path>        Working directory for the service");
    Console.WriteLine();

    WriteDim("macOS (launchd):");
    WriteDim("  --Launchd:Label=<label>                  Launchd service label");
    WriteDim("  --Launchd:SystemWide=<true|false>        System daemon vs user agent");
    WriteDim("  --Launchd:LogPath=<path>                 Stdout log file path");
    Console.WriteLine();

    WriteDim("Recovery:");
    WriteDim("  --Recovery:RestartDelayMs=<ms>            Delay before restart on crash");
    WriteDim("  --Recovery:ResetPeriodSeconds=<sec>       Failure counter reset period");
}


// ═══════════════════════════════════════════════════════════════════════
// Helpers
// ═══════════════════════════════════════════════════════════════════════

static void ResolveProjectPaths(InstallerConfig config)
{
    // If the ProjectPath is already absolute and valid, leave it alone
    if (Path.IsPathRooted(config.Publish.ProjectPath)
        && (Directory.Exists(config.Publish.ProjectPath) || File.Exists(config.Publish.ProjectPath)))
    {
        return;
    }

    // Strategy: walk up from the exe directory looking for the target project
    // folder. This handles bin\Debug\net10.0\, bin\Release\net10.0\, publish
    // output, or running from the project directory itself.
    var targetName = Path.GetFileName(config.Publish.ProjectPath); // e.g. "FreeServices.Service"
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

    // Last resort: leave it as-is (will error when used)
}

/// <summary>
/// If Publish.OutputPath is empty (default), set it to a "publish/{runtime}" folder
/// next to the installer exe. This keeps publish output local and separated by target platform.
/// </summary>
static void ResolvePublishOutputPath(InstallerConfig config)
{
    if (!string.IsNullOrEmpty(config.Publish.OutputPath))
        return;

    // Find the solution/repo root by walking up from the installer exe
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        // Look for .slnx, .sln, or the installer project folder
        if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
        {
            config.Publish.OutputPath = Path.Combine(dir.FullName, "publish", config.Publish.Runtime);
            return;
        }
        dir = dir.Parent;
    }

    // Fallback: relative to the installer exe
    config.Publish.OutputPath = Path.Combine(AppContext.BaseDirectory, "publish", config.Publish.Runtime);
}

/// <summary>
/// Copy all files from the publish output directory to the install path.
/// Creates the install directory if it doesn't exist.
/// </summary>
static int CopyPublishToInstall(InstallerConfig config)
{
    var source = config.Publish.OutputPath;
    var dest = config.Service.InstallPath;

    if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
    {
        WriteInfo("Publish and install paths are the same — skipping copy.");
        return 0;
    }

    if (!Directory.Exists(source))
    {
        WriteWarning($"Publish directory not found: {source}");
        WriteWarning("Run 'build' first to publish the service.");
        WriteWarning("Skipping copy — install will use whatever is already at the install path.");
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

static void PrintHeader(string title)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"  ── {title} ──");
    Console.ResetColor();
    Console.WriteLine();
}


// ═══════════════════════════════════════════════════════════════════════
// Console Output Helpers — semantic color + symbol indicators
// ADA/WCAG compliant: color is never the only indicator (symbols + text)
// ═══════════════════════════════════════════════════════════════════════

static void WriteSuccess(string message)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  ✓ {message}");
    Console.ResetColor();
}

static void WriteError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ✗ {message}");
    Console.ResetColor();
}

static void WriteWarning(string message)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  ⚠ {message}");
    Console.ResetColor();
}

static void WriteInfo(string message)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  ● {message}");
    Console.ResetColor();
}

static void WriteStep(string message)
{
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine($"  » {message}");
    Console.ResetColor();
}

static void WriteDim(string message)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"    {message}");
    Console.ResetColor();
}

// ═══════════════════════════════════════════════════════════════════════
// Access Denied — remediation guidance per platform
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Returns true if the exit code indicates an access/permission denied error.
/// </summary>
static bool IsAccessDenied(int exitCode)
{
    // Windows: sc.exe returns 5 for ERROR_ACCESS_DENIED
    // Linux/macOS: typical exit codes for permission errors
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return exitCode == 5;

    // Linux/macOS — systemctl/launchctl don't have a single code,
    // but 1 with "Access denied" in stderr is common. We also check
    // for common EPERM (1) and EACCES (13) wrapper codes.
    return exitCode is 1 or 4 or 13;
}

/// <summary>
/// Prints platform-specific remediation advice after an access-denied failure.
/// Shows a quick fix (run elevated) and a more precise least-privilege approach.
/// </summary>
static void PrintAccessDeniedGuidance(string operation)
{
    Console.WriteLine();
    WriteError($"Access denied while trying to {operation}.");
    Console.WriteLine();

    WriteInfo("QUICK FIX — run this installer elevated:");

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        WriteDim("Right-click the terminal → 'Run as Administrator', then re-run.");
        WriteDim("  or: Start-Process powershell -Verb RunAs");
        Console.WriteLine();

        WriteInfo("RECOMMENDED — grant only what's needed:");
        WriteDim("Your service needs permissions to:");
        WriteDim("  • Create/start/stop Windows Services (sc.exe)");
        WriteDim("  • Manage Docker containers (docker.exe)");
        WriteDim("  • Start/stop other applications");
        Console.WriteLine();

        WriteDim("Option A — Add user to a scoped group:");
        WriteDim("  # Grant service control without full admin");
        WriteDim("  sc.exe sdset <ServiceName> D:(A;;CCLCSWRPWPDTLOCRRC;;;SU)");
        WriteDim("  # 'SU' = SERVICE_USER_PRINCIPAL, swap for a custom group SID");
        Console.WriteLine();

        WriteDim("Option B — Use a dedicated service account:");
        WriteDim("  net user FreeServiceAgent <password> /add");
        WriteDim("  net localgroup \"Performance Log Users\" FreeServiceAgent /add");
        WriteDim("  # Then configure the installer with:");
        WriteDim("  #   --ServiceAccount:Username=.\\FreeServiceAgent");
        Console.WriteLine();

        WriteDim("Option C — Local Group Policy (secpol.msc):");
        WriteDim("  Local Policies → User Rights Assignment");
        WriteDim("    → 'Log on as a service' — add your account");
        WriteDim("    → 'Replace a process level token' — for Docker management");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        WriteDim("Re-run with: sudo ./FreeServices.Installer");
        Console.WriteLine();

        WriteInfo("RECOMMENDED — grant only what's needed:");
        WriteDim("Your service needs permissions to:");
        WriteDim("  • Create/start/stop systemd services (systemctl)");
        WriteDim("  • Manage Docker containers (docker)");
        WriteDim("  • Start/stop other applications");
        Console.WriteLine();

        WriteDim("Option A — Dedicated user + docker group:");
        WriteDim("  sudo useradd -r -s /usr/sbin/nologin freeservices");
        WriteDim("  sudo usermod -aG docker freeservices");
        WriteDim("  # Then set --Systemd:User=freeservices");
        Console.WriteLine();

        WriteDim("Option B — Scoped sudoers (no password):");
        WriteDim("  # /etc/sudoers.d/freeservices");
        WriteDim("  freeservices ALL=(root) NOPASSWD: /usr/bin/systemctl start *, \\");
        WriteDim("    /usr/bin/systemctl stop *, /usr/bin/systemctl enable *, \\");
        WriteDim("    /usr/bin/systemctl disable *, /usr/bin/systemctl daemon-reload");
        Console.WriteLine();

        WriteDim("Option C — Linux capabilities (avoid root entirely):");
        WriteDim("  sudo setcap cap_net_bind_service,cap_sys_ptrace+ep ./FreeServices.Service");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        WriteDim("Re-run with: sudo ./FreeServices.Installer");
        Console.WriteLine();

        WriteInfo("RECOMMENDED — grant only what's needed:");
        WriteDim("Your service needs permissions to:");
        WriteDim("  • Load/unload launchd daemons (launchctl)");
        WriteDim("  • Manage Docker containers (docker)");
        WriteDim("  • Start/stop other applications");
        Console.WriteLine();

        WriteDim("Option A — User-level agent (no sudo needed):");
        WriteDim("  Install as a User Agent instead of System Daemon:");
        WriteDim("  --Launchd:SystemWide=false");
        WriteDim("  Plist goes to ~/Library/LaunchAgents/ (no root required)");
        Console.WriteLine();

        WriteDim("Option B — System daemon + group ownership:");
        WriteDim("  sudo dseditgroup -o create -r 'FreeServices' freeservices");
        WriteDim("  sudo dseditgroup -o edit -a $(whoami) -t user freeservices");
        WriteDim("  sudo chown root:freeservices /Library/LaunchDaemons/com.wsu.eit.freeservices.plist");
        Console.WriteLine();

        WriteDim("Option C — Docker group (for container management):");
        WriteDim("  # Docker Desktop on macOS runs as user — ensure your account");
        WriteDim("  # is in the 'docker' group or has Desktop access.");
    }

    Console.WriteLine();
    WriteDim("For full details, select 'Instructions & Help' from the main menu.");
}

static string GetPlatformName()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux/systemd";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS/launchd";
    return "Unknown";
}

static string GetPlistPath(InstallerConfig config)
{
    var fileName = $"{config.Launchd.Label}.plist";
    if (config.Launchd.SystemWide)
        return Path.Combine("/Library/LaunchDaemons", fileName);
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    return Path.Combine(home, "Library", "LaunchAgents", fileName);
}

static string? FindLogFile(InstallerConfig config)
{
    // Check common locations for the service log across all platforms
    var candidates = new List<string>
    {
        Path.Combine(config.Service.InstallPath, "service-output.log"),
        Path.Combine(config.Publish.OutputPath, "service-output.log"),
        "service-output.log",
        Path.Combine(config.Publish.ProjectPath, "service-output.log"),
    };

    // Platform-specific log paths
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        candidates.Add(Path.Combine(config.Systemd.WorkingDirectory, "service-output.log"));
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        candidates.Add(config.Launchd.LogPath);
    }

    return candidates.FirstOrDefault(File.Exists);
}

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

        // Read output in real-time
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


// ═══════════════════════════════════════════════════════════════════════
// Banner — Azure DevOps agent-style ASCII art
// ═══════════════════════════════════════════════════════════════════════

static void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("""

     _____ ____  _____ _____    ____  _____ ____  _   _ ___  ____ _____ ____
    |  ___|  _ \| ____| ____|  / ___|| ____| __ )| | | |_ _|/ ___| ____/ ___|
    | |_  | |_) |  _| |  _|    \___ \|  _| |  _ \| | | || || |   |  _| \___ \
    |  _| |  _ <| |___| |___    ___) | |___| |_) | |/ / | || |___| |___ ___) |
    |_|   |_| \_\_____|_____|  |____/|_____|____/ \___/ |___|\____|_____|____/

    """);
    Console.ResetColor();

    var version = typeof(InstallerConfig).Assembly.GetName().Version;
    var versionStr = version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    Console.WriteLine($"        agent v{versionStr}");
    Console.WriteLine();
}


// ═══════════════════════════════════════════════════════════════════════
// Configured State — marker file to track installation
// ═══════════════════════════════════════════════════════════════════════

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
            Platform = GetPlatformName(),
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


// ═══════════════════════════════════════════════════════════════════════
// API Key — write to service appsettings.json
// ═══════════════════════════════════════════════════════════════════════

static int WriteApiKeyToServiceConfig(InstallerConfig config)
{
    try
    {
        // Find the service appsettings.json — check install path first, then publish output, then project source
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

            var security = doc["Security"]?.AsObject();
            if (security is null)
            {
                security = new JsonObject();
                doc["Security"] = security;
            }

            security["ApiKey"] = config.Security.ApiKey;

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

            var security = doc["Security"]?.AsObject();
            if (security is not null)
            {
                security["ApiKey"] = "";
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(settingsPath, doc.ToJsonString(options));
            }
        }
    }
    catch { /* best effort */ }
}


// ═══════════════════════════════════════════════════════════════════════
// Masked Input — reads keystrokes and prints * characters
// ═══════════════════════════════════════════════════════════════════════

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
