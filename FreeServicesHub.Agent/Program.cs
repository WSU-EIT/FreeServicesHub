// FreeServicesHub.Agent -- Program.cs
// Host builder for the Agent worker service.
// Runs as a Windows Service via sc.exe or as a console app for debugging.

using FreeServicesHub.Agent;

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "FreeServicesHubAgent";
    })
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<AgentWorkerService>();
    });

var host = builder.Build();

// Expose the lifetime so the SignalR Shutdown handler can trigger graceful stop.
FreeServicesHub.Agent.Program.Lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

host.Run();
