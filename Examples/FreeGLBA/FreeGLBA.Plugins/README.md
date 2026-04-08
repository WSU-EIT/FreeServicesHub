# FreeGLBA.Plugins

Dynamic C# plugin system for the FreeGLBA GLBA Compliance Data Access Tracking System. Enables runtime code execution and extensibility without recompilation.

Developed by **Enrollment Information Technology** at **Washington State University**.

## Purpose

This project provides a plugin architecture that allows:
- **Dynamic Code Execution** - Execute C# code at runtime
- **Extensibility** - Add custom functionality without modifying core code
- **Plugin Types** - Auth, Background Process, Reports, Custom Actions
- **Hot Reload** - Load plugins from files without restart

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.CodeAnalysis.CSharp` | 5.0.0 | Roslyn compiler for dynamic C# |
| `Basic.Reference.Assemblies.Net100` | 1.8.4 | .NET 10 reference assemblies for compilation |

## Project Structure

```
FreeGLBA.Plugins/
├── FreeGLBA.Plugins.csproj
├── README.md
├── Plugins.cs                       # Main plugin system
└── Encryption.cs                    # Encryption utilities for plugins
```

## How It Works

1. **Startup**: Plugin files are loaded from the `/Plugins` folder
2. **Parsing**: Each file's metadata is extracted (Name, Type, Version, etc.)
3. **Caching**: Compiled plugins are cached for performance
4. **Execution**: Plugins are invoked via the `Execute` method with parameters

## Plugin File Types

| Extension | Description |
|-----------|-------------|
| `.cs` | C# source files (compiled with solution) |
| `.plugin` | C# source files (loaded at runtime, avoids build conflicts) |
| `.assemblies` | Lists additional DLL references needed by a plugin |

## Creating a Plugin

### Basic Plugin Structure

Every plugin must have a `Properties()` method returning metadata:

```csharp
using System;
using System.Collections.Generic;

namespace MyPlugins
{
    public class MyCustomPlugin
    {
        public Dictionary<string, object> Properties() =>
            new Dictionary<string, object>
            {
                { "Id", new Guid("00000000-0000-0000-0000-000000000001") },
                { "Author", "Your Name" },
                { "ContainsSensitiveData", false },
                { "Description", "What this plugin does" },
                { "Name", "My Custom Plugin" },
                { "SortOrder", 0 },
                { "Type", "Example" },       // Auth, BackgroundProcess, Report, etc.
                { "Version", "1.0.0" }
            };

        public Dictionary<string, object> Execute(
            Dictionary<string, object>? objects,
            List<PluginPromptValue>? prompts)
        {
            var output = new Dictionary<string, object>();
            
            // Your plugin logic here
            output["Success"] = true;
            output["Message"] = "Plugin executed successfully!";
            
            return output;
        }
    }
}
```

### Plugin Types

| Type | Purpose | When Executed |
|------|---------|---------------|
| `Auth` | Custom authentication | Login flow |
| `BackgroundProcess` | Scheduled tasks | Background service timer |
| `Report` | Custom reports | Report generation |
| `UserUpdate` | User sync/update | User save/create |
| `Example` | Demo/testing | Manual trigger |

### Plugin with Prompts

Plugins can define UI prompts for user input:

```csharp
public Dictionary<string, object> Properties() =>
    new Dictionary<string, object>
    {
        // ... standard properties ...
        { "Prompts", new List<PluginPrompt>
            {
                new PluginPrompt
                {
                    Name = "Username",
                    Type = PluginPromptType.Text,
                    Required = true
                },
                new PluginPrompt
                {
                    Name = "Password",
                    Type = PluginPromptType.Password,
                    Required = true
                },
                new PluginPrompt
                {
                    Name = "RememberMe",
                    Type = PluginPromptType.Checkbox,
                    Required = false
                }
            }
        }
    };
