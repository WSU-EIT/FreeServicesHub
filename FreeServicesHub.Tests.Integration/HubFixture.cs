using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using FreeServicesHub.EFModels.EFModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreeServicesHub.Tests.Integration;

/// <summary>
/// Shared test fixture that starts the hub server in-process with InMemory database
/// and pre-seeds a registration key for agent testing.
/// </summary>
public class HubFixture : IAsyncLifetime
{
    public const string TestRegistrationKey = "integration-test-key";

    private WebApplicationFactory<Program>? _factory;
    public HttpClient Client { get; private set; } = null!;
    public string ServerUrl { get; private set; } = "";
    public IServiceProvider Services => _factory!.Services;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    // Force InMemory database
                    services.AddDbContext<EFDataModel>(options =>
                    {
                        options.UseInMemoryDatabase("IntegrationTest_" + Guid.NewGuid().ToString("N"));
                    }, ServiceLifetime.Scoped);
                });
            });

        Client = _factory.CreateClient();
        ServerUrl = Client.BaseAddress?.ToString().TrimEnd('/') ?? "";

        // Seed the registration key
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EFDataModel>();

        var keyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(TestRegistrationKey)));
        db.RegistrationKeys.Add(new EFModels.EFModels.RegistrationKey
        {
            RegistrationKeyId = Guid.NewGuid(),
            TenantId = Guid.Empty,
            KeyHash = keyHash,
            KeyPrefix = TestRegistrationKey[..8],
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            Used = false,
            Created = DateTime.UtcNow,
            CreatedBy = "IntegrationTest",
        });
        db.SaveChanges();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Create an HttpClient with an agent's Bearer token set.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
