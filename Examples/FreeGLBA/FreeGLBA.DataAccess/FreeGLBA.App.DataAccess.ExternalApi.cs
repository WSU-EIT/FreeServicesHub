using Microsoft.EntityFrameworkCore;

namespace FreeGLBA;

// ============================================================================
// GLBA EXTERNAL API DATA ACCESS
// ============================================================================

/// <summary>Glba External API interface extensions.</summary>
public partial interface IDataAccess
{
    /// <summary>Process a single event from external source.</summary>
    Task<DataObjects.GlbaEventResponse> ProcessGlbaEventAsync(DataObjects.GlbaEventRequest request, Guid sourceSystemId);

    /// <summary>Process a batch of events from external source.</summary>
    Task<DataObjects.GlbaBatchResponse> ProcessGlbaBatchAsync(List<DataObjects.GlbaEventRequest> requests, Guid sourceSystemId);

    /// <summary>Get dashboard statistics.</summary>
    Task<DataObjects.GlbaStats> GetGlbaStatsAsync();

    /// <summary>Get recent events for dashboard feed.</summary>
    Task<List<DataObjects.AccessEvent>> GetRecentAccessEventsAsync(int limit = 50);

    /// <summary>Get access events for a specific subject by external ID.</summary>
    Task<List<DataObjects.AccessEvent>> GetAccessEventsBySubjectAsync(string subjectId, int limit = 100);

    /// <summary>Get accessor (user) statistics with filtering and pagination.</summary>
    Task<DataObjects.AccessorFilterResult> GetAccessorsAsync(DataObjects.AccessorFilter filter);

    /// <summary>Get top accessors for dashboard display.</summary>
    Task<List<DataObjects.AccessorSummary>> GetTopAccessorsAsync(int limit = 10);
}

public partial class DataAccess
{
    #region Glba External API

    /// <summary>Process a single event from external source.</summary>
    public async Task<DataObjects.GlbaEventResponse> ProcessGlbaEventAsync(
        DataObjects.GlbaEventRequest request, Guid sourceSystemId)
    {
        var response = new DataObjects.GlbaEventResponse
        {
            ReceivedAt = DateTime.UtcNow
        };

        // Validation - UserId and AccessType are required
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            response.Status = "error";
            response.Message = "Missing required field: UserId";
            return response;
        }

        if (string.IsNullOrWhiteSpace(request.AccessType))
        {
            response.Status = "error";
            response.Message = "Missing required field: AccessType";
            return response;
        }

        // Validation - SubjectId is optional for general audit logging
        // If no SubjectId provided, use "SYSTEM" as a placeholder
        var hasSubject = !string.IsNullOrWhiteSpace(request.SubjectId);
        var hasBulkSubjects = request.SubjectIds?.Any(s => !string.IsNullOrWhiteSpace(s)) == true;
        
        if (!hasSubject && !hasBulkSubjects)
        {
            // General audit log - no specific data subject
            request.SubjectId = "SYSTEM";
        }

        // Deduplication check
        if (!string.IsNullOrEmpty(request.SourceEventId))
        {
            var exists = await data.AccessEvents.AnyAsync(x =>
                x.SourceSystemId == sourceSystemId &&
                x.SourceEventId == request.SourceEventId);

            if (exists)
            {
                response.Status = "duplicate";
                response.Message = "Event with this SourceEventId already exists";
                return response;
            }
        }

        // Handle bulk subjects - calculate count and serialize IDs
        var subjectIdList = request.SubjectIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        hasBulkSubjects = subjectIdList?.Count > 0;
        var subjectCount = hasBulkSubjects ? subjectIdList!.Count : (hasSubject ? 1 : 0);
        var subjectIdsJson = hasBulkSubjects ? System.Text.Json.JsonSerializer.Serialize(subjectIdList) : string.Empty;
        var primarySubjectId = hasBulkSubjects 
            ? (subjectIdList!.Count > 1 ? "BULK" : subjectIdList[0])
            : request.SubjectId;

