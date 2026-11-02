using System.Windows;
using System.Windows.Media;
using LocalAIVideo.Models;

namespace LocalAIVideo.Engine;

/// <summary>Procedural environment engine — rooms, streets, nature, space. All drawn, none loaded.</summary>
public static class EnvironmentRenderer
{
    public static bool IsIndoor(EnvType e) => e is
        EnvType.bedroom or EnvType.living_room or EnvType.kitchen or EnvType.office or
        EnvType.classroom or EnvType.hospital or EnvType.store or EnvType.warehouse or
        EnvType.hallway or EnvType.basement;

    /// Objects that hang on the wall rather than standing on the floor.
    public static bool IsWall(string type) => type is "window" or "poster" or "clock" or "mirror";

    private static readonly Dictionary<EnvType, (string wall, string floor)> Room = new()
    {
        [EnvType.bedroom] = ("#d8ccdd", "#9b7b64"), [EnvType.living_room] = ("#e6d9c6", "#9c6f43"),
        [EnvType.kitchen] = ("#e8efe6", "#b9bec4"), [EnvType.office] = ("#cfd8e3", "#8c99a8"),
        [EnvType.classroom] = ("#e3ddc8", "#9e8f74"), [EnvType.hospital] = ("#dbe7ef", "#c3d3dd"),
        [EnvType.store] = ("#f1e4cf", "#b98f63"), [EnvType.warehouse] = ("#4a4f57", "#3a3f46"),
        [EnvType.hallway] = ("#cbc7bd", "#8a8279"), [EnvType.basement] = ("#4e4a45", "#3a3733"),
    };

    public static void DrawFar(DrawingContext dc, SceneSpec spec, StyleBible bible, double W, double H, int seed)
    {
        var st = Palette.Style(bible.Style);
        switch (spec.Environment.Type)
        {
            case EnvType.space:
                dc.DrawRectangle(Palette.Brush(Palette.Styled(Palette.C("#05060f"), st)), null, new Rect(-W, -H, W * 3, H * 3));
                var starsRandom = new Random(seed);
                for (int i = 0; i < 120; i++)
                {
                    dc.DrawEllipse(Palette.Alpha(Colors.White, .2 + starsRandom.NextDouble() * .75), null,
                        new Point(starsRandom.NextDouble() * W * 1.6 - W * .3, starsRandom.NextDouble() * H * .62), .4 + starsRandom.NextDouble() * 1.6, .4 + starsRandom.NextDouble() * 1.6);
                }
                var neb = Palette.Radial(Palette.AlphaColor(Palette.C(bible.Primary), .32), Colors.Transparent);
                dc.DrawRectangle(neb, null, new Rect(-W, -H, W * 3, H * 3));
                var pg = Palette.Radial(Palette.Styled(Palette.C(bible.Secondary), st), Palette.C("#0b1020"));
                pg.GradientOrigin = new Point(-.3, -.3);
                dc.DrawEllipse(pg, null, new Point(W * .22, H * .68), W * .26, W * .26);
                break;

            case EnvType.@abstract:
                var bg = Palette.Linear(Palette.Styled(Palette.C(bible.Sky), st), Palette.Styled(Palette.C(bible.Primary), st));
                bg.StartPoint = new Point(0, 0);
                bg.EndPoint = new Point(1, 1);
                dc.DrawRectangle(bg, null, new Rect(-W, -H, W * 3, H * 3));
                var r2 = new Random(seed);
                for (int i = 0; i < 14; i++)
                {
                    var c = i % 2 == 0 ? Palette.C(bible.Accent) : Palette.C(bible.Secondary);
                    var g = Palette.Radial(Palette.AlphaColor(c, .22), Colors.Transparent);
                    double x = r2.NextDouble() * W, y = r2.NextDouble() * H, rad = W * (.08 + r2.NextDouble() * .3);
                    dc.DrawEllipse(g, null, new Point(x, y), rad, rad);
                }
                break;

            case EnvType.cave:
                dc.DrawRectangle(Palette.Brush(Palette.Styled(Palette.C("#161210"), st)), null, new Rect(-W, -H, W * 3, H * 3));
                break;

            default:
                if (IsIndoor(spec.Environment.Type) || spec.Environment.Type == EnvType.cave)
                {
                    var (wall, _) = Room.TryGetValue(spec.Environment.Type, out var c) ? c : ("#cbc7bd", "#8a8279");
                    bool night = spec.Environment.Time == TimeOfDay.night;
                    var wc = Palette.Styled(night ? Palette.Shade(Palette.C(wall), -.5) : Palette.C(wall), st);
                    dc.DrawRectangle(Palette.Brush(wc), null, new Rect(-W, -H, W * 3, H * 3));
                }
                else
                {
                    var (top, bot) = Palette.Sky[spec.Environment.Time];
                    var sky = Palette.Linear(Palette.Styled(top, st), Palette.Styled(bot, st));
                    sky.StartPoint = new Point(0, 0);
                    sky.EndPoint = new Point(0, 1);
                    dc.DrawRectangle(sky, null, new Rect(-W, -H, W * 3, H * 3));
                    if (spec.Environment.Time == TimeOfDay.night)
                    {
                        var nightStarsRandom = new Random(seed);
                        for (int i = 0; i < 70; i++)
                            dc.DrawEllipse(Palette.Alpha(Colors.White, .2 + nightStarsRandom.NextDouble() * .7), null, new Point(nightStarsRandom.NextDouble() * W * 1.6 - W * .3, nightStarsRandom.NextDouble() * H * .6), .4 + nightStarsRandom.NextDouble(), .4 + nightStarsRandom.NextDouble());
                        dc.DrawEllipse(Palette.Alpha(Palette.C("#f2f6ff"), .95), null, new Point(W * .76, H * .16), W * .05, W * .05);
                    }
                    // distant silhouettes
                    var skylineRandom = new Random(seed + 3);
                    if (spec.Environment.Type is EnvType.city or EnvType.street or EnvType.rooftop)
                    {
                        var far = Palette.Brush(Palette.Styled(Palette.C(spec.Environment.Time == TimeOfDay.night ? "#101627" : "#6d7f99"), st));
                        double x = -W * .5;
                        var geo = new PathGeometry();
                        var fig = new PathFigure { StartPoint = new Point(x, H) };
                        while (x < W * 1.6)
                        {
                            double bw = W * (.05 + skylineRandom.NextDouble() * .08), bh = H * (.1 + skylineRandom.NextDouble() * .26);
                            fig.Segments.Add(new LineSegment(new Point(x, H - bh), true));
                            fig.Segments.Add(new LineSegment(new Point(x + bw, H - bh), true));
                            x += bw;
                        }
                        fig.Segments.Add(new LineSegment(new Point(x, H), true));
                        geo.Figures.Add(fig);
                        dc.DrawGeometry(far, null, geo);
                    }
                }
                break;
        }
    }

