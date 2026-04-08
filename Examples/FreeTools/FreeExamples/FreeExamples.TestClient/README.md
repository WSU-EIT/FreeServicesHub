# FreeExamples.TestClient

> Console application that exercises the `FreeExamples.Client` NuGet client against the API key-protected endpoints — verifies authentication, error handling, and retry behavior.

**Target:** .NET 10 · **Type:** Console Application

---

## What This Does

A test harness that runs a suite of API calls against the FreeExamples server to verify:

- **Authorized access** — valid API key produces successful responses
- **Unauthorized access** — invalid/missing keys produce proper error responses
- **Error handling** — typed exception hierarchy works correctly
- **Fire-and-forget** — `TryPostDataAsync` never throws

---

## Usage

```bash
# 1. Start the FreeExamples server
dotnet run --project FreeExamples/FreeExamples

# 2. Open the API Key Demo page in the browser and generate a key

# 3. Configure the test client
cd FreeExamples/FreeExamples.TestClient
dotnet user-secrets set "FreeExamples:Endpoint" "https://localhost:7271"
dotnet user-secrets set "FreeExamples:ApiKey" "your-generated-api-key"

# 4. Run the tests
dotnet run
```

---

## Configuration

Set via `appsettings.json` or user secrets:

| Key | Description |
|-----|-------------|
| `FreeExamples:Endpoint` | Base URL of the FreeExamples server |
| `FreeExamples:ApiKey` | API key generated from the API Key Demo page |

---

## Pattern Source

Based on `FreeGLBA.TestClientWithNugetPackage` — same test suite structure.

---

*Part of the [FreeExamples](..) suite.*
