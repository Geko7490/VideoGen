namespace LocalAIVideo.Models;

public enum AspectRatio { Portrait916, Landscape169, Square11 }

public enum Language { en, tr }

public enum QualityPreset { low, medium, high, auto }

public enum CaptionStyle { clean, bold, shorts, tiktok, minimal, cinematic }

public enum TtsStatus { pending, generating, done, failed, skipped }

/// <summary>A single narrated beat in the storyboard.</summary>
public class Scene
{
    public int SceneId { get; set; }
    public string Voiceover { get; set; } = "";
    public double DurationEstimate { get; set; } = 5.0;
    public double? ActualDuration { get; set; }     // measured from the real TTS audio
    public SceneSpec Spec { get; set; } = new();
    public string CaptionText { get; set; } = "";
    public int Seed { get; set; }
    public TtsStatus TtsStatus { get; set; } = TtsStatus.pending;
    public string? TtsError { get; set; }
    /// Word timings [start, end] in seconds relative to the scene start (for captions).
    public List<(string Word, double Start, double End)> WordTimings { get; set; } = new();
}

public class Script
{
    public string Title { get; set; } = "";
    public string Hook { get; set; } = "";
    public string MusicMood { get; set; } = "";
    public string StyleNotes { get; set; } = "";
    public List<Scene> Scenes { get; set; } = new();
}

public class CharacterDef
{
    public string Id { get; set; } = "character_01";
    public string Name { get; set; } = "";
    public CharacterKind Kind { get; set; } = CharacterKind.human;
    public string Skin { get; set; } = "#E8B98A";
    public string HairColor { get; set; } = "#2B2118";
    public string HairStyle { get; set; } = "short";
    public string TopColor { get; set; } = "#3B5BDB";
    public string BottomColor { get; set; } = "#343A40";
    public string ShoeColor { get; set; } = "#212529";
    public string Build { get; set; } = "average";
    public string Accessory { get; set; } = "none";
    public string AccessoryColor { get; set; } = "#DEE2E6";
}

/// <summary>One persistent visual identity for the whole video (style + cast).</summary>
public class StyleBible
{
    public VisualStyle Style { get; set; } = VisualStyle.cinematic_2d;
    public string Primary { get; set; } = "#4C6EF5";
    public string Secondary { get; set; } = "#15AABF";
    public string Accent { get; set; } = "#F59F00";
    public string Shadow { get; set; } = "#0B0D12";
    public string Sky { get; set; } = "#1C2333";
    public string MoodNotes { get; set; } = "";
    public List<CharacterDef> Characters { get; set; } = new();
    public bool Locked { get; set; }
}

public class MusicSettings
{
    public bool Enabled { get; set; }
    public string? FileName { get; set; }
    public double Volume { get; set; } = 0.25;
    public double FadeIn { get; set; } = 1.5;
    public double FadeOut { get; set; } = 2.5;
    public bool Loop { get; set; } = true;
    public bool Ducking { get; set; } = true;
    public double DuckLevel { get; set; } = 0.35;
}

/// <summary>Persisted project. Stored as JSON under %APPDATA%\LocalAIVideo\projects\.</summary>
public class Project
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled";
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdatedAt { get; set; }

    public string Topic { get; set; } = "";
    public double DurationSec { get; set; } = 60;
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Portrait916;
    public Language Language { get; set; } = Language.en;
    public QualityPreset Quality { get; set; } = QualityPreset.auto;
    public bool CaptionsEnabled { get; set; } = true;
    public CaptionStyle CaptionStyle { get; set; } = CaptionStyle.bold;

    public string LlmModelId { get; set; } = "qwen-0.5b";
    public string TtsVoice { get; set; } = "";
    public double TtsSpeed { get; set; } = 1.0;

    public int MasterSeed { get; set; } = new Random().Next(1_000_000);
    public int ChunkSeconds { get; set; } = 30;
    public int Fps { get; set; } = 30;

    public CharacterMode CharacterMode { get; set; } = CharacterMode.auto;
    public string OllamaUrl { get; set; } = "http://127.0.0.1:11434";

    public MusicSettings Music { get; set; } = new();
    public StyleBible? StyleBible { get; set; }
    public Script? Script { get; set; }
    public string? OutputPath { get; set; }
    public double? OutputDuration { get; set; }
}