    public static void DrawBack(DrawingContext dc, SceneSpec spec, StyleBible bible, double W, double H, double horizon, int seed)
    {
        var st = Palette.Style(bible.Style);
        bool night = spec.Environment.Time == TimeOfDay.night;
        double detail = st.Flat ? 1.0 : 1.0;
        var rng = new Random(seed + 11);

        if (IsIndoor(spec.Environment.Type))
        {
            var (wallHex, floorHex) = Room.TryGetValue(spec.Environment.Type, out var c) ? c : ("#cbc7bd", "#8a8279");
            var wc = Palette.Styled(Palette.C(night ? "#2b3350" : wallHex), st);
            var wc2 = Palette.Styled(Palette.C(floorHex), st);
            var wall = Palette.Linear(Palette.Shade(wc, -.12), Palette.Shade(wc, .06));
            wall.StartPoint = new Point(0, 0);
            wall.EndPoint = new Point(0, 1);
            dc.DrawRectangle(wall, null, new Rect(-W, -H, W * 3, horizon + H * .5));
            var floor = Palette.Linear(Palette.Shade(wc2, -.18), Palette.Shade(wc2, .1));
            floor.StartPoint = new Point(0, 0);
            floor.EndPoint = new Point(0, 1);
            dc.DrawRectangle(floor, null, new Rect(-W, horizon, W * 3, H));
            dc.DrawRectangle(Palette.Brush(Palette.Shade(wc, -.34)), null, new Rect(-W, horizon - H * .016, W * 3, H * .016));
            if (detail > .5)
            {
                var fp = Palette.Pen(Palette.Alpha(Colors.Black, .08), Math.Max(1, W * .0014));
                for (int i = 1; i < 9; i++)
                {
                    double y = horizon + (H - horizon) * Math.Pow(i / 9.0, 1.6);
                    dc.DrawLine(fp, new Point(-W, y), new Point(W * 2, y));
                }
            }
        }
        else
        {
            switch (spec.Environment.Type)
            {
                case EnvType.street:
                case EnvType.city:
                case EnvType.rooftop:
                    var road = Palette.Brush(Palette.Styled(Palette.C("#2f3237"), st));
                    dc.DrawRectangle(road, null, new Rect(-W, horizon, W * 3, H));
                    // closer buildings
                    double x = -W * .35;
                    while (x < W * 1.45)
                    {
                        double bw = W * (.15 + rng.NextDouble() * .12), bh = H * (.22 + rng.NextDouble() * .26);
                        var bcol = Palette.Brush(Palette.Styled(Palette.C(night ? "#1d2436" : "#7d8899"), st));
                        dc.DrawRectangle(bcol, Palette.OutlinePen(st.Outline, 1), new Rect(x, horizon - bh, bw, bh));
                        for (int i = 0; i < 8; i++)
                        {
                            var lit = night ? Palette.Alpha(Palette.C("#ffd483"), rng.NextDouble() > .45 ? .85 : .08) : Palette.Alpha(Palette.C("#cfe4ff"), .5);
                            dc.DrawRectangle(lit, null, new Rect(x + bw * (.12 + (i % 3) * .3), horizon - bh + H * (.03 + (i / 3) * .07), bw * .17, H * .038));
                        }
                        x += bw + W * .02;
                    }
                    break;

                case EnvType.forest:
                case EnvType.park:
                    var ground = Palette.Brush(Palette.Styled(Palette.C(spec.Environment.Type == EnvType.park ? (night ? "#1d4529" : "#3f8f4f") : (night ? "#14301d" : "#2d6a3f")), st));
                    dc.DrawRectangle(ground, null, new Rect(-W, horizon, W * 3, H));
                    int n = spec.Environment.Type == EnvType.forest ? 9 : 4;
                    for (int i = 0; i < n; i++)
                        Tree(dc, -W * .3 + i / (double)n * W * 1.7 + rng.NextDouble() * W * .05, horizon, W * (.1 + rng.NextDouble() * .07), st, night);
                    break;

                case EnvType.beach:
                    var sea = Palette.Linear(Palette.Styled(Palette.C("#1b6ca8"), st), Palette.Styled(Palette.C("#2a9d8f"), st));
                    sea.StartPoint = new Point(0, 0);
                    sea.EndPoint = new Point(0, 1);
                    dc.DrawRectangle(sea, null, new Rect(-W, horizon - H * .22, W * 3, H * .27));
                    var sand = Palette.Linear(Palette.Styled(Palette.C("#e8d2a0"), st), Palette.Styled(Palette.C("#c9ad79"), st));
                    sand.StartPoint = new Point(0, 0);
                    sand.EndPoint = new Point(0, 1);
                    dc.DrawRectangle(sand, null, new Rect(-W, horizon, W * 3, H));
                    break;

                case EnvType.cave:
                    var floor = Palette.Brush(Palette.Styled(Palette.C("#1a1512"), st));
                    dc.DrawRectangle(floor, null, new Rect(-W, horizon, W * 3, H));
                    for (int i = 0; i < 7; i++)
                    {
                        double cx = -W * .2 + i * W * .22;
                        dc.DrawGeometry(Palette.Brush(Palette.Styled(Palette.C("#2b221c"), st)), null,
                            Tri(cx, -H * .1, cx + W * .035, H * (.16 + rng.NextDouble() * .16), cx + W * .07, -H * .1));
                    }
                    break;
            }
        }
    }

