using Terminal.Gui.Drawing;
using Todo.Tui;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Todo.Tui.Views;

/// <summary>
/// Terminal.Gui color schemes built from the configured palette. With NO_COLOR set, schemes use only the
/// terminal's default colors and rely on reverse/bold text styles instead.
/// </summary>
public sealed class TuiTheme
{
    private TuiTheme(Scheme baseScheme, Scheme accent, Scheme dialog, Scheme keyHint, Scheme error, IReadOnlyList<string> problems)
    {
        Base = baseScheme;
        Accent = accent;
        Dialog = dialog;
        KeyHint = keyHint;
        Error = error;
        Problems = problems;
    }

    /// <summary>Main workspace.</summary>
    public Scheme Base { get; }

    /// <summary>Title bar, menu bar and key bar.</summary>
    public Scheme Accent { get; }

    public Scheme Dialog { get; }

    /// <summary>Inverted look used for key labels and the command flash.</summary>
    public Scheme KeyHint { get; }

    /// <summary>Validation and error text; always accompanied by a "!" so color is never the only cue.</summary>
    public Scheme Error { get; }

    /// <summary>Palette entries that could not be parsed and were replaced by defaults.</summary>
    public IReadOnlyList<string> Problems { get; }

    public static TuiTheme Create(TuiSettings settings)
    {
        if (settings.NoColor)
            return CreateMonochrome();

        var problems = new List<string>();
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

        Scheme Make(Color fg, Color bg, Color hot, Color focusFg, Color focusBg) => new()
        {
            Normal = new Attribute(fg, bg),
            HotNormal = new Attribute(hot, bg, TextStyle.Underline),
            Focus = new Attribute(focusFg, focusBg),
            HotFocus = new Attribute(hot == focusBg ? focusFg : hot, focusBg, TextStyle.Underline),
            Active = new Attribute(focusFg, focusBg),
            HotActive = new Attribute(focusFg, focusBg, TextStyle.Underline),
            Highlight = new Attribute(focusFg, focusBg),
            Editable = new Attribute(foreground, muted),
            ReadOnly = new Attribute(muted, bg),
            Disabled = new Attribute(muted, bg)
        };

        return new TuiTheme(
            Make(foreground, background, warning, selectionText, selection),
            Make(accentText, accent, warning, accent, accentText),
            Make(foreground, background, warning, selectionText, selection),
            Make(accent, accentText, accent, accentText, accent),
            Make(error, background, error, selectionText, selection),
            problems);
    }

    private static TuiTheme CreateMonochrome()
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
        return new TuiTheme(plain, Make(TextStyle.Reverse, TextStyle.Bold), plain, Make(TextStyle.Reverse, TextStyle.Bold),
            Make(TextStyle.Bold, TextStyle.Reverse), []);
    }
}
