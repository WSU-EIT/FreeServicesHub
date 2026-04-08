# FreeCICD (Server)

## Overview

The `FreeCICD` project is the **ASP.NET Core server** that hosts the Blazor WebAssembly application. It provides REST API endpoints, SignalR hub for real-time communication, authentication, and serves as the backend for all client operations.

---

## Project Structure

```
FreeCICD/
+-- Classes/
|   +-- ConfigurationHelper.cs         # Configuration DI interface
|   +-- ConfigurationHelper.App.cs     # App-specific config extensions
|   +-- CustomAuthenticationHandler.cs # JWT/Cookie auth handler
|   +-- CustomAuthIdentity.cs          # Auth identity helper
|   +-- RouteHelper.cs                 # Route configuration
+-- Components/
|   +-- (Blazor server components)
+-- Controllers/
|   +-- DataController.cs              # Base controller with DI
|   +-- DataController.Ajax.cs         # AJAX endpoints
|   +-- DataController.App.cs          # App-specific endpoints
|   +-- DataController.ApplicationSettings.cs
|   +-- DataController.Authenticate.cs # Auth endpoints
|   +-- DataController.Departments.cs  # Department CRUD
|   +-- DataController.Encryption.cs   # Encryption endpoints
|   +-- DataController.FileStorage.cs  # File upload/download
|   +-- DataController.Language.cs     # Internationalization
|   +-- DataController.Plugins.cs      # Plugin execution
|   +-- DataController.Tags.cs         # Tag management
|   +-- DataController.Tenants.cs      # Tenant management
|   +-- DataController.UDF.cs          # User-defined fields
|   +-- DataController.UserGroups.cs   # User group CRUD
|   +-- DataController.Users.cs        # User CRUD
|   +-- DataController.Utilities.cs    # Utility endpoints
|   +-- AuthorizationController.cs     # OAuth callbacks
|   +-- SetupController.cs             # Initial setup
+-- Hubs/
|   +-- signalrHub.cs                  # SignalR real-time hub
+-- Plugins/
|   +-- Example1.cs                    # Sample plugin
|   +-- Example2.cs                    # Sample plugin
|   +-- Example3.cs                    # Sample plugin
|   +-- LoginWithPrompts.cs            # Auth plugin example
|   +-- UserUpdate.cs                  # User update plugin
+-- PluginsInterfaces.cs               # Plugin DI interfaces
+-- Program.cs                         # Application entry point
+-- Program.App.cs                     # App-specific startup
+-- appsettings.json                   # Configuration
+-- FreeCICD.csproj                    # Project file
```

---

## Architecture Diagram

```
+-----------------------------------------------------------------------------+
|                         ASP.NET CORE SERVER                                 |
+-----------------------------------------------------------------------------+

                              Internet
                                 |
                                 |
+-----------------------------------------------------------------------------+
|                           Kestrel Server                                    |
+-----------------------------------------------------------------------------+
|                                                                             |
|   +---------------------------------------------------------------------+   |
|   |                     Request Pipeline                                |   |
|   |  +---------+  +---------+  +---------+  +---------+  +---------+   |   |
|   |  | Static  |  |  Auth   |  | Routing |  | Authz   |  | CORS    |   |   |
|   |  | Files   |  |         |  |         |  |         |  |         |   |   |
|   |  +---------+  +---------+  +---------+  +---------+  +---------+   |   |
|   |       |            |            |            |            |        |   |
|   |       +----------------------------------------------------+        |   |
|   |                                 |                                   |   |
|   +---------------------------------------------------------------------+   |
|                                     |                                       |
|           +-----------------------------+                                   |
|           |                         |                         |             |
|           |                         |                         |             |
|   +---------------+         +---------------+         +---------------+     |
|   |  Controllers  |         |  SignalR Hub  |         |    Blazor     |     |
|   |  (REST API)   |         | (Real-time)   |         |  Components   |     |
|   |               |         |               |         |               |     |
|   | DataController|         | freecicdHub   |         |     App       |     |
|   | AuthController|         |               |         |               |     |
|   | SetupController         |               |         |               |     |
|   +---------------+         +---------------+         +---------------+     |
|           |                         |                         |             |
|           +-----------------------------+                                   |
|                                     |                                       |
|                                     |                                       |
|   +---------------------------------------------------------------------+   |
|   |                    Dependency Injection Container                    |   |
|   |  +-----------+  +-----------+  +-----------+  +-----------+        |   |
|   |  | IDataAccess |  | IPlugins    |  | IConfig     |  | ICustomAuth | |   |
|   |  |             |  |             |  | Helper      |  |             | |   |
|   |  +-----------+  +-----------+  +-----------+  +-----------+        |   |
|   +---------------------------------------------------------------------+   |
|                                                                             |
+-----------------------------------------------------------------------------+
```

---

## Authentication Flow

