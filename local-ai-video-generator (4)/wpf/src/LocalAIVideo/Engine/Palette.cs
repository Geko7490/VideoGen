using System.Windows;
using System.Windows.Media;
using LocalAIVideo.Models;

namespace LocalAIVideo.Engine;

/// <summary>
/// Procedural palette: a visual style is not just colours — it drives line weight, detail,
/// grain, bloom and animation feel. Ported from the original engine.
/// </summary>
public sealed class StyleSpec
{
    public Color? Outline;
    public double OutlineWidth = 0.7;
    public double Saturation = 1.0;
    public double Contrast = 1.0;
    public double Grain = 0.0;
    public bool Hatch;
    public bool Flat;
    public double Bloom;
    public double Darkness;
    public double AnimSpeed = 1.0;
    public double AnimJitter;
    public double Monochrome;
}

public sealed class LightSpec
{
    public Color Ambient; public double AmbientAlpha;
    public Color Key; public double KeyAlpha;
    public double Vignette; public double ShadowAlpha;
    public bool Beam; public double Flicker;
    public Color Rim; public double RimAlpha;
}

public static class Palette
{
    public static readonly Dictionary<VisualStyle, StyleSpec> Styles = new()
    {
        [VisualStyle.cinematic_2d] = new() { Outline = C("#0d1017"), OutlineWidth = .7, Saturation = .95, Contrast = 1.1, Grain = .07, Darkness = .14, Bloom = .12 },
        [VisualStyle.cartoon]      = new() { Outline = C("#1b1f27"), OutlineWidth = 1.1, Saturation = 1.2, Grain = .02, Flat = false, Bloom = 0, AnimSpeed = 1.2, AnimJitter = .1 },
        [VisualStyle.dark_cartoon] = new() { Outline = C("#08090d"), OutlineWidth = 1.25, Saturation = .85, Contrast = 1.18, Grain = .06, Darkness = .24, Bloom = .1 },
        [VisualStyle.comic]        = new() { Outline = C("#0b0b0b"), OutlineWidth = 1.9, Saturation = 1.3, Contrast = 1.25, Grain = .1, Flat = true, Hatch = true },
        [VisualStyle.minimal]      = new() { Outline = null, Saturation = .7, Contrast = .9, Flat = true, AnimSpeed = .9 },
        [VisualStyle.flat]         = new() { Outline = null, Saturation = 1.25, Flat = true },
        [VisualStyle.stylized]     = new() { Outline = C("#161b26"), OutlineWidth = .9, Saturation = 1.15, Contrast = 1.12, Grain = .04, Bloom = .2 },
        [VisualStyle.horror]       = new() { Outline = C("#05070a"), OutlineWidth = 1.1, Saturation = .4, Contrast = 1.4, Grain = .17, Darkness = .44, AnimJitter = .08 },
        [VisualStyle.noir]         = new() { Outline = Colors.Black, OutlineWidth = 1.3, Saturation = .12, Contrast = 1.5, Grain = .14, Darkness = .3, Monochrome = .92 },
        [VisualStyle.neon]         = new() { Outline = C("#06060f"), OutlineWidth = .9, Saturation = 1.55, Contrast = 1.2, Grain = .05, Bloom = .6, Darkness = .32 },
        [VisualStyle.dreamlike]    = new() { Outline = null, Saturation = 1.05, Contrast = .88, Grain = .09, Bloom = .45, AnimSpeed = .75 },
        [VisualStyle.documentary]  = new() { Outline = C("#2a2f38"), OutlineWidth = .8, Saturation = .82, Grain = .12, Hatch = true, AnimJitter = .06 },
    };

    public static readonly Dictionary<LightStyle, LightSpec> Lights = new()
    {
        [LightStyle.daylight]  = new() { Ambient = C("#fff6e0"), AmbientAlpha = .05, Key = C("#fff3cf"), KeyAlpha = .22, Vignette = .18, ShadowAlpha = .26, Rim = Colors.White, RimAlpha = .18 },
        [LightStyle.night]     = new() { Ambient = C("#0b1430"), AmbientAlpha = .5, Key = C("#8fb4ff"), KeyAlpha = .14, Vignette = .5, ShadowAlpha = .44, Rim = C("#9fc0ff"), RimAlpha = .3 },
        [LightStyle.moonlight] = new() { Ambient = C("#16254d"), AmbientAlpha = .44, Key = C("#cfe0ff"), KeyAlpha = .26, Vignette = .46, ShadowAlpha = .42, Beam = true, Rim = C("#dbe8ff"), RimAlpha = .38 },
        [LightStyle.warm]      = new() { Ambient = C("#ffb366"), AmbientAlpha = .15, Key = C("#ffd9a0"), KeyAlpha = .24, Vignette = .26, ShadowAlpha = .28, Rim = C("#ffd9a0"), RimAlpha = .2 },
        [LightStyle.cold]      = new() { Ambient = C("#b9d9ff"), AmbientAlpha = .14, Key = Colors.White, KeyAlpha = .2, Vignette = .26, ShadowAlpha = .24, Rim = C("#e8f3ff"), RimAlpha = .22 },
        [LightStyle.sunset]    = new() { Ambient = C("#ff8f4d"), AmbientAlpha = .22, Key = C("#ffd08a"), KeyAlpha = .32, Vignette = .3, ShadowAlpha = .34, Beam = true, Rim = C("#ffbb70"), RimAlpha = .42 },
        [LightStyle.neon]      = new() { Ambient = C("#12042a"), AmbientAlpha = .4, Key = C("#ff3cac"), KeyAlpha = .32, Vignette = .44, ShadowAlpha = .38, Flicker = .12, Rim = C("#36e0ff"), RimAlpha = .5 },
        [LightStyle.flashlight]= new() { Ambient = C("#05070d"), AmbientAlpha = .66, Key = C("#ffeec2"), KeyAlpha = .52, Vignette = .64, ShadowAlpha = .5, Beam = true, Flicker = .18, Rim = C("#ffeec2"), RimAlpha = .3 },
        [LightStyle.dark]      = new() { Ambient = C("#05060b"), AmbientAlpha = .58, Key = C("#5b6b9a"), KeyAlpha = .12, Vignette = .58, ShadowAlpha = .5, Rim = C("#6f82b8"), RimAlpha = .25 },
        [LightStyle.horror]    = new() { Ambient = C("#060a08"), AmbientAlpha = .62, Key = C("#7fe3b0"), KeyAlpha = .15, Vignette = .68, ShadowAlpha = .56, Flicker = .25, Rim = C("#86ffc4"), RimAlpha = .3 },
        [LightStyle.dramatic]  = new() { Ambient = C("#07090f"), AmbientAlpha = .46, Key = Colors.White, KeyAlpha = .44, Vignette = .56, ShadowAlpha = .54, Beam = true, Rim = Colors.White, RimAlpha = .55 },
    };

