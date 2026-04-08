# FreeGLBA.TestClient

Test client application for validating the FreeGLBA.NugetClient library using project references.

Developed by **Enrollment Information Technology** at **Washington State University**.

## Purpose

This project is a console application used for:
- **Integration Testing** - Test client library against local server
- **Development** - Debug client library with project references
- **Examples** - Demonstrate client library usage patterns

This project references the NugetClient project directly (not the NuGet package), making it ideal for development and debugging scenarios.

## Technology Stack

- **.NET 10** - Console Application
- **Microsoft.Extensions.Configuration** - Configuration management
- **User Secrets** - Secure credential storage

## Dependencies

| Package | Purpose |
|---------|---------|
| Microsoft.Extensions.Configuration | Configuration system |
| Microsoft.Extensions.Configuration.Json | JSON config files |
| Microsoft.Extensions.Configuration.UserSecrets | Secure secrets |

### Project References
- **FreeGLBA.NugetClient** - Client library (project reference)

## Configuration

Create user secrets for the API key:

```bash
cd FreeGLBA.TestClient
dotnet user-secrets set \"GlbaApiKey\" \"your-api-key-here\"
```

Or use appsettings.json:

```json
{
  \"GlbaEndpoint\": \"https://localhost:5001\",
  \"GlbaApiKey\": \"your-api-key\"
}
```

## Running

```bash
cd FreeGLBA.TestClient
dotnet run
```

## vs FreeGLBA.TestClientWithNugetPackage

| Feature | TestClient | TestClientWithNugetPackage |
|---------|------------|----------------------------|
| Reference | Project reference | NuGet package |
| Use Case | Development/debugging | Package validation |
| Debugging | Full source debugging | Package symbols only |

## About

Part of the [FreeGLBA](../README.md) project.
