# FreeGLBA

**GLBA Compliance Data Access Tracking System**

A free, open-source solution for tracking and auditing access to protected financial information as required by the Gramm-Leach-Bliley Act (GLBA).

## About

FreeGLBA helps educational institutions and financial organizations maintain compliance with GLBA requirements by providing:

- **Centralized Access Logging** - Track who accessed what data, when, and why
- **Real-time Dashboard** - Monitor access patterns and statistics
- **Compliance Reporting** - Generate audit-ready reports
- **API Integration** - Easy integration with existing systems via REST API
- **Bulk Access Tracking** - Track access to multiple subjects in a single event

## Technology Stack

- **.NET 10** - Latest .NET runtime
- **Blazor Server** - Interactive web UI with server-side rendering
- **Entity Framework Core** - Database access (SQL Server, PostgreSQL, SQLite)
- **SignalR** - Real-time notifications

## Quick Start

### Prerequisites
- .NET 10 SDK
- SQL Server, PostgreSQL, or SQLite

### Running Locally

```bash
git clone https://github.com/WSU-EIT/FreeGLBA.git
cd FreeGLBA/FreeGLBA
dotnet run
```

Navigate to `https://localhost:5001`

### Client Library

For integrating your applications with FreeGLBA:

```bash
dotnet add package FreeGLBA.Client
```

```csharp
var client = new GlbaClient("https://your-server.com", "your-api-key");
await client.LogAccessAsync(new GlbaEventRequest
{
    UserId = "jsmith",
    SubjectId = "S12345678",
    AccessType = "View",
    Purpose = "Enrollment verification"
});
```

## Project Structure

| Project | Description |
|---------|-------------|
| [`FreeGLBA`](FreeGLBA/README.md) | Main server application (ASP.NET Core, Blazor Server) |
| [`FreeGLBA.Client`](FreeGLBA.Client/README.md) | Blazor WebAssembly UI components |
| [`FreeGLBA.DataAccess`](FreeGLBA.DataAccess/README.md) | Business logic and data access layer |
| [`FreeGLBA.DataObjects`](FreeGLBA.DataObjects/README.md) | DTOs, configuration, and API endpoints |
| [`FreeGLBA.EFModels`](FreeGLBA.EFModels/README.md) | Entity Framework Core database models |
| [`FreeGLBA.NugetClient`](FreeGLBA.NugetClient/README.md) | Client library for API integration (NuGet package) |
| [`FreeGLBA.NugetClientPublisher`](FreeGLBA.NugetClientPublisher/README.md) | NuGet package publishing tool |
| [`FreeGLBA.Plugins`](FreeGLBA.Plugins/README.md) | Dynamic C# plugin system |
| [`FreeGLBA.TestClient`](FreeGLBA.TestClient/README.md) | Test client (project reference) |
| [`FreeGLBA.TestClientWithNugetPackage`](FreeGLBA.TestClientWithNugetPackage/README.md) | Test client (NuGet package) |
| [`Docs`](Docs/README.md) | Documentation and guides |

## Documentation

- [Server Setup](Docs/README.md)
- [Client Library](FreeGLBA.NugetClient/README.md)
- [API Reference](FreeGLBA.DataObjects/README.md)
- [Database Models](FreeGLBA.EFModels/README.md)
- [Plugin System](FreeGLBA.Plugins/README.md)
- [Data Access Layer](FreeGLBA.DataAccess/README.md)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

MIT License - see [LICENSE](LICENSE) for details.

---

## About the Team

**FreeGLBA** is developed and maintained by the **Enrollment Information Technology** team at **Washington State University**.

We build software solutions to support enrollment management, student services, and compliance needs across the university.

🔗 **Meet Our Team**: [https://em.wsu.edu/eit/meet-our-staff/](https://em.wsu.edu/eit/meet-our-staff/)

📧 **Contact**: [GitHub Issues](https://github.com/WSU-EIT/FreeGLBA/issues)

---

*Go Cougs! 🐾*