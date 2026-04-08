# FreeCICD.Client

## Overview

The `FreeCICD.Client` project is the **Blazor WebAssembly client** application. It contains the UI components, state management via `BlazorDataModel`, API helpers, and all client-side logic. This project runs entirely in the browser.

---

## Project Structure

```
FreeCICD.Client/
+-- Pages/                         # Razor page components
|   +-- Index.razor               # Home page
|   +-- Login.razor               # Login page
|   +-- Profile.razor             # User profile
|   +-- Users.razor               # User management
|   +-- ...                       # Other pages
+-- Shared/
|   +-- AppComponents/            # Reusable app components
|   |   +-- Index.App.razor       # Home page extension
|   |   +-- ...                   # Other app components
|   +-- MainLayout.razor          # Main layout wrapper
|   +-- NavMenu.razor             # Navigation menu
+-- wwwroot/
|   +-- css/                      # Stylesheets
|   +-- js/                       # JavaScript files
|   +-- ...                       # Static assets
+-- DataModel.cs                  # BlazorDataModel (state)
+-- DataModel.App.cs              # App-specific model extensions
+-- Helpers.cs                    # API and utility helpers
+-- Helpers.App.cs                # App-specific helper extensions
+-- Program.cs                    # WASM entry point
+-- _Imports.razor                # Global Razor imports
+-- FreeCICD.Client.csproj        # Project file
```

---

## Architecture Diagram

```
+-----------------------------------------------------------------------------+
|                      BLAZOR WEBASSEMBLY CLIENT                              |
+-----------------------------------------------------------------------------+

    Browser (WebAssembly Runtime)
           |
           |
+-----------------------------------------------------------------------------+
|                          Razor Components                                   |
|  +-----------+  +-----------+  +-----------+  +-----------+                |
|  |   Pages/    |  |   Shared/   |  |   Layouts/  |  |  AppComps/  |        |
|  |   Index     |  |   NavMenu   |  | MainLayout  |  |  Custom UI  |        |
|  |   Login     |  |  Dialogs    |  |             |  |             |        |
|  |   Users     |  |             |  |             |  |             |        |
|  +-----------+  +-----------+  +-----------+  +-----------+                |
|         |                |                |                |               |
|         +----------------------------------------------------+               |
|                                   |                                        |
|                                   |                                        |
|  +---------------------------------------------------------------------+   |
|  |                      BlazorDataModel (State)                        |   |
|  |  +-----------+  +-----------+  +-----------+                        |   |
|  |  |    User     |  |   Tenant    |  |  Languages  |                  |   |
|  |  |  LoggedIn   |  |  Settings   |  |  Messages   |                  |   |
|  |  |  Loaded     |  |  Features   |  |  Plugins    |                  |   |
|  |  +-----------+  +-----------+  +-----------+                        |   |
|  |                                                                     |   |
|  |  Events:  OnChange -> OnSignalRUpdate -> OnTenantChanged           |   |
|  +---------------------------------------------------------------------+   |
|                                   |                                        |
|                                   |                                        |
|  +---------------------------------------------------------------------+   |
|  |                        Helpers (API Client)                         |   |
|  |  +-----------+  +-----------+  +-----------+                        |   |
|  |  | HTTP Client |  |   SignalR   |  |  Utilities  |                  |   |
|  |  |  API Calls  |  |  Real-time  |  |  Formatting |                  |   |
|  |  +-----------+  +-----------+  +-----------+                        |   |
|  +---------------------------------------------------------------------+   |
|                                   |                                        |
+-----------------------------------------------------------------------------+
                                    |
                                    |
                         +------------------+
                         |   HTTP/SignalR   |
                         |   to Server      |
                         +------------------+
```

---

## BlazorDataModel

The `BlazorDataModel` is the **central state container** for the entire client application. It uses a publish-subscribe pattern to notify components of state changes.

### Key Properties

