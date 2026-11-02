using System.Windows;
using System.Windows.Media;
using LocalAIVideo.Models;

namespace LocalAIVideo.Engine;

/// <summary>
/// Procedural character engine. Humans are built from a parametric skeleton; animals / robots /
/// shadows / abstract are dedicated procedural bodies. Everything is drawn, nothing is loaded.
/// </summary>
public static class CharacterRenderer
{
    private record Joints(
        Point Hip, Point Chest, Point Neck, Point Head, double HeadR, double HeadTilt,
        Point ShL, Point ShR, Point ElL, Point ElR, Point HaL, Point HaR,
        Point HiL, Point HiR, Point KnL, Point KnR, Point FoL, Point FoR,
        double Lean, double Bob);

    public static void Draw(DrawingContext dc, CharacterInScene cin, CharacterDef def,
        double t, Point pos, double height, StyleSpec st, Color rim, double rimAlpha, int seed)
    {
        var rand = new Random(seed);
        switch (def.Kind)
        {
            case CharacterKind.animal: DrawAnimal(dc, cin, def, t, pos, height, st, seed); break;
            case CharacterKind.robot: DrawRobot(dc, cin, def, t, pos, height, st, seed); break;
            case CharacterKind.shadow: DrawShadow(dc, cin, t, pos, height); break;
            case CharacterKind.abstract: DrawAbstract(dc, cin, def, t, pos, height, st); break;
            default: DrawHuman(dc, cin, def, t, pos, height, st, rim, rimAlpha, seed, rand); break;
        }
    }

    private static double ActionPhase(List<ActionKind> actions, double t)
    {
        // The action chain is spread across the scene; we use t directly for continuous motion.
        return t;
    }

    private static Joints BuildSkeleton(CharacterDef def, ActionKind action, double t, double body, double headScale, double w)
    {
        var r = new Random(1);
        double idle = Math.Sin(t * 1.6) * .004;
        double bob = idle;
        double hipY = .5 * body, chestY = .72 * body, neckY = .795 * body, headY = .885 * body;
        double lean = 0;

        double hipLx = -.052 * w, hipRx = .052 * w;
        double knLx = -.055 * w, knRx = .055 * w;
        double foLx = -.062 * w, foRx = .062 * w;
        double haLx = -.12 * w, haRx = .12 * w;
        double haY = .5 * body, knY = .26 * body, foY = .015;
        double lean2 = lean;

        switch (action)
        {
            case ActionKind.walk:
            {
                double ph = t * 4.2;
                double L = Math.Sin(ph), R = Math.Sin(ph + Math.PI);
                bob = Math.Abs(Math.Sin(ph)) * .012;
                foLx = L * .11; foRx = R * .11;
                foY = Math.Max(.01, .015 + Math.Max(0, -L) * .045);
                knLx = L * .055; knRx = R * .055;
                haLx = -.1 * w + R * .07; haRx = .1 * w + L * .07;
                lean = .03;
                break;
            }
            case ActionKind.run:
            case ActionKind.flee:
            {
                double ph = t * 7.5;
                double L = Math.Sin(ph), R = Math.Sin(ph + Math.PI);
                bob = Math.Abs(Math.Sin(ph)) * .028;
                foLx = L * .17; foRx = R * .17;
                foY = Math.Max(.01, .02 + Math.Max(0, -L) * .11);
                knLx = L * .1 + .02; knRx = R * .1 + .02;
                haLx = -.08 * w + R * .12; haRx = .08 * w + L * .12;
                haY = .56 * body;
                lean = action == ActionKind.flee ? .14 : .1;
                break;
            }
            case ActionKind.sit:
                hipY = .3 * body; chestY = .52 * body; neckY = .6 * body; headY = .69 * body;
                knLx = .11 * w; knRx = .14 * w; foLx = .12 * w; foRx = .16 * w;
                haLx = -.04 * w; haRx = .05 * w; haY = .37 * body;
                bob = idle * .6;
                break;
            case ActionKind.react:
            {
                double k = Math.Exp(-t * 1.4) * Math.Sin(t * 9);
                lean = -.09 - k * .05; bob = Math.Abs(k) * .02;
                haLx = -.17 * w; haRx = .17 * w; haY = .66 * body;
                break;
            }
            case ActionKind.reach:
            case ActionKind.grab:
                haRx = .25 * w; haY = .8 * body; lean = .06; break;
            case ActionKind.talk:
                haLx = -.14 * w + Math.Sin(t * 2.4) * .02;
                haRx = .14 * w + Math.Sin(t * 2.2) * .02;
                haY = .56 * body; break;
            case ActionKind.jump:
            {
                double j = Math.Abs(Math.Sin(t * 2.6));
                bob = j * .09; haLx = -.16 * w; haRx = .16 * w; haY = .8 * body;
                break;
            }
            case ActionKind.turn:
                lean = Math.Sin(t * .9) * .08; break;
        }

        Joints J(Point p) => default;
        var bodyScale = body;
        return new Joints(
            new Point(0, hipY), new Point(0, chestY), new Point(0, neckY),
            new Point(0, headY), .072 * headScale, 0,
            new Point(-.085 * w, .775 * body), new Point(.085 * w, .775 * body),
            new Point(-.115 * w, .635 * body), new Point(.115 * w, .635 * body),
            new Point(haLx, haY), new Point(haRx, haY),
            new Point(hipLx, hipY), new Point(hipRx, hipY),
            new Point(knLx, knY), new Point(knRx, knY),
            new Point(foLx, foY), new Point(foRx, foY),
            lean2, bob);
    }

