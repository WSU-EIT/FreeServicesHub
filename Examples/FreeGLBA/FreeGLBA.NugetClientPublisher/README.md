# FreeGLBA.NugetClientPublisher

A command-line tool for managing NuGet package publishing for the FreeGLBA.Client package.

## 🎯 Purpose

This tool automates the process of building, packing, and publishing the `FreeGLBA.Client` NuGet package to NuGet.org. It provides:

- **Version validation** - Prevents publishing if your version isn't newer than what's on NuGet.org
- **Dry run mode** - Preview what would happen without making changes
- **Version management** - Lookup existing versions and trim/unlist old ones
- **Semantic versioning guidance** - Helps you choose the right version number

## 🚀 Quick Start

### 1. Configure API Key (One-time setup)

```bash
cd FreeGLBA.NugetClientPublisher
dotnet user-secrets init
dotnet user-secrets set "NuGet:ApiKey" "your-nuget-api-key-here"
```

> Get your API key from https://www.nuget.org/account/apikeys

### 2. Run the Tool

```bash
dotnet run
```

### 3. Use the Menu

```
═══════════════════════════════════════════════════════════════
              MENU - 🔒 DRY RUN MODE (No writes)              
═══════════════════════════════════════════════════════════════
  1. View current configuration - READ ONLY
  2. Verify project builds successfully - READ ONLY
  3. Pack NuGet package (build .nupkg)
  4. Push to NuGet.org
  5. Full publish (Clean → Build → Pack → Push)

  L. Lookup versions from NuGet.org - READ ONLY
  T. Trim/Unlist old versions from NuGet.org
  V. Change version number
  D. Toggle DRY RUN mode
  H. Help - Show documentation
  0. Exit
```

## 📋 Menu Options

### Read-Only Operations (Safe)

| Option | Description |
|--------|-------------|
| **1** | View current configuration and project paths |
| **2** | Verify the project builds successfully |
| **L** | Lookup all versions published on NuGet.org |
| **H** | Show help and documentation |

### Write Operations (Respects Dry Run Mode)

| Option | Description |
|--------|-------------|
| **3** | Clean, build, and pack the NuGet package locally |
| **4** | Push an existing .nupkg to NuGet.org |
| **5** | Full publish: Clean → Build → Pack → Push |
| **T** | Trim/unlist old versions from NuGet.org |

### Configuration

| Option | Description |
|--------|-------------|
| **V** | Change the version number for this session |
| **D** | Toggle between DRY RUN and LIVE mode |
| **0** | Exit the application |

## 🔒 Dry Run Mode

The tool starts in **DRY RUN MODE** by default. In this mode:
- All operations show what WOULD happen
- No packages are pushed to NuGet.org
- No versions are unlisted
- Safe to experiment and verify settings

Press **D** to toggle to **LIVE MODE** when ready to publish.

## 📦 Version Management

### Semantic Versioning (SemVer)

The tool enforces semantic versioning: `MAJOR.MINOR.PATCH`

| Version Part | When to Increment | Example |
|--------------|-------------------|---------|
| **MAJOR** (X.0.0) | Full breaking changes - existing code WILL break | 1.0.0 → 2.0.0 |
| **MINOR** (0.X.0) | Limited breaking changes - new features, some code MAY need updates | 1.0.0 → 1.1.0 |
| **PATCH** (0.0.X) | Non-breaking changes - bug fixes, existing code will NOT break | 1.0.0 → 1.0.1 |

### Version Validation

Before publishing, the tool automatically:
1. Fetches the latest version from NuGet.org
2. Compares it to your configured version
3. **Blocks** if your version is ≤ the latest
4. Suggests the next patch version
5. Offers to auto-update your version

### Trimming Old Versions

Use option **T** to unlist old versions. You specify how many versions to **keep** per Major.Minor group:

```
Example: Keep 3 versions per group

1.0.x: KEEP 1.0.5, 1.0.4, 1.0.3  |  UNLIST 1.0.2, 1.0.1, 1.0.0
1.1.x: KEEP 1.1.2, 1.1.1, 1.1.0  |  (nothing to unlist)
```

> **Note:** Unlisting hides packages from search but does NOT delete them. Existing projects can still restore unlisted versions.

## ⚙️ Configuration

### appsettings.json

```json
{
  "NuGet": {
    "ApiKey": "",                    // Use user-secrets instead!
    "Source": "https://api.nuget.org/v3/index.json",
    "PackageId": "FreeGLBA.Client",
    "Version": "1.0.3",
    "SolutionRoot": "",              // Auto-detected if empty
    "ProjectPath": "FreeGLBA.NugetClient\\FreeGLBA.NugetClient.csproj",
    "Configuration": "Release",
    "SkipDuplicate": true,
    "IncludeSymbols": true
  }
}
```

### User Secrets (Recommended for API Key)

```bash
# Initialize user secrets (one-time)
dotnet user-secrets init

# Set API key
dotnet user-secrets set "NuGet:ApiKey" "your-api-key-here"

# View secrets
dotnet user-secrets list

# Remove API key
dotnet user-secrets remove "NuGet:ApiKey"
```

## 🤖 CI/CD Integration

### Command Line Arguments

```bash
# Override version from command line
dotnet run -- --version 1.0.5

# For automated scripts, you can also set config values
dotnet run -- NuGet:Version=1.0.5
```

### Example GitHub Actions Workflow

```yaml
- name: Publish NuGet Package
  run: |
    cd FreeGLBA.NugetClientPublisher
    dotnet run -- --version ${{ github.event.inputs.version }}
  env:
    NuGet__ApiKey: ${{ secrets.NUGET_API_KEY }}
```

## 📁 Project Structure

```
FreeGLBA.NugetClientPublisher/
├── Program.cs           # Main application code
├── appsettings.json     # Configuration (non-sensitive)
├── README.md            # This file
└── FreeGLBA.NugetClientPublisher.csproj
```

## 🔗 Related Projects

| Project | Description |
|---------|-------------|
| **FreeGLBA.NugetClient** | The actual NuGet package being published |
| **FreeGLBA.Client** | Blazor WebAssembly client components |
| **FreeGLBA** | Main server application |

## 📝 Troubleshooting

### "API Key not configured"
```bash
dotnet user-secrets set "NuGet:ApiKey" "your-key-here"
```

### "Project file not found"
Update `SolutionRoot` in appsettings.json to your actual solution folder path.

### "Version must be greater than X.X.X"
Your configured version is ≤ what's already on NuGet.org. Use option **V** to change it, or accept the suggested version when prompted.

### "Package not found"
Run option **3** (Pack) before option **4** (Push) to create the .nupkg file.

## 📜 License

Part of the FreeGLBA project. See the main repository for license information.
