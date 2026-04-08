namespace FreeTools.Core;

/// <summary>
/// Command-line argument parsing utilities.
/// </summary>
public static class CliArgs
{
    public static bool HasFlag(List<string> args, string flag)
    {
        var index = args.FindIndex(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            args.RemoveAt(index);
            return true;
        }
        return false;
    }

    public static bool HasFlag(List<string> args, params string[] flags)
    {
        foreach (var flag in flags)
        {
            if (HasFlag(args, flag))
                return true;
        }
        return false;
    }

    public static string? GetOption(List<string> args, string prefix)
    {
        var index = args.FindIndex(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            var value = args[index][prefix.Length..];
            args.RemoveAt(index);
            return value;
        }
        return null;
    }

    public static string? GetOption(List<string> args, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            var value = GetOption(args, prefix);
            if (value is not null)
                return value;
        }
        return null;
    }

    public static string? GetPositional(List<string> args, int index, string? defaultValue = null) 
        => args.Count > index ? args[index] : defaultValue;

    public static int GetPositionalInt(List<string> args, int index, int defaultValue) 
        => args.Count > index && int.TryParse(args[index], out var value) ? value : defaultValue;

    public static string GetRequired(List<string> args, int index, string name)
    {
        if (args.Count <= index)
            throw new ArgumentException($"Missing required argument: {name}");
        return args[index];
    }

    // --- Environment Variable Helpers ---

    /// <summary>
    /// Get a string value from environment variable or CLI arg, with fallback default.
    /// </summary>
    public static string GetEnvOrArg(string envVar, string[] args, int argIndex, string defaultValue)
    {
        return Environment.GetEnvironmentVariable(envVar)
            ?? (args.Length > argIndex ? args[argIndex] : null)
            ?? defaultValue;
    }

    /// <summary>
    /// Get an integer value from environment variable or CLI arg, with fallback default.
    /// </summary>
    public static int GetEnvOrArgInt(string envVar, string[] args, int argIndex, int defaultValue)
    {
        var envValue = Environment.GetEnvironmentVariable(envVar);
        if (int.TryParse(envValue, out var envInt))
            return envInt;

        if (args.Length > argIndex && int.TryParse(args[argIndex], out var argInt))
            return argInt;

        return defaultValue;
    }

    /// <summary>
    /// Check if an environment variable is set to "true" (case-insensitive).
    /// </summary>
    public static bool GetEnvBool(string envVar)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
