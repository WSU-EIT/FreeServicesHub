# 311 — FreeServicesHub Architecture Report
## Full ASCII Art Model of Every Moving Part

Generated from source analysis — reflects current codebase state.

---

## 1. System Overview — The Big Picture

```
 ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
 │                          F R E E S E R V I C E S H U B                                     │
 │                                                                                             │
 │  ┌───────────────────┐     ┌────────────────────────────┐     ┌───────────────────────────┐  │
 │  │   INSTALLER (CLI) │     │      HUB WEB APP           │     │    AGENT (Worker Svc)     │  │
 │  │   ─────────────── │     │  ────────────────────────── │     │  ─────────────────────── │  │
 │  │  Build & Deploy   │     │  Blazor Server + WASM       │     │  BackgroundService       │  │
 │  │  the Agent as a   │────▶│  SignalR Hub                │◀────│  Heartbeats + Snapshots  │  │
 │  │  Windows Service  │     │  REST API                   │     │  Windows Service or CLI  │  │
 │  │                   │     │  Agent Monitor              │     │                           │  │
 │  └───────────────────┘     └────────────────────────────┘     └───────────────────────────┘  │
 │                                        │                                                     │
 │                               ┌────────┴────────┐                                            │
 │                               │   DATA LAYER    │                                            │
 │                               │  EF Core + DA   │                                            │
 │                               │  Multi-Provider │                                            │
 │                               └─────────────────┘                                            │
 └─────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. The Agent — `FreeServicesHub.Agent`

```
 ┌═══════════════════════════════════════════════════════════════════════════════════════┐
 ║                        AGENT  (FreeServicesHub.Agent.exe)                            ║
 ║                        Target: .NET 10 | Windows Service                            ║
 ╠═════════════════════════════════════════════════════════════════════════════════════════╣
 ║                                                                                     ║
 ║  ┌─────────────────────────────────────────────────────────────────────────────────┐  ║
 ║  │                          Program.cs — Host Builder                             │  ║
 ║  │                                                                                 │  ║
 ║  │   Host.CreateDefaultBuilder(args)                                               │  ║
 ║  │     .UseWindowsService()              ◄── runs as Windows Service (sc.exe)     │  ║
 ║  │     .ConfigureAppConfiguration(...)   ◄── appsettings.json                     │  ║
 ║  │     .ConfigureLogging(logging => {                                              │  ║
 ║  │         logging.AddProvider(                                                    │  ║
 ║  │           new FileLoggerProvider(     ◄── rolling file logger                  │  ║
 ║  │             "agent.log", 5MB))                                                  │  ║
 ║  │       })                                                                        │  ║
 ║  │     .ConfigureServices(s =>                                                     │  ║
 ║  │         s.AddHostedService<                                                     │  ║
 ║  │           AgentWorkerService>())      ◄── the main background service          │  ║
 ║  │                                                                                 │  ║
 ║  │   Program.Lifetime = host...Lifetime  ◄── exposed for SignalR Shutdown cmd     │  ║
 ║  └─────────────────────────────────────────────────────────────────────────────────┘  ║
 ║                                         │                                            ║
 ║                                         ▼                                            ║
 ║  ┌─────────────────────────────────────────────────────────────────────────────────┐  ║
 ║  │                   AgentWorkerService : BackgroundService                        │  ║
 ║  │                                                                                 │  ║
 ║  │   ExecuteAsync(CancellationToken stoppingToken)                                 │  ║
 ║  │     │                                                                           │  ║
 ║  │     ├── LoadOptions()  ◄── reads "Agent" section from appsettings.json         │  ║
 ║  │     │     ┌──────────────────────────────────────┐                              │  ║
 ║  │     │     │  AgentOptions                        │                              │  ║
 ║  │     │     │    HubUrl: "https://localhost:7271"  │                              │  ║
 ║  │     │     │    RegistrationKey: ""               │                              │  ║
 ║  │     │     │    ApiClientToken: ""                │                              │  ║
 ║  │     │     │    HeartbeatIntervalSeconds: 30      │                              │  ║
 ║  │     │     │    AgentName: <MachineName>          │                              │  ║
 ║  │     │     └──────────────────────────────────────┘                              │  ║
 ║  │     │                                                                           │  ║
 ║  │     ├── DECISION POINT: has credentials?                                        │  ║
 ║  │     │                                                                           │  ║
 ║  │     │   NO credentials ──────────────┐                                          │  ║
 ║  │     │                                ▼                                          │  ║
 ║  │     │              ┌──────────────────────────────────┐                         │  ║
 ║  │     │              │     STANDALONE MODE              │                         │  ║
 ║  │     │              │  ┌───────────────────────────┐   │                         │  ║
 ║  │     │              │  │ RunStandaloneLoop()       │   │                         │  ║
 ║  │     │              │  │  while (!cancelled)       │   │                         │  ║
 ║  │     │              │  │    CollectSnapshot()      │   │                         │  ║
 ║  │     │              │  │    LogSnapshotToConsole() │   │                         │  ║
 ║  │     │              │  │    Delay(30s)             │   │                         │  ║
 ║  │     │              │  └───────────────────────────┘   │                         │  ║
 ║  │     │              │  Output: console + agent.log     │                         │  ║
 ║  │     │              └──────────────────────────────────┘                         │  ║
 ║  │     │                                                                           │  ║
 ║  │     │   HAS credentials ─────────────┐                                          │  ║
 ║  │     │                                ▼                                          │  ║
 ║  │     │              ┌──────────────────────────────────┐                         │  ║
 ║  │     │              │     CONNECTED MODE               │                         │  ║
 ║  │     │              │                                  │                         │  ║
 ║  │     │              │  1. RegisterWithHub()            │                         │  ║
 ║  │     │              │     POST /api/Data/RegisterAgent │                         │  ║
 ║  │     │              │     → receives ApiClientToken    │                         │  ║
 ║  │     │              │     → PersistToken() to json     │                         │  ║
 ║  │     │              │                                  │                         │  ║
 ║  │     │              │  2. ConnectToSignalR()           │                         │  ║
 ║  │     │              │     → /freeserviceshubHub        │                         │  ║
 ║  │     │              │     → Bearer token auth          │                         │  ║
 ║  │     │              │     → JoinGroup("Agents")       │                         │  ║
 ║  │     │              │     → Listen: "Shutdown" cmd    │                         │  ║
 ║  │     │              │                                  │                         │  ║
 ║  │     │              │  3. RunHeartbeatLoop()           │                         │  ║
 ║  │     │              │     ┌──── loop every 30s ────┐  │                         │  ║
 ║  │     │              │     │ CollectSnapshot()      │  │                         │  ║
 ║  │     │              │     │ IF connected:          │  │                         │  ║
 ║  │     │              │     │   FlushBuffered()      │  │                         │  ║
 ║  │     │              │     │   SendHeartbeat(hub)   │  │                         │  ║
 ║  │     │              │     │ ELSE:                  │  │                         │  ║
 ║  │     │              │     │   HTTP fallback POST   │  │                         │  ║
 ║  │     │              │     │   Buffer if failed     │  │                         │  ║
 ║  │     │              │     │   TryReconnectSignalR  │  │                         │  ║
 ║  │     │              │     └────────────────────────┘  │                         │  ║
 ║  │     │              └──────────────────────────────────┘                         │  ║
 ║  │     │                                                                           │  ║
 ║  │     │   REGISTRATION FAILED ─► fallback to STANDALONE MODE                     │  ║
 ║  │     │                                                                           │  ║
 ║  └─────┴───────────────────────────────────────────────────────────────────────────┘  ║
 ║                                                                                     ║
 ║  ┌─────────────────────────────────────────────────────────────────────────────────┐  ║
 ║  │                    System Data Collectors (Windows)                             │  ║
 ║  │                                                                                 │  ║
 ║  │   CollectSnapshot()                                                             │  ║
 ║  │     ├── MeasureCpuWindows()     powershell Get-CimInstance Win32_Processor      │  ║
 ║  │     ├── GetMemoryWindows()      GC.GetGCMemoryInfo() total/load bytes           │  ║
 ║  │     ├── GetDriveSnapshots()     DriveInfo.GetDrives() → fixed drives only       │  ║
 ║  │     └── Environment.*           MachineName, ProcessorCount, OSDescription      │  ║
 ║  │                                                                                 │  ║
 ║  │   ┌───────────────────────────────────────────────────────┐                     │  ║
 ║  │   │  SystemSnapshot                                      │                     │  ║
 ║  │   │    MachineName, OsDescription, ProcessorCount        │                     │  ║
 ║  │   │    CpuUsagePercent, TotalMemoryMb, FreeMemoryMb      │                     │  ║
 ║  │   │    UsedMemoryMb, MemoryUsagePercent                  │                     │  ║
 ║  │   │    Drives: List<DriveSnapshot>                       │                     │  ║
 ║  │   │    Uptime, TimestampUtc                              │                     │  ║
 ║  │   └───────────────────────────────────────────────────────┘                     │  ║
 ║  │                         │                                                       │  ║
 ║  │                         ▼ ConvertToHeartbeat()                                  │  ║
 ║  │   ┌───────────────────────────────────────────────────────┐                     │  ║
 ║  │   │  AgentHeartbeat (anonymous object)                   │                     │  ║
 ║  │   │    HeartbeatId, AgentId, Timestamp                   │                     │  ║
 ║  │   │    CpuPercent, MemoryPercent                         │                     │  ║
 ║  │   │    MemoryUsedGB, MemoryTotalGB                       │                     │  ║
 ║  │   │    DiskMetricsJson (serialized drives)               │                     │  ║
 ║  │   │    AgentName                                         │                     │  ║
 ║  │   └───────────────────────────────────────────────────────┘                     │  ║
 ║  └─────────────────────────────────────────────────────────────────────────────────┘  ║
 ║                                                                                     ║
 ║  ┌─────────────────────────────────────────────────────────────────────────────────┐  ║
 ║  │                    FileLoggerProvider                                           │  ║
 ║  │                                                                                 │  ║
 ║  │   ILoggerProvider implementation                                                │  ║
 ║  │   ├── Rolling file: agent.log (5 MB cap)                                       │  ║
 ║  │   ├── Thread-safe via Lock writeLock                                            │  ║
 ║  │   ├── ConcurrentDictionary<string, FileLogger>                                 │  ║
 ║  │   └── Mirrors all ILogger output to disk                                       │  ║
 ║  │                                                                                 │  ║
 ║  │   Console ──┐                                                                   │  ║
 ║  │             ├──▶ stdout (ConsoleLoggerProvider)                                 │  ║
 ║  │   ILogger ──┤                                                                   │  ║
 ║  │             └──▶ agent.log (FileLoggerProvider)                                 │  ║
 ║  └─────────────────────────────────────────────────────────────────────────────────┘  ║
 ║                                                                                     ║
 ║  ┌─────────────────────────────────────────────────────────────────────────────────┐  ║
 ║  │                    SignalR Reconnection Strategy                                │  ║
 ║  │                                                                                 │  ║
 ║  │   ExponentialBackoffRetryPolicy : IRetryPolicy                                  │  ║
 ║  │     Retry 1:  2s                                                                │  ║
 ║  │     Retry 2:  4s                                                                │  ║
 ║  │     Retry 3:  8s                                                                │  ║
 ║  │     Retry 4: 16s                                                                │  ║
 ║  │     Retry 5+: 30s (capped)                                                     │  ║
 ║  │     Never gives up (always returns a delay)                                     │  ║
 ║  └─────────────────────────────────────────────────────────────────────────────────┘  ║
 ╚═════════════════════════════════════════════════════════════════════════════════════════╝
