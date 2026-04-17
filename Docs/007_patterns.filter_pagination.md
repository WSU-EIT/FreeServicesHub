# FreeCRM: Filter, Pagination & Sorting Patterns

> Standard patterns for filterable, paginated, sortable list pages in FreeCRM.

**Source:** FreeCRM base template (`DataObjects.Filter`), Helpdesk4 (IpManager), FreeCICD (Dashboard)

---

## Overview

Every FreeCRM list page follows the same filter→API→render cycle:

```
User changes filter → Filter object updated → POST to API → Server paginates/sorts → Response rendered
```

> **Note:** For simple CRUD without pagination/sorting, prefer the three-endpoint pattern (GetMany/SaveMany/DeleteMany).
> See [007_patterns.crud_api.md](007_patterns.crud_api.md). The filter pattern below is for advanced list pages that need server-side pagination and sorting.

---

## The Filter Base Class

All filter DTOs extend `DataObjects.Filter`:

```csharp
public partial class Filter : ActionResponseObject
{
    public Guid TenantId { get; set; }
    public bool Loading { get; set; }
    public bool ShowFilters { get; set; }
    public bool IncludeDeletedItems { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string? Keyword { get; set; }
    public string? Sort { get; set; }
    public string? SortOrder { get; set; }     // "asc" or "desc"
    public int RecordsPerPage { get; set; }
    public int PageCount { get; set; }
    public int RecordCount { get; set; }
    public int Page { get; set; }
    public string? Export { get; set; }         // Populated for CSV export
    public List<FilterColumn>? Columns { get; set; }
}
```

**Extend it for your entity:**

```csharp
public partial class FilterExampleItems : Filter
{
    public List<ExampleItem>? Records { get; set; }
    public string? Status { get; set; }
    public Guid[]? Categories { get; set; } = new Guid[] { };
}
```

---

## Client-Side Pattern

### Filter Toggle + Load

```razor
<div class="btn-group mb-2" role="group">
    <button type="button" class="btn btn-success" @onclick="Add">
        <Language Tag="AddNew" IncludeIcon="true" />
    </button>
    <button type="button" class="btn btn-warning" @onclick="ClearFilter" disabled="@Filter.Loading">
        <Language Tag="Clear" IncludeIcon="true" />
    </button>
    @if (Filter.ShowFilters) {
        <button type="button" class="btn btn-dark" @onclick="ToggleShowFilter">
            <Language Tag="HideFilter" IncludeIcon="true" />
        </button>
    } else {
        <button type="button" class="btn btn-dark" @onclick="ToggleShowFilter">
            <Language Tag="ShowFilter" IncludeIcon="true" />
        </button>
    }
    <button type="button" class="btn btn-dark" @onclick="Refresh" disabled="@Filter.Loading">
        <Language Tag="Refresh" IncludeIcon="true" />
    </button>
</div>
```

### Sortable Column Headers

```razor
<thead>
    <tr>
        <SortableColumn Filter="@Filter" Column="Name" OnSortChanged="LoadFilter" />
        <SortableColumn Filter="@Filter" Column="Status" OnSortChanged="LoadFilter" />
        <SortableColumn Filter="@Filter" Column="Added" OnSortChanged="LoadFilter" />
    </tr>
</thead>
```

### Pagination

```razor
<Pagination Filter="@Filter" OnPageChanged="LoadFilter" />
```

### Load Filter Pattern

```csharp
protected DataObjects.FilterExampleItems Filter = new() {
    Page = 1,
    RecordsPerPage = 25,
    Sort = "Name",
    SortOrder = "asc",
};

protected async Task LoadFilter()
{
    Filter.Loading = true;
    Filter.TenantId = Model.TenantId;
    StateHasChanged();

    var result = await Helpers.GetOrPost<DataObjects.FilterExampleItems>(
        "api/Data/GetExampleItemsFiltered", Filter);

    if (result != null && Helpers.ActionResponse(result.ActionResponse).Result) {
        Filter = result;
    }

    Filter.Loading = false;
    StateHasChanged();
}

protected async Task ClearFilter()
{
    Filter = new DataObjects.FilterExampleItems {
        Page = 1,
        RecordsPerPage = 25,
        Sort = "Name",
        SortOrder = "asc",
        ShowFilters = Filter.ShowFilters,
    };
    await LoadFilter();
}

protected void ToggleShowFilter()
{
    Filter.ShowFilters = !Filter.ShowFilters;
}
```

### OnChangeHandler for Filter Inputs

FreeCRM uses `Helpers.OnChangeHandler` to debounce filter changes:

```razor
<input type="text" class="form-control"
    value="@Filter.Keyword"
    @onchange="@((ChangeEventArgs e) => Helpers.OnChangeHandler<string>(Filter, "Keyword", e, LoadFilter))" />
```

---

## Server-Side Pattern

```csharp
public async Task<DataObjects.FilterExampleItems> GetExampleItemsFiltered(DataObjects.FilterExampleItems filter)
{
    var output = filter;
    var sw = System.Diagnostics.Stopwatch.StartNew();

    var query = data.ExampleItems
        .Where(x => x.TenantId == filter.TenantId);

    // Apply keyword filter
    if (!string.IsNullOrWhiteSpace(filter.Keyword)) {
        query = query.Where(x => x.Name.Contains(filter.Keyword));
    }

    // Apply date range
    if (filter.Start.HasValue) query = query.Where(x => x.Added >= filter.Start.Value);
    if (filter.End.HasValue) query = query.Where(x => x.Added <= filter.End.Value);

    // Count before pagination
    output.RecordCount = await query.CountAsync();
    output.PageCount = (int)Math.Ceiling((double)output.RecordCount / output.RecordsPerPage);

    // Sort
    query = (filter.Sort?.ToLower(), filter.SortOrder?.ToLower()) switch {
        ("name", "desc") => query.OrderByDescending(x => x.Name),
        ("added", "asc") => query.OrderBy(x => x.Added),
        ("added", "desc") => query.OrderByDescending(x => x.Added),
        _ => query.OrderBy(x => x.Name),
    };

    // Paginate
    query = query.Skip((filter.Page - 1) * filter.RecordsPerPage).Take(filter.RecordsPerPage);

    output.Records = await query.Select(x => new DataObjects.ExampleItem {
        ExampleItemId = x.ExampleItemId,
        Name = x.Name,
        Status = (DataObjects.ExampleItemStatus)x.Status,
        Added = x.Added,
    }).ToListAsync();

    sw.Stop();
    output.ExecutionTime = sw.Elapsed.TotalMilliseconds;

    return output;
}
```

---

*Category: 007_patterns*
*Source: FreeCRM base template, Helpdesk4, FreeCICD*
