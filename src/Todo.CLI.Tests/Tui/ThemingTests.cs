using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Terminal.Gui.Drawing;
using Todo.Tui;
using Todo.Tui.Theming;
using Todo.Tui.Views;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Todo.CLI.Tests.Tui;

public class ColorMathTests
{
    private static Color C(string hex) => Color.TryParse(hex, out Color? c) ? c!.Value : throw new ArgumentException(hex);

    [Fact]
    public void ContrastRatio_BlackOnWhite_Is21()
    {
        Assert.Equal(21.0, ColorMath.ContrastRatio(C("#000000"), C("#ffffff")), 1);
    }

    [Fact]
    public void ContrastRatio_IsSymmetricAndOneForSameColor()
    {
        var a = C("#61afef");
        var b = C("#282c34");

        Assert.Equal(ColorMath.ContrastRatio(a, b), ColorMath.ContrastRatio(b, a), 6);
        Assert.Equal(1.0, ColorMath.ContrastRatio(a, a), 6);
    }

    [Theory]
    [InlineData("#282c34")]
    [InlineData("#e06c75")]
    [InlineData("#61afef")]
    [InlineData("#ffffff")]
    [InlineData("#000000")]
    [InlineData("#808080")]
    public void Oklch_RoundTripsWithinOneStepPerChannel(string hex)
    {
        var color = C(hex);

        var lch = ColorMath.ToOklch(color);
        var back = ColorMath.FromOklch(lch.L, lch.C, lch.H);

        Assert.InRange(Math.Abs(back.R - color.R), 0, 1);
        Assert.InRange(Math.Abs(back.G - color.G), 0, 1);
        Assert.InRange(Math.Abs(back.B - color.B), 0, 1);
    }

    [Fact]
    public void FromOklch_OutOfGamutChroma_IsReducedNotWrapped()
    {
        // Very high chroma cannot exist in sRGB; the result must still be a valid, similarly-hued color.
        var color = ColorMath.FromOklch(0.7, 0.5, 145);

        var lch = ColorMath.ToOklch(color);
        Assert.InRange(lch.H, 125, 165);
        Assert.InRange(lch.L, 0.6, 0.8);
    }

    [Fact]
    public void EnsureContrast_ReadableColor_IsReturnedUnchanged()
    {
        var preferred = C("#ffffff");

        Assert.Equal(preferred, ColorMath.EnsureContrast(preferred, C("#000000"), ColorMath.TextContrast));
    }

    [Fact]
    public void EnsureContrast_FaintColor_KeepsItsHueAndReachesTheMinimum()
    {
        var background = C("#282c34");
        var comment = C("#5c6370");   // blue-grey, contrast about 2.4

        var fixedColor = ColorMath.EnsureContrast(comment, background, ColorMath.TextContrast);

        Assert.True(ColorMath.ContrastRatio(fixedColor, background) >= ColorMath.TextContrast);
        var before = ColorMath.ToOklch(comment);
        var after = ColorMath.ToOklch(fixedColor);
        Assert.True(after.L > before.L, "should get lighter on a dark background");
        Assert.InRange(Math.Abs(after.H - before.H), 0, 15);
    }

    [Fact]
    public void EnsureContrast_YellowOnWhite_BecomesADarkerYellowNotBlack()
    {
        // The reported bug: yellow hotkey text on a white highlight.
        var fixedColor = ColorMath.EnsureContrast(C("#ffff00"), C("#ffffff"), ColorMath.TextContrast);

        Assert.True(ColorMath.ContrastRatio(fixedColor, C("#ffffff")) >= ColorMath.TextContrast);
        var lch = ColorMath.ToOklch(fixedColor);
        Assert.InRange(lch.H, 80, 125);
    }

    [Fact]
    public void EnsureContrast_ImpossibleRequest_FallsBackToBlackOrWhite()
    {
        var mid = C("#777777");

        var result = ColorMath.EnsureContrast(mid, mid, 21.0);

        Assert.True(result == C("#000000") || result == C("#ffffff"));
    }
}

public class ThemeCatalogTests
{
    public static IEnumerable<object[]> BuiltIn() => TuiThemes.Names.Select(n => new object[] { n });

    private static Color C(string value) => Color.TryParse(value, out Color? c) ? c!.Value : throw new ArgumentException(value);

    [Fact]
    public void Names_ContainDefaultAndTheEightRequestedThemes()
    {
        Assert.Equal(
            new[] { "Default", "One Dark Pro", "Dracula", "Tokyo Night", "Catppuccin Mocha", "GitHub Dark", "Night Owl", "Gruvbox Material", "SynthWave '84" },
            TuiThemes.Names);
    }

