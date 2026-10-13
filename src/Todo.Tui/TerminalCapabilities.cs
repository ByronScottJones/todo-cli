namespace Todo.Tui;

/// <summary>Best-effort checks of what the hosting terminal can display.</summary>
public static class TerminalCapabilities
{
    /// <summary>
    /// True when the locale advertises UTF-8, or on Windows. Otherwise the UI uses ASCII glyphs so box and
    /// check characters never render as garbage.
    /// </summary>
    public static bool SupportsUnicode(Func<string, string?> getEnvironmentVariable, bool isWindows)
    {
        if (isWindows)
            return true;

        if (string.Equals(getEnvironmentVariable("TERM"), "linux", StringComparison.OrdinalIgnoreCase))
            return false;

        // Locale precedence: LC_ALL overrides LC_CTYPE overrides LANG.
        foreach (var name in new[] { "LC_ALL", "LC_CTYPE", "LANG" })
        {
            var value = getEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
                return value.Contains("utf-8", StringComparison.OrdinalIgnoreCase)
                       || value.Contains("utf8", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
