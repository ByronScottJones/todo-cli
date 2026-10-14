using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Todo.Tui;
using Todo.Tui.Input;
using Todo.Tui.Model;
using Todo.Tui.Views;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Todo.CLI.Tests.Tui;

/// <summary>
/// Runs the real <see cref="MainWindow"/> headlessly (ANSI driver, fixed screen size) and drives it with scripted
/// steps. Steps run from timers so they also fire inside the nested loop of an open modal dialog; a step that
/// returns false is retried until it passes or the session deadline expires.
/// </summary>
internal sealed class UiSession : IDisposable
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(15);
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);

    private readonly LinkedList<Func<bool>> _steps = new();
    private readonly Dictionary<string, string> _screens = new();
    private readonly DateTime _started = DateTime.UtcNow;
    private string? _failure;
    private bool _stopping;

    private UiSession(IApplication app, MainWindow window, FakeTodoService service, TodoWorkspace workspace)
    {
        App = app;
        Window = window;
        Service = service;
        Workspace = workspace;
    }

    public IApplication App { get; }

    public List<string> SavedThemes { get; init; } = [];

    public TuiSettings Settings { get; init; } = TuiSettings.From(null, null);

    private readonly Dictionary<string, object?> _samples = new();

    public MainWindow Window { get; }

    public FakeTodoService Service { get; }

    public TodoWorkspace Workspace { get; }

    public static UiSession Create(
        FakeTodoService? service = null,
        int width = 100,
        int height = 24,
        AltKeyStatus alt = AltKeyStatus.Confirmed,
        RowGlyphs? glyphs = null,
        bool noColor = false,
        string themeName = "Default",
        Action<string>? saveTheme = null)
    {
        service ??= new FakeTodoService();
        var workspace = new TodoWorkspace(service, service);
        workspace.LoadAsync().GetAwaiter().GetResult();

        var app = Application.Create();
        app.Init("ansi");
        app.Driver!.SetScreenSize(width, height);
        TuiKeyboard.Configure(app);
        var settings = TuiSettings.From(null, noColor ? "1" : null).WithTheme(themeName);
        var theme = TuiTheme.Create(settings);
        var saved = new List<string>();
        var window = new MainWindow(
            app, workspace, theme, alt, glyphs ?? RowGlyphs.Unicode, TimeSpan.Zero,
            () => new DateTime(2026, 11, 1), alreadyLoaded: true,
            themeName: settings.ThemeName,
            themeFactory: name => TuiTheme.Create(settings.WithTheme(name)),
            saveTheme: saveTheme ?? saved.Add);
        return new UiSession(app, window, service, workspace) { SavedThemes = saved, Settings = settings };
    }

    public string Screen(string name) => _screens[name];

    // ---- script building --------------------------------------------------------------------------------

    public UiSession Press(params string[] keys) => Step(() =>
    {
        foreach (var key in keys)
            App.Keyboard.RaiseKeyDownEvent(ParseKey(key));
        return true;
    });

    public UiSession Type(string text) => Step(() =>
    {
        foreach (var c in text)
            App.Keyboard.RaiseKeyDownEvent(new Key(c));
        return true;
    });

    public UiSession Shot(string name) => Step(() =>
    {
        // The driver buffer is only refreshed on the next loop iteration; force it so the capture is current.
        App.LayoutAndDraw(true);
        var focused = App.Navigation?.GetFocused();
        _screens[name] = App.Driver!.ToString() + $"\n[focus: {focused?.GetType().Name}]\n[top: {App.TopRunnable?.GetType().Name}]";
        return true;
    });

    /// <summary>Reads something from the driver (cell colors, text positions) after forcing a fresh draw.</summary>
    public UiSession Sample(string name, Func<IDriver, object?> read) => Step(() =>
    {
        App.LayoutAndDraw(true);
        _samples[name] = read(App.Driver!);
        return true;
    });

    public T Sampled<T>(string name) => (T)_samples[name]!;

    /// <summary>Column and row where <paramref name="text"/> first appears on screen.</summary>
    public static (int Col, int Row)? Find(IDriver driver, string text)
    {
        for (var row = 0; row < driver.Rows; row++)
        {
            var cells = Enumerable.Range(0, driver.Cols).Select(c => driver.Contents![row, c].Grapheme ?? " ").ToList();
            var line = string.Concat(cells);
            var index = line.IndexOf(text, StringComparison.Ordinal);
            if (index >= 0 && cells.Take(index).All(c => c.Length <= 1))
                return (index, row);
        }

        return null;
    }

    public static Attribute AttributeAt(IDriver driver, int col, int row) =>
        driver.Contents![row, col].Attribute ?? throw new InvalidOperationException($"No attribute at {col},{row}");

    public static Attribute AttributeOf(IDriver driver, string text)
    {
        var (col, row) = Find(driver, text) ?? throw new InvalidOperationException($"'{text}' is not on screen.");
        return AttributeAt(driver, col, row);
    }

    public UiSession WaitUntil(Func<bool> condition) => Step(condition);

    /// <summary>
    /// Waits for background work to finish. It first lets the loop run a few ticks: closing a dialog unwinds its
    /// nested loop before the window starts the operation, so "not busy" right away is not yet "done".
    /// </summary>
    public UiSession WaitIdle()
    {
        var ticks = 4;
        return Step(() => --ticks <= 0 && !Window.IsBusy);
    }

    /// <summary>Lets the loop run a few ticks (layout and draw) so a freshly opened dialog is ready for input.</summary>
    public UiSession Settle(int ticks = 4)
    {
        var remaining = ticks;
        return Step(() => --remaining <= 0);
    }

    public UiSession Do(Action<UiSession> action) => Step(() =>
    {
        action(this);
        return true;
    });

    public UiSession Resize(int width, int height) => Step(() =>
    {
        App.Driver!.SetScreenSize(width, height);
        App.LayoutAndDraw(true);
        return true;
    });

    private UiSession Step(Func<bool> step)
    {
        _steps.AddLast(step);
        return this;
    }

    // ---- execution --------------------------------------------------------------------------------------

    /// <summary>Runs the script to completion, then closes any open dialog and the window.</summary>
    public void Run()
    {
        Pump();
        App.Run(Window);
        if (_failure is not null)
            Assert.Fail(_failure);
    }

    private void Pump()
    {
        App.AddTimeout(Tick, () =>
        {
            if (_stopping)
                return false;

            if (DateTime.UtcNow - _started > Deadline)
            {
                _failure = "UI script did not finish in time. Last screens: " + string.Join(" | ", _screens.Keys);
                StopAll();
                return false;
            }

            if (_steps.Count == 0)
            {
                StopAll();
                return false;
            }

            var step = _steps.First!.Value;
            _steps.RemoveFirst();
            // Schedule the next tick first: if this step opens a modal dialog it blocks in that dialog's loop,
            // and the next tick must still be able to fire there.
            Pump();
            try
            {
                if (!step())
                    _steps.AddFirst(step);
            }
            catch (Exception ex)
            {
                _failure = ex.ToString();
                StopAll();
            }

            return false;
        });
    }

    private void StopAll()
    {
        _stopping = true;
        CloseTopThenWindow();
    }

    private void CloseTopThenWindow()
    {
        App.AddTimeout(Tick, () =>
        {
            if (App.TopRunnable == Window || App.TopRunnable is null)
            {
                Window.RequestStop();
                return false;
            }

            App.RequestStop();
            return true;
        });
    }

    internal static Key ParseKey(string key) =>
        key.Length == 1 ? new Key(key[0]) : Key.TryParse(key, out var parsed) ? parsed : throw new ArgumentException($"Unknown key '{key}'.");

    public void Dispose()
    {
        Window.Dispose();
        App.Dispose();
    }
}
