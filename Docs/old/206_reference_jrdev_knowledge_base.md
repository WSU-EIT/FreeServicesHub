# 206 — Reference: JrDev Knowledge Base

> **Document ID:** 206
> **Category:** Reference
> **Purpose:** Common questions answered so JrDev (and future AI agents) don't have to rediscover things.
> **Audience:** [JrDev], new contributors, AI agents.

---

## Part 1: Q&A — Things JrDev Asked That Everyone Should Know

### Q: Why does SignalR work through corporate firewalls when raw WebSockets get blocked?

**[Architect]:** SignalR negotiates over standard HTTPS (port 443) first. The initial handshake is a regular HTTP POST to `/negotiate`. Once that succeeds, it upgrades to WebSocket over the same port. Firewalls see HTTPS traffic, not a weird port. If WebSocket upgrade fails, SignalR falls back to Server-Sent Events, then Long Polling — all over port 443. The agent never needs a special firewall rule.

### Q: Why SHA-256 for API keys instead of bcrypt?

**[Security]:** Different threat model. Bcrypt is slow on purpose — it protects passwords that humans pick (short, predictable). Our API keys are 32 random bytes — no dictionary attack possible. SHA-256 is fast, which matters because we validate on every heartbeat (every 30 seconds per agent). Bcrypt at 100ms per hash × 50 agents = 5 seconds of CPU per heartbeat cycle. SHA-256 does the same in microseconds. The key itself is the entropy, not the hash function.

### Q: What's the `.App.` extension pattern and why can't I just edit the framework files?

**[Architect]:** The base framework (FreeCRM) gets updated independently. If you edit `DataAccess.cs` directly, the next framework update overwrites your work. Instead, every framework file that supports customization has a `.App.` companion:

```
Framework file:          DataAccess.cs        (never touch)
Your extension:          DataAccess.App.cs    (your code here)
Framework file:          Program.cs           (never touch)
Your extension:          Program.App.cs       (your hooks here)
```

Both are `partial class` — C# merges them at compile time. The framework file calls into your `.App.` file at specific hook points (`AppModifyBuilderEnd`, `ProcessBackgroundTasksApp`, `SignalRUpdateApp`, etc.). One-line tie-ins, never full rewrites.

### Q: How does EF work here without migrations?

**[Backend]:** The EF entities define the schema. The `EFDataModel` is a partial class — the base defines `Departments`, `Users`, etc. Our `FreeServicesHub.App.EFDataModel.cs` adds `Agents`, `RegistrationKeys`, etc. via `OnModelCreatingPartial`. When using InMemory provider for dev, tables just appear. For real databases, we scaffold migrations separately per provider (SQLite, SQL Server, etc.) using the commented-out `OnConfiguring` connection strings. The migration files live in `DataMigrations.*.cs`.

### Q: How does multi-tenancy work? Does every agent belong to a tenant?

**[Backend]:** Yes. Every entity has a `TenantId` (GUID). The base framework handles tenant isolation — `DataAccess` methods filter by `CurrentUser.TenantId` automatically. When an agent registers, it gets assigned to the tenant that owns the registration key. Dashboard viewers only see their tenant's agents. The SignalR hub uses tenant-specific groups (`JoinTenantId`).

### Q: What happens if the agent loses SignalR mid-heartbeat?

**[AgentDev]:** The agent catches the disconnect and enters reconnect mode with exponential backoff (2s, 4s, 8s, 16s, then 30s forever). While disconnected, it buffers heartbeat data and logs locally. When reconnected, it flushes the buffer. The hub side detects the disconnect via `OnDisconnectedAsync`, marks the agent as offline, and broadcasts `AgentDisconnected` to dashboard viewers. The `AgentMonitorService` also has a stale check — if no heartbeat for 120 seconds, it marks the agent Stale even if SignalR didn't report a disconnect.

### Q: Why does the installer write to appsettings.json instead of using environment variables?