    public static void DrawForeground(DrawingContext dc, SceneSpec spec, StyleBible bible, double W, double H)
    {
        var st = Palette.Style(bible.Style);
        if (st.Flat && st.Grain == 0 && st.Saturation < 1) return; // minimal
        switch (spec.Environment.Type)
        {
            case EnvType.forest:
            case EnvType.park:
                dc.DrawRectangle(Palette.Alpha(Palette.C("#06140c"), .88), null, new Rect(-W * .12, -H * .1, W * .09, H * 1.3));
                dc.DrawRectangle(Palette.Alpha(Palette.C("#06140c"), .88), null, new Rect(W * 1.03, -H * .1, W * .09, H * 1.3));
                break;
            case EnvType.street:
            case EnvType.city:
                dc.DrawRectangle(Palette.Alpha(Colors.Black, .5), null, new Rect(-W * .1, H * .88, W * 1.2, H * .24));
                break;
            default:
                if (IsIndoor(spec.Environment.Type))
                {
                    var a = Palette.Alpha(Colors.Black, spec.Environment.Time == TimeOfDay.night ? .3 : .16);
                    dc.DrawRectangle(a, null, new Rect(-W * .02, -H * .1, W * .05, H * 1.3));
                    dc.DrawRectangle(a, null, new Rect(W * .95, -H * .1, W * .06, H * 1.3));
                }
                break;
        }
    }