    [Theory]
    [InlineData("One Dark Pro", "One Dark Pro")]
    [InlineData("one-dark-pro", "One Dark Pro")]
    [InlineData("ONEDARK", "One Dark Pro")]
    [InlineData("dracula", "Dracula")]
    [InlineData("Tokyo Night", "Tokyo Night")]
    [InlineData("catppuccin", "Catppuccin Mocha")]
    [InlineData("github", "GitHub Dark")]
    [InlineData("night owl", "Night Owl")]
    [InlineData("gruvbox", "Gruvbox Material")]
    [InlineData("SynthWave '84", "SynthWave '84")]
    [InlineData("synthwave84", "SynthWave '84")]
    [InlineData("default", "Default")]
    public void TryGet_AcceptsNamesAndAliasesIgnoringCaseAndPunctuation(string input, string expected)
    {
        Assert.True(TuiThemes.TryGet(input, out var name, out _));
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("solarized")]
    public void TryGet_UnknownOrBlank_ReturnsFalseAndTheDefault(string? input)
    {
        Assert.False(TuiThemes.TryGet(input, out var name, out var palette));
        Assert.Equal("Default", name);
        Assert.Equal(TuiPalette.Default, palette);
    }

    [Theory]
    [MemberData(nameof(BuiltIn))]
    public void EveryThemeColor_Parses(string name)
    {
        TuiThemes.TryGet(name, out _, out var palette);

        foreach (var key in TuiPalette.Keys)
        {
            var value = (string)typeof(TuiPalette).GetProperty(key)!.GetValue(palette)!;
            Assert.True(Color.TryParse(value, out Color? _), $"{name}.{key} = '{value}'");
        }
    }

    [Theory]
    [MemberData(nameof(BuiltIn))]
    public void UserSuppliedColors_AreExactlyTheRequestedOnes(string name)
    {
        if (name == "Default")
            return;
        TuiThemes.TryGet(name, out _, out var p);
        var expected = new Dictionary<string, (string Bg, string Fg, string Keyword, string Function, string Comment)>
        {
            ["One Dark Pro"] = ("#282c34", "#abb2bf", "#e06c75", "#61afef", "#5c6370"),
            ["Dracula"] = ("#282a36", "#f8f8f2", "#50fa7b", "#bd93f9", "#6272a4"),
            ["Tokyo Night"] = ("#1a1b26", "#c0caf5", "#bb9af7", "#7aa2f7", "#565f89"),
            ["Catppuccin Mocha"] = ("#1e1e2e", "#cdd6f4", "#cba6f7", "#89b4fa", "#6c7086"),
            ["GitHub Dark"] = ("#0d1117", "#c9d1d9", "#ff7b72", "#79c0ff", "#8b949e"),
            ["Night Owl"] = ("#011627", "#d6deeb", "#c792ea", "#82aaff", "#637777"),
            ["Gruvbox Material"] = ("#282828", "#dfbf8e", "#ea6962", "#a9b665", "#928374"),
            ["SynthWave '84"] = ("#262335", "#ffffff", "#fede5d", "#ff7edb", "#848bbd")
        }[name];

        Assert.Equal(expected.Bg, p.Background);
        Assert.Equal(expected.Fg, p.Foreground);
        Assert.Equal(expected.Keyword, p.Selection);
        Assert.Equal(expected.Function, p.Accent);
        // Muted starts from the comment color; it is only ever made lighter and keeps its hue.
        var comment = ColorMath.ToOklch(C(expected.Comment));
        var muted = ColorMath.ToOklch(C(p.Muted));
        Assert.True(muted.L >= comment.L - 0.005);
        Assert.InRange(Math.Abs(muted.H - comment.H), 0, 20);
    }

