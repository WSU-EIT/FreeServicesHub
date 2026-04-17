# 302 — Deep Dive: Agent Installer

> **Document ID:** 302  
> **Category:** Reference — Deep Dive  
> **Investigator:** Agent 2 (Installer & Deployment Specialist)  
> **Scope:** `FreeServicesHub.Agent.Installer` project — the CLI/UI tool for building, deploying, and managing the agent  
> **Outcome:** Complete understanding of the installer's dual-mode interface, service management, and CI/CD integration points.

---

## Executive Summary

`FreeServicesHub.Agent.Installer` is a .NET 10 console app that serves as both an interactive menu and a headless CLI for building, deploying, and managing the `FreeServicesHub.Agent` Windows Service. It is a streamlined, Windows-only fork of the cross-platform `FreeServices.Installer`, stripped down to focus on the specific needs of the agent deployment pipeline.

---

## Project Structure

| File | Purpose |
|------|---------|
| `FreeServicesHub.Agent.Installer.csproj` | .NET 10 console app |
| `InstallerConfig.cs` | Typed configuration model (`ServiceSettings`, `PublishSettings`, `SecuritySettings`) |
| `Program.cs` | All logic — ~600 lines of top-level statements and static methods |
| `appsettings.json` | Default configuration (overridable via CLI args) |

---

<a id="configuration-model"></a>
## Configuration Model (`InstallerConfig`)

```
InstallerConfig
├── Service: ServiceSettings
│   ├── Name           = "FreeServicesHubAgent"
│   ├── DisplayName    = "FreeServicesHub Agent"
│   ├── Description    = "Agent that connects to FreeServicesHub..."
│   ├── ExePath        = @"C:\FreeServicesHubAgent\FreeServicesHub.Agent.exe"
│   └── InstallPath    = @"C:\FreeServicesHubAgent"
├── Publish: PublishSettings
│   ├── ProjectPath    = "../FreeServicesHub.Agent"
│   ├── OutputPath     = "" (auto-resolved to {sln_root}/publish/win-x64)
│   ├── Runtime        = "win-x64"
│   ├── SelfContained  = true
│   └── SingleFile     = true
└── Security: SecuritySettings
    └── ApiKey          = "" (the registration key for agent auth)
```

**Key difference from FreeServices.Installer:** No `RecoverySettings`, `ServiceAccountSettings`, `SystemdSettings`, `LaunchdSettings`, or `TargetSettings`. This is Windows-only by design.

Every property is overridable via CLI: `--Service:Name=MyAgent --Security:ApiKey=abc123`

---

<a id="dual-mode"></a>
## Dual-Mode Interface

### Routing Logic

```
args present?
├── YES → Extract first non-"--" arg as action → RunAction(action, config)
└── NO  → RunInteractive(config) → menu loop
```

### CLI Mode (Non-Interactive)

```bash
# Build the agent
dotnet run -- build

# Full headless install (CI/CD mode)
dotnet run -- configure --Security:ApiKey=<reg-key>

# Remove
dotnet run -- remove --Security:ApiKey=<key>

# Service control
dotnet run -- start
dotnet run -- stop
dotnet run -- status

# Nuclear option
dotnet run -- destroy --Security:ApiKey=<key>
```

When `Security:ApiKey` is provided via CLI, all interactive prompts are skipped — the installer runs fully headless. This is the CI/CD integration point.

### Interactive Mode (Menu)

```
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
```

The menu shows a status header (CONFIGURED/NOT CONFIGURED based on `.configured` marker file) and all current configuration values.

---

<a id="actions"></a>
## Action Reference

### `build` — Publish the Agent

```
1. Validate project path exists
2. Clean publish output directory
3. dotnet publish "{projectPath}" -c Release -r win-x64 --self-contained -o "{outputPath}"
   + -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Produces a single-file, self-contained `FreeServicesHub.Agent.exe` + `appsettings.json`.

### `configure` (alias: `install`) — Full Install Pipeline

```
1. Guard: reject if already configured (.configured marker exists)
2. Prompt/accept service name
3. Prompt/accept API key (masked input or --Security:ApiKey)
4. Prompt/accept publish path and install path
5. Derive ExePath = InstallPath + "FreeServicesHub.Agent.exe"
6. CopyPublishToInstall() — copy all files from publish output to install path
7. WriteApiKeyToServiceConfig() — inject RegistrationKey into appsettings.json
8. InstallWindows() — sc.exe create + description + failure recovery
9. WriteConfiguredMarker() — write .configured JSON file
```

### `remove` (alias: `uninstall`) — Clean Uninstall

```
1. Prompt/accept API key
2. sc.exe stop {serviceName}
3. Wait 2 seconds
4. sc.exe delete {serviceName}
5. Remove .configured marker
6. Clear API key from appsettings.json (set RegistrationKey="" and ApiClientToken="")
```

### `start` / `stop` — Service Control

Simple `sc.exe start/stop {serviceName}` with access-denied guidance.

### `status` — Query + Log Tail

```
1. sc.exe query {serviceName}
2. Read last 10 lines of {installPath}/agent.log (if exists)
```

### `destroy` — Nuclear Undo

```
1. Confirmation prompt (type "DESTROY") — skipped in non-interactive mode
2. [1/4] sc.exe stop + sc.exe delete
3. [2/4] Delete install directory recursively
4. [3/4] Delete publish directories recursively
5. [4/4] Delete .configured marker
```

---

<a id="windows-service-install"></a>
## Windows Service Installation Detail

The `InstallWindows()` function issues four `sc.exe` commands:

```
sc.exe create FreeServicesHubAgent
    binPath= "C:\FreeServicesHubAgent\FreeServicesHub.Agent.exe"
    start= auto
    DisplayName= "FreeServicesHub Agent"