    // ---- objects ----
    public static void DrawObject(DrawingContext dc, ObjectInScene ob, double x, double baseY, double size, StyleSpec st, bool night)
    {
        var pen = Palette.OutlinePen(st.Outline, Math.Max(.8, size * .012));
        try
        {
            switch (ob.Type)
            {
                case "window":
                {
                    var wc = Palette.Brush(Palette.Styled(Palette.C("#55606e"), st));
                    dc.DrawGeometry(wc, pen, RRect(x - size * .5, baseY, size, size * 1.1, size * .03));
                    var wg = night ? Palette.Brush(Palette.C("#0d1733")) : Palette.Brush(Palette.C("#bfe3ff"));
                    dc.DrawRectangle(wg, null, new Rect(x - size * .44, baseY + size * .05, size * .88, size));
                    if (night) dc.DrawEllipse(Palette.Alpha(Colors.White, .85), null, new Point(x + size * .24, baseY + size * .3), size * .1, size * .1);
                    break;
                }
                case "lamp":
                {
                    dc.DrawRectangle(Palette.Brush(Palette.Styled(Palette.C("#4a4f57"), st)), null, new Rect(x - size * .05, baseY - size * 1.1, size * .1, size * 1.1));
                    var shadeCol = night || ob.State == "on" ? Palette.C("#ffdd96") : Palette.C("#555b63");
                    dc.DrawGeometry(Palette.Brush(shadeCol), pen,
                        Poly(x - size * .42, baseY - size * 1.1, x + size * .42, baseY - size * 1.1, x + size * .26, baseY - size * 1.5, x - size * .26, baseY - size * 1.5));
                    if (night || ob.State == "on")
                    {
                        var g = new RadialGradientBrush(Palette.AlphaColor(Palette.C("#ffdd96"), .5), Colors.Transparent);
                        dc.DrawEllipse(g, null, new Point(x, baseY - size * 1.15), size * 2.6, size * 2.6);
                    }
                    break;
                }
                case "door":
                {
                    var frame = Palette.Brush(Palette.Styled(Palette.C("#3b2a1c"), st));
                    dc.DrawRectangle(frame, null, new Rect(x - size * .56, baseY - size * 2.08, size * 1.12, size * 2.08));
                    var leaf = Palette.Brush(Palette.Styled(Palette.C("#7a5536"), st));
                    double openAmt = ob.State == "open" ? 1 : (ob.State == "moving" ? .5 : 0);
                    dc.DrawRectangle(leaf, pen, new Rect(x - size * .5, baseY - size * 2.0, size * (1 - openAmt * .8), size * 2.0));
                    dc.DrawEllipse(Palette.Brush(ob.State == "locked" ? Palette.C("#c0392b") : Palette.C("#e9c46a")), null, new Point(x - size * .5 + size * (1 - openAmt * .8) * .82, baseY - size), size * .06, size * .06);
                    break;
                }
                case "bed":
                {
                    dc.DrawRectangle(Palette.Brush(Palette.Styled(Palette.C("#6b4a2f"), st)), null, new Rect(x - size * .5, baseY - size * .26, size, size * .12));
                    dc.DrawGeometry(Palette.Brush(Palette.Styled(Palette.C("#5b7bd5"), st)), pen, RRect(x - size * .5, baseY - size * .36, size, size * .12, size * .03));
                    dc.DrawGeometry(Palette.Brush(Palette.Styled(Palette.C("#f1f3f5"), st)), pen, RRect(x - size * .46, baseY - size * .44, size * .28, size * .1, size * .03));
                    break;
                }
                case "tree":
                    Tree(dc, x, baseY, size, st, night);
                    break;
                case "streetlight":
                {
                    dc.DrawRectangle(Palette.Brush(Palette.Styled(Palette.C("#3f454e"), st)), null, new Rect(x - size * .05, baseY - size * 2.4, size * .1, size * 2.4));
                    if (night)
                    {
                        var g = new RadialGradientBrush(Palette.AlphaColor(Palette.C("#ffe096"), .55), Colors.Transparent);
                        dc.DrawEllipse(g, null, new Point(x - size * .6, baseY - size * 2.25), size * 2.4, size * 2.4);
                    }
                    break;
                }
                case "car":
                {
                    var paint = Palette.Brush(Palette.Styled(Palette.C("#c92a2a"), st));
                    dc.DrawGeometry(paint, pen, RRect(x - size * .6, baseY - size * .34, size * 1.2, size * .26, size * .06));
                    dc.DrawGeometry(paint, pen, Poly(x - size * .32, baseY - size * .34, x - size * .18, baseY - size * .58, x + size * .2, baseY - size * .58, x + size * .34, baseY - size * .34));
                    dc.DrawRectangle(Palette.Brush(Palette.C("#9ad2f5")), null, new Rect(x - size * .15, baseY - size * .54, size * .3, size * .18));
                    dc.DrawEllipse(Palette.Brush(Palette.C("#16181c")), null, new Point(x - size * .34, baseY - size * .06), size * .11, size * .11);
                    dc.DrawEllipse(Palette.Brush(Palette.C("#16181c")), null, new Point(x + size * .34, baseY - size * .06), size * .11, size * .11);
                    break;
                }
                case "box":
                {
                    dc.DrawGeometry(Palette.Brush(Palette.Styled(Palette.C("#8a6a45"), st)), pen, RRect(x - size * .32, baseY - size * .52, size * .64, size * .52, size * .02));
                    if (ob.State == "open")
                        dc.DrawGeometry(Palette.Brush(Palette.Styled(Palette.C("#9c7a52"), st)), pen, Poly(x - size * .32, baseY - size * .52, x - size * .46, baseY - size * .68, x - size * .1, baseY - size * .68, x, baseY - size * .52));
                    break;
                }
                case "chair":
                {
                    var wood = Palette.Brush(Palette.Styled(Palette.C("#6b4a2f"), st));
                    dc.DrawRectangle(wood, pen, new Rect(x - size * .3, baseY - size * .3, size * .6, size * .06));
                    dc.DrawRectangle(wood, pen, new Rect(x - size * .3, baseY - size * .72, size * .1, size * .44));
                    dc.DrawRectangle(wood, null, new Rect(x - size * .26, baseY - size * .26, size * .05, size * .26));
                    dc.DrawRectangle(wood, null, new Rect(x + size * .2, baseY - size * .26, size * .05, size * .26));
                    break;
                }
                case "table":
                {
                    var wood = Palette.Brush(Palette.Styled(Palette.C("#8a5a33"), st));
                    dc.DrawGeometry(wood, pen, RRect(x - size * .5, baseY - size * .34, size, size * .07, size * .015));
                    dc.DrawRectangle(wood, null, new Rect(x - size * .42, baseY - size * .28, size * .06, size * .28));
                    dc.DrawRectangle(wood, null, new Rect(x + size * .36, baseY - size * .28, size * .06, size * .28));
                    break;
                }
                default:
                    dc.DrawGeometry(Palette.Brush(Palette.Styled(Palette.C("#8a6a45"), st)), pen, RRect(x - size * .25, baseY - size * .3, size * .5, size * .3, size * .02));
                    break;
            }
        }
        catch { /* a single bad object must never crash the frame */ }
    }

