# FreeExamples.Client

A strongly-typed .NET client library for the FreeExamples external API, protected by API key middleware.

## Installation

```bash
dotnet add package FreeExamples.Client
```

## Quick Start

```csharp
using FreeExamples.Client;

// Simple constructor
using var client = new FreeExamplesClient("https://your-server.com", "your-api-key");

// Ping the protected endpoint
var pong = await client.PingAsync();
Console.WriteLine(pong.Message); // "pong"

// Post data
var response = await client.PostDataAsync(new ApiTestRequest { Message = "Hello!" });
Console.WriteLine(response.AuthenticatedAs); // Your API key name
```

## Dependency Injection

```csharp
// In Program.cs or Startup.cs
builder.Services.AddFreeExamplesClient(options =>
{
    options.Endpoint = "https://your-server.com";
    options.ApiKey = builder.Configuration["FreeExamples:ApiKey"]!;
});

// In your service
public class MyService(IFreeExamplesClient client)
{
    public async Task DoWork()
    {
        var response = await client.PostDataAsync(new ApiTestRequest { Message = "From DI" });
    }
}
```

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `Endpoint` | (required) | Base URL of the FreeExamples server |
| `ApiKey` | (required) | API key from the API Key Demo page |
| `Timeout` | 30 seconds | HTTP request timeout |
| `RetryCount` | 3 | Retry attempts for transient failures |
| `ThrowOnError` | true | Throw exceptions vs. return error responses |

## Fire-and-Forget

```csharp
// Never throws — returns true/false
bool success = await client.TryPostDataAsync(new ApiTestRequest { Message = "fire and forget" });
```

## Error Handling

```csharp
try {
    await client.PostDataAsync(new ApiTestRequest { Message = "test" });
}
catch (FreeExamplesAuthenticationException) {
    // 401 — API key is invalid or revoked
}
catch (FreeExamplesValidationException) {
    // 400 — Bad request
}
catch (FreeExamplesException ex) {
    // Other server errors
    Console.WriteLine($"Status {ex.StatusCode}: {ex.Message}");
}
```

## Pattern Source

This client follows the exact pattern from `FreeGLBA.Client` (NuGet: `FreeGLBA.Client`):
- SHA-256 hashed API keys (plaintext never stored server-side)
- Bearer token in Authorization header
- Exponential backoff retry (1s, 2s, 4s...)
- Typed exception hierarchy
- DI-friendly with `IHttpClientFactory`
