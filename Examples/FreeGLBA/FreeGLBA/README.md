# FreeGLBA (Server)

Main server application for the FreeGLBA GLBA Compliance Data Access Tracking System.

Developed by **Enrollment Information Technology** at **Washington State University**.

## Purpose

This project is the main entry point and web host that provides:
- **Blazor Server Hosting** - Serves the interactive web UI
- **REST API Endpoints** - External API for client integrations
- **Authentication** - OAuth, OpenID Connect, and API key auth
- **SignalR Hub** - Real-time notifications
- **Background Services** - Scheduled tasks and cleanup
- **Plugin Execution** - Runtime plugin hosting

## Technology Stack

- **.NET 10** - ASP.NET Core Web Application
- **Blazor Server** - Interactive server-side UI
- **SignalR** - Real-time communication (Azure SignalR supported)
- **Scalar** - OpenAPI documentation UI
- **Serilog** - Structured logging

## Dependencies

| Package | Purpose |
|---------|---------|
| Microsoft.AspNetCore.Authentication.* | OAuth providers |
| Microsoft.Azure.SignalR | Azure SignalR Service |
| Scalar.AspNetCore | OpenAPI documentation UI |

### Project References
- **FreeGLBA.Client** - Blazor UI components
- **FreeGLBA.DataAccess** - Business logic and data layer
- **FreeGLBA.Plugins** - Plugin system

## Running the Server

```bash
cd FreeGLBA
dotnet run
```

Navigate to https://localhost:5001

## API Documentation

- **Scalar UI**: /scalar
- **OpenAPI JSON**: /openapi/v1.json

## About

Part of the [FreeGLBA](../README.md) project.
