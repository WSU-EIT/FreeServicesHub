# FreeServices.Installer

A cross-platform CLI and interactive UI tool for building, deploying, installing, starting, stopping, querying, and uninstalling .NET background services. Provides a single unified interface that auto-detects the operating system and routes to the correct platform service manager.

**Windows** (`sc.exe`) · **Linux** (`systemd/systemctl`) · **macOS** (`launchd/launchctl`)

Developed by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

---

## What This Is

The Installer wraps every service lifecycle operation — build, publish, configure, start, stop, status, remove — into a single cross-platform .NET console application with two equal interfaces:

- **Interactive menu** — run with no arguments, get a numbered menu
- **CLI flags** — pass a command like `deploy` or `status` for headless/pipeline use

Both interfaces call the exact same `RunAction()` dispatcher with the same config object. There is no "primary" interface — menu option `2` and `dotnet run -- deploy` execute identical code paths.

This matters because:
- A developer uses the menu locally to explore and iterate
- A pipeline uses CLI flags for headless automation
- The test harness uses CLI flags for verification
- All three paths produce identical behavior

---

## Quick Start

### Interactive Menu

```bash
dotnet run
```

```
═══ FREESERVICES INSTALLER (Windows/sc.exe) ═══

    Status: NOT CONFIGURED  |  Platform: Windows

    CURRENT CONFIGURATION
    Service Name:   FreeServicesMonitor
    Display Name:   FreeServices System Monitor
    Exe Path:       C:\FreeServices\FreeServices.Service.exe
    Project:        C:\repos\FreeServices\FreeServices.Service
    Publish To:     C:\repos\FreeServices\publish\win-x64
    Install To:     C:\FreeServices
    Runtime:        win-x64
    Self-Contained: Yes
    Single File:    Yes

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

  Select option:
```

The platform name and all defaults auto-detect from the current OS and architecture.

### CLI Mode

```bash
# Core lifecycle
dotnet run -- build                          # Build/publish the service
dotnet run -- deploy                         # Full pipeline: build → configure → start
dotnet run -- configure                      # Interactive setup (API key, account, install)
dotnet run -- remove                         # Auth + stop + uninstall + clear credentials
dotnet run -- start                          # Start the service
dotnet run -- stop                           # Stop the service
dotnet run -- status                         # Query status + recent log
dotnet run -- config                         # View current configuration
dotnet run -- help                           # Show usage

# Account management
dotnet run -- account-view                   # Show current user & system accounts
dotnet run -- account-create                 # Create a service account
dotnet run -- account-delete                 # Delete a service account
dotnet run -- account-lookup                 # Look up a user: existence, groups, permissions

# Permission management
dotnet run -- grant                          # Grant a permission to an account
dotnet run -- revoke                         # Revoke a permission from an account

# External service control
dotnet run -- svc-list                       # List all OS services
dotnet run -- svc-search                     # Search services by name
dotnet run -- svc-start                      # Start an OS service
dotnet run -- svc-stop                       # Stop an OS service

# Docker control
dotnet run -- docker-list                    # List all Docker containers
dotnet run -- docker-start                   # Start a Docker container
dotnet run -- docker-stop                    # Stop a Docker container

# Maintenance
dotnet run -- cleanup                        # Delete publish folders to free disk space
dotnet run -- destroy                        # Nuclear option: remove everything
```

### Config Overrides

Any configuration value can be overridden via CLI args using colon-delimited paths:

```bash
# Override service name and runtime
dotnet run -- deploy --Service:Name=MyMonitor --Publish:Runtime=linux-arm64

# Override systemd settings (Linux)
dotnet run -- deploy --Systemd:User=appuser --Systemd:WorkingDirectory=/opt/myservice

# Override launchd settings (macOS)
dotnet run -- deploy --Launchd:SystemWide=true --Launchd:Label=com.example.myservice

# Pipeline usage — publish to artifact staging
dotnet run -- build --Publish:OutputPath=$(Build.ArtifactStagingDirectory)/Service
```

---

## Platform Operations

### What Each Action Does Per Platform

