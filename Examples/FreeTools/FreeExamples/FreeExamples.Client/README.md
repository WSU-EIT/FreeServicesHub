# FreeExamples.Client

> Blazor WebAssembly client application — UI pages, interactive examples, code playground with in-browser C#/Razor compilation, and dynamic component rendering.

**Target:** .NET 10 · **Type:** Blazor WebAssembly

---

## What This Project Contains

| Area | Description |
|------|-------------|
| **Pages** | Blazor routable pages for all examples (Dashboard, Files, Signatures, Settings, etc.) |
| **Shared** | Shared layout components and navigation (`ExampleNav.razor`) |
| **DynamicBlazorSupport** | In-browser C#/Razor compilation using Roslyn and CodeAnalysis — based on TryMudBlazor and SpawnDev.BlazorJS.CodeRunner |
| **Helpers** | Client-side utility classes, app icons, menu definitions |
| **wwwroot** | Static assets — CSS, JavaScript, images |

---

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.AspNetCore.Components.WebAssembly` | Blazor WASM runtime |
| `MudBlazor` | Material Design component library |
| `Blazor.Bootstrap` | Bootstrap component library |
| `Radzen.Blazor` | Radzen component library |
| `BlazorMonaco` | Monaco code editor (VS Code editor in browser) |
| `BlazorSortableList` | Drag-and-drop sortable lists |
| `Blazored.LocalStorage` | Browser local storage access |
| `FreeBlazor` | Custom Blazor component library |
| `FluentValidation` | Model validation rules |
| `CsvHelper` | CSV parsing/generation |
| `HtmlAgilityPack` | HTML parsing |
| `Humanizer` | String humanization (pluralization, casing, etc.) |
| `Microsoft.CodeAnalysis.CSharp` | Roslyn C# compiler for code playground |
| `Microsoft.CodeAnalysis.CSharp.Features` | Roslyn code analysis features (completions, diagnostics) |
| `Microsoft.AspNetCore.SignalR.Client` | SignalR client for real-time features |
| `MetadataReferenceService.BlazorWasm` | Assembly metadata resolution in WASM |

---

*Part of the [FreeExamples](..) suite.*
