namespace FreeServicesHub;

public partial class DataObjects
{
    /// <summary>
    /// Windows Service metadata and editable agent settings.
    /// Sent with every heartbeat and on-demand via SignalR.
    /// </summary>
    public class AgentServiceInfo
    {
        public Guid AgentId { get; set; }

        // ── Windows Service read-only metadata ──
        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ServiceStatus { get; set; } = string.Empty; // Running, Stopped, StartPending, etc.
        public string StartupType { get; set; } = string.Empty;   // Automatic, Manual, Disabled
        public string LogOnAccount { get; set; } = string.Empty;   // e.g. LocalSystem, NT AUTHORITY\NETWORK SERVICE
        public int ProcessId { get; set; }
        public string ServiceDescription { get; set; } = string.Empty;

        // ── Editable agent settings ──
        public string HubUrl { get; set; } = string.Empty;
        public int HeartbeatIntervalSeconds { get; set; } = 30;
        public string AgentName { get; set; } = string.Empty;

        // ── Runtime info ──
        public string AgentVersion { get; set; } = string.Empty;
        public string DotNetVersion { get; set; } = string.Empty;
        public DateTime? LastBootTime { get; set; }
        public TimeSpan Uptime { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    /// <summary>
    /// Payload sent from the hub to an agent to update its editable settings.
    /// </summary>
    public class AgentSettingsUpdate
    {
        public Guid AgentId { get; set; }
        public string? HubUrl { get; set; }
        public int? HeartbeatIntervalSeconds { get; set; }
        public string? AgentName { get; set; }
        public string? StartupType { get; set; } // Automatic, Manual, Disabled
    }
}
