using System.IO;

namespace LocalAIVideo.Media;

/// <summary>Minimal 16-bit PCM WAV reader/writer (no external audio dependencies).</summary>
public static class WavFile
{
    public class Wav
    {
        public int SampleRate = 48000;
        public int Channels = 1;
        public short[] Samples = Array.Empty<short>(); // interleaved
        public double Duration => SampleRate > 0 ? Samples.Length / (double)(SampleRate * Channels) : 0;
    }

    public static Wav Read(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);
        r.ReadChars(4); // RIFF
        r.ReadUInt32(); // size
        r.ReadChars(4); // WAVE
        int sampleRate = 0, channels = 0;
        bool fmt = false, data = false;
        long dataPos = -1, dataLen = 0;
        while (ms.Position + 8 <= data.Length)
        {
            string id = r.ReadChars(4);
            int sz = (int)r.ReadUInt32();
            if (id == "fmt ")
            {
                r.ReadUInt16(); // audio format (1 = PCM)
                channels = r.ReadUInt16();
                sampleRate = (int)r.ReadUInt32();
                r.ReadUInt32(); // byte rate
                r.ReadUInt16(); // block align
                int bits = r.ReadUInt16();
                fmt = true;
                if (sz > 16) ms.Position += sz - 16;
                if (bits != 16) throw new InvalidDataException("Only 16-bit PCM WAV is supported by the mixer.");
            }
            else if (id == "data") { data = true; dataPos = ms.Position; dataLen = sz; }
            else ms.Position += sz;
        }
        if (!fmt) throw new InvalidDataException("Not a PCM WAV file.");
        var w = new Wav { SampleRate = sampleRate, Channels = channels };
        if (data)
        {
            ms.Position = dataPos;
            int n = (int)(dataLen / 2);
            var bytes = new byte[n * 2];
            r.Read(bytes, 0, (int)Math.Min(n * 2, dataLen - (ms.Position - dataPos)));
            w.Samples = new short[n];
            for (int i = 0; i < n; i++) w.Samples[i] = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
        }
        return w;
    }

    public static void Write(string path, Wav w)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        long byteRate = (long)w.SampleRate * w.Channels * 2;
        long dataSize = (long)w.Samples.Length * 2;
        using var ms = new MemoryStream();
        using var wtr = new BinaryWriter(ms);
        wtr.Write("RIFF"u8); wtr.Write(36 + dataSize); wtr.Write("WAVE"u8);
        wtr.Write("fmt "u8); wtr.Write((uint)16); wtr.Write((ushort)1);
        wtr.Write((ushort)w.Channels); wtr.Write((uint)w.SampleRate); wtr.Write(byteRate);
        wtr.Write((ushort)(w.Channels * 2)); wtr.Write((ushort)16);
        wtr.Write("data"u8); wtr.Write((uint)dataSize);
        for (int i = 0; i < w.Samples.Length; i++) wtr.Write(w.Samples[i]);
        ms.WriteTo(File.Create(path));
    }

    /// Reads only the header to get duration without loading the whole file.
    public static double HeaderDuration(string path, out int sampleRate)
    {
        sampleRate = 0;
        byte[] head = new byte[64];
        using (var f = File.OpenRead(path))
        using (var r = new BinaryReader(f))
        {
            if (f.Length < 64) return 0;
            var block = r.ReadBytes(64);
            // scan for 'fmt ' and 'data'
            int pos = 12;
            while (pos + 8 <= block.Length)
            {
                string id = new string(block, pos, 4);
                int sz = BitConverter.ToInt32(block, pos + 4);
                if (id == "fmt ") { sampleRate = BitConverter.ToInt32(block, pos + 12); pos += sz; }
                else if (id == "data") { return sz / (double)(sampleRate * 2); }
                else pos += sz;
            }
        }
        return 0;
    }

    /// Resample + downmix a WAV to mono at a target rate (linear interpolation).
    public static Wav ToMono(Wav src, int targetRate)
    {
        short[] mono;
        if (src.Channels == 1) mono = src.Samples;
        else
        {
            mono = new short[src.Samples.Length / src.Channels];
            for (int i = 0; i < mono.Length; i++)
            {
                int sum = 0;
                for (int c = 0; c < src.Channels; c++) sum += src.Samples[i * src.Channels + c];
                mono[i] = (short)(sum / src.Channels);
            }
        }
        if (src.SampleRate == targetRate) return new Wav { SampleRate = targetRate, Channels = 1, Samples = mono };
        double ratio = (double)src.SampleRate / targetRate;
        int outLen = (int)(mono.Length / ratio);
        var outS = new short[outLen];
        for (int i = 0; i < outLen; i++)
        {
            double pos = i * ratio;
            int i0 = (int)pos;
            int i1 = Math.Min(mono.Length - 1, i0 + 1);
            double frac = pos - i0;
            outS[i] = (short)(mono[i0] * (1 - frac) + mono[i1] * frac);
        }
        return new Wav { SampleRate = targetRate, Channels = 1, Samples = outS };
    }
}
