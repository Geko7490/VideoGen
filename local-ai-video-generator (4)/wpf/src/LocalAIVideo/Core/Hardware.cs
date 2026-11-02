using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LocalAIVideo.Core;

public class HardwareInfo
{
    public int CpuCores { get; set; } = Environment.ProcessorCount;
    public long TotalMemoryBytes { get; set; } = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    public double TotalMemoryGB => TotalMemoryBytes / 1073741824.0;
    /// Estimated usable GPU/VRAM tier. We cannot read exact VRAM without a GPU API,
    /// so we derive a conservative estimate from total RAM (honest, labelled "estimated").
    public double EstimatedVramGB { get; set; }
    public bool WebCodecsLikeEncoder { get; set; } = true; // FFmpeg provides H.264/AAC
    public string GpuDescription { get; set; } = "";
    public bool H264Available { get; set; } = true;
    public bool AacAvailable { get; set; } = true;

    public string Tier => EstimatedVramGB >= 10 ? "high" : EstimatedVramGB >= 6 ? "medium" : "low";
}

public static class Hardware
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    public static HardwareInfo Detect()
    {
        var info = new HardwareInfo
        {
            CpuCores = Environment.ProcessorCount,
            TotalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
        };
        try
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref ms))
            {
                info.TotalMemoryBytes = (long)ms.ullTotalPhys;
                // Rough VRAM estimate: on modern integrated GPUs it's a share of system RAM;
                // on discrete GPUs it is separate. We take a conservative 0.5x of RAM as an
                // upper bound and clamp to a sane range. Clearly "estimated" in the UI.
                info.EstimatedVramGB = Math.Clamp(ms.ullTotalPhys / 1073741824.0 * 0.5, 2, 24);
            }
        }
        catch { info.EstimatedVramGB = 8; }

        info.GpuDescription = QueryGpuDescription();
        return info;
    }

    private static string QueryGpuDescription()
    {
        try
        {
            var psi = new ProcessStartInfo("wmic", "path win32_VideoController get Name /value")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            var output = p!.StandardOutput.ReadToEnd();
            p.WaitForExit(1500);
            var line = output.Split('\n').FirstOrDefault(l => l.StartsWith("Name=", StringComparison.OrdinalIgnoreCase));
            return line?.Replace("Name=", "").Trim() ?? "Unknown";
        }
        catch { return "Unknown"; }
    }
}