| Action | Windows | Linux | macOS |
|--------|---------|-------|-------|
| **build** | `dotnet publish` → `win-x64` self-contained single-file | `dotnet publish` → `linux-x64` self-contained single-file | `dotnet publish` → `osx-arm64` self-contained single-file |
| **configure** | Copy files → write API key → `sc.exe create` + auto-start + description + crash recovery (`sc.exe failure`) + `.configured` marker | Copy files → write API key → generate systemd unit file → `systemctl daemon-reload` → `systemctl enable` + marker | Copy files → write API key → generate launchd plist → `launchctl load` + marker |
| **remove** | Auth → `sc.exe stop` → `sc.exe delete` → clear API key → remove marker | Auth → `systemctl stop` → `systemctl disable` → delete unit file → `daemon-reload` → clear API key → remove marker | Auth → `launchctl unload` → delete plist → clear API key → remove marker |
| **start** | `sc.exe start` | `systemctl start` | `launchctl start` |
| **stop** | `sc.exe stop` | `systemctl stop` | `launchctl stop` |
| **status** | `sc.exe query` + last 10 log lines | `systemctl status --no-pager` + last 10 log lines | `launchctl list` + last 10 log lines |
| **deploy** | build → stop (if exists) → remove (if exists) → configure → start | Same | Same |

### Windows — `sc.exe`

The Installer creates the service with `sc.exe create`, sets the description, and configures crash recovery:

```
sc.exe create FreeServicesMonitor binPath= "C:\FreeServices\FreeServices.Service.exe" start= auto DisplayName= "FreeServices System Monitor"
sc.exe description FreeServicesMonitor "Periodically collects and logs system information."
sc.exe failure FreeServicesMonitor reset= 86400 actions= restart/5000/restart/5000/restart/5000
sc.exe failureflag FreeServicesMonitor 1
```

Recovery: restarts on all three failure types with a 5-second delay. Failure counter resets after 24 hours.

### Linux — systemd

The Installer generates a unit file at `/etc/systemd/system/freeservices.service`:

```ini
[Unit]
Description=FreeServices System Monitor
After=network.target

[Service]
Type=notify
ExecStart=/opt/freeservices/FreeServices.Service
WorkingDirectory=/opt/freeservices
User=root
Restart=on-failure
RestartSec=5
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Then runs `systemctl daemon-reload` and `systemctl enable`.

### macOS — launchd

The Installer generates a plist at `~/Library/LaunchAgents/com.wsu.eit.freeservices.plist` (user agent) or `/Library/LaunchDaemons/` (system daemon):

```xml
<plist version="1.0">
<dict>
    <key>Label</key><string>com.wsu.eit.freeservices</string>
    <key>ProgramArguments</key><array><string>/usr/local/bin/FreeServices.Service</string></array>
    <key>RunAtLoad</key><true/>
    <key>KeepAlive</key><true/>
    <key>StandardOutPath</key><string>/tmp/freeservices.log</string>
    <key>StandardErrorPath</key><string>/tmp/freeservices.error.log</string>
    <key>WorkingDirectory</key><string>/usr/local/bin</string>
</dict>
</plist>
```

Then runs `launchctl load`. `KeepAlive=true` makes launchd restart the service automatically on crash.

---

## Service Account Manager

Menu option **9** opens a submenu for managing service accounts and permissions:

```
── SERVICE ACCOUNT MANAGER ──

 1. View Current User & System Accounts
 2. Create Service Account
 3. Delete Service Account
 4. Manage Permissions
 5. Manage Services & Docker

 B. Back to main menu
