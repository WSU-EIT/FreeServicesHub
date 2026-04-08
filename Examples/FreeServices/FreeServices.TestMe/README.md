# FreeServices.TestMe

An automated integration test harness that validates the entire FreeServices lifecycle — from build through execution to teardown. Provides four test modes that cover developer workflow, production deployment, installer CLI automation, and full feature showcase across all platforms.

**Test 1** — Console mode (any OS, no elevation) · **Test 2** — Platform service mode (real service install) · **Test 3** — Installer CLI non-interactive (configure/remove) · **Test 4** — CLI feature showcase (account lifecycle)

Developed by **[Enrollment Information Technology (EIT)](https://em.wsu.edu/eit/meet-our-staff/)** at **Washington State University**.

---

## What This Is

TestMe launches FreeServices components in controlled conditions, validates behavior at each step, and reports pass/fail. It is designed to run:

- **Locally** — by a developer verifying changes before commit
- **In CI/CD** — by Azure DevOps pipelines via the `FileTransform@2` task, which injects pipeline variables into `appsettings.json` before tests run

All four tests are fully automated. No human interaction is required.

---

## Quick Start

### Run All Tests

```bash
dotnet run
```

### Run a Single Test

```bash
dotnet run -- --test=1    # Console mode lifecycle
dotnet run -- --test=2    # Platform service lifecycle (admin/sudo)
dotnet run -- --test=3    # Installer CLI non-interactive mode
dotnet run -- --test=4    # CLI feature showcase (admin/sudo)
```

### Run with Custom Parameters

```bash
dotnet run -- --test=1 --heartbeats=5 --interval=2 --timeout=120
dotnet run -- --test=3 --installerdir=../FreeServices.Installer
```

### Expected Output (Test 1)

```
═══ FREESERVICES TEST HARNESS ═══

  Service Project:   C:\repos\FreeServices\FreeServices.Service
  Installer Project: C:\repos\FreeServices\FreeServices.Installer
  Heartbeats:        3
  Interval:          2s
  Timeout:           60s
  Build Config:      Release
  Config Source:      appsettings.json → user secrets → env → CLI

── Test 1: Console Mode Lifecycle ──

  [1/4] Cleaning...
  [2/4] Building...
  [3/4] Launching service (interval=2s)...
         PID: 12345
  [4/4] Waiting for 3 heartbeats (timeout: 60s)...

         Captured 3/3 heartbeats...

  Output length: 4523 chars
  Iterations found: 3
  ✓ Test 1 PASSED — captured 3 heartbeats.

═══ ALL TESTS PASSED ═══
```

---

## Test Modes

### Test 1: Console Mode Lifecycle

**Requirements:** .NET SDK only (no elevation, any OS)

| Step | What Happens |
|------|-------------|
| 1. Clean | `dotnet clean` the Service project |
| 2. Build | `dotnet build` in the configured build configuration |
| 3. Launch | Starts the Service as a background process via `dotnet run --no-build`, passing `--Service:IntervalSeconds` to override the heartbeat interval |
| 4. Monitor | Captures stdout asynchronously with a 4KB buffer, counts `"Iteration "` occurrences every 500ms until the target heartbeat count is reached or timeout expires |
| — | Kills the entire process tree (`Kill(entireProcessTree: true)`), reports pass/fail |

**Pass criteria:** The captured output contains at least `heartbeats` occurrences of the string `"Iteration "` before `timeout` seconds elapse.

**On failure:** Dumps the last 2000 characters of captured output for debugging.

### Test 2: Platform Service Lifecycle

**Requirements:** Elevated privileges — Administrator (Windows), sudo (Linux), or user-level (macOS)

A 14-step lifecycle test with verification at every transition:

```
Publish → Install → Verify → Start → Heartbeats →
Stop → Verify stopped → Restart → Heartbeats →
Stop → Verify stopped → Uninstall → Verify removed
```

| Step | What Happens |
|------|-------------|
| 1. Clean | Deletes the temp publish directory (`%TEMP%\FreeServicesTest`) |
| 2. Publish | `dotnet publish` as self-contained for the current platform runtime |
| 3. Install | Creates a real platform service (`sc.exe create ... start= demand` / systemd unit / launchd plist) with the name `FreeServicesTestMe` |
| 4. Verify install | Queries the OS service manager to confirm the service is registered |
| 5. Start | Starts the service via the platform service manager |
| 6. Monitor | Reads the service log file, counts `"Iteration "` occurrences every 1s |
| 7. Stop | Stops the service |
| 8. Verify stop | Waits one interval + 2 seconds, confirms heartbeat count is stable (not growing) |
| 9. Restart | Starts the service again |
| 10. Monitor restart | Waits for `heartbeats` new iterations after restart |
| 11. Stop | Stops the service again |
| 12. Verify stop | Same stable-heartbeat verification as step 8 |
| 13. Uninstall | Removes the service registration |
| 14. Verify removal | Queries the OS service manager to confirm the service no longer exists |

**Pass criteria:** All 14 steps complete without failure. The service starts, produces heartbeats, stops cleanly (heartbeats stop growing), restarts successfully, and is fully removed.

**Cleanup:** Runs in a `finally` block — `CleanupTest2()` calls `UninstallTestService()` and deletes the temp directory even on failure.

**Platform-specific details:**

| Platform | Service Name | Install Method | Cleanup |
|----------|-------------|----------------|---------|
| Windows | `FreeServicesTestMe` | `sc.exe create ... start= demand` | `sc.exe stop` → `sc.exe delete` |
| Linux | `FreeServicesTestMe` | Write unit file to `/etc/systemd/system/` → `daemon-reload` | `systemctl stop` → `disable` → delete unit → `daemon-reload` |
| macOS | `com.freeservices.testme` | Write plist to `~/Library/LaunchAgents/` → `launchctl load` | `launchctl unload` → delete plist |

### Test 3: Installer CLI Non-Interactive Mode

**Requirements:** .NET SDK only (no elevation needed for the test logic itself; service install step may fail without admin, which the test handles gracefully)

A 9-step test that validates the Installer's configure/remove CLI flow without requiring admin:

| Step | What Happens |
|------|-------------|
| 1. Clean | Deletes temp test directories (`%TEMP%\FreeServicesInstallerTest`, `%TEMP%\FreeServicesAgentTest`) |
| 2. Publish | `dotnet publish` the Installer as self-contained |
| 3. Configure | Runs `installer configure --Security:ApiKey=<key> --Publish:OutputPath=... --Service:ExePath=...` |
| 4. Verify marker | Checks that `.configured` marker file was created. If not (expected without admin), creates it manually to test remaining flow |
| 5. Verify API key | Checks that the API key was written to the service's `appsettings.json` |
| 6. Re-configure guard | Runs configure again — expects exit code ≠ 0 and "already configured" message |
| 7. Remove | Runs `installer remove --Security:ApiKey=<key>` |
| 8. Verify marker removed | Confirms `.configured` marker was deleted |
| 9. Verify API key cleared | Confirms API key was removed from service `appsettings.json` |

**What this validates:**
- The installer binary runs end-to-end with CLI flags
- Non-interactive mode skips all prompts when `--Security:ApiKey` is provided
- The `.configured` marker is written during configure and removed during remove
- The API key is written to the service's `appsettings.json` during configure and cleared during remove
- Re-running configure when already configured returns an error
- Graceful handling when service install requires elevation (the test adapts)

**Pass criteria:** The configure/remove flow completes, marker lifecycle is correct, API key is written and cleared.

### Test 4: Installer CLI Feature Showcase

**Requirements:** Administrator (Windows) or sudo (Linux/macOS) — creates real OS users and modifies permissions

A 14-step, 6-phase test that exercises the full Service Account Manager CLI:

| Phase | Steps | What Happens |
|-------|-------|-------------|
| **A: Reconnaissance** | 1-5 | Publishes installer, then runs read-only commands: `account-view`, `status`, `config`, `svc-list`, `docker-list` |
| **B: Create Account** | 6 | `account-create --Target:Username=FsTestAgent --ServiceAccount:Password=<random> --Target:Confirm=true` |
| **C: Verify Account** | 7 | `account-lookup --Target:Username=FsTestAgent` → expects exit 0, output contains "EXISTS" |
| **D: Grant & Verify** | 8-9 | `grant --Target:Permission=svc`, `grant --Target:Permission=install`, then `account-lookup` to verify |
| **E: Revoke & Verify** | 10 | `revoke --Target:Permission=svc`, then `account-lookup` to verify |
| **F: Delete & Verify** | 11-14 | `account-delete --Target:Username=FsTestAgent --Target:Confirm=true`, then `account-lookup` → expects exit 1, "NOT FOUND" |

**Test username:** `FsTestAgent` (created and deleted during the test)
**Test password:** Random `Fs!Test` + 8-char GUID suffix (Windows only; Linux uses `useradd -r` with no password)

**Pass criteria:** Account is created, verified via lookup, permissions are granted/revoked, account is deleted and confirmed gone.

**Cleanup:** `finally` block deletes the test user if it still exists (`net user /delete` / `userdel` / `sysadminctl -deleteUser`) and removes the temp publish directory.

---

## Configuration

### Configuration Chain

TestMe loads configuration in this order (each layer overrides the previous):

```
1. appsettings.json          Base defaults
2. User Secrets              Developer-specific overrides (assembly-based, ID: freeservices-testme-dev)
3. Environment Variables      FREESERVICES_ prefix (e.g., FREESERVICES_TestSettings__Test=1)
4. CLI Arguments             Highest priority — always wins
```

### appsettings.json

```json
{
  "TestSettings": {
    "Test": 0,
    "Heartbeats": 3,
    "Interval": 2,
    "Timeout": 60,
    "BuildConfiguration": "Release",
    "ServiceProjectDir": "",
    "InstallerProjectDir": ""
  }
}
```

### Settings Reference

| Key | Default | CLI Flag | Description |
|-----|---------|----------|-------------|
| `Test` | `0` | `--test` | Which test to run: `0` = all, `1` = console only, `2` = service only, `3` = installer CLI, `4` = feature showcase |
| `Heartbeats` | `3` | `--heartbeats` | Number of service iterations to wait for before declaring success (Tests 1 & 2) |
| `Interval` | `2` | `--interval` | Override service heartbeat interval (seconds). Passed to the Service as `--Service:IntervalSeconds` (Tests 1 & 2) |
| `Timeout` | `60` | `--timeout` | Max wait time (seconds) before declaring failure (Tests 1 & 2) |
| `BuildConfiguration` | `Release` | `--config` | Build configuration: `Release` or `Debug` |
| `ServiceProjectDir` | `""` (auto-detect) | `--servicedir` | Path to the Service `.csproj` directory. Empty = auto-detect relative to `AppContext.BaseDirectory` |
| `InstallerProjectDir` | `""` (auto-detect) | `--installerdir` | Path to the Installer `.csproj` directory. Empty = auto-detect relative to `AppContext.BaseDirectory`. Used by Tests 3 & 4. |

### CLI Switch Mappings

The `--flag` shortcuts are mapped to their full configuration paths:

```csharp
{ "--test",         "TestSettings:Test" },
{ "--heartbeats",   "TestSettings:Heartbeats" },
{ "--interval",     "TestSettings:Interval" },
{ "--timeout",      "TestSettings:Timeout" },
{ "--config",       "TestSettings:BuildConfiguration" },
{ "--servicedir",   "TestSettings:ServiceProjectDir" },
{ "--installerdir", "TestSettings:InstallerProjectDir" },
```

### User Secrets

TestMe supports .NET user secrets for developer-specific configuration that shouldn't be committed:

```bash
# Initialize (already done — UserSecretsId is in .csproj)
dotnet user-secrets set "TestSettings:Heartbeats" "5"
dotnet user-secrets set "TestSettings:Interval" "1"
```

User secrets are loaded via `AddUserSecrets(Assembly.GetExecutingAssembly())` so they work even in top-level program files without a `Startup` class.

### Environment Variables

Use the `FREESERVICES_` prefix with double-underscore section separators:

```bash
export FREESERVICES_TestSettings__Test=1
export FREESERVICES_TestSettings__Heartbeats=5
dotnet run
```

---

## Project Auto-Detection

When `ServiceProjectDir` or `InstallerProjectDir` is empty (default), TestMe navigates from `AppContext.BaseDirectory` four levels up, then into the sibling project directory:

```
bin/Release/net10.0/ → ../../../../FreeServices.Service
bin/Release/net10.0/ → ../../../../FreeServices.Installer
```

This works because the standard .NET build output is always `bin/{Config}/{TFM}/`, and the Service and Installer projects are sibling directories.

Both directories are validated at startup — if either is not found, the test harness exits with an error message and exit code 1.

---

## Pipeline Integration

### Azure DevOps — FileTransform@2

In the CI/CD pipeline, `variables.yml` defines the test parameters:

```yaml
variables:
  - name: TestSettings.Test
    value: '0'
  - name: TestSettings.Heartbeats
    value: '3'
  - name: TestSettings.Interval
    value: '2'
  - name: TestSettings.Timeout
    value: '60'
  - name: TestSettings.BuildConfiguration
    value: 'Release'
  - name: TestSettings.ServiceProjectDir
    value: ''
  - name: TestSettings.InstallerProjectDir
    value: ''
```

The `FileTransform@2` task injects these into `appsettings.json` before tests run:

```yaml
- task: FileTransform@2
  displayName: 'Inject test variables into appsettings.json'
  inputs:
    folderPath: '$(Build.SourcesDirectory)/FreeServices/FreeServices.TestMe'
    jsonTargetFiles: 'appsettings.json'
```

This means:
- Pipeline variables → injected into `appsettings.json` → TestMe reads them as config
- No CLI flags needed in the pipeline run step
- The same `appsettings.json` code path works locally and in CI

### Pipeline Test Jobs

The Azure DevOps pipeline runs 3 test jobs:

| Job | Test | Agent | Notes |
|-----|------|-------|-------|
| Test1 | `--test=1` | Any | Console mode, no elevation needed |
| Test3 | `--test=3` | Any | Installer CLI, no elevation needed |
| Test4 | `--test=4` | Windows (admin) | Feature showcase, creates real OS users |

Test 2 is not run in CI by default because it requires admin to register a real OS service.

---

## How It Works Internally

### Heartbeat Detection

Both Tests 1 and 2 count occurrences of the literal string `"Iteration "` in the output (Test 1 captures stdout, Test 2 reads a log file). The Service prints `"Iteration N"` at the start of each heartbeat cycle, making this a reliable marker.

### Process Management (Test 1)

- Launches via `Process.Start` with `RedirectStandardOutput = true`
- Reads stdout asynchronously on a background task using `ReadAsync` with a 4KB buffer
- Thread-safe output accumulation using `lock` on a `List<string>`
- Kills the entire process tree (`Kill(entireProcessTree: true)`) on completion or timeout

### Stop Verification (Test 2)

After issuing a stop command, Test 2 waits 3 seconds, then:
1. Records the current heartbeat count from the log file
2. Waits one interval + 2 seconds
3. Checks the heartbeat count again
4. Passes if the count grew by at most 1 (allows one in-flight heartbeat)

This confirms the service actually stopped and isn't still producing output.

### Cleanup Safety

**Test 2:** Wraps all operations in `try/finally`. `CleanupTest2()` calls `UninstallTestService()` and deletes the temp publish directory — called in both the success path and the `finally` block (idempotent).

**Test 3:** `finally` block deletes both temp directories (`FreeServicesInstallerTest`, `FreeServicesAgentTest`).

**Test 4:** `finally` block force-deletes the test user (`FsTestAgent`) using OS commands and removes the temp publish directory.

### Graceful Degradation (Test 3)

Test 3 is designed to work without admin privileges:
- If the service install step fails (expected without admin), Test 3 creates the `.configured` marker manually
- If the API key wasn't written (because configure failed early), Test 3 notes this and continues
- The re-configure guard and remove flow are still tested with the manually created marker

---

## Files

| File | Purpose |
|------|---------|
| `FreeServices.TestMe.csproj` | Console app targeting `net10.0`. References 6 Configuration packages + UserSecretsId. |
| `Program.cs` | Entire test harness: config loading, CLI mapping, Test 1 (console lifecycle), Test 2 (14-step platform service lifecycle), Test 3 (9-step installer CLI), Test 4 (14-step CLI feature showcase), platform-specific service operations, process management helpers. |
| `appsettings.json` | Default test parameters. 7 settings including `InstallerProjectDir`. Overwritten by `FileTransform@2` in CI/CD pipelines. |

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Extensions.Configuration` | 10.0.0-preview.3 | Configuration builder base |
| `Microsoft.Extensions.Configuration.Json` | 10.0.0-preview.3 | `AddJsonFile()` for appsettings.json |
| `Microsoft.Extensions.Configuration.CommandLine` | 10.0.0-preview.3 | `AddCommandLine()` with switch mappings |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | 10.0.0-preview.3 | `AddEnvironmentVariables()` with `FREESERVICES_` prefix |
| `Microsoft.Extensions.Configuration.UserSecrets` | 10.0.0-preview.3 | `AddUserSecrets()` for developer overrides |
| `Microsoft.Extensions.Configuration.Binder` | 10.0.0-preview.3 | `GetValue<T>()` for typed access |

---

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | All requested tests passed |
| `1` | One or more tests failed, or a required project directory was not found |

---

## Prerequisites

- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Test 1** — no additional requirements
- **Test 2** — Administrator (Windows), sudo (Linux), or user-level (macOS)
- **Test 3** — no additional requirements (gracefully handles missing admin)
- **Test 4** — Administrator (Windows) or sudo (Linux/macOS)

---

## Related Projects

| Project | Purpose |
|---------|---------|
| **FreeServices.Service** | The background service being tested (Tests 1 & 2) |
| **FreeServices.Installer** | The installer CLI being tested (Tests 3 & 4). Builds, deploys, and manages the Service as a platform daemon. |

---

## License

[MIT License](../LICENSE) — Washington State University, Enrollment Information Technology.