```

---

## 3. The Installer — `FreeServicesHub.Agent.Installer`

```
 ┌═══════════════════════════════════════════════════════════════════════════════════════┐
 ║                    INSTALLER  (FreeServicesHub.Agent.Installer.exe)                  ║
 ║                    Dual Mode: Interactive Menu  OR  CLI Headless                     ║
 ╠═════════════════════════════════════════════════════════════════════════════════════════╣
 ║                                                                                     ║
 ║  ┌─────────────────────────────────────────────────────────────────────────────────┐  ║
 ║  │                        Entry Point — Program.cs                                │  ║
 ║  │                                                                                 │  ║
 ║  │  ConfigurationBuilder                                                           │  ║
 ║  │    .AddJsonFile("appsettings.json")                                             │  ║
 ║  │    .AddCommandLine(args)               ◄── CLI overrides                       │  ║
 ║  │    → Bind to InstallerConfig                                                    │  ║
 ║  │                                                                                 │  ║
 ║  │  Route Decision:                                                                │  ║
 ║  │    args has action? ──YES──▶ NonInteractive = true → RunAction()               │  ║
 ║  │                      NO───▶ RunInteractive() → menu loop                       │  ║
 ║  └─────────────────────────────────────────────────────────────────────────────────┘  ║
 ║                                                                                     ║
 ║  ┌──────────────────────────────────────┐                                            ║
 ║  │  InstallerConfig                     │                                            ║
 ║  │  ├── Service                         │                                            ║
 ║  │  │   ├── Name: "FreeServicesHubAgent"│                                            ║
 ║  │  │   ├── DisplayName                 │                                            ║
 ║  │  │   ├── Description                 │                                            ║
 ║  │  │   ├── ExePath                     │                                            ║
 ║  │  │   └── InstallPath                 │                                            ║
 ║  │  ├── Publish                         │                                            ║
 ║  │  │   ├── ProjectPath                 │                                            ║
 ║  │  │   ├── OutputPath                  │                                            ║
 ║  │  │   ├── Runtime: "win-x64"          │                                            ║
 ║  │  │   ├── SelfContained               │                                            ║
 ║  │  │   └── SingleFile                  │                                            ║
 ║  │  ├── Security                        │                                            ║
 ║  │  │   └── ApiKey (optional)           │                                            ║
 ║  │  └── NonInteractive (bool)           │                                            ║
 ║  └──────────────────────────────────────┘                                            ║
 ║                                                                                     ║
 ║  ┌─────────────────────────────────────────────────────────────────────────────────┐  ║
 ║  │                     INTERACTIVE MENU                                            │  ║
 ║  │                                                                                 │  ║
 ║  │   ╔════════════════════════════════════════════════════════════╗                 │  ║
 ║  │   ║  FreeServicesHub Agent Installer                         ║                 │  ║
 ║  │   ║                                                          ║                 │  ║
 ║  │   ║  Status: CONFIGURED | NOT CONFIGURED                    ║                 │  ║
 ║  │   ║                                                          ║                 │  ║
 ║  │   ║  BUILD & DEPLOY                                         ║                 │  ║
 ║  │   ║    1. Build (dotnet publish)                             ║                 │  ║
 ║  │   ║                                                          ║                 │  ║
 ║  │   ║  SERVICE MANAGEMENT                                     ║                 │  ║
 ║  │   ║    2. Configure (install + set API key)                  ║                 │  ║
 ║  │   ║    3. Remove (stop + uninstall)                          ║                 │  ║
 ║  │   ║    4. Start Service                                      ║                 │  ║
 ║  │   ║    5. Stop Service                                       ║                 │  ║
 ║  │   ║    6. Query Status                                       ║                 │  ║
 ║  │   ║                                                          ║                 │  ║
 ║  │   ║  MAINTENANCE                                             ║                 │  ║
 ║  │   ║    7. Destroy (undo everything)                          ║                 │  ║
 ║  │   ║                                                          ║                 │  ║
 ║  │   ║  Q. Quit                                                 ║                 │  ║
 ║  │   ╚════════════════════════════════════════════════════════════╝                 │  ║
 ║  └─────────────────────────────────────────────────────────────────────────────────┘  ║
 ║                                                                                     ║
 ║  ┌─────────────────────────────────────────────────────────────────────────────────┐  ║
 ║  │                     ACTION PIPELINE — what each action does                    │  ║
 ║  │                                                                                 │  ║
 ║  │  ┌─────────────┐                                                                │  ║
 ║  │  │ 1. BUILD    │  dotnet publish → -c Release -r win-x64                       │  ║
 ║  │  │             │  Optional: --self-contained, -p:PublishSingleFile=true         │  ║
 ║  │  │             │  Output → Publish.OutputPath                                   │  ║
 ║  │  └──────┬──────┘                                                                │  ║
 ║  │         ▼                                                                       │  ║
 ║  │  ┌─────────────┐                                                                │  ║
 ║  │  │ 2. CONFIG   │  Guard: not already configured                                │  ║
 ║  │  │             │  Prompt: service name, API key (optional)                      │  ║
 ║  │  │             │  Prompt: source path, install path                             │  ║
 ║  │  │             │  ├── CopyPublishToInstall()  (xcopy publish → install dir)     │  ║
 ║  │  │             │  ├── WriteApiKeyToServiceConfig() (if key provided)            │  ║
 ║  │  │             │  ├── sc.exe create ... binPath= ... start= auto               │  ║
 ║  │  │             │  ├── sc.exe description ...                                    │  ║
 ║  │  │             │  ├── sc.exe failure ... restart/5000 (3x)                      │  ║
 ║  │  │             │  └── WriteConfiguredMarker()                                   │  ║
 ║  │  └──────┬──────┘                                                                │  ║
 ║  │         ▼                                                                       │  ║
 ║  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                             │  ║
 ║  │  │ 3. REMOVE   │  │ 4. START    │  │ 5. STOP     │                             │  ║
 ║  │  │ sc stop     │  │ sc start    │  │ sc stop     │                             │  ║
 ║  │  │ sc delete   │  │ <svcname>   │  │ <svcname>   │                             │  ║
 ║  │  │ clear key   │  └─────────────┘  └─────────────┘                             │  ║
 ║  │  │ rm marker   │                                                                │  ║
 ║  │  └─────────────┘                                                                │  ║
 ║  │                                                                                 │  ║
 ║  │  ┌─────────────┐                                                                │  ║
 ║  │  │ 6. STATUS   │  sc query <svcname>                                           │  ║
 ║  │  │             │  tail -10 agent.log (from FileLoggerProvider)                  │  ║
 ║  │  └─────────────┘                                                                │  ║
 ║  │                                                                                 │  ║
 ║  │  ┌─────────────┐                                                                │  ║
 ║  │  │ 7. DESTROY  │  "Type DESTROY to confirm" (unless NonInteractive)            │  ║
 ║  │  │             │  [1/4] sc stop + sc delete                                     │  ║
 ║  │  │             │  [2/4] rm -rf InstallPath                                      │  ║
 ║  │  │             │  [3/4] rm -rf PublishPath                                      │  ║
 ║  │  │             │  [4/4] rm .configured marker                                   │  ║
 ║  │  └─────────────┘                                                                │  ║
 ║  └─────────────────────────────────────────────────────────────────────────────────┘  ║
 ║                                                                                     ║
 ║  ┌─────────────────────────────────────────────────────────────────────────────────┐  ║
 ║  │                     NonInteractive Detection                                   │  ║
 ║  │                                                                                 │  ║
 ║  │   CLI invocation with action arg → NonInteractive = true (auto)                │  ║
 ║  │   No action arg → Interactive menu → prompts + ReadLine() safe                 │  ║
 ║  │   Prevents pipeline hangs (no Console.ReadLine in CI/CD)                       │  ║
 ║  └─────────────────────────────────────────────────────────────────────────────────┘  ║
 ╚═════════════════════════════════════════════════════════════════════════════════════════╝
