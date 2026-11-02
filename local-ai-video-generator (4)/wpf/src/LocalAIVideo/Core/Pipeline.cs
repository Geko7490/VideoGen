using System.Diagnostics;
using System.Windows.Media;
using LocalAIVideo.Engine;
using LocalAIVideo.Media;
using LocalAIVideo.Models;

namespace LocalAIVideo.Core;

/// <summary>
/// Orchestrates the whole production: storyboard → sequential TTS → real audio timing →
/// offline audio mix → frame-by-frame FFmpeg encode. One model/step at a time; nothing in
/// parallel. Every stage is idempotent and resumable.
/// </summary>
public class Pipeline : IDisposable
{
    public ScriptEngine Script { get; } = new();
    public TtsService Tts { get; } = new();

    public event Action<string>? Status;
    public event Action<double, string>? Progress; // 0..1, stage name
    public event Action<string>? Error;

    private void Raise(string s) => Status?.Invoke(s);
    private void RaiseProg(double p, string stage) => Progress?.Invoke(p, stage);

    /// Builds a persistent visual identity (style + cast) from the topic. Deterministic, no LLM needed.
    public static StyleBible BuildBible(Project p, string topic)
    {
        var seed = new Random(p.MasterSeed);
        var bible = new StyleBible { MoodNotes = topic };
        // Derive a palette from the topic so different videos look different but consistent.
        var h1 = (topic.GetHashCode() & 0x7fffffff) % 360;
        bible.Primary = Hsl(h1, 70, 60);
        bible.Secondary = Hsl((h1 + 40) % 360, 65, 55);
        bible.Accent = Hsl((h1 + 180) % 360, 80, 60);
        bible.Sky = Hsl((h1 + 200) % 360, 40, 25);

        if (p.CharacterMode is CharacterMode.required or CharacterMode.auto)
        {
            int n = p.CharacterMode == CharacterMode.required ? 1 : 1;
            for (int i = 0; i < n; i++)
            {
                bible.Characters.Add(new CharacterDef
                {
                    Id = "character_01",
                    Name = "Protagonist",
                    Kind = CharacterKind.human,
                    Skin = new[] { "#E8B98A", "#C9865A", "#F3D2B3", "#8D5524" }[seed.Next(4)],
                    HairColor = new[] { "#2B2118", "#1A1410", "#9C6644", "#140F0B" }[seed.Next(4)],
                    HairStyle = new[] { "short", "long", "messy", "bun" }[seed.Next(4)],
                    TopColor = Hsl(seed.Next(360), 60, 50),
                    BottomColor = Hsl(seed.Next(360), 45, 35),
                    ShoeColor = "#212529",
                    Build = new[] { "slim", "average", "heavy", "tall" }[seed.Next(4)],
                    Accessory = "none",
                    AccessoryColor = "#DEE2E6",
                });
            }
        }
        bible.Locked = true;
        return bible;
    }