    private static void DrawHuman(DrawingContext dc, CharacterInScene cin, CharacterDef def,
        double t, Point pos, double height, StyleSpec st, Color rim, double rimAlpha, int seed, Random rand)
    {
        double body = 1.0;
        double w = def.Build switch { "slim" => .86, "heavy" => 1.24, "tall" => .94, _ => 1.0 };
        if (def.Build == "tall") body = 1.08;
        if (def.Build == "slim") body = 1.02;
        if (def.Build == "heavy") body = .97;
        double headScale = def.HairStyle == "bun" ? 1.0 : 1.0;

        var actions = cin.Actions.Count > 0 ? cin.Actions : new List<ActionKind> { ActionKind.idle };
        double tt = t * st.AnimSpeed;
        var sk = BuildSkeleton(def, actions[0], tt, body, headScale, w);

        // Apply position, flip and lean.
        dc.PushTransform(new TranslateTransform(pos.X, pos.Y - sk.Bob * height));
        double flip = cin.FacingLeft ? -1 : 1;
        dc.PushTransform(new ScaleTransform(flip, 1));

        double U = height; // unit
        var skin = Palette.Styled(Palette.C(def.Skin), st);
        var top = Palette.Styled(Palette.C(def.TopColor), st);
        var bottom = Palette.Styled(Palette.C(def.BottomColor), st);
        var shoe = Palette.Styled(Palette.C(def.ShoColorSafe()), st);
        var hair = Palette.Styled(Palette.C(def.HairColor), st);
        double lw = U * .001;

        Point P(Point p) => new(p.X * U, -p.Y * U);
        double legW = U * .049 * w, armW = U * .038 * w, torsoW = U * .17 * w;
        var pen = Palette.OutlinePen(st.Outline, lw);

        // back leg & arm
        Limb(dc, P(sk.HiL), P(sk.KnL), P(sk.FoL), legW, Palette.Shade(bottom, -.18), pen);
        Limb(dc, P(sk.ShL), P(sk.ElL), P(sk.HaL), armW, Palette.Shade(top, -.2), pen);

        Shoe(dc, P(sk.FoL), Palette.Shade(shoe, -.2), U, pen);

        // torso
        var hipP = P(sk.Hip), chestP = P(sk.Chest), neckP = P(sk.Neck);
        var torso = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(hipP.X - torsoW * .42, hipP.Y) };
        fig.Segments.Add(new QuadraticBezierSegment(new Point(chestP.X - torsoW * .56, chestP.Y), new Point(neckP.X - torsoW * .3, neckP.Y), true));
        fig.Segments.Add(new LineSegment(new Point(neckP.X + torsoW * .3, neckP.Y), true));
        fig.Segments.Add(new QuadraticBezierSegment(new Point(chestP.X + torsoW * .56, chestP.Y), new Point(hipP.X + torsoW * .42, hipP.Y), true));
        fig.Segments.Add(new LineSegment(hipP, true));
        torso.Figures.Add(fig);
        dc.DrawGeometry(st.Flat ? Palette.Brush(top) : VerticalGradient(top, hipP, torsoW), pen, torso);