sc.exe description FreeServicesHubAgent "Agent that connects to..."

sc.exe failure FreeServicesHubAgent
    reset= 86400
    actions= restart/5000/restart/5000/restart/5000

sc.exe failureflag FreeServicesHubAgent 1
```

**`start= auto`** — service starts with Windows, no login required.

**Failure recovery** — three restart attempts at 5-second intervals, counter resets every 24 hours.

**`failureflag 1`** — ensures non-zero exit codes (not just crashes) trigger recovery.

---

<a id="api-key-injection"></a>
## API Key Injection

The installer writes the registration key into the agent's `appsettings.json`:

```json
{
  "Agent": {
    "RegistrationKey": "<the-key>"
  }
}
```

It checks three candidate paths in order:
1. `{InstallPath}/appsettings.json` (primary — the installed copy)
2. `{PublishOutputPath}/appsettings.json` (staging area)
3. `{ProjectPath}/appsettings.json` (source — development copy)

On uninstall, it clears both `RegistrationKey` and `ApiClientToken` from all three candidates.

---

<a id="configured-marker"></a>
## Configured Marker (`.configured`)

A JSON file at `{InstallPath}/.configured`:

```json
{
  "ConfiguredAt": "2025-01-15T10:30:00.0000000Z",
  "ServiceName": "FreeServicesHubAgent",
  "Platform": "Windows"
}
```

Used as a guard: `configure` refuses to run if the marker exists — you must `remove` first. This prevents accidental double-installs.

---

<a id="path-resolution"></a>
## Path Resolution Strategy

### Project Path

When running from `bin\Debug\net10.0\`, the default `"../FreeServicesHub.Agent"` would resolve incorrectly. The resolver walks up from `AppContext.BaseDirectory` looking for a directory matching the target name.

### Publish Output Path

If `OutputPath` is empty (default), the resolver walks up from `AppContext.BaseDirectory` looking for a `.slnx` or `.sln` file, then sets `OutputPath = {slnRoot}/publish/{runtime}`.

---

<a id="comparison-to-freeservices"></a>
## Comparison to FreeServices.Installer

| Aspect | FreeServices.Installer | FreeServicesHub.Agent.Installer |
|--------|----------------------|--------------------------------|
| Platform | Windows + Linux + macOS | Windows only |
| Menu options | 12 (build, deploy, configure, remove, start, stop, status, config, users, instructions, cleanup, destroy) | 7 (build, configure, remove, start, stop, status, destroy) |
| Service accounts | Full user management (create, delete, lookup, grant, revoke) | None |
| Docker support | List, start, stop containers | None |
| Config model | `ServiceSettings`, `PublishSettings`, `RecoverySettings`, `SecuritySettings`, `ServiceAccountSettings`, `SystemdSettings`, `LaunchdSettings`, `TargetSettings` | `ServiceSettings`, `PublishSettings`, `SecuritySettings` |
| Deploy action | Yes (build + configure + start) | No (manual sequence) |
| API key handling | Same pattern | Same pattern |
| `sc.exe` commands | Same pattern | Same pattern |

The Agent Installer is intentionally a stripped-down derivative. The FreeServices.Installer is the "full" reference — the Agent Installer takes only what's needed for the agent use case.

---

<a id="cicd-integration"></a>
## CI/CD Integration Model

The installer is designed to be called from a CI/CD pipeline (Azure DevOps YAML) in fully non-interactive mode:

```yaml
# Conceptual pipeline step
- script: |
    FreeServicesHub.Agent.Installer.exe build
    FreeServicesHub.Agent.Installer.exe configure --Security:ApiKey=$(AGENT_REG_KEY)
    FreeServicesHub.Agent.Installer.exe start
```

The current FreeServices model (from the user's description): the pipeline builds and drops files to a target, then a sysadmin runs the installer exe manually. The FreeServicesHub.Agent.Installer is designed to close that gap — the pipeline can run the full install headlessly.

---

*Prev: [301 — Agent Worker Service](301_deepdive_agent_worker_service.md) | Next: [303 — Hub Server Infrastructure](303_deepdive_hub_server_infrastructure.md)*
