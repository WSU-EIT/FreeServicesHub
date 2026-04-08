namespace FreeServicesHub;

public partial class Program
{
    private static WebApplicationBuilder MyAppModifyBuilderEnd(WebApplicationBuilder Output)
    {
        Output.Services.AddHostedService<AgentMonitorService>();
        return Output;
    }

    private static WebApplication MyAppModifyStart(WebApplication Output)
    {
        Output.UseMiddleware<ApiKeyMiddleware>();
        return Output;
    }

    // See if we have app-specific configuration values to load.
    private static ConfigurationHelperLoader MyConfigurationHelpersLoadApp(
        ConfigurationHelperLoader output, WebApplicationBuilder builder)
    {
        output.AgentHeartbeatIntervalSeconds = builder.Configuration.GetValue<int>("App:AgentHeartbeatIntervalSeconds", 30);
        output.AgentStaleThresholdSeconds = builder.Configuration.GetValue<int>("App:AgentStaleThresholdSeconds", 120);
        output.RegistrationKeyExpiryHours = builder.Configuration.GetValue<int>("App:RegistrationKeyExpiryHours", 24);
        output.HeartbeatRetentionHours = builder.Configuration.GetValue<int>("App:HeartbeatRetentionHours", 24);
        output.CpuWarningThreshold = builder.Configuration.GetValue<int>("App:CpuWarningThreshold", 70);
        output.CpuErrorThreshold = builder.Configuration.GetValue<int>("App:CpuErrorThreshold", 90);
        output.MemoryWarningThreshold = builder.Configuration.GetValue<int>("App:MemoryWarningThreshold", 70);
        output.MemoryErrorThreshold = builder.Configuration.GetValue<int>("App:MemoryErrorThreshold", 90);
        output.DiskWarningThreshold = builder.Configuration.GetValue<int>("App:DiskWarningThreshold", 50);
        output.DiskErrorThreshold = builder.Configuration.GetValue<int>("App:DiskErrorThreshold", 90);

        return output;
    }
}
