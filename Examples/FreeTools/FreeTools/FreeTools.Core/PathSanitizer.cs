namespace FreeTools.Core;

/// <summary>
/// Utilities for converting routes to safe file system paths.
/// </summary>
public static class PathSanitizer
{
    /// <summary>
    /// Convert a route to a safe directory path for output files.
    /// Example: "/Account/Login" -> "Account\Login" (on Windows)
    /// </summary>
    public static string RouteToDirectoryPath(string route)
    {
        var safePath = route.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return string.IsNullOrEmpty(safePath) ? "root" : safePath;
    }

    /// <summary>
    /// Get the full output file path for a route.
    /// </summary>
    public static string GetOutputFilePath(string outputDir, string route, string filename)
    {
        var safePath = RouteToDirectoryPath(route);
        return Path.Combine(outputDir, safePath, filename);
    }

    /// <summary>
    /// Ensure the directory for a file path exists.
    /// </summary>
    public static void EnsureDirectoryExists(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Format bytes into human-readable string (KB, MB, etc.)
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:F1} GB",
            >= MB => $"{bytes / (double)MB:F1} MB",
            >= KB => $"{bytes / (double)KB:F1} KB",
            _ => $"{bytes} bytes"
        };
    }
}