    public async Task<(string mp4, double duration)> GenerateAsync(Project p, CancellationToken ct)
    {
        var dir = Storage.ProjectDir(p.Id);
        Storage.EnsureProjectDirs(p.Id);
        var sw = Stopwatch.StartNew();

        // ---- 1) visual identity + storyboard ----
        Raise("Building visual identity…");
        p.StyleBible = p.StyleBible ?? BuildBible(p, p.Topic);
        RaiseProg(0.02, "Script");

        Raise($"Planning storyboard with {p.LlmModelId}…");
        Script.SetBase(p.OllamaUrl);
        var script = await Script.GenerateAsync(p, p.StyleBible, p.LlmModelId, _ => { });
        p.Script = script;
        RaiseProg(0.18, "Script");
        Raise($"Storyboard ready: {script.Scenes.Count} scenes.");

        // ---- 2) sequential TTS, measured durations ----
        var ttsDir = Storage.TtsDir(p.Id);
        var narration = new List<(double Start, string Path)>();
        double cursor = 0;
        var starts = new List<double>();
        const double Padding = 0.35;
        for (int i = 0; i < script.Scenes.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var sc = script.Scenes[i];
            Raise($"TTS scene {i + 1}/{script.Scenes.Count}…");
            var wavPath = Path.Combine(ttsDir, $"scene_{i + 1:000}.wav");
            try
            {
                var (path, dur, timings) = Tts.SpeakToFile(sc.Voiceover, wavPath, p.TtsSpeed);
                sc.ActualDuration = Math.Max(1.5, dur + Padding);
                sc.WordTimings = timings;
                starts.Add(cursor);
                narration.Add((cursor, path));
                cursor += sc.ActualDuration ?? sc.DurationEstimate;
            }
            catch (Exception e)
            {
                sc.TtsStatus = TtsStatus.failed;
                sc.TtsError = e.Message;
                sc.ActualDuration = Math.Max(1.5, sc.DurationEstimate); // silent fallback, video still renders
                starts.Add(cursor);
                cursor += sc.ActualDuration ?? sc.DurationEstimate;
                Error?.Invoke($"Scene {i + 1} TTS failed: {e.Message}");
            }
            sc.TtsStatus = sc.TtsStatus == TtsStatus.failed ? TtsStatus.failed : TtsStatus.done;
            RaiseProg(0.18 + 0.42 * (i + 1) / (double)script.Scenes.Count, "TTS");
        }

        double total = cursor;
        p.OutputDuration = total;
        RaiseProg(0.60, "Mix");

        // ---- 3) offline audio mix ----
        string mixWav = Path.Combine(dir, "mixed.wav");
        string? musicPath = null;
        if (p.Music.Enabled && p.Music.FileName != null)
        {
            var candidate = Path.Combine(dir, "assets", p.Music.FileName);
            musicPath = File.Exists(candidate) ? candidate : null;
        }
        AudioMixer.Mix(narration, p.Music, musicPath ?? "", total, mixWav);
        RaiseProg(0.64, "Mix");

        // ---- 4) frame-by-frame encode ----
        RaiseProg(0.66, "Render");
        var (w, h) = p.AspectRatio switch
        {
            AspectRatio.Portrait916 => (720, 1280),
            AspectRatio.Landscape169 => (1280, 720),
            _ => (900, 900),
        };
        var quality = ResolveQuality(p.Quality);
        string outMp4 = Path.Combine(Storage.ExportDir(p.Id), "final.mp4");
        var frameProvider = new FrameBuilder(script, p.StyleBible, p.MasterSeed, quality, w, h, starts, total).FrameAt;
        var result = VideoEncoder.Encode(w, h, p.Fps, File.Exists(mixWav) ? mixWav : null, outMp4, frameProvider, total,
            frac => RaiseProg(0.66 + 0.34 * frac, "Render"), Raise);

        if (!result.Ok) throw new InvalidOperationException(result.Error);
        p.OutputPath = result.OutputPath;
        Storage.SaveProject(p);
        RaiseProg(1.0, "Done");
        Raise($"Done in {sw.Elapsed.TotalSeconds:0.0}s → {result.OutputPath}");
        return (result.OutputPath, total);
    }

    public static EngineQuality ResolveQuality(QualityPreset q) => q switch
    {
        QualityPreset.low => EngineQuality.low,
        QualityPreset.medium => EngineQuality.medium,
        QualityPreset.high => EngineQuality.high,
        _ => EngineQuality.high, // auto → high on desktop
    };

    public void Dispose()
    {
        Script.Dispose();
        Tts.Dispose();
    }

    private static string Hsl(int h, int s, int l)
    {
        double c = (1 - Math.Abs(2 * l / 100.0 - 1)) * s / 100.0;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = l / 100.0 - c / 2;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x),
            < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x),
        };
        return "#" + ((int)Math.Round((r + m) * 255)).ToString("X2") + ((int)Math.Round((g + m) * 255)).ToString("X2") + ((int)Math.Round((b + m) * 255)).ToString("X2");
    }
}

