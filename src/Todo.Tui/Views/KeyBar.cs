using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Todo.Tui.Views;

/// <summary>
/// Bottom panel listing the commands available on the current screen. It wraps onto extra rows when the
/// terminal is narrow, and can flash an entry to acknowledge a key press before the command runs.
/// </summary>
public sealed class KeyBar : View
{
    private sealed record Entry(string Id, Label Key, Label Text);

    private readonly TuiTheme _theme;
    private readonly List<Entry> _entries = [];

    public KeyBar(TuiTheme theme)
    {
        _theme = theme;
        X = 0;
        Y = Pos.AnchorEnd();
        Width = Dim.Fill();
        Height = 1;
        CanFocus = false;
        SetScheme(theme.Accent);
    }

    public int RowCount { get; private set; } = 1;

    public IReadOnlyList<string> EntryIds => _entries.Select(e => e.Id).ToList();

    /// <summary>Replaces the entries. Each is an id, the key text (e.g. "Alt+N") and a short label.</summary>
    public void SetEntries(IEnumerable<(string Id, string KeyText, string Label)> entries)
    {
        foreach (var entry in _entries)
        {
            Remove(entry.Key);
            Remove(entry.Text);
            entry.Key.Dispose();
            entry.Text.Dispose();
        }

        _entries.Clear();
        foreach (var (id, keyText, label) in entries)
        {
            var key = new Label { Text = keyText, CanFocus = false };
            key.SetScheme(_theme.KeyHint);
            var text = new Label { Text = label, CanFocus = false };
            Add(key, text);
            _entries.Add(new Entry(id, key, text));
        }

        Reflow(Math.Max(1, Viewport.Width));
    }

    /// <summary>Packs entries into as many rows as the width requires; returns the row count.</summary>
    public int Reflow(int width)
    {
        var x = 0;
        var row = 0;
        foreach (var entry in _entries)
        {
            var keyWidth = entry.Key.Text.Length;
            var textWidth = entry.Text.Text.Length;
            var needed = keyWidth + 1 + textWidth;
            if (x > 0 && x + needed > width)
            {
                row++;
                x = 0;
            }

            entry.Key.X = x;
            entry.Key.Y = row;
            entry.Key.Width = keyWidth;
            entry.Text.X = x + keyWidth + 1;
            entry.Text.Y = row;
            entry.Text.Width = textWidth;
            x += needed + 2;
        }

        RowCount = row + 1;
        Height = RowCount;
        return RowCount;
    }

    /// <summary>Highlights the entry, waits <paramref name="duration"/>, then restores it and calls <paramref name="then"/>.</summary>
    public void Flash(IApplication app, string id, TimeSpan duration, Action then)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == id);
        if (entry is null || duration <= TimeSpan.Zero)
        {
            then();
            return;
        }

        entry.Key.SetScheme(_theme.Accent);
        entry.Text.SetScheme(_theme.KeyHint);
        SetNeedsDraw();
        app.AddTimeout(duration, () =>
        {
            entry.Key.SetScheme(_theme.KeyHint);
            entry.Text.SetScheme(_theme.Accent);
            SetNeedsDraw();
            then();
            return false;
        });
    }
}
