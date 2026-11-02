using System.Windows;
using System.Windows.Media;
using LocalAIVideo.Models;

namespace LocalAIVideo.Engine;

/// <summary>Procedural atmosphere/effects. Deterministic from (index, time) — no state, cheap, no DOM.</summary>
public static class EffectsRenderer
{
    private static double Dens(EngineQuality q) => q switch { EngineQuality.low => .35, EngineQuality.medium => .7, _ => 1.0 };

    public static void Draw(DrawingContext dc, EffectKind fx, double W, double H, double t, int seed, EngineQuality q)
    {
        switch (fx)
        {
            case EffectKind.rain: Rain(dc, W, H, t, seed, q); break;
            case EffectKind.snow: Snow(dc, W, H, t, seed, q); break;
            case EffectKind.fog: Fog(dc, W, H, t, seed, q); break;
            case EffectKind.smoke: Smoke(dc, W, H, t, seed, q); break;
            case EffectKind.fire: Fire(dc, W, H, t, seed, q); break;
            case EffectKind.dust:
            case EffectKind.floating_dust: Dust(dc, W, H, t, seed, q); break;
            case EffectKind.particles: Particles(dc, W, H, t, seed, q); break;
            case EffectKind.lightning: Lightning(dc, W, H, t, seed); break;
            case EffectKind.wind: Wind(dc, W, H, t, seed, q); break;
            case EffectKind.moving_shadows: MovingShadows(dc, W, H, t, seed, q); break;
            case EffectKind.screen_flicker: Flicker(dc, W, H, t); break;
            case EffectKind.glow: Glow(dc, W, H, t); break;
        }
    }

    private static void Rain(DrawingContext dc, double W, double H, double t, int seed, EngineQuality q)
    {
        int n = (int)(240 * Dens(q));
        var r = new Random(seed + 1);
        var pen = Palette.Pen(Palette.Alpha(Palette.C("#beddff"), .55), Math.Max(1, W * .0016));
        for (int i = 0; i < n; i++)
        {
            double x0 = r.NextDouble() * W * 1.3 - W * .15;
            double speed = 700 + r.NextDouble() * 700;
            double len = H * (.035 + r.NextDouble() * .05);
            double y = (r.NextDouble() * H + t * speed) % (H * 1.3) - H * .15;
            dc.DrawLine(pen, new Point(x0, y), new Point(x0 - W * .012, y + len));
        }
    }

    private static void Snow(DrawingContext dc, double W, double H, double t, int seed, EngineQuality q)
    {
        int n = (int)(170 * Dens(q));
        var r = new Random(seed + 2);
        var brush = Palette.Alpha(Colors.White, .8);
        for (int i = 0; i < n; i++)
        {
            double baseX = r.NextDouble() * W;
            double sp = 40 + r.NextDouble() * 90;
            double y = (r.NextDouble() * H + t * sp) % (H * 1.2) - H * .1;
            double x = baseX + Math.Sin(t * .7 + i) * W * .03;
            dc.DrawEllipse(brush, null, new Point(x, y), W * (.0018 + r.NextDouble() * .004), W * (.0018 + r.NextDouble() * .004));
        }
    }

    private static void Fog(DrawingContext dc, double W, double H, double t, int seed, EngineQuality q)
    {
        int n = (int)(9 * Dens(q)) + 3;
        var r = new Random(seed + 3);
        for (int i = 0; i < n; i++)
        {
            double y = H * (.45 + r.NextDouble() * .5);
            double x = (r.NextDouble() * 1.6 - .3) * W + t * (6 + r.NextDouble() * 18);
            double rad = W * (.22 + r.NextDouble() * .3);
            var g = Palette.Radial(Palette.AlphaColor(Palette.C("#cdd7e6"), .1 + r.NextDouble() * .13), Colors.Transparent);
            dc.DrawEllipse(g, null, new Point(x % (W * 1.8), y), rad, rad);
        }
    }

    private static void Dust(DrawingContext dc, double W, double H, double t, int seed, EngineQuality q)
    {
        int n = (int)(42 * Dens(q));
        var r = new Random(seed + 21);
        for (int i = 0; i < n; i++)
        {
            double bx = r.NextDouble() * W, by = r.NextDouble() * H, sp = .15 + r.NextDouble() * .4;
            double x = bx + Math.Sin(t * sp + i) * W * .03;
            double y = (by - t * sp * 14 + H) % H;
            double rad = W * (.0012 + r.NextDouble() * .0026);
            var g = new RadialGradientBrush(Palette.AlphaColor(Palette.C("#fff3d6"), .25 + r.NextDouble() * .45), Colors.Transparent);
            dc.DrawEllipse(g, null, new Point(x, y), rad * 3, rad * 3);
        }
    }

    private static void Particles(DrawingContext dc, double W, double H, double t, int seed, EngineQuality q)
    {
        int n = (int)(48 * Dens(q));
        var r = new Random(seed + 8);
        for (int i = 0; i < n; i++)
        {
            double ph = t * (.2 + r.NextDouble() * .5) + r.NextDouble() * 10;
            double x = W * (.1 + .8 * (Math.Sin(ph) + 1) / 2);
            double y = H * (.15 + .75 * (Math.Cos(ph * .7 + i) + 1) / 2);
            double rad = W * (.004 + r.NextDouble() * .01);
            var g = new RadialGradientBrush(Palette.AlphaColor(Palette.C("#aad2ff"), .35 + r.NextDouble() * .4), Colors.Transparent);
            dc.DrawEllipse(g, null, new Point(x, y), rad, rad);
        }
    }