    private static void Tree(DrawingContext dc, double x, double baseY, double size, StyleSpec st, bool night)
    {
        var trunk = Palette.Brush(Palette.Styled(Palette.C("#5a3d26"), st));
        var leaf = Palette.Brush(Palette.Styled(Palette.C(night ? "#1d4a2b" : "#2f8f47"), st));
        var pen = Palette.OutlinePen(st.Outline, size * .008);
        dc.DrawRectangle(trunk, null, new Rect(x - size * .07, baseY - size * .75, size * .14, size * .75));
        dc.DrawEllipse(leaf, pen, new Point(x - size * .26, baseY - size * .9), size * .34, size * .34);
        dc.DrawEllipse(leaf, pen, new Point(x + size * .26, baseY - size * .92), size * .32, size * .32);
        dc.DrawEllipse(leaf, pen, new Point(x, baseY - size * 1.18), size * .38, size * .38);
    }

    // geometry helpers
    private static RectangleGeometry RRect(double x, double y, double w, double h, double r)
        => new(new Rect(x, y, w, h), r, r);

    private static PathGeometry Poly(params double[] pts)
    {
        var g = new PathGeometry();
        var f = new PathFigure { StartPoint = new Point(pts[0], pts[1]) };
        for (int i = 2; i < pts.Length; i += 2) f.Segments.Add(new LineSegment(new Point(pts[i], pts[i + 1]), true));
        f.IsClosed = true;
        g.Figures.Add(f);
        return g;
    }

    private static PathGeometry Tri(double x0, double y0, double x1, double y1, double x2, double y2)
    {
        var g = new PathGeometry();
        var f = new PathFigure { StartPoint = new Point(x0, y0) };
        f.Segments.Add(new LineSegment(new Point(x1, y1), true));
        f.Segments.Add(new LineSegment(new Point(x2, y2), true));
        f.IsClosed = true;
        g.Figures.Add(f);
        return g;
    }
}