**[AgentDev]:** Windows services don't inherit user environment variables reliably. The service runs as `LocalSystem` or a service account — its environment is different from the admin who installed it. Writing directly to `appsettings.json` means the config is always where the service expects it. The FreeServices installer does the same thing — see `FreeServices.Installer/Program.cs`. The CI/CD pipeline also uses `FileTransform@2` to inject values into `appsettings.json` during deployment, so everything follows the same pattern.

### Q: What's a BackgroundService and how does the worker loop work?

**[AgentDev]:** `BackgroundService` is a .NET class that runs in the background of your app. You override `ExecuteAsync` and put your loop there:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        // Do work (collect snapshot, send heartbeat)
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
    }
}
```

The `stoppingToken` fires when the service is told to stop (shutdown command, `sc stop`, Ctrl+C). You register it in `Program.cs` with `builder.Services.AddHostedService<AgentWorkerService>()`. The FreeServices `SystemMonitorService.cs` is our reference — same loop, just collecting system snapshots instead of sending heartbeats.

### Q: How do Windows services get installed?

**[AgentDev]:** On Windows, `sc.exe create` registers a service with the Service Control Manager. Our installer calls it like:

```
sc.exe create FreeServicesHub.Agent binPath="C:\path\to\FreeServicesHub.Agent.exe" start=delayed-auto
sc.exe description FreeServicesHub.Agent "FreeServicesHub Agent Service"
sc.exe start FreeServicesHub.Agent
```

We're targeting **Windows only** — no Linux systemd or macOS launchd. The installer also writes the registration key and hub URL into `appsettings.json` before starting the service. See `FreeServices.Installer/Program.cs` for the full pattern with the dual-interface (interactive menu + CLI flags) and `.configured` marker.

---

## Part 2: Pseudocode Reference — Patterns We're Adapting

### Pattern 1: API Key Generation (from FreeGLBA)

**Source:** `FreeGLBA.App.DataAccess.ApiKey.cs`

```
FUNCTION GenerateApiKey():
    // First, generate 32 random bytes
    bytes = new byte[32]
    RandomNumberGenerator.Fill(bytes)
    plaintext = Convert.ToBase64String(bytes)
    
    // Now, hash it for storage
    hash = SHA256(UTF8.GetBytes(plaintext))
    hashString = Convert.ToBase64String(hash)
    prefix = plaintext.Substring(0, 8)
    
    RETURN { Plaintext: plaintext, Hash: hashString, Prefix: prefix }
    // Only return plaintext ONCE. Store only Hash and Prefix.

FUNCTION ValidateApiKey(providedKey):
    hash = SHA256(UTF8.GetBytes(providedKey))
    hashString = Convert.ToBase64String(hash)
    record = DB.ApiClientTokens.FirstOrDefault(t => t.TokenHash == hashString AND t.Active)
    RETURN record  // null means invalid
```

### Pattern 2: API Key Middleware (from FreeGLBA)

**Source:** `FreeGLBA.App.ApiKeyMiddleware.cs`

```
FUNCTION Invoke(HttpContext context):
    // See if this request path needs API key auth
    IF path does NOT start with "/api/Data/Agent":
        next(context)  // Skip, not an agent endpoint
        RETURN
    
    // Check for Authorization header
    header = context.Request.Headers["Authorization"]
    IF header missing OR NOT starts with "Bearer ":
        context.Response.StatusCode = 401
        RETURN
    
    token = header.Replace("Bearer ", "").Trim()
    agent = ValidateApiKey(token)
    
    IF agent is null:
        context.Response.StatusCode = 401
        RETURN
    
    // Valid — store agent info for downstream use
    context.Items["Agent"] = agent
    next(context)
