using LocalAIVideo.Models;

namespace LocalAIVideo.Media;

/// <summary>
/// Offline audio mixer (pure .NET, no external audio libs). Mixes the per-scene TTS narration
/// with optional background music (loop + fades + voice ducking) into one 48 kHz mono WAV.
/// </summary>
public static class AudioMixer
{
    public const int SampleRate = 48000;

    public static string Mix(List<(double Start, string WavPath)> narration, MusicSettings music, string musicPath, double totalDuration, string outPath)
    {
        int len = (int)(totalDuration * SampleRate);
        var master = new float[len];

        // ---- narration ----
        var voiceEnv = new float[len]; // 0..1 where voice is active (for ducking)
        foreach (var (start, path) in narration)
        {
            if (!File.Exists(path)) continue;
            var wav = WavFile.ToMono(WavFile.Read(path), SampleRate);
            int at = (int)(start * SampleRate);
            for (int i = 0; i < wav.Samples.Length && at + i < len; i++)
            {
                master[at + i] += wav.Samples[i] / 32768f;
                voiceEnv[at + i] = 1f;
            }
        }

        // ---- music ----
        if (music.Enabled && !string.IsNullOrEmpty(musicPath) && File.Exists(musicPath))
        {
            var musicWav = WavFile.ToMono(WavFile.Read(musicPath), SampleRate);
            if (musicWav.Samples.Length > 0)
            {
                for (int i = 0; i < len; i++)
                {
                    int mi = music.Loop ? i % musicWav.Samples.Length : i;
                    if (mi >= musicWav.Samples.Length) break;
                    double t = i / (double)SampleRate;
                    float v = musicWav.Samples[mi] / 32768f * (float)music.Volume;

                    // fades
                    if (t < music.FadeIn && music.FadeIn > 0) v *= (float)(t / music.FadeIn);
                    if (music.FadeOut > 0 && t > totalDuration - music.FadeOut) v *= (float)Math.Max(0, (totalDuration - t) / music.FadeOut);

                    // voice ducking
                    if (music.Ducking && voiceEnv[i] > 0f) v *= (float)music.DuckLevel;

                    master[i] += v;
                }
            }
        }

        // ---- normalise ----
        float peak = 0;
        for (int i = 0; i < len; i++) peak = Math.Max(peak, Math.Abs(master[i]));
        if (peak > 0.0001f)
        {
            float g = 0.95f / peak;
            for (int i = 0; i < len; i++) master[i] *= g;
        }

        var outWav = new WavFile.Wav { SampleRate = SampleRate, Channels = 1, Samples = new short[len] };
        for (int i = 0; i < len; i++) outWav.Samples[i] = (short)(Math.Clamp(master[i], -1f, 1f) * 32767);
        WavFile.Write(outPath, outWav);
        return outPath;
    }
}