```csharp
public partial class BlazorDataModel
{
    // Authentication State
    public bool LoggedIn { get; set; }
    public bool Loaded { get; set; }
    public DataObjects.User User { get; set; }
    public string Fingerprint { get; set; }
    
    // Tenant State
    public Guid TenantId { get; set; }
    public DataObjects.Tenant Tenant { get; set; }
    public List<DataObjects.Tenant> Tenants { get; set; }
    public bool UseTenantCodeInUrl { get; set; }
    
    // UI State
    public string View { get; set; }
    public string? NavigationId { get; set; }
    public string Theme { get; set; }
    public List<Message> Messages { get; set; }
    
    // Data Collections
    public List<DataObjects.Department> Departments { get; set; }
    public List<DataObjects.UserGroup> UserGroups { get; set; }
    public List<DataObjects.Tag> Tags { get; set; }
    public List<Plugins.Plugin> Plugins { get; set; }
    
    // Configuration
    public DataObjects.AuthenticationProviders AuthenticationProviders { get; set; }
    public List<string>? GloballyDisabledModules { get; set; }
    public List<string>? GloballyEnabledModules { get; set; }
}
```

### Events

```csharp
// Subscribe to state changes
Model.OnChange += HandleStateChange;

// Subscribe to SignalR updates
Model.OnSignalRUpdate += HandleSignalRUpdate;

// Subscribe to tenant changes
Model.OnTenantChanged += HandleTenantChanged;
```

### Toast Messages

```csharp
// Show a success message
Model.Message_Saved();

// Show a loading message
Model.Message_Loading();

// Show an error
Model.ErrorMessage("Something went wrong");
Model.ErrorMessages(new List<string> { "Error 1", "Error 2" });

// Clear messages
Model.ClearMessages();
```

---

## State Management Flow

```
+-----------------------------------------------------------------------------+
|                         STATE MANAGEMENT FLOW                               |
+-----------------------------------------------------------------------------+

    Component A                  BlazorDataModel                  Component B
         |                            |                                |
         |  Set User                  |                                |
         | -------------------------> |                                |
         |                            |                                |
         |                   Compare old vs new                        |
         |                   (ObjectsAreEqual)                         |
         |                            |                                |
         |                    +---------------+                        |
         |                    |   Changed?    |                        |
         |                    +---------------+                        |
         |                            |                                |
         |                   YES      |                                |
         |                    |       |                                |
         |                    |       |                                |
         |            Update ModelUpdated                              |
         |            Invoke OnChange                                  |
         |                    |       |                                |
         |                    |       |                                |
         |                    +-------------------------------->        |
         |                            |              OnChange fired    |
         |                            |                                |
         |                            |                   StateHasChanged()
         |                            |                       Re-render
         |                            |                                |


SignalR Update Flow:

    Server                     SignalR Hub                    Component
       |                           |                              |
       |  SignalRUpdate            |                              |
       |  (User changed)           |                              |
       | ------------------------> |                              |
       |                           |                              |
       |                           |  Broadcast to Group          |
       |                           | ---------------------------> |
       |                           |                              |
       |                           |                   OnSignalRUpdate
       |                           |                   HandleUpdate()
       |                           |                              |
       |                           |                   Update local data
       |                           |                   StateHasChanged()
       |                           |                              |
```

---

## Feature Flags

The model provides feature flag helpers:

```csharp
// Check if a feature is enabled
bool enabled = Model.FeatureEnabledDepartments;
bool filesEnabled = Model.FeatureEnabledFiles;
bool tagsEnabled = Model.FeatureEnabledTags;

// Check if user can manage feature
bool canManageEmail = Model.AllowUsersToManageEmail;
bool canManageDept = Model.AllowUsersToManageDepartment;
```

### Feature Enable Logic

```
+-----------------------------------------------------------------------+
|                    FEATURE FLAG LOGIC                                 |
+-----------------------------------------------------------------------+
|                                                                       |
|  1. Check GloballyDisabledModules -> Return FALSE                     |
|                 |                                                     |
|  2. Check GloballyEnabledModules -> Return TRUE                       |
|                 |                                                     |
|  3. Check Tenant.ModuleHideElements -> Return FALSE                   |
|                 |                                                     |
|  4. Check Tenant.ModuleOptInElements -> Return TRUE                   |
|                 |                                                     |
|  5. Default: FALSE                                                    |
|                                                                       |
+-----------------------------------------------------------------------+
```

---

## API Helpers

The `Helpers` class provides static methods for API communication:

```csharp
// HTTP Client
HttpClient client = Helpers.GetHttpClient(baseUrl);

// API Calls (example pattern)
var user = await Helpers.GetUserAsync(userId);
var result = await Helpers.SaveUserAsync(user);

// Language/Text
string text = Helpers.Text("SaveButton");  // Get translated text

// URL Helpers
string currentUrl = Helpers.CurrentUrl;
```

