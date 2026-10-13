using Microsoft.Extensions.Configuration;
using Todo.Tui.Input;

namespace Todo.Tui;

/// <summary>Color names for the TUI. Values come from the application config file, never from code.</summary>
public sealed record TuiPalette(
    string Background,
    string Foreground,
    string Accent,
    string AccentText,
    string Selection,
    string SelectionText,
    string Warning,
    string Error,
    string Muted)
{
    /// <summary>Blue/yellow/white on black: distinguishable for common color-vision deficiencies.</summary>
    public static readonly TuiPalette Default = new(
        Background: "Black",
        Foreground: "White",
        Accent: "Blue",
        AccentText: "White",
        Selection: "White",
        SelectionText: "Black",
        Warning: "Yellow",
        Error: "BrightRed",
        Muted: "DarkGray");

    public static TuiPalette From(IConfigurationSection? section)
    {
        string Read(string key, string fallback) =>
            section is not null && !string.IsNullOrWhiteSpace(section[key]) ? section[key]! : fallback;

        return new TuiPalette(
            Read(nameof(Background), Default.Background),
            Read(nameof(Foreground), Default.Foreground),
            Read(nameof(Accent), Default.Accent),
            Read(nameof(AccentText), Default.AccentText),
            Read(nameof(Selection), Default.Selection),
            Read(nameof(SelectionText), Default.SelectionText),
            Read(nameof(Warning), Default.Warning),
            Read(nameof(Error), Default.Error),
            Read(nameof(Muted), Default.Muted));
    }
}

/// <summary>TUI options read from the <c>Tui</c> section of the application configuration.</summary>
public sealed record TuiSettings(AltKeyMode AltKeys, TuiPalette Palette, bool NoColor)
{
    public static TuiSettings From(IConfiguration? configuration, string? noColorEnvironmentValue)
    {
        var section = configuration?.GetSection("Tui");
        var altKeys = Enum.TryParse<AltKeyMode>(section?["AltKeys"], ignoreCase: true, out var mode)
            ? mode
            : AltKeyMode.Auto;

        // https://no-color.org: any non-empty value disables color.
        var noColor = !string.IsNullOrEmpty(noColorEnvironmentValue);
        return new TuiSettings(altKeys, TuiPalette.From(section?.GetSection("Palette")), noColor);
    }
}
