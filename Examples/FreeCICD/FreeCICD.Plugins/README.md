# FreeCICD.Plugins

## Overview

The `FreeCICD.Plugins` project provides a **dynamic C# code execution engine** that allows plugins to be loaded at runtime. Plugins are `.cs` files stored in a designated folder that are compiled and executed on-demand using Roslyn.

---

## Project Structure

```
FreeCICD.Plugins/
+-- Plugins.cs                     # Plugin system and dynamic compiler
+-- Encryption.cs                  # AES encryption for sensitive plugin code
+-- FreeCICD.Plugins.csproj        # Project file
```

---

## Architecture Diagram

```
+-----------------------------------------------------------------------------+
|                         PLUGIN SYSTEM ARCHITECTURE                          |
+-----------------------------------------------------------------------------+

                              +-----------------+
                              |  Plugins Folder |
                              | (*.cs, *.plugin)|
                              +-----------------+
                                       |
                                       |
+-----------------------------------------------------------------------------+
|                          IPlugins Interface                                 |
+-----------------------------------------------------------------------------+
|                                                                             |
|   Load(path)                                                                |
|      |                                                                      |
|      +-> Scan for *.cs, *.plugin files                                     |
|      |                                                                      |
|      +-> For each file:                                                    |
|      |   +-> Read source code                                              |
|      |   +-> Load .assemblies file (if exists)                             |
|      |   +-> Extract namespace and class name                              |
|      |   +-> Execute Properties() method                                   |
|      |   +-> Register Plugin object                                        |
|      |                                                                      |
|      +-> Return List<Plugin>                                               |
|                                                                             |
|   ExecuteDynamicCSharpCode<T>(...)                                          |
|      |                                                                      |
|      +-> Decrypt code (if encrypted)                                       |
|      +-> Add missing using statements                                      |
|      +-> Load reference assemblies                                         |
|      |   +-> Basic.Reference.Assemblies.Net100                             |
|      |   +-> ServerReferences                                              |
|      |   +-> AdditionalAssemblies                                          |
|      |                                                                      |
|      +-> Compile with Roslyn CSharpCompilation                             |
|      |                                                                      |
|      +-> Load compiled assembly                                            |
|      |                                                                      |
|      +-> Invoke specified function                                         |
|                                                                             |
+-----------------------------------------------------------------------------+
```

---

## Plugin Execution Flow

```
+-----------------------------------------------------------------------------+
|                         PLUGIN EXECUTION FLOW                               |
+-----------------------------------------------------------------------------+

    Request                    Plugin System                    Result
      |                             |                              |
      |  ExecuteDynamicCSharpCode   |                              |
      | --------------------------> |                              |
      |                             |                              |
      |                    +-----------------+                     |
      |                    | Is code         |                     |
      |                    | encrypted?      |                     |
      |                    +-----------------+                     |
      |                             |                              |
      |                      YES    |    NO                        |
      |                       |     |     |                        |
      |                       |     |     |                        |
      |                   Decrypt   |     |                        |
      |                   code      |     |                        |
      |                       |     |     |                        |
      |                       +-----+-----+                        |
      |                             |                              |
      |                             |                              |
      |                    Add using statements                    |
      |                             |                              |
      |                             |                              |
      |                    Load reference assemblies               |
      |                    +--------------------+                  |
      |                    | • .NET 10 base     |                  |
      |                    | • Server refs      |                  |
      |                    | • Additional refs  |                  |
      |                    +--------------------+                  |
      |                             |                              |
      |                             |                              |
      |                    +--------------------+                  |
      |                    |  Roslyn Compiler   |                  |
      |                    |  CSharpCompilation |                  |
      |                    +--------------------+                  |
      |                             |                              |
      |                    +-----------------+                     |
      |                    | Compilation     |                     |
      |                    | successful?     |                     |
      |                    +-----------------+                     |
      |                             |                              |
      |                      YES    |    NO                        |
      |                       |     |     |                        |
      |                       |     |     |                        |
      |               Load assembly |  Log errors                  |
      |               Create instance   return null                |
      |               Invoke method |                              |
      |                       |     |                              |
      |                       |     |                              |
      | <--------------------- | <-- |                             |
      |   Return T result                                          |
      |                                                            |
```

---

## Plugin Types

