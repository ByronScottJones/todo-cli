using System;
using System.IO;
using System.Runtime.InteropServices;
using Todo.Tui;
using Xunit;

namespace Todo.CLI.Tests.Tui;

/// <summary>
/// Covers FR-11: the terminal is restored (the application is disposed) on normal exit and on errors, and
/// SIGINT, SIGTERM and SIGHUP from outside the UI stop it cleanly.
/// </summary>
[Collection("TerminalGui")]
public class TerminalRestoreTests
{
    [Fact]
    public void RunWindow_NormalExit_ReturnsOkAndDisposesTheApplication()
    {
        using var session = UiSession.Create();
        session.App.AddTimeout(TimeSpan.FromMilliseconds(50), () =>
        {
            session.Window.RequestStop();
            return false;
        });

        var exitCode = TuiHost.RunWindow(session.App, () => session.Window);

        Assert.Equal(TuiHost.ExitOk, exitCode);
        Assert.Null(session.App.Driver);
    }

    [Fact]
    public void RunWindow_WindowCreationThrows_ReportsTheErrorAndStillDisposesTheApplication()
    {
        using var session = UiSession.Create();
        var originalError = Console.Error;
        var captured = new StringWriter();
        Console.SetError(captured);
        int exitCode;
        try
        {
            exitCode = TuiHost.RunWindow(session.App, () => throw new InvalidOperationException("boom"));
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal(TuiHost.ExitFailed, exitCode);
        Assert.Contains("The terminal UI stopped unexpectedly: boom", captured.ToString());
        Assert.Null(session.App.Driver);
    }

    [Theory]
    [InlineData(PosixSignalNumber.SIGHUP)]
    [InlineData(PosixSignalNumber.SIGTERM)]
    [InlineData(PosixSignalNumber.SIGINT)]
    public void TerminationSignal_StopsTheRunningUi(PosixSignalNumber signal)
    {
        // Real signals are sent to this process; the registration under test must absorb them.
        if (OperatingSystem.IsWindows())
            return;

        using var session = UiSession.Create();
        using var registration = TuiHost.RegisterTerminationSignals(session.App);

        // The script never finishes on its own, so the run can only end if the signal stops the UI. If it does
        // not, the session deadline fails the test.
        session
            .Do(_ => Assert.Equal(0, kill(Environment.ProcessId, (int)signal)))
            .WaitUntil(() => false)
            .Run();
    }

    // Standard POSIX numbers; identical on Linux and macOS.
    public enum PosixSignalNumber
    {
        SIGHUP = 1,
        SIGINT = 2,
        SIGTERM = 15
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);
}
