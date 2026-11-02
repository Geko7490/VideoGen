using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LocalAIVideo.Models;

namespace LocalAIVideo.Engine;

/// <summary>
/// The Procedural 2D Scene Engine. Composes FAR → BACK → OBJECTS → CHARACTERS → FOREGROUND
/// with per-layer parallax, then lighting, bloom and grain. Renders to a bitmap so it can drive
/// both the live preview and the video encoder.
/// </summary>
public static class SceneRenderer
{
    public static RenderTargetBitmap Render(SceneSpec spec, StyleBible bible, double t, double progress,
        int width, int height, int seed, EngineQuality quality)
    {
        var dv = BuildDrawing(spec, bible, t, progress, width, height, seed, quality);
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    /// Builds the scene as a DrawingVisual (used by both the preview and the video encoder).
    public static DrawingVisual BuildDrawing(SceneSpec spec, StyleBible bible, double t, double progress,
        double width, double height, int seed, EngineQuality quality)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            Render(dc, spec, bible, t, progress, width, height, seed, quality);
        return dv;
    }

    public static void Render(DrawingContext dc, SceneSpec spec, StyleBible bible, double t, double progress,
        double W, double H, int seed, EngineQuality quality)
    {
        var st = Palette.Style(bible.Style);
        var light = Palette.Light(spec.Lighting.Style);
        double horizon = H * (EnvironmentRenderer.IsIndoor(spec.Environment.Type) ? .72 : .68);
        var cam = Camera.Compute(spec.Camera, progress, t, seed);
        double tAnim = t * st.AnimSpeed;
        double intensity = spec.Lighting.Intensity;
        double flick = light.Flicker > 0 ? 1 - light.Flicker * (.5 + .5 * Math.Sin(tAnim * 23) * Math.Sin(tAnim * 7.1)) : 1;

        // base fill
        dc.DrawRectangle(Palette.Brush(Palette.C("#05070c")), null, new Rect(0, 0, W, H));

        // ---- layers with parallax ----
        Push(dc, cam.ForDepth(.22, W, H));
        EnvironmentRenderer.DrawFar(dc, spec, bible, W, H, seed);
        dc.Pop();

        Push(dc, cam.ForDepth(.5, W, H));
        EnvironmentRenderer.DrawBack(dc, spec, bible, W, H, horizon, seed);
        dc.Pop();

        // ---- objects (depth 0.95) ----
        Push(dc, cam.ForDepth(.95, W, H));
        foreach (var ob in spec.Objects.Where(o => o.Position != ObjPos.character_hand).OrderBy(o => o.Position == ObjPos.background ? 0 : 1))
        {
            double x = ObX(ob.Position, W);
            double baseY = ob.Position switch
            {
                ObjPos.foreground => H * 1.0,
                ObjPos.background => horizon - H * .05,
                EnvironmentRenderer.IsIndoor(spec.Environment.Type) && EnvironmentRenderer.IsWall(ob.Type) => horizon - H * .26,
                _ => horizon,
            };
            double size = W * .15 * ob.Scale * (ob.Position == ObjPos.foreground ? 1.7 : ob.Position == ObjPos.background ? .6 : 1);
            EnvironmentRenderer.DrawObject(dc, ob, x, baseY, size, st, spec.Environment.Time == TimeOfDay.night);
        }
        dc.Pop();

        // ---- characters (depth 1.0) ----
        Push(dc, cam.ForDepth(1.0, W, H));
        var held = spec.Objects.FirstOrDefault(o => o.Position == ObjPos.character_hand);
        var ordered = spec.Characters
            .OrderBy(c => Math.Abs(.5 - PosX(c.Position)))
            .ToList();
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var c = ordered[i];
            var def = bible.Characters.FirstOrDefault(d => d.Id == c.Id) ?? CharacterRenderer.DefaultDef(c.Id);
            double baseH = H * .46 * c.Scale;
            Point pos = new(W * PosX(c.Position), horizon + H * .02 * (1 - c.Scale));
            CharacterRenderer.Draw(dc, c, def, tAnim, pos, baseH, st, light.Rim, light.RimAlpha, seed + i * 977);
            if (i == 0 && held != null)
                EnvironmentRenderer.DrawObject(dc, held, pos.X, pos.Y - baseH * .35, baseH * .16 * held.Scale, st, spec.Environment.Time == TimeOfDay.night);
        }
        dc.Pop();

