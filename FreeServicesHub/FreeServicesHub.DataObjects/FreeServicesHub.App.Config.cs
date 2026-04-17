namespace FreeServicesHub;

// Interface — what consumers see
public partial interface IConfigurationHelper
{
    public string CiCdAdminToken { get; }
    public int CiCdMaxAgentRegistrations { get; }
    public int AgentHeartbeatIntervalSeconds { get; }
    public int AgentStaleThresholdSeconds { get; }
    public int RegistrationKeyExpiryHours { get; }
    public int HeartbeatRetentionHours { get; }
    public int CpuWarningThreshold { get; }
    public int CpuErrorThreshold { get; }
    public int MemoryWarningThreshold { get; }
    public int MemoryErrorThreshold { get; }
    public int DiskWarningThreshold { get; }
    public int DiskErrorThreshold { get; }
}

// Implementation — reads from loader
public partial class ConfigurationHelper : IConfigurationHelper
{
    public string CiCdAdminToken { get { return _loader.CiCdAdminToken; } }
    public int CiCdMaxAgentRegistrations { get { return _loader.CiCdMaxAgentRegistrations; } }
    public int AgentHeartbeatIntervalSeconds { get { return _loader.AgentHeartbeatIntervalSeconds; } }
    public int AgentStaleThresholdSeconds { get { return _loader.AgentStaleThresholdSeconds; } }
    public int RegistrationKeyExpiryHours { get { return _loader.RegistrationKeyExpiryHours; } }
    public int HeartbeatRetentionHours { get { return _loader.HeartbeatRetentionHours; } }
    public int CpuWarningThreshold { get { return _loader.CpuWarningThreshold; } }
    public int CpuErrorThreshold { get { return _loader.CpuErrorThreshold; } }
    public int MemoryWarningThreshold { get { return _loader.MemoryWarningThreshold; } }
    public int MemoryErrorThreshold { get { return _loader.MemoryErrorThreshold; } }
    public int DiskWarningThreshold { get { return _loader.DiskWarningThreshold; } }
    public int DiskErrorThreshold { get { return _loader.DiskErrorThreshold; } }
}

// Loader — populated during startup
public partial class ConfigurationHelperLoader
{
    public string CiCdAdminToken { get; set; } = string.Empty;
    public int CiCdMaxAgentRegistrations { get; set; } = 10;
    public int AgentHeartbeatIntervalSeconds { get; set; } = 30;
    public int AgentStaleThresholdSeconds { get; set; } = 120;
    public int RegistrationKeyExpiryHours { get; set; } = 24;
    public int HeartbeatRetentionHours { get; set; } = 24;
    public int CpuWarningThreshold { get; set; } = 70;
    public int CpuErrorThreshold { get; set; } = 90;
    public int MemoryWarningThreshold { get; set; } = 70;
    public int MemoryErrorThreshold { get; set; } = 90;
    public int DiskWarningThreshold { get; set; } = 50;
    public int DiskErrorThreshold { get; set; } = 90;
}
