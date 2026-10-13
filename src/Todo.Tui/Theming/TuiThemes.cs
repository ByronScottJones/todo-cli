using Terminal.Gui.Drawing;

namespace Todo.Tui.Theming;

/// <summary>
/// Built-in color themes. Background, foreground, the two highlight colors and the comment color come from each
/// theme's well-known editor palette. The remaining roles (dialog surface, warning, error, readable muted text) are
/// derived in OKLCH from those colors so they harmonize with the theme; see <see cref="Derive"/>.
/// </summary>
public static class TuiThemes
{
    public const string DefaultName = "Default";

    private sealed record Theme(string Name, TuiPalette Palette, string[] Aliases);

    // Role mapping: Accent = the theme's "function" color (bars, dialog highlight); Selection = its "keyword"
    // color (selected row, menu highlight); Muted = its comment color. Bars and highlights use the background color
    // for their text so the bright colors stay legible.
    private static readonly Theme[] All =
    [
        new(DefaultName, TuiPalette.Default, ["default"]),
        Make("One Dark Pro", "#282c34", "#abb2bf", keyword: "#e06c75", function: "#61afef", comment: "#5c6370", ["onedark", "onedarkpro"]),
        Make("Dracula", "#282a36", "#f8f8f2", keyword: "#50fa7b", function: "#bd93f9", comment: "#6272a4", ["dracula", "draculaofficial"]),
        Make("Tokyo Night", "#1a1b26", "#c0caf5", keyword: "#bb9af7", function: "#7aa2f7", comment: "#565f89", ["tokyonight", "tokyo"]),
        Make("Catppuccin Mocha", "#1e1e2e", "#cdd6f4", keyword: "#cba6f7", function: "#89b4fa", comment: "#6c7086", ["catppuccin", "catppuccinmocha", "mocha"]),
        Make("GitHub Dark", "#0d1117", "#c9d1d9", keyword: "#ff7b72", function: "#79c0ff", comment: "#8b949e", ["github", "githubdark", "githubdarkdefault"]),
        Make("Night Owl", "#011627", "#d6deeb", keyword: "#c792ea", function: "#82aaff", comment: "#637777", ["nightowl"]),
        Make("Gruvbox Material", "#282828", "#dfbf8e", keyword: "#ea6962", function: "#a9b665", comment: "#928374", ["gruvbox", "gruvboxmaterial"]),
        Make("SynthWave '84", "#262335", "#ffffff", keyword: "#fede5d", function: "#ff7edb", comment: "#848bbd", ["synthwave", "synthwave84"])
    ];

    /// <summary>Display names in menu order, starting with the default.</summary>
    public static IReadOnlyList<string> Names { get; } = All.Select(t => t.Name).ToList();

    /// <summary>Finds a theme by display name or alias, ignoring case, spaces, hyphens and punctuation.</summary>
    public static bool TryGet(string? name, out string displayName, out TuiPalette palette)
    {
        var key = Normalize(name);
        var theme = key.Length == 0
            ? null
            : All.FirstOrDefault(t => Normalize(t.Name) == key || t.Aliases.Contains(key));
        displayName = theme?.Name ?? DefaultName;
        palette = theme?.Palette ?? TuiPalette.Default;
        return theme is not null;
    }

    public static string Normalize(string? name) =>
        new((name ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>The roles that are not part of a theme's published palette.</summary>
    public readonly record struct Derived(Color Surface, Color Warning, Color Error, Color Muted);

    /// <summary>
    /// Derives the missing roles in OKLCH so they belong to the theme.
    /// <list type="bullet">
    /// <item>Surface (dialogs): the background moved 0.05 in lightness toward the foreground, keeping hue and chroma.</item>
    /// <item>Warning and error: amber (hue 85) and red (hue 25) with the average lightness and chroma of the theme's
    /// two highlight colors, so they carry the same visual weight. Error is placed 0.12 in lightness away from
    /// warning, so the two differ in brightness as well as hue, which keeps them distinct for red-green color
    /// blindness. Both are then raised to text contrast on the background and on the surface.</item>
    /// <item>Muted: the comment color with only enough extra lightness to be readable as dimmed text (3:1).</item>
    /// </list>
    /// </summary>
    public static Derived Derive(Color background, Color foreground, Color keyword, Color function, Color comment)
    {
        var bg = ColorMath.ToOklch(background);
        var darkTheme = ColorMath.Luminance(background) < 0.5;

        var surfaceL = bg.L + (darkTheme ? 0.05 : -0.05);
        var surface = ColorMath.FromOklch(surfaceL, bg.C, bg.H);

        var kw = ColorMath.ToOklch(keyword);
        var fn = ColorMath.ToOklch(function);
        // Keep chroma in a band where hue is recognizable but colors do not glare.
        var chroma = Math.Clamp((kw.C + fn.C) / 2, 0.10, 0.16);
        var lightness = Math.Clamp((kw.L + fn.L) / 2, darkTheme ? 0.72 : 0.40, darkTheme ? 0.88 : 0.58);

        var warningL = darkTheme ? Math.Min(0.92, lightness + 0.06) : Math.Max(0.35, lightness - 0.06);
        var errorL = darkTheme ? warningL - 0.12 : warningL + 0.12;
        var warning = ColorMath.FromOklch(warningL, chroma, 85);
        var error = ColorMath.FromOklch(errorL, chroma, 25);

        // They appear on both the main background and the dialog surface; satisfy the harder of the two.
        warning = ColorMath.EnsureContrast(ColorMath.EnsureContrast(warning, background, ColorMath.TextContrast), surface, ColorMath.TextContrast);
        error = ColorMath.EnsureContrast(ColorMath.EnsureContrast(error, background, ColorMath.TextContrast), surface, ColorMath.TextContrast);
        // Readability fixes may have pulled the two together (red needs extra lightness on dark backgrounds). Restore a
        // brightness gap by moving the amber, which tolerates extreme lightness, away from the red.
        const double MinGap = 0.10;
        var errorActual = ColorMath.ToOklch(error).L;
        var warningActual = ColorMath.ToOklch(warning).L;
        if (Math.Abs(warningActual - errorActual) < MinGap)
        {
            var target = darkTheme ? Math.Min(0.97, errorActual + MinGap) : Math.Max(0.30, errorActual - MinGap);
            warning = ColorMath.FromOklch(target, chroma, 85);
            warning = ColorMath.EnsureContrast(ColorMath.EnsureContrast(warning, background, ColorMath.TextContrast), surface, ColorMath.TextContrast);
        }

        var muted = ColorMath.EnsureContrast(ColorMath.EnsureContrast(comment, background, ColorMath.DimmedContrast), surface, ColorMath.DimmedContrast);

        return new Derived(surface, warning, error, muted);
    }

    private static Color Parse(string hex) =>
        Color.TryParse(hex, out Color? parsed) && parsed is { } color ? color : default;

    private static Theme Make(string name, string background, string foreground, string keyword, string function,
        string comment, string[] aliases)
    {
        var derived = Derive(Parse(background), Parse(foreground), Parse(keyword), Parse(function), Parse(comment));
        return new Theme(name, new TuiPalette(
            Background: background,
            Foreground: foreground,
            Accent: function,
            AccentText: background,
            Selection: keyword,
            SelectionText: background,
            Warning: ColorMath.ToHex(derived.Warning),
            Error: ColorMath.ToHex(derived.Error),
            Muted: ColorMath.ToHex(derived.Muted),
            DialogBackground: ColorMath.ToHex(derived.Surface),
            DialogText: foreground), aliases);
    }
}
