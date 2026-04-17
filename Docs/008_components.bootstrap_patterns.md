# FreeCRM: Bootstrap UI Patterns (Cards, Tabs, Modals, Badges)

> Common Bootstrap 5 component patterns used across all FreeCRM projects.

**Source:** FreeCICD (Pipeline Cards), Helpdesk4 (IP Manager tabs), all projects (modals, badges)

---

## Pattern 1: Status Card with Badge and Actions

From FreeCICD's pipeline dashboard — a card pattern showing entity status with clickable elements:

```razor
<div class="card h-100 shadow-sm border-0">
    @* Header: Title + Status Badge *@
    <div class="card-header bg-white border-bottom-0 py-3">
        <div class="d-flex justify-content-between align-items-start">
            <div class="flex-grow-1 me-2">
                <h6 class="card-title mb-0">
                    <a href="@Item.Url" target="_blank"
                       class="text-decoration-none text-dark text-truncate"
                       title="@Item.Name" style="max-width: 180px;">
                        @Item.Name
                    </a>
                </h6>
            </div>
            @GetStatusBadge()
        </div>
    </div>

    @* Body: Metadata *@
    <div class="card-body pt-0">
        <div class="small text-muted mb-2">
            <i class="fa fa-database me-1"></i>@Item.Category
        </div>
        <div class="small text-muted d-flex align-items-center gap-2">
            <i class="fa fa-clock me-1"></i>
            <span title="@Item.LastModified.ToString("g")">
                @Item.LastModified.Humanize()
            </span>
        </div>
    </div>

    @* Footer: Action Buttons *@
    <div class="card-footer bg-white border-top-0 pt-0 pb-3">
        <div class="d-flex gap-2">
            <button class="btn btn-sm btn-outline-primary" @onclick="@(() => Edit(Item))">
                <i class="fa fa-edit"></i>
            </button>
            <button class="btn btn-sm btn-outline-success" @onclick="@(() => Run(Item))">
                <i class="fa fa-play"></i>
            </button>
        </div>
    </div>
</div>

@code {
    [Parameter] public DataObjects.DashboardItem Item { get; set; } = new();

    private RenderFragment GetStatusBadge() => __builder => {
        string badgeClass = Item.Status switch {
            "succeeded" => "bg-success",
            "failed" => "bg-danger",
            "running" => "bg-primary",
            "canceled" => "bg-warning text-dark",
            _ => "bg-secondary",
        };
        <span class="badge @badgeClass">@Item.Status</span>
    };
}
```

### Card Grid Layout

```razor
<div class="row row-cols-1 row-cols-md-2 row-cols-lg-3 row-cols-xl-4 g-3">
    @foreach (var item in Items) {
        <div class="col">
            <ItemCard Item="@item" />
        </div>
    }
</div>
```

---

## Pattern 2: Card/Table View Toggle

Switch between card grid and table views (FreeCICD pattern):

```razor
<div class="btn-group mb-2" role="group">
    <button class="btn @(_viewMode == "card" ? "btn-primary" : "btn-outline-primary")"
            @onclick="@(() => _viewMode = "card")">
        <i class="fa fa-th-large"></i>
    </button>
    <button class="btn @(_viewMode == "table" ? "btn-primary" : "btn-outline-primary")"
            @onclick="@(() => _viewMode = "table")">
        <i class="fa fa-list"></i>
    </button>
</div>

@if (_viewMode == "card") {
    <div class="row row-cols-1 row-cols-md-3 g-3">
        @foreach (var item in Items) {
            <div class="col"><ItemCard Item="@item" /></div>
        }
    </div>
} else {
    <table class="table table-hover">
        @* Table rows *@
    </table>
}

@code {
    private string _viewMode = "card";
}
```

---

## Pattern 3: Bootstrap Tabs

```razor
<ul class="nav nav-tabs mb-3" role="tablist">
    <li class="nav-item" role="presentation">
        <button class="nav-link @(_activeTab == "general" ? "active" : "")"
                @onclick="@(() => _activeTab = "general")">
            <Language Tag="General" />
        </button>
    </li>
    <li class="nav-item" role="presentation">
        <button class="nav-link @(_activeTab == "advanced" ? "active" : "")"
                @onclick="@(() => _activeTab = "advanced")">
            <Language Tag="Advanced" />
        </button>
    </li>
</ul>

<div class="tab-content">
    @if (_activeTab == "general") {
        @* General tab content *@
    }
    @if (_activeTab == "advanced") {
        @* Advanced tab content *@
    }
</div>

@code {
    private string _activeTab = "general";
}
```

---

## Pattern 4: Modal/Dialog with Radzen

FreeCRM uses `Radzen.DialogService` for modals:

```csharp
// Open a dialog
var result = await DialogService.OpenAsync<EditItemDialog>(
    Helpers.Text("EditItem"),
    new Dictionary<string, object> { { "ItemId", itemId } },
    new Radzen.DialogOptions {
        Width = "600px",
        Height = "auto",
        Resizable = true,
        Draggable = true,
        CloseDialogOnOverlayClick = false,
    });

// Handle result
if (result is DataObjects.ExampleItem savedItem) {
    await LoadData();
}
```

### Dialog Component

```razor
@inject Radzen.DialogService DialogService

<div class="mb-2">
    @* Form fields here *@
</div>

<div class="d-flex gap-2">
    <button class="btn btn-success" @onclick="Save">
        <Language Tag="Save" IncludeIcon="true" />
    </button>
    <button class="btn btn-dark" @onclick="Close">
        <Language Tag="Cancel" IncludeIcon="true" />
    </button>
</div>

@code {
    [Parameter] public Guid ItemId { get; set; }

    private async Task Save()
    {
        // Save logic...
        DialogService.Close(_item);  // Return data to caller
    }

    private void Close()
    {
        Model.DialogOpen = false;
        DialogService.Close();
    }
}
```

---

## Pattern 5: Delete Confirmation

The `DeleteConfirmation` component provides a two-click delete pattern:

```razor
<DeleteConfirmation OnConfirmed="Delete"
    CancelText="@Helpers.ConfirmButtonTextCancel"
    DeleteText="@Helpers.ConfirmButtonTextDelete"
    ConfirmDeleteText="@Helpers.ConfirmButtonTextConfirmDelete" />
```

First click shows "Delete", second click shows "Confirm Delete", with a cancel option.

---

## Pattern 6: Status Badges

```csharp
public static string GetStatusBadgeClass(string status) => status?.ToLower() switch {
    "active" or "succeeded" or "completed" => "bg-success",
    "failed" or "error" => "bg-danger",
    "running" or "processing" or "inprogress" => "bg-primary",
    "canceled" or "warning" => "bg-warning text-dark",
    "draft" or "pending" => "bg-secondary",
    _ => "bg-light text-dark",
};
```

```razor
<span class="badge @GetStatusBadgeClass(item.Status)">@item.Status</span>
```

---

*Category: 008_components*
*Source: FreeCICD (Pipeline Cards), Helpdesk4, all FreeCRM projects*
