using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terminal.Gui.Drawing;
using Todo.Tui.Theming;
using Todo.Tui.Views;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Todo.CLI.Tests.Tui;

/// <summary>Checks the colors that are actually rendered into the screen buffer, not just the configured ones.</summary>
[Collection("TerminalGui")]
public class ThemeUiTests
{
    public static IEnumerable<object[]> Themes() => TuiThemes.Names.Select(n => new object[] { n });

    private static FakeTodoService Seeded()
    {
        var service = new FakeTodoService();
        service.Add("Pay the electricity bill", "L1", important: true, due: new DateTime(2026, 10, 30));
        service.Add("Write report", "L2");
        return service;
    }

    private static TuiTheme ThemeFor(UiSession ui, string name) => TuiTheme.Create(ui.Settings.WithTheme(name));

    [Theory]
    [MemberData(nameof(Themes))]
    public void TitleAndKeyBar_UseTheAccentColor(string name)
    {
        using var ui = UiSession.Create(Seeded(), themeName: name);

        ui.Sample("title", d => UiSession.AttributeAt(d, 2, 0))
            .Sample("keybar", d => UiSession.AttributeAt(d, d.Cols - 1, d.Rows - 2)).Run();

        var accent = ThemeFor(ui, name).Accent.Normal.Background;
        Assert.Equal(accent, ui.Sampled<Attribute>("title").Background);
        Assert.Equal(accent, ui.Sampled<Attribute>("keybar").Background);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Dialog_HasItsOwnSurfaceDistinctFromTheWorkspace(string name)
    {
        using var ui = UiSession.Create(Seeded(), themeName: name);

        ui.Press("Alt+N").Settle()
            .Sample("dialog", d => UiSession.AttributeOf(d, "Subject:"))
            .Sample("list", d => UiSession.AttributeOf(d, "Write report")).Run();

        var theme = ThemeFor(ui, name);
        Assert.Equal(theme.Dialog.Normal.Background, ui.Sampled<Attribute>("dialog").Background);
        Assert.NotEqual(ui.Sampled<Attribute>("list").Background, ui.Sampled<Attribute>("dialog").Background);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void HighlightedButton_IsReadableAndStandsOutFromTheDialog(string name)
    {
        using var ui = UiSession.Create(Seeded(), themeName: name);

        // The confirm dialog opens with [ OK ] focused and highlighted.
        ui.Press("Delete").Settle()
            .Sample("ok", d => UiSession.AttributeOf(d, "OK"))
            .Sample("cancel", d => UiSession.AttributeOf(d, "Cancel"))
            .Sample("message", d => UiSession.AttributeOf(d, "This cannot be undone")).Run();

        var ok = ui.Sampled<Attribute>("ok");
        var cancel = ui.Sampled<Attribute>("cancel");
        var message = ui.Sampled<Attribute>("message");

        Assert.True(ColorMath.ContrastRatio(ok.Foreground, ok.Background) >= ColorMath.TextContrast,
            $"{name}: OK {ok.Foreground} on {ok.Background}");
        Assert.True(ColorMath.ContrastRatio(cancel.Foreground, cancel.Background) >= ColorMath.TextContrast,
            $"{name}: Cancel {cancel.Foreground} on {cancel.Background}");
        Assert.NotEqual(ok.Background, message.Background);
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Dracula")]
    [InlineData("SynthWave '84")]
    public void MenuDropdown_UsesTheAccentColors(string name)
    {
        using var ui = UiSession.Create(Seeded(), themeName: name);

        ui.Press("Alt+F").Settle().Sample("menu", d => UiSession.AttributeOf(d, "Refresh")).Run();

        var theme = ThemeFor(ui, name);
        Assert.Equal(theme.Accent.Normal.Background, ui.Sampled<Attribute>("menu").Background);
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Tokyo Night")]
    public void InlineValidationError_IsReadableOnTheDialogSurface(string name)
    {
        using var ui = UiSession.Create(Seeded(), themeName: name);

        ui.Press("Alt+N").Settle().Sample("error", d => UiSession.AttributeOf(d, "! Subject is required.")).Run();

        var error = ui.Sampled<Attribute>("error");
        Assert.True(ColorMath.ContrastRatio(error.Foreground, error.Background) >= ColorMath.TextContrast);
        Assert.Equal(ThemeFor(ui, name).Dialog.Normal.Background, error.Background);
    }

    // ---- choosing a theme -------------------------------------------------------------------------------

    [Fact]
    public void ThemePicker_PreviewsWhileMovingThenAppliesAndSavesOnOk()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+O").Settle().Shot("picker")
            .Press("CursorDown").Settle().Sample("preview", d => UiSession.AttributeAt(d, 2, 0))
            .Press("Enter").Settle().Sample("applied", d => UiSession.AttributeAt(d, 2, 0)).Run();

        Assert.Contains("Choose theme", ui.Screen("picker"));
        Assert.Contains("Dracula", ui.Screen("picker"));
        var oneDark = ThemeFor(ui, "One Dark Pro").Accent.Normal.Background;
        Assert.Equal(oneDark, ui.Sampled<Attribute>("preview").Background);
        Assert.Equal(oneDark, ui.Sampled<Attribute>("applied").Background);
        Assert.Equal("One Dark Pro", ui.Window.ThemeName);
        Assert.Equal(new[] { "One Dark Pro" }, ui.SavedThemes);
        Assert.Contains("Theme: One Dark Pro.", ui.Window.StatusText);
    }

    [Fact]
    public void ThemePicker_EscapeRestoresTheOriginalThemeAndSavesNothing()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+O").Settle().Press("CursorDown", "CursorDown").Settle().Press("Esc").Settle()
            .Sample("title", d => UiSession.AttributeAt(d, 2, 0)).Run();

        Assert.Equal("Default", ui.Window.ThemeName);
        Assert.Empty(ui.SavedThemes);
        Assert.Equal(ThemeFor(ui, "Default").Accent.Normal.Background, ui.Sampled<Attribute>("title").Background);
        Assert.Contains("Theme unchanged.", ui.Window.StatusText);
    }

    [Fact]
    public void ThemePicker_StartsOnTheCurrentTheme()
    {
        using var ui = UiSession.Create(Seeded(), themeName: "Night Owl");

        ui.Press("Alt+O").Settle().Press("Enter").Settle().Run();

        Assert.Equal("Night Owl", ui.Window.ThemeName);
        Assert.Equal(new[] { "Night Owl" }, ui.SavedThemes);
    }

    [Fact]
    public void ThemePicker_SaveFailure_StillAppliesTheThemeAndSaysSo()
    {
        using var ui = UiSession.Create(Seeded(), saveTheme: _ => throw new IOException("read-only file system"));

        ui.Press("Alt+O").Settle().Press("CursorDown", "Enter").Settle().Run();

        Assert.Equal("One Dark Pro", ui.Window.ThemeName);
        Assert.Contains("could not be saved", ui.Window.StatusText);
        Assert.Contains("read-only file system", ui.Window.StatusText);
    }

    [Fact]
    public void ThemeMenuItem_OpensThePicker()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+V").Settle().Shot("menu").Run();

        Assert.Contains("Theme...", ui.Screen("menu"));
    }

    [Fact]
    public void ThemeKey_HasACtrlAlternative()
    {
        using var ui = UiSession.Create(Seeded(), alt: Todo.Tui.Input.AltKeyStatus.Unconfirmed);

        ui.Press("Ctrl+O").Settle().Shot("picker").Run();

        Assert.Contains("Choose theme", ui.Screen("picker"));
    }

    [Fact]
    public void SwitchingTheme_KeepsTheWindowFullyRendered()
    {
        using var ui = UiSession.Create(Seeded());

        ui.Press("Alt+O").Settle().Press("CursorDown", "Enter").Settle().Shot("after").Run();

        var screen = ui.Screen("after");
        Assert.Contains("Pay the electricity bill", screen);
        Assert.Contains("Alt+O Theme", screen);
        Assert.Contains("[top: MainWindow]", screen);
    }
}