```
+-----------------------------------------------------------------------------+
|                           PLUGIN TYPES                                      |
+-----------------------------------------------------------------------------+
|                                                                             |
|  +-----------+   +-----------+   +-----------+   +-----------+             |
|  |    Auth     |   | UserUpdate  |   |   Custom    |   |   Prompts   |     |
|  |             |   |             |   |             |   |             |     |
|  | Invoker:    |   | Invoker:    |   | Invoker:    |   | Invoker:    |     |
|  |  "Login"    |   | "UpdateUser"|   |  "Execute"  |   |  "Execute"  |     |
|  +-----------+   +-----------+   +-----------+   +-----------+             |
|        |                 |                 |                 |              |
|        |                 |                 |                 |              |
|  Custom auth       Update user        Generic code      UI prompts         |
|  providers         after login        execution         with results       |
|                                                                             |
+-----------------------------------------------------------------------------+
```

---

## Plugin Model

### Plugin Class

```csharp
public class Plugin
{
    public Guid Id { get; set; }
    public string Author { get; set; }
    public string ClassName { get; set; }
    public string Code { get; set; }
    public bool ContainsSensitiveData { get; set; }
    public string Description { get; set; }
    public List<Guid> LimitToTenants { get; set; }
    public string Name { get; set; }
    public string Namespace { get; set; }
    public string Invoker { get; set; }  // Default: "Execute"
    public List<PluginPrompt> Prompts { get; set; }
    public int SortOrder { get; set; }
    public string Type { get; set; }
    public string Version { get; set; }
    public List<string> AdditionalAssemblies { get; set; }
}
```

### Plugin Prompt Types

```csharp
public enum PluginPromptType
{
    Button,
    Checkbox,
    CheckboxList,
    Date,
    DateTime,
    File,
    Files,
    HTML,
    Multiselect,
    Number,
    Password,
    Radio,
    Select,
    Text,
    Textarea,
    Time,
}
```

---

## Creating a Plugin

### Basic Plugin Structure

```csharp
// MyPlugin.cs
namespace MyPlugins
{
    public class MyPlugin
    {
        public Dictionary<string, object> Properties()
        {
            return new Dictionary<string, object>
            {
                { "Id", new Guid("12345678-1234-1234-1234-123456789abc") },
                { "Name", "My Custom Plugin" },
                { "Description", "Does something useful" },
                { "Author", "Your Name" },
                { "Version", "1.0.0" },
                { "Type", "custom" },
                { "SortOrder", 10 },
                { "ContainsSensitiveData", false }
            };
        }
        
        public PluginExecuteResult Execute(object[] args)
        {
            var result = new PluginExecuteResult();
            
            // Your logic here
            result.Result = true;
            result.Messages.Add("Plugin executed successfully");
            
            return result;
        }
    }
}
```

### Authentication Plugin

```csharp
namespace AuthPlugins
{
    public class CustomAuthPlugin
    {
        public Dictionary<string, object> Properties()
        {
            return new Dictionary<string, object>
            {
                { "Id", new Guid("...") },
                { "Name", "Custom SSO" },
                { "Type", "auth" },  // Sets Invoker to "Login"
                { "ContainsSensitiveData", true }
            };
        }
        
        public DataObjects.User Login(
            DataObjects.Authenticate auth,
            DataObjects.TenantSettings settings)
        {
            var user = new DataObjects.User();
            
            // Custom authentication logic
            // Validate credentials against external system
            // Return populated user on success
            
            return user;
        }
    }
}
```

### Plugin with Prompts

```csharp
public Dictionary<string, object> Properties()
{
    return new Dictionary<string, object>
    {
        { "Id", new Guid("...") },
        { "Name", "Report Generator" },
        { "Prompts", new List<PluginPrompt>
            {
                new PluginPrompt {
                    Name = "startDate",
                    Type = PluginPromptType.Date,
                    Description = "Start Date",
                    Required = true
                },
                new PluginPrompt {
                    Name = "format",
                    Type = PluginPromptType.Select,
                    Description = "Output Format",
                    Options = new List<PluginPromptOption> {
                        new() { Label = "PDF", Value = "pdf" },
                        new() { Label = "Excel", Value = "xlsx" }
                    }
                }
            }
        }
    };
}
```

---

## Additional Assemblies

### .assemblies File

Create a file with the same name as your plugin but with `.assemblies` extension:

