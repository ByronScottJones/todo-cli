using System.Collections.Generic;
using System.Linq;
using Todo.Tui;
using Todo.Tui.Input;
using Todo.Tui.Views;
using Xunit;

namespace Todo.CLI.Tests.Tui;

public class TerminalAndThemeTests
{
    private static bool Unicode(Dictionary<string, string?> env, bool windows = false) =>
        TerminalCapabilities.SupportsUnicode(name => env.GetValueOrDefault(name), windows);

    [Theory]
    [InlineData("en_US.UTF-8", true)]
    [InlineData("en_US.utf8", true)]
    [InlineData("C", false)]
    [InlineData("POSIX", false)]
    public void SupportsUnicode_FollowsTheLocale(string lang, bool expected)
    {
        Assert.Equal(expected, Unicode(new() { ["LANG"] = lang }));
    }

    [Fact]
    public void SupportsUnicode_LcAllOverridesLang()
    {
        Assert.False(Unicode(new() { ["LC_ALL"] = "C", ["LANG"] = "en_US.UTF-8" }));
        Assert.True(Unicode(new() { ["LC_ALL"] = "en_US.UTF-8", ["LANG"] = "C" }));
    }

    [Fact]
    public void SupportsUnicode_LinuxConsoleAndUnsetLocale_AreAscii()
    {
        Assert.False(Unicode(new() { ["TERM"] = "linux", ["LANG"] = "en_US.UTF-8" }));
        Assert.False(Unicode(new()));
    }

    [Fact]
    public void SupportsUnicode_Windows_IsAlwaysTrue()
    {
        Assert.True(Unicode(new(), windows: true));
    }

    [Fact]
    public void Theme_DefaultPalette_HasNoProblems()
    {
        var theme = TuiTheme.Create(TuiSettings.From(null, null));

        Assert.Empty(theme.Problems);
    }

    [Fact]
    public void Theme_InvalidColor_IsReportedAndFallsBackToTheDefault()
    {
        var palette = TuiPalette.Default with { Accent = "not-a-color" };
        var theme = TuiTheme.Create(new TuiSettings(AltKeyMode.Auto, palette, NoColor: false));

        var problem = Assert.Single(theme.Problems);
        Assert.Contains("Accent", problem);
        Assert.Contains("not-a-color", problem);
    }

    [Fact]
    public void Theme_NoColor_UsesNoPaletteColorsAndReportsNoProblems()
    {
        var palette = TuiPalette.Default with { Accent = "not-a-color" };
        var theme = TuiTheme.Create(new TuiSettings(AltKeyMode.Auto, palette, NoColor: true));

        Assert.Empty(theme.Problems);
    }

    [Fact]
    public void Help_EveryLineFitsASixtyColumnTerminal()
    {
        foreach (var status in new[] { AltKeyStatus.Confirmed, AltKeyStatus.Unconfirmed })
        {
            var lines = HelpDialog.BuildText(status).Split('\n').Select(l => l.TrimEnd('\r'));
            Assert.All(lines, line => Assert.True(line.Length <= 60, $"'{line}' is {line.Length} columns"));
        }
    }

    [Fact]
    public void Help_ListsEveryCommandKey()
    {
        var text = HelpDialog.BuildText(AltKeyStatus.Confirmed);

        foreach (var command in System.Enum.GetValues<TuiCommand>())
        {
            Assert.Contains(CommandKeys.AltText(command), text);
            Assert.Contains(CommandKeys.CtrlText(command), text);
        }
    }

    [Fact]
    public void Help_MentionsMacOptionKeyOnlyWhenAltIsUnconfirmed()
    {
        Assert.DoesNotContain("Option", HelpDialog.BuildText(AltKeyStatus.Confirmed));
        Assert.Contains("Option", HelpDialog.BuildText(AltKeyStatus.Unconfirmed));
    }
}
