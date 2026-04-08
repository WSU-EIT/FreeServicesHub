# FreeGLBA.Client

Blazor WebAssembly client application for the FreeGLBA GLBA Compliance Data Access Tracking System. Contains all UI components, pages, and client-side logic.

Developed by **Enrollment Information Technology** at **Washington State University**.

## Purpose

This project provides the interactive web UI for FreeGLBA:
- **Dashboard** - Real-time access event monitoring
- **Source Systems** - Manage external systems and API keys
- **Access Events** - Browse, search, and filter access logs
- **Reports** - Generate compliance reports
- **Settings** - Configure application settings
- **User Management** - Manage users and permissions

## Technology Stack

- **.NET 10** - Blazor WebAssembly
- **MudBlazor** - Material Design component library
- **Blazor.Bootstrap** - Bootstrap components
- **Radzen.Blazor** - Additional UI components
- **SignalR** - Real-time updates

## Dependencies

| Package | Purpose |
|---------|---------|
| `MudBlazor` | Material Design UI components |
| `Blazor.Bootstrap` | Bootstrap components for Blazor |
| `Radzen.Blazor` | Data grid, charts, forms |
| `BlazorMonaco` | Monaco code editor |
| `BlazorSortableList` | Drag-and-drop lists |
| `Blazored.LocalStorage` | Browser local storage |
| `FreeBlazor` | Custom Blazor utilities |
| `CsvHelper` | CSV export |
| `HtmlAgilityPack` | HTML parsing |
| `Humanizer` | String formatting |
| `Microsoft.AspNetCore.SignalR.Client` | Real-time communication |

### Project References
- **FreeGLBA.DataObjects** - DTOs and API endpoints

## Project Structure

```
FreeGLBA.Client/
├── FreeGLBA.Client.csproj
├── README.md
├── Program.cs                      # WebAssembly entry point
├── _Imports.razor                  # Global Blazor imports
├── Helpers.cs                      # Client-side utilities
│
├── Pages/                          # Routable pages
│   ├── Index.razor                 # Dashboard
│   ├── About.razor
│   ├── Authorization/              # Auth pages
│   ├── Settings/                   # Settings pages
│   └── [Entity]/                   # CRUD pages per entity
│
├── Shared/                         # Shared components
│   ├── MainLayout.razor
│   ├── NavMenu.razor
│   └── AppComponents/              # Reusable components
│
└── wwwroot/                        # Static assets
    ├── css/
    ├── js/
    ├── images/
    ├── appsettings.json            # Client configuration
    └── index.html                  # SPA host page
```

## Key Features

### Dashboard
- Real-time event counter
- Recent events feed (SignalR updates)
- Statistics by category and access type
- Source system health indicators

### Source System Management
- Register new source systems
- Generate and rotate API keys
- View event counts and last activity
- Enable/disable systems

### Access Event Browser
- Advanced filtering (date, user, subject, type)
- Full-text search
- Export to CSV
- Drill-down to event details

### Compliance Reports
- Date range selection
- Filter by source system
- PDF export
- Scheduled report generation

## Configuration

### wwwroot/appsettings.json

```json
{
  "ApiBaseUrl": "https://your-server.com",
  "SignalREnabled": true,
  "DefaultPageSize": 25
}
```

## Usage with Main Application

This project is referenced by the main `FreeGLBA` server project and runs as a Blazor WebAssembly application hosted by the server:

```xml
<!-- In FreeGLBA.csproj -->
<ProjectReference Include="..\FreeGLBA.Client\FreeGLBA.Client.csproj" />
```

The server hosts the WebAssembly files and serves them to browsers.

## Component Libraries

### MudBlazor Components Used
- `MudDataGrid` - Data tables with sorting, filtering, paging
- `MudChart` - Charts and graphs
- `MudDialog` - Modal dialogs
- `MudForm` - Form validation
- `MudNavMenu` - Navigation menus
- `MudAppBar` - Top app bar

### Radzen Components Used
- `RadzenDataGrid` - Advanced data grid
- `RadzenChart` - Charts
- `RadzenDropDown` - Dropdowns

### BlazorMonaco
Used for code editing in the plugin management interface.

## API Communication

Uses `HttpClient` to communicate with the server API:

```csharp
@inject HttpClient Http

var events = await Http.GetFromJsonAsync<List<AccessEvent>>(
    Endpoints.FreeGLBA.GetAccessEvents);
```

## Real-Time Updates

SignalR connection for live dashboard updates:

```csharp
@inject HubConnection HubConnection

protected override async Task OnInitializedAsync()
{
    HubConnection.On<AccessEvent>("NewEvent", OnNewEvent);
    await HubConnection.StartAsync();
}
```

## Styling

- **Bootstrap 5** - Base styling
- **MudBlazor Theme** - Material Design colors
- **Custom CSS** - In `wwwroot/css/`

## Browser Support

- Chrome (recommended)
- Firefox
- Edge
- Safari

*Note: WebAssembly required - IE11 not supported*

## About

FreeGLBA is developed and maintained by the **Enrollment Information Technology** team at **Washington State University**.

🔗 [Meet Our Staff](https://em.wsu.edu/eit/meet-our-staff/)
