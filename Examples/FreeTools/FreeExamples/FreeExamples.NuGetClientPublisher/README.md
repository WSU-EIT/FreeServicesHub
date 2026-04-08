# FreeExamples.NuGetClientPublisher

> Interactive console tool for packing and publishing the `FreeExamples.Client` NuGet package to NuGet.org — with dry-run safety and version management.

**Target:** .NET 10 · **Type:** Console Application

---

## What This Does

An interactive menu-driven tool (same pattern as the DatabaseMigration tool) that:

1. **Builds** the FreeExamples.NuGetClient project
2. **Packs** it into a `.nupkg` file
3. **Pushes** to NuGet.org (or a private feed)

Starts in **dry-run mode** by default — toggle to live mode when ready to publish.

---

## Usage

```bash
cd FreeExamples/FreeExamples.NuGetClientPublisher

# Store your NuGet API key in user secrets
dotnet user-secrets set "NuGet:ApiKey" "your-nuget-api-key"

# Run the interactive publisher
dotnet run
```

### Menu Options

| Key | Description |
|-----|-------------|
| **1** | View current configuration |
| **2** | Verify project builds |
| **3** | Pack NuGet package (build `.nupkg`) |
| **4** | Push to NuGet.org |
| **5** | Full publish (Clean → Build → Pack → Push) |
| **L** | Lookup existing versions on NuGet.org |
| **V** | Change version number |
| **D** | Toggle DRY RUN mode |
| **0** | Exit |

### CLI Version Override

```bash
dotnet run -- --version 1.2.3
```

---

## Pattern Source

Based on `FreeGLBA.NuGetClientPublisher` — same interactive publish workflow.

---

*Part of the [FreeExamples](..) suite.*
