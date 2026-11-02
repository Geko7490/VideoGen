namespace LocalAIVideo.Core;

public enum DeviceKind { desktop }

public class DeviceProfile
{
    public DeviceKind Kind { get; } = DeviceKind.desktop;
    /// Default render chunk length (seconds).
    public int DefaultChunkSeconds { get; } = 30;
    public int[] ChunkOptions { get; } = { 5, 10, 15, 30, 60 };
    /// TTS is always strictly sequential — one voice job at a time.
    public bool SequentialTts { get; } = true;
    /// Max encoder queue depth (bounds memory).
    public int EncoderQueueLimit { get; } = 6;

    /// This app is a PC-focused workstation tool.
    public static readonly DeviceProfile Desktop = new();
    public static DeviceProfile Current => Desktop;
}
