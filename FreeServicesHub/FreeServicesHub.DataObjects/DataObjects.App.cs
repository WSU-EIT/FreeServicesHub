namespace FreeServicesHub;

// Use this file as a place to put any application-specific data objects.

public partial class DataObjects
{
    public partial class SignalRUpdateType
    {
        //public const string YourSignalRUpdateType = "YourSignalRUpdateType";
        public const string AgentHeartbeat = "AgentHeartbeat";
        public const string AgentConnected = "AgentConnected";
        public const string AgentDisconnected = "AgentDisconnected";
        public const string AgentStatusChanged = "AgentStatusChanged";
        public const string AgentShutdown = "AgentShutdown";
        public const string RegistrationKeyGenerated = "RegistrationKeyGenerated";
    }

    public partial class User
    {
        //public string? MyCustomUserProperty { get; set; }
    }

    //public class YourClass
    //{
    //    public string? YourProperty { get; set; }
    //}
}