    [Theory]
    [MemberData(nameof(BuiltIn))]
    public void DerivedRoles_AreHarmoniousReadableAndDistinguishable(string name)
    {
        if (name == "Default")
            return;
        TuiThemes.TryGet(name, out _, out var p);
        Color bg = C(p.Background), surface = C(p.DialogBackground), warning = C(p.Warning), error = C(p.Error), muted = C(p.Muted);

        // Surface: slightly lighter than the background, same family.
        var bgLch = ColorMath.ToOklch(bg);
        var surfaceLch = ColorMath.ToOklch(surface);
        Assert.InRange(surfaceLch.L - bgLch.L, 0.03, 0.07);

        // Warning is amber, error is red.
        Assert.InRange(ColorMath.ToOklch(warning).H, 70, 110);
        var errorHue = ColorMath.ToOklch(error).H;
        Assert.True(errorHue is >= 5 and <= 45, $"error hue {errorHue}");

        // They differ in brightness as well as hue (red-green color blindness).
        Assert.InRange(Math.Abs(ColorMath.ToOklch(warning).L - ColorMath.ToOklch(error).L), 0.08, 0.30);

        // Readable on both places they are drawn.
        foreach (var color in new[] { warning, error })
        {
            Assert.True(ColorMath.ContrastRatio(color, bg) >= ColorMath.TextContrast, $"{name} contrast on background");
            Assert.True(ColorMath.ContrastRatio(color, surface) >= ColorMath.TextContrast, $"{name} contrast on surface");
        }

        Assert.True(ColorMath.ContrastRatio(muted, bg) >= ColorMath.DimmedContrast);
        Assert.True(ColorMath.ContrastRatio(muted, surface) >= ColorMath.DimmedContrast);
    }
}

public class ThemeReadabilityTests
{
    public static IEnumerable<object[]> BuiltIn() => TuiThemes.Names.Select(n => new object[] { n });

    private static IEnumerable<(string Role, Attribute Attr, double Minimum)> Roles(TuiTheme theme)
    {
        foreach (var (schemeName, scheme) in new[]
                 {
                     ("Base", theme.Base), ("Accent", theme.Accent), ("Dialog", theme.Dialog),
                     ("KeyHint", theme.KeyHint), ("Error", theme.Error), ("DialogError", theme.DialogError)
                 })
        {
            yield return ($"{schemeName}.Normal", scheme.Normal, ColorMath.TextContrast);
            yield return ($"{schemeName}.HotNormal", scheme.HotNormal, ColorMath.TextContrast);
            yield return ($"{schemeName}.Focus", scheme.Focus, ColorMath.TextContrast);
            yield return ($"{schemeName}.HotFocus", scheme.HotFocus, ColorMath.TextContrast);
            yield return ($"{schemeName}.Active", scheme.Active, ColorMath.TextContrast);
            yield return ($"{schemeName}.HotActive", scheme.HotActive, ColorMath.TextContrast);
            yield return ($"{schemeName}.Highlight", scheme.Highlight, ColorMath.TextContrast);
            yield return ($"{schemeName}.Editable", scheme.Editable, ColorMath.TextContrast);
            yield return ($"{schemeName}.ReadOnly", scheme.ReadOnly, ColorMath.DimmedContrast);
            yield return ($"{schemeName}.Disabled", scheme.Disabled, ColorMath.DimmedContrast);
        }
    }

    private static void AssertReadable(TuiTheme theme, string label)
    {
        foreach (var (role, attr, minimum) in Roles(theme))
        {
            var ratio = ColorMath.ContrastRatio(attr.Foreground, attr.Background);
            Assert.True(ratio >= minimum - 0.01, $"{label} {role}: {attr.Foreground} on {attr.Background} = {ratio:0.00} (< {minimum})");
        }
    }

    [Theory]
    [MemberData(nameof(BuiltIn))]
    public void EveryRoleInEveryTheme_IsReadable(string name)
    {
        var theme = TuiTheme.Create(TuiSettings.From(null, null).WithTheme(name));

        AssertReadable(theme, name);
    }

    [Theory]
    [MemberData(nameof(BuiltIn))]
    public void FocusedDialogButton_DiffersFromTheDialogSurface(string name)
    {
        var theme = TuiTheme.Create(TuiSettings.From(null, null).WithTheme(name));

        var surface = theme.Dialog.Normal.Background;
        var highlight = theme.Dialog.Focus.Background;
        Assert.True(ColorMath.ContrastRatio(surface, highlight) >= 1.5,
            $"{name}: highlight {highlight} barely differs from surface {surface}");
    }

    [Theory]
    [InlineData("#ffffff", "#ffffff")]
    [InlineData("#777777", "#777777")]
    [InlineData("#000000", "#000000")]
    [InlineData("#ffff00", "#ffffff")]
    public void HostilePalettes_StillProduceReadableText(string background, string foreground)
    {
        var palette = TuiPalette.Default with
        {
            Background = background,
            Foreground = foreground,
            Accent = background,
            AccentText = foreground,
            Selection = foreground,
            SelectionText = foreground,
            DialogBackground = background,
            DialogText = foreground,
            Warning = background,
            Error = background,
            Muted = background
        };
        var theme = TuiTheme.Create(new TuiSettings(Todo.Tui.Input.AltKeyMode.Auto, palette, NoColor: false));

        AssertReadable(theme, $"{background}/{foreground}");
    }

