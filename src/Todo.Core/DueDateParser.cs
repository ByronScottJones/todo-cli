using System.Globalization;

namespace Todo.Core;

/// <summary>
/// Parses user-entered due dates. Shared by the CLI and the TUI so both accept the same formats.
/// </summary>
public static class DueDateParser
{
    public const string FormatHint = "yyyy-MM-dd or MM-dd";

    /// <summary>
    /// Parses <c>yyyy-MM-dd</c> or <c>MM-dd</c> (current year). Returns <see langword="null"/> for blank or invalid input.
    /// </summary>
    public static DateTime? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", null, DateTimeStyles.None, out var fullDate))
            return fullDate;

        // Without a year, TryParseExact fills in the current year, which also rejects 02-29 in non-leap years.
        if (DateTime.TryParseExact(text, "MM-dd", null, DateTimeStyles.None, out var partialDate))
            return new DateTime(DateTime.Now.Year, partialDate.Month, partialDate.Day);

        return null;
    }
}
