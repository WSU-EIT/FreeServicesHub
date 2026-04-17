namespace FreeServicesHub;

public partial class DataObjects
{
    /// <summary>
    /// Represents a single log entry produced by a hub-side background service.
    /// Transmitted via SignalR for real-time display on the Background Services page.
    /// </summary>
    public class BackgroundServiceLogEntry
    {
        public Guid LogId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string LogLevel { get; set; } = string.Empty; // Information, Warning, Error, etc.
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Describes a registered hub-side background service and its current state.
    /// </summary>
    public class BackgroundServiceInfo
    {
        public string ServiceName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Running"; // Running, Stopped, Error
        public DateTime? LastActivity { get; set; }
    }
}
