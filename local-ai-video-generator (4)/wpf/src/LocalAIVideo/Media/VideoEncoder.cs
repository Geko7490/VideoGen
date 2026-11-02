using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LocalAIVideo.Media;

/// <summary>
/// Frame-by-frame video encoder backed by FFmpeg (the native replacement for WebCodecs).
/// Frames are produced as DrawingVisuals → rendered to BGR24 → piped as rawvideo → H.264 + AAC MP4.
/// No screen capture; real frames are encoded.
/// </summary>
public static class VideoEncoder
{
    public static bool IsAvailable => FindFfmpeg() != null;

    /// Looks for ffmpeg in: the app directory, then PATH.
    public static string? FindFfmpeg()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(local)) return local;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try { var p = Path.Combine(dir, "ffmpeg.exe"); if (File.Exists(p)) return p; }
            catch { /* ignore */ }
        }
        return null;
    }

    public record Result(bool Ok, string OutputPath, string Error);

    public static Result Encode(
        int width, int height, int fps,
        string? audioWav, string outMp4,
        Func<double, DrawingVisual> frameProvider,
        double totalDuration,
        Action<double>? onProgress = null,
        Action<string>? log = null)
    {
        var ffmpeg = FindFfmpeg();
        if (ffmpeg == null)
            return new Result(false, outMp4, "FFmpeg bulunamadı. ffmpeg.exe uygulama klasörüne koyun veya PATH'e ekleyin.");

        int totalFrames = Math.Max(1, (int)Math.Round(totalDuration * fps));
        int stride = width * 3; // BGR24, no padding
        var buffer = new byte[stride];
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Bgr24);

        var args = $"-y -f rawvideo -pix_fmt bgr24 -s {width}x{height} -r {fps} -i - ";
        if (!string.IsNullOrEmpty(audioWav) && File.Exists(audioWav)) args += $"-i \"{audioWav}\" ";
        args += "-c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p ";
        if (!string.IsNullOrEmpty(audioWav) && File.Exists(audioWav)) args += "-c:a aac -b:a 192k -shortest ";
        args += $"\"{outMp4}\"";

        log?.Invoke($"ffmpeg {args}");
        var psi = new ProcessStartInfo(ffmpeg, args)
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)!;
        string stderr = "";
        var errTask = proc.StandardError.ReadToEndAsync();

        try
        {
            using var stdin = proc.StandardInput.BaseStream;
            for (int i = 0; i < totalFrames; i++)
            {
                double t = i / (double)fps;
                var dv = frameProvider(t);
                rtb.Render(dv);
                rtb.CopyPixels(buffer, stride);
                stdin.Write(buffer, 0, buffer.Length);
                if ((i & 3) == 0) onProgress?.Invoke((i + 1) / (double)totalFrames);
            }
            stdin.Flush();
            stdin.Close();
        }
        catch (IOException) { /* ffmpeg may close the pipe early on error */ }

        proc.WaitForExit(1000 * 60 * 30);
        stderr = errTask.Wait(5000) ? errTask.Result : "";
        if (proc.ExitCode != 0)
            return new Result(false, outMp4, TrimError(stderr));

        onProgress?.Invoke(1.0);
        return new Result(true, outMp4, "");
    }

    private static string TrimError(string s)
    {
        var lines = s.Split('\n').Where(l => l.Trim().Length > 0).ToList();
        return lines.Count <= 6 ? s : string.Join("\n", lines.TakeLast(6));
    }
}