        // Ensure dates are UTC
        var accessedAtUtc = request.AccessedAt.Kind == DateTimeKind.Utc 
            ? request.AccessedAt 
            : DateTime.SpecifyKind(request.AccessedAt, DateTimeKind.Utc);
        var agreementAtUtc = request.AgreementAcknowledgedAt.HasValue
            ? (request.AgreementAcknowledgedAt.Value.Kind == DateTimeKind.Utc 
                ? request.AgreementAcknowledgedAt.Value 
                : DateTime.SpecifyKind(request.AgreementAcknowledgedAt.Value, DateTimeKind.Utc))
            : accessedAtUtc;

        // Create event record - ensure all strings are never null
        var evt = new EFModels.EFModels.AccessEventItem
        {
            AccessEventId = Guid.NewGuid(),
            SourceSystemId = sourceSystemId,
            ReceivedAt = DateTime.UtcNow,
            SourceEventId = (request.SourceEventId ?? string.Empty).Trim(),
            AccessedAt = accessedAtUtc,
            UserId = (request.UserId ?? string.Empty).Trim(),
            UserName = (request.UserName ?? string.Empty).Trim(),
            UserEmail = (request.UserEmail ?? string.Empty).Trim(),
            UserDepartment = (request.UserDepartment ?? string.Empty).Trim(),
            SubjectId = (primarySubjectId ?? string.Empty).Trim(),
            SubjectType = (request.SubjectType ?? string.Empty).Trim(),
            SubjectIds = subjectIdsJson ?? "[]",
            SubjectCount = subjectCount,
            DataCategory = (request.DataCategory ?? string.Empty).Trim(),
            AccessType = (request.AccessType ?? string.Empty).Trim(),
            Purpose = (request.Purpose ?? string.Empty).Trim(),
            IpAddress = (request.IpAddress ?? string.Empty).Trim(),
            AdditionalData = string.IsNullOrWhiteSpace(request.AdditionalData) ? "{}" : request.AdditionalData.Trim(),
            AgreementText = (request.AgreementText ?? string.Empty).Trim(),
            AgreementAcknowledgedAt = agreementAtUtc,
        };

        data.AccessEvents.Add(evt);
        await data.SaveChangesAsync();

        // Update LastEventReceivedAt on source system (works with all providers including InMemory)
        var sourceSystem = await data.SourceSystems.FindAsync(sourceSystemId);
        if (sourceSystem != null) {
            sourceSystem.LastEventReceivedAt = DateTime.UtcNow;
            await data.SaveChangesAsync();
        }

        // Update DataSubject stats - handle bulk or single
        // Skip for SYSTEM subjects (general audit logs without a specific data subject)
        if (hasBulkSubjects) {
            await UpdateDataSubjectStatsAsync(subjectIdList!, request.SubjectType);
        } else if (hasSubject && request.SubjectId != "SYSTEM") {
            await UpdateDataSubjectStatsAsync(request.SubjectId, request.SubjectType);
        }

