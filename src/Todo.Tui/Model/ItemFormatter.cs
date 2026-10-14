using Terminal.Gui.Text;
using Todo.Core.Model;

namespace Todo.Tui.Model;

/// <summary>RowGlyphs used in item rows; ASCII fallbacks keep the UI usable without Unicode.</summary>
public sealed record RowGlyphs(string Open, string Done, string Important, string Overdue, string Ellipsis)
{
    public static readonly RowGlyphs Unicode = new("☐", "☑", "★", "!", "…");
    public static readonly RowGlyphs Ascii = new("[ ]", "[x]", "*", "!", "...");
}

/// <summary>
/// Formats an item as one fixed-width row. State is conveyed with text and glyphs, never color alone.
/// </summary>
public static class ItemFormatter
{
    private const int MinSubjectColumns = 12;

    public static string FormatRow(TodoItem item, string? listName, int width, DateTime today, RowGlyphs glyphs)
    {
        var check = item.IsCompleted ? glyphs.Done : glyphs.Open;
        var star = item.IsImportant ? glyphs.Important : new string(' ', glyphs.Important.GetColumns());
        var prefix = $"{check} {star} ";

        var suffix = ChooseSuffix(item, listName, width - prefix.GetColumns(), today, glyphs);
        var subjectWidth = Math.Max(0, width - prefix.GetColumns() - suffix.GetColumns());
        var subject = FitToWidth(item.Subject ?? string.Empty, subjectWidth, glyphs.Ellipsis);
        var padding = Math.Max(0, subjectWidth - subject.GetColumns());

        // On extremely narrow terminals even the prefix may not fit; clamp so a row never overflows.
        var row = prefix + subject + new string(' ', padding) + suffix;
        return row.GetColumns() <= width ? row : FitToWidth(row, width, string.Empty);
    }

    /// <summary>
    /// Keeps the most useful trailing details that still leave room for the subject: due date and list, then
    /// the due date alone, then nothing.
    /// </summary>
    private static string ChooseSuffix(TodoItem item, string? listName, int availableColumns, DateTime today, RowGlyphs glyphs)
    {
        foreach (var candidate in new[]
                 {
                     BuildSuffix(item, listName, today, glyphs),
                     BuildSuffix(item, null, today, glyphs)
                 })
        {
            if (availableColumns - candidate.GetColumns() >= MinSubjectColumns)
                return candidate;
        }

        return string.Empty;
    }

    public static string BuildSuffix(TodoItem item, string? listName, DateTime today, RowGlyphs glyphs)
    {
        var parts = new List<string>();
        if (item.DueDate is { } due)
        {
            var overdue = !item.IsCompleted && due.Date < today.Date;
            parts.Add($"{(overdue ? glyphs.Overdue + " " : string.Empty)}due {due:MM-dd}");
        }

        if (!string.IsNullOrEmpty(listName))
            parts.Add($"[{listName}]");

        return parts.Count == 0 ? string.Empty : "  " + string.Join("  ", parts);
    }

    /// <summary>Truncates to a display width in terminal columns, appending the ellipsis when cut.</summary>
    public static string FitToWidth(string text, int width, string ellipsis)
    {
        if (text.GetColumns() <= width)
            return text;

        var budget = width - ellipsis.GetColumns();
        if (budget <= 0)
            return ellipsis.GetColumns() <= width ? ellipsis : string.Empty;

        var builder = new System.Text.StringBuilder();
        var used = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var columns = rune.GetColumns();
            if (used + columns > budget)
                break;
            builder.Append(rune.ToString());
            used += columns;
        }

        return builder + ellipsis;
    }
}
