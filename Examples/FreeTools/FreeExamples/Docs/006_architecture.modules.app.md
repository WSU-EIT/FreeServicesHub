# FreeCRM Hook: Modules.App.razor & site.App.css

> Server-side HTML injection (head, body, JavaScript) and custom CSS.

**Files:** `{ProjectName}/Components/Modules.App.razor`, `{ProjectName}.Client/wwwroot/css/site.App.css`
**Complexity:** MEDIUM — 3 injection areas + CSS placeholder

---

## Modules.App.razor

Server-side Razor component that injects into the HTML document structure.

### Injection Areas

| Module (switch case) | Renders In | Use For |
|----------------------|-----------|---------|
| `"head"` | `<head>` tag | External CSS, meta tags, fonts |
| `"body"` | `<body>` tag (before closing) | External script tags |
| `"javascript"` | Inline `<script>` block | Custom JavaScript functions |

### Example: Adding External Libraries

```razor
@switch (Module) {
    case "head":
        <link rel="stylesheet" href="https://cdn.example.com/library.css" />
        <link rel="stylesheet" href="@(applicationUrl + "_content/MyLib/styles.css?v=" + appVersion)" />
        break;

    case "body":
        <script src="https://cdn.example.com/library.js"></script>
        <script src="@(applicationUrl + "_content/MyLib/script.js?v=" + appVersion)"></script>
        break;

    case "javascript":
        <script type="text/javascript">
            const onReadyApp = (callback) => {
                if (document.readyState != 'loading') callback();
                else document.addEventListener('DOMContentLoaded', callback);
            };

            onReadyApp(() => {
                initMyLibrary();
            });

            function initMyLibrary() {
                console.log("Library initialized");
            }
        </script>
        break;
}
```

### Available Variables

The component has access to these server-side variables:

```csharp
string applicationUrl = data.ApplicationUrl(HttpContextAccessor.HttpContext);
string appVersion = data.Version;
string basePath = ConfigurationHelper.BasePath ?? "/";
```

Use `appVersion` as a cache-buster query parameter: `?v=@appVersion`.

---

## site.App.css

```css
/* {ProjectName}.Client/wwwroot/css/site.App.css */
/* Add any app-specific CSS here. */
```

**Use for:** App-specific styles that don't belong in the framework's `site.css`.

**Example:**

```css
/* Pipeline dashboard styles */
.pipeline-card { border-left: 4px solid var(--bs-primary); }
.pipeline-card.succeeded { border-left-color: var(--bs-success); }
.pipeline-card.failed { border-left-color: var(--bs-danger); }

/* GLBA compliance styles */
.access-event-row.bulk { background-color: rgba(var(--bs-warning-rgb), 0.1); }
```

---

## Suggested File Names

| Scenario | File Name |
|----------|-----------|
| Custom JavaScript module | `{ProjectName}.App.{Feature}.js` (colocated with Razor) |
| Additional CSS | `{ProjectName}.App.{Feature}.css` |

---

*Category: 006_architecture*
*Source: `ReferenceProjects/FreeCRM-main/CRM/Components/Modules.App.razor`, `CRM.Client/wwwroot/css/site.App.css`*
