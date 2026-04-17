# FreeCRM: Three-Endpoint CRUD API Pattern

> Minimize API surface to three endpoints per entity: **GetMany**, **SaveMany**, **DeleteMany**.

**Source:** Team preference — simplifies controller, client, and testing.

---

## Philosophy

Instead of 6+ endpoints per entity (Get, GetById, GetFiltered, Add, Update, Delete), use **three**:

| Endpoint | Accepts | Returns | Behavior |
|----------|---------|---------|----------|
| `GetMany` | `List<Guid>?` (nullable) | `List<T>` | `null`/empty → return all; IDs → return matching |
| `SaveMany` | `List<T>` | `List<T>` (saved) | PK exists → update; PK missing/empty → insert with generated ID; PK provided but new → insert with that ID |
| `DeleteMany` | `List<Guid>` | `BooleanResponse` | Must provide IDs explicitly; `null`/empty → error |

**Single-item convenience methods** wrap the "many" versions:

```csharp
// These just call the batch versions with a single-item list
public T Save(T item) => SaveMany([item]).First();
public T? Get(Guid id) => GetMany([id]).FirstOrDefault();
public void Delete(Guid id) => DeleteMany([id]);
```

---

## DataAccess Pattern

```csharp
public async Task<List<DataObjects.ExampleItem>> GetSampleItems(List<Guid>? ids)
{
    var query = data.ExampleItems.AsQueryable();

    if (ids != null && ids.Count > 0) {
        query = query.Where(x => ids.Contains(x.ExampleItemId));
    }

    return await query.OrderBy(x => x.Name).Select(x => new DataObjects.ExampleItem {
        ExampleItemId = x.ExampleItemId,
        Name = x.Name,
        // ... map fields
    }).ToListAsync();
}

public async Task<List<DataObjects.ExampleItem>> SaveSampleItems(
    List<DataObjects.ExampleItem> items, DataObjects.User currentUser)
{
    var saved = new List<DataObjects.ExampleItem>();

    foreach (var item in items) {
        // If no ID provided, generate one
        if (item.ExampleItemId == Guid.Empty) {
            item.ExampleItemId = Guid.NewGuid();
        }

        var existing = await data.ExampleItems.FindAsync(item.ExampleItemId);

        if (existing != null) {
            // Update
            existing.Name = item.Name;
            existing.LastModified = DateTime.UtcNow;
            existing.LastModifiedBy = currentUser.DisplayName();
        } else {
            // Insert
            data.ExampleItems.Add(new EFModels.ExampleItem {
                ExampleItemId = item.ExampleItemId,
                Name = item.Name,
                Added = DateTime.UtcNow,
                AddedBy = currentUser.DisplayName(),
                LastModified = DateTime.UtcNow,
                LastModifiedBy = currentUser.DisplayName(),
            });
        }

        saved.Add(item);
    }

    await data.SaveChangesAsync();
    return saved;
}

public async Task<DataObjects.BooleanResponse> DeleteSampleItems(List<Guid>? ids)
{
    var output = new DataObjects.BooleanResponse();

    if (ids == null || ids.Count == 0) {
        output.Messages.Add("You must provide IDs to delete. Use GetMany first to find them.");
        return output;
    }

    var records = await data.ExampleItems.Where(x => ids.Contains(x.ExampleItemId)).ToListAsync();
    data.ExampleItems.RemoveRange(records);
    await data.SaveChangesAsync();

    output.Result = true;
    return output;
}
```

---

## API Controller Pattern

```csharp
[HttpPost]
[Authorize]
[Route("~/api/Data/GetSampleItems")]
public async Task<ActionResult<List<DataObjects.ExampleItem>>> GetSampleItems(List<Guid>? ids)
{
    return Ok(await da.GetSampleItems(ids));
}

[HttpPost]
[Authorize]
[Route("~/api/Data/SaveSampleItems")]
public async Task<ActionResult<List<DataObjects.ExampleItem>>> SaveSampleItems(List<DataObjects.ExampleItem> items)
{
    return Ok(await da.SaveSampleItems(items, CurrentUser));
}

[HttpPost]
[Authorize]
[Route("~/api/Data/DeleteSampleItems")]
public async Task<ActionResult<DataObjects.BooleanResponse>> DeleteSampleItems(List<Guid>? ids)
{
    return Ok(await da.DeleteSampleItems(ids));
}
```

---

## Client Usage

```csharp
// Get all
var all = await Helpers.GetOrPost<List<DataObjects.ExampleItem>>("api/Data/GetSampleItems", new List<Guid>());

// Get specific
var some = await Helpers.GetOrPost<List<DataObjects.ExampleItem>>("api/Data/GetSampleItems", new List<Guid> { id1, id2 });

// Save (add or update — doesn't matter)
var saved = await Helpers.GetOrPost<List<DataObjects.ExampleItem>>("api/Data/SaveSampleItems", new List<DataObjects.ExampleItem> { item });

// Delete
var result = await Helpers.GetOrPost<DataObjects.BooleanResponse>("api/Data/DeleteSampleItems", new List<Guid> { item.ExampleItemId });

// Delete all (explicit — must fetch IDs first)
var allItems = await Helpers.GetOrPost<List<DataObjects.ExampleItem>>("api/Data/GetSampleItems", new List<Guid>());
var deleteResult = await Helpers.GetOrPost<DataObjects.BooleanResponse>(
    "api/Data/DeleteSampleItems", allItems.Select(x => x.ExampleItemId).ToList());
```

---

## Why Three Endpoints

| Traditional (6+) | Three-Endpoint | Benefit |
|-------------------|---------------|---------|
| GET /items | POST GetMany([]) | One endpoint, flexible |
| GET /items/{id} | POST GetMany([id]) | Same endpoint |
| GET /items/filtered | POST GetMany(ids from filter) | Client filters, or add a FilterMany |
| POST /items | POST SaveMany([item]) | Same endpoint |
| PUT /items/{id} | POST SaveMany([item]) | Same endpoint |
| DELETE /items/{id} | POST DeleteMany([id]) | Explicit, safe |
| DELETE /items (bulk) | POST DeleteMany(ids) | Same endpoint |

---

*Category: 007_patterns*
*Source: Team preference, applied across FreeExamples*
