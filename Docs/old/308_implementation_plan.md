# 308 — Implementation Plan: Agent ↔ Hub Integration Fixes & Testing Infrastructure

> **Document ID:** 308  
> **Category:** Engineering — Implementation Plan  
> **Prerequisite:** 307 (Feasibility Analysis & Gap Inventory)  
> **Scope:** Every code change required to make the FreeServicesHub Agent ↔ Hub pipeline work end-to-end, plus the testing infrastructure to prove it  
> **Repository:** `https://dev.azure.com/wsueit/FreeServicesHub/_git/FreeServicesHub` (branch: `main`)

---

## Table of Contents

1. [Phase 1 — Agent HTTP Wiring Fixes (Blockers)](#phase-1--agent-http-wiring-fixes-blockers)
2. [Phase 2 — SignalR Hub Expansion](#phase-2--signalr-hub-expansion)
3. [Phase 3 — SignalR Authentication Bridge](#phase-3--signalr-authentication-bridge)
4. [Phase 4 — Agent-to-Hub Data Shape Alignment](#phase-4--agent-to-hub-data-shape-alignment)
5. [Phase 5 — Aspire AppHost (Local Orchestration)](#phase-5--aspire-apphost-local-orchestration)
6. [Phase 6 — CI/CD Pipeline Definition](#phase-6--cicd-pipeline-definition)
7. [Phase 7 — Integration Test Project](#phase-7--integration-test-project)
8. [Phase 8 — Validation & Smoke Testing](#phase-8--validation--smoke-testing)
9. [Dependency Graph](#dependency-graph)
10. [Risk Register](#risk-register)

---

## Phase 1 — Agent HTTP Wiring Fixes (Blockers)

> **Goal:** Make every HTTP request the agent sends actually reach an existing server endpoint.  
> **Effort:** ~30 minutes  
> **Depends on:** Nothing — this is the foundation  
> **Blocks:** Everything downstream

### Task 1.1 — Fix Default HubUrl Port

- **What:** Change the agent's default `HubUrl` from `https://localhost:5001` to `https://localhost:7271`
- **Why:** The hub's `launchSettings.json` binds the HTTPS profile to port `7271` and the HTTP profile to port `5111`/`5201`. Port `5001` is not used anywhere — the agent would get `ConnectionRefused` on every request.
- **Where:**
  - `FreeServicesHub.Agent/appsettings.json` — line with `"HubUrl": "https://localhost:5001"`
  - `FreeServicesHub.Agent/AgentWorkerService.cs` — line 20, the `AgentOptions` default: `public string HubUrl { get; set; } = "https://localhost:5001";`
- **How:** Replace `https://localhost:5001` with `https://localhost:7271` in both locations.
- **When:** First — nothing works until the agent can reach the hub.
- **Verification:** After this change, `HttpClient.PostAsJsonAsync` to the hub should no longer throw `HttpRequestException (ConnectionRefused)`.

### Task 1.2 — Fix Registration Endpoint URL

- **What:** Change the registration URL from `/api/agents/register` to `/api/Data/RegisterAgent`
- **Why:** The server's `DataController` uses the template convention `~/api/Data/{Action}`. The route `[Route("~/api/Data/RegisterAgent")]` is defined at line 58 of `FreeServicesHub.App.API.cs`. The agent's invented `/api/agents/register` doesn't match any controller route — the server would return `404 Not Found`.
- **Where:** `FreeServicesHub.Agent/AgentWorkerService.cs` — line 184:
  ```csharp
  var response = await httpClient.PostAsJsonAsync("/api/agents/register", payload, ct);
  ```
- **How:** Change to:
  ```csharp
  var response = await httpClient.PostAsJsonAsync("/api/Data/RegisterAgent", payload, ct);
  ```
- **When:** Immediately after Task 1.1.
- **Verification:** The POST request hits the `RegisterAgent` action on `DataController` and doesn't return 404.

### Task 1.3 — Fix Heartbeat HTTP Fallback Endpoint URL

- **What:** Change the heartbeat fallback URL from `/api/agents/heartbeat` to `/api/Data/SaveHeartbeat`
- **Why:** Same issue as 1.2 — the server route is `[Route("~/api/Data/SaveHeartbeat")]` at line 93 of `FreeServicesHub.App.API.cs`. The agent's `/api/agents/heartbeat` returns 404.
- **Where:** `FreeServicesHub.Agent/AgentWorkerService.cs` — line 387:
  ```csharp
  var response = await httpClient.PostAsJsonAsync("/api/agents/heartbeat", snapshot, ct);
  ```
- **How:** Change to:
  ```csharp
  var response = await httpClient.PostAsJsonAsync("/api/Data/SaveHeartbeat", snapshot, ct);
  ```
- **When:** Same batch as 1.2.
- **Verification:** When SignalR is disconnected and the agent falls back to HTTP, the heartbeat POST hits `SaveHeartbeat` on the server.

---

## Phase 2 — SignalR Hub Expansion

> **Goal:** Add the hub methods the agent calls so SignalR invocations don't throw `HubException: Unknown hub method`.  
> **Effort:** ~1 hour  
> **Depends on:** Nothing (can be done in parallel with Phase 1)  
> **Blocks:** Phase 3 (auth must work for these methods to be reachable), Phase 8 (smoke tests)

### Task 2.1 — Add `JoinGroup` Method to Hub

- **What:** Add a `JoinGroup(string groupName)` method to `freeserviceshubHub`
- **Why:** The agent calls `InvokeAsync("JoinGroup", "Agents")` at line 307 of `AgentWorkerService.cs`. The hub currently has NO `JoinGroup` method — only `JoinTenantId(string)`. Without this method, the SignalR client receives:
  ```
  HubException: Failed to invoke 'JoinGroup' because it does not exist.
  ```
- **Where:** `FreeServicesHub/FreeServicesHub/Hubs/signalrHub.cs` — inside the `freeserviceshubHub` class (after the existing `SignalRUpdate` method, around line 43)
- **How:** Add:
  ```csharp
  public async Task JoinGroup(string groupName)
  {
      await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
  }
  ```
- **When:** Phase 2, first task.
- **Design Decision:** We add a new method rather than making the agent call `JoinTenantId` because:
  1. Agent groups ("Agents") are conceptually different from tenant UI groups
  2. The agent doesn't know its `TenantId` at connection time (it only has an `ApiClientToken`)
  3. Keeping `JoinGroup` generic allows future group use cases (e.g., "HighPriority", "Windows", "Linux")

### Task 2.2 — Add `SendHeartbeat` Method to Hub

- **What:** Add a `SendHeartbeat` method to `freeserviceshubHub` that receives agent telemetry, persists it via `DataAccess.SaveHeartbeat()`, and broadcasts updates
- **Why:** The agent calls `InvokeAsync("SendHeartbeat", snapshot)` at lines 336 and 414 of `AgentWorkerService.cs`. This method does not exist in the hub. The agent needs a real-time channel to push heartbeats that bypasses the HTTP controller layer.
- **Where:** `FreeServicesHub/FreeServicesHub/Hubs/signalrHub.cs` — inside the `freeserviceshubHub` class
- **How:** The method must:
  1. Accept a `DataObjects.AgentHeartbeat` (or a compatible shape — see Phase 4)
  2. Extract the `AgentId` from the authenticated connection context (set during auth in Phase 3)
  3. Call `DataAccess.SaveHeartbeat()` to persist + evaluate thresholds
  4. The `SaveHeartbeat` DataAccess method already broadcasts a `SignalRUpdate` with type `AgentHeartbeat` (see `FreeServicesHub.App.DataAccess.Heartbeats.cs` lines 66-72), so no additional broadcast is needed here
- **Implementation sketch:**
  ```csharp
  public async Task SendHeartbeat(DataObjects.AgentHeartbeat heartbeat)
  {
      // AgentId comes from the authenticated connection (set by auth handler)
      if (Context.Items.TryGetValue("AgentId", out var agentIdObj) && agentIdObj is Guid agentId)
      {
          heartbeat.AgentId = agentId;
      }

      using var scope = _serviceProvider.CreateScope();
      var da = scope.ServiceProvider.GetRequiredService<IDataAccess>();
      await da.SaveHeartbeat(heartbeat);
  }
  ```
- **When:** After Task 2.1.
- **Dependency:** The hub class needs `IServiceProvider` injected. Currently it has no constructor — we'll need to add DI.
- **Design Note:** The `SaveHeartbeat` DataAccess call at line 66-72 already calls `SignalRUpdate` to broadcast the heartbeat to the tenant group. This means the hub method doesn't need to manually broadcast — the DataAccess layer handles it. This is the same pattern FreeCICD uses (DataAccess fires the SignalR broadcast, not the hub method directly).

### Task 2.3 — Add DI Constructor to Hub

- **What:** Add `IServiceProvider` injection to `freeserviceshubHub` so `SendHeartbeat` can resolve `IDataAccess`
- **Why:** The hub currently has no constructor. `SendHeartbeat` needs to call `DataAccess.SaveHeartbeat()`, which requires a scoped `IDataAccess` instance.
- **Where:** `FreeServicesHub/FreeServicesHub/Hubs/signalrHub.cs` — add constructor to `freeserviceshubHub`
- **How:**
  ```csharp
  private readonly IServiceProvider _serviceProvider;

  public freeserviceshubHub(IServiceProvider serviceProvider)
  {
      _serviceProvider = serviceProvider;
  }
  ```
- **When:** Before Task 2.2 (2.2 depends on this).
- **Risk:** The existing `tenants` field is an instance-level `List<string>`. Hub instances are per-connection, so this is already per-connection state. Adding a constructor doesn't change this behavior.

---

## Phase 3 — SignalR Authentication Bridge

> **Goal:** Allow agents to authenticate their SignalR WebSocket connections using the same `ApiClientToken` they received during registration.  
> **Effort:** ~2 hours  
> **Depends on:** Phase 2 (hub methods must exist to be useful)  
> **Blocks:** Phase 8 (full end-to-end test)

### Task 3.1 — Understand the Current Auth Gap

- **What:** Document and understand exactly why agent SignalR connections fail auth
- **Why:** The hub has `[Authorize]` on the class (line 12 of `signalrHub.cs`), which requires a valid `ClaimsPrincipal` on `HttpContext.User`. The agent provides its `ApiClientToken` via `AccessTokenProvider` (line 251 of `AgentWorkerService.cs`), which sends the token as `?access_token=<token>` on the WebSocket negotiate request. However:
  1. `ApiKeyMiddleware` only intercepts paths starting with `/api/agent/` (line 27 of `FreeServicesHub.App.ApiKeyMiddleware.cs`) — it does NOT intercept `/freeserviceshubHub`
  2. The hub's `[Authorize]` attribute falls through to the default ASP.NET authentication, which is cookie-based for the Blazor UI — it doesn't know about agent API tokens
  3. Result: the negotiate handshake returns `401 Unauthorized`
- **Where:** This is an architectural gap spanning:
  - `FreeServicesHub/FreeServicesHub/Program.cs` (auth pipeline config, lines 196-200, 232-233)
  - `FreeServicesHub/FreeServicesHub/FreeServicesHub.App.ApiKeyMiddleware.cs` (route filter, line 27)
  - `FreeServicesHub/FreeServicesHub/Hubs/signalrHub.cs` (`[Authorize]` attribute, line 12)
- **When:** First step in Phase 3 — understanding before implementing.

### Task 3.2 — Extend ApiKeyMiddleware to Cover SignalR Negotiate

- **What:** Modify `ApiKeyMiddleware.InvokeAsync` to also intercept the SignalR negotiate endpoint and populate `HttpContext.User` with a valid `ClaimsPrincipal` when a valid agent token is provided
- **Why:** SignalR's negotiate request hits `/freeserviceshubHub/negotiate` as an HTTP POST with `?access_token=<token>`. If we intercept this path and create a `ClaimsPrincipal` from the validated token, the `[Authorize]` attribute will pass.
- **Where:** `FreeServicesHub/FreeServicesHub/FreeServicesHub.App.ApiKeyMiddleware.cs`
- **How:** 
  1. Expand the `requiresAgentAuth` check to also match SignalR paths:
     ```csharp
     bool requiresAgentAuth = path.StartsWith("/api/agent/", StringComparison.OrdinalIgnoreCase);
     bool isSignalRNegotiate = path.StartsWith("/freeserviceshubHub", StringComparison.OrdinalIgnoreCase);
     ```
  2. For SignalR, the token arrives as a query parameter `access_token` (not in the Authorization header):
     ```csharp
     string? token = null;
     if (isSignalRNegotiate)
     {
         token = Context.Request.Query["access_token"];
     }
     else if (requiresAgentAuth)
     {
         // existing header extraction logic
     }
     ```
  3. After validating the token against `ApiClientTokens`, create a `ClaimsPrincipal`:
     ```csharp
     var claims = new List<Claim>
     {
         new Claim(ClaimTypes.NameIdentifier, clientToken.AgentId.ToString()),
         new Claim("AgentId", clientToken.AgentId.ToString()),
         new Claim("TenantId", clientToken.TenantId.ToString()),
     };
     var identity = new ClaimsIdentity(claims, "AgentToken");
     Context.User = new ClaimsPrincipal(identity);
     ```
  4. Stash `AgentId` and `TenantId` in `Context.Items` (same as current behavior for HTTP routes) so the hub can access them
- **When:** After Task 3.1.
- **Critical Detail:** The middleware runs BEFORE `UseAuthentication()` and `UseAuthorization()` in the pipeline (it's registered in `MyAppModifyStart` which calls `app.UseMiddleware<ApiKeyMiddleware>()` before the standard auth middleware at lines 232-233 of `Program.cs`). This means we need to set `Context.User` so the downstream `UseAuthorization()` sees it. This is the correct approach.
- **Alternative Considered:** Adding a custom `AuthenticationHandler<T>`. This is more "correct" per ASP.NET conventions but requires significantly more plumbing (registering a scheme, configuring `AddAuthentication`, etc.) and would touch the template's `Program.cs` more invasively. The middleware approach keeps changes contained to the app-specific extension file.

### Task 3.3 — Pass AgentId into Hub Context

- **What:** Ensure the `AgentId` from the authenticated token is available inside hub method calls
- **Why:** `SendHeartbeat` (Task 2.2) needs to know which agent is sending data. The `ClaimsPrincipal` set in Task 3.2 carries the `AgentId` claim, and `Context.Items` is populated during negotiate.
- **Where:** Hub methods in `signalrHub.cs`
- **How:** In the `SendHeartbeat` method, extract `AgentId` from claims:
  ```csharp
  var agentIdClaim = Context.User?.FindFirst("AgentId")?.Value;
  if (Guid.TryParse(agentIdClaim, out var agentId))
  {
      heartbeat.AgentId = agentId;
  }
  ```
- **When:** After Task 3.2.
- **Note:** `Context.Items` from the HTTP negotiate request may NOT carry over to WebSocket message processing. Claims on `Context.User` DO persist. Use claims, not Items.

### Task 3.4 — Add `[AllowAnonymous]` Alternative (Optional — Dev Mode)

- **What:** Consider adding a dev-mode override that allows unauthenticated SignalR connections
- **Why:** During Aspire-orchestrated development, requiring a valid token for SignalR complicates the startup sequence. A dev-mode bypass would let the agent connect without a real token during local testing.
- **Where:** Would be a conditional `[AllowAnonymous]` or environment-based middleware bypass
- **When:** Phase 5 (Aspire) — only if the auth flow proves too cumbersome for local dev
- **Decision:** Defer. Implement the real auth first. Only add a dev bypass if needed.

---

## Phase 4 — Agent-to-Hub Data Shape Alignment

> **Goal:** Make the data payloads the agent sends match the data contracts the server expects.  
> **Effort:** ~1 hour  
> **Depends on:** Phase 1 (URLs must be correct to test shapes)  
> **Blocks:** Phase 8 (smoke tests)

### Task 4.1 — Fix Registration Request Payload

- **What:** Change the agent's registration request to match `DataObjects.AgentRegistrationRequest`
- **Why:** The agent currently sends:
  ```json
  { "RegistrationKey": "...", "AgentName": "...", "MachineName": "..." }
  ```
  But the server's `AgentRegistrationRequest` (defined in `FreeServicesHub.App.DataObjects.ApiKeys.cs` lines 38-46) expects:
  ```json
  { "RegistrationKey": "...", "Hostname": "...", "OperatingSystem": "...", 
    "Architecture": "...", "AgentVersion": "...", "DotNetVersion": "..." }
  ```
  The server's `RegisterAgent` DataAccess method (line 31-36 of `FreeServicesHub.App.DataAccess.Registration.cs`) reads `Request.Hostname`, `Request.OperatingSystem`, etc. — the agent's `AgentName` and `MachineName` properties are silently ignored, resulting in an agent record with empty `Hostname`, `OperatingSystem`, `Architecture`, `AgentVersion`, and `DotNetVersion` fields.
- **Where:** `FreeServicesHub.Agent/AgentWorkerService.cs` — lines 177-182, the `RegisterWithHub` method
- **How:** Replace the anonymous payload:
  ```csharp
  var payload = new
  {
      RegistrationKey = _options.RegistrationKey,
      Hostname = Environment.MachineName,
      OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
      Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
      AgentVersion = typeof(AgentWorkerService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
      DotNetVersion = Environment.Version.ToString(),
  };
  ```
- **When:** Phase 4, first task.
- **Verification:** After registration, the `Agent` record in the database has populated `Hostname`, `OperatingSystem`, `Architecture`, `AgentVersion`, and `DotNetVersion` fields.

### Task 4.2 — Fix Registration Response Parsing

- **What:** Change the agent to read `ApiClientToken` (and `AgentId`) from the registration response instead of `token`/`Token`
- **Why:** The server returns `AgentRegistrationResponse` (defined in `FreeServicesHub.App.DataObjects.ApiKeys.cs` lines 49-54):
  ```json
  { "agentId": "...", "apiClientToken": "...", "hubUrl": "...", "actionResponse": {...} }
  ```
  But the agent reads `result["token"]` or `result["Token"]` (lines 194-197 of `AgentWorkerService.cs`) — both of which don't exist in the response. The agent logs `"Registration response did not contain a token."` and returns `null`, treating registration as failed even when it succeeded on the server.
- **Where:** `FreeServicesHub.Agent/AgentWorkerService.cs` — lines 193-200
- **How:** Replace the property lookup:
  ```csharp
  var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

  // Read the API client token (camelCase from System.Text.Json default serialization)
  if (result.TryGetProperty("apiClientToken", out var tokenElement))
      return tokenElement.GetString();
  if (result.TryGetProperty("ApiClientToken", out var tokenElement2))
      return tokenElement2.GetString();
  ```
- **When:** After Task 4.1.
- **Enhancement:** Also extract `AgentId` from the response and persist it. This allows the agent to know its own identity for future requests. Consider adding an `AgentId` field to `AgentOptions` and persisting it alongside the token:
  ```csharp
  if (result.TryGetProperty("agentId", out var agentIdElement))
  {
      var agentId = agentIdElement.GetGuid();
      // Persist AgentId for future use
  }
  ```

### Task 4.3 — Align Heartbeat Payload with `DataObjects.AgentHeartbeat`

- **What:** Ensure the heartbeat data the agent sends (via both SignalR and HTTP) can be deserialized into `DataObjects.AgentHeartbeat`
- **Why:** The agent sends its internal `SystemSnapshot` record (lines 30-43 of `AgentWorkerService.cs`), which has properties like `CpuUsagePercent`, `TotalMemoryMb`, `FreeMemoryMb`, `UsedMemoryMb`, `MemoryUsagePercent`, etc. The server's `DataObjects.AgentHeartbeat` (lines 29-41 of `FreeServicesHub.App.DataObjects.Agents.cs`) has different property names: `CpuPercent`, `MemoryPercent`, `MemoryUsedGB`, `MemoryTotalGB`, `DiskMetricsJson`, `AgentName`, etc.
  
  **Property mapping needed:**
  | Agent `SystemSnapshot` | Server `AgentHeartbeat` | Conversion |
  |----------------------|----------------------|------------|
  | `CpuUsagePercent` | `CpuPercent` | Direct |
  | `MemoryUsagePercent` | `MemoryPercent` | Direct |
  | `UsedMemoryMb` | `MemoryUsedGB` | MB → GB (`/ 1024.0`) |
  | `TotalMemoryMb` | `MemoryTotalGB` | MB → GB (`/ 1024.0`) |
  | `Drives` (list of `DriveSnapshot`) | `DiskMetricsJson` (JSON string) | Serialize drives to `List<DiskMetric>` JSON |
  | N/A | `AgentName` | Use `_options.AgentName` |
  | N/A | `AgentId` | Set by hub from auth context |
  | N/A | `HeartbeatId` | Server generates if empty |
  | `TimestampUtc` | `Timestamp` | Direct |
  
- **Where:** `FreeServicesHub.Agent/AgentWorkerService.cs` — the `RunHeartbeatLoop`, `SendHeartbeatViaHttp`, and `FlushBufferedHeartbeats` methods, plus the `CollectSnapshot` method
- **How:** Two options:
  
  **Option A (Recommended) — Add a conversion method:**
  Create a method in `AgentWorkerService` that converts `SystemSnapshot` to a shape matching `AgentHeartbeat`:
  ```csharp
  private object ConvertToHeartbeat(SystemSnapshot snapshot)
  {
      var diskMetrics = snapshot.Drives.Select(d => new {
          Drive = d.Name,
          UsedGB = Math.Round(d.TotalGb - d.FreeGb, 2),
          TotalGB = d.TotalGb,
          Percent = d.UsedPercent,
      }).ToList();

      return new {
          HeartbeatId = Guid.Empty, // Server will generate
          AgentId = Guid.Empty,     // Server will set from auth context
          Timestamp = snapshot.TimestampUtc,
          CpuPercent = snapshot.CpuUsagePercent,
          MemoryPercent = snapshot.MemoryUsagePercent,
          MemoryUsedGB = Math.Round(snapshot.UsedMemoryMb / 1024.0, 2),
          MemoryTotalGB = Math.Round(snapshot.TotalMemoryMb / 1024.0, 2),
          DiskMetricsJson = JsonSerializer.Serialize(diskMetrics),
          CustomDataJson = "",
          AgentName = _options.AgentName,
      };
  }
  ```
  
  **Option B — Reference the DataObjects project directly:**
  Add a project reference from `FreeServicesHub.Agent` to `FreeServicesHub.DataObjects` and use `DataObjects.AgentHeartbeat` directly. This is cleaner but creates a compile-time dependency between agent and server.

  **Decision:** Go with Option A. The agent should be independently deployable without requiring server assemblies. The conversion method keeps the agent self-contained.
  
- **When:** After Tasks 4.1 and 4.2.
- **Verification:** The server's `DataAccess.SaveHeartbeat()` successfully creates an `EFModels.AgentHeartbeat` record with correct CPU, memory, and disk data.

### Task 4.4 — Update Agent's Heartbeat Send Calls

- **What:** Change all `InvokeAsync("SendHeartbeat", snapshot)` calls to use the converted heartbeat object
- **Why:** After Task 4.3, we have a conversion method. All send paths must use it:
  1. `RunHeartbeatLoop` — line 336: `await _hubConnection.InvokeAsync("SendHeartbeat", snapshot, ct);`
  2. `FlushBufferedHeartbeats` — line 414: `await _hubConnection.InvokeAsync("SendHeartbeat", snapshot);`
  3. `SendHeartbeatViaHttp` — line 387: `await httpClient.PostAsJsonAsync("/api/agents/heartbeat", snapshot, ct);`
- **Where:** `FreeServicesHub.Agent/AgentWorkerService.cs` — lines 336, 387, 414
- **How:** Replace `snapshot` with `ConvertToHeartbeat(snapshot)` in all three locations. Also update the HTTP path (already done in Task 1.3).
- **When:** After Task 4.3.
- **Note:** The `_bufferedHeartbeats` list stores `SystemSnapshot` objects. Convert at send time, not at buffer time, so the buffer remains type-safe.

---

## Phase 5 — Aspire AppHost (Local Orchestration)

> **Goal:** Create an Aspire-based local development harness that starts the hub and agent together with automatic port wiring, health checks, and the Aspire dashboard.  
> **Effort:** ~1.5 hours  
> **Depends on:** Phases 1-4 (code must be correct before orchestrating)  
> **Blocks:** Nothing directly — but enables rapid local iteration

### Task 5.1 — Create `FreeServicesHub.AppHost` Project

- **What:** Create a new Aspire AppHost project in the solution
- **Why:** Aspire orchestration provides:
  - Automatic port discovery and injection (no hard-coded `localhost:7271`)
  - Startup ordering (`WaitFor(hub)` ensures the hub is ready before agent starts)
  - Centralized log/trace/metric dashboard
  - One-command reproducibility (`dotnet run`)
  - InMemory database by default for fast iteration
- **Where:** New project at `FreeServicesHub.AppHost/`
- **How:**
  1. Create `FreeServicesHub.AppHost.csproj`:
     ```xml
     <Project Sdk="Microsoft.NET.Sdk">
       <Sdk Name="Aspire.AppHost.Sdk" Version="9.2.0" />
       <PropertyGroup>
         <OutputType>Exe</OutputType>
         <TargetFramework>net10.0</TargetFramework>
         <ImplicitUsings>enable</ImplicitUsings>
         <Nullable>enable</Nullable>
         <RootNamespace>FreeServicesHub.AppHost</RootNamespace>
         <UserSecretsId>freeserviceshub-apphost</UserSecretsId>
       </PropertyGroup>
       <ItemGroup>
         <PackageReference Include="Aspire.Hosting.AppHost" Version="9.2.0" />
       </ItemGroup>
       <ItemGroup>
         <ProjectReference Include="..\FreeServicesHub\FreeServicesHub\FreeServicesHub.csproj" />
         <ProjectReference Include="..\FreeServicesHub.Agent\FreeServicesHub.Agent.csproj" />
       </ItemGroup>
     </Project>
     ```
  2. Add to solution file
- **When:** After all code fixes pass manual testing.
- **Reference Pattern:** `FreeTools.AppHost` uses `Aspire.AppHost.Sdk 9.2.0` with `AddProject<Projects.X>`, `WithEndpoint`, `WithEnvironment`. Same pattern applies here.

### Task 5.2 — Write AppHost `Program.cs`

- **What:** Orchestrate the hub and agent with port wiring and startup ordering
- **Why:** The hub must be alive before the agent attempts registration. Aspire's `WaitFor` ensures this. Environment variable injection replaces the need for the agent to know the hub's port.
- **Where:** `FreeServicesHub.AppHost/Program.cs`
- **How:**
  ```csharp
  var builder = DistributedApplication.CreateBuilder(args);

  // Hub server — pin to same ports as launchSettings.json
  var hub = builder.AddProject<Projects.FreeServicesHub>("hub")
      .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
      .WithEnvironment("DatabaseType", "InMemory")
      .WithEndpoint("https", endpoint =>
      {
          endpoint.Port = 7271;
          endpoint.IsProxied = false;
      })
      .WithEndpoint("http", endpoint =>
      {
          endpoint.Port = 5111;
          endpoint.IsProxied = false;
      });

  // Agent — inject hub URL, skip Windows Service mode, run as console
  builder.AddProject<Projects.FreeServicesHub_Agent>("agent")
      .WithEnvironment("Agent__HubUrl", "https://localhost:7271")
      .WithEnvironment("Agent__RegistrationKey", "<dev-test-key>")
      .WaitFor(hub);

  builder.Build().Run();
  ```
- **When:** After Task 5.1.
- **Open Question:** The agent needs a valid `RegistrationKey` to register. Options:
  1. Pre-seed a known key in the InMemory database on hub startup
  2. Add a dev-only endpoint that auto-generates a key
  3. Hard-code a dev bypass in the agent when `ASPNETCORE_ENVIRONMENT=Development`
  4. Have the AppHost generate a key via HTTP after the hub starts (complex)
  - **Recommendation:** Option 1 — add seed data to `EFDataModel.OnModelCreating` when in Development mode.

### Task 5.3 — Handle Registration Key Seeding for Dev Mode

- **What:** Seed a known registration key in the InMemory database when running in Development mode
- **Why:** The agent needs a `RegistrationKey` to register. In production, keys are generated via the admin API. In local dev, we need one pre-seeded so the agent can auto-register on startup.
- **Where:** Either:
  - A custom `IHostedService` in the hub that seeds on startup
  - Or a dev-only section in `FreeServicesHub.App.Program.cs` `MyAppModifyBuilderEnd`
- **How:** On hub startup in Development mode, check if any registration keys exist. If not, generate one and log it. The agent's AppHost config provides this key via environment variable.
- **When:** After Task 5.2.
- **Alternative:** Manually call `POST /api/Data/GenerateRegistrationKeys/1` after the hub starts and paste the key. This works but breaks the one-command Aspire experience.

### Task 5.4 — Verify Aspire Dashboard Shows Both Projects

- **What:** Run `dotnet run` in the AppHost and verify the Aspire dashboard shows hub + agent, with logs, health, and traces
- **Why:** This is the acceptance test for the Aspire setup.
- **Where:** Browser — Aspire dashboard at `https://localhost:15888` (default)
- **How:** 
  1. `cd FreeServicesHub.AppHost && dotnet run`
  2. Open dashboard
  3. Verify hub starts on port 7271
  4. Verify agent starts, registers, and begins heartbeat loop
  5. Check hub logs for "Agent Registered" message
  6. Check agent logs for "Registration successful. Token stored."
- **When:** After all Task 5.x subtasks.

---

## Phase 6 — CI/CD Pipeline Definition

> **Goal:** Create an Azure DevOps YAML pipeline that builds, tests, deploys the hub, generates a registration key, deploys the agent, and runs smoke tests.  
> **Effort:** ~2 hours  
> **Depends on:** Phases 1-4 (code must work), Phase 5 (optional — tests can run without Aspire)  
> **Blocks:** Nothing — can be created and iterated on independently

### Task 6.1 — Create Pipeline YAML File

- **What:** Create `.azure-pipelines/workflows/freeserviceshub-ci.yml`
- **Why:** No pipeline exists. The user wants to "check it in, create a pipeline, and spawn it off for testing."
- **Where:** `.azure-pipelines/workflows/freeserviceshub-ci.yml` (Azure DevOps convention)
- **How:** Multi-stage pipeline:
  ```yaml
  trigger:
    branches:
      include: [main]

  stages:
    - stage: Build
      jobs:
        - job: BuildAndTest
          steps:
            - dotnet restore
            - dotnet build
            - dotnet test
            - dotnet publish (hub)
            - dotnet publish (agent + installer)
            - publish artifacts

    - stage: DeployHub
      dependsOn: Build
      jobs:
        - job: DeployHubApp
          steps:
            - download artifacts
            - deploy to Azure App Service / IIS / VM
            - health check: curl <hubUrl>/health

    - stage: DeployAgent
      dependsOn: DeployHub
      jobs:
        - job: GenerateKeyAndInstall
          steps:
            - generate registration key: POST /api/Data/GenerateRegistrationKeys/1
            - copy agent files to target
            - run installer: dotnet FreeServicesHub.Agent.Installer.dll configure --Security:ApiKey=<key> ...
            - start service: sc.exe start FreeServicesHubAgent

    - stage: SmokeTest
      dependsOn: DeployAgent
      jobs:
        - job: VerifyAgentOnline
          steps:
            - wait 60s for heartbeat cycle
            - verify: GET /api/Data/GetAgents — agent registered
            - verify: GET /api/Data/GetHeartbeats/<agentId> — heartbeats flowing
  ```
- **When:** Can be created in parallel with code fixes (just can't run until fixes are in).
- **Variables:** `hubUrl`, `agentInstallPath`, `serviceName` — parameterized per environment.

### Task 6.2 — Define Pipeline Variables and Secrets

- **What:** Define variables for hub URL, install paths, and service names per environment
- **Why:** Each environment (Dev, Staging, Prod) has different URLs and service names. The registration key is a secret that should not be hard-coded.
- **Where:** Pipeline variables in Azure DevOps, referenced in YAML
- **How:**
  ```yaml
  variables:
    - group: FreeServicesHub-$(environment)  # Variable group per environment
    # Contains: hubUrl, agentInstallPath, serviceName, hubAdminToken
  ```
- **When:** After Task 6.1.
- **Security:** The `hubAdminToken` (used to generate registration keys) must be stored in a variable group marked as secret. Registration keys are one-time use, so even if exposed in logs, they can't be replayed after the agent uses them.

### Task 6.3 — Add Health Check Endpoint to Hub

- **What:** Add a `/health` endpoint to the hub for pipeline health checks
- **Why:** Stage transitions in the pipeline need to verify the hub is alive before deploying agents. A health endpoint returns 200 when the app is ready.
- **Where:** `FreeServicesHub/FreeServicesHub/Program.cs` or `FreeServicesHub.App.Program.cs`
- **How:** 
  ```csharp
  app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
  ```
  Or use `builder.Services.AddHealthChecks()` + `app.MapHealthChecks("/health")` for the full ASP.NET health check framework.
- **When:** Before Task 6.1 finalizes (the pipeline references this endpoint).

---

## Phase 7 — Integration Test Project

> **Goal:** Create automated tests that verify the registration → connection → heartbeat flow without deploying to a real environment.  
> **Effort:** ~4 hours  
> **Depends on:** Phases 1-4 (code must work)  
> **Blocks:** Nothing — enhances confidence but pipeline can run without it

### Task 7.1 — Create Test Project

- **What:** Create `FreeServicesHub.Tests.Integration` with xUnit and `WebApplicationFactory`
- **Why:** `WebApplicationFactory` spins up the hub in-process with InMemory database. Tests can exercise the full HTTP/SignalR stack without any external dependencies.
- **Where:** New project at `FreeServicesHub.Tests.Integration/`
- **How:**
  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
      <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
    <ItemGroup>
      <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="..." />
      <PackageReference Include="xunit" Version="..." />
      <PackageReference Include="xunit.runner.visualstudio" Version="..." />
    </ItemGroup>
    <ItemGroup>
      <ProjectReference Include="..\FreeServicesHub\FreeServicesHub\FreeServicesHub.csproj" />
      <ProjectReference Include="..\FreeServicesHub.DataObjects\FreeServicesHub.DataObjects.csproj" />
    </ItemGroup>
  </Project>
  ```
- **When:** After Phases 1-4 are complete and manually verified.

### Task 7.2 — Create Hub Test Fixture

- **What:** Create a `HubFixture` class that wraps `WebApplicationFactory<Program>`
- **Why:** Provides a reusable test host with InMemory database and pre-seeded registration keys
- **Where:** `FreeServicesHub.Tests.Integration/HubFixture.cs`
- **How:**
  ```csharp
  public class HubFixture : IAsyncLifetime
  {
      private WebApplicationFactory<Program> _factory;
      public HttpClient Client { get; private set; }

      public async Task InitializeAsync()
      {
          _factory = new WebApplicationFactory<Program>()
              .WithWebHostBuilder(builder =>
              {
                  builder.ConfigureServices(services =>
                  {
                      // Force InMemory database
                      // Seed a test registration key
                      // Disable external auth
                  });
              });
          Client = _factory.CreateClient();
      }

      public Task DisposeAsync() { _factory?.Dispose(); return Task.CompletedTask; }
  }
  ```
- **When:** After Task 7.1.

### Task 7.3 — Write Registration Test

- **What:** Test: POST RegisterAgent with valid key → get back AgentId + ApiClientToken
- **Why:** Validates the complete registration flow: key validation, agent creation, token generation
- **Where:** `FreeServicesHub.Tests.Integration/RegistrationTests.cs`
- **How:**
  ```csharp
  [Fact]
  public async Task RegisterAgent_WithValidKey_ReturnsToken()
  {
      var request = new { RegistrationKey = "test-key", Hostname = "TEST-PC", ... };
      var response = await Client.PostAsJsonAsync("/api/Data/RegisterAgent", request);
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<JsonElement>();
      Assert.True(result.TryGetProperty("apiClientToken", out _));
  }
  ```
- **When:** After Task 7.2.

### Task 7.4 — Write Heartbeat Test

- **What:** Test: POST SaveHeartbeat with valid token → verify saved in database
- **Why:** Validates the heartbeat persistence and threshold evaluation
- **Where:** `FreeServicesHub.Tests.Integration/HeartbeatTests.cs`
- **When:** After Task 7.3.

### Task 7.5 — Write SignalR Connection Test

- **What:** Test: Connect to hub with valid token, call JoinGroup, call SendHeartbeat
- **Why:** Validates the full SignalR path including auth
- **Where:** `FreeServicesHub.Tests.Integration/SignalRTests.cs`
- **When:** After Tasks 7.3 and 7.4 (requires working auth).

---

## Phase 8 — Validation & Smoke Testing

> **Goal:** Verify every change end-to-end in both local and deployed environments.  
> **Effort:** ~2 hours  
> **Depends on:** All previous phases

### Task 8.1 — Manual Local Test (Two Terminals)

- **What:** Start hub in Terminal 1, agent in Terminal 2, verify registration + heartbeats
- **Steps:**
  1. Start hub: `cd FreeServicesHub\FreeServicesHub && dotnet run --launch-profile https`
  2. Generate key: `curl -X POST https://localhost:7271/api/Data/GenerateRegistrationKeys/1 -H "Authorization: Bearer <admin-token>"`
  3. Start agent: `cd FreeServicesHub.Agent && dotnet run -- --Agent:HubUrl=https://localhost:7271 --Agent:RegistrationKey=<key>`
  4. Watch agent logs for: "Registration successful. Token stored."
  5. Watch agent logs for: "Heartbeat sent via SignalR."
  6. Query status: `curl https://localhost:7271/api/Data/GetAgents`
- **Acceptance Criteria:**
  - Agent registers without errors
  - Agent connects to SignalR
  - Agent joins "Agents" group
  - Heartbeats appear in database
  - Hub's `AgentMonitorService` detects the agent and broadcasts status
- **When:** After Phases 1-4.

### Task 8.2 — Aspire Orchestrated Test

- **What:** Run the AppHost and verify everything starts automatically
- **Steps:**
  1. `cd FreeServicesHub.AppHost && dotnet run`
  2. Open Aspire dashboard
  3. Verify both projects start and become healthy
  4. Verify agent registration in hub logs
  5. Verify heartbeat flow
- **When:** After Phase 5.

### Task 8.3 — Pipeline Execution

- **What:** Push changes, create the pipeline in Azure DevOps, run it
- **Steps:**
  1. `git add -A && git commit -m "Fix agent-hub integration gaps (308 plan)" && git push`
  2. In Azure DevOps: Pipelines → New Pipeline → select repo → select YAML file
  3. Run pipeline
  4. Monitor each stage
  5. Verify smoke tests pass
- **When:** After Phase 6.

### Task 8.4 — Parallel Local + Pipeline Testing

- **What:** Run Aspire locally while the pipeline runs in Azure DevOps
- **Why:** This is exactly what the user asked for — "I want to locally test while that runs"
- **How:** These are completely independent:
  - Local Aspire uses InMemory database on localhost — no external dependencies
  - Pipeline deploys to Azure/VM with its own database — isolated environment
  - Both test the same code from the same commit
  - Local iteration catches issues faster; pipeline catches deployment/environment issues
- **When:** After Tasks 8.2 and 8.3 are individually verified.

---

## Dependency Graph

```
Phase 1 (HTTP URLs + Port)  ─────────┐
                                      ├─→ Phase 4 (Data Shapes)
Phase 2 (Hub Methods) ───────────────┤
                                      ├─→ Phase 3 (SignalR Auth)
                                      │
                                      ├─→ Phase 8 (Validation)
                                      │
Phase 5 (Aspire) ←────── All of 1-4 ─┘
                                      
Phase 6 (Pipeline) ←──── Independent (can start in parallel)

Phase 7 (Tests) ←──────── 1-4 (code must work)
```

**Critical path:** Phase 1 → Phase 4 → Phase 3 → Phase 8  
**Parallelizable:** Phase 2 ∥ Phase 1, Phase 6 (structure) ∥ everything

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| SignalR auth changes break Blazor dashboard auth | Medium | High | Test Blazor login after middleware changes. The middleware only adds a code path — it doesn't remove the existing cookie auth. |
| InMemory database loses state on hub restart (Aspire) | Certain | Low | Expected behavior. Agent re-registers on restart. Pipeline uses persistent database. |
| Agent sends heartbeat before hub is ready (race condition) | Medium | Medium | Aspire's `WaitFor` prevents this. Manual testing requires starting hub first. Agent already has retry logic. |
| `sc.exe` commands require admin elevation in pipeline | High | Medium | Pipeline agent must run as admin or use a service account with service management permissions. Document in pipeline YAML. |
| Registration key expires before agent starts (24h default) | Low | Low | Only relevant if there's a long delay between key generation and agent deployment. Increase `RegistrationKeyExpiryHours` if needed. |
| Template `Program.cs` updates overwrite app hook files | Low | High | All changes are in `FreeServicesHub.App.*` files, which are not overwritten by template updates. This is the correct pattern. |

---

## Files Changed Summary

| File | Action | Phase | Description |
|------|--------|-------|-------------|
| `FreeServicesHub.Agent/appsettings.json` | Modify | 1 | Fix HubUrl port to 7271 |
| `FreeServicesHub.Agent/AgentWorkerService.cs` | Modify | 1, 4 | Fix URLs, request/response shapes, heartbeat conversion |
| `FreeServicesHub/Hubs/signalrHub.cs` | Modify | 2 | Add JoinGroup, SendHeartbeat methods, DI constructor |
| `FreeServicesHub/FreeServicesHub.App.ApiKeyMiddleware.cs` | Modify | 3 | Extend to cover SignalR negotiate + create ClaimsPrincipal |
| `FreeServicesHub.AppHost/FreeServicesHub.AppHost.csproj` | Create | 5 | Aspire AppHost project |
| `FreeServicesHub.AppHost/Program.cs` | Create | 5 | Orchestration with port wiring |
| `.azure-pipelines/workflows/freeserviceshub-ci.yml` | Create | 6 | CI/CD pipeline |
| `FreeServicesHub.Tests.Integration/*.cs` | Create | 7 | Integration tests |

---

*End of 308 — Implementation Plan*