```

### View Current User & System Accounts

Shows the current logged-in user, privileges, group memberships, and all local accounts. Platform-specific commands:

| Platform | Commands |
|----------|----------|
| Windows | `whoami`, `whoami /priv`, `whoami /groups`, `net localgroup Administrators`, `net user` |
| Linux | `whoami`, `id`, `awk` on `/etc/passwd`, `getent group sudo wheel`, `getent group docker` |
| macOS | `whoami`, `id`, `dscl . list /Users`, `dscl . read /Groups/admin GroupMembership` |

### Create Service Account

Creates a dedicated user for running FreeServices with auto-configured permissions:

| Platform | Method | Auto-configured permissions |
|----------|--------|-----------------------------|
| Windows | `net user ... /add` | Password never expires, "Log on as a service" right, docker-users group, Performance Monitor/Log Users groups, full control on install path |
| Linux | `sudo useradd -r -s /usr/sbin/nologin` | Docker group, scoped sudoers rules for systemctl commands, ownership on install path |
| macOS | `sudo sysadminctl -addUser` | Docker group (if available), ownership on install path |

CLI equivalent:

```bash
installer account-create --Target:Username=FreeServiceAgent --ServiceAccount:Password=<pass>
```

### Delete Service Account

Removes the account and cleans up ACLs/sudoers. Requires confirmation by typing the username (interactive) or `--Target:Confirm=true` (CLI). Cannot delete the currently logged-in user.

CLI equivalent:

```bash
installer account-delete --Target:Username=FreeServiceAgent --Target:Confirm=true
```

### Account Lookup

Focused single-user report: checks existence, groups, and permission status across all 5 categories.

CLI equivalent:

```bash
installer account-lookup --Target:Username=FreeServiceAgent
```

### Manage Permissions

Interactive toggle screen with 5 permission categories:

| # | Category | Key | Description |
|---|----------|-----|-------------|
| 1 | Service Control | `svc` | Start/stop/manage Windows/systemd/launchd services |
| 2 | Docker Management | `docker` | Start/stop/manage Docker containers and images |
| 3 | Install Directory | `install` | Full control of the service install path |
| 4 | System Stats | `stats` | Read CPU, memory, disk space, performance counters |
| 5 | Application Control | `apps` | Start/stop/manage other applications and processes |

Each shows `[GRANTED]` or `[NOT SET]` and can be toggled individually. Options `A` grants all and `R` revokes all.

CLI equivalents:

```bash
installer grant --Target:Username=FreeServiceAgent --Target:Permission=docker
installer revoke --Target:Username=FreeServiceAgent --Target:Permission=svc
installer grant --Target:Username=FreeServiceAgent --Target:Permission=all
```

### Manage Services & Docker

Submenu for controlling any service or Docker container on the system:

```
── SERVICES & DOCKER MANAGER ──

 1. List All Services
 2. Search Services by Name
 3. Start a Service
 4. Stop a Service
 5. List Docker Containers
 6. Start Docker Container
 7. Stop Docker Container

 B. Back
```

CLI equivalents:

```bash
installer svc-list
installer svc-search --Target:Search=docker
installer svc-start --Target:ServiceName=docker
installer svc-stop --Target:ServiceName=docker
installer docker-list
installer docker-start --Target:ContainerName=my-redis
installer docker-stop --Target:ContainerName=my-redis
```

---

## Cleanup & Destroy

### Cleanup (Menu Option 11)

Scans all publish subdirectories, shows sizes, and prompts for deletion. Defaults to **N** for safety.

```bash
# Interactive
Select option 11

