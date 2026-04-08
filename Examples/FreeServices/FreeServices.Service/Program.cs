// FreeServices.Service — Program.cs
// Host builder for the System Monitor service.
// Runs identically as a console app, Windows Service, systemd daemon, or launchd agent.

using System.Runtime.InteropServices;
using FreeServices.Service;

var builder = Host.CreateApplicationBuilder(args);

// Register platform-specific service lifetime
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "FreeServicesMonitor";
    });
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Services.AddSystemd();
}
// macOS (launchd): no special lifetime registration needed —
// launchd manages the process directly; the default ConsoleLifetime works.

builder.Services.AddHostedService<SystemMonitorService>();

var host = builder.Build();
host.Run();