```
MyPlugin.cs
MyPlugin.assemblies
```

**MyPlugin.assemblies:**
```
typeof(Newtonsoft.Json.JsonConvert).Assembly.Location
typeof(System.Net.Http.HttpClient).Assembly.Location
.\MyCustom.dll
```

---

## Security

### Code Encryption

Plugins with `ContainsSensitiveData = true` have their code encrypted:

```
+-----------------------------------------------------------------------------+
|                         CODE ENCRYPTION FLOW                                |
+-----------------------------------------------------------------------------+

    Plugin Code                  Encryption                   Stored Code
         |                           |                             |
         |  ContainsSensitiveData    |                             |
         |  = true                   |                             |
         | ------------------------> |                             |
         |                           |                             |
         |                    +-------------+                      |
         |                    | AES-256     |                      |
         |                    | Encryption  |                      |
         |                    +-------------+                      |
         |                           |                             |
         |                           |                             |
         |                    Prepend IV to                        |
         |                    encrypted bytes                      |
         |                           |                             |
         |                           |                             |
         |                    Convert to hex                       |
         |                    string                               |
         |                           |                             |
         |                           | --------------------------> |
         |                                                         |
         |                           On Execution:                 |
         |                           | <-------------------------- |
         |                           |                             |
         |                    Detect "0x" prefix                   |
         |                    Decrypt before compile               |
         |                           |                             |
         | <------------------------ |                             |
         |   Decrypted code                                        |

```

### Tenant Restrictions

```csharp
// Limit plugin to specific tenants
{ "LimitToTenants", new List<Guid> {
    new Guid("tenant-1-guid"),
    new Guid("tenant-2-guid")
}}
```

---

## Interface Reference

### IPlugins Interface

```csharp
public interface IPlugins
{
    // All loaded plugins
    List<Plugin> AllPlugins { get; }
    
    // Plugins with code encrypted for cache storage
    List<Plugin> AllPluginsForCache { get; }
    
    // Execute dynamic C# code
    T? ExecuteDynamicCSharpCode<T>(
        string code,
        IEnumerable<object>? objects,
        List<string>? additionalAssemblies,
        string Namespace,
        string Classname,
        string invokerFunction
    );
    
    // Load plugins from folder
    List<Plugin> Load(string path);
    
    // Path to plugins folder
    string PluginFolder { get; }
    
    // Server assembly references
    List<string> ServerReferences { get; set; }
    
    // Global using statements
    List<string> UsingStatements { get; set; }
}
```

### IEncryption Interface

```csharp
public interface IEncryption
{
    string Decrypt(string? EncryptedData);
    string Decrypt(byte[] EncryptedData);
    T? DecryptObject<T>(byte[]? EncryptedObject);
    string Encrypt(string? ToEncrypt);
    byte[]? EncryptObject(object o);
    string GenerateChecksum(string input);
    byte[] GetNewEncryptionKey();
    string GetNewEncryptionKeyAsString();
}
```

---

## Dependencies

```xml
<ItemGroup>
  <!-- .NET 10 Reference Assemblies for Roslyn -->
  <PackageReference Include="Basic.Reference.Assemblies.Net100" Version="1.8.4" />
  
  <!-- Roslyn C# Compiler -->
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />
</ItemGroup>
```

---

## Usage in DataAccess

```csharp
// Initialize plugin system
IPlugins plugins = new Plugins.Plugins();
plugins.ServerReferences = serverAssemblyPaths;
plugins.UsingStatements = globalUsings;

// Load plugins from folder
var loadedPlugins = plugins.Load("./Plugins");

// Execute a plugin
var result = plugins.ExecuteDynamicCSharpCode<PluginExecuteResult>(
    plugin.Code,
    new object[] { parameter1, parameter2 },
    plugin.AdditionalAssemblies,
    plugin.Namespace,
    plugin.ClassName,
    plugin.Invoker
);
```

---

## Best Practices

1. **Always set unique Id**: Each plugin must have a unique GUID
2. **Use ContainsSensitiveData**: Set to `true` for any plugins with secrets
3. **Keep plugins focused**: One responsibility per plugin
4. **Handle errors gracefully**: Return meaningful error messages
5. **Use LimitToTenants**: Restrict plugins to appropriate tenants
6. **Version your plugins**: Track changes with version numbers
7. **Document properties**: Use Description for plugin documentation
