using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FreeServicesHub.EFModels.EFModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreeServicesHub.Tests.Integration;

public class TenantIsolationTests : IClassFixture<HubFixture>
{
    private readonly HubFixture _fixture;

    public TenantIsolationTests(HubFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Agent_CannotSeeJobsFromAnotherTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var agentAId = Guid.NewGuid();
        var agentBId = Guid.NewGuid();
        var tokenA = "test-token-tenant-a-" + Guid.NewGuid().ToString("N");
        var tokenB = "test-token-tenant-b-" + Guid.NewGuid().ToString("N");
        var jobIdB = Guid.NewGuid();

        // Seed directly in DB
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EFDataModel>();

            // Create agents in different tenants
            db.Agents.Add(new Agent { AgentId = agentAId, TenantId = tenantA, Name = "Agent-A", Added = DateTime.UtcNow, LastModified = DateTime.UtcNow });
            db.Agents.Add(new Agent { AgentId = agentBId, TenantId = tenantB, Name = "Agent-B", Added = DateTime.UtcNow, LastModified = DateTime.UtcNow });

            // Create API tokens for both agents
            db.ApiClientTokens.Add(new ApiClientToken
            {
                ApiClientTokenId = Guid.NewGuid(),
                AgentId = agentAId,
                TenantId = tenantA,
                TokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(tokenA))),
                TokenPrefix = tokenA[..8],
                Active = true,
                Created = DateTime.UtcNow,
            });
            db.ApiClientTokens.Add(new ApiClientToken
            {
                ApiClientTokenId = Guid.NewGuid(),
                AgentId = agentBId,
                TenantId = tenantB,
                TokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(tokenB))),
                TokenPrefix = tokenB[..8],
                Active = true,
                Created = DateTime.UtcNow,
            });

            // Create a job in Tenant B
            db.HubJobs.Add(new HubJob
            {
                HubJobId = jobIdB,
                TenantId = tenantB,
                AgentId = null,
                JobType = "CollectLogs",
                Status = "Queued",
                Priority = 0,
                MaxRetries = 3,
                RetryCount = 0,
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();
        }

        // Agent A polls for jobs — should NOT see TenantB's job
        using var clientA = _fixture.CreateAuthenticatedClient(tokenA);
        var responseA = await clientA.PostAsync("/api/agent/jobs", null);
        responseA.EnsureSuccessStatusCode();
        var jobsA = await responseA.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(jobsA);
        Assert.DoesNotContain(jobsA, j => j.GetProperty("hubJobId").GetGuid() == jobIdB);

        // Agent B polls for jobs — SHOULD see its own tenant's job
        using var clientB = _fixture.CreateAuthenticatedClient(tokenB);
        var responseB = await clientB.PostAsync("/api/agent/jobs", null);
        responseB.EnsureSuccessStatusCode();
        var jobsB = await responseB.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(jobsB);
        Assert.Contains(jobsB, j => j.GetProperty("hubJobId").GetGuid() == jobIdB);
    }
}
