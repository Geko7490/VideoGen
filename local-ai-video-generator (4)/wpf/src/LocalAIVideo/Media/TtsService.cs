using System.Speech;
using System.Speech.Synthesis;
using System.Text;
using LocalAIVideo.Models;

namespace LocalAIVideo.Media;

public class TtsVoice
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
}

/// <summary>
/// Native, offline speech synthesis using the Windows built-in SAPI engine (System.Speech).
/// No network, no external service. This is the native replacement for the browser Kokoro path.
/// </summary>
public class TtsService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private TtsVoice? _selected;
    public string SelectedVoiceId { get; private set; } = "";

    public List<TtsVoice> GetVoices()
    {
        var list = new List<TtsVoice>();
        foreach (var v in _synth.GetInstalledVoices().Where(v => v.Enabled))
            list.Add(new TtsVoice { Id = v.VoiceInfo.Name, Name = v.VoiceInfo.Culture.DisplayName + " — " + v.VoiceInfo.Name, IsDefault = v.VoiceInfo.Name == _synth.SelectVoiceByHints().Name });
        return list;
    }

    public bool SelectVoice(string id)
    {
        if (string.IsNullOrEmpty(id)) { _synth.SelectDefaultVoice(); SelectedVoiceId = ""; return true; }
        try { _synth.SelectVoice(id); SelectedVoiceId = id; return true; }
        catch { return false; }
    }

    public void SetRate(double rate)
    {
        // Map our 0.6..1.5 range onto SAPI's -10..10 scale.
        int sapi = (int)Math.Clamp((rate - 1.0) * 10, -10, 10);
        _synth.Rate = sapi;
    }

    /// Synthesises text to a 16-bit mono WAV and returns the measured duration + word timings.
    public (string path, double duration, List<(string Word, double Start, double End)> timings) SpeakToFile(string text, string outPath, double rate)
    {
        SetRate(rate);
        _synth.SetOutputToWaveFile(outPath);
        _synth.Speak(text);
        _synth.SetOutputToNull();
        double dur = WavFile.HeaderDuration(outPath, out _);
        return (outPath, dur, EstimateTimings(text, dur));
    }

    /// Deterministic proportional word timings (same approach as the original engine).
    public static List<(string Word, double Start, double End)> EstimateTimings(string text, double duration)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 0).ToList();
        var result = new List<(string, double, double)>();
        if (words.Count == 0) return result;
        double[] weights = words.Select(w => w.Where(char.IsLetterOrDigit).Count() + 1.2 + (w.EndsWithAny(".!?;:") ? 1.5 : 0)).ToArray();
        double total = weights.Sum();
        if (total <= 0) total = 1;
        double usable = duration * .97;
        double t = 0;
        for (int i = 0; i < words.Count; i++)
        {
            double d = weights[i] / total * usable;
            result.Add((words[i], t, t + d));
            t += d;
        }
        return result;
    }

    public void Dispose() => _synth.Dispose();
}

static class CharExtensions
{
    public static bool EndsWithAny(this string s, string set) => set.Any(s.EndsWith);
}
