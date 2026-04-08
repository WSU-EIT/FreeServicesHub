using System.Collections.Concurrent;

namespace FreeExamples;

public partial interface IDataAccess
{
    List<DataObjects.SampleItem> GetSampleItems(List<Guid>? ids);
    DataObjects.FilterSampleItems GetSampleItemsFiltered(DataObjects.FilterSampleItems filter);
    List<DataObjects.SampleItem> SaveSampleItems(List<DataObjects.SampleItem> items, DataObjects.User? currentUser);
    DataObjects.BooleanResponse DeleteSampleItems(List<Guid>? ids);
    DataObjects.SampleDashboard GetSampleDashboard();
    DataObjects.SampleFileResponse GenerateSampleTextFile();
    DataObjects.SampleFileResponse GenerateSampleCsvExport();
    DataObjects.SampleGraphData GetSampleGraphData();
}

public partial class DataAccess
{
    // In-memory store for sample data. Not persisted to DB — reseeds on app restart.
    private static readonly ConcurrentDictionary<Guid, DataObjects.SampleItem> _sampleItems = new();
    private static bool _sampleDataSeeded = false;

    private void SeedSampleDataIfNeeded()
    {
        if (_sampleDataSeeded) return;
        _sampleDataSeeded = true;

        var categories = new[] { "Engineering", "Marketing", "Operations", "Finance", "Support" };
        var statuses = new[] {
            DataObjects.SampleItemStatus.Draft,
            DataObjects.SampleItemStatus.Active,
            DataObjects.SampleItemStatus.Completed,
            DataObjects.SampleItemStatus.Archived,
        };
        var names = new[] {
            "Website Redesign", "Q4 Budget Review", "Server Migration",
            "Onboarding Workflow", "API Documentation", "Security Audit",
            "Mobile App Launch", "Data Pipeline", "CI/CD Setup",
            "Customer Portal", "Email Templates", "Performance Review",
            "Inventory System", "Compliance Report", "Training Materials",
            "Brand Guidelines", "Help Desk Rollout", "Backup Strategy",
            "Dashboard Widgets", "SSO Integration", "Load Testing",
            "Release Notes", "Vendor Evaluation", "Cost Analysis",
            "Network Diagram",
        };

        var rng = new Random(42); // Deterministic seed for repeatable demo data

        for (int i = 0; i < names.Length; i++) {
            var id = Guid.NewGuid();
            var daysAgo = rng.Next(1, 180);
            var added = DateTime.UtcNow.AddDays(-daysAgo);

            _sampleItems[id] = new DataObjects.SampleItem {
                SampleItemId = id,
                TenantId = Guid.Empty,
                Name = names[i],
                Description = $"Sample description for {names[i]}. This is demo data generated on startup.",
                Category = categories[rng.Next(categories.Length)],
                Status = statuses[rng.Next(statuses.Length)],
                Priority = rng.Next(1, 6),
                Amount = Math.Round((decimal)(rng.NextDouble() * 10000), 2),
                Enabled = rng.NextDouble() > 0.2,
                DueDate = rng.NextDouble() > 0.3 ? DateTime.UtcNow.AddDays(rng.Next(-30, 90)) : null,
                Added = added,
                AddedBy = "System",
                LastModified = added.AddDays(rng.Next(0, Math.Max(1, daysAgo / 2))),
                LastModifiedBy = "System",
            };
        }
    }

    // --- GetMany: null/empty → all (non-deleted), IDs → filtered (includes deleted) ---
    public List<DataObjects.SampleItem> GetSampleItems(List<Guid>? ids)
    {
        SeedSampleDataIfNeeded();

        if (ids != null && ids.Count > 0) {
            return _sampleItems.Values
                .Where(x => ids.Contains(x.SampleItemId))
                .OrderBy(x => x.Name)
                .ToList();
        }

        return _sampleItems.Values.Where(x => !x.Deleted).OrderBy(x => x.Name).ToList();
    }

    // --- SaveMany: PK exists → update, empty/new PK → insert ---
    public List<DataObjects.SampleItem> SaveSampleItems(List<DataObjects.SampleItem> items, DataObjects.User? currentUser)
    {
        SeedSampleDataIfNeeded();

        var saved = new List<DataObjects.SampleItem>();
        string modifiedBy = currentUser?.DisplayName ?? "Unknown";

        foreach (var item in items) {
            if (item.SampleItemId == Guid.Empty) {
                item.SampleItemId = Guid.NewGuid();
            }

            if (_sampleItems.TryGetValue(item.SampleItemId, out var existing)) {
                // Update
                existing.Name = item.Name;
                existing.Description = item.Description;
                existing.Category = item.Category;
                existing.Status = item.Status;
                existing.Priority = item.Priority;
                existing.Amount = item.Amount;
                existing.Enabled = item.Enabled;
                existing.DueDate = item.DueDate;
                existing.LastModified = DateTime.UtcNow;
                existing.LastModifiedBy = modifiedBy;
                saved.Add(existing);
            } else {
                // Insert
                item.Added = DateTime.UtcNow;
                item.AddedBy = modifiedBy;
                item.LastModified = DateTime.UtcNow;
                item.LastModifiedBy = modifiedBy;
                _sampleItems[item.SampleItemId] = item;
                saved.Add(item);
            }
        }

        return saved;
    }

