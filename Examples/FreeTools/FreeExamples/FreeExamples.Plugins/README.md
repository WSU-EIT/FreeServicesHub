# FreeExamples.Plugins

> Plugin runtime compiler — loads `.cs` and `.plugin` files at runtime using Roslyn, compiles them into assemblies, and makes them available to the application.

**Target:** .NET 10 · **Type:** Class Library

---

## What This Project Contains

| Area | Description |
|------|-------------|
| **Plugin Compiler** | Roslyn-based C# compilation of plugin source files at runtime |
| **Plugin Interfaces** | Base types and interfaces that plugins implement |
| **Assembly Resolution** | Dynamic assembly loading from `.assemblies` manifest files |

---

## Plugin Types

| Type | Description |
|------|-------------|
| `Auth` | Custom authentication logic |
| `BackgroundProcess` | Periodic tasks run by the background service |
| `Example` | Example/demo plugins |
| `UserUpdate` | Hooks that run when user records are modified |

---

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.CodeAnalysis.CSharp` | Roslyn C# compiler for runtime compilation |
| `Basic.Reference.Assemblies.Net100` | .NET reference assemblies for compilation targets |

---

## How Plugins Work

1. Plugin source files (`.cs` or `.plugin`) are placed in the `PluginFiles/` folder of the server project
2. At startup, the plugin compiler reads each file and compiles it using Roslyn
3. Compiled assemblies are cached in the `PluginCaches` database table
4. External dependencies are loaded from `.assemblies` manifest files

---

*Part of the [FreeExamples](..) suite.*
