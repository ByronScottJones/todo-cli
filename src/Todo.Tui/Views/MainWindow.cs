using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Todo.Core.Model;
using Todo.Tui.Input;
using Todo.Tui.Model;
using Todo.Tui.Theming;

namespace Todo.Tui.Views;

/// <summary>
/// Full-screen CUA layout: title bar, menu bar, context line, item list, status line and a key bar.
/// All behavior is reachable from the keyboard; the mouse works as a progressive enhancement.
/// </summary>
public sealed class MainWindow : Window
{
    public const int MinColumns = 60;
    public const int MinRows = 12;

    private readonly IApplication _app;
    private readonly TodoWorkspace _workspace;
    private TuiTheme _theme;
    private string _themeName;
    private readonly Func<string, TuiTheme>? _themeFactory;
    private readonly Action<string>? _saveTheme;
    private readonly AltKeyStatus _altStatus;
    private readonly TimeSpan _flashDuration;
    private readonly RowGlyphs _glyphs;
    private readonly Func<DateTime> _clock;
    private readonly Action<Action> _uiInvoke;

    private MenuBarItem[] _menuItems = [];
    private readonly Label _title;
    private readonly MenuBar _menu;
    private readonly Label _context;
    private readonly ItemListView _list;
    private readonly Label _empty;
    private readonly Label _status;
    private readonly KeyBar _keyBar;
    private readonly Label _tooSmall;

    private List<TodoItem> _rows = [];
    private bool _busy;
    private bool _loaded;

    public MainWindow(
        IApplication app,
        TodoWorkspace workspace,
        TuiTheme theme,
        AltKeyStatus altStatus,
        RowGlyphs glyphs,
        TimeSpan flashDuration,
        Func<DateTime>? clock = null,
        Action<Action>? uiInvoke = null,
        string startupNotice = "",
        bool alreadyLoaded = false,
        string themeName = TuiThemes.DefaultName,
        Func<string, TuiTheme>? themeFactory = null,
        Action<string>? saveTheme = null)
    {
        _app = app;
        _workspace = workspace;
        _theme = theme;
        _altStatus = altStatus;
        _glyphs = glyphs;
        _flashDuration = flashDuration;
        _clock = clock ?? (() => DateTime.Now);
        _uiInvoke = uiInvoke ?? (action => app.Invoke(action));
        _loaded = alreadyLoaded;
        _themeName = themeName;
        _themeFactory = themeFactory;
        _saveTheme = saveTheme;

        Title = string.Empty;
        BorderStyle = LineStyle.None;
        Width = Dim.Fill();
        Height = Dim.Fill();
        _title = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = 1, CanFocus = false };
        _menu = BuildMenu();
        _menu.Y = 1;
        _menu.Key = Key.F10;

        _context = new Label { X = 0, Y = 2, Width = Dim.Fill(), Height = 1, CanFocus = false };

        _keyBar = new KeyBar(theme);
        _keyBar.SetEntries(BuildKeyBarEntries());

        _status = new Label { X = 0, Y = Pos.Top(_keyBar) - 1, Width = Dim.Fill(), Height = 1, CanFocus = false };