---

## Language/Internationalization

```csharp
// Get translated text
string label = Helpers.Text("Username");

// Replace language tags in strings
string message = Model.ReplaceLanguageTagsInString("{{SaveButton}} clicked");

// Available phrases from Model.Language.Phrases
// Default language from Model.DefaultLanguage
```

---

## User-Defined Fields (UDF)

```csharp
// Get UDF label
string label = Model.UdfLabel("Users", 1);  // UDF01 label

// Get UDF type (input, select, checkbox, etc.)
string fieldType = Model.UdfFieldType("Users", 1);

// Get UDF options (for select/checkbox types)
List<string> options = Model.UdfFieldOptions("Users", 1);

// Check visibility
bool showField = Model.UdfShowField("Users", 1);
bool showColumn = Model.UdfShowColumn("Users", 1);
bool showInFilter = Model.UdfShowInFilter("Users", 1);
```

---

## Extension Points

### DataModel.App.cs

```csharp
public partial class BlazorDataModel
{
    // App-specific state properties
    private string _myCustomProperty = "";
    
    public string MyCustomProperty {
        get { return _myCustomProperty; }
        set {
            if (_myCustomProperty != value) {
                _myCustomProperty = value;
                _ModelUpdated = DateTime.UtcNow;
                NotifyDataChanged();
            }
        }
    }
    
    // App-specific deleted record check
    public bool HaveDeletedRecordsApp => false;
}
```

### Helpers.App.cs

```csharp
public partial class Helpers
{
    // App-specific API calls
    public static async Task<List<MyCustomObject>> GetCustomDataAsync()
    {
        // Implementation
    }
}
```

---

## Component Patterns

### Subscribing to Model Changes

```razor
@inject BlazorDataModel Model

@code {
    protected override void OnInitialized()
    {
        Model.OnChange += HandleModelChange;
    }
    
    private void HandleModelChange()
    {
        InvokeAsync(StateHasChanged);
    }
    
    public void Dispose()
    {
        Model.OnChange -= HandleModelChange;
    }
}
```

### Handling SignalR Updates

```razor
@code {
    protected override void OnInitialized()
    {
        Model.OnSignalRUpdate += HandleSignalRUpdate;
    }
    
    private void HandleSignalRUpdate(DataObjects.SignalRUpdate update)
    {
        if (update.UpdateType == DataObjects.SignalRUpdateType.User &&
            update.ItemId == currentUserId)
        {
            // Reload user data
            InvokeAsync(async () => {
                await LoadUserData();
                StateHasChanged();
            });
        }
    }
}
```

---

## Toast Message Types

```
+-----------------------------------------------------------------------+
|                    MESSAGE TYPES                                      |
+-----------------------------------------------------------------------+
|                                                                       |
|  MessageType.Primary    -> Blue (informational)                       |
|  MessageType.Secondary  -> Gray (secondary info)                      |
|  MessageType.Success    -> Green (success, saved)                     |
|  MessageType.Danger     -> Red (error, delete)                        |
|  MessageType.Warning    -> Yellow (warning, update)                   |
|  MessageType.Info       -> Light blue (info)                          |
|  MessageType.Light      -> Light (neutral)                            |
|  MessageType.Dark       -> Dark (default)                             |
|                                                                       |
+-----------------------------------------------------------------------+
```

---

## Dependencies

```xml
<ItemGroup>
  <!-- Blazor WebAssembly -->
  <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.0" />
  
  <!-- UI Component Libraries -->
  <PackageReference Include="Blazor.Bootstrap" Version="3.4.2" />
  <PackageReference Include="MudBlazor" Version="8.8.0" />
  
  <!-- SignalR Client -->
  <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.0" />
</ItemGroup>
```

---

## Best Practices

1. **Always subscribe/unsubscribe**: Clean up event subscriptions in `Dispose()`
2. **Use InvokeAsync**: Always call `StateHasChanged()` via `InvokeAsync`
3. **Check Model.Loaded**: Wait for model to load before rendering data
4. **Check Model.LoggedIn**: Guard authenticated content
5. **Use feature flags**: Check `FeatureEnabled*` before showing features
6. **Handle SignalR**: Update local state when receiving updates
7. **Use toast messages**: Provide feedback for user actions
8. **Extend via App files**: Don't modify core model/helper files
