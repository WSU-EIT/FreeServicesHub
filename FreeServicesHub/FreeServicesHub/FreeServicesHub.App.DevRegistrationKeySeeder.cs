using System.Security.Cryptography;
using System.Text;
using FreeServicesHub.EFModels.EFModels;
using Microsoft.EntityFrameworkCore;

namespace FreeServicesHub;

/// <summary>
/// Seeds a known registration key when running in Development mode with InMemory database.
/// This allows the Aspire AppHost to start the agent with a pre-seeded key for auto-registration.
/// </summary>
public class DevRegistrationKeySeeder : IHostedService
{
    private const string DevKey = "dev-test-key-seed";

    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DevRegistrationKeySeeder> _logger;

    public DevRegistrationKeySeeder(
        IServiceProvider serviceProvider,
        IHostEnvironment environment,
        ILogger<DevRegistrationKeySeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EFDataModel>();

        bool anyKeys = await db.RegistrationKeys.AnyAsync(cancellationToken);
        if (anyKeys)
            return;

        var keyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(DevKey)));

        var regKey = new EFModels.EFModels.RegistrationKey
        {
            RegistrationKeyId = Guid.NewGuid(),
            TenantId = Guid.Empty,
            KeyHash = keyHash,
            KeyPrefix = DevKey[..8],
            ExpiresAt = DateTime.UtcNow.AddDays(365),
            Used = false,
            Created = DateTime.UtcNow,
            CreatedBy = "DevSeeder",
        };

        await db.RegistrationKeys.AddAsync(regKey, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded dev registration key: {Prefix}...", regKey.KeyPrefix);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