        response.EventId = evt.AccessEventId;
        response.Status = "accepted";
        response.SubjectCount = subjectCount;
        return response;
    }

    /// <summary>Process a batch of events from external source.</summary>
    public async Task<DataObjects.GlbaBatchResponse> ProcessGlbaBatchAsync(
        List<DataObjects.GlbaEventRequest> requests, Guid sourceSystemId)
    {
        var response = new DataObjects.GlbaBatchResponse();

        for (int i = 0; i < requests.Count; i++)
        {
            try
            {
                var result = await ProcessGlbaEventAsync(requests[i], sourceSystemId);
                switch (result.Status)
                {
                    case "accepted": response.Accepted++; break;
                    case "duplicate": response.Duplicate++; break;
                    default:
                        response.Rejected++;
                        response.Errors.Add(new DataObjects.GlbaBatchError { Index = i, Error = result.Message ?? "Unknown error" });
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Rejected++;
                response.Errors.Add(new DataObjects.GlbaBatchError { Index = i, Error = ex.Message });
            }
        }

        return response;
    }

    /// <summary>Get dashboard statistics.</summary>
    public async Task<DataObjects.GlbaStats> GetGlbaStatsAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = now.Date.AddDays(-(int)now.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1);

        // Event counts
        var eventsToday = await data.AccessEvents.CountAsync(x => x.AccessedAt >= todayStart);
        var eventsThisWeek = await data.AccessEvents.CountAsync(x => x.AccessedAt >= weekStart);
        var eventsThisMonth = await data.AccessEvents.CountAsync(x => x.AccessedAt >= monthStart);

        // Subject counts - subjects accessed in each period
        var subjectsToday = await data.DataSubjects.CountAsync(x => x.LastAccessedAt >= todayStart);
        var subjectsThisWeek = await data.DataSubjects.CountAsync(x => x.LastAccessedAt >= weekStart);
        var subjectsThisMonth = await data.DataSubjects.CountAsync(x => x.LastAccessedAt >= monthStart);
        var totalSubjects = await data.DataSubjects.CountAsync();

        return new DataObjects.GlbaStats
        {
            Today = eventsToday,
            ThisWeek = eventsThisWeek,
            ThisMonth = eventsThisMonth,
            TotalSubjects = totalSubjects,
            SubjectsToday = subjectsToday,
            SubjectsThisWeek = subjectsThisWeek,
            SubjectsThisMonth = subjectsThisMonth,
        };
    }

    /// <summary>Get recent events for dashboard feed.</summary>
    public async Task<List<DataObjects.AccessEvent>> GetRecentAccessEventsAsync(int limit = 50)
    {
        return await data.AccessEvents
            .OrderByDescending(x => x.AccessedAt)
            .Take(limit)
            .Select(x => new DataObjects.AccessEvent
            {
                AccessEventId = x.AccessEventId,
                SourceSystemId = x.SourceSystemId,
                SourceEventId = x.SourceEventId,
                AccessedAt = x.AccessedAt,
                ReceivedAt = x.ReceivedAt,
                UserId = x.UserId,
                UserName = x.UserName,
                UserEmail = x.UserEmail,
                UserDepartment = x.UserDepartment,
                SubjectId = x.SubjectId,
                SubjectType = x.SubjectType,
                SubjectIds = x.SubjectIds,
                SubjectCount = x.SubjectCount,
                DataCategory = x.DataCategory,
                AccessType = x.AccessType,
                Purpose = x.Purpose,
                IpAddress = x.IpAddress,
                AdditionalData = x.AdditionalData,
            })
            .ToListAsync();
    }

    /// <summary>Get access events for a specific subject by external ID.</summary>
    public async Task<List<DataObjects.AccessEvent>> GetAccessEventsBySubjectAsync(string subjectId, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(subjectId)) return new List<DataObjects.AccessEvent>();

        // Search both direct SubjectId match AND in SubjectIds JSON array (for bulk events)
        return await data.AccessEvents
            .Where(x => x.SubjectId == subjectId || x.SubjectIds.Contains(subjectId))
            .OrderByDescending(x => x.AccessedAt)
            .Take(limit)
            .Select(x => new DataObjects.AccessEvent
            {
                AccessEventId = x.AccessEventId,
                SourceSystemId = x.SourceSystemId,
                SourceEventId = x.SourceEventId,
                AccessedAt = x.AccessedAt,
                ReceivedAt = x.ReceivedAt,
                UserId = x.UserId,
                UserName = x.UserName,
                UserEmail = x.UserEmail,
                UserDepartment = x.UserDepartment,
                SubjectId = x.SubjectId,
                SubjectType = x.SubjectType,
                SubjectIds = x.SubjectIds,
                SubjectCount = x.SubjectCount,
                DataCategory = x.DataCategory,
                AccessType = x.AccessType,
                Purpose = x.Purpose,
                IpAddress = x.IpAddress,
                AdditionalData = x.AdditionalData,
            })
            .ToListAsync();
    }

    /// <summary>Update or create DataSubject stats on event.</summary>
    private async Task UpdateDataSubjectStatsAsync(string subjectId, string? subjectType = null)
    {
        if (string.IsNullOrWhiteSpace(subjectId)) return;

        var subject = await data.DataSubjects
            .FirstOrDefaultAsync(x => x.ExternalId == subjectId);

        if (subject == null) {
            subject = new EFModels.EFModels.DataSubjectItem
            {
                DataSubjectId = Guid.NewGuid(),
                ExternalId = subjectId,
                SubjectType = subjectType ?? "Student",
                FirstAccessedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                TotalAccessCount = 1,
                UniqueAccessorCount = 1
            };
            data.DataSubjects.Add(subject);
        } else {
            subject.LastAccessedAt = DateTime.UtcNow;
            subject.TotalAccessCount++;
            // Update SubjectType if provided and currently empty
            if (!string.IsNullOrEmpty(subjectType) && string.IsNullOrEmpty(subject.SubjectType)) {
                subject.SubjectType = subjectType;
            }
        }

        await data.SaveChangesAsync();
    }

    /// <summary>Update or create DataSubject stats for multiple subjects (bulk access).</summary>
    private async Task UpdateDataSubjectStatsAsync(IEnumerable<string> subjectIds, string? subjectType = null)
    {
        if (subjectIds == null) return;

        var distinctIds = subjectIds.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (distinctIds.Count == 0) return;

        // Get existing subjects
        var existingSubjects = await data.DataSubjects
            .Where(x => distinctIds.Contains(x.ExternalId))
            .ToDictionaryAsync(x => x.ExternalId);

        foreach (var subjectId in distinctIds) {
            if (existingSubjects.TryGetValue(subjectId, out var subject)) {
                subject.LastAccessedAt = DateTime.UtcNow;
                subject.TotalAccessCount++;
                if (!string.IsNullOrEmpty(subjectType) && string.IsNullOrEmpty(subject.SubjectType)) {
                    subject.SubjectType = subjectType;
                }
            } else {
                var newSubject = new EFModels.EFModels.DataSubjectItem
                {
                    DataSubjectId = Guid.NewGuid(),
                    ExternalId = subjectId,
                    SubjectType = subjectType ?? "Student",
                    FirstAccessedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    TotalAccessCount = 1,
                    UniqueAccessorCount = 1
                };
                data.DataSubjects.Add(newSubject);
            }
        }

        await data.SaveChangesAsync();
    }

    /// <summary>Get accessor (user) statistics with filtering and pagination.</summary>
    public async Task<DataObjects.AccessorFilterResult> GetAccessorsAsync(DataObjects.AccessorFilter filter)
    {
        // Group access events by UserId to get accessor stats
        var query = data.AccessEvents
            .AsNoTracking()
            .GroupBy(x => x.UserId)
            .Select(g => new DataObjects.AccessorSummary
            {
                UserId = g.Key,
                UserName = g.OrderByDescending(x => x.AccessedAt).Select(x => x.UserName).FirstOrDefault(),
                UserEmail = g.OrderByDescending(x => x.AccessedAt).Select(x => x.UserEmail).FirstOrDefault(),
                UserDepartment = g.OrderByDescending(x => x.AccessedAt).Select(x => x.UserDepartment).FirstOrDefault(),
                TotalAccesses = g.Count(),
                UniqueSubjectsAccessed = g.Select(x => x.SubjectId).Distinct().Count(),
                ExportCount = g.Count(x => x.AccessType == "Export" || x.AccessType == "Download"),
                ViewCount = g.Count(x => x.AccessType == "View" || x.AccessType == "Query"),
                FirstAccessAt = g.Min(x => x.AccessedAt),
                LastAccessAt = g.Max(x => x.AccessedAt)
            });

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(x => x.UserId.ToLower().Contains(search) ||
                                     (x.UserName != null && x.UserName.ToLower().Contains(search)) ||
                                     (x.UserEmail != null && x.UserEmail.ToLower().Contains(search)) ||
                                     (x.UserDepartment != null && x.UserDepartment.ToLower().Contains(search)));
        }

        // Apply department filter
        if (!string.IsNullOrWhiteSpace(filter.Department))
        {
            query = query.Where(x => x.UserDepartment == filter.Department);
        }

        // Apply advanced filters
        if (filter.MinTotalAccesses.HasValue)
            query = query.Where(x => x.TotalAccesses >= filter.MinTotalAccesses.Value);
        if (filter.MaxTotalAccesses.HasValue)
            query = query.Where(x => x.TotalAccesses <= filter.MaxTotalAccesses.Value);
        if (filter.MinUniqueSubjects.HasValue)
            query = query.Where(x => x.UniqueSubjectsAccessed >= filter.MinUniqueSubjects.Value);
        if (filter.MaxUniqueSubjects.HasValue)
            query = query.Where(x => x.UniqueSubjectsAccessed <= filter.MaxUniqueSubjects.Value);
        if (filter.MinExportCount.HasValue)
            query = query.Where(x => x.ExportCount >= filter.MinExportCount.Value);
        if (filter.MaxExportCount.HasValue)
            query = query.Where(x => x.ExportCount <= filter.MaxExportCount.Value);
        if (filter.MinViewCount.HasValue)
            query = query.Where(x => x.ViewCount >= filter.MinViewCount.Value);
        if (filter.MaxViewCount.HasValue)
            query = query.Where(x => x.ViewCount <= filter.MaxViewCount.Value);
        if (filter.LastAccessAfter.HasValue)
            query = query.Where(x => x.LastAccessAt >= filter.LastAccessAfter.Value);
        if (filter.LastAccessBefore.HasValue)
            query = query.Where(x => x.LastAccessAt <= filter.LastAccessBefore.Value);

        var total = await query.CountAsync();

        // Apply sorting
        query = filter.SortColumn switch
        {
            "UserId" => filter.SortDescending ? query.OrderByDescending(x => x.UserId) : query.OrderBy(x => x.UserId),
            "UserName" => filter.SortDescending ? query.OrderByDescending(x => x.UserName) : query.OrderBy(x => x.UserName),
            "UserDepartment" => filter.SortDescending ? query.OrderByDescending(x => x.UserDepartment) : query.OrderBy(x => x.UserDepartment),
            "TotalAccesses" => filter.SortDescending ? query.OrderByDescending(x => x.TotalAccesses) : query.OrderBy(x => x.TotalAccesses),
            "UniqueSubjectsAccessed" => filter.SortDescending ? query.OrderByDescending(x => x.UniqueSubjectsAccessed) : query.OrderBy(x => x.UniqueSubjectsAccessed),
            "ExportCount" => filter.SortDescending ? query.OrderByDescending(x => x.ExportCount) : query.OrderBy(x => x.ExportCount),
            "ViewCount" => filter.SortDescending ? query.OrderByDescending(x => x.ViewCount) : query.OrderBy(x => x.ViewCount),
            "LastAccessAt" => filter.SortDescending ? query.OrderByDescending(x => x.LastAccessAt) : query.OrderBy(x => x.LastAccessAt),
            _ => query.OrderByDescending(x => x.TotalAccesses) // Default: most active first
        };

        // Apply pagination
        var records = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new DataObjects.AccessorFilterResult
        {
            Records = records,
            TotalRecords = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    /// <summary>Get top accessors for dashboard display.</summary>
    public async Task<List<DataObjects.AccessorSummary>> GetTopAccessorsAsync(int limit = 10)
    {
        return await data.AccessEvents
            .AsNoTracking()
            .GroupBy(x => x.UserId)
            .Select(g => new DataObjects.AccessorSummary
            {
                UserId = g.Key,
                UserName = g.OrderByDescending(x => x.AccessedAt).Select(x => x.UserName).FirstOrDefault(),
                UserDepartment = g.OrderByDescending(x => x.AccessedAt).Select(x => x.UserDepartment).FirstOrDefault(),
                TotalAccesses = g.Count(),
                UniqueSubjectsAccessed = g.Select(x => x.SubjectId).Distinct().Count(),
                ExportCount = g.Count(x => x.AccessType == "Export" || x.AccessType == "Download"),
                LastAccessAt = g.Max(x => x.AccessedAt)
            })
            .OrderByDescending(x => x.TotalAccesses)
            .Take(limit)
            .ToListAsync();
    }

    #endregion
}
