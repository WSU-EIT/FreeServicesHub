using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace FreeServicesHub.Tests.Integration;

/// <summary>
/// Tests the SignalR connection flow: connect to hub with valid token,
/// join Agents group, send heartbeat via SignalR.
/// </summary>
public class SignalRTests : IClassFixture<HubFixture>
{
    private readonly HubFixture _fixture;

    public SignalRTests(HubFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConnectToHub_WithValidToken_Succeeds()
    {
        // Register an agent to get a token
        var regRequest = new
        {
            RegistrationKey = HubFixture.TestRegistrationKey,
            Hostname = "SIGNALR-TEST-PC",
            OperatingSystem = "Windows",
            Architecture = "X64",
            AgentVersion = "1.0.0",
            DotNetVersion = "10.0.0",
        };

        var regResponse = await _fixture.Client.PostAsJsonAsync("/api/Data/RegisterAgent", regRequest);
        regResponse.EnsureSuccessStatusCode();

        var regResult = await regResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = regResult.GetProperty("apiClientToken").GetString()!;

        // Connect to SignalR hub with the token
        var hubUrl = $"{_fixture.ServerUrl}/freeserviceshubHub";

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => _fixture.Client
                    .GetType()
                    .GetField("_handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .GetValue(_fixture.Client) as HttpMessageHandler ?? new HttpClientHandler();
            })
            .Build();

        try
        {
            await hubConnection.StartAsync();
            Assert.Equal(HubConnectionState.Connected, hubConnection.State);

            // Join the Agents group
            await hubConnection.InvokeAsync("JoinGroup", "Agents");

            // Send a heartbeat via SignalR
            var heartbeat = new
            {
                HeartbeatId = Guid.Empty,
                AgentId = Guid.Empty, // Will be set by hub from claims
                Timestamp = DateTime.UtcNow,
                CpuPercent = 25.0,
                MemoryPercent = 40.0,
                MemoryUsedGB = 6.4,
                MemoryTotalGB = 16.0,
                DiskMetricsJson = "[]",
                CustomDataJson = "",
                AgentName = "SIGNALR-TEST-PC",
            };

            // This should not throw if the hub method exists and works
            await hubConnection.InvokeAsync("SendHeartbeat", heartbeat);
        }
        finally
        {
            await hubConnection.DisposeAsync();
        }
    }
}
