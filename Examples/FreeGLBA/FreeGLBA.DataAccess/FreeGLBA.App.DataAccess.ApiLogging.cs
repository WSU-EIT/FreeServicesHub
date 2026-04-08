using Microsoft.EntityFrameworkCore;

namespace FreeGLBA;

// ============================================================================
// API REQUEST LOGGING - Data Access Methods
// CRUD operations for API request logs and body logging configuration
// ============================================================================

public partial class DataAccess
{
    #region API Request Logging

    /// <summary>
    /// Create a new API request log entry.
    /// </summary>
    public async Task<Guid> CreateApiLogAsync(EFModels.EFModels.ApiRequestLogItem log)
    {
        if (log.ApiRequestLogId == Guid.Empty)
        {
            log.ApiRequestLogId = Guid.NewGuid();
        }

        data.ApiRequestLogs.Add(log);
        await data.SaveChangesAsync();

        return log.ApiRequestLogId;
    }

    /// <summary>
    /// Get paginated/filtered list of API request logs.
    /// </summary>
    public async Task<DataObjects.ApiLogFilterResult> GetApiLogsAsync(DataObjects.ApiLogFilter filter)
    {
        var query = data.ApiRequestLogs
            .AsNoTracking()
            .AsQueryable();

        // Apply time range filter
        if (filter.FromDate.HasValue)
        {
            query = query.Where(x => x.RequestedAt >= filter.FromDate.Value);
        }
        if (filter.ToDate.HasValue)
        {
            query = query.Where(x => x.RequestedAt <= filter.ToDate.Value);
        }

        // Apply source system filter
        if (filter.SourceSystemId.HasValue)
        {
            query = query.Where(x => x.SourceSystemId == filter.SourceSystemId.Value);
        }

        // Apply status filters
        if (filter.ErrorsOnly == true)
        {
            query = query.Where(x => !x.IsSuccess);
        }
        else if (filter.SuccessOnly == true)
        {
            query = query.Where(x => x.IsSuccess);
        }

        if (filter.StatusCodes?.Count > 0)
        {
            query = query.Where(x => filter.StatusCodes.Contains(x.StatusCode));
        }

        // Apply duration filters
        if (filter.MinDurationMs.HasValue)
        {
            query = query.Where(x => x.DurationMs >= filter.MinDurationMs.Value);
        }
        if (filter.MaxDurationMs.HasValue)
        {
            query = query.Where(x => x.DurationMs <= filter.MaxDurationMs.Value);
        }

        // Apply correlation filter
        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
        {
            query = query.Where(x => x.CorrelationId == filter.CorrelationId);
        }

        // Apply search term
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.ToLower();
            query = query.Where(x => 
                x.RequestPath.ToLower().Contains(term) ||
                x.ErrorMessage.ToLower().Contains(term) ||
                x.SourceSystemName.ToLower().Contains(term));
        }

        // Get total count before pagination
        var totalRecords = await query.CountAsync();

        // Apply sorting
        query = filter.SortColumn switch
        {
            "RequestedAt" => filter.SortDescending ? query.OrderByDescending(x => x.RequestedAt) : query.OrderBy(x => x.RequestedAt),
            "DurationMs" => filter.SortDescending ? query.OrderByDescending(x => x.DurationMs) : query.OrderBy(x => x.DurationMs),
            "StatusCode" => filter.SortDescending ? query.OrderByDescending(x => x.StatusCode) : query.OrderBy(x => x.StatusCode),
            "RequestPath" => filter.SortDescending ? query.OrderByDescending(x => x.RequestPath) : query.OrderBy(x => x.RequestPath),
            "SourceSystemName" => filter.SortDescending ? query.OrderByDescending(x => x.SourceSystemName) : query.OrderBy(x => x.SourceSystemName),
            _ => query.OrderByDescending(x => x.RequestedAt)
        };

