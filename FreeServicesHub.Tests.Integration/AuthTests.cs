using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FreeServicesHub.Tests.Integration;

public class AuthTests : IClassFixture<HubFixture>
{
    private readonly HubFixture _fixture;

    public AuthTests(HubFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AgentEndpoint_WithoutToken_Returns401()
    {
        var response = await _fixture.Client.PostAsync("/api/agent/jobs", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AgentEndpoint_WithInvalidToken_Returns401()
    {
        using var client = _fixture.CreateAuthenticatedClient("totally-invalid-token-value");
        var response = await client.PostAsync("/api/agent/jobs", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AgentEndpoint_WithValidToken_Returns200()
    {
        // First register to get a valid token
        var regRequest = new
        {
            RegistrationKey = HubFixture.TestRegistrationKey,
            Hostname = "AUTH-TEST-PC",
            OperatingSystem = "Windows",
            Architecture = "X64",
            AgentVersion = "1.0.0",
            DotNetVersion = "10.0.0",
        };

        var regResponse = await _fixture.Client.PostAsJsonAsync("/api/Data/RegisterAgent", regRequest);
        regResponse.EnsureSuccessStatusCode();

        var regResult = await regResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = regResult.GetProperty("apiClientToken").GetString();
        Assert.NotNull(token);

        using var authedClient = _fixture.CreateAuthenticatedClient(token!);
        var jobResponse = await authedClient.PostAsync("/api/agent/jobs", null);
        Assert.Equal(HttpStatusCode.OK, jobResponse.StatusCode);
    }
}
