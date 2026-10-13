using Terminal.Gui.Drawing;

namespace Todo.Tui.Theming;

/// <summary>WCAG 2.x contrast helpers so no text/background pair is ever unreadable, whatever the palette.</summary>
public static class ColorMath
{
    /// <summary>Minimum contrast for normal text (WCAG AA).</summary>
    public const double TextContrast = 4.5;

    /// <summary>Minimum contrast for deliberately dimmed text such as disabled controls.</summary>
    public const double DimmedContrast = 3.0;

    public static double Luminance(Color color)
    {
        static double Channel(int value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    public static double ContrastRatio(Color a, Color b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (lighter, darker) = la >= lb ? (la, lb) : (lb, la);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Returns <paramref name="preferred"/> when it is readable on <paramref name="background"/>. Otherwise it keeps
    /// the color's hue and chroma and moves only its lightness, away from the background, just far enough to reach
    /// <paramref name="minimum"/>, so a theme's amber stays amber. Only if that is impossible does it fall back to
    /// the first fallback that is readable, then to black or white.
    /// </summary>
    public static Color EnsureContrast(Color preferred, Color background, double minimum, params Color[] fallbacks)
    {
        if (ContrastRatio(preferred, background) >= minimum)
            return preferred;

        var adjusted = AdjustLightnessForContrast(preferred, background, minimum);
        if (adjusted is { } color)
            return color;

        foreach (var fallback in fallbacks)
        {
            if (ContrastRatio(fallback, background) >= minimum)
                return fallback;
        }

        var black = new Color(0, 0, 0);
        var white = new Color(255, 255, 255);
        return ContrastRatio(white, background) >= ContrastRatio(black, background) ? white : black;
    }

    /// <summary>
    /// Moves the lightness of <paramref name="color"/> (in OKLCH) away from the background until the contrast
    /// reaches <paramref name="minimum"/>. Returns null when even the extreme lightness is not enough.
    /// </summary>
    public static Color? AdjustLightnessForContrast(Color color, Color background, double minimum)
    {
        var lch = ToOklch(color);
        var towardLight = Luminance(background) < 0.5;
        var limit = towardLight ? 1.0 : 0.0;
        if (ContrastRatio(FromOklch(limit, lch.C, lch.H), background) < minimum)
            return null;

        // Binary search for the lightness closest to the original that still meets the minimum.
        double near = lch.L, far = limit;
        for (var i = 0; i < 24; i++)
        {
            var mid = (near + far) / 2;
            if (ContrastRatio(FromOklch(mid, lch.C, lch.H), background) >= minimum)
                far = mid;
            else
                near = mid;
        }

        return FromOklch(far, lch.C, lch.H);
    }

    // ---- OKLCH (Björn Ottosson's OKLab) ---------------------------------------------------------------

    public readonly record struct Oklch(double L, double C, double H);

    public static Oklch ToOklch(Color color)
    {
        static double Linear(int v)
        {
            var c = v / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        double r = Linear(color.R), g = Linear(color.G), b = Linear(color.B);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

        var lightness = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
        var a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
        var bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;
        var hue = Math.Atan2(bb, a) * 180 / Math.PI;
        return new Oklch(lightness, Math.Sqrt(a * a + bb * bb), hue < 0 ? hue + 360 : hue);
    }

    /// <summary>Converts OKLCH to sRGB, lowering chroma until the color fits the sRGB gamut.</summary>
    public static Color FromOklch(double lightness, double chroma, double hue)
    {
        lightness = Math.Clamp(lightness, 0, 1);
        var low = 0.0;
        var high = Math.Max(0, chroma);
        if (!TryConvert(lightness, high, hue, out var rgb))
        {
            for (var i = 0; i < 20; i++)
            {
                var mid = (low + high) / 2;
                if (TryConvert(lightness, mid, hue, out _))
                    low = mid;
                else
                    high = mid;
            }

            TryConvert(lightness, low, hue, out rgb);
        }

        return new Color(rgb.R, rgb.G, rgb.B);
    }

    private static bool TryConvert(double lightness, double chroma, double hue, out (int R, int G, int B) rgb)
    {
        var radians = hue * Math.PI / 180;
        var a = chroma * Math.Cos(radians);
        var b = chroma * Math.Sin(radians);

        var l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);

        var r = +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var bl = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

        const double epsilon = 1e-4;
        var inGamut = r is >= -epsilon and <= 1 + epsilon && g is >= -epsilon and <= 1 + epsilon
                      && bl is >= -epsilon and <= 1 + epsilon;
        rgb = (Encode(r), Encode(g), Encode(bl));
        return inGamut;

        static int Encode(double linear)
        {
            var v = Math.Clamp(linear, 0, 1);
            var c = v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
            return (int)Math.Round(c * 255);
        }
    }

    public static string ToHex(Color color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";
}