```

### Pattern 3: Worker Loop (from FreeServices)

**Source:** `FreeServices.Service/SystemMonitorService.cs`

```
CLASS AgentWorkerService : BackgroundService

    ExecuteAsync(CancellationToken stoppingToken):
        WHILE NOT stoppingToken.IsCancellationRequested:
            TRY:
                snapshot = CollectSystemSnapshot()
                    // CPU via PerformanceCounter or /proc/stat
                    // Memory via GC + OS APIs
                    // Disk via DriveInfo.GetDrives()
                
                await SendHeartbeat(snapshot)
                await Task.Delay(interval, stoppingToken)
            CATCH TaskCanceledException:
                BREAK  // Normal shutdown
            CATCH Exception:
                LogError(ex)
                await Task.Delay(retryDelay, stoppingToken)
```

### Pattern 4: AgentMonitorService (from FreeCICD)

**Source:** `FreeCICD.App.PipelineMonitorService.cs`

```
CLASS AgentMonitorService : BackgroundService

    _agentStatuses = new ConcurrentDictionary<Guid, AgentStatus>()

    ExecuteAsync(CancellationToken stoppingToken):
        WHILE NOT stoppingToken.IsCancellationRequested:
            // Only do work if dashboard viewers are subscribed
            IF AgentMonitor group has subscribers:
                CheckForStaleAgents()
                BroadcastChanges()
            await Task.Delay(10_000, stoppingToken)

    OnHeartbeatReceived(agentId, snapshot):
        oldStatus = _agentStatuses.GetOrAdd(agentId, default)
        newStatus = CalculateStatus(snapshot)  // OK/Warning/Error
        _agentStatuses[agentId] = newStatus
        
        IF oldStatus != newStatus:
            Broadcast AgentStatusChanged to "AgentMonitor" group
        ELSE:
            Broadcast AgentHeartbeat to "AgentMonitor" group

    CheckForStaleAgents():
        FOR EACH agent in _agentStatuses:
            IF agent.LastHeartbeat < Now - StaleThreshold:
                agent.Status = "Stale"
                Broadcast AgentDisconnected
```

### Pattern 5: AboutSection and InfoTip (from FreeExamples)

**Source:** `FreeExamples.Client/Shared/AboutSection.razor`, `InfoTip.razor`

```
COMPONENT AboutSection:
    Parameters: Title, Subtitle, Icon, StartExpanded (default: false)
    
    Renders a Bootstrap card with:
    - Collapsible header (click to expand/collapse)
    - Icon + Title + Subtitle
    - @ChildContent inside card body
    
    Usage:
    <AboutSection Title="Agent Dashboard" Icon="fa-server"
                  Subtitle="Real-time monitoring for all service agents">
        <p>What this page does...</p>
    </AboutSection>

COMPONENT InfoTip:
    Parameters: Title, Description, Code (optional)
    
    Renders an inline (i) icon that shows a popover on click:
    - Title in bold
    - Description paragraph
    - Optional code snippet in <pre> block
    - Close button
    - Smart positioning (flips left/right based on screen edge)
    
    Usage:
    <InfoTip Title="API Client Token"
             Description="Long-lived token issued during registration. 
                          The hub stores only the SHA-256 hash." />
```

---

## Framework Dependencies Already Present (Don't Add These)

| Dependency | Version | Location | Used For |
|-----------|---------|----------|----------|
| **Bootstrap 5** | 5.x | wwwroot/lib/ | Grid, cards, alerts, modals, utilities |
| **Font Awesome** | 6.x | wwwroot/lib/ | Icons (fa-server, fa-check-circle, etc.) |
| **jQuery** | 3.7.0 | wwwroot/lib/ | DOM manipulation, AJAX |
| **SortableJS** | — | wwwroot/lib/ | Drag/drop sorting |
| **Radzen** | — | NuGet | DialogService, NotificationService |
| **Highcharts** | — | CDN chain-load via Modules.App.razor | Charts on Agent Detail page |
| **MudBlazor** | — | NuGet | Additional Blazor components |
| **BlazorBootstrap** | — | NuGet | Bootstrap Blazor wrappers |

**Do not add new CSS/JS frameworks.** Everything we need is already loaded. Highcharts gets chain-loaded via CDN in `Modules.App.razor` when needed — no local install required.

---

*Created: 2026-04-08*
*Maintained by: [Quality]*
