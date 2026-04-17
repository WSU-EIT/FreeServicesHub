using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FreeServicesHub.Tests.Integration;

/// <summary>
/// Tests the heartbeat save flow: POST /api/Data/SaveHeartbeat with a valid Bearer token
/// should persist the heartbeat data.
/// </summary>
public class HeartbeatTests : IClassFixture<HubFixture>
{
    private readonly HubFixture _fixture;

    public HeartbeatTests(HubFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveHeartbeat_WithValidToken_ReturnsSuccess()
    {
        // First, register an agent to get a token
        var regRequest = new
        {
            RegistrationKey = HubFixture.TestRegistrationKey,
            Hostname = "HEARTBEAT-TEST-PC",
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

        // Now send a heartbeat with the token
        using var authClient = _fixture.CreateAuthenticatedClient(token);

        var heartbeat = new
        {
            HeartbeatId = Guid.Empty,
            AgentId = agentId,
            Timestamp = DateTime.UtcNow,
            CpuPercent = 42.5,
            MemoryPercent = 65.0,
            MemoryUsedGB = 8.5,
            MemoryTotalGB = 16.0,
            DiskMetricsJson = "[{\"Drive\":\"C:\\\\\",\"UsedGB\":120.5,\"TotalGB\":256.0,\"Percent\":47.1}]",
            CustomDataJson = "",
            AgentName = "HEARTBEAT-TEST-PC",
        };

        var hbResponse = await authClient.PostAsJsonAsync("/api/Data/SaveHeartbeat", heartbeat);
        hbResponse.EnsureSuccessStatusCode();

        var hbResult = await hbResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(hbResult.GetProperty("result").GetBoolean(),
            "SaveHeartbeat should return result=true");
    }

    [Fact]
    public async Task SaveHeartbeat_WithoutToken_Returns401()
    {
        var heartbeat = new
        {
            HeartbeatId = Guid.Empty,
            AgentId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CpuPercent = 50.0,
            MemoryPercent = 50.0,
            MemoryUsedGB = 8.0,
            MemoryTotalGB = 16.0,
            DiskMetricsJson = "[]",
            CustomDataJson = "",
            AgentName = "NO-AUTH-PC",
        };

        // The SaveHeartbeat endpoint requires [Authorize], so this should fail
        var response = await _fixture.Client.PostAsJsonAsync("/api/Data/SaveHeartbeat", heartbeat);
        // Expect either 401 or redirect to login
        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Redirect,
            $"Expected 401 or redirect, got {response.StatusCode}");
    }
}
