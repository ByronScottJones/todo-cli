using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Todo.Tui;
using Todo.Tui.Input;
using Xunit;

namespace Todo.CLI.Tests.Tui;

public class TuiSettingsTests
{
    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void From_NoConfiguration_UsesDefaults()
    {
        var settings = TuiSettings.From(null, null);

        Assert.Equal(AltKeyMode.Auto, settings.AltKeys);
        Assert.Equal(TuiPalette.Default, settings.Palette);
        Assert.False(settings.NoColor);
    }

    [Fact]
    public void From_ReadsPaletteAndAltKeysFromTuiSection()
    {
        var configuration = Build(new()
        {
            ["Tui:AltKeys"] = "off",
            ["Tui:Palette:Accent"] = "Magenta",
            ["Tui:Palette:Warning"] = "BrightYellow"
        });

        var settings = TuiSettings.From(configuration, null);

        Assert.Equal(AltKeyMode.Off, settings.AltKeys);
        Assert.Equal("Magenta", settings.Palette.Accent);
        Assert.Equal("BrightYellow", settings.Palette.Warning);
        Assert.Equal(TuiPalette.Default.Background, settings.Palette.Background);
    }

    [Fact]
    public void From_InvalidAltKeys_FallsBackToAuto()
    {
        var settings = TuiSettings.From(Build(new() { ["Tui:AltKeys"] = "sometimes" }), null);

        Assert.Equal(AltKeyMode.Auto, settings.AltKeys);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("anything", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void From_NoColorEnvironment_FollowsNoColorConvention(string? value, bool expected)
    {
        Assert.Equal(expected, TuiSettings.From(null, value).NoColor);
    }

    [Fact]
    public void From_BlankPaletteValue_UsesDefault()
    {
        var settings = TuiSettings.From(Build(new() { ["Tui:Palette:Accent"] = "  " }), null);

        Assert.Equal(TuiPalette.Default.Accent, settings.Palette.Accent);
    }
}