    [Fact]
    public void UserOverrides_StillGetTheReadabilityGuarantee()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tui:Theme"] = "Dracula",
            ["Tui:Palette:SelectionText"] = "#fffffe",   // white on a light selection: unreadable as given
            ["Tui:Palette:Selection"] = "#ffffff"
        }).Build();

        var theme = TuiTheme.Create(TuiSettings.From(configuration, null));

        AssertReadable(theme, "overrides");
    }
}

public class ThemeSettingsTests
{
    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void From_NoTheme_UsesDefaultWithoutAProblem()
    {
        var settings = TuiSettings.From(Build(new()), null);

        Assert.Equal("Default", settings.ThemeName);
        Assert.Null(settings.ThemeProblem);
    }

    [Fact]
    public void From_NamedTheme_UsesItsPalette()
    {
        var settings = TuiSettings.From(Build(new() { ["Tui:Theme"] = "tokyo-night" }), null);

        Assert.Equal("Tokyo Night", settings.ThemeName);
        Assert.Equal("#1a1b26", settings.Palette.Background);
    }

    [Fact]
    public void From_UnknownTheme_FallsBackAndExplainsHowToFixIt()
    {
        var settings = TuiSettings.From(Build(new() { ["Tui:Theme"] = "solarized" }), null);

        Assert.Equal("Default", settings.ThemeName);
        Assert.Contains("solarized", settings.ThemeProblem);
        Assert.Contains("Dracula", settings.ThemeProblem);
        Assert.Contains("solarized", Assert.Single(TuiTheme.Create(settings).Problems));
    }

    [Fact]
    public void From_PaletteEntriesOverrideTheTheme_AndSurviveSwitchingThemes()
    {
        var settings = TuiSettings.From(Build(new()
        {
            ["Tui:Theme"] = "Dracula",
            ["Tui:Palette:Accent"] = "#123456"
        }), null);

        Assert.Equal("#123456", settings.Palette.Accent);
        Assert.Equal("#282a36", settings.Palette.Background);

        var other = settings.WithTheme("Night Owl");

        Assert.Equal("Night Owl", other.ThemeName);
        Assert.Equal("#011627", other.Palette.Background);
        Assert.Equal("#123456", other.Palette.Accent);
    }

    [Fact]
    public void NoColor_IgnoresTheThemeButKeepsItsName()
    {
        var settings = TuiSettings.From(Build(new() { ["Tui:Theme"] = "Dracula" }), "1");

        var theme = TuiTheme.Create(settings);

        Assert.Equal("Dracula", theme.Name);
        Assert.Equal(Color.None, theme.Base.Normal.Foreground);
    }
}

public class UserSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "todo-tests-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "nested", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void SaveTheme_CreatesTheFileAndDirectory()
    {
        new UserSettingsStore(SettingsPath).SaveTheme("Dracula");

        Assert.Contains("\"Theme\": \"Dracula\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void SaveTheme_PreservesOtherSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ \"Tui\": { \"AltKeys\": \"on\", \"Theme\": \"Night Owl\" }, \"Other\": 1 }");

        new UserSettingsStore(SettingsPath).SaveTheme("Dracula");

        var text = File.ReadAllText(SettingsPath);
        Assert.Contains("\"AltKeys\": \"on\"", text);
        Assert.Contains("\"Other\": 1", text);
        Assert.Contains("\"Theme\": \"Dracula\"", text);
        Assert.DoesNotContain("Night Owl", text);
    }

    [Fact]
    public void SaveTheme_CorruptFile_IsReplaced()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ not json");

        new UserSettingsStore(SettingsPath).SaveTheme("Tokyo Night");

        Assert.Contains("Tokyo Night", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void SavedTheme_IsPickedUpByTheConfigurationSystem()
    {
        new UserSettingsStore(SettingsPath).SaveTheme("Gruvbox Material");

        var configuration = new ConfigurationBuilder().AddJsonFile(SettingsPath, optional: true).Build();

        Assert.Equal("Gruvbox Material", TuiSettings.From(configuration, null).ThemeName);
    }

    [Fact]
    public void SaveTheme_LeavesNoTemporaryFileBehind()
    {
        new UserSettingsStore(SettingsPath).SaveTheme("Dracula");

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!, "*.tmp"));
    }
}