        // hips
        dc.DrawGeometry(st.Flat ? Palette.Brush(bottom) : Palette.Brush(bottom), pen,
            new RectangleGeometry(new Rect(hipP.X - torsoW * .44, hipP.Y - U * .012, torsoW * .88, U * .057)));

        // front leg, shoe, arm
        Limb(dc, P(sk.HiR), P(sk.KnR), P(sk.FoR), legW, bottom, pen);
        Shoe(dc, P(sk.FoR), shoe, U, pen);
        Limb(dc, P(sk.ShR), P(sk.ElR), P(sk.HaR), armW, top, pen);

        // hands
        dc.DrawEllipse(Palette.Brush(skin), null, P(sk.HaL), U * .023, U * .023);
        var handR = P(sk.HaR);
        dc.DrawEllipse(Palette.Brush(skin), null, handR, U * .023, U * .023);

        // neck
        dc.DrawRectangle(Palette.Brush(Palette.Shade(skin, -.14)), null,
            new Rect(neckP.X - U * .026, neckP.Y - U * .02, U * .052, U * .05));

        // head
        var headP = P(sk.Head);
        double hr = sk.HeadR * U;
        dc.PushTransform(new TranslateTransform(headP.X, headP.Y));
        dc.PushTransform(new RotateTransform(-sk.HeadTilt * 180 / Math.PI * flip));
        var headGeo = new EllipseGeometry(new Point(0, 0), hr * .88, hr);
        dc.DrawGeometry(st.Flat ? Palette.Brush(skin) : RadialGradient(skin, hr), pen, headGeo);
        // ear
        dc.DrawEllipse(Palette.Brush(Palette.Shade(skin, -.08)), null, new Point(hr * .86, hr * .06), hr * .12, hr * .18);

        DrawFace(dc, hr, cin.Emotion, cin.Actions.Contains(ActionKind.talk), tt, st, flip);
        DrawHair(dc, hr, def, hair, st, lw);
        DrawAccessory(dc, hr, def, st, lw, flip);
        dc.Pop(); dc.Pop();

        // rim light
        if (rimAlpha > 0.01 && st.Monochrome < .5)
        {
            dc.PushTransform(new TranslateTransform(headP.X, headP.Y));
            var rimPen = Palette.Pen(Palette.Shade(rim, .3), U * .009);
            dc.DrawGeometry(null, rimPen,
                new EllipseGeometry(new Point(-hr * .2 * flip, 0), hr * .9, hr * 1.02).Clone());
            dc.Pop();
        }