/// <summary>Maps an absolute time to a scene + progress and renders the frame (with crossfade).</summary>
public class FrameBuilder
{
    private readonly Script _script;
    private readonly StyleBible _bible;
    private readonly int _seed;
    private readonly EngineQuality _q;
    private readonly int _w, _h;
    private readonly double[] _starts;
    private readonly double _total;

    public FrameBuilder(Script script, StyleBible bible, int seed, EngineQuality q, int w, int h, List<double> starts, double total)
    {
        _script = script; _bible = bible; _seed = seed; _q = q; _w = w; _h = h; _starts = starts.ToArray();
        _total = total > 0 ? total : 1;
    }

    public DrawingVisual FrameAt(double t)
    {
        var dv = new DrawingVisual();
        using var dc = dv.RenderOpen();
        dc.DrawRectangle(Brushes.Black, null, new System.Windows.Rect(0, 0, _w, _h));

        // find scene index
        int idx = 0;
        for (int i = 0; i < _starts.Length; i++) if (t >= _starts[i]) idx = i;
        var sc = _script.Scenes[idx];
        double sceneStart = _starts[idx];
        double sceneDur = sc.ActualDuration!.Value;
        double local = t - sceneStart;
        double progress = Math.Clamp(local / Math.Max(0.1, sceneDur), 0, 1);

        const double XF = 0.6; // crossfade duration
        // previous scene during the overlap
        if (idx > 0 && local < XF)
        {
            var prev = _script.Scenes[idx - 1];
            double pPrev = Math.Clamp(local / Math.Max(0.1, prev.ActualDuration!.Value), 0, 1);
            SceneRenderer.Render(dc, prev.Spec, _bible, local, 1.0, _w, _h, _seed + (idx - 1) * 977, _q);
            dc.PushOpacity(1 - local / XF);
            SceneRenderer.Render(dc, sc.Spec, _bible, local, progress, _w, _h, _seed + idx * 977, _q);
            dc.Pop();
        }
        else
        {
            SceneRenderer.Render(dc, sc.Spec, _bible, local, progress, _w, _h, _seed + idx * 977, _q);
        }

        // global fade in/out (alpha overlay, never a full black-out)
        double fadeIn = Math.Clamp(t / 0.8, 0, 1);
        double fadeOut = Math.Clamp((_total - t) / 1.0, 0, 1);
        double fade = Math.Min(fadeIn, fadeOut);
        if (fade < 1)
        {
            var black = new SolidColorBrush(Color.FromArgb((byte)((1 - fade) * 255), 0, 0, 0));
            dc.DrawRectangle(black, null, new System.Windows.Rect(0, 0, _w, _h));
        }
        // captions
        if (sc.WordTimings.Count > 0)
            CaptionRenderer.Draw(dc, sc.WordTimings, local, _w, _h);
        return dv;
    }
}

/// <summary>Simple word/phrase captions synced to the measured word timings.</summary>
public static class CaptionRenderer
{
    public static void Draw(System.Windows.Media.DrawingContext dc, List<(string Word, double Start, double End)> words, double t, double W, double H)
    {
        // build a phrase (up to 4 words) containing the current time
        int i = 0;
        while (i < words.Count && words[i].End < t) i++;
        if (i >= words.Count) return;
        var phrase = string.Join(" ", words.Skip(i).Take(4).Select(w => w.Word));

        var typeface = new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Segoe UI"),
            System.Windows.FontStyles.Normal, System.Windows.FontWeights.SemiBold, System.Windows.FontStretches.Normal);
        var ft = new System.Windows.Media.FormattedText(phrase, System.Globalization.CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight, typeface, H * .045, System.Windows.Media.Brushes.White, 1.0);
        var x = (W - ft.Width) / 2;
        var y = H * .80;
        // background for legibility
        var bg = new System.Windows.Rect(x - 8, y - 4, ft.Width + 16, ft.Height + 8);
        dc.DrawRectangle(System.Windows.Media.Brushes.Black, null, bg);
        dc.DrawText(ft, new System.Windows.Point(x, y));
    }
}
