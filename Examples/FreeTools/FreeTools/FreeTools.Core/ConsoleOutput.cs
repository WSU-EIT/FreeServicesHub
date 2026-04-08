namespace FreeTools.Core;

/// <summary>
/// Thread-safe console output utilities for CLI tools.
/// </summary>
public static class ConsoleOutput
{
    private static readonly object _lock = new();

    /// <summary>
    /// Write a message to console in a thread-safe manner.
    /// </summary>
    public static void WriteLine(string message, bool isError = false)
    {
        lock (_lock)
        {
            if (isError)
                Console.Error.WriteLine(message);
            else
                Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Write to console without newline in a thread-safe manner.
    /// </summary>
    public static void Write(string message, bool isError = false)
    {
        lock (_lock)
        {
            if (isError)
                Console.Error.Write(message);
            else
                Console.Write(message);
        }
    }

    /// <summary>
    /// Print a standard tool banner header.
    /// </summary>
    public static void PrintBanner(string toolName, string? version = null)
    {
        var title = version is null ? toolName : $"{toolName} v{version}";
        Console.WriteLine("============================================================");
        Console.WriteLine($" {title}");
        Console.WriteLine("============================================================");
    }

    /// <summary>
    /// Print a section divider with optional title.
    /// </summary>
    public static void PrintDivider(string? title = null)
    {
        if (title is null)
            Console.WriteLine("============================================================");
        else
        {
            Console.WriteLine("============================================================");
            Console.WriteLine($" {title}");
            Console.WriteLine("============================================================");
        }
    }

    /// <summary>
    /// Print a key-value configuration line.
    /// </summary>
    public static void PrintConfig(string key, string value, int keyWidth = 18)
    {
        var label = (key + ":").PadRight(keyWidth);
        Console.WriteLine($"{label} {value}");
    }
}