```
+-----------------------------------------------------------------------------+
|                         AUTHENTICATION FLOW                                 |
+-----------------------------------------------------------------------------+

    Client                    Controller                    DataAccess
      |                           |                             |
      |  POST /api/Data/Authenticate                            |
      |  { username, password }   |                             |
      | ------------------------> |                             |
      |                           |                             |
      |                           |  da.Authenticate()          |
      |                           | --------------------------> |
      |                           |                             |
      |                           | <-------------------------- |
      |                           |  User + ActionResponse      |
      |                           |                             |
      |                    +-------------+                      |
      |                    |  Success?   |                      |
      |                    +-------------+                      |
      |                           |                             |
      |               YES         |         NO                  |
      |                |          |          |                  |
      |                |          |          |                  |
      |         Generate JWT      |    Return error             |
      |         Set Cookie        |                             |
      |         Set Claims        |                             |
      |                |          |                             |
      | <----------------         |                             |
      |   User + AuthToken        |                             |


OAuth Flow (Google, Microsoft, etc.):

    Browser                  AuthController               External Provider
      |                           |                             |
      |  GET /signin-{provider}   |                             |
      | ------------------------> |                             |
      |                           |                             |
      | <------------------------ |  Redirect to OAuth          |
      |   302 Redirect            |                             |
      |                           |                             |
      | --------------------------------------------------------> |
      |   User authenticates                                    |
      |                                                         |
      | <-------------------------------------------------------- |
      |   302 Redirect + Code                                   |
      |                           |                             |
      |  GET /signin-{provider}   |                             |
      |  ?code=xxx                |                             |
      | ------------------------> |                             |
      |                           |   Exchange code for token   |
      |                           | --------------------------> |
      |                           | <-------------------------- |
      |                           |                             |
      |                           |   Create/update user        |
      |                           |   Set auth cookie           |
      |                           |                             |
      | <------------------------ |  Redirect to app            |
```

---

## SignalR Real-Time Hub

```
+-----------------------------------------------------------------------------+
|                         SIGNALR ARCHITECTURE                                |
+-----------------------------------------------------------------------------+

    Clients (Browsers)                            Server
         |                                           |
         |  WebSocket Connect                        |
         |  /freecicdHub                             |
         | ----------------------------------------> |
         |                                           |
         |  JoinTenantId(tenantId)                   |
         | ----------------------------------------> |
         |                                           |
         |                          +--------------------------------+
         |                          |  Groups (by TenantId)          |
         |                          |  +----------+  +----------+    |
         |                          |  | Tenant A |  | Tenant B |    |
         |                          |  | [conn1]  |  | [conn3]  |    |
         |                          |  | [conn2]  |  | [conn4]  |    |
         |                          |  +----------+  +----------+    |
         |                          +--------------------------------+
         |                                           |
         |                                           |
         |   Data Changes:                           |
         |   SignalRUpdate {                         |
         |     TenantId: A,                          |
         |     UpdateType: "User",                   |
         |     ItemId: userId                        |
         |   }                                       |
         | <---------------------------------------- |
         |   (Only Tenant A clients)                 |


Hub Methods:

+-----------------------------------------------------------------------+
|  freecicdHub                                                          |
+-----------------------------------------------------------------------+
|  • JoinTenantId(string tenantId)                                     |
|    -> Adds client to tenant group for targeted updates               |
|                                                                       |
|  • SignalRUpdate(SignalRUpdate update)                               |
|    -> Broadcasts update to tenant group or all clients               |
+-----------------------------------------------------------------------+
```

---

## API Endpoints

### Authentication Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/Data/Authenticate` | POST | Anonymous | Local username/password login |
| `/api/Data/ValidateToken` | GET | Required | Validate JWT token |
| `/signin-google` | GET | Anonymous | Google OAuth callback |
| `/signin-microsoft` | GET | Anonymous | Microsoft OAuth callback |
| `/signin-facebook` | GET | Anonymous | Facebook OAuth callback |
| `/signin-apple` | GET | Anonymous | Apple OAuth callback |
| `/signin-openid` | GET | Anonymous | OpenID Connect callback |
| `/api/Data/LogOut` | POST | Required | End session |

### User Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/Data/GetUser` | GET | Required | Get user by ID |
| `/api/Data/GetUsers` | GET | Required | List users with filtering |
| `/api/Data/SaveUser` | POST | Required | Create/update user |
| `/api/Data/DeleteUser` | POST | Admin | Delete user |
| `/api/Data/ResetPassword` | POST | Required | Change password |

### Tenant Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/Data/GetTenant` | GET | Required | Get tenant details |
| `/api/Data/GetTenants` | GET | AppAdmin | List all tenants |
| `/api/Data/SaveTenant` | POST | Admin | Create/update tenant |
| `/api/Data/GetTenantSettings` | GET | Required | Get tenant settings |
| `/api/Data/SaveTenantSettings` | POST | Admin | Update tenant settings |

### Department Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/Data/GetDepartments` | GET | Required | List departments |
| `/api/Data/SaveDepartment` | POST | Admin | Create/update department |
| `/api/Data/DeleteDepartment` | POST | Admin | Delete department |

### File Storage Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/Data/GetFile` | GET | Required | Download file |
| `/api/Data/SaveFile` | POST | Required | Upload file |
| `/api/Data/DeleteFile` | POST | Required | Delete file |
| `/api/Data/GetFiles` | GET | Required | List files |

