using Terminal.Gui.Drawing;
using Todo.Tui.Theming;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Todo.Tui.Views;

/// <summary>
/// Terminal.Gui color schemes built from a palette. Every foreground is checked against the background it is drawn
/// on and, if the pair is too faint, moved in lightness until it reaches WCAG AA (4.5:1; 3:1 for deliberately dimmed
/// text), so no palette can produce unreadable text. With NO_COLOR set, schemes use only the terminal's default colors
/// and rely on reverse/bold/underline styles instead.
/// </summary>
public sealed class TuiTheme
{
    private TuiTheme(string name, Scheme baseScheme, Scheme accent, Scheme dialog, Scheme keyHint, Scheme error,
        Scheme dialogError, IReadOnlyList<string> problems)
    {
        Name = name;
        Base = baseScheme;
        Accent = accent;
        Dialog = dialog;
        KeyHint = keyHint;
        Error = error;
        DialogError = dialogError;
        Problems = problems;
    }

    public string Name { get; }

    /// <summary>Main workspace.</summary>
    public Scheme Base { get; }

    /// <summary>Title bar, menu bar and key bar.</summary>
    public Scheme Accent { get; }

    /// <summary>Dialog surface; its focus color is the accent, so a highlighted button stands out.</summary>
    public Scheme Dialog { get; }

    /// <summary>Inverted look used for key labels and the command flash.</summary>
    public Scheme KeyHint { get; }

    /// <summary>Error text on the main background; always accompanied by a "!" so color is never the only cue.</summary>
    public Scheme Error { get; }

    /// <summary>Error text on the dialog surface.</summary>
    public Scheme DialogError { get; }

    /// <summary>Palette entries that could not be parsed and were replaced by defaults.</summary>
    public IReadOnlyList<string> Problems { get; }

