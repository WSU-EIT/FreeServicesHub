// FreeServicesHub.Agent.Installer -- InstallerConfig.cs
// Typed configuration model matching appsettings.json structure.
// Every property is overridable via CLI args (e.g., --Service:Name=MyAgent).
// Windows only -- no systemd, no launchd.

namespace FreeServicesHub.Agent.Installer;

/// <summary>
/// Root configuration model for the Agent Installer.
/// </summary>
public sealed class InstallerConfig
{
    public ServiceSettings Service { get; set; } = new();
    public PublishSettings Publish { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
}

/// <summary>
/// Service identity and metadata (Windows).
/// </summary>
public sealed class ServiceSettings
{
    public string Name { get; set; } = "FreeServicesHubAgent";
    public string DisplayName { get; set; } = "FreeServicesHub Agent";
    public string Description { get; set; } = "Agent that connects to FreeServicesHub and reports system status.";
    public string ExePath { get; set; } = @"C:\FreeServicesHubAgent\FreeServicesHub.Agent.exe";
    public string InstallPath { get; set; } = @"C:\FreeServicesHubAgent";
}

/// <summary>
/// dotnet publish configuration.
/// </summary>
public sealed class PublishSettings
{
    public string ProjectPath { get; set; } = "../FreeServicesHub.Agent";
    public string OutputPath { get; set; } = "";
    public string Runtime { get; set; } = "win-x64";
    public bool SelfContained { get; set; } = true;
    public bool SingleFile { get; set; } = true;
}

/// <summary>
/// Security settings for agent authentication.
/// The API key is written to the agent's appsettings.json on install.
/// </summary>
public sealed class SecuritySettings
{
    public string ApiKey { get; set; } = "";
}