### Plugin Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/Data/GetPlugins` | GET | Required | List available plugins |
| `/api/Data/ExecutePlugin` | POST | Required | Execute a plugin |

### Utility Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/Data/SignalRUpdate` | POST | Anonymous | Broadcast SignalR update |
| `/api/Data/GetVersion` | GET | Anonymous | Get app version info |
| `/api/Data/GetLanguage` | GET | Anonymous | Get language phrases |

---

## Security Policies

```
+-----------------------------------------------------------------------------+
|                         AUTHORIZATION POLICIES                              |
+-----------------------------------------------------------------------------+
|                                                                             |
|  Policy Name          |  Claim Required      |  Description                |
|  ----------------------------------------------------------------------     |
|  AppAdmin             |  Role: AppAdmin      |  Global application admin   |
|  Admin                |  Role: Admin         |  Tenant administrator       |
|  CanBeScheduled       |  Role: CanBeScheduled|  User can be in schedules   |
|  ManageAppointments   |  Role: Manage...     |  Can manage appointments    |
|  ManageFiles          |  Role: ManageFiles   |  Can manage file storage    |
|  PreventPasswordChange|  Role: Prevent...    |  Cannot change password     |
|                                                                             |
+-----------------------------------------------------------------------------+

Authentication Methods:

+-----------------------------------------------------------------------+
|  1. JWT Token (Header: Authorization: Bearer xxx)                    |
|  2. Cookie Authentication (ASP.NET Core Identity)                    |
|  3. OAuth Providers (Google, Microsoft, Facebook, Apple)             |
|  4. OpenID Connect                                                   |
|  5. Custom Plugin Authentication                                     |
+-----------------------------------------------------------------------+
```

---

## Dependency Injection

### Service Registration

```csharp
// Data Access
builder.Services.AddTransient<IDataAccess>(x => 
    ActivatorUtilities.CreateInstance<DataAccess>(x, 
        connectionString, databaseType, localModeUrl, 
        x.GetRequiredService<IServiceProvider>(), 
        cookiePrefix));

// Plugin System
builder.Services.AddTransient<Plugins.IPlugins>(x => plugins);

// Configuration
builder.Services.AddTransient<IConfigurationHelper>(x => 
    ActivatorUtilities.CreateInstance<ConfigurationHelper>(x, 
        configurationHelperLoader));

// Authentication Providers
builder.Services.AddTransient<ICustomAuthentication>(x => 
    ActivatorUtilities.CreateInstance<CustomAuthentication>(x, 
        useAuthorization));
```

### Controller Dependencies

```csharp
public DataController(
    IDataAccess daInjection,
    IHttpContextAccessor httpContextAccessor,
    ICustomAuthentication auth,
    IHubContext<freecicdHub, IsrHub> hubContext,
    IConfigurationHelper configHelper,
    Plugins.IPlugins diPlugins)
```

---

## Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "AppData": "Data Source=(local);Initial Catalog=FreeCICD;..."
  },
  "DatabaseType": "SQLServer",
  "AzureSignalRurl": "",
  "AuthenticationProviders": {
    "Google": { "ClientId": "", "ClientSecret": "" },
    "MicrosoftAccount": { "ClientId": "", "ClientSecret": "" },
    "Facebook": { "AppId": "", "AppSecret": "" },
    "Apple": { "ClientId": "", "KeyId": "", "TeamId": "" },
    "OpenId": {
      "ClientId": "",
      "ClientSecret": "",
      "Authority": "",
      "ButtonText": "Login with SSO"
    }
  },
  "BasePath": "",
  "AllowApplicationEmbedding": true,
  "GloballyDisabledModules": [],
  "GloballyEnabledModules": [],
  "PluginUsingStatements": [ "using FreeCICD;", "..." ]
}
```

---

## Extension Points

### Program.App.cs

```csharp
public partial class Program
{
    // Modify builder before service registration
    public static WebApplicationBuilder AppModifyBuilderStart(WebApplicationBuilder builder)
    
    // Modify builder after service registration
    public static WebApplicationBuilder AppModifyBuilderEnd(WebApplicationBuilder builder)
    
    // Modify app before middleware configuration
    public static WebApplication AppModifyStart(WebApplication app)
    
    // Modify app after middleware configuration
    public static WebApplication AppModifyEnd(WebApplication app)
    
    // Load app-specific configuration
    public static ConfigurationHelperLoader ConfigurationHelpersLoadApp(
        ConfigurationHelperLoader loader, WebApplicationBuilder builder)
    
    // App-specific authorization policies
    public static List<string> AuthenticationPoliciesApp
}
```

### DataController.App.cs

```csharp
public partial class DataController
{
    // Add app-specific API endpoints here
}
```

---

## Best Practices

1. **Use DI everywhere**: Never instantiate DataAccess manually
2. **Check CurrentUser**: Always verify user permissions
3. **Use SignalR for updates**: Broadcast changes for real-time UI
4. **Filter by TenantId**: Every query must be tenant-scoped
5. **Return ActionResponse**: Use consistent response patterns
6. **Log errors**: Use proper error handling and logging
7. **Validate input**: Check all request parameters
