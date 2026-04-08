# FreeExamples (Server)

> ASP.NET Core host for the FreeExamples Blazor WebAssembly application — API controllers, authentication middleware, SignalR hub, plugin system, and background service.

**Target:** .NET 10 · **Type:** ASP.NET Core Web App (Blazor WASM hosted)

---

## What This Project Contains

| Area | Files | Purpose |
|------|-------|---------|
| **Controllers** | `DataController.*.cs` | Entity CRUD endpoints (Users, Departments, Tags, etc.) |
| **App Controllers** | `FreeExamples.App.API.*.cs` | Example-specific APIs (ApiKeyDemo, CodePlayground, GitBrowser, CommentThread, SampleData) |
| **Middleware** | `ApiKeyDemoMiddleware.cs` | SHA-256 hashed API key authentication |
| **Authentication** | `CustomAuthenticationHandler.cs` | Custom auth with LDAP, OAuth, local login support |
| **SignalR** | `signalrHub.cs` | Real-time communication hub |
| **Background** | `BackgroundProcessor.cs` | Configurable periodic task runner |
| **Services** | `GitBrowserService.cs`, `CodeSnippetService.cs`, `CommentService.cs` | App-specific business logic |
| **Plugins** | `PluginFiles/` | Example plugins (Example1-3, BackgroundProcess, LoginWithPrompts, UserUpdate) |
| **Startup** | `Program.cs`, `Program.App.cs` | Service registration and middleware pipeline |

---

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.AspNetCore.Components.WebAssembly.Server` | Blazor WASM hosting |
| `Microsoft.Azure.SignalR` | Azure SignalR Service support |
| `AspNet.Security.OAuth.Apple` | Apple OAuth provider |
| `Microsoft.AspNetCore.Authentication.*` | Google, Facebook, Microsoft, OpenID Connect |
| `Serilog.Extensions.Logging.File` | File-based logging |

---

*Part of the [FreeExamples](..) suite.*