```

### Using External Assemblies

Create a `.assemblies` file with the same name as your plugin:

**HelloWorld.plugin**
```csharp
namespace Hello
{
    public class World
    {
        public static string SayHello() => "Hello, World!";
    }
}
```

**HelloWorld.assemblies**
```
.\HelloWorld\HelloWorld.dll
typeof(SomeNameSpace.SomeProperty).Assembly.Location
```

## IPlugins Interface

```csharp
public interface IPlugins
{
    /// <summary>All loaded plugins</summary>
    List<Plugin> AllPlugins { get; }
    
    /// <summary>Plugins formatted for caching</summary>
    List<Plugin> AllPluginsForCache { get; }
    
    /// <summary>Path to the plugins folder</summary>
    string PluginFolder { get; }
    
    /// <summary>Server assembly references for compilation</summary>
    List<string> ServerReferences { get; set; }
    
    /// <summary>Using statements to add to plugins</summary>
    List<string> UsingStatements { get; set; }
    
    /// <summary>Load plugins from disk</summary>
    List<Plugin> Load(string path);
    
    /// <summary>Execute dynamic C# code</summary>
    T? ExecuteDynamicCSharpCode<T>(
        string code,
        IEnumerable<object>? objects,
        List<string>? additionalAssemblies,
        string Namespace,
        string Classname,
        string invokerFunction);
}
```

## Plugin Class

```csharp
public class Plugin
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public string Author { get; set; }
    public string Version { get; set; }
    public int SortOrder { get; set; }
    public bool ContainsSensitiveData { get; set; }
    public string Code { get; set; }
    public List<string> AdditionalAssemblies { get; set; }
    public List<PluginPrompt> Prompts { get; set; }
}
```

## Usage Examples

### Loading Plugins at Startup

```csharp
// Program.cs
var plugins = new Plugins();
plugins.ServerReferences = GetServerReferences();
plugins.UsingStatements = GetUsingStatements();
var loadedPlugins = plugins.Load("./Plugins");
builder.Services.AddSingleton<IPlugins>(plugins);
```

### Executing a Plugin

```csharp
public async Task<Dictionary<string, object>> RunPluginAsync(
    Guid pluginId, 
    Dictionary<string, object> parameters)
{
    var plugin = _plugins.AllPlugins.FirstOrDefault(p => p.Id == pluginId);
    if (plugin == null) throw new Exception("Plugin not found");
    
    var result = _plugins.ExecuteDynamicCSharpCode<Dictionary<string, object>>(
        plugin.Code,
        new object[] { parameters },
        plugin.AdditionalAssemblies,
        "Plugins",
        plugin.Name.Replace(" ", ""),
        "Execute"
    );
    
    return result ?? new Dictionary<string, object>();
}
```

### Getting Plugins by Type

```csharp
// Get all authentication plugins
var authPlugins = _plugins.AllPlugins
    .Where(p => p.Type == "Auth")
    .OrderBy(p => p.SortOrder)
    .ThenBy(p => p.Name)
    .ToList();
```

## File Listing

| File | Description |
|------|-------------|
| `Plugins.cs` | Main plugin system - IPlugins interface, Plugin class, dynamic code execution |
| `Encryption.cs` | AES encryption utilities for secure plugin data |

## Security Considerations

- Plugins execute with full trust - only load from trusted sources
- Use `ContainsSensitiveData = true` for plugins handling credentials
- Plugin code is compiled with Roslyn - syntax errors will throw exceptions
- Assemblies must be .NET 10 compatible

## Example Plugins (in FreeGLBA/Plugins/)

| File | Type | Purpose |
|------|------|---------|
| `Example1.cs` | Example | Basic plugin demonstration |
| `Example2.cs` | Example | Plugin with prompts |
| `Example3.cs` | Example | Advanced plugin features |
| `ExampleBackgroundProcess.cs` | BackgroundProcess | Scheduled task example |
| `LoginWithPrompts.cs` | Auth | Custom login with prompts |
| `UserUpdate.cs` | UserUpdate | User synchronization |

## Related Projects

- **FreeGLBA.DataAccess** - Uses plugins for extensibility

## About

FreeGLBA is developed and maintained by the **Enrollment Information Technology** team at **Washington State University**.

🔗 [Meet Our Staff](https://em.wsu.edu/eit/meet-our-staff/)
