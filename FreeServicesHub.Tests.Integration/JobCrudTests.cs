using System.Net.Http.Json;
using System.Text.Json;
using FreeServicesHub.EFModels.EFModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreeServicesHub.Tests.Integration;

public class JobCrudTests : IClassFixture<HubFixture>
{
    private readonly HubFixture _fixture;

    public JobCrudTests(HubFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AgentJobPolling_ReturnsQueuedJobs()
    {
        // 1. Register an agent to get a token
        var regRequest = new
        {
            RegistrationKey = HubFixture.TestRegistrationKey,
            Hostname = "JOB-POLL-PC",
            OperatingSystem = "Windows",
            Architecture = "X64",
            AgentVersion = "1.0.0",
            DotNetVersion = "10.0.0",
        };

        var regResponse = await _fixture.Client.PostAsJsonAsync("/api/Data/RegisterAgent", regRequest);
        regResponse.EnsureSuccessStatusCode();

        var regResult = await regResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = regResult.GetProperty("apiClientToken").GetString()!;
        var agentId = regResult.GetProperty("agentId").GetGuid();

        // 2. Seed a HubJob directly in the DB (Queued, no agent assigned)
        var jobId = Guid.NewGuid();
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EFDataModel>();
            db.HubJobs.Add(new EFModels.EFModels.HubJob
            {
                HubJobId = jobId,
                TenantId = Guid.Empty,
                AgentId = null,
                JobType = "CollectLogs",
                Status = "Queued",
                Priority = 0,
                MaxRetries = 3,
                RetryCount = 0,
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Deleted = false,
            });
            await db.SaveChangesAsync();
        }

        // 3. Call /api/agent/jobs with the agent's token
        using var authClient = _fixture.CreateAuthenticatedClient(token);
        var jobResponse = await authClient.PostAsync("/api/agent/jobs", null);
        jobResponse.EnsureSuccessStatusCode();

        var jobs = await jobResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(jobs.GetArrayLength() > 0, "Agent should see at least one queued job");

        var found = false;
        foreach (var j in jobs.EnumerateArray())
        {
            if (j.GetProperty("hubJobId").GetGuid() == jobId)
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "The seeded job should appear in the agent's polling response");
    }

    [Fact]
    public async Task AgentJobUpdate_ChangesStatus()
    {
        // 1. Register an agent
        var regRequest = new
        {
            RegistrationKey = HubFixture.TestRegistrationKey,
            Hostname = "JOB-UPDATE-PC",
            OperatingSystem = "Windows",
            Architecture = "X64",
            AgentVersion = "1.0.0",
            DotNetVersion = "10.0.0",
        };

        var regResponse = await _fixture.Client.PostAsJsonAsync("/api/Data/RegisterAgent", regRequest);
        regResponse.EnsureSuccessStatusCode();
        var regResult = await regResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = regResult.GetProperty("apiClientToken").GetString()!;
        var agentId = regResult.GetProperty("agentId").GetGuid();

        // 2. Seed a job assigned to this agent
        var jobId = Guid.NewGuid();
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EFDataModel>();
            db.HubJobs.Add(new EFModels.EFModels.HubJob
            {
                HubJobId = jobId,
                TenantId = Guid.Empty,
                AgentId = agentId,
                JobType = "RestartService",
                Status = "Assigned",
                Priority = 0,
                MaxRetries = 3,
                RetryCount = 0,
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Deleted = false,
            });
            await db.SaveChangesAsync();
        }

        // 3. Update the job status to Running via agent endpoint
        using var authClient = _fixture.CreateAuthenticatedClient(token);
        var updatePayload = new[]
        {
            new
            {
                HubJobId = jobId,
                TenantId = Guid.Empty,
                AgentId = agentId,
                JobType = "RestartService",
                Status = "Running",
                Payload = "",
                Result = "",
                ErrorMessage = "",
                Priority = 0,
                MaxRetries = 3,
                RetryCount = 0,
                Created = DateTime.UtcNow,
                StartedAt = (DateTime?)DateTime.UtcNow,
                CompletedAt = (DateTime?)null,
                LastModified = DateTime.UtcNow,
                LastModifiedBy = "",
                CreatedBy = "",
                Deleted = false,
                DeletedAt = (DateTime?)null,
            }
        };

        var updateResponse = await authClient.PostAsJsonAsync("/api/agent/jobs/update", updatePayload);
        updateResponse.EnsureSuccessStatusCode();

        // 4. Verify status changed in DB
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EFDataModel>();
            var job = await db.HubJobs.FirstOrDefaultAsync(x => x.HubJobId == jobId);
            Assert.NotNull(job);
            Assert.Equal("Running", job.Status);
        }
    }

    [Fact]
    public async Task CascadeDelete_CancelsOrphanedJobs()
    {
        // 1. Seed agent + job directly in DB
        var agentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EFDataModel>();

            db.Agents.Add(new EFModels.EFModels.Agent
            {
                AgentId = agentId,
                TenantId = Guid.Empty,
                Name = "CascadeTestAgent",
                Added = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Deleted = false,
            });

            db.HubJobs.Add(new EFModels.EFModels.HubJob
            {
                HubJobId = jobId,
                TenantId = Guid.Empty,
                AgentId = agentId,
                JobType = "RunScript",
                Status = "Assigned",
                Priority = 0,
                MaxRetries = 3,
                RetryCount = 0,
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Deleted = false,
            });

            await db.SaveChangesAsync();
        }

        // 2. Delete the agent via IDataAccess (admin endpoints require auth)
        using (var scope = _fixture.Services.CreateScope())
        {
            var da = scope.ServiceProvider.GetRequiredService<IDataAccess>();
            var result = await da.DeleteAgents(new List<Guid> { agentId });
            Assert.True(result.Result, "Agent deletion should succeed");
        }

        // 3. Verify job status is Cancelled
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EFDataModel>();
            var job = await db.HubJobs.FirstOrDefaultAsync(x => x.HubJobId == jobId);
            Assert.NotNull(job);
            Assert.Equal("Cancelled", job.Status);
            Assert.Equal("Agent deleted", job.ErrorMessage);
        }
    }
}
