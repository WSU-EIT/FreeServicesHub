namespace FreeTools.Core;

/// <summary>
/// Utilities for parsing routes from CSV files and detecting route parameters.
/// </summary>
public static class RouteParser
{
    /// <summary>
    /// Check if a route contains parameters (e.g., "{id}", "{name}").
    /// </summary>
    public static bool HasParameter(string route)
        => route.Contains('{') && route.Contains('}');

    /// <summary>
    /// Parse routes from a CSV file. Returns testable routes and skipped routes with parameters.
    /// When tenantCode is provided, substitutes {TenantCode} before checking for parameters
    /// and deduplicates so tenant-code routes replace bare routes.
    /// CSV format expected: FilePath,Route,RequiresAuth,Project (header row + data rows)
    /// </summary>
    public static (List<string> routes, List<string> skipped) ParseRoutesFromCsv(
        string[] csvLines,
        int routeColumnIndex = 1,
        bool skipParameterizedRoutes = true,
        string? tenantCode = null)
    {
        var routeSet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // route -> route (for dedup)
        var skipped = new List<string>();
        var hasTenantCode = !string.IsNullOrWhiteSpace(tenantCode);

        for (int i = 1; i < csvLines.Length; i++)
        {
            var line = csvLines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(',');
            if (parts.Length <= routeColumnIndex)
                continue;

            var rawRoute = parts[routeColumnIndex].Trim('"').Trim();
            if (string.IsNullOrWhiteSpace(rawRoute))
                continue;

            // Substitute {TenantCode} with the actual tenant code
            var route = rawRoute;
            bool wasTenantRoute = false;
            if (hasTenantCode && route.Contains("{TenantCode}", StringComparison.OrdinalIgnoreCase))
            {
                route = route.Replace("{TenantCode}", tenantCode!, StringComparison.OrdinalIgnoreCase);
                wasTenantRoute = true;
            }

            if (skipParameterizedRoutes && HasParameter(route))
            {
                skipped.Add(rawRoute);
                continue;
            }

            // Dedup: tenant-code routes win over bare routes
            if (wasTenantRoute)
            {
                routeSet[route] = route;
            }
            else if (!routeSet.ContainsKey(route))
            {
                if (!hasTenantCode || !routeSet.ContainsKey($"/{tenantCode}{route}"))
                    routeSet[route] = route;
            }
        }

        // Remove bare routes that have a tenant-code equivalent
        if (hasTenantCode)
        {
            var toRemove = routeSet.Keys
                .Where(r => !r.StartsWith($"/{tenantCode}", StringComparison.OrdinalIgnoreCase)
                          && routeSet.ContainsKey($"/{tenantCode}{r}"))
                .ToList();
            foreach (var key in toRemove)
                routeSet.Remove(key);
        }

        var routes = routeSet.Values.OrderBy(r => r).ToList();
        return (routes, skipped);
    }

    /// <summary>
    /// Parse routes from a CSV file path.
    /// </summary>
    public static async Task<(List<string> routes, List<string> skipped)> ParseRoutesFromCsvFileAsync(
        string csvPath,
        int routeColumnIndex = 1,
        bool skipParameterizedRoutes = true,
        string? tenantCode = null)
    {
        var lines = await File.ReadAllLinesAsync(csvPath);
        return ParseRoutesFromCsv(lines, routeColumnIndex, skipParameterizedRoutes, tenantCode);
    }

    /// <summary>
    /// Build a full URL from base URL and route.
    /// </summary>
    public static string BuildUrl(string baseUrl, string route)
        => baseUrl.TrimEnd('/') + "/" + route.TrimStart('/');
}