    private static void Smoke(DrawingContext dc, double W, double H, double t, int seed, EngineQuality q)
    {
        int n = (int)(14 * Dens(q));
        var r = new Random(seed + 4);
        for (int i = 0; i < n; i++)
        {
            double life = (t * .3 + r.NextDouble()) % 1;
            double x = W * (.3 + r.NextDouble() * .4) + Math.Sin(life * 4 + i) * W * .04;
            double y = H * (.95 - life * .8);
            double rad = W * (.04 + life * .18);
            var g = new RadialGradientBrush(Palette.AlphaColor(Palette.C("#5a5f69"), .3 * (1 - life)), Colors.Transparent);
            dc.DrawEllipse(g, null, new Point(x, y), rad, rad);
        }
    }

    private static void Fire(DrawingContext dc, double W, double H, double t, int seed, EngineQuality q)
    {
        int n = (int)(30 * Dens(q));
        var r = new Random(seed + 5);
        double cx = W * .5, cy = H * .9;
        for (int i = 0; i < n; i++)
        {
            double life = (t * 1.4 + r.NextDouble()) % 1;
            double x = cx + (r.NextDouble() - .5) * W * .16 + Math.Sin(life * 6 + i) * W * .02;
            double y = cy - life * H * .3;
            double rad = W * (.035 * (1 - life) + .008);
            var g = new RadialGradientBrush(Palette.AlphaColor(Palette.C("#ff8c3c"), .75 * (1 - life)), Colors.Transparent);
            dc.DrawEllipse(g, null, new Point(x, y), rad, rad);
        }
    }

    private static void Lightning(DrawingContext dc, double W, double H, double t, int seed)
    {
        double cycle = 3.4;
        double p = (t % cycle) / cycle;
        if (p > .07) return;
        double flash = Math.Max(0, 1 - p / .07);
        dc.DrawRectangle(Palette.Alpha(Palette.C("#e1ebff"), flash * .55), null, new Rect(0, 0, W, H));
        var r = new Random(seed + (int)(t / cycle) * 977);
        var pen = Palette.Pen(Palette.Alpha(Colors.White, flash), Math.Max(1.6, W * .004));
        var geo = new PathGeometry();
        var fig = new PathFigure();
        double x = W * (.2 + r.NextDouble() * .6), y = 0;
        fig.StartPoint = new Point(x, y);
        while (y < H * .6)
        {
            x += (r.NextDouble() - .5) * W * .12;
            y += H * (.05 + r.NextDouble() * .08);
            fig.Segments.Add(new LineSegment(new Point(x, y), true));
        }
        geo.Figures.Add(fig);
        dc.DrawGeometry(null, pen, geo);
    }

    private static void Wind(DrawingContext dc, double W, double H, double t, int seed, EngineQuality q)
    {
        int n = (int)(16 * Dens(q));
        var r = new Random(seed + 30);
        for (int i = 0; i < n; i++)
        {
            double y = r.NextDouble() * H;
            double x = (r.NextDouble() * 1.4 - .2) * W + t * (120 + r.NextDouble() * 220);
            double len = W * (.06 + r.NextDouble() * .14);
            x %= W * 1.5;
            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(x, y) };
            fig.Segments.Add(new QuadraticBezierSegment(new Point(x + len * .5, y - H * .01), new Point(x + len, y), true));
            geo.Figures.Add(fig);
            dc.DrawGeometry(null, Palette.Pen(Palette.Alpha(Colors.White, .1 + r.NextDouble() * .25), Math.Max(1, W * .0015)), geo);
        }
    }

    private static void MovingShadows(DrawingContext dc, double W, double H, double t, int seed, EngineQuality q)
    {
        int n = Math.Max(2, (int)(3 * Dens(q)));
        var r = new Random(seed + 33);
        for (int i = 0; i < n; i++)
        {
            double speed = .06 + r.NextDouble() * .1;
            double phase = (t * speed + r.NextDouble()) % 1.4 - .2;
            double x = phase * W * 1.3;
            double wdt = W * (.18 + r.NextDouble() * .22);
            var g = Palette.Linear(Colors.Transparent, Palette.AlphaColor(Colors.Black, .3 + r.NextDouble() * .2), Colors.Transparent);
            g.StartPoint = new Point(0, 0);
            g.EndPoint = new Point(1, 0);
            dc.DrawRectangle(g, null, new Rect(x - wdt, 0, wdt * 2, H));
        }
    }

    private static void Flicker(DrawingContext dc, double W, double H, double t)
    {
        double n = Math.Sin(t * 37) * Math.Sin(t * 13.3) * Math.Sin(t * 5.1);
        double a = Math.Max(0, n) * .18;
        if (a > 0.01) dc.DrawRectangle(Palette.Alpha(Palette.C("#96c8ff"), a), null, new Rect(0, 0, W, H));
    }

    private static void Glow(DrawingContext dc, double W, double H, double t)
    {
        double pulse = .55 + Math.Sin(t * 1.6) * .18;
        var g = new RadialGradientBrush(Palette.AlphaColor(Palette.C("#ffecbe"), .2 * pulse), Colors.Transparent);
        dc.DrawRectangle(g, null, new Rect(0, 0, W, H));
    }
}

public enum EngineQuality { low, medium, high }
