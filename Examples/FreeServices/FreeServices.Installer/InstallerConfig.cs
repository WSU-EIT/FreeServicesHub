// FreeServices.Installer — InstallerConfig.cs
// Typed configuration model matching appsettings.json structure.
// Every property is overridable via CLI args (e.g., --Service:Name=MyService).
// Defaults are platform-aware — detects OS at construction time.

using System.Runtime.InteropServices;

namespace FreeServices.Installer;

/// <summary>
/// Root configuration model for the Installer.
/// </summary>
public sealed class InstallerConfig
{
    public ServiceSettings Service { get; set; } = new();
    public PublishSettings Publish { get; set; } = new();
    public RecoverySettings Recovery { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public ServiceAccountSettings ServiceAccount { get; set; } = new();
    public SystemdSettings Systemd { get; set; } = new();
    public LaunchdSettings Launchd { get; set; } = new();
    public TargetSettings Target { get; set; } = new();
}

/// <summary>
/// Service identity and metadata (all platforms).
/// </summary>
public sealed class ServiceSettings
{
    public string Name { get; set; } = "FreeServicesMonitor";
    public string DisplayName { get; set; } = "FreeServices System Monitor";
    public string Description { get; set; } = "Periodically collects and logs system information.";
    public string ExePath { get; set; } = GetDefaultExePath();

    /// <summary>
    /// The directory where the service binaries are installed and run from.
    /// This is separate from the publish output — files are copied here during configure.
    /// </summary>
    public string InstallPath { get; set; } = GetDefaultInstallPath();

    private static string GetDefaultInstallPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return @"C:\FreeServices";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "/usr/local/bin";
        return "/opt/freeservices";
    }

    private static string GetDefaultExePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return @"C:\FreeServices\FreeServices.Service.exe";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "/usr/local/bin/FreeServices.Service";
        return "/opt/freeservices/FreeServices.Service";
    }
}

/// <summary>
/// dotnet publish configuration.
/// </summary>
public sealed class PublishSettings
{
    public string ProjectPath { get; set; } = "../FreeServices.Service";

    /// <summary>
    /// The directory where dotnet publish outputs files.
    /// Defaults to a "publish/{runtime}" folder relative to the installer directory.
    /// This is a staging area — files are later copied to Service.InstallPath during configure.
    /// </summary>
    public string OutputPath { get; set; } = "";
    public string Runtime { get; set; } = GetDefaultRuntime();
    public bool SelfContained { get; set; } = true;
    public bool SingleFile { get; set; } = true;

    private static string GetDefaultRuntime()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            _ => "x64",
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"win-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"osx-{arch}";
        return $"linux-{arch}";
    }
}

/// <summary>
/// Service crash recovery settings (Windows sc.exe / systemd Restart=).
/// </summary>
public sealed class RecoverySettings
{
    public int RestartDelayMs { get; set; } = 5000;
    public int ResetPeriodSeconds { get; set; } = 86400;
}

/// <summary>
/// Linux systemd unit file settings.
/// </summary>
public sealed class SystemdSettings
{
    public string UnitFilePath { get; set; } = "/etc/systemd/system/freeservices.service";
    public string User { get; set; } = "root";
    public string WorkingDirectory { get; set; } = "/opt/freeservices";
}

/// <summary>
/// macOS launchd plist settings.
/// </summary>
public sealed class LaunchdSettings
{
    /// <summary>
    /// Whether to install as a system daemon (/Library/LaunchDaemons) or user agent (~/Library/LaunchAgents).
    /// </summary>
    public bool SystemWide { get; set; } = false;

    public string Label { get; set; } = "com.wsu.eit.freeservices";
    public string LogPath { get; set; } = "/tmp/freeservices.log";
    public string ErrorLogPath { get; set; } = "/tmp/freeservices.error.log";
}

/// <summary>
/// Security settings for agent authentication and key rotation.
/// The API key is written to the service's appsettings.json on install.
/// </summary>
public sealed class SecuritySettings
{
    public string ApiKey { get; set; } = "";
}

/// <summary>
/// Service account identity. On Windows this is the logon account for sc.exe.
/// On Linux this maps to the systemd User= directive.
/// </summary>
public sealed class ServiceAccountSettings
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

/// <summary>
/// CLI target settings for non-interactive sub-commands.
/// Used by account management, permission toggling, and service/Docker control.
/// Example: --Target:Username=FreeServiceAgent --Target:Permission=docker
/// </summary>
public sealed class TargetSettings
{
    /// <summary>Target username for account-create, account-delete, grant, revoke.</summary>
    public string Username { get; set; } = "";

    /// <summary>Permission key for grant/revoke: svc, docker, install, stats, apps, all.</summary>
    public string Permission { get; set; } = "";

    /// <summary>Service name for svc-start, svc-stop.</summary>
    public string ServiceName { get; set; } = "";

    /// <summary>Search term for svc-search.</summary>
    public string Search { get; set; } = "";

    /// <summary>Container name or ID for docker-start, docker-stop.</summary>
    public string ContainerName { get; set; } = "";

    /// <summary>Skip confirmation prompts (for CI/CD).</summary>
    public bool Confirm { get; set; } = false;
}
