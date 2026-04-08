using Microsoft.EntityFrameworkCore;

namespace FreeGLBA;

// ============================================================================
// FREEGLBA PROJECT DATA ACCESS
// ============================================================================

// SourceSystem Data Access Methods
public partial class DataAccess
{
    #region SourceSystem

    public async Task<DataObjects.SourceSystemFilterResult> GetSourceSystemsAsync(DataObjects.SourceSystemFilter filter)
    {
        var query = data.SourceSystems
            .AsQueryable();

        if (filter.IsActiveFilter.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.IsActiveFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(x => x.Name.Contains(filter.Search) || x.DisplayName.Contains(filter.Search) || x.ContactEmail.Contains(filter.Search));
        }

        // Apply advanced filters
        if (filter.LastActivityAfter.HasValue)
            query = query.Where(x => x.LastEventReceivedAt >= filter.LastActivityAfter.Value);
        if (filter.LastActivityBefore.HasValue)
            query = query.Where(x => x.LastEventReceivedAt <= filter.LastActivityBefore.Value);

        var total = await query.CountAsync();

        query = filter.SortColumn switch
        {
            "Name" => filter.SortDescending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
            "DisplayName" => filter.SortDescending ? query.OrderByDescending(x => x.DisplayName) : query.OrderBy(x => x.DisplayName),
            "ContactEmail" => filter.SortDescending ? query.OrderByDescending(x => x.ContactEmail) : query.OrderBy(x => x.ContactEmail),
            "IsActive" => filter.SortDescending ? query.OrderByDescending(x => x.IsActive) : query.OrderBy(x => x.IsActive),
            "LastEventReceivedAt" => filter.SortDescending ? query.OrderByDescending(x => x.LastEventReceivedAt) : query.OrderBy(x => x.LastEventReceivedAt),
            _ => query.OrderByDescending(x => x.SourceSystemId)
        };

        var items = await query.Skip(filter.Skip).Take(filter.PageSize).ToListAsync();

        // Get event counts via query (computed, not stored)
        var sourceSystemIds = items.Select(x => x.SourceSystemId).ToList();
        var eventCounts = await data.AccessEvents
            .Where(x => sourceSystemIds.Contains(x.SourceSystemId))
            .GroupBy(x => x.SourceSystemId)
            .Select(g => new { SourceSystemId = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(x => x.SourceSystemId, x => x.Count);

        // Apply event count filters after getting counts (post-filter since it's computed)
        var records = items.Select(x => new DataObjects.SourceSystem
        {
            SourceSystemId = x.SourceSystemId,
            Name = x.Name,
            DisplayName = x.DisplayName,
            ApiKey = x.ApiKey,
            ContactEmail = x.ContactEmail,
            IsActive = x.IsActive,
            LastEventReceivedAt = x.LastEventReceivedAt,
            EventCount = eventCounts.GetValueOrDefault(x.SourceSystemId, 0),
        }).ToList();

        // Apply event count filters
        if (filter.MinEventCount.HasValue)
            records = records.Where(x => x.EventCount >= filter.MinEventCount.Value).ToList();
        if (filter.MaxEventCount.HasValue)
            records = records.Where(x => x.EventCount <= filter.MaxEventCount.Value).ToList();

        return new DataObjects.SourceSystemFilterResult
        {
            Records = records,
            TotalRecords = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<DataObjects.SourceSystem?> GetSourceSystemAsync(Guid id)
    {
        var item = await data.SourceSystems
            .FirstOrDefaultAsync(x => x.SourceSystemId == id);
        if (item == null) return null;

        // Get event count via query
        var eventCount = await data.AccessEvents.CountAsync(x => x.SourceSystemId == id);

        return new DataObjects.SourceSystem
        {
            SourceSystemId = item.SourceSystemId,
            Name = item.Name,
            DisplayName = item.DisplayName,
            ApiKey = item.ApiKey,
            ContactEmail = item.ContactEmail,
            IsActive = item.IsActive,
            LastEventReceivedAt = item.LastEventReceivedAt,
            EventCount = eventCount,
        };
    }

    public async Task<DataObjects.SourceSystem?> SaveSourceSystemAsync(DataObjects.SourceSystem dto)
    {
        EFModels.EFModels.SourceSystemItem item;
        var isNew = dto.SourceSystemId == default;
        string? newPlaintextKey = null;

        if (isNew) {
            item = new EFModels.EFModels.SourceSystemItem();
            item.SourceSystemId = Guid.NewGuid();
            data.SourceSystems.Add(item);
        } else {
            item = await data.SourceSystems.FindAsync(dto.SourceSystemId);
            if (item == null) return null;
        }

        // Only update user-editable fields
        item.Name = dto.Name;
        item.DisplayName = dto.DisplayName;
        item.ContactEmail = dto.ContactEmail;
        item.IsActive = dto.IsActive;

        // Generate API key if new or if regeneration requested
        if (isNew || dto.ApiKey == "REGENERATE") {
            newPlaintextKey = GenerateApiKey();
            item.ApiKey = HashApiKey(newPlaintextKey); // Store the hash, not the plaintext
        }

        await data.SaveChangesAsync();
        
        // Get event count via query for return value
        var eventCount = await data.AccessEvents.CountAsync(x => x.SourceSystemId == item.SourceSystemId);
        
        // Return the updated DTO with the generated values
        dto.SourceSystemId = item.SourceSystemId;
        dto.ApiKey = item.ApiKey; // This is the hash (for display masking)
        dto.NewApiKey = newPlaintextKey; // This is the plaintext key (show ONCE to user)
        dto.EventCount = eventCount;
        dto.LastEventReceivedAt = item.LastEventReceivedAt;
        return dto;
    }

    /// <summary>
    /// Generates a secure random API key for source system authentication.
    /// </summary>
    private string GenerateApiKey()
    {
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[64]; // 64 bytes = 512 bits = equivalent to 4 GUIDs
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public async Task<bool> DeleteSourceSystemAsync(Guid id)
    {
        var item = await data.SourceSystems.FindAsync(id);
        if (item == null) return false;
        data.SourceSystems.Remove(item);
        await data.SaveChangesAsync();
        return true;
    }

    public async Task<List<DataObjects.SourceSystemLookup>> GetSourceSystemLookupsAsync()
    {
        return await data.SourceSystems
            .Select(x => new DataObjects.SourceSystemLookup
            {
                SourceSystemId = x.SourceSystemId,
                DisplayName = x.Name
            })
            .ToListAsync();
    }

    #endregion
}


// AccessEvent Data Access Methods
public partial class DataAccess
{
    #region AccessEvent

    public async Task<DataObjects.AccessEventFilterResult> GetAccessEventsAsync(DataObjects.AccessEventFilter filter)
    {
        var query = data.AccessEvents
            .AsNoTracking()
            .AsQueryable();

        if (filter.SourceSystemIdFilter != default)
            query = query.Where(x => x.SourceSystemId == filter.SourceSystemIdFilter);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(x => x.SourceEventId.Contains(filter.Search) || 
                                     x.UserId.Contains(filter.Search) || 
                                     x.UserName.Contains(filter.Search) ||
                                     x.SubjectId.Contains(filter.Search) ||
                                     x.Purpose.Contains(filter.Search));
        }

        // Apply advanced filters
        if (filter.AccessedAfter.HasValue)
            query = query.Where(x => x.AccessedAt >= filter.AccessedAfter.Value);
        if (filter.AccessedBefore.HasValue)
            query = query.Where(x => x.AccessedAt <= filter.AccessedBefore.Value);
        if (!string.IsNullOrWhiteSpace(filter.UserIdFilter))
            query = query.Where(x => x.UserId.Contains(filter.UserIdFilter));
        if (!string.IsNullOrWhiteSpace(filter.SubjectIdFilter))
            query = query.Where(x => x.SubjectId.Contains(filter.SubjectIdFilter));
        if (!string.IsNullOrWhiteSpace(filter.AccessTypeFilter))
            query = query.Where(x => x.AccessType == filter.AccessTypeFilter);
        if (!string.IsNullOrWhiteSpace(filter.DataCategoryFilter))
            query = query.Where(x => x.DataCategory == filter.DataCategoryFilter);
        if (!string.IsNullOrWhiteSpace(filter.DepartmentFilter))
            query = query.Where(x => x.UserDepartment == filter.DepartmentFilter);

        var total = await query.CountAsync();

        query = filter.SortColumn switch
        {
            "SourceSystemName" => filter.SortDescending ? query.OrderByDescending(x => x.SourceSystem.Name) : query.OrderBy(x => x.SourceSystem.Name),
            "SourceEventId" => filter.SortDescending ? query.OrderByDescending(x => x.SourceEventId) : query.OrderBy(x => x.SourceEventId),
            "AccessedAt" => filter.SortDescending ? query.OrderByDescending(x => x.AccessedAt) : query.OrderBy(x => x.AccessedAt),
            "ReceivedAt" => filter.SortDescending ? query.OrderByDescending(x => x.ReceivedAt) : query.OrderBy(x => x.ReceivedAt),
            "UserId" => filter.SortDescending ? query.OrderByDescending(x => x.UserId) : query.OrderBy(x => x.UserId),
            "SubjectId" => filter.SortDescending ? query.OrderByDescending(x => x.SubjectId) : query.OrderBy(x => x.SubjectId),
            "AccessType" => filter.SortDescending ? query.OrderByDescending(x => x.AccessType) : query.OrderBy(x => x.AccessType),
            _ => query.OrderByDescending(x => x.AccessedAt)
        };

        var items = await query.Skip(filter.Skip).Take(filter.PageSize).ToListAsync();

        // Get source system names separately to avoid join issues
        var sourceSystemIds = items.Select(x => x.SourceSystemId).Distinct().ToList();
        var sourceSystemNames = await data.SourceSystems
            .Where(x => sourceSystemIds.Contains(x.SourceSystemId))
            .ToDictionaryAsync(x => x.SourceSystemId, x => x.Name);

        return new DataObjects.AccessEventFilterResult
        {
            Records = items.Select(x => new DataObjects.AccessEvent
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
                AgreementText = x.AgreementText,
                AgreementAcknowledgedAt = x.AgreementAcknowledgedAt,
                SourceSystemName = sourceSystemNames.GetValueOrDefault(x.SourceSystemId, string.Empty),
            }).ToList(),
            TotalRecords = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<DataObjects.AccessEvent?> GetAccessEventAsync(Guid id)
    {
        var item = await data.AccessEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AccessEventId == id);
        if (item == null) return null;

        // Get source system name separately
        string sourceSystemName = string.Empty;
        if (item.SourceSystemId != default) {
            var sourceSystem = await data.SourceSystems.FirstOrDefaultAsync(x => x.SourceSystemId == item.SourceSystemId);
            sourceSystemName = sourceSystem?.Name ?? string.Empty;
        }

        return new DataObjects.AccessEvent
        {
            AccessEventId = item.AccessEventId,
            SourceSystemId = item.SourceSystemId,
            SourceEventId = item.SourceEventId,
            AccessedAt = item.AccessedAt,
            ReceivedAt = item.ReceivedAt,
            UserId = item.UserId,
            UserName = item.UserName,
            UserEmail = item.UserEmail,
            UserDepartment = item.UserDepartment,
            SubjectId = item.SubjectId,
            SubjectType = item.SubjectType,
            SubjectIds = item.SubjectIds,
            SubjectCount = item.SubjectCount,
            DataCategory = item.DataCategory,
            AccessType = item.AccessType,
            Purpose = item.Purpose,
            IpAddress = item.IpAddress,
            AdditionalData = item.AdditionalData,
            AgreementText = item.AgreementText,
            AgreementAcknowledgedAt = item.AgreementAcknowledgedAt,
            SourceSystemName = sourceSystemName,
        };
    }

    public async Task<DataObjects.AccessEvent?> SaveAccessEventAsync(DataObjects.AccessEvent dto)
    {
        EFModels.EFModels.AccessEventItem item;
        var isNew = dto.AccessEventId == default;

        if (isNew) {
            item = new EFModels.EFModels.AccessEventItem();
            item.AccessEventId = Guid.NewGuid();
            item.ReceivedAt = DateTime.UtcNow;
            data.AccessEvents.Add(item);
        } else {
            item = await data.AccessEvents.FindAsync(dto.AccessEventId);
            if (item == null) return null;
        }

        item.SourceSystemId = dto.SourceSystemId;
        item.SourceEventId = dto.SourceEventId ?? string.Empty;
        // Ensure AccessedAt is stored as UTC
        item.AccessedAt = dto.AccessedAt.Kind == DateTimeKind.Utc 
            ? dto.AccessedAt 
            : DateTime.SpecifyKind(dto.AccessedAt, DateTimeKind.Utc);
        item.UserId = dto.UserId ?? string.Empty;
        item.UserName = dto.UserName ?? string.Empty;
        item.UserEmail = dto.UserEmail ?? string.Empty;
        item.UserDepartment = dto.UserDepartment ?? string.Empty;
        item.SubjectId = dto.SubjectId ?? string.Empty;
        item.SubjectType = dto.SubjectType ?? string.Empty;
        item.SubjectIds = dto.SubjectIds ?? string.Empty;
        item.SubjectCount = dto.SubjectCount > 0 ? dto.SubjectCount : 1;
        item.DataCategory = dto.DataCategory ?? string.Empty;
        item.AccessType = dto.AccessType ?? string.Empty;
        item.Purpose = dto.Purpose ?? string.Empty;
        item.IpAddress = dto.IpAddress ?? string.Empty;
        item.AdditionalData = dto.AdditionalData ?? string.Empty;
        item.AgreementText = dto.AgreementText ?? string.Empty;
        // Ensure AgreementAcknowledgedAt is stored as UTC
        item.AgreementAcknowledgedAt = dto.AgreementAcknowledgedAt.HasValue
            ? (dto.AgreementAcknowledgedAt.Value.Kind == DateTimeKind.Utc 
                ? dto.AgreementAcknowledgedAt.Value 
                : DateTime.SpecifyKind(dto.AgreementAcknowledgedAt.Value, DateTimeKind.Utc))
            : null;

        await data.SaveChangesAsync();

        // Update LastEventReceivedAt on source system (works with all providers including InMemory)
        if (isNew && dto.SourceSystemId != Guid.Empty) {
            var sourceSystem = await data.SourceSystems.FindAsync(dto.SourceSystemId);
            if (sourceSystem != null) {
                sourceSystem.LastEventReceivedAt = DateTime.UtcNow;
                await data.SaveChangesAsync();
            }
        }

        // Update DataSubject stats - handle both single and bulk access
        if (isNew) {
            // Parse SubjectIds if it's a bulk access
            if (!string.IsNullOrEmpty(dto.SubjectIds)) {
                try {
                    var subjectIdList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(dto.SubjectIds);
                    if (subjectIdList?.Count > 0) {
                        await UpdateDataSubjectStatsAsync(subjectIdList, dto.SubjectType);
                    }
                } catch {
                    // If JSON parsing fails, fall back to single subject
                    if (!string.IsNullOrEmpty(dto.SubjectId)) {
                        await UpdateDataSubjectStatsAsync(dto.SubjectId, dto.SubjectType);
                    }
                }
            } else if (!string.IsNullOrEmpty(dto.SubjectId) && dto.SubjectId != "BULK") {
                await UpdateDataSubjectStatsAsync(dto.SubjectId, dto.SubjectType);
            }
        }
        
        // Return updated DTO
        dto.AccessEventId = item.AccessEventId;
        dto.ReceivedAt = item.ReceivedAt;
        dto.SubjectCount = item.SubjectCount;
        return dto;
    }

    public async Task<bool> DeleteAccessEventAsync(Guid id)
    {
        var item = await data.AccessEvents.FindAsync(id);
        if (item == null) return false;
        
        var subjectId = item.SubjectId;
        
        data.AccessEvents.Remove(item);
        await data.SaveChangesAsync();
        
        // Update DataSubject stats
        if (!string.IsNullOrEmpty(subjectId))
        {
            await UpdateDataSubjectStatsAsync(subjectId);
        }
        
        return true;
    }

    public async Task<List<DataObjects.AccessEventLookup>> GetAccessEventLookupsAsync()
    {
        return await data.AccessEvents
            .Select(x => new DataObjects.AccessEventLookup
            {
                AccessEventId = x.AccessEventId,
                DisplayName = x.UserName
            })
            .ToListAsync();
    }

    #endregion
}


// DataSubject Data Access Methods
public partial class DataAccess
{
    #region DataSubject

    public async Task<DataObjects.DataSubjectFilterResult> GetDataSubjectsAsync(DataObjects.DataSubjectFilter filter)
    {
        var query = data.DataSubjects
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(x => x.ExternalId.Contains(filter.Search) || x.SubjectType.Contains(filter.Search));
        }

        // Apply advanced filters
        if (!string.IsNullOrWhiteSpace(filter.SubjectTypeFilter))
            query = query.Where(x => x.SubjectType == filter.SubjectTypeFilter);
        if (filter.MinTotalAccesses.HasValue)
            query = query.Where(x => x.TotalAccessCount >= filter.MinTotalAccesses.Value);
        if (filter.MaxTotalAccesses.HasValue)
            query = query.Where(x => x.TotalAccessCount <= filter.MaxTotalAccesses.Value);
        if (filter.MinUniqueAccessors.HasValue)
            query = query.Where(x => x.UniqueAccessorCount >= filter.MinUniqueAccessors.Value);
        if (filter.MaxUniqueAccessors.HasValue)
            query = query.Where(x => x.UniqueAccessorCount <= filter.MaxUniqueAccessors.Value);
        if (filter.LastAccessAfter.HasValue)
            query = query.Where(x => x.LastAccessedAt >= filter.LastAccessAfter.Value);
        if (filter.LastAccessBefore.HasValue)
            query = query.Where(x => x.LastAccessedAt <= filter.LastAccessBefore.Value);
        if (filter.FirstAccessAfter.HasValue)
            query = query.Where(x => x.FirstAccessedAt >= filter.FirstAccessAfter.Value);
        if (filter.FirstAccessBefore.HasValue)
            query = query.Where(x => x.FirstAccessedAt <= filter.FirstAccessBefore.Value);

        var total = await query.CountAsync();

        query = filter.SortColumn switch
        {
            "ExternalId" => filter.SortDescending ? query.OrderByDescending(x => x.ExternalId) : query.OrderBy(x => x.ExternalId),
            "SubjectType" => filter.SortDescending ? query.OrderByDescending(x => x.SubjectType) : query.OrderBy(x => x.SubjectType),
            "FirstAccessedAt" => filter.SortDescending ? query.OrderByDescending(x => x.FirstAccessedAt) : query.OrderBy(x => x.FirstAccessedAt),
            "LastAccessedAt" => filter.SortDescending ? query.OrderByDescending(x => x.LastAccessedAt) : query.OrderBy(x => x.LastAccessedAt),
            "TotalAccessCount" => filter.SortDescending ? query.OrderByDescending(x => x.TotalAccessCount) : query.OrderBy(x => x.TotalAccessCount),
            "UniqueAccessorCount" => filter.SortDescending ? query.OrderByDescending(x => x.UniqueAccessorCount) : query.OrderBy(x => x.UniqueAccessorCount),
            _ => query.OrderByDescending(x => x.DataSubjectId)
        };

        var items = await query.Skip(filter.Skip).Take(filter.PageSize).ToListAsync();

        return new DataObjects.DataSubjectFilterResult
        {
            Records = items.Select(x => new DataObjects.DataSubject
            {
                DataSubjectId = x.DataSubjectId,
                ExternalId = x.ExternalId,
                SubjectType = x.SubjectType,
                FirstAccessedAt = x.FirstAccessedAt,
                LastAccessedAt = x.LastAccessedAt,
                TotalAccessCount = x.TotalAccessCount,
                UniqueAccessorCount = x.UniqueAccessorCount,
            }).ToList(),
            TotalRecords = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<DataObjects.DataSubject?> GetDataSubjectAsync(Guid id)
    {
        var item = await data.DataSubjects
            .FirstOrDefaultAsync(x => x.DataSubjectId == id);
        if (item == null) return null;

        return new DataObjects.DataSubject
        {
            DataSubjectId = item.DataSubjectId,
            ExternalId = item.ExternalId,
            SubjectType = item.SubjectType,
            FirstAccessedAt = item.FirstAccessedAt,
            LastAccessedAt = item.LastAccessedAt,
            TotalAccessCount = item.TotalAccessCount,
            UniqueAccessorCount = item.UniqueAccessorCount,
        };
    }

    public async Task<DataObjects.DataSubject?> SaveDataSubjectAsync(DataObjects.DataSubject dto)
    {
        EFModels.EFModels.DataSubjectItem item;
        var isNew = dto.DataSubjectId == default;

        if (isNew) {
            item = new EFModels.EFModels.DataSubjectItem();
            item.DataSubjectId = Guid.NewGuid();
            item.FirstAccessedAt = DateTime.UtcNow;
            item.LastAccessedAt = DateTime.UtcNow;
            item.TotalAccessCount = 0;
            item.UniqueAccessorCount = 0;
            data.DataSubjects.Add(item);
        } else {
            item = await data.DataSubjects.FindAsync(dto.DataSubjectId);
            if (item == null) return null;
        }

        // Only update user-editable fields
        item.ExternalId = dto.ExternalId ?? string.Empty;
        item.SubjectType = dto.SubjectType ?? string.Empty;
        // Don't update statistics - those are managed by the system

        await data.SaveChangesAsync();
        
        // Return updated DTO with all values
        dto.DataSubjectId = item.DataSubjectId;
        dto.FirstAccessedAt = item.FirstAccessedAt;
        dto.LastAccessedAt = item.LastAccessedAt;
        dto.TotalAccessCount = item.TotalAccessCount;
        dto.UniqueAccessorCount = item.UniqueAccessorCount;
        return dto;
    }

    public async Task<bool> DeleteDataSubjectAsync(Guid id)
    {
        var item = await data.DataSubjects.FindAsync(id);
        if (item == null) return false;
        data.DataSubjects.Remove(item);
        await data.SaveChangesAsync();
        return true;
    }

    public async Task<List<DataObjects.DataSubjectLookup>> GetDataSubjectLookupsAsync()
    {
        return await data.DataSubjects
            .Select(x => new DataObjects.DataSubjectLookup
            {
                DataSubjectId = x.DataSubjectId,
                DisplayName = x.ExternalId
            })
            .ToListAsync();
    }

    #endregion
}


// ComplianceReport Data Access Methods
public partial class DataAccess
{
    #region ComplianceReport

    public async Task<DataObjects.ComplianceReportFilterResult> GetComplianceReportsAsync(DataObjects.ComplianceReportFilter filter)
    {
        var query = data.ComplianceReports
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(x => x.ReportType.Contains(filter.Search) || x.GeneratedBy.Contains(filter.Search) || x.ReportData.Contains(filter.Search));
        }

        // Apply advanced filters
        if (!string.IsNullOrWhiteSpace(filter.ReportTypeFilter))
            query = query.Where(x => x.ReportType == filter.ReportTypeFilter);
        if (filter.GeneratedAfter.HasValue)
            query = query.Where(x => x.GeneratedAt >= filter.GeneratedAfter.Value);
        if (filter.GeneratedBefore.HasValue)
            query = query.Where(x => x.GeneratedAt <= filter.GeneratedBefore.Value);
        if (filter.MinTotalEvents.HasValue)
            query = query.Where(x => x.TotalEvents >= filter.MinTotalEvents.Value);
        if (filter.MaxTotalEvents.HasValue)
            query = query.Where(x => x.TotalEvents <= filter.MaxTotalEvents.Value);
        if (filter.MinUniqueUsers.HasValue)
            query = query.Where(x => x.UniqueUsers >= filter.MinUniqueUsers.Value);
        if (filter.MaxUniqueUsers.HasValue)
            query = query.Where(x => x.UniqueUsers <= filter.MaxUniqueUsers.Value);
        if (filter.MinUniqueSubjects.HasValue)
            query = query.Where(x => x.UniqueSubjects >= filter.MinUniqueSubjects.Value);
        if (filter.MaxUniqueSubjects.HasValue)
            query = query.Where(x => x.UniqueSubjects <= filter.MaxUniqueSubjects.Value);

        var total = await query.CountAsync();

        query = filter.SortColumn switch
        {
            "ReportType" => filter.SortDescending ? query.OrderByDescending(x => x.ReportType) : query.OrderBy(x => x.ReportType),
            "GeneratedAt" => filter.SortDescending ? query.OrderByDescending(x => x.GeneratedAt) : query.OrderBy(x => x.GeneratedAt),
            "GeneratedBy" => filter.SortDescending ? query.OrderByDescending(x => x.GeneratedBy) : query.OrderBy(x => x.GeneratedBy),
            "PeriodStart" => filter.SortDescending ? query.OrderByDescending(x => x.PeriodStart) : query.OrderBy(x => x.PeriodStart),
            "PeriodEnd" => filter.SortDescending ? query.OrderByDescending(x => x.PeriodEnd) : query.OrderBy(x => x.PeriodEnd),
            "TotalEvents" => filter.SortDescending ? query.OrderByDescending(x => x.TotalEvents) : query.OrderBy(x => x.TotalEvents),
            "UniqueUsers" => filter.SortDescending ? query.OrderByDescending(x => x.UniqueUsers) : query.OrderBy(x => x.UniqueUsers),
            "UniqueSubjects" => filter.SortDescending ? query.OrderByDescending(x => x.UniqueSubjects) : query.OrderBy(x => x.UniqueSubjects),
            _ => query.OrderByDescending(x => x.ComplianceReportId)
        };

        var items = await query.Skip(filter.Skip).Take(filter.PageSize).ToListAsync();

        return new DataObjects.ComplianceReportFilterResult
        {
            Records = items.Select(x => new DataObjects.ComplianceReport
            {
                ComplianceReportId = x.ComplianceReportId,
                ReportType = x.ReportType,
                GeneratedAt = x.GeneratedAt,
                GeneratedBy = x.GeneratedBy,
                PeriodStart = x.PeriodStart,
                PeriodEnd = x.PeriodEnd,
                TotalEvents = x.TotalEvents,
                UniqueUsers = x.UniqueUsers,
                UniqueSubjects = x.UniqueSubjects,
                ReportData = x.ReportData,
                FileUrl = x.FileUrl,
            }).ToList(),
            TotalRecords = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<DataObjects.ComplianceReport?> GetComplianceReportAsync(Guid id)
    {
        var item = await data.ComplianceReports
            .FirstOrDefaultAsync(x => x.ComplianceReportId == id);
        if (item == null) return null;

        return new DataObjects.ComplianceReport
        {
            ComplianceReportId = item.ComplianceReportId,
            ReportType = item.ReportType,
            GeneratedAt = item.GeneratedAt,
            GeneratedBy = item.GeneratedBy,
            PeriodStart = item.PeriodStart,
            PeriodEnd = item.PeriodEnd,
            TotalEvents = item.TotalEvents,
            UniqueUsers = item.UniqueUsers,
            UniqueSubjects = item.UniqueSubjects,
            ReportData = item.ReportData,
            FileUrl = item.FileUrl,
        };
    }

    public async Task<DataObjects.ComplianceReport?> SaveComplianceReportAsync(DataObjects.ComplianceReport dto)
    {
        EFModels.EFModels.ComplianceReportItem item;
        var isNew = dto.ComplianceReportId == default;

        // Ensure period dates are treated as UTC (end of day for PeriodEnd)
        var periodStartUtc = dto.PeriodStart.Kind == DateTimeKind.Utc 
            ? dto.PeriodStart.Date 
            : DateTime.SpecifyKind(dto.PeriodStart.Date, DateTimeKind.Utc);
        var periodEndUtc = dto.PeriodEnd.Kind == DateTimeKind.Utc 
            ? dto.PeriodEnd.Date.AddDays(1).AddTicks(-1) 
            : DateTime.SpecifyKind(dto.PeriodEnd.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        if (isNew) {
            item = new EFModels.EFModels.ComplianceReportItem();
            item.ComplianceReportId = Guid.NewGuid();
            item.GeneratedAt = DateTime.UtcNow;
            item.GeneratedBy = "System"; // TODO: Get from CurrentUser when available
            
            // Calculate statistics for the report period (using UTC dates)
            var stats = await CalculateReportStatisticsAsync(periodStartUtc, periodEndUtc);
            item.TotalEvents = stats.TotalEvents;
            item.UniqueUsers = stats.UniqueUsers;
            item.UniqueSubjects = stats.UniqueSubjects;
            
            data.ComplianceReports.Add(item);
        } else {
            item = await data.ComplianceReports.FindAsync(dto.ComplianceReportId);
            if (item == null) return null;
        }

        // User-editable fields - store dates as UTC
        item.ReportType = dto.ReportType ?? string.Empty;
        item.PeriodStart = periodStartUtc;
        item.PeriodEnd = DateTime.SpecifyKind(dto.PeriodEnd.Date, DateTimeKind.Utc); // Store just the date part
        item.ReportData = dto.ReportData ?? string.Empty;
        item.FileUrl = dto.FileUrl ?? string.Empty;
        // Don't update GeneratedAt, GeneratedBy, or statistics for existing records

        await data.SaveChangesAsync();
        
        // Return updated DTO
        dto.ComplianceReportId = item.ComplianceReportId;
        dto.GeneratedAt = item.GeneratedAt;
        dto.GeneratedBy = item.GeneratedBy;
        dto.TotalEvents = item.TotalEvents;
        dto.UniqueUsers = item.UniqueUsers;
        dto.UniqueSubjects = item.UniqueSubjects;
        dto.PeriodStart = item.PeriodStart;
        dto.PeriodEnd = item.PeriodEnd;
        return dto;
    }

    /// <summary>
    /// Calculate report statistics for a given period.
    /// Period dates should already be in UTC.
    /// </summary>
    private async Task<(int TotalEvents, int UniqueUsers, int UniqueSubjects)> CalculateReportStatisticsAsync(
        DateTime periodStartUtc, DateTime periodEndUtc)
    {
        var events = await data.AccessEvents
            .Where(x => x.AccessedAt >= periodStartUtc && x.AccessedAt <= periodEndUtc)
            .ToListAsync();

        return (
            TotalEvents: events.Count,
            UniqueUsers: events.Select(x => x.UserId).Distinct().Count(),
            UniqueSubjects: events.Select(x => x.SubjectId).Distinct().Count()
        );
    }

    public async Task<bool> DeleteComplianceReportAsync(Guid id)
    {
        var item = await data.ComplianceReports.FindAsync(id);
        if (item == null) return false;
        data.ComplianceReports.Remove(item);
        await data.SaveChangesAsync();
        return true;
    }

    public async Task<List<DataObjects.ComplianceReportLookup>> GetComplianceReportLookupsAsync()
    {
        return await data.ComplianceReports
            .Select(x => new DataObjects.ComplianceReportLookup
            {
                ComplianceReportId = x.ComplianceReportId,
                DisplayName = x.ReportType
            })
            .ToListAsync();
    }

    #endregion
}