    // --- DeleteMany: soft delete (set Deleted flag) ---
    public DataObjects.BooleanResponse DeleteSampleItems(List<Guid>? ids)
    {
        SeedSampleDataIfNeeded();

        var output = new DataObjects.BooleanResponse();

        if (ids == null || ids.Count == 0) {
            output.Messages.Add("You must provide IDs to delete. Use GetSampleItems first to find them.");
            return output;
        }

        int deleted = 0;
        foreach (var id in ids) {
            if (_sampleItems.TryGetValue(id, out var item)) {
                item.Deleted = true;
                item.DeletedAt = DateTime.UtcNow;
                deleted++;
            }
        }

        output.Result = true;
        output.Messages.Add($"Deleted {deleted} item(s).");
        return output;
    }

    // --- Dashboard aggregate ---
    public DataObjects.SampleDashboard GetSampleDashboard()
    {
        SeedSampleDataIfNeeded();

        var items = _sampleItems.Values.ToList();

        var dashboard = new DataObjects.SampleDashboard {
            TotalItems = items.Count,
            ActiveItems = items.Count(x => x.Status == DataObjects.SampleItemStatus.Active),
            CompletedItems = items.Count(x => x.Status == DataObjects.SampleItemStatus.Completed),
            DraftItems = items.Count(x => x.Status == DataObjects.SampleItemStatus.Draft),
            ArchivedItems = items.Count(x => x.Status == DataObjects.SampleItemStatus.Archived),
        };

        dashboard.ByCategory = items
            .GroupBy(x => x.Category ?? "Uncategorized")
            .Select(g => new DataObjects.SampleCategorySummary {
                Category = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
            })
            .OrderBy(x => x.Category)
            .ToList();

        dashboard.ByMonth = items
            .GroupBy(x => x.Added.ToString("yyyy-MM"))
            .Select(g => new DataObjects.SampleTimelineSummary {
                Month = g.Key,
                Added = g.Count(),
                Completed = g.Count(x => x.Status == DataObjects.SampleItemStatus.Completed),
            })
            .OrderBy(x => x.Month)
            .ToList();

        return dashboard;
    }

    // --- Filtered list with search, sort, pagination ---
    public DataObjects.FilterSampleItems GetSampleItemsFiltered(DataObjects.FilterSampleItems filter)
    {
        SeedSampleDataIfNeeded();

        var output = new DataObjects.FilterSampleItems {
            ActionResponse = GetNewActionResponse(true),
            Sort = filter.Sort ?? "name",
            SortOrder = filter.SortOrder ?? "ASC",
            ShowFilters = filter.ShowFilters,
            Keyword = filter.Keyword,
            Status = filter.Status,
            Category = filter.Category,
            Enabled = filter.Enabled,
            IncludeDeletedItems = filter.IncludeDeletedItems,
        };

        var query = _sampleItems.Values.AsEnumerable();

        // Exclude soft-deleted items unless Include Deleted is checked
        if (!filter.IncludeDeletedItems) {
            query = query.Where(x => !x.Deleted);
        }

        // Enabled filter (All / Enabled Only / Disabled Only)
        if (!string.IsNullOrWhiteSpace(filter.Enabled)) {
            if (filter.Enabled.Equals("enabled", StringComparison.OrdinalIgnoreCase)) {
                query = query.Where(x => x.Enabled);
            } else if (filter.Enabled.Equals("disabled", StringComparison.OrdinalIgnoreCase)) {
                query = query.Where(x => !x.Enabled);
            }
        }

        // Text search
        if (!string.IsNullOrWhiteSpace(filter.Keyword)) {
            string keyword = filter.Keyword.ToLower();
            query = query.Where(x =>
                (x.Name != null && x.Name.ToLower().Contains(keyword)) ||
                (x.Description != null && x.Description.ToLower().Contains(keyword)) ||
                (x.Category != null && x.Category.ToLower().Contains(keyword)));
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<DataObjects.SampleItemStatus>(filter.Status, true, out var statusVal)) {
            query = query.Where(x => x.Status == statusVal);
        }

        // Category filter
        if (!string.IsNullOrWhiteSpace(filter.Category)) {
            query = query.Where(x => x.Category == filter.Category);
        }

        // Collect distinct categories for filter dropdown
        output.AvailableCategories = _sampleItems.Values
            .Select(x => x.Category ?? "")
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        output.RecordCount = query.Count();

        // Sorting
        string sortField = (output.Sort ?? "name").ToLower();
        bool desc = (output.SortOrder ?? "").ToLower() == "desc";

        query = sortField switch {
            "category" => desc ? query.OrderByDescending(x => x.Category) : query.OrderBy(x => x.Category),
            "status" => desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "priority" => desc ? query.OrderByDescending(x => x.Priority) : query.OrderBy(x => x.Priority),
            "amount" => desc ? query.OrderByDescending(x => x.Amount) : query.OrderBy(x => x.Amount),
            "enabled" => desc ? query.OrderByDescending(x => x.Enabled) : query.OrderBy(x => x.Enabled),
            "added" => desc ? query.OrderByDescending(x => x.Added) : query.OrderBy(x => x.Added),
            "duedate" => desc ? query.OrderByDescending(x => x.DueDate) : query.OrderBy(x => x.DueDate),
            _ => desc ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
        };

        // Pagination
        int page = filter.Page > 0 ? filter.Page : 1;
        int pageSize = filter.RecordsPerPage > 0 ? filter.RecordsPerPage : 10;
        output.Page = page;
        output.RecordsPerPage = pageSize;
        output.PageCount = (int)Math.Ceiling((double)output.RecordCount / pageSize);

        output.Records = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Column definitions for PagedRecordset component
        output.Columns = new List<DataObjects.FilterColumn> {
            new() { Label = "Name", DataElementName = "Name", Sortable = true },
            new() { Label = "Category", DataElementName = "Category", Sortable = true },
            new() { Label = "Status", DataElementName = "Status", Sortable = true },
            new() { Label = "Priority", DataElementName = "Priority", Sortable = true, Align = "center" },
            new() { Label = "Amount", DataElementName = "Amount", Sortable = true, Align = "right", DataType = "decimal" },
            new() { Label = "Enabled", DataElementName = "Enabled", Sortable = true, Align = "center", BooleanIcon = "icon fa-regular fa-square-check" },
        };

        return output;
    }