        _list = new ItemListView
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill(),
            Height = Dim.Fill(Dim.Func(_ => _keyBar.RowCount + 1)),
            CanFocus = true
        };
        // The list has its own Emacs-style bindings (Ctrl+N/P/E/F/A ...) and type-ahead; claim our keys first.
        _list.PreviewKey = HandleKey;
        _list.Accepting += (_, e) =>
        {
            e.Handled = true;
            Execute(TuiCommand.Edit);
        };
        _list.ViewportChanged += (_, _) => OnListViewportChanged();
        _list.ValueChanged += (_, _) => UpdateContext();

        _empty = new Label { X = Pos.Center(), Y = 5, Width = Dim.Auto(), Height = 1, CanFocus = false };

        _tooSmall = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            CanFocus = false,
            TextAlignment = Alignment.Center,
            VerticalTextAlignment = Alignment.Center
        };
        Add(_title, _menu, _context, _list, _empty, _status, _keyBar, _tooSmall);

        ViewportChanged += (_, _) => OnWindowSized();
        KeyDown += (_, key) =>
        {
            if (HandleKey(key))
                key.Handled = true;
        };
        _workspace.Changed += () => Rebuild();
        Initialized += (_, _) => _list.SetFocus();
        ApplyTheme(theme);
        Rebuild();
        ShowStatus(startupNotice);
    }

    /// <summary>The item under the cursor, or null when the list is empty.</summary>
    public TodoItem? SelectedItem
    {
        get
        {
            var index = _list.SelectedItem;
            return index is { } i && i >= 0 && i < _rows.Count ? _rows[i] : null;
        }
    }

    public string StatusText => _status.Text;

    public string ContextText => _context.Text;

    public bool IsBusy => _busy;

    public IReadOnlyList<string> KeyBarEntries => _keyBar.EntryIds;

    public ListView ItemList => _list;

    public bool ShowsTooSmallWarning => _tooSmall.Visible;

    /// <summary>Loads data from the service and shows it. Used on startup and for F5.</summary>
    public Task LoadAsync(string busyMessage = "Loading...") => RunOperation(busyMessage, _workspace.LoadAsync, "Loaded.");

    private MenuBar BuildMenu()
    {
        MenuItem Item(string text, string help, Action action) => new(text, help, action, Key.Empty);
        string K(TuiCommand c) => CommandKeys.Text(c, _altStatus);

        _menuItems =
        [
            new MenuBarItem("_Todo", [Item("_About...", string.Empty, ShowAbout)]),
            new MenuBarItem("_File",
            [
                Item("_New Item...", K(TuiCommand.New), () => Execute(TuiCommand.New)),
                Item("_Refresh", "F5", () => RefreshData()),
                Item("E_xit", "F3", () => RequestStop())
            ]),
            new MenuBarItem("_Edit",
            [
                Item("_Modify Item...", K(TuiCommand.Edit), () => Execute(TuiCommand.Edit)),
                Item("_Complete / Reopen", K(TuiCommand.ToggleComplete), () => Execute(TuiCommand.ToggleComplete)),
                Item("_Important", K(TuiCommand.ToggleImportant), () => Execute(TuiCommand.ToggleImportant)),
                Item("_Remove Item...", K(TuiCommand.Remove), () => Execute(TuiCommand.Remove)),
                Item("Copy _Subject", "Ctrl+C", CopySubject)
            ]),
            new MenuBarItem("_View",
            [
                Item("_Lists...", K(TuiCommand.Lists), () => Execute(TuiCommand.Lists)),
                Item("_Search...", K(TuiCommand.Search), () => Execute(TuiCommand.Search)),
                Item("Show _Completed", K(TuiCommand.ShowCompleted), () => Execute(TuiCommand.ShowCompleted)),
                Item("T_heme...", K(TuiCommand.Theme), () => Execute(TuiCommand.Theme))
            ]),
            new MenuBarItem("_Help",
            [
                Item("_Help", "F1", ShowHelp),
                Item("_Keyboard Shortcuts", "F1", ShowHelp)
            ])
        ];
        return new MenuBar(_menuItems);
    }

    private IEnumerable<(string Id, string KeyText, string Label)> BuildKeyBarEntries()
    {
        yield return ("F1", "F1", "Help");
        yield return ("F3", "F3", "Exit");
        yield return ("F5", "F5", "Refresh");
        yield return ("F10", "F10", "Menu");
        foreach (var command in Enum.GetValues<TuiCommand>())
            yield return (command.ToString(), CommandKeys.Text(command, _altStatus), CommandKeys.Label(command));
    }

    // ---- keyboard ---------------------------------------------------------------------------------------

    /// <summary>Handles global and custom keys. Returns true when the key was consumed.</summary>
    internal bool HandleKey(Key key)
    {
        if (key == Key.F1) return Flash("F1", ShowHelp);
        if (key == Key.F3 || key == Key.X.WithAlt) return Flash("F3", () => RequestStop());
        if (key == Key.F5) return Flash("F5", () => RefreshData());
        if (key == Key.F7) return PageList(up: true);
        if (key == Key.F8) return PageList(up: false);

        foreach (var command in Enum.GetValues<TuiCommand>())
        {
            if (key == CommandKeys.AltKey(command) || key == CommandKeys.CtrlKey(command))
                return Flash(command.ToString(), () => Execute(command));
        }

        // Keys below act on the list only; text-entry controls and menus keep their own meaning for them.
        if (!_list.HasFocus)
            return false;

        if (key == Key.Delete) return Flash(nameof(TuiCommand.Remove), () => Execute(TuiCommand.Remove));
        if (key == Key.Space) return Flash(nameof(TuiCommand.ToggleComplete), () => Execute(TuiCommand.ToggleComplete));
        if (key == Key.C.WithCtrl) { CopySubject(); return true; }
        if (key.AsRune.Value == '?') return Flash(nameof(TuiCommand.Search), () => Execute(TuiCommand.Search));
        if (key == Key.CursorUp.WithShift) return PageList(up: true);
        if (key == Key.CursorDown.WithShift) return PageList(up: false);
        return false;
    }

    private bool Flash(string id, Action action)
    {
        _keyBar.Flash(_app, id, _flashDuration, action);
        return true;
    }

    private bool PageList(bool up)
    {
        if (up) _list.MovePageUp(false); else _list.MovePageDown(false);
        return true;
    }

    // ---- commands ---------------------------------------------------------------------------------------

    /// <summary>Runs a command. Public so menu items, keys and tests share one path.</summary>
    public void Execute(TuiCommand command)
    {
        switch (command)
        {
            case TuiCommand.New: NewItem(); break;
            case TuiCommand.Edit: EditItem(); break;
            case TuiCommand.ToggleComplete: WithSelection(item => RunOperation("Updating...", () => _workspace.ToggleCompleteAsync(item), item.IsCompleted ? "Reopened." : "Completed.")); break;
            case TuiCommand.ToggleImportant: WithSelection(item => RunOperation("Updating...", () => _workspace.ToggleImportantAsync(item), item.IsImportant ? "Unmarked." : "Marked important.")); break;
            case TuiCommand.Remove: RemoveItem(); break;
            case TuiCommand.Lists: ChooseList(); break;
            case TuiCommand.Search: Search(); break;
            case TuiCommand.Theme: ChooseTheme(); break;
            case TuiCommand.ShowCompleted:
                _workspace.SetShowCompleted(!_workspace.ShowCompleted);
                ShowStatus(_workspace.ShowCompleted ? "Showing completed items." : "Hiding completed items.");
                break;
        }
    }

    public Task RefreshData() => LoadAsync("Reloading...");

    private void WithSelection(Func<Task> operation) => _ = operation();

    private void WithSelection(Func<TodoItem, Task> operation)
    {
        if (SelectedItem is { } item)
            _ = operation(item);
        else
            ShowStatus("No item selected.");
    }

    private void NewItem()
    {
        if (_busy) { ShowStatus("Busy - please wait."); return; }
        if (_workspace.DefaultListId is not { } listId)
        {
            ShowStatus("There is no list to add the item to.");
            return;
        }

        using var dialog = new ItemDialog("New item", _workspace.Lists.ToList(), listId, null, _theme);
        _app.Run(dialog);
        var confirmed = dialog.Confirmed;
        var draft = dialog.Draft;
        _list.SetFocus();
        if (confirmed)
            _ = RunOperation("Adding...", () => _workspace.CreateAsync(draft), "Item added.");
    }

    private void EditItem()
    {
        if (_busy) { ShowStatus("Busy - please wait."); return; }
        if (SelectedItem is not { } item)
        {
            ShowStatus("No item selected.");
            return;
        }

        using var dialog = new ItemDialog("Modify item", _workspace.Lists.ToList(), item.ListId ?? string.Empty, item, _theme);
        _app.Run(dialog);
        var confirmed = dialog.Confirmed;
        var draft = dialog.Draft;
        _list.SetFocus();
        if (confirmed)
            _ = RunOperation("Saving...", () => _workspace.UpdateAsync(item, draft), "Item saved.");
    }

    private void RemoveItem()
    {
        if (_busy) { ShowStatus("Busy - please wait."); return; }
        if (SelectedItem is not { } item)
        {
            ShowStatus("No item selected.");
            return;
        }

        using var dialog = new MessageDialog("Remove item", $"Remove \"{item.Subject}\"?\nThis cannot be undone.", _theme, showCancel: true);
        _app.Run(dialog);
        var confirmed = dialog.Confirmed;
        _list.SetFocus();
        if (confirmed)
            _ = RunOperation("Removing...", () => _workspace.DeleteAsync(item), "Item removed.");
    }

    private void ChooseList()
    {
        var names = new List<string> { "All lists" };
        names.AddRange(_workspace.Lists.Select(l => l.Name ?? "(unnamed)"));
        var current = _workspace.SelectedListId is null
            ? 0
            : _workspace.Lists.ToList().FindIndex(l => l.Id == _workspace.SelectedListId) + 1;

        using var dialog = new ListPickerDialog("Choose list", names, Math.Max(0, current), _theme);
        _app.Run(dialog);
        var confirmed = dialog.Confirmed;
        var index = dialog.SelectedIndex;
        _list.SetFocus();
        if (!confirmed)
            return;

        _workspace.SelectList(index == 0 ? null : _workspace.Lists[index - 1].Id);
        ShowStatus($"Showing {_workspace.SelectedListName}.");
    }

    private void Search()
    {
        using var dialog = new TextPromptDialog("Search", "_Subject contains (blank clears the search):", _workspace.FilterText, _theme);
        _app.Run(dialog);
        var confirmed = dialog.Confirmed;
        var query = dialog.Query;
        _list.SetFocus();
        if (!confirmed)
            return;

        _workspace.SetFilter(query);
        ShowStatus(_workspace.FilterText.Length == 0 ? "Search cleared." : $"Searching for \"{_workspace.FilterText}\".");
    }

    /// <summary>Name of the theme currently applied.</summary>
    public string ThemeName => _themeName;

    /// <summary>Applies colors to every part of the window. Dialogs opened later pick up the current theme.</summary>
    internal void ApplyTheme(TuiTheme theme)
    {
        _theme = theme;
        // Register the colors under Terminal.Gui's built-in scheme names too. Views I do not style explicitly (menu
        // dropdowns, message boxes) look their scheme up by name and would otherwise keep the framework defaults.
        SchemeManager.AddScheme("Base", theme.Base);
        SchemeManager.AddScheme("Menu", theme.Accent);
        SchemeManager.AddScheme("Dialog", theme.Dialog);
        SchemeManager.AddScheme("Error", theme.DialogError);
        SetScheme(theme.Base);
        _title.SetScheme(theme.Accent);
        _menu.SetScheme(theme.Accent);
        foreach (var item in _menuItems)
            item.PopoverMenu?.SetScheme(theme.Accent);
        _tooSmall.SetScheme(theme.Error);
        _keyBar.SetTheme(theme);
        SetNeedsDraw();
    }

    private void SwitchTheme(string name)
    {
        if (_themeFactory is null)
            return;
        _themeName = name;
        ApplyTheme(_themeFactory(name));
    }

    private void ChooseTheme()
    {
        if (_themeFactory is null)
        {
            ShowStatus("Themes are not available.");
            return;
        }

        var names = TuiThemes.Names;
        var original = _themeName;
        var current = names.ToList().FindIndex(n => string.Equals(n, original, StringComparison.OrdinalIgnoreCase));

        using var dialog = new ListPickerDialog("Choose theme", names, Math.Max(0, current), _theme);
        // Preview on the window behind the dialog while the highlight moves; Cancel restores the original.
        dialog.SelectionChanged += index =>
        {
            if (index >= 0 && index < names.Count)
                SwitchTheme(names[index]);
        };
        _app.Run(dialog);
        var confirmed = dialog.Confirmed;
        var chosen = names[Math.Clamp(dialog.SelectedIndex, 0, names.Count - 1)];
        _list.SetFocus();

        if (!confirmed)
        {
            SwitchTheme(original);
            ShowStatus("Theme unchanged.");
            return;
        }

        SwitchTheme(chosen);
        try
        {
            _saveTheme?.Invoke(chosen);
            ShowStatus($"Theme: {chosen}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"! Theme applied, but could not be saved: {ex.Message}");
        }
    }

    private void CopySubject()
    {
        if (SelectedItem is not { Subject: { Length: > 0 } subject })
        {
            ShowStatus("No item selected.");
            return;
        }

        var copied = _app.Clipboard?.TrySetClipboardData(subject) ?? false;
        ShowStatus(copied ? "Subject copied to the clipboard." : "The clipboard is not available in this terminal.");
    }

    private void ShowHelp()
    {
        using var dialog = new HelpDialog(_altStatus, _theme);
        _app.Run(dialog);
        _list.SetFocus();
    }

    private void ShowAbout()
    {
        using var dialog = new MessageDialog(
            "About Todo", "Todo CLI\nA keyboard-driven terminal UI for Microsoft To Do.", _theme, showCancel: false);
        _app.Run(dialog);
        _list.SetFocus();
    }

    // ---- background work --------------------------------------------------------------------------------

    /// <summary>
    /// Runs a service call off the UI thread, then refreshes the view on the UI thread. Failures are reported
    /// in the status line and an error dialog; the previous data stays on screen.
    /// </summary>
    internal Task RunOperation(string busyMessage, Func<Task> work, string doneMessage)
    {
        if (_busy)
        {
            ShowStatus("Busy - please wait.");
            return Task.CompletedTask;
        }

        _busy = true;
        ShowStatus(busyMessage);
        return Task.Run(async () =>
        {
            string? error = null;
            try
            {
                await work().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            _uiInvoke(() =>
            {
                _busy = false;
                _loaded = _loaded || error is null;
                if (error is null)
                {
                    ShowStatus(doneMessage);
                    return;
                }

                ShowStatus("! " + error);
                using var dialog = new MessageDialog("Error", error, _theme, showCancel: false, isError: true);
                _app.Run(dialog);
                _list.SetFocus();
            });
        });
    }

    // ---- rendering --------------------------------------------------------------------------------------

    private void Rebuild()
    {
        var keepId = SelectedItem?.Id;
        _rows = _workspace.VisibleItems.ToList();
        RenderRows();
        var index = keepId is null ? -1 : _rows.FindIndex(i => i.Id == keepId);
        if (_rows.Count > 0)
            _list.SelectedItem = Math.Max(0, index);
        UpdateContext();
        UpdateEmptyState();
    }

    private void RenderRows()
    {
        var width = Math.Max(10, _list.Viewport.Width);
        var showListName = _workspace.SelectedListId is null && _workspace.Lists.Count > 1;
        var lines = _rows.Select(item => ItemFormatter.FormatRow(
            item, showListName ? _workspace.ListName(item.ListId) : null, width, _clock(), _glyphs));
        var selected = _list.SelectedItem;
        _list.SetSource(new ObservableCollection<string>(lines));
        if (selected is { } s && _rows.Count > 0)
            _list.SelectedItem = Math.Clamp(s, 0, _rows.Count - 1);
    }

    private void OnListViewportChanged()
    {
        // Row text is padded to the list width, so a width change needs a re-render.
        if (_list.Viewport.Width > 0 && _rows.Count > 0 && _lastRenderedWidth != _list.Viewport.Width)
        {
            _lastRenderedWidth = _list.Viewport.Width;
            RenderRows();
        }

        UpdateContext();
    }

    private int _lastRenderedWidth;

    private void OnWindowSized()
    {
        var tooSmall = Viewport.Width < MinColumns || Viewport.Height < MinRows;
        _tooSmall.Text = $"Terminal too small: {Viewport.Width}x{Viewport.Height}.\n" +
                         $"Todo needs at least {MinColumns}x{MinRows}.\nResize the window to continue.";
        _tooSmall.Visible = tooSmall;
        foreach (var view in new View[] { _title, _menu, _context, _list, _empty, _status, _keyBar })
            view.Visible = !tooSmall;

        _keyBar.Reflow(Math.Max(1, Viewport.Width));
        UpdateContext();
    }

    private void UpdateContext()
    {
        _title.Text = $" Todo - Microsoft To Do  |  {_workspace.SelectedListName}";

        var scroll = ScrollWindow.Compute(_rows.Count, _list.Viewport.Y, _list.Viewport.Height);
        var parts = new List<string>
        {
            $"List: {_workspace.SelectedListName}",
            $"{_rows.Count} shown, {_workspace.OpenCount} open of {_workspace.TotalCount}",
            _workspace.ShowCompleted ? "incl. completed" : "open only"
        };
        if (_workspace.FilterText.Length > 0)
            parts.Add($"search: \"{_workspace.FilterText}\"");
        var indicator = scroll.Describe();
        if (indicator.Length > 0)
            parts.Add(indicator);
        _context.Text = " " + string.Join("  |  ", parts);
    }

    private void UpdateEmptyState()
    {
        if (_rows.Count > 0)
        {
            _empty.Visible = !_tooSmall.Visible && false;
            return;
        }

        _empty.Text = !_loaded
            ? "Loading..."
            : _workspace.FilterText.Length > 0
                ? "No items match the search."
                : _workspace.TotalCount == 0
                    ? $"No items yet. Press {CommandKeys.Text(TuiCommand.New, _altStatus)} to add one."
                    : $"No open items. Press {CommandKeys.Text(TuiCommand.ShowCompleted, _altStatus)} to show completed ones.";
        _empty.Visible = !_tooSmall.Visible;
    }

    private void ShowStatus(string message)
    {
        _status.Text = " " + message;
        UpdateEmptyState();
    }
}
