// FreeServicesHub.AppHost -- Aspire orchestration for local development.
// Starts the hub (Blazor + SignalR) and one agent, wiring ports and environment.

var builder = DistributedApplication.CreateBuilder(args);

// Hub server — pin to the same ports as launchSettings.json
var hub = builder.AddProject<Projects.FreeServicesHub>("hub")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DatabaseType", "InMemory")
    .WithEndpoint("https", endpoint =>
    {
        endpoint.Port = 7271;
        endpoint.IsProxied = false;
    })
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5111;
        endpoint.IsProxied = false;
    });

// Agent — inject hub URL and a dev registration key, run as console process
builder.AddProject<Projects.FreeServicesHub_Agent>("agent")
    .WithEnvironment("Agent__HubUrl", "https://localhost:7271")
    .WithEnvironment("Agent__RegistrationKey", "dev-test-key-seed")
    .WaitFor(hub);

builder.Build().Run();
