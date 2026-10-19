using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.App;
using Todo.Core.Repository;
using Todo.Tui.Input;
using Todo.Tui.Model;
using Todo.Tui.Views;

namespace Todo.Tui;

/// <summary>Entry point for the full-screen terminal UI.</summary>
public static class TuiHost
{
    public const int ExitOk = 0;
    public const int ExitFailed = 1;
    public const int ExitNotInteractive = 2;

    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(120);

    /// <summary>True when both input and output are attached to a terminal.</summary>
    public static bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public static async Task<int> RunAsync(IServiceProvider services, IConfiguration? configuration = null)
    {
        if (!IsInteractive)
        {
            await Console.Error.WriteLineAsync(
                "The terminal UI needs an interactive terminal, but input or output is redirected. " +
                "Use the add, list, complete and remove commands instead (see 'todo --help').");
            return ExitNotInteractive;
        }

        var settings = TuiSettings.From(configuration, Environment.GetEnvironmentVariable("NO_COLOR"));
        var altStatus = AltKeySupport.Detect(settings.AltKeys);
        var workspace = new TodoWorkspace(
            services.GetRequiredService<ITodoItemRepository>(),
            services.GetRequiredService<ITodoListRepository>());

        // Load before taking over the screen: a first-time sign-in opens a browser and prints to the console,
        // and a failure here can be reported normally instead of inside a half-drawn UI.
        Console.WriteLine("Loading your to-do items (a browser window may open to sign in)...");
        try
        {
            await workspace.LoadAsync();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Could not load your to-do items: {ex.Message}");
            return ExitFailed;
        }

        return Run(workspace, settings, altStatus);
    }

    private static int Run(TodoWorkspace workspace, TuiSettings settings, AltKeyStatus altStatus)
    {
        var theme = TuiTheme.Create(settings);
        var glyphs = TerminalCapabilities.SupportsUnicode(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows())
            ? RowGlyphs.Unicode
            : RowGlyphs.Ascii;

        // Disposing the application restores the terminal, so RunWindow disposes it on every exit path.
        var app = Application.Create();
        try
        {
            app.Init();
            TuiKeyboard.Configure(app);
        }
        catch
        {
            app.Dispose();
            throw;
        }

        return RunWindow(app, () => new MainWindow(
            app, workspace, theme, altStatus, glyphs, FlashDuration,
            startupNotice: BuildStartupNotice(theme, altStatus, settings), alreadyLoaded: true,
            themeName: settings.ThemeName,
            themeFactory: name => TuiTheme.Create(settings.WithTheme(name)),
            saveTheme: new UserSettingsStore(UserSettingsStore.DefaultPath).SaveTheme));
    }

    /// <summary>
    /// Runs the window and always disposes <paramref name="app"/> (restoring the terminal) and unregisters the
    /// termination signals, whether the UI exits normally or throws. A failure is reported on stderr and returned
    /// as <see cref="ExitFailed"/> instead of escaping with the terminal still in raw mode.
    /// </summary>
    internal static int RunWindow(IApplication app, Func<MainWindow> createWindow)
    {
        Exception? failure = null;
        using (app)
        {
            using var signals = RegisterTerminationSignals(app);
            try
            {
                using var window = createWindow();
                app.Run(window);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }

        if (failure is null)
            return ExitOk;

        Console.Error.WriteLine($"The terminal UI stopped unexpectedly: {failure.Message}");
        return ExitFailed;
    }

    private static string BuildStartupNotice(TuiTheme theme, AltKeyStatus altStatus, TuiSettings settings)
    {
        var notes = new List<string>();
        if (altStatus == AltKeyStatus.Unconfirmed)
            notes.Add("Alt keys unconfirmed: showing Ctrl shortcuts (F1 for details).");
        notes.AddRange(theme.Problems);
        if (settings.NoColor)
            notes.Add("NO_COLOR set: using monochrome.");
        return string.Join(" ", notes);
    }

    /// <summary>
    /// SIGINT/SIGTERM/SIGHUP from outside the UI (kill, closing the terminal tab) stop the app cleanly so the
    /// terminal state is restored. Inside the UI, Ctrl+C is Copy, not an interrupt.
    /// </summary>
    internal static IDisposable RegisterTerminationSignals(IApplication app)
    {
        var registrations = new List<PosixSignalRegistration>();
        foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGHUP })
        {
            try
            {
                registrations.Add(PosixSignalRegistration.Create(signal, context =>
                {
                    context.Cancel = true;
                    app.Invoke(() => app.RequestStop());
                }));
            }
            catch (PlatformNotSupportedException)
            {
                // Not every signal exists on every platform; the others still apply.
            }
        }

        return new SignalScope(registrations);
    }

    private sealed class SignalScope(List<PosixSignalRegistration> registrations) : IDisposable
    {
        public void Dispose()
        {
            foreach (var registration in registrations)
                registration.Dispose();
        }
    }
}
