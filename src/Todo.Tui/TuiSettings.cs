using Microsoft.Extensions.Configuration;
using Todo.Tui.Input;
using Todo.Tui.Theming;

namespace Todo.Tui;

/// <summary>Color names or hex values for the TUI. Values come from a theme and the config file, never from code paths.</summary>
public sealed record TuiPalette(
    string Background,
    string Foreground,
    string Accent,
    string AccentText,
    string Selection,
    string SelectionText,
    string Warning,
    string Error,
    string Muted,
    string DialogBackground = "#c0c0c0",
    string DialogText = "Black")
{
    /// <summary>The names of the palette entries, as used in the <c>Tui:Palette</c> config section.</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        nameof(Background), nameof(Foreground), nameof(Accent), nameof(AccentText), nameof(Selection),
        nameof(SelectionText), nameof(Warning), nameof(Error), nameof(Muted), nameof(DialogBackground),
        nameof(DialogText)
    ];

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

    /// <summary>Reads only the entries present in the section (blank values are ignored).</summary>
    public static IReadOnlyDictionary<string, string> ReadOverrides(IConfigurationSection? section)
    {
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (section is null)
            return overrides;

        foreach (var key in Keys)
        {
            var value = section[key];
            if (!string.IsNullOrWhiteSpace(value))
                overrides[key] = value;
        }

        return overrides;
    }

    public TuiPalette WithOverrides(IReadOnlyDictionary<string, string> overrides)
    {
        string Pick(string key, string current) => overrides.TryGetValue(key, out var value) ? value : current;

        return new TuiPalette(
            Pick(nameof(Background), Background),
            Pick(nameof(Foreground), Foreground),
            Pick(nameof(Accent), Accent),
            Pick(nameof(AccentText), AccentText),
            Pick(nameof(Selection), Selection),
            Pick(nameof(SelectionText), SelectionText),
            Pick(nameof(Warning), Warning),
            Pick(nameof(Error), Error),
            Pick(nameof(Muted), Muted),
            Pick(nameof(DialogBackground), DialogBackground),
            Pick(nameof(DialogText), DialogText));
    }

    public static TuiPalette From(IConfigurationSection? section) => Default.WithOverrides(ReadOverrides(section));
}

/// <summary>
/// TUI options read from the <c>Tui</c> section of the application configuration. <see cref="Palette"/> is the
/// chosen theme's palette with any <c>Tui:Palette</c> entries applied on top.
/// </summary>
public sealed record TuiSettings(
    AltKeyMode AltKeys,
    TuiPalette Palette,
    bool NoColor,
    string ThemeName = TuiThemes.DefaultName,
    string? ThemeProblem = null)
{
    /// <summary>Individual palette entries from config; they win over the theme.</summary>
    public IReadOnlyDictionary<string, string> Overrides { get; init; } = new Dictionary<string, string>();

    public static TuiSettings From(IConfiguration? configuration, string? noColorEnvironmentValue)
    {
        var section = configuration?.GetSection("Tui");
        var altKeys = Enum.TryParse<AltKeyMode>(section?["AltKeys"], ignoreCase: true, out var mode)
            ? mode
            : AltKeyMode.Auto;

        // https://no-color.org: any non-empty value disables color.
        var noColor = !string.IsNullOrEmpty(noColorEnvironmentValue);
        var overrides = TuiPalette.ReadOverrides(section?.GetSection("Palette"));

        var requested = section?["Theme"];
        string? problem = null;
        if (!TuiThemes.TryGet(requested, out var themeName, out var themePalette) && !string.IsNullOrWhiteSpace(requested))
            problem = $"Unknown theme '{requested}'; using {themeName}. Available: {string.Join(", ", TuiThemes.Names)}.";

        return new TuiSettings(altKeys, themePalette.WithOverrides(overrides), noColor, themeName, problem)
        {
            Overrides = overrides
        };
    }

    /// <summary>The same settings with another built-in theme (config palette entries still apply on top).</summary>
    public TuiSettings WithTheme(string name)
    {
        TuiThemes.TryGet(name, out var themeName, out var themePalette);
        return this with { ThemeName = themeName, ThemeProblem = null, Palette = themePalette.WithOverrides(Overrides) };
    }
}