        // Apply pagination
        var items = await query
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .Select(x => new DataObjects.ApiRequestLogListItem
            {
                ApiRequestLogId = x.ApiRequestLogId,
                SourceSystemName = x.SourceSystemName,
                HttpMethod = x.HttpMethod,
                RequestPath = x.RequestPath,
                RequestedAt = x.RequestedAt,
                DurationMs = x.DurationMs,
                StatusCode = x.StatusCode,
                IsSuccess = x.IsSuccess,
                ErrorMessage = x.ErrorMessage
            })
            .ToListAsync();

        return new DataObjects.ApiLogFilterResult
        {
            Records = items,
            TotalRecords = totalRecords,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    /// <summary>
    /// Get a single API request log by ID.
    /// </summary>
    public async Task<DataObjects.ApiRequestLog?> GetApiLogAsync(Guid id)
    {
        var item = await data.ApiRequestLogs
            .AsNoTracking()
            .Where(x => x.ApiRequestLogId == id)
            .FirstOrDefaultAsync();

        if (item == null) return null;

        return new DataObjects.ApiRequestLog
        {
            ApiRequestLogId = item.ApiRequestLogId,
            SourceSystemId = item.SourceSystemId,
            SourceSystemName = item.SourceSystemName,
            UserId = item.UserId,
            UserName = item.UserName,
            TenantId = item.TenantId,
            HttpMethod = item.HttpMethod,
            RequestPath = item.RequestPath,
            QueryString = item.QueryString,
            RequestHeaders = item.RequestHeaders,
            RequestBody = item.RequestBody,
            RequestBodySize = item.RequestBodySize,
            RequestedAt = item.RequestedAt,
            RespondedAt = item.RespondedAt,
            DurationMs = item.DurationMs,
            IpAddress = item.IpAddress,
            UserAgent = item.UserAgent,
            ForwardedFor = item.ForwardedFor,
            StatusCode = item.StatusCode,
            IsSuccess = item.IsSuccess,
            ResponseBody = item.ResponseBody,
            ResponseBodySize = item.ResponseBodySize,
            ErrorMessage = item.ErrorMessage,
            ExceptionType = item.ExceptionType,
            CorrelationId = item.CorrelationId,
            AuthType = item.AuthType,
            RelatedEntityId = item.RelatedEntityId,
            RelatedEntityType = item.RelatedEntityType,
            BodyLoggingEnabled = item.BodyLoggingEnabled
        };
    }

    /// <summary>
    /// Get dashboard statistics for API logs.
    /// </summary>
    public async Task<DataObjects.ApiLogDashboardStats> GetApiLogDashboardStatsAsync(DateTime from, DateTime to)
    {
        var query = data.ApiRequestLogs
            .AsNoTracking()
            .Where(x => x.RequestedAt >= from && x.RequestedAt <= to);

        // Get basic stats
        var totalRequests = await query.CountAsync();
        var totalErrors = await query.CountAsync(x => !x.IsSuccess);
        var avgDurationMs = totalRequests > 0 
            ? await query.AverageAsync(x => (double)x.DurationMs) 
            : 0;

        // Get total log count (all time)
        var totalLogCount = await data.ApiRequestLogs.LongCountAsync();

        // Get logs older than 7 years
        var sevenYearsAgo = DateTime.UtcNow.AddYears(-7);
        var logsOlderThan7Years = await data.ApiRequestLogs
            .LongCountAsync(x => x.RequestedAt < sevenYearsAgo);

        // Get breakdown by source system
        var bySourceSystem = await query
            .GroupBy(x => x.SourceSystemName)
            .Select(g => new DataObjects.SourceSystemStats
            {
                SourceSystemName = g.Key ?? "(Unknown)",
                Count = g.Count(),
                Percentage = 0 // Calculated below
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        // Calculate percentages
        foreach (var item in bySourceSystem)
        {
            item.Percentage = totalRequests > 0 
                ? Math.Round((double)item.Count / totalRequests * 100, 1) 
                : 0;
        }

        // Get breakdown by status code
        var byStatusCode = await query
            .GroupBy(x => x.StatusCode)
            .Select(g => new DataObjects.StatusCodeStats
            {
                StatusCode = g.Key,
                Category = GetStatusCategory(g.Key),
                Count = g.Count(),
                Percentage = 0 // Calculated below
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        // Calculate percentages and set categories
        foreach (var item in byStatusCode)
        {
            item.Category = GetStatusCategory(item.StatusCode);
            item.Percentage = totalRequests > 0 
                ? Math.Round((double)item.Count / totalRequests * 100, 1) 
                : 0;
        }

        // Get recent errors
        var recentErrors = await query
            .Where(x => !x.IsSuccess)
            .OrderByDescending(x => x.RequestedAt)
            .Take(10)
            .Select(x => new DataObjects.ApiRequestLogListItem
            {
                ApiRequestLogId = x.ApiRequestLogId,
                SourceSystemName = x.SourceSystemName,
                HttpMethod = x.HttpMethod,
                RequestPath = x.RequestPath,
                RequestedAt = x.RequestedAt,
                DurationMs = x.DurationMs,
                StatusCode = x.StatusCode,
                IsSuccess = x.IsSuccess,
                ErrorMessage = x.ErrorMessage
            })
            .ToListAsync();

        // Get requests over time (hourly buckets for last 24h, daily for longer ranges)
        var requestsOverTime = new List<DataObjects.TimeSeriesPoint>();
        var timeSpan = to - from;
        
        if (timeSpan.TotalHours <= 24)
        {
            // Hourly buckets
            for (var hour = from; hour < to; hour = hour.AddHours(1))
            {
                var hourEnd = hour.AddHours(1);
                var count = await query.CountAsync(x => x.RequestedAt >= hour && x.RequestedAt < hourEnd);
                requestsOverTime.Add(new DataObjects.TimeSeriesPoint
                {
                    Timestamp = hour,
                    Count = count
                });
            }
        }
        else
        {
            // Daily buckets
            for (var day = from.Date; day < to.Date; day = day.AddDays(1))
            {
                var dayEnd = day.AddDays(1);
                var count = await query.CountAsync(x => x.RequestedAt >= day && x.RequestedAt < dayEnd);
                requestsOverTime.Add(new DataObjects.TimeSeriesPoint
                {
                    Timestamp = day,
                    Count = count
                });
            }
        }

        return new DataObjects.ApiLogDashboardStats
        {
            TotalRequests = totalRequests,
            TotalErrors = totalErrors,
            ErrorRate = totalRequests > 0 ? Math.Round((double)totalErrors / totalRequests * 100, 2) : 0,
            AvgDurationMs = Math.Round(avgDurationMs, 1),
            TotalLogCount = totalLogCount,
            LogsOlderThan7Years = logsOlderThan7Years,
            BySourceSystem = bySourceSystem,
            ByStatusCode = byStatusCode,
            RequestsOverTime = requestsOverTime,
            RecentErrors = recentErrors
        };
    }

    private static string GetStatusCategory(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 300 => "2xx Success",
            >= 300 and < 400 => "3xx Redirect",
            >= 400 and < 500 => "4xx Client Error",
            >= 500 => "5xx Server Error",
            _ => "Other"
        };
    }

    #endregion

    #region Body Logging Configuration

    /// <summary>
    /// Get active body logging configuration for a source system.
    /// </summary>
    public async Task<EFModels.EFModels.BodyLoggingConfigItem?> GetBodyLoggingConfigAsync(Guid sourceSystemId)
    {
        // First check for expired configs and auto-disable them
        var expiredConfigs = await data.BodyLoggingConfigs
            .Where(c => c.SourceSystemId == sourceSystemId && c.IsActive && c.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
        
        foreach (var config in expiredConfigs)
        {
            config.IsActive = false;
            config.DisabledAt = DateTime.UtcNow;
        }
        
        if (expiredConfigs.Any())
        {
            await data.SaveChangesAsync();
        }

        return await data.BodyLoggingConfigs
            .Where(c => c.SourceSystemId == sourceSystemId && c.IsActive)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get all body logging configurations (for settings page).
    /// </summary>
    public async Task<List<DataObjects.BodyLoggingConfig>> GetBodyLoggingConfigsAsync()
    {
        // Auto-disable any expired configs first
        var expiredConfigs = await data.BodyLoggingConfigs
            .Where(c => c.IsActive && c.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
        
        foreach (var config in expiredConfigs)
        {
            config.IsActive = false;
            config.DisabledAt = DateTime.UtcNow;
        }
        
        if (expiredConfigs.Any())
        {
            await data.SaveChangesAsync();
        }

        var items = await data.BodyLoggingConfigs
            .AsNoTracking()
            .OrderByDescending(c => c.EnabledAt)
            .Take(100)
            .ToListAsync();

        return items.Select(c => new DataObjects.BodyLoggingConfig
        {
            BodyLoggingConfigId = c.BodyLoggingConfigId,
            SourceSystemId = c.SourceSystemId,
            EnabledByUserId = c.EnabledByUserId,
            EnabledByUserName = c.EnabledByUserName,
            EnabledAt = c.EnabledAt,
            ExpiresAt = c.ExpiresAt,
            DisabledAt = c.DisabledAt,
            IsActive = c.IsActive,
            Reason = c.Reason
        }).ToList();
    }

    /// <summary>
    /// Enable body logging for a source system.
    /// </summary>
    public async Task<DataObjects.BodyLoggingConfig> EnableBodyLoggingAsync(
        Guid sourceSystemId, 
        Guid enabledByUserId, 
        string enabledByUserName, 
        int durationHours, 
        string reason)
    {
        // Disable any existing active configs for this source system
        var existingConfigs = await data.BodyLoggingConfigs
            .Where(c => c.SourceSystemId == sourceSystemId && c.IsActive)
            .ToListAsync();
        
        foreach (var config in existingConfigs)
        {
            config.IsActive = false;
            config.DisabledAt = DateTime.UtcNow;
        }

        // Create new config
        var now = DateTime.UtcNow;
        var newConfig = new EFModels.EFModels.BodyLoggingConfigItem
        {
            BodyLoggingConfigId = Guid.NewGuid(),
            SourceSystemId = sourceSystemId,
            EnabledByUserId = enabledByUserId,
            EnabledByUserName = enabledByUserName,
            EnabledAt = now,
            ExpiresAt = now.AddHours(durationHours),
            IsActive = true,
            Reason = reason
        };

        data.BodyLoggingConfigs.Add(newConfig);
        await data.SaveChangesAsync();

        return new DataObjects.BodyLoggingConfig
        {
            BodyLoggingConfigId = newConfig.BodyLoggingConfigId,
            SourceSystemId = newConfig.SourceSystemId,
            EnabledByUserId = newConfig.EnabledByUserId,
            EnabledByUserName = newConfig.EnabledByUserName,
            EnabledAt = newConfig.EnabledAt,
            ExpiresAt = newConfig.ExpiresAt,
            DisabledAt = newConfig.DisabledAt,
            IsActive = newConfig.IsActive,
            Reason = newConfig.Reason
        };
    }

    /// <summary>
    /// Disable body logging for a source system.
    /// </summary>
    public async Task<bool> DisableBodyLoggingAsync(Guid configId)
    {
        var config = await data.BodyLoggingConfigs
            .Where(c => c.BodyLoggingConfigId == configId)
            .FirstOrDefaultAsync();
        
        if (config == null) return false;

        config.IsActive = false;
        config.DisabledAt = DateTime.UtcNow;
        await data.SaveChangesAsync();

        return true;
    }

    #endregion

    // NOTE: Database cleanup (log retention) is handled externally
    // via scheduled SQL jobs managed by your DBA team.
    // See doc 123 for recommended SQL scripts.
}