# CLI
installer cleanup --Target:Confirm=true
```

### Destroy (Menu Option 12)

Nuclear 6-step teardown using appsettings.json defaults:

1. Stop and delete the OS service (`Service:Name`)
2. Delete the service account (`ServiceAccount:Username`) — skips if it's the current user
3. Delete the install directory (`Service:InstallPath`)
4. Delete all publish folders (`publish/*`)
5. Remove the `.configured` marker
6. Clear the API key from the service's `appsettings.json`

Interactive: must type `DESTROY` to confirm. CLI: `--Target:Confirm=true`.

```bash
# Interactive
Select option 12, type DESTROY

# CLI
installer destroy --Target:Confirm=true
```

---

## Instructions & Help (Menu Option 10)

Interactive submenu with 10 detailed help topics:

| # | Topic |
|---|-------|
| 1 | Build (dotnet publish) |
| 2 | Full Deploy (build → configure → start) |
| 3 | Configure (install + set API key) |
| 4 | Remove (stop + uninstall) |
| 5 | Start / Stop / Status |
| 6 | Service Account Manager |
| 7 | Permissions Model (why admin is needed) |
| 8 | Service Accounts (least-privilege setup) |
| 9 | CI/CD Non-Interactive Mode |
| 10 | Configuration Overrides |

Also available as CLI commands:

```bash
dotnet run -- instructions    # Opens the interactive Instructions menu
dotnet run -- docs            # Alias for instructions
dotnet run -- help            # Prints the full CLI reference
```

---

## Configuration

### appsettings.json

```json
{
  "Service": {
    "Name": "FreeServicesMonitor",
    "DisplayName": "FreeServices System Monitor",
    "Description": "Periodically collects and logs system information.",
    "ExePath": "",
    "InstallPath": ""
  },
  "Publish": {
    "ProjectPath": "../FreeServices.Service",
    "OutputPath": "",
    "Runtime": "",
    "SelfContained": true,
    "SingleFile": true
  },
  "Recovery": {
    "RestartDelayMs": 5000,
    "ResetPeriodSeconds": 86400
  },
  "Security": {
    "ApiKey": ""
  },
  "ServiceAccount": {
    "Username": "",
    "Password": ""
  },
  "Systemd": {
    "UnitFilePath": "/etc/systemd/system/freeservices.service",
    "User": "root",
    "WorkingDirectory": "/opt/freeservices"
  },
  "Launchd": {
    "SystemWide": false,
    "Label": "com.wsu.eit.freeservices",
    "LogPath": "/tmp/freeservices.log",
    "ErrorLogPath": "/tmp/freeservices.error.log"
  }
}
```

> **Empty strings** (`ExePath`, `InstallPath`, `OutputPath`, `Runtime`) trigger platform-aware defaults in `InstallerConfig.cs`. The Installer auto-detects OS and architecture at runtime.

### Configuration Sections

| Section | Keys | Purpose |
|---------|------|---------|
| **Service** | `Name`, `DisplayName`, `Description`, `ExePath`, `InstallPath` | Service identity and metadata. Used by `sc.exe create`, systemd unit, launchd plist. `InstallPath` is where binaries are deployed. |
| **Publish** | `ProjectPath`, `OutputPath`, `Runtime`, `SelfContained`, `SingleFile` | `dotnet publish` parameters. `OutputPath` is the staging area; files are copied to `InstallPath` during configure. |
| **Recovery** | `RestartDelayMs`, `ResetPeriodSeconds` | Crash restart delay (Windows + systemd). Reset period for failure counter (Windows). |
| **Security** | `ApiKey` | API key written to the service's `appsettings.json` during configure. Cleared during remove. |
| **ServiceAccount** | `Username`, `Password` | Logon account for the service (Windows `sc.exe create obj=`). Linux maps to `Systemd:User`. |
| **Systemd** | `UnitFilePath`, `User`, `WorkingDirectory` | Linux-only. Where to write the unit file, which user runs the service, working directory. |
| **Launchd** | `SystemWide`, `Label`, `LogPath`, `ErrorLogPath` | macOS-only. User agent vs. system daemon, plist label, stdout/stderr log paths. |
| **Target** | `Username`, `Permission`, `ServiceName`, `Search`, `ContainerName`, `Confirm` | CLI-only. Used by account management, permission toggling, service/Docker control, and maintenance commands. Not persisted. |

### Platform-Aware Defaults (InstallerConfig.cs)

The `InstallerConfig.cs` typed model provides defaults that auto-detect the current platform:

| Setting | Windows | Linux | macOS |
|---------|---------|-------|-------|
| `ExePath` | `C:\FreeServices\FreeServices.Service.exe` | `/opt/freeservices/FreeServices.Service` | `/usr/local/bin/FreeServices.Service` |
| `InstallPath` | `C:\FreeServices` | `/opt/freeservices` | `/usr/local/bin` |
| `Runtime` | `win-x64` or `win-arm64` | `linux-x64` or `linux-arm64` | `osx-x64` or `osx-arm64` |

### Configuration Precedence

```
CLI args  →  appsettings.json  →  coded defaults in InstallerConfig.cs
```

CLI args always win. Use `dotnet run -- config` to see the fully resolved configuration.

---

## Non-Interactive / CI/CD Mode

When `--Security:ApiKey=<key>` is provided via CLI, the installer detects non-interactive mode and skips all prompts, using values from flags/config directly.

```bash
# Full deploy
installer.exe deploy --Security:ApiKey=$(API_KEY) --Service:Name=MyAgent

# Configure only
installer.exe configure --Security:ApiKey=$(API_KEY) --ServiceAccount:Username="NT AUTHORITY\NETWORK SERVICE"

# Remove
installer.exe remove --Security:ApiKey=$(OLD_KEY)

# Account lifecycle
installer.exe account-create --Target:Username=FreeServiceAgent --ServiceAccount:Password=s3cret
installer.exe account-lookup --Target:Username=FreeServiceAgent
installer.exe grant --Target:Username=FreeServiceAgent --Target:Permission=all
installer.exe revoke --Target:Username=FreeServiceAgent --Target:Permission=docker
installer.exe account-delete --Target:Username=FreeServiceAgent --Target:Confirm=true

# Service control
installer.exe svc-start --Target:ServiceName=docker
installer.exe docker-stop --Target:ContainerName=my-redis

# Maintenance
installer.exe cleanup --Target:Confirm=true
installer.exe destroy --Target:Confirm=true
```

---

## How It Works Internally

### Architecture

```
CLI args ─────┐
              │
appsettings ──┼──→ ConfigurationBuilder ──→ InstallerConfig ──→ RunAction()
              │                                                     │
              │                                    ┌────────────────┼────────────────┐
              │                                    ▼                ▼                ▼
              │                                 Windows          Linux            macOS
              │                                 sc.exe           systemd          launchd
              │
Interactive ──┘ (menu maps "1"→"build", "2"→"deploy", ..., "12"→"destroy")
```

### The RunAction() Pattern

Both the CLI path and the interactive menu path converge on a single method:

```csharp
static int RunAction(string action, InstallerConfig config)
{
    return action.ToLowerInvariant() switch
    {
        "build"     => ActionBuild(config),
        "deploy"    => ActionDeploy(config),
        "configure" or "install" => ActionConfigure(config),
        "remove"    or "uninstall" => ActionUninstall(config),
        "start"     => ActionStart(config),
        "stop"      => ActionStop(config),
        "status"    => ActionStatus(config),
        "config"    => ActionViewConfig(config),
        "users"     => ActionUsers(config),
        "instructions" or "docs" => ActionInstructions(),
        "help"      => ActionHelp(),
        "cleanup"   => ActionCleanup(config),
        "destroy"   => ActionDestroy(config),

        // Service Account Manager CLI equivalents
        "account-view"   => ActionAccountView(),
        "account-create" => ActionAccountCreate(config),
        "account-delete" => ActionAccountDelete(config),
        "account-lookup" => ActionAccountLookup(config),
        "grant"          => ActionGrant(config),
        "revoke"         => ActionRevoke(config),
        "svc-list"       => ActionSvcList(),
        "svc-search"     => ActionSvcSearch(config),
        "svc-start"      => ActionSvcControl(config, start: true),
        "svc-stop"       => ActionSvcControl(config, start: false),
        "docker-list"    => ActionDockerList(),
        "docker-start"   => ActionDockerControl(config, start: true),
        "docker-stop"    => ActionDockerControl(config, start: false),

        _           => ActionUnknown(action),
    };
}
```

Each action method internally routes to platform-specific implementations based on `RuntimeInformation.IsOSPlatform()`.

### Configure vs. Remove Flow

**Configure** (Azure DevOps agent-style):
1. Check `.configured` marker — blocks re-install if already configured
2. Prompt for service name, display name, API key (masked input)
3. Prompt for service account and install paths
4. Copy published files from publish dir → install dir
5. Write API key to the service's `appsettings.json`
6. Register the service with the OS (`sc.exe create` / systemd unit / launchd plist)
7. Write `.configured` marker file

**Remove** (reverse of configure):
1. Prompt for API key (authentication)
2. Stop the running service
3. Delete the service registration
4. Clear the API key from the service's `appsettings.json`
5. Remove the `.configured` marker

### Console Output

The Installer uses 6 semantic console output helpers for ADA/WCAG-compliant color coding:

| Helper | Color | Symbol | Usage |
|--------|-------|--------|-------|
| `WriteSuccess` | Green | ✓ | Operation succeeded |
| `WriteError` | Red | ✗ | Operation failed |
| `WriteWarning` | Yellow | ⚠ | Caution or advisory |
| `WriteInfo` | Cyan | ● | Informational detail |
| `WriteStep` | DarkCyan | » | Action in progress |
| `WriteDim` | DarkGray | (none) | Secondary/context text |

### Access Denied Handling

When operations fail with access denied errors, the Installer provides platform-specific least-privilege guidance via `PrintAccessDeniedGuidance()`, explaining exactly which elevation or permission is needed and how to obtain it.

---

## Files

| File | Purpose |
|------|---------|
| `FreeServices.Installer.csproj` | Console app targeting `net10.0`. References 4 `Microsoft.Extensions.Configuration` packages (base, Json, CommandLine, Binder). |
| `Program.cs` | The entire installer: config loading, CLI/menu routing, `RunAction()` dispatcher, all 27 action implementations, Service Account Manager, platform-specific install/uninstall for all 3 OSes, permissions, services/Docker control, cleanup, destroy, instructions, process runner helpers, console output helpers. |
| `InstallerConfig.cs` | Typed configuration model. 9 settings classes: `ServiceSettings`, `PublishSettings`, `RecoverySettings`, `SecuritySettings`, `ServiceAccountSettings`, `SystemdSettings`, `LaunchdSettings`, `TargetSettings`, and root `InstallerConfig`. Platform-aware defaults via static methods. |
| `appsettings.json` | Default configuration for all 7 sections (Service, Publish, Recovery, Security, ServiceAccount, Systemd, Launchd). Empty strings trigger platform-aware defaults. |

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Extensions.Configuration` | 10.0.0-preview.3 | Configuration builder base |
| `Microsoft.Extensions.Configuration.Json` | 10.0.0-preview.3 | `AddJsonFile()` for appsettings.json |
| `Microsoft.Extensions.Configuration.CommandLine` | 10.0.0-preview.3 | `AddCommandLine()` for CLI arg overrides |
| `Microsoft.Extensions.Configuration.Binder` | 10.0.0-preview.3 | `Bind()` to typed `InstallerConfig` model |

---

## Deployment Walkthrough

### Windows (Administrator Required)

```bash
# Full deploy: build → configure → start
dotnet run -- deploy

# Or step by step:
dotnet run -- build        # Publishes to publish/win-x64, then copied to C:\FreeServices
dotnet run -- configure    # Copies files, sets API key, creates Windows Service
dotnet run -- start        # Starts the service
dotnet run -- status       # Verifies it's running + shows log tail
```

### Linux (sudo Required)

```bash
# Full deploy
sudo dotnet run -- deploy

# Or step by step:
sudo dotnet run -- build              # Publishes to publish/linux-x64
sudo dotnet run -- configure          # Copies files, creates systemd unit file + enables
sudo dotnet run -- start              # Starts the service
dotnet run -- status                  # Shows systemctl status + log tail
```

### macOS (No Elevation for User Agent)

```bash
# Full deploy
dotnet run -- deploy

# Or step by step:
dotnet run -- build        # Publishes to publish/osx-arm64
dotnet run -- configure    # Copies files, creates launchd plist + loads
dotnet run -- start        # Starts the agent
dotnet run -- status       # Shows launchctl list + log tail
```

### Remove (Any Platform)

```bash
dotnet run -- remove
```

This authenticates via API key, stops the service (if running), removes the OS registration (sc.exe delete / systemctl disable + delete unit / launchctl unload + delete plist), clears the API key from the service config, and removes the `.configured` marker.

---

## Repurposing for Your Own Service

1. **Update `appsettings.json`** — change `Service.Name`, `Service.DisplayName`, `Service.Description` to match your service
2. **Update `Publish.ProjectPath`** — point to your service project (default is `../FreeServices.Service`)
3. **That's it** — the Installer doesn't know or care about what the service does internally

For deeper customization:
- Edit `InstallerConfig.cs` to change platform defaults or add new settings
- The systemd unit file template is in `InstallLinux()`
- The launchd plist template is in `InstallMacOS()`

---

## Prerequisites

- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Elevated privileges** — Administrator on Windows, sudo on Linux, user-level on macOS (by default)

---

## Related Projects

| Project | Purpose |
|---------|---------|
| **FreeServices.Service** | The background service that this Installer builds, deploys, and manages |
| **FreeServices.TestMe** | Automated integration tests (4 tests) that verify the full service and installer lifecycle |

---

## License

[MIT License](../LICENSE) — Washington State University, Enrollment Information Technology.
