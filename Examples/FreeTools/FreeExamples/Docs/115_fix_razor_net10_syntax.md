# Fix: Razor Syntax Changes for .NET 10

> **Date:** 2026-03-18  
> **Scope:** 6 Razor files in `FreeExamples.Client/Pages/Examples/`  
> **Root Cause:** .NET 10 Razor source generator rejects two patterns that compiled in .NET 9

---

## Problem 1 — `@{ var ... }` Code Blocks in Markup (RZ1010)

The Razor compiler now rejects explicit `@{ }` blocks inside `@if` branches.

```
  BEFORE                                       AFTER
  ══════                                       ═════

  @if (condition) {                            @if (condition) {
  │                                            │
  │   <div>Some HTML</div>                     │   <div>Some HTML</div>
  │                                            │
  │   @{  ◄── RZ1010: Unexpected "{"          │   @foreach (var x in GetData()) {
  │       var items = GetData();               │       <p>@x</p>
  │   }                                        │   }
  │                                            │
  │   @foreach (var x in items) {              │   ...
  │       <p>@x</p>                            │
  │   }                                        │
  │   ...                                      │
  }                                            }
```

### Fix Strategy

Move the variable into a **method** or **computed property** in `@code`, then reference it directly.

```
  ┌─────────────────────────────────────────────────────────────────────┐
  │  BEFORE (markup section)              AFTER (markup section)        │
  │  ───────────────────────              ────────────────────────      │
  │                                                                     │
  │  @{                                   @foreach (var s in            │
  │      var filtered = _tab == "a"           GetFilteredSprints()) {   │
  │          ? _sprints.Where(...)            ...                       │
  │          : _sprints.Where(...);       }                             │
  │  }                                                                  │
  │  @foreach (var s in filtered) {                                     │
  │      ...                              ─────────────────────────     │
  │  }                                    AFTER (@code section)         │
  │                                       ─────────────────────────     │
  │                                       IEnumerable<Sprint>           │
  │                                         GetFilteredSprints() =>     │
  │                                           _tab == "a"               │
  │                                             ? _sprints.Where(...)   │
  │                                             : _sprints.Where(...);  │
  └─────────────────────────────────────────────────────────────────────┘
```

**Files fixed:**

| File | `@{}` block | Replaced with |
|---|---|---|
| `SprintPlanning.razor` | `var filtered = ...` | `GetFilteredSprints()` method |
| `SprintPlanningV4.razor` | `var avg/best/worst/maxBar` | Computed properties (`VelocityAvg`, etc.) |
| `BacklogV3.razor` | `var groups = GetGroups()` | Inlined `GetGroups()` in `@foreach` |

---

## Problem 2 — Helper Methods That Render Markup (`__builder` CS0103)

In .NET 10, the Razor source generator no longer injects `__builder` into helper
methods that emit HTML. Any `void` method in `@code` that contains `<tags>` fails.

```
  BEFORE                                    ERROR
  ══════                                    ═════

  @code {                                   CS0103: The name '__builder'
      void RenderTree(Guid? id, int d) {    does not exist in the
          foreach (var p in children) {      current context
              <tr>  ◄── markup in method
                  <td>@p.Name</td>
              </tr>
              RenderTree(p.Id, d + 1);
          }
      }
  }
```

### Fix Strategy

Split the method into **data** (pure C#) and **markup** (`@foreach` in the template).

```
  ┌───────────────────────────────────────────────────────────────────────┐
  │                                                                       │
  │   BEFORE                               AFTER                          │
  │   ══════                               ═════                          │
  │                                                                       │
  │   ┌──── markup ─────┐                 ┌──── markup ──────────────┐    │
  │   │                  │                 │                          │    │
  │   │  <tbody>         │                 │  <tbody>                 │    │
  │   │    @{            │                 │    @foreach (var node    │    │
  │   │      RenderTree  │                 │        in GetFlatTree    │    │
  │   │        (null,0); │                 │           (null, 0)) {  │    │
  │   │    }             │                 │      var p = node.Proj;  │    │
  │   │  </tbody>        │                 │      <tr>                │    │
  │   │                  │                 │        <td>@p.Name</td>  │    │
  │   └──────────────────┘                 │      </tr>               │    │
  │                                        │    }                     │    │
  │   ┌──── @code ───────────────┐         │  </tbody>                │    │
  │   │                          │         │                          │    │
  │   │  void RenderTree(        │         └──────────────────────────┘    │
  │   │      Guid? pid, int d) { │                                        │
  │   │    foreach (var p in ..) │         ┌──── @code ──────────────┐    │
  │   │      <tr>        ◄─ BAD  │         │                          │    │
  │   │        <td>@p.Name</td>  │         │  record TreeNode(        │    │
  │   │      </tr>               │         │    Project Proj, int D); │    │
  │   │      RenderTree(         │         │                          │    │
  │   │        p.Id, d+1);       │         │  List<TreeNode>          │    │
  │   │    }                     │         │    GetFlatTree(           │    │
  │   │  }                       │         │      Guid? pid, int d) { │    │
  │   │                          │         │    // pure data, no HTML  │    │
  │   └──────────────────────────┘         │    result.Add(node);     │    │
  │                                        │    result.AddRange(       │    │
  │                                        │      GetFlatTree(..));    │    │
  │                                        │    return result;         │    │
  │                                        │  }                        │    │
  │                                        │                          │    │
  │                                        └──────────────────────────┘    │
  │                                                                       │
  └───────────────────────────────────────────────────────────────────────┘
```

The key insight:

```
   ┌─────────────────────────────────────────────────────┐
   │                                                     │
   │   MARKUP methods    ──X──►   DATA methods           │
   │   (emit <tags>)              (return List<T>)       │
   │                                                     │
   │   void RenderTree()          List<TreeNode>          │
   │     <tr>@p.Name</tr>          GetFlatTree()          │
   │     RenderTree(child)           .Add(node)           │
   │                                 .AddRange(recurse)   │
   │                                 return result        │
   │                                                     │
   │   Markup stays in    ◄────   @foreach (var node      │
   │   the .razor template           in GetFlatTree()) { │
   │   where __builder exists         <tr>@node...</tr>   │
   │                                }                     │
   └─────────────────────────────────────────────────────┘
```

**Files fixed:**

| File | Removed method | Replaced with |
|---|---|---|
| `Projects.razor` | `void RenderTree()` | `List<TreeNode> GetFlatTree()` + `@foreach` |
| `ProjectsV1.razor` | `void RenderTreeNodes()` | `List<TreeNode> GetFlatTreeNodes()` + `@foreach` |
| `ProjectsV2.razor` | `void RenderParentOptions()` | `List<ParentOption> GetParentOptions()` + `@foreach` |

---

## Rule of Thumb for .NET 10 Razor

```
   ╔═══════════════════════════════════════════════════════╗
   ║                                                       ║
   ║   1. No @{ var x = ...; } inside @if/else branches   ║
   ║      → use methods, properties, or inline @foreach    ║
   ║                                                       ║
   ║   2. No <html> inside @code methods                   ║
   ║      → return data, render with @foreach in markup    ║
   ║                                                       ║
   ╚═══════════════════════════════════════════════════════╝
```
