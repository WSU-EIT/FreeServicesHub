# Agent Service Identity & Credentials — How It Works

## TL;DR

Your installer uses `sc.exe create` **without** an `obj=` parameter, so the service runs as **Local System** (`NT AUTHORITY\SYSTEM`). The Azure DevOps pipeline agent that *invokes* the installer also typically runs as Local System. This means it has full permission to create/start/stop services. **Yes, this will work.**

---

## What Happens Step-by-Step

### 1. Azure DevOps Pipeline Agent (the runner)

When you install an Azure DevOps self-hosted agent as a Windows service (the default for "Environment" VM resources), the installer asks for a logon account. The options are:

| Option | Identity | Notes |
|--------|----------|-------|
| **Default (recommended)** | `NT AUTHORITY\SYSTEM` | Full local admin. No password needed. |
| Network Service | `NT AUTHORITY\NETWORK SERVICE` | Lower privilege, authenticates as machine on network. |
| Custom account | Domain\User or local user | You provide the password at setup. |

Most teams (and yours) use the default → **Local System**.

You can verify this on the target VM:

```powershell
Get-CimInstance Win32_Service | Where-Object Name -like '*vstsagent*' | Select-Object Name, StartName
```

`StartName` will show `LocalSystem` or similar.

### 2. Your Pipeline Step Runs As...

When your pipeline YAML runs a `PowerShell@2` task on an Environment VM resource, that PowerShell process **inherits the identity of the Azure DevOps agent service**. So if the agent runs as Local System, your script runs as Local System.

### 3. Your Installer Creates the Service As...

In `Program.cs`, `InstallWindows()` does:

```csharp
var createArgs = $"create {config.Service.Name} binPath= \"{exePath}\" start= auto DisplayName= \"{config.Service.DisplayName}\"";
RunProcess("sc.exe", createArgs);
```

**No `obj=` parameter** → Windows defaults to `LocalSystem` (`NT AUTHORITY\SYSTEM`).

### 4. The FreeServicesHub Agent Service Runs As...

**`NT AUTHORITY\SYSTEM`** — same as the Azure DevOps agent.

---

## How This Compares to Azure DevOps / GitHub Actions

You're right — they're architecturally almost identical:

| Feature | Azure DevOps Agent | GitHub Actions Runner | Your Agent |
|---------|-------------------|----------------------|------------|
| Installer | `config.cmd` | `config.cmd` | `FreeServicesHub.Agent.Installer.exe` |
| Service creation | `sc.exe create` | `sc.exe create` | `sc.exe create` |
| Default identity | `NT AUTHORITY\SYSTEM` | `NT AUTHORITY\NETWORK SERVICE`* | `NT AUTHORITY\SYSTEM` |
| Recovery policy | Restart on failure | Restart on failure | Restart on failure (you set this) |
| Auto-start | Yes (`start= auto`) | Yes | Yes (`start= auto`) |

*GitHub defaults to Network Service but offers Local System during config.

Both Azure DevOps and GitHub's runners are open-source forks of the same codebase originally called "vsts-agent" (now `azure-pipelines-agent` / `actions-runner`). So yes, they share a common root.

---

## Will This Work?

**Yes**, given these conditions (all met in your pipeline):

| Requirement | Status |
|-------------|--------|
| Pipeline agent runs as Local System | ✅ Default for Environment VMs |
| `sc.exe create` permission | ✅ Local System has full admin |
| `sc.exe start` permission | ✅ Same |
| Write to `C:\WebRoot\CICD\...\Services\` | ✅ Local System has full filesystem access |
| Service runs and can make outbound HTTPS | ✅ Local System can do network I/O |

---

## Security Considerations

| Concern | Risk | Mitigation |
|---------|------|------------|
| Local System is overprivileged | Medium | Agent only does heartbeats + system stats. Low attack surface. |
| API key in appsettings.json on disk | Medium | File is on the server, readable only by admins. Rotated each pipeline run. |
| Service can access all local resources | Low | No user data access needed. Consider `NETWORK SERVICE` if you want least privilege later. |

### Optional: Run Under Network Service Instead

If you ever want to reduce privilege, add `obj=` to the `sc.exe create` call in `InstallWindows()`:

```csharp
var createArgs = $"create {config.Service.Name} binPath= \"{exePath}\" start= auto obj= \"NT AUTHORITY\\NetworkService\" DisplayName= \"{config.Service.DisplayName}\"";
```

This would still allow outbound HTTPS (for SignalR + heartbeats) but would block local admin operations. Fine for your use case.

---

## The Actual Error You Hit

The `--NonInteractive` error was **not** an identity/permissions issue. It was a CLI parsing bug — .NET's `AddCommandLine(args)` treated `--NonInteractive` as a key and consumed the next argument as its value. Fixed by using `--NonInteractive=true`.

The pipeline agent had full permissions to create the service the entire time.

---

## Verify After Deployment

On the target VM, run:

```powershell
# Check the FreeServicesHub Agent service
Get-CimInstance Win32_Service | Where-Object Name -eq 'FreeServicesHubAgent' | Select-Object Name, State, StartName, PathName

# Check the Azure DevOps agent for comparison
Get-CimInstance Win32_Service | Where-Object Name -like '*vstsagent*' | Select-Object Name, State, StartName
```

Both should show `StartName = LocalSystem` and `State = Running`.