    public static TuiTheme Create(TuiSettings settings)
    {
        if (settings.NoColor)
            return CreateMonochrome(settings.ThemeName);

        var problems = new List<string>();
        if (settings.ThemeProblem is not null)
            problems.Add(settings.ThemeProblem);

        Color Parse(string name, string value, string fallback)
        {
            if (Color.TryParse(value, out Color? parsed) && parsed is { } color)
                return color;

            problems.Add($"Palette '{name}' has invalid color '{value}'; using {fallback}.");
            return Color.TryParse(fallback, out Color? fb) && fb is { } fbColor ? fbColor : default;
        }

        var p = settings.Palette;
        var d = TuiPalette.Default;
        var background = Parse(nameof(p.Background), p.Background, d.Background);
        var foreground = Parse(nameof(p.Foreground), p.Foreground, d.Foreground);
        var accent = Parse(nameof(p.Accent), p.Accent, d.Accent);
        var accentText = Parse(nameof(p.AccentText), p.AccentText, d.AccentText);
        var selection = Parse(nameof(p.Selection), p.Selection, d.Selection);
        var selectionText = Parse(nameof(p.SelectionText), p.SelectionText, d.SelectionText);
        var warning = Parse(nameof(p.Warning), p.Warning, d.Warning);
        var error = Parse(nameof(p.Error), p.Error, d.Error);
        var muted = Parse(nameof(p.Muted), p.Muted, d.Muted);
        var dialogBackground = Parse(nameof(p.DialogBackground), p.DialogBackground, d.DialogBackground);
        var dialogText = Parse(nameof(p.DialogText), p.DialogText, d.DialogText);

        var black = new Color(0, 0, 0);
        var white = new Color(255, 255, 255);

        // Text colored `preferred` drawn on `bg`, made readable if needed (hue is kept where possible).
        Color Readable(Color preferred, Color bg, double minimum = ColorMath.TextContrast) =>
            ColorMath.EnsureContrast(preferred, bg, minimum, foreground, background, white, black);

        Attribute Pair(Color fg, Color bg, TextStyle style = TextStyle.None, double minimum = ColorMath.TextContrast) =>
            new(Readable(fg, bg, minimum), bg, style);

        // One surface: normal text, hotkeys (underlined so they differ from normal text even when the color
        // has to fall back), the focus/active highlight, editable fields and dimmed text.
        Scheme Surface(Color bg, Color text, Color focusBg, Color focusText, Color fieldBg) => new()
        {
            Normal = Pair(text, bg),
            HotNormal = Pair(warning, bg, TextStyle.Underline),
            Focus = Pair(focusText, focusBg),
            HotFocus = Pair(warning, focusBg, TextStyle.Underline),
            Active = Pair(focusText, focusBg),
            HotActive = Pair(warning, focusBg, TextStyle.Underline),
            Highlight = Pair(focusText, focusBg),
            Editable = Pair(foreground, fieldBg),
            ReadOnly = Pair(muted, bg, minimum: ColorMath.DimmedContrast),
            Disabled = Pair(muted, bg, minimum: ColorMath.DimmedContrast)
        };

        Scheme TextOn(Color bg, Color text, Color fieldBg) => new()
        {
            Normal = Pair(text, bg),
            HotNormal = Pair(text, bg, TextStyle.Underline),
            Focus = Pair(selectionText, selection),
            HotFocus = Pair(selectionText, selection, TextStyle.Underline),
            Active = Pair(selectionText, selection),
            HotActive = Pair(selectionText, selection, TextStyle.Underline),
            Highlight = Pair(selectionText, selection),
            Editable = Pair(foreground, fieldBg),
            ReadOnly = Pair(muted, bg, minimum: ColorMath.DimmedContrast),
            Disabled = Pair(muted, bg, minimum: ColorMath.DimmedContrast)
        };

        var baseScheme = Surface(background, foreground, selection, selectionText, background);
        // Menu bar and bars: accent background; the highlighted menu entry uses the selection color.
        var accentScheme = new Scheme
        {
            Normal = Pair(accentText, accent),
            HotNormal = Pair(accentText, accent, TextStyle.Underline),
            Focus = Pair(selectionText, selection),
            HotFocus = Pair(selectionText, selection, TextStyle.Underline),
            Active = Pair(selectionText, selection),
            HotActive = Pair(selectionText, selection, TextStyle.Underline),
            Highlight = Pair(selectionText, selection),
            Editable = Pair(foreground, background),
            ReadOnly = Pair(accentText, accent, TextStyle.Faint, ColorMath.DimmedContrast),
            Disabled = Pair(accentText, accent, TextStyle.Faint, ColorMath.DimmedContrast)
        };

        // Dialogs: their own surface; a focused button or row takes the accent so it is unmistakable. Entry fields
        // are inset (drawn on the main background) so they read as wells in the dialog surface.
        var dialogScheme = Surface(dialogBackground, dialogText, accent, accentText, background);

        // Key labels: inverted bar colors.
        var keyHint = new Scheme
        {
            Normal = Pair(accent, accentText),
            HotNormal = Pair(accent, accentText, TextStyle.Underline),
            Focus = Pair(accent, accentText),
            HotFocus = Pair(accent, accentText, TextStyle.Underline),
            Active = Pair(accent, accentText),
            HotActive = Pair(accent, accentText, TextStyle.Underline),
            Highlight = Pair(accent, accentText),
            Editable = Pair(accent, accentText),
            ReadOnly = Pair(accent, accentText),
            Disabled = Pair(accent, accentText)
        };

        return new TuiTheme(
            settings.ThemeName,
            baseScheme,
            accentScheme,
            dialogScheme,
            keyHint,
            TextOn(background, error, background),
            TextOn(dialogBackground, error, background),
            problems);
    }

    private static TuiTheme CreateMonochrome(string name)
    {
        Scheme Make(TextStyle normal, TextStyle focus) => new()
        {
            Normal = new Attribute(Color.None, Color.None, normal),
            HotNormal = new Attribute(Color.None, Color.None, TextStyle.Underline),
            Focus = new Attribute(Color.None, Color.None, focus),
            HotFocus = new Attribute(Color.None, Color.None, focus | TextStyle.Underline),
            Active = new Attribute(Color.None, Color.None, focus),
            HotActive = new Attribute(Color.None, Color.None, focus | TextStyle.Underline),
            Highlight = new Attribute(Color.None, Color.None, focus),
            Editable = new Attribute(Color.None, Color.None, TextStyle.Underline),
            ReadOnly = new Attribute(Color.None, Color.None, TextStyle.Faint),
            Disabled = new Attribute(Color.None, Color.None, TextStyle.Faint)
        };

        var plain = Make(TextStyle.None, TextStyle.Reverse);
        var bar = Make(TextStyle.Reverse, TextStyle.Bold);
        var err = Make(TextStyle.Bold, TextStyle.Reverse);
        return new TuiTheme(name, plain, bar, plain, bar, err, err, []);
    }
}