        dc.Pop(); // flip
        dc.Pop(); // translate
    }

    /// A safe default identity so a scene that references an unknown character still renders.
    public static CharacterDef DefaultDef(string id) => new()
    {
        Id = id,
        Name = id,
        Kind = CharacterKind.human,
        Skin = "#E8B98A",
        HairColor = "#2B2118",
        HairStyle = "short",
        TopColor = "#3B5BDB",
        BottomColor = "#343A40",
        ShoeColor = "#212529",
        Build = "average",
        Accessory = "none",
        AccessoryColor = "#DEE2E6",
    };

    private static void Limb(DrawingContext dc, Point a, Point b, Point c, double width, Color color, Pen? pen)
    {
        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = a };
        fig.Segments.Add(new QuadraticBezierSegment(b, c, true));
        geo.Figures.Add(fig);
        if (pen != null) dc.DrawGeometry(null, pen, ShiftGrow(geo, width + Math.Max(1.2, pen.Thickness * 1.6), a, c));
        dc.DrawGeometry(null, Palette.Pen(color, width), geo);
    }

    // Approximate an outline by drawing the same path with a thicker pen (cheap & effective).
    private static PathGeometry ShiftGrow(PathGeometry g, double, Point, Point) => g;

    private static void Shoe(DrawingContext dc, Point p, Color color, double U, Pen? pen)
        => dc.DrawEllipse(Palette.Brush(color), pen, new Point(p.X + U * .012, p.Y), U * .034, U * .017);

    private static void DrawFace(DrawingContext dc, double hr, Emotion emotion, bool talking, double t, StyleSpec st, double flip)
    {
        var ink = st.Outline ?? Palette.C("#16181c");
        var inkPen = Palette.Pen(ink, 1);
        double ex = hr * .33, ey = hr * .06;
        bool forceBlink = talking;
        double blink = forceBlink ? (Math.Sin(t * 4) > 0 ? .08 : 1) : (Math.Sin(t * 1.35) > .985 ? .08 : 1);

        var f = emotion switch
        {
            Emotion.happy => new FaceParams(.62, .1, .004, 1, .3, blush: true),
            Emotion.sad => new FaceParams(.82, -.42, -.001, -.85, .08, tear: true),
            Emotion.angry => new FaceParams(.95, .65, -.006, -.55, .18),
            Emotion.scared => new FaceParams(1.45, -.3, .009, -.3, .6, sweat: true),
            Emotion.surprised => new FaceParams(1.55, 0, .013, 0, .78),
            Emotion.confused => new FaceParams(1.0, .3, .006, -.2, .1, sweat: true),
            Emotion.worried => new FaceParams(.95, -.3, .003, -.45, .08, sweat: true),
            Emotion.shocked => new FaceParams(1.75, -.1, .016, -.1, .95, sweat: true),
            Emotion.evil => new FaceParams(.72, .75, -.008, .85, .22, glow: true),
            Emotion.tired => new FaceParams(.32, -.18, -.004, -.3, .12),
            Emotion.excited => new FaceParams(1.25, .12, .009, .95, .52, blush: true),
            _ => new FaceParams(1, 0, 0, .05, .06)
        };

        double open = Math.Max(.06, f.EyeOpen * blink);
        for (int side = -1; side <= 1; side += 2)
        {
            double x = side * ex + flip * hr * .04;
            dc.DrawEllipse(Colors.White, null, new Point(x, ey), hr * .15, hr * .17 * open);
            var pupil = f.Glow ? Palette.C("#ff4d4d") : ink;
            dc.DrawEllipse(Palette.Brush(pupil), null, new Point(x + flip * hr * .03, ey + hr * .01), hr * .075, hr * .095 * Math.Max(.2, open));
            if (open > .5 && !f.Glow)
                dc.DrawEllipse(Palette.Alpha(Colors.White, .9), null, new Point(x + flip * hr * .05, ey - hr * .03), hr * .028, hr * .028);

            if (f.EyeOpen < .7)
                dc.DrawLine(Palette.Pen(ink, hr * .05), new Point(x - hr * .16, ey - hr * .04), new Point(x + hr * .16, ey - hr * .04));

            // eyebrow
            dc.PushTransform(new TranslateTransform(x, ey - hr * .3 - f.BrowY * 14));
            dc.PushTransform(new RotateTransform(f.BrowAngle * -1 * side * 90 / Math.PI));
            var brow = new PathGeometry();
            var bf = new PathFigure { StartPoint = new Point(-hr * .16, 0) };
            bf.Segments.Add(new QuadraticBezierSegment(new Point(0, -hr * .06), new Point(hr * .16, 0), true));
            brow.Figures.Add(bf);
            dc.DrawGeometry(null, Palette.Pen(ink, hr * .065), brow);
            dc.Pop(); dc.Pop();
        }

        // nose
        dc.DrawLine(Palette.Pen(Palette.Alpha(ink, .5), hr * .035), new Point(flip * hr * .06, ey + hr * .18), new Point(flip * hr * .12, ey + hr * .3));

        // mouth
        double mo = talking ? f.MouthOpen * .5 + Math.Abs(Math.Sin(t * 9.5)) * .55 : f.MouthOpen;
        double my = hr * .52;
        if (mo > .3)
        {
            dc.DrawEllipse(Palette.Brush(Palette.C("#5b2130")), Palette.Pen(ink, hr * .03), new Point(flip * hr * .03, my), hr * .17, hr * .1 + hr * .16 * mo);
        }
        else
        {
            var mouth = new PathGeometry();
            var mf = new PathFigure { StartPoint = new Point(flip * hr * .03 - hr * .17, my) };
            mf.Segments.Add(new QuadraticBezierSegment(new Point(flip * hr * .03, my + f.MouthCurve * hr * .2), new Point(flip * hr * .03 + hr * .17, my), true));
            mouth.Figures.Add(mf);
            dc.DrawGeometry(null, Palette.Pen(ink, hr * .05), mouth);
        }

        if (f.Blush)
        {
            dc.DrawEllipse(Palette.Alpha(Palette.C("#ff7878"), .32), null, new Point(-hr * .46, hr * .33), hr * .14, hr * .08);
            dc.DrawEllipse(Palette.Alpha(Palette.C("#ff7878"), .32), null, new Point(hr * .46, hr * .33), hr * .14, hr * .08);
        }
        if (f.Tear)
            dc.DrawEllipse(Palette.Alpha(Palette.C("#96d2ff"), .85), null, new Point(-ex, ey + hr * .3 + (Math.Sin(t * 1.5) * .5 + .5) * hr * .3), hr * .05, hr * .08);
        if (f.Sweat)
            dc.DrawEllipse(Palette.Alpha(Palette.C("#a0dcff"), .8), null, new Point(hr * .62, -hr * .35), hr * .06, hr * .09);
    }

    private readonly record struct FaceParams(double EyeOpen, double BrowAngle, double BrowY, double MouthCurve, double MouthOpen,
        bool Blush = false, bool Tear = false, bool Sweat = false, bool Glow = false);

    private static void DrawHair(DrawingContext dc, double hr, CharacterDef def, Color hair, StyleSpec st, double lw)
    {
        if (def.HairStyle == "bald") return;
        var pen = Palette.OutlinePen(st.Outline, lw);
        var geo = new PathGeometry();
        var fig = new PathFigure();
        switch (def.HairStyle)
        {
            case "long":
            case "ponytail":
                fig.StartPoint = new Point(-hr * .92, hr * .1);
                fig.Segments.Add(new QuadraticBezierSegment(new Point(-hr * 1.05, -hr * 1.15), new Point(0, -hr * 1.06), true));
                fig.Segments.Add(new QuadraticBezierSegment(new Point(hr * 1.05, -hr * 1.15), new Point(hr * .92, hr * .1), true));
                fig.Segments.Add(new LineSegment(new Point(hr * .78, hr * (def.HairStyle == "long" ? 1.25 : .4)), true));
                fig.Segments.Add(new QuadraticBezierSegment(new Point(hr * .5, hr * .1), new Point(hr * .5, -hr * .25), true));
                fig.Segments.Add(new LineSegment(new Point(-hr * .5, -hr * .25), true));
                fig.Segments.Add(new QuadraticBezierSegment(new Point(-hr * .5, hr * .1), new Point(-hr * .78, hr * (def.HairStyle == "long" ? 1.25 : .4)), true));
                break;
            case "curly":
                for (int i = 0; i <= 10; i++)
                {
                    double a = Math.PI + (i / 10.0) * Math.PI;
                    var p = new Point(Math.Cos(a) * hr * .9, Math.Sin(a) * hr * .95 - hr * .08);
                    if (i == 0) fig.StartPoint = p;
                    fig.Segments.Add(new ArcSegment(new Point(p.X + hr * .5, p.Y), new Size(hr * .26, hr * .26), 0, true, SweepDirection.Clockwise, true));
                }
                break;
            case "bun":
                fig.StartPoint = new Point(0, -hr * 1.42);
                fig.Segments.Add(new ArcSegment(new Point(hr * .33, -hr * 1.1), new Size(hr * .33, hr * .33), 0, false, SweepDirection.Clockwise, true));
                fig.Segments.Add(new ArcSegment(new Point(-hr * .33, -hr * 1.1), new Size(hr * .33, hr * .33), 0, false, SweepDirection.Clockwise, false));
                fig.Segments.Add(new QuadraticBezierSegment(new Point(0, -hr * 1.25), new Point(-hr * .9, 0), true));
                fig.Segments.Add(new QuadraticBezierSegment(new Point(0, -hr * .55), new Point(hr * .9, 0), true));
                break;
            case "hat":
            case "hood":
                fig.StartPoint = new Point(-hr * 1.08, -hr * .1);
                fig.Segments.Add(new QuadraticBezierSegment(new Point(0, -hr * 1.5), new Point(hr * 1.08, -hr * .1), true));
                fig.Segments.Add(new LineSegment(new Point(hr * 1.2, -hr * .02), true));
                fig.Segments.Add(new LineSegment(new Point(-hr * 1.2, -hr * .02), true));
                break;
            default: // short / messy
                fig.StartPoint = new Point(-hr * .92, hr * .02);
                fig.Segments.Add(new QuadraticBezierSegment(new Point(-hr, -hr * 1.12), new Point(0, -hr * 1.04), true));
                fig.Segments.Add(new QuadraticBezierSegment(new Point(hr, -hr * 1.12), new Point(hr * .92, hr * .02), true));
                fig.Segments.Add(new QuadraticBezierSegment(new Point(hr * .55, -hr * .42), new Point(0, -hr * .38), true));
                fig.Segments.Add(new QuadraticBezierSegment(new Point(-hr * .55, -hr * .42), new Point(-hr * .92, hr * .02), true));
                break;
        }
        fig.IsClosed = true;
        geo.Figures.Add(fig);
        dc.DrawGeometry(Palette.Brush(hair), pen, geo);
    }

    private static void DrawAccessory(DrawingContext dc, double hr, CharacterDef def, StyleSpec st, double lw, double flip)
    {
        var c = Palette.Styled(Palette.C(def.AccessoryColor), st);
        switch (def.Accessory)
        {
            case "glasses":
                var gpen = Palette.Pen(c, hr * .05);
                dc.DrawGeometry(null, gpen, new EllipseGeometry(new Point(-hr * .33 + flip * hr * .04, hr * .06), hr * .2, hr * .18));
                dc.DrawGeometry(null, gpen, new EllipseGeometry(new Point(hr * .33 + flip * hr * .04, hr * .06), hr * .2, hr * .18));
                dc.DrawLine(gpen, new Point(-hr * .13, hr * .06), new Point(hr * .13, hr * .06));
                break;
            case "hood":
                dc.DrawEllipse(Palette.Brush(c), null, new Point(0, hr * 1.05), hr * .6, hr * .22);
                break;
            case "headphones":
                var hpen = Palette.Pen(c, hr * .11);
                dc.DrawGeometry(null, hpen, Arc(new Point(0, -hr * .1), hr * 1.02, 200, 340));
                dc.DrawEllipse(Palette.Brush(c), null, new Point(-hr * .95, hr * .02), hr * .14, hr * .24);
                dc.DrawEllipse(Palette.Brush(c), null, new Point(hr * .95, hr * .02), hr * .14, hr * .24);
                break;
            case "cap":
                dc.DrawEllipse(Palette.Brush(c), null, new Point(0, -hr * .9), hr, hr * .55);
                break;
        }
    }

    private static PathGeometry Arc(Point c, double r, double a0, double a1)
    {
        Point P(double a) => new(c.X + r * Math.Cos(a * Math.PI / 180), c.Y + r * Math.Sin(a * Math.PI / 180));
        var g = new PathGeometry();
        var f = new PathFigure { StartPoint = P(a0) };
        f.Segments.Add(new ArcSegment(P(a1), new Size(r, r), 0, a1 - a0 > 180, SweepDirection.Clockwise, true));
        g.Figures.Add(f);
        return g;
    }

    // ---- other kinds ----

    private static void DrawAnimal(DrawingContext dc, CharacterInScene cin, CharacterDef def, double t, Point pos, double height, StyleSpec st, int seed)
    {
        dc.PushTransform(new TranslateTransform(pos.X, pos.Y));
        double u = height * .55;
        double flip = cin.FacingLeft ? -1 : 1;
        dc.PushTransform(new ScaleTransform(flip, 1));
        var fur = Palette.Styled(Palette.C(def.Skin), st);
        var pen = Palette.OutlinePen(st.Outline, u * .004);
        double bob = Math.Abs(Math.Sin(t * 3.4)) * .03 * u;

        // legs
        for (int i = 0; i < 4; i++)
        {
            double lx = (i < 2 ? -.33 : .33) * u;
            double ph = Math.Sin(t * 3.4 + i * 1.6) * u * .09;
            dc.DrawLine(Palette.Pen(Palette.Shade(fur, i < 2 ? -.18 : 0), u * .07), new Point(lx, -bob + u * .4), new Point(lx + ph, 0));
        }
        // body
        dc.DrawEllipse(st.Flat ? Palette.Brush(fur) : RadialGradient(fur, u * .5), pen, new Point(0, -u * .4 - bob), u * .5, u * .4);
        // head
        var head = new Point(u * .52, -u * .95 - bob);
        dc.DrawEllipse(Palette.Brush(Palette.Shade(fur, .06)), pen, head, u * .17, u * .16);
        // ears
        dc.DrawGeometry(Palette.Brush(Palette.Shade(fur, -.15)), pen, Tri(head.X - u * .07, head.Y - u * .06, head.X - u * .12, head.Y - u * .4, head.X - u * .01, head.Y - u * .14));
        dc.DrawGeometry(Palette.Brush(Palette.Shade(fur, -.15)), pen, Tri(head.X + u * .07, head.Y - u * .06, head.X + u * .12, head.Y - u * .4, head.X + u * .01, head.Y - u * .14));
        // snout + eye
        dc.DrawEllipse(Palette.Brush(Palette.Shade(fur, -.12)), null, new Point(head.X + u * .12, head.Y + u * .05), u * .07, u * .05);
        dc.DrawEllipse(Palette.Brush(cin.Emotion == Emotion.evil ? Palette.C("#ff4d4d") : Palette.C("#15181d")), null, new Point(head.X + u * .05, head.Y - u * .02), u * .02, u * .025);
        dc.Pop(); dc.Pop();
    }

    private static void DrawRobot(DrawingContext dc, CharacterInScene cin, CharacterDef def, double t, Point pos, double height, StyleSpec st, int seed)
    {
        dc.PushTransform(new TranslateTransform(pos.X, pos.Y));
        double u = height;
        double flip = cin.FacingLeft ? -1 : 1;
        dc.PushTransform(new ScaleTransform(flip, 1));
        var metal = Palette.Styled(Palette.C(def.Skin), st);
        var trim = Palette.Styled(Palette.C(def.TopColor), st);
        var pen = Palette.OutlinePen(st.Outline, u * .0014);

        // legs
        double ph = Math.Sin(t * 3.6) * u * .06;
        dc.DrawLine(Palette.Pen(Palette.Shade(metal, -.2), u * .07), new Point(-u * .06, -u * .3), new Point(-u * .06 + ph, 0));
        dc.DrawLine(Palette.Pen(Palette.Shade(metal, -.2), u * .07), new Point(u * .06, -u * .3), new Point(u * .06 - ph, 0));
        // torso
        var torso = new RectangleGeometry(new Rect(-u * .1, -u * .62, u * .2, u * .33), u * .03, u * .03);
        dc.DrawGeometry(Palette.Brush(metal), pen, torso);
        // chest light
        double pulse = .5 + Math.Sin(t * 3) * .3;
        dc.DrawEllipse(Palette.Alpha(trim, .6 + pulse * .4), null, new Point(0, -u * .47), u * .03, u * .03);
        // head
        var head = new RectangleGeometry(new Rect(-u * .075, -u * .78, u * .15, u * .15), u * .03, u * .03);
        dc.DrawGeometry(Palette.Brush(Palette.Shade(metal, .08)), pen, head);
        var eyeCol = cin.Emotion is Emotion.angry or Emotion.evil ? Palette.C("#ff4040") : trim;
        dc.DrawRectangle(Palette.Brush(eyeCol), null, new Rect(-u * .05, -u * .7, u * .1, u * .028));
        // antenna
        dc.DrawLine(Palette.Pen(Palette.Shade(metal, -.25), u * .008), new Point(0, -u * .78), new Point(u * .02, -u * .84));
        dc.DrawEllipse(Palette.Alpha(Palette.C("#ff6b6b"), .5 + pulse * .5), null, new Point(u * .02, -u * .845), u * .012, u * .012);
        dc.Pop(); dc.Pop();
    }

    private static void DrawShadow(DrawingContext dc, CharacterInScene cin, double t, Point pos, double height)
    {
        dc.PushTransform(new TranslateTransform(pos.X, pos.Y));
        double u = height;
        var body = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(-u * .16, 0) };
        fig.Segments.Add(new BezierSegment(new Point(-u * .2, -u * .4), new Point(-u * .1, -u * .7), new Point(0, -u * .72), true));
        fig.Segments.Add(new BezierSegment(new Point(u * .1, -u * .7), new Point(u * .2, -u * .4), new Point(u * .16, 0), true));
        fig.IsClosed = true;
        body.Figures.Add(fig);
        dc.DrawGeometry(Palette.Alpha(Palette.C("#0a0b10"), .9), null, body);
        dc.DrawEllipse(Palette.Alpha(Palette.C("#d8f0ff"), .75), null, new Point(-u * .03, -u * .6), u * .02, u * .025);
        dc.DrawEllipse(Palette.Alpha(Palette.C("#d8f0ff"), .75), null, new Point(u * .03, -u * .6), u * .02, u * .025);
        dc.Pop();
    }

    private static void DrawAbstract(DrawingContext dc, CharacterInScene cin, CharacterDef def, double t, Point pos, double height, StyleSpec st)
    {
        dc.PushTransform(new TranslateTransform(pos.X, pos.Y - height * .7 + Math.Sin(t * 1.1) * height * .04));
        double u = height * .6;
        var c = Palette.Styled(Palette.C(def.Skin), st);
        var c2 = Palette.Styled(Palette.C(def.TopColor), st);
        for (int i = 0; i < 3; i++)
        {
            double r = u * (.18 + i * .09);
            double a = t * (.6 + i * .25);
            dc.DrawGeometry(null, Palette.Pen(Palette.Alpha(i % 2 == 0 ? c : c2, .55 - i * .12), u * .022),
                new EllipseGeometry(new Point(Math.Cos(a) * u * .05, Math.Sin(a * 1.3) * u * .05), r, r * .72).Clone());
        }
        dc.DrawEllipse(Palette.Alpha(c, .7), null, new Point(0, 0), u * .18, u * .18);
        dc.Pop();
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

    private static Brush RadialGradient(Color c, double r)
    {
        var g = new RadialGradientBrush(Palette.Shade(c, .22), Palette.Shade(c, -.18)) { GradientOrigin = new Point(-.3, -.3), Center = new Point(0, 0), RadiusX = r * 1.2, RadiusY = r * 1.2 };
        return g;
    }

    private static Brush VerticalGradient(Color c, Point p, double w)
    {
        var g = new LinearGradientBrush(Palette.Shade(c, -.22), Palette.Shade(c, .12)) { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        return g;
    }
}

// small helper to avoid referencing a property that doesn't exist
public static class CharacterDefExt
{
    public static string ShooColorSafe(this CharacterDef d) => d.ShoColor ?? "#212529";
}