    // --- Server-side file generation ---
    public DataObjects.SampleFileResponse GenerateSampleTextFile()
    {
        SeedSampleDataIfNeeded();

        var items = _sampleItems.Values.OrderBy(x => x.Name).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("FreeExamples Sample Data Report");
        sb.AppendLine($"Generated: {DateTime.UtcNow:O}");
        sb.AppendLine($"Total Items: {items.Count}");
        sb.AppendLine(new string('-', 60));

        foreach (var item in items) {
            sb.AppendLine($"  {item.Name} | {item.Category} | {item.Status} | ${item.Amount:N2}");
        }

        return new DataObjects.SampleFileResponse {
            FileName = "SampleReport_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".txt",
            FileData = System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
        };
    }

    public DataObjects.SampleFileResponse GenerateSampleCsvExport()
    {
        SeedSampleDataIfNeeded();

        var items = _sampleItems.Values.OrderBy(x => x.Name).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Name,Category,Status,Priority,Amount,Enabled,DueDate,Added");

        foreach (var item in items) {
            sb.AppendLine($"\"{item.Name}\",\"{item.Category}\",\"{item.Status}\",{item.Priority},{item.Amount},{item.Enabled},{item.DueDate?.ToString("yyyy-MM-dd") ?? ""},{item.Added:yyyy-MM-dd}");
        }

        return new DataObjects.SampleFileResponse {
            FileName = "SampleItems_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".csv",
            FileData = System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
        };
    }

    // --- Network graph data from sample items ---
    public DataObjects.SampleGraphData GetSampleGraphData()
    {
        SeedSampleDataIfNeeded();

        var items = _sampleItems.Values.ToList();
        var categories = items.Select(x => x.Category ?? "Uncategorized").Distinct().OrderBy(x => x).ToList();

        var graphData = new DataObjects.SampleGraphData();

        // Category nodes (ids 1-N)
        for (int i = 0; i < categories.Count; i++) {
            graphData.Nodes.Add(new DataObjects.SampleGraphNode {
                Id = i + 1,
                Label = categories[i],
                Group = "category",
            });
        }

        // Item nodes (ids 100+)
        int nodeId = 100;
        foreach (var item in items) {
            graphData.Nodes.Add(new DataObjects.SampleGraphNode {
                Id = nodeId,
                Label = item.Name,
                Group = item.Status.ToString().ToLower(),
            });

            // Edge from category → item
            int catIndex = categories.IndexOf(item.Category ?? "Uncategorized");
            graphData.Edges.Add(new DataObjects.SampleGraphEdge {
                From = catIndex + 1,
                To = nodeId,
                Label = item.Status.ToString(),
            });

            nodeId++;
        }

        // Cross-link items with same priority
        var byPriority = items.GroupBy(x => x.Priority).Where(g => g.Count() > 1);
        foreach (var group in byPriority) {
            var groupItems = group.ToList();
            for (int i = 0; i < groupItems.Count - 1 && i < 2; i++) {
                int fromId = 100 + items.IndexOf(groupItems[i]);
                int toId = 100 + items.IndexOf(groupItems[i + 1]);
                graphData.Edges.Add(new DataObjects.SampleGraphEdge {
                    From = fromId,
                    To = toId,
                    Label = $"P{group.Key}",
                });
            }
        }

        return graphData;
    }
}