        // ---- foreground (1.4) ----
        Push(dc, cam.ForDepth(1.4, W, H));
        EnvironmentRenderer.DrawForeground(dc, spec, bible, W, H);
        dc.Pop();

        // ---- lighting ----
        DrawLighting(dc, W, H, light, intensity, flick, spec.Lighting.Direction);

        // ---- bloom / grain ----
        if (st.Bloom > 0 && quality != EngineQuality.low)
        {
            var g = new RadialGradientBrush(Palette.AlphaColor(Palette.C(bible.Primary), st.Bloom * .25), Colors.Transparent);
            dc.DrawRectangle(g, null, new Rect(0, 0, W, H));
        }
        DrawGrain(dc, W, H, st.Grain, t, quality);
    }

    private static void Push(DrawingContext dc, Matrix m) => dc.PushTransform(m);

    private static double PosX(ScreenPos p) => p switch
    {
        ScreenPos.far_left => .12, ScreenPos.left => .28, ScreenPos.center => .5,
        ScreenPos.right => .72, ScreenPos.far_right => .88, _ => .5,
    };
    private static double ObX(ObjPos p, double W) => p switch
    {
        ObjPos.far_left => W * .1, ObjPos.left => W * .26, ObjPos.center => W * .5,
        ObjPos.right => W * .74, ObjPos.far_right => W * .9, _ => W * .5,
    };

    private static void DrawLighting(DrawingContext dc, double W, double H, LightSpec light, double intensity, double flick, LightDir dir)
    {
        // ambient wash
        dc.DrawRectangle(Palette.Alpha(light.Ambient, light.AmbientAlpha * (1.25 - intensity * .5)), null, new Rect(0, 0, W, H));

        // key light
        var o = LightOrigin(dir, W, H);
        var key = new RadialGradientBrush(Palette.AlphaColor(light.Key, light.KeyAlpha * intensity * flick), Colors.Transparent) { Center = new Point(o.X / W, o.Y / H), RadiusX = Math.Max(W, H) / W / (light.Beam ? 1.2 : 1) };
        dc.DrawRectangle(key, null, new Rect(0, 0, W, H));

        // beam
        if (light.Beam)
        {
            var beam = new LinearGradientBrush(Palette.AlphaColor(light.Key, .22 * intensity * flick), Colors.Transparent) { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(o.X, o.Y) };
            fig.Segments.Add(new LineSegment(new Point(o.X - W * .55, H * 1.1), true));
            fig.Segments.Add(new LineSegment(new Point(o.X + W * .3, H * 1.1), true));
            fig.IsClosed = true;
            geo.Figures.Add(fig);
            dc.DrawGeometry(beam, null, geo);
        }

        // vignette
        var vig = new RadialGradientBrush(Colors.Transparent, Palette.AlphaColor(Colors.Black, light.Vignette)) { GradientOrigin = new Point(.5, .5) };
        dc.DrawRectangle(vig, null, new Rect(0, 0, W, H));
    }

    private static Point LightOrigin(LightDir dir, double W, double H) => dir switch
    {
        LightDir.left => new Point(W * .05, H * .25),
        LightDir.right => new Point(W * .95, H * .25),
        LightDir.top => new Point(W * .5, -H * .1),
        LightDir.window => new Point(W * .78, H * .3),
        LightDir.front => new Point(W * .5, H * .45),
        _ => new Point(W * .5, H * 1.05),
    };

    private static void DrawGrain(DrawingContext dc, double W, double H, double amount, double t, EngineQuality q)
    {
        if (amount <= .005) return;
        int step = q switch { EngineQuality.low => 5, EngineQuality.medium => 3, _ => 2 };
        var r = new Random((int)(t * 24) * 7919);
        var brush = new VisualBrush(null);
        // cheap grain: scatter tiny semi-transparent dots
        var dots = new DrawingGroup();
        using (var ddc = dots.Open())
        {
            for (int y = 0; y < H; y += step * 4)
                for (int x = 0; x < W; x += step * 4)
                {
                    double v = r.NextDouble();
                    if (v > .55)
                    {
                        var c = v > .8 ? Colors.White : Colors.Black;
                        ddc.DrawRectangle(Palette.Alpha(c, amount * .6), null, new Rect(x, y, step, step));
                    }
                }
        }
        dc.DrawDrawing(dots);
    }
}
