using System.Windows;
using System.Windows.Media;
using LocalAIVideo.Models;

namespace LocalAIVideo.Engine;

/// <summary>
/// Camera: computes a base transform from shot + movement, then derives a per-depth matrix
/// so the environment layers move at different rates (true parallax).
/// </summary>
public struct Camera
{
    public double Zoom;
    public double X;   // normalised pan (-1..1)
    public double Y;   // normalised tilt
    public double Rot; // radians

    public static double Ease(double t) { t = Math.Clamp(t, 0, 1); return t < .5 ? 2 * t * t : -1 + (4 - 2 * t) * t; }

    private static double N(double t, double seed) =>
        Math.Sin(t * 1.7 + seed) * .5 + Math.Sin(t * 3.1 + seed * 2.3) * .3 + Math.Sin(t * 7.3 + seed * .7) * .2;

    public static (double zoom, double y) Base(ShotType shot) => shot switch
    {
        ShotType.establishing => (0.92, 0),
        ShotType.wide => (1.0, 0),
        ShotType.medium => (1.45, -0.1),
        ShotType.close_up => (2.5, -0.22),
        ShotType.extreme_close_up => (4.0, -0.28),
        ShotType.pov => (1.9, -0.05),
        ShotType.low_angle => (1.5, 0.16),
        ShotType.high_angle => (1.35, -0.2),
        ShotType.top_down => (1.2, 0.14),
        ShotType.over_shoulder => (1.75, -0.14),
        ShotType.side_view => (1.3, -0.06),
        _ => (1.45, -0.1),
    };

    public static Camera Compute(CameraSpec cam, double progress, double t, int seed)
    {
        var (bz, by) = Base(cam.Shot);
        double e = Ease(progress);
        var c = new Camera { Zoom = bz, X = 0, Y = by, Rot = 0 };

        c.Movement = cam.Movement;
        switch (cam.Movement)
        {
            case CameraMove.slow_zoom_in: c.Zoom = bz * (1 + .16 * e); break;
            case CameraMove.slow_zoom_out: c.Zoom = bz * (1.16 - .16 * e); break;
            case CameraMove.pan_left: c.X = (.5 - e) * .42; break;
            case CameraMove.pan_right: c.X = (e - .5) * .42; break;
            case CameraMove.tilt_up: c.Y = by + (.5 - e) * .3; break;
            case CameraMove.tilt_down: c.Y = by + (e - .5) * .3; break;
            case CameraMove.tracking: c.X = (e - .5) * .55; c.Zoom = bz * 1.05; break;
            case CameraMove.orbit:
                c.X = Math.Sin(e * Math.PI - Math.PI / 2) * .3;
                c.Rot = Math.Sin(e * Math.PI - Math.PI / 2) * -.025;
                c.Zoom = bz * (1.04 + Math.Sin(e * Math.PI) * .08);
                break;
            case CameraMove.shake:
                c.X = N(t * 2.2, seed % 7) * .035;
                c.Y = by + N(t * 2.6, seed % 11) * .03;
                c.Rot = N(t * 1.9, seed % 5) * .012;
                c.Zoom = bz * 1.06;
                break;
            case CameraMove.handheld:
                c.X = N(t * 1.1, seed % 3) * .02;
                c.Y = by + N(t * .9, seed % 13) * .018;
                c.Rot = N(t * .7, seed % 9) * .006;
                c.Zoom = bz * (1.02 + .02 * e);
                break;
            default: c.Zoom = bz * (1 + .012 * e); break;
        }

        if (cam.Shot == ShotType.low_angle) c.Rot += .015;
        if (cam.Shot == ShotType.high_angle) c.Rot -= .012;
        return c;
    }

    /// Matrix for a given depth (0.22 background → 1.4 foreground). Applied around the canvas centre.
    public Matrix ForDepth(double depth, double W, double H)
    {
        double z = 1 + (Zoom - 1) * (0.5 + depth * 0.7);
        var m = new Matrix();
        m.RotateAt(Rot, W / 2, H / 2);
        m.ScaleAt(z, z, W / 2, H / 2);
        m.Translate(-X * W * depth, -Y * H * depth);
        return m;
    }

    public CameraMove Movement { get; set; }
}