    public static readonly Dictionary<TimeOfDay, (Color, Color)> Sky = new()
    {
        [TimeOfDay.dawn]   = (C("#2b3a67"), C("#f6a07a")),
        [TimeOfDay.day]    = (C("#4a90d9"), C("#bfe3ff")),
        [TimeOfDay.sunset] = (C("#2a1b4a"), C("#ff8a52")),
        [TimeOfDay.night]  = (C("#060a1c"), C("#1b2a4d")),
    };

    public static StyleSpec Style(VisualStyle v) => Styles.TryGetValue(v, out var s) ? s : Styles[VisualStyle.cinematic_2d];
    public static LightSpec Light(LightStyle v) => Lights.TryGetValue(v, out var s) ? s : Lights[LightStyle.daylight];

    // ---- colour helpers ----
    public static Color C(string hex)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length == 3) hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        return Color.FromRgb((byte)Convert.ToInt32(hex[0..2], 16),
                              (byte)Convert.ToInt32(hex[2..4], 16),
                              (byte)Convert.ToInt32(hex[4..6], 16));
    }

    public static Color Shade(Color c, double amt)
    {
        double r = c.R, g = c.G, b = c.B;
        if (amt >= 0) { r += (255 - r) * amt; g += (255 - g) * amt; b += (255 - b) * amt; }
        else { r *= 1 + amt; g *= 1 + amt; b *= 1 + amt; }
        return Color.FromRgb(Clamp(r), Clamp(g), Clamp(b));
    }
    static byte Clamp(double v) => (byte)Math.Clamp(v, 0, 255);

    /// Applies the style's saturation / contrast / darkness / monochrome to a colour.
    public static Color Styled(Color c, StyleSpec s)
    {
        double lum = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        double sat = s.Saturation * (1 - s.Monochrome);
        double r = lum + (c.R - lum) * sat;
        double g = lum + (c.G - lum) * sat;
        double b = lum + (c.B - lum) * sat;
        r = 128 + (r - 128) * s.Contrast;
        g = 128 + (g - 128) * s.Contrast;
        b = 128 + (b - 128) * s.Contrast;
        double d = 1 - s.Darkness;
        return Color.FromRgb(Clamp(r * d), Clamp(g * d), Clamp(b * d));
    }

    public static SolidColorBrush Brush(Color c) => new(c);
    /// Alpha'da bir fırça (DrawEllipse/DrawRectangle vb. için).
    public static SolidColorBrush Alpha(Color c, double a) => new(Color.FromArgb(Clamp(a * 255), c.R, c.G, c.B));
    /// Alpha'da bir renk (gradient constructor'ları Color bekler).
    public static Color AlphaColor(Color c, double a) => Color.FromArgb(Clamp(a * 255), c.R, c.G, c.B);
    public static Pen Pen(Color c, double w) => new(Brush(c), w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
    public static Pen Pen(Brush brush, double w) => new(brush, w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
    public static RadialGradientBrush Radial(Color center, Color edge) => new(new GradientStopCollection
    {
        new GradientStop(center, 0), new GradientStop(edge, 1)
    });
    public static LinearGradientBrush Linear(Color start, Color end) => new(new GradientStopCollection
    {
        new GradientStop(start, 0), new GradientStop(end, 1)
    }, new Point(0, 0), new Point(0, 1));
    public static LinearGradientBrush Linear(Color start, Color middle, Color end) => new(new GradientStopCollection
    {
        new GradientStop(start, 0), new GradientStop(middle, .5), new GradientStop(end, 1)
    }, new Point(0, 0), new Point(1, 0));
    public static Pen? OutlinePen(Color? c, double w) => c == null ? null : Pen(c.Value, Math.Max(0.6, w));
}