```

---

## 4. The Hub Web App — `FreeServicesHub` (Blazor Server + SignalR)

```
 ┌═══════════════════════════════════════════════════════════════════════════════════════════════┐
 ║                      HUB WEB APP  (FreeServicesHub.exe)                                     ║
 ║                      Blazor Server + WebAssembly | SignalR | REST API                        ║
 ║                      Ports: https://localhost:7271  http://localhost:5111                    ║
 ╠═══════════════════════════════════════════════════════════════════════════════════════════════════╣
 ║                                                                                             ║
 ║  ┌───────────────────────────────────────────────────────────────────────────────────────┐    ║
 ║  │                        REQUEST PIPELINE                                              │    ║
 ║  │                                                                                       │    ║
 ║  │   Incoming Request                                                                    │    ║
 ║  │        │                                                                              │    ║
 ║  │        ▼                                                                              │    ║
 ║  │   ┌─────────────────────────────────────────────────────────────────────┐              │    ║
 ║  │   │              ApiKeyMiddleware                                      │              │    ║
 ║  │   │                                                                     │              │    ║
 ║  │   │   Path Check:                                                       │              │    ║
 ║  │   │     /api/agent/*        → requires Bearer token                    │              │    ║
 ║  │   │     /freeserviceshubHub → requires Bearer token (negotiate)        │              │    ║
 ║  │   │     /api/Data/*         → pass through to Blazor auth              │              │    ║
 ║  │   │     /health             → pass through (anonymous)                 │              │    ║
 ║  │   │     everything else     → pass through (Blazor UI)                 │              │    ║
 ║  │   │                                                                     │              │    ║
 ║  │   │   Token Extraction:                                                 │              │    ║
 ║  │   │     1. Query string: ?access_token=<token>  (SignalR WebSocket)    │              │    ║
 ║  │   │     2. Header: Authorization: Bearer <token> (REST API)            │              │    ║
 ║  │   │                                                                     │              │    ║
 ║  │   │   Validation:                                                       │              │    ║
 ║  │   │     SHA-256(token) → lookup in ApiClientTokens table               │              │    ║
 ║  │   │     Found → create ClaimsPrincipal with AgentId claim              │              │    ║
 ║  │   │     Not found → 401 Unauthorized                                   │              │    ║
 ║  │   └─────────────────────────────────────────────────────────────────────┘              │    ║
 ║  │        │                                                                              │    ║
 ║  │        ├──────────────────────────────┐                                               │    ║
 ║  │        ▼                              ▼                                               │    ║
 ║  │   ┌──────────────┐           ┌───────────────────┐                                    │    ║
 ║  │   │  REST API    │           │  SignalR Hub       │                                    │    ║
 ║  │   │  Controller  │           │  Endpoint          │                                    │    ║
 ║  │   └──────────────┘           └───────────────────┘                                    │    ║
 ║  └───────────────────────────────────────────────────────────────────────────────────────┘    ║
 ║                                                                                             ║
 ║  ┌───────────────────────────────────────────────────────────────────────────────────────┐    ║
 ║  │                    REST API — DataController                                         │    ║
 ║  │                                                                                       │    ║
 ║  │   Agent Endpoints:                                                                    │    ║
 ║  │     POST /api/Data/RegisterAgent      [AllowAnonymous]  ◄── agent registration       │    ║
 ║  │     POST /api/Data/GetAgents          [Authorize]                                     │    ║
 ║  │     POST /api/Data/SaveAgents         [Admin]                                         │    ║
 ║  │     POST /api/Data/DeleteAgents       [Admin]                                         │    ║
 ║  │                                                                                       │    ║
 ║  │   Token Endpoints:                                                                    │    ║
 ║  │     POST /api/Data/GetApiClientTokens     [Admin]                                     │    ║
 ║  │     POST /api/Data/RevokeApiClientToken   [Admin]                                     │    ║
 ║  │                                                                                       │    ║
 ║  │   Key Endpoints:                                                                      │    ║
 ║  │     POST /api/Data/GenerateRegistrationKeys/{count}  [Admin]                          │    ║
 ║  │                                                                                       │    ║
 ║  │   Health:                                                                             │    ║
 ║  │     GET /health  [Anonymous]                                                          │    ║
 ║  └───────────────────────────────────────────────────────────────────────────────────────┘    ║
 ║                                                                                             ║
 ║  ┌───────────────────────────────────────────────────────────────────────────────────────┐    ║
 ║  │                    SignalR Hub — freeserviceshubHub                                   │    ║
 ║  │                    Endpoint: /freeserviceshubHub                                      │    ║
 ║  │                    Strongly-typed: Hub<IsrHub>                                        │    ║
 ║  │                                                                                       │    ║
 ║  │   Interface IsrHub:                                                                   │    ║
 ║  │     SignalRUpdate(SignalRUpdate update)  → push to dashboard clients                  │    ║
 ║  │                                                                                       │    ║
 ║  │   Server Methods (invoked by agents):                                                 │    ║
 ║  │     ┌─────────────────────────────────────────────────────────────────┐                │    ║
 ║  │     │  JoinGroup(string groupName)                                   │                │    ║
 ║  │     │    Adds connection to named group (e.g., "Agents")             │                │    ║
 ║  │     │                                                                 │                │    ║
 ║  │     │  JoinTenantId(Guid tenantId)                                   │                │    ║
 ║  │     │    Adds connection to tenant-specific group                    │                │    ║
 ║  │     │                                                                 │                │    ║
 ║  │     │  SendHeartbeat(AgentHeartbeat heartbeat)                        │                │    ║
 ║  │     │    1. Extract AgentId from ClaimsPrincipal                     │                │    ║
 ║  │     │    2. IDataAccess.SaveHeartbeat(heartbeat)                     │                │    ║
 ║  │     │    3. Broadcast → "AgentMonitor" group                        │                │    ║
 ║  │     └─────────────────────────────────────────────────────────────────┘                │    ║
 ║  │                                                                                       │    ║
 ║  │   Server → Agent Commands:                                                            │    ║
 ║  │     "Shutdown"  → agent calls StopApplication()                                      │    ║
 ║  │                                                                                       │    ║
 ║  │   Groups:                                                                             │    ║
 ║  │     "Agents"       — all connected agent instances                                    │    ║
 ║  │     "AgentMonitor" — dashboard UI clients watching agents                             │    ║
 ║  │     "<TenantId>"   — tenant-scoped groups                                             │    ║
 ║  └───────────────────────────────────────────────────────────────────────────────────────┘    ║
 ║                                                                                             ║
 ║  ┌───────────────────────────────────────────────────────────────────────────────────────┐    ║
 ║  │                    AgentMonitorService : BackgroundService                            │    ║
 ║  │                    Server-side poll-detect-broadcast                                  │    ║
 ║  │                                                                                       │    ║
 ║  │   Runs on the HUB (not the agent)                                                     │    ║
 ║  │                                                                                       │    ║
 ║  │   ┌──── poll loop (5s default) ────────────────────────────────────┐                   │    ║
 ║  │   │                                                                │                   │    ║
 ║  │   │  1. IDataAccess.GetAgents() → load all agents from DB         │                   │    ║
 ║  │   │  2. Check each agent.LastHeartbeat vs staleEdge               │                   │    ║
 ║  │   │     Online → missed heartbeat → Stale → missed more → Offline │                   │    ║
 ║  │   │  3. Compare with _cachedStatuses (ConcurrentDictionary)       │                   │    ║
 ║  │   │  4. If status changed:                                         │                   │    ║
 ║  │   │     → SignalRUpdate(AgentStatusChanged) to "AgentMonitor"     │                   │    ║
 ║  │   │  5. Always:                                                    │                   │    ║
 ║  │   │     → SignalRUpdate(AgentHeartbeat) to "AgentMonitor"         │                   │    ║
 ║  │   │                                                                │                   │    ║
 ║  │   │  Backoff: on error, multiplier++ (max 12 = 60s)               │                   │    ║
 ║  │   └────────────────────────────────────────────────────────────────┘                   │    ║
 ║  └───────────────────────────────────────────────────────────────────────────────────────┘    ║
 ║                                                                                             ║
 ║  ┌───────────────────────────────────────────────────────────────────────────────────────┐    ║
 ║  │                    Blazor UI (Server + WebAssembly)                                   │    ║
 ║  │                                                                                       │    ║
 ║  │   Radzen component library                                                            │    ║
 ║  │   Agent Dashboard page:                                                               │    ║
 ║  │     → Joins "AgentMonitor" SignalR group                                              │    ║
 ║  │     → Receives real-time SignalRUpdate pushes                                         │    ║
 ║  │     → Shows: agent list, status indicators, heartbeat data                            │    ║
 ║  │     → CPU %, Memory %, Disk % per agent                                               │    ║
 ║  │     → Online / Stale / Offline status badges                                          │    ║
 ║  └───────────────────────────────────────────────────────────────────────────────────────┘    ║
 ╚═══════════════════════════════════════════════════════════════════════════════════════════════╝
```

---

## 5. Data Layer

```
 ┌═══════════════════════════════════════════════════════════════════════════════════════┐
 ║                          DATA LAYER                                                 ║
 ╠═════════════════════════════════════════════════════════════════════════════════════════╣
 ║                                                                                     ║
 ║  ┌───────────────────────────────────────────────────────────────────────────┐        ║
 ║  │  IDataAccess (interface)                                                 │        ║
 ║  │    RegisterAgent(request, tenantId) → AgentRegistrationResponse         │        ║
 ║  │    SaveHeartbeat(heartbeat)         → persists to DB                    │        ║
 ║  │    GetAgents(ids, tenantId, user)   → List<Agent>                       │        ║
 ║  │    SaveAgents(items, user)          → List<Agent>                       │        ║
 ║  │    DeleteAgents(ids, user)          → BooleanResponse                   │        ║
 ║  │    GenerateRegistrationKeys(...)    → List<RegistrationKey>             │        ║
 ║  │    GetApiClientTokens(tenantId)     → List<ApiClientToken>              │        ║
 ║  └───────────────────────┬───────────────────────────────────────────────────┘        ║
 ║                          │                                                           ║
 ║                          ▼                                                           ║
 ║  ┌───────────────────────────────────────────────────────────────────────────┐        ║
 ║  │  EFDataModel : DbContext  (partial class pattern)                        │        ║
 ║  │                                                                           │        ║
 ║  │  Tables:                                                                  │        ║
 ║  │    Agents              — registered agent records                        │        ║
 ║  │    AgentHeartbeats     — heartbeat history                               │        ║
 ║  │    RegistrationKeys    — one-time-use reg keys (SHA-256 hashed)          │        ║
 ║  │    ApiClientTokens     — bearer tokens (SHA-256 hashed)                  │        ║
 ║  │    Tenants             — multi-tenant support                            │        ║
 ║  │                                                                           │        ║
 ║  │  Providers:                                                               │        ║
 ║  │    ├── InMemory    (dev/test)                                             │        ║
 ║  │    ├── SQL Server  (production)                                           │        ║
 ║  │    ├── MySQL                                                              │        ║
 ║  │    ├── PostgreSQL                                                         │        ║
 ║  │    └── SQLite                                                             │        ║
 ║  └───────────────────────────────────────────────────────────────────────────┘        ║
 ║                                                                                     ║
 ║  ┌───────────────────────────────────────────────────────────────────────────┐        ║
 ║  │  DataObjects (shared DTOs)                                               │        ║
 ║  │                                                                           │        ║
 ║  │  AgentRegistrationRequest:                                                │        ║
 ║  │    RegistrationKey, Hostname, OperatingSystem, Architecture              │        ║
 ║  │    AgentVersion, DotNetVersion                                           │        ║
 ║  │                                                                           │        ║
 ║  │  AgentRegistrationResponse:                                               │        ║
 ║  │    ApiClientToken (raw, returned once only)                              │        ║
 ║  │                                                                           │        ║
 ║  │  AgentHeartbeat:                                                          │        ║
 ║  │    HeartbeatId, AgentId, Timestamp                                       │        ║
 ║  │    CpuPercent, MemoryPercent, MemoryUsedGB, MemoryTotalGB               │        ║
 ║  │    DiskMetricsJson, CustomDataJson, AgentName                            │        ║
 ║  │                                                                           │        ║
 ║  │  SignalRUpdate:                                                           │        ║
 ║  │    UpdateType (enum), Message, Object                                    │        ║
 ║  │                                                                           │        ║
 ║  │  AgentStatuses:                                                           │        ║
 ║  │    Online | Stale | Offline                                              │        ║
 ║  └───────────────────────────────────────────────────────────────────────────┘        ║
 ╚═════════════════════════════════════════════════════════════════════════════════════════╝
```

---

## 6. Complete Data Flow — Agent ↔ Hub Communication

```
                         AGENT                                    HUB
                    (Windows Service)                     (Blazor + SignalR)

      ┌────────────────────┐
      │  Agent starts up   │
      │  No ApiClientToken │
      └────────┬───────────┘
               │
      STEP 1:  │  POST /api/Data/RegisterAgent
      REGISTER │  { RegistrationKey, Hostname, OS, Arch, Version }
               │ ─────────────────────────────────────────────────────▶ ┌──────────────────┐
               │                                                       │  Validate RegKey  │
               │                                                       │  Create Agent     │
               │                                                       │  Generate Token   │
               │ ◀───────────────────────────────────────────────────── │  Hash & Store     │
               │  { ApiClientToken: "raw-token-abc123" }               └──────────────────┘
               │
      ┌────────┴───────────┐
      │ PersistToken()     │
      │ saves to           │
      │ appsettings.json   │
      └────────┬───────────┘
               │
      STEP 2:  │  WebSocket: /freeserviceshubHub?access_token=raw-token-abc123
      SIGNALR  │ ─────────────────────────────────────────────────────▶ ┌──────────────────┐
               │                                                       │  ApiKeyMiddleware │
               │                                                       │  SHA256(token)    │
               │                                                       │  → lookup DB      │
               │                                                       │  → ClaimsPrincipal│
               │  InvokeAsync("JoinGroup", "Agents")                   │  → set Context    │
               │ ─────────────────────────────────────────────────────▶ └──────────────────┘
               │                                                       ┌──────────────────┐
               │ ◀───────────────────────────────────────────────────── │ Groups.Add(      │
               │  (connection added to "Agents" group)                 │  "Agents")        │
               │                                                       └──────────────────┘
               │
      STEP 3:  │  ┌───── every 30 seconds ─────┐
      HEARTBEAT│  │                              │
       LOOP    │  │  CollectSnapshot()           │
               │  │  CPU, RAM, Drives, Uptime    │
               │  │                              │
               │  │  InvokeAsync("SendHeartbeat",│
               │  │    { CpuPercent: 23.5,       │
               │  │      MemoryPercent: 67.1,    │
               │  │      DiskMetricsJson: [...], │
               │  │      AgentName: "DEV-01" })  │
               │  │                              │
               │  │ ────────────────────────────────────────────────▶ ┌──────────────────────┐
               │  │                                                  │  SendHeartbeat()      │
               │  │                                                  │  1. Claims → AgentId  │
               │  │                                                  │  2. da.SaveHeartbeat()│
               │  │                                                  │  3. Broadcast to      │
               │  │                                                  │     "AgentMonitor"    │
               │  │ ◀──────────────────────────────────────────────── │  → SignalRUpdate      │
               │  │                                                  └──────────────────────┘
               │  │                              │                            │
               │  └──────────────────────────────┘                            │
               │                                                              ▼
               │                                                   ┌────────────────────────┐
               │                                                   │  AgentMonitorService   │
               │                                                   │  (every 5s)            │
               │                                                   │                        │
               │                                                   │  Check last heartbeat  │
               │                                                   │  vs stale threshold    │
               │                                                   │                        │
               │                                                   │  Status transitions:   │
               │                                                   │   Online → Stale       │
               │                                                   │   Stale  → Offline     │
               │                                                   │                        │
               │                                                   │  Broadcast changes →   │
               │                                                   │   "AgentMonitor" group │
               │                                                   └──────────┬─────────────┘
               │                                                              │
               │                                                              ▼
               │                                                   ┌────────────────────────┐
               │                                                   │  Blazor Dashboard      │
               │                                                   │  (browser)             │
               │                                                   │                        │
               │                                                   │  Receives push updates │
               │                                                   │  Agent cards:          │
               │                                                   │   ● DEV-01  Online     │
               │                                                   │   ● PROD-01 Online     │
               │                                                   │   ○ CMS-01  Stale      │
               │                                                   └────────────────────────┘
               │
      SHUTDOWN │  ◀──────── hub sends "Shutdown" ────────────────────
               │  _hubConnection.On("Shutdown", ...)
               │  → Program.Lifetime.StopApplication()
               │  → graceful service stop
```

---

## 7. Installer → Agent Lifecycle

```
  ┌─────────────────────────────────────────────────────────────────────────────┐
  │                  INSTALLER LIFECYCLE FLOW                                  │
  │                                                                             │
  │  Developer / DevOps                                                        │
  │       │                                                                    │
  │       ▼                                                                    │
  │  ┌──────────┐     ┌──────────────────────────────────────────────┐         │
  │  │  BUILD   │────▶│  dotnet publish → publish/win-x64/           │         │
  │  │  (1)     │     │    FreeServicesHub.Agent.exe                  │         │
  │  └──────────┘     │    appsettings.json                          │         │
  │       │           │    agent.log (runtime)                       │         │
  │       ▼           └──────────────────────────────────────────────┘         │
  │  ┌──────────┐                                                              │
  │  │ CONFIG   │─── CopyPublishToInstall() ──▶ C:\Services\FreeServicesHub\  │
  │  │  (2)     │─── WriteApiKeyToServiceConfig() (if key provided)           │
  │  │          │─── sc.exe create FreeServicesHubAgent                        │
  │  │          │       binPath= "...\FreeServicesHub.Agent.exe"              │
  │  │          │       start= auto                                            │
  │  │          │─── sc.exe failure → restart/5000 (3 retries)                │
  │  └──────────┘─── .configured marker                                        │
  │       │                                                                    │
  │       ▼                                                                    │
  │  ┌──────────┐                                                              │
  │  │  START   │─── sc.exe start FreeServicesHubAgent                        │
  │  │  (4)     │                                                              │
  │  └──────────┘                                                              │
  │       │                                                                    │
  │       ▼                                                                    │
  │  ┌──────────────────────────────────────────────────────┐                  │
  │  │  AGENT RUNNING AS WINDOWS SERVICE                    │                  │
  │  │                                                       │                  │
  │  │  With API key:    connected mode → SignalR + hub     │                  │
  │  │  Without API key: standalone mode → console + log    │                  │
  │  │                                                       │                  │
  │  │  Auto-start on boot ✓                                │                  │
  │  │  Auto-restart on crash (3x with 5s delay) ✓          │                  │
  │  └──────────────────────────────────────────────────────┘                  │
  │       │                                                                    │
  │  ┌──────────┐                                                              │
  │  │  STATUS  │─── sc.exe query → SERVICE_STATUS_PROCESS                    │
  │  │  (6)     │─── tail agent.log (last 10 lines)                           │
  │  └──────────┘                                                              │
  │       │                                                                    │
  │  ┌──────────┐                                                              │
  │  │  STOP    │─── sc.exe stop FreeServicesHubAgent                         │
  │  │  (5)     │                                                              │
  │  └──────────┘                                                              │
  │       │                                                                    │
  │  ┌──────────┐                                                              │
  │  │  REMOVE  │─── sc.exe stop + sc.exe delete                              │
  │  │  (3)     │─── clear API key from config                                 │
  │  │          │─── remove .configured marker                                 │
  │  └──────────┘                                                              │
  │       │                                                                    │
  │  ┌──────────┐                                                              │
  │  │  DESTROY │─── stop + delete service                                     │
  │  │  (7)     │─── rm install directory                                      │
  │  │          │─── rm publish directory                                       │
  │  │  NUCLEAR │─── rm .configured marker                                     │
  │  └──────────┘                                                              │
  └─────────────────────────────────────────────────────────────────────────────┘
```

---

## 8. Security Model

```
  ┌─────────────────────────────────────────────────────────────────────────────┐
  │                       SECURITY / AUTH FLOW                                 │
  │                                                                             │
  │   ┌──────────────┐          ┌─────────────────────┐                        │
  │   │ Registration │          │  ApiClientTokens DB  │                        │
  │   │ Key (one-use)│          │                      │                        │
  │   │              │          │  TokenHash (SHA-256) │                        │
  │   │  Admin       │          │  AgentId             │                        │
  │   │  generates   │          │  TenantId            │                        │
  │   │  via UI      │          │  IsRevoked           │                        │
  │   └──────┬───────┘          └──────────┬──────────┘                        │
  │          │                             │                                    │
  │          ▼                             │                                    │
  │   Agent sends key                      │                                    │
  │   in RegisterAgent ──────▶ Hub:        │                                    │
  │                          │ Validate key │                                    │
  │                          │ Create Agent │                                    │
  │                          │ Generate raw │                                    │
  │                          │   token      │                                    │
  │                          │ SHA-256 hash │──────▶ Store hash in DB           │
  │                          │ Return raw   │                                    │
  │                          │   token      │                                    │
  │                          └──────────────┘                                    │
  │                                │                                            │
  │                                ▼                                            │
  │   Agent stores raw token in appsettings.json                               │
  │                                │                                            │
  │   All subsequent requests:     │                                            │
  │     Authorization: Bearer <raw-token>                                      │
  │                                │                                            │
  │   ApiKeyMiddleware:            ▼                                            │
  │     SHA-256(raw-token) → lookup hash in DB → match? → ClaimsPrincipal     │
  │                                                                             │
  │   NOTE: Raw token is NEVER stored server-side.                             │
  │         Only the SHA-256 hash is persisted.                                │
  │         Token is returned exactly ONCE at registration.                    │
  └─────────────────────────────────────────────────────────────────────────────┘
```

---

## 9. Production Deployment — Three Agents, One Hub

```
  ┌─────────────────────────────────────────────────────────────────────────┐
  │                    PRODUCTION TOPOLOGY                                 │
  │                                                                         │
  │                    ┌──────────────────────┐                             │
  │                    │     HUB SERVER       │                             │
  │                    │  ┌────────────────┐  │                             │
  │                    │  │ Blazor UI      │  │                             │
  │                    │  │ SignalR Hub    │  │                             │
  │                    │  │ REST API      │  │                             │
  │                    │  │ Monitor Svc   │  │                             │
  │                    │  │ EF Core → DB  │  │                             │
  │                    │  └────────────────┘  │                             │
  │                    └──────────┬───────────┘                             │
  │                               │                                         │
  │              ┌────────────────┼────────────────┐                        │
  │              │                │                │                        │
  │              ▼                ▼                ▼                        │
  │   ┌──────────────┐ ┌──────────────┐ ┌──────────────┐                   │
  │   │  DEV Agent   │ │  PROD Agent  │ │  CMS Agent   │                   │
  │   │  (DEV-01)    │ │  (PROD-01)   │ │  (CMS-01)    │                   │
  │   │              │ │              │ │              │                   │
  │   │  Windows Svc │ │  Windows Svc │ │  Windows Svc │                   │
  │   │  SignalR ◀──▶│ │  SignalR ◀──▶│ │  SignalR ◀──▶│                   │
  │   │  30s beats   │ │  30s beats   │ │  30s beats   │                   │
  │   │  agent.log   │ │  agent.log   │ │  agent.log   │                   │
  │   └──────────────┘ └──────────────┘ └──────────────┘                   │
  │                                                                         │
  │   Each agent:                                                           │
  │     • Runs as auto-start Windows Service                               │
  │     • Has its own ApiClientToken                                        │
  │     • Reports CPU, RAM, Disk every 30 seconds                          │
  │     • Reconnects with exponential backoff (2s→30s)                     │
  │     • Buffers heartbeats when disconnected (max 100)                   │
  │     • Falls back to HTTP POST when SignalR is down                     │
  │     • Accepts remote Shutdown command from hub                         │
  └─────────────────────────────────────────────────────────────────────────┘
```

---

## 10. CI/CD Pipeline — Azure DevOps

```
  ┌═══════════════════════════════════════════════════════════════════════════════════════┐
  ║            AZURE DEVOPS PIPELINE  (freeserviceshub-ci.yml)                           ║
  ╠═════════════════════════════════════════════════════════════════════════════════════════╣
  ║                                                                                     ║
  ║  STAGE 1: BUILD  (3 parallel jobs)                                                  ║
  ║  ═══════════════════════════════════════════════════════════                         ║
  ║                                                                                     ║
  ║  ┌────────────────┐  ┌──────────────────────┐  ┌────────────────┐                   ║
  ║  │  Job: Hub      │  │  Job: Agent+Installer │  │  Job: TestMe  │                   ║
  ║  │                │  │                        │  │               │                   ║
  ║  │  dotnet publish│  │  dotnet publish Agent  │  │  dotnet       │                   ║
  ║  │  → hub.zip     │  │  dotnet publish        │  │   publish     │                   ║
  ║  │  → artifact    │  │    Installer            │  │  → testme.zip│                   ║
  ║  │                │  │  → agent.zip            │  │  → artifact  │                   ║
  ║  └────────────────┘  │  → artifact             │  └────────────────┘                   ║
  ║                      └──────────────────────────┘                                   ║
  ║                                                                                     ║
  ║  STAGE 2: TEST                                                                      ║
  ║  ═══════════════════════════════════════════════════════════                         ║
  ║                                                                                     ║
  ║  ┌─────────────────────┐  ┌─────────────────────┐                                   ║
  ║  │  Pass A: Source     │  │  Pass B: Artifact    │                                   ║
  ║  │                     │  │                      │                                   ║
  ║  │  dotnet run TestMe  │  │  TestMe.exe          │                                   ║
  ║  │    --test 1         │  │    --test 1           │                                   ║
  ║  │    --test 3         │  │    --test 3           │                                   ║
  ║  │    --test 4         │  │    --test 4           │                                   ║
  ║  │                     │  │                      │                                   ║
  ║  │  Tests:             │  │  (same tests against │                                   ║
  ║  │  1: Agent console   │  │   published exes)    │                                   ║
  ║  │  3: Installer CLI   │  │                      │                                   ║
  ║  │  4: Agent standalone│  │                      │                                   ║
  ║  └─────────────────────┘  └─────────────────────┘                                   ║
  ║                                                                                     ║
  ║  STAGE 3: DEPLOY                                                                    ║
  ║  ═══════════════════════════════════════════════════════════                         ║
  ║                                                                                     ║
  ║  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐     ║
  ║  │  Deploy Hub    │  │  Deploy Agent  │  │  Deploy Agent  │  │  Deploy Agent  │     ║
  ║  │  (placeholder) │  │  DEV-01        │  │  CMS-01        │  │  PROD-01       │     ║
  ║  │                │  │                │  │                │  │                │     ║
  ║  │                │  │  template:     │  │  template:     │  │  template:     │     ║
  ║  │                │  │  deploy-       │  │  deploy-       │  │  deploy-       │     ║
  ║  │                │  │  agent.yml     │  │  agent.yml     │  │  agent.yml     │     ║
  ║  │                │  │                │  │                │  │                │     ║
  ║  │                │  │  sc stop       │  │  sc stop       │  │  sc stop       │     ║
  ║  │                │  │  xcopy files   │  │  xcopy files   │  │  xcopy files   │     ║
  ║  │                │  │  sc start      │  │  sc start      │  │  sc start      │     ║
  ║  └────────────────┘  └────────────────┘  └────────────────┘  └────────────────┘     ║
  ║                                                                                     ║
  ║  STAGE 4: SMOKE TEST                                                                ║
  ║  ═══════════════════════════════════════════════════════════                         ║
  ║                                                                                     ║
  ║  ┌──────────────────────────────────────────────────────────┐                        ║
  ║  │  Wait 60 seconds                                         │                        ║
  ║  │  Query each agent service status                        │                        ║
  ║  │  Verify agents responding                               │                        ║
  ║  └──────────────────────────────────────────────────────────┘                        ║
  ╚═════════════════════════════════════════════════════════════════════════════════════════╝
```

---

## 11. Project Map — Solution Structure

```
  FreeServicesHub.sln
  │
  ├── FreeServicesHub/                     ◄── HUB WEB APP (Blazor Server + WASM)
  │   └── FreeServicesHub/
  │       ├── Program.App.cs               Program entry + DI + middleware
  │       ├── FreeServicesHub.App.*.cs      Partial class files:
  │       │   ├── ApiKeyMiddleware         Bearer token validation
  │       │   ├── AgentMonitorService      Poll-detect-broadcast
  │       │   └── DevRegistrationKeySeeder Dev-time key seeder
  │       ├── Hubs/
  │       │   └── signalrHub.cs            SignalR Hub<IsrHub>
  │       ├── Controllers/
  │       │   └── FreeServicesHub.App.API  REST endpoints
  │       └── Pages/ + Components/         Blazor UI (Radzen)
  │
  ├── FreeServicesHub.Client/              ◄── BLAZOR WASM CLIENT
  │
  ├── FreeServicesHub.DataObjects/         ◄── SHARED DTOs
  │   └── Agent, AgentHeartbeat, ApiClientToken, RegistrationKey, SignalRUpdate
  │
  ├── FreeServicesHub.DataAccess/          ◄── BUSINESS LOGIC (IDataAccess)
  │   ├── Registration.cs                 RegisterAgent flow
  │   └── Heartbeats.cs                   SaveHeartbeat flow
  │
  ├── FreeServicesHub.EFModels/            ◄── EF CORE DbContext
  │   └── EFDataModel.cs                  Multi-provider (5 DBs)
  │
  ├── FreeServicesHub.Agent/               ◄── AGENT (Worker Service)
  │   ├── Program.cs                       Host builder
  │   ├── AgentWorkerService.cs            BackgroundService
  │   ├── FileLoggerProvider.cs            Rolling file logger
  │   └── appsettings.json                Config (Agent section)
  │
  ├── FreeServicesHub.Agent.Installer/     ◄── INSTALLER (CLI/UI)
  │   ├── Program.cs                       All actions + menu
  │   ├── InstallerConfig.cs               Typed config model
  │   └── appsettings.json                Config (Service/Publish/Security)
  │
  ├── FreeServicesHub.AppHost/             ◄── ASPIRE ORCHESTRATOR
  │   └── Program.cs                       Hub + Agent orchestration
  │
  ├── FreeServicesHub.TestMe/              ◄── TEST HARNESS (CLI)
  │   └── Program.cs                       Tests 1/3/4 (source + artifact)
  │
  ├── FreeServicesHub.Tests.Integration/   ◄── INTEGRATION TESTS (xunit v3)
  │   ├── HubFixture.cs                   WebApplicationFactory
  │   ├── RegistrationTests.cs            3 tests
  │   ├── HeartbeatTests.cs               2 tests
  │   └── SignalRTests.cs                 1 test
  │
  ├── .azure-pipelines/workflows/          ◄── CI/CD
  │   ├── freeserviceshub-ci.yml           4-stage pipeline
  │   └── templates/deploy-agent.yml       Reusable deploy template
  │
  ├── Pipelines/                           ◄── PRODUCTION DEPLOY
  │   ├── deploy-freeserviceshub.yml       6-stage full deploy
  │   └── variables.yml                    Variable groups
  │
  └── Docs/                               ◄── DOCUMENTATION
      ├── 301-Overview.md
      ├── 302-Agent-Deep-Dive.md
      ├── 303-Installer-Deep-Dive.md
      ├── 304-Hub-Deep-Dive.md
      ├── 305-Data-Layer.md
      ├── 306-SignalR-Integration.md
      ├── 307-Feasibility-Gaps.md
      ├── 308-Implementation-Plan.md
      ├── 309-Aspire-Setup.md
      ├── 310-CI-CD-Pipeline.md
      └── 311-Architecture-ASCII-Art.md    ◄── THIS FILE
```

---

*End of architecture report — Doc 311*
