using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FreeServicesHub.Tests.Integration;

/// <summary>
/// Tests the agent registration flow: POST /api/Data/RegisterAgent with a valid key
/// should return an AgentId and ApiClientToken.
/// </summary>
public class RegistrationTests : IClassFixture<HubFixture>
{
    private readonly HubFixture _fixture;

    public RegistrationTests(HubFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RegisterAgent_WithValidKey_ReturnsTokenAndAgentId()
    {
        var request = new
        {
            RegistrationKey = HubFixture.TestRegistrationKey,
            Hostname = "TEST-PC",
            OperatingSystem = "Windows 10 Enterprise",
            Architecture = "X64",
            AgentVersion = "1.0.0",
            DotNetVersion = "10.0.0",
        };

        var response = await _fixture.Client.PostAsJsonAsync("/api/Data/RegisterAgent", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Verify response contains ApiClientToken
        Assert.True(result.TryGetProperty("apiClientToken", out var tokenElement),
            "Response should contain 'apiClientToken'");
        Assert.False(string.IsNullOrEmpty(tokenElement.GetString()),
            "ApiClientToken should not be empty");

        // Verify response contains AgentId
        Assert.True(result.TryGetProperty("agentId", out var agentIdElement),
            "Response should contain 'agentId'");
        Assert.NotEqual(Guid.Empty, agentIdElement.GetGuid());
    }

    [Fact]
    public async Task RegisterAgent_WithInvalidKey_ReturnsErrorMessage()
    {
        var request = new
        {
            RegistrationKey = "invalid-key-that-does-not-exist",
            Hostname = "TEST-PC",
            OperatingSystem = "Windows",
            Architecture = "X64",
            AgentVersion = "1.0.0",
            DotNetVersion = "10.0.0",
        };

        var response = await _fixture.Client.PostAsJsonAsync("/api/Data/RegisterAgent", request);
        response.EnsureSuccessStatusCode(); // Server returns 200 with error in body

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Should have an empty/null token
        if (result.TryGetProperty("apiClientToken", out var tokenElement))
        {
            var token = tokenElement.GetString();
            Assert.True(string.IsNullOrEmpty(token),
                "ApiClientToken should be empty for invalid registration key");
        }
    }

    [Fact]
    public async Task RegisterAgent_KeyCannotBeReused()
    {
        // First registration should succeed
        var request = new
        {
            RegistrationKey = HubFixture.TestRegistrationKey,
            Hostname = "REUSE-TEST-PC",
            OperatingSystem = "Windows",
            Architecture = "X64",
            AgentVersion = "1.0.0",
            DotNetVersion = "10.0.0",
        };

        var response1 = await _fixture.Client.PostAsJsonAsync("/api/Data/RegisterAgent", request);
        response1.EnsureSuccessStatusCode();

        // Second registration with same key should fail (key is burned)
        var response2 = await _fixture.Client.PostAsJsonAsync("/api/Data/RegisterAgent", request);
        response2.EnsureSuccessStatusCode();

        var result2 = await response2.Content.ReadFromJsonAsync<JsonElement>();
        if (result2.TryGetProperty("apiClientToken", out var tokenElement))
        {
            var token = tokenElement.GetString();
            Assert.True(string.IsNullOrEmpty(token),
                "Reused registration key should not produce a token");
        }
    }
}
