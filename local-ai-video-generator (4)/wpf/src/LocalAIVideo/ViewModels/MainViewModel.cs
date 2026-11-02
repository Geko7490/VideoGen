using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LocalAIVideo.Core;
using LocalAIVideo.Engine;
using LocalAIVideo.Helpers;
using LocalAIVideo.Media;
using LocalAIVideo.Models;

namespace LocalAIVideo.ViewModels;

public class SceneRow : ObservableObject
{
    public int SceneId { get; set; }
    public string Voiceover { get; set; } = "";
    public double Duration { get; set; }
    public string Env { get; set; } = "";
    public string Emotion { get; set; } = "";
    public string Camera { get; set; } = "";
    public string TtsStatus { get; set; } = "pending";
    private bool _selected;
    public bool IsSelected { get => _selected; set { Set(ref _selected, value); } }
}

public class ProjectRow : ObservableObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public long UpdatedAt { get; set; }
    public int Scenes { get; set; }
}

public partial class MainViewModel : ObservableObject
{
    private readonly Pipeline _pipeline = new();
    private readonly DispatcherTimer _previewTimer;
    private CancellationTokenSource? _cts;
    private double _previewT;
    private SceneSpec? _previewSpec;
    private StyleBible? _previewBible;
    private int _previewSeed;
    private int _previewW = 360, _previewH = 640;
    private bool _playing;

    public MainViewModel()
    {
        _previewSpec = DemoScene();
        _previewBible = Pipeline.BuildBible(new Project { MasterSeed = 12345, CharacterMode = CharacterMode.auto, Topic = "demo" }, "demo");
        _previewSeed = 12345;

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _previewTimer.Tick += (_, _) =>
        {
            if (_playing) { _previewT += 0.033; if (_previewT > 8) _previewT = 0; }
            RenderPreview();
        };
        _previewTimer.Start();

        Device = Hardware.Detect();
        LoadProjects();
        WireCommands();
        _ = LoadModelsAsync();
        _ = LoadVoicesAsync();
    }

    // ---------------- bindable project fields ----------------
    private string _topic = "What would happen if humans stopped sleeping?";
    public string Topic { get => _topic; set => Set(ref _topic, value); }

    private double _duration = 60;
    public double DurationSec { get => _duration; set => Set(ref _duration, value); }
    public double[] DurationPresets { get; } = { 30, 60, 300, 600 };

    private AspectRatio _aspect = AspectRatio.Portrait916;
    public AspectRatio Aspect { get => _aspect; set { Set(ref _aspect, value); UpdatePreviewSize(); } }
    public AspectRatio[] Aspects { get; } = { AspectRatio.Portrait916, AspectRatio.Landscape169, AspectRatio.Square11 };

    private Language _language = Language.en;
    public Language Language { get => _language; set => Set(ref _language, value); }
    public Language[] Languages { get; } = { Language.en, Language.tr };

    private QualityPreset _quality = QualityPreset.auto;
    public QualityPreset Quality { get => _quality; set => Set(ref _quality, value); }
    public QualityPreset[] Qualities { get; } = { QualityPreset.auto, QualityPreset.low, QualityPreset.medium, QualityPreset.high };

    private CharacterMode _charMode = CharacterMode.auto;
    public CharacterMode CharMode { get => _charMode; set => Set(ref _charMode, value); }
    public CharacterMode[] CharModes { get; } = { CharacterMode.auto, CharacterMode.optional, CharacterMode.required, CharacterMode.none };

    private string _llmModel = "qwen2.5:0.5b";
    public string LlmModel { get => _llmModel; set => Set(ref _llmModel, value); }

    private string _ttsVoice = "";
    public string TtsVoice { get => _ttsVoice; set => Set(ref _ttsVoice, value); }

    private double _ttsSpeed = 1.0;
    public double TtsSpeed { get => _ttsSpeed; set => Set(ref _ttsSpeed, value); }

    private int _masterSeed = new Random().Next(1_000_000);
    public int MasterSeed { get => _masterSeed; set => Set(ref _masterSeed, value); }

    private bool _captions = true;
    public bool Captions { get => _captions; set => Set(ref _captions, value); }

    private int _chunkSeconds = 30;
    public int ChunkSeconds { get => _chunkSeconds; set => Set(ref _chunkSeconds, value); }
    public int[] ChunkOptions { get; } = { 5, 10, 15, 30, 60 };

    // ---------------- model / voice / project lists ----------------
    public ObservableCollection<ScriptEngine.OllamaModel> Models { get; } = new();
    public ObservableCollection<TtsVoice> Voices { get; } = new();
    public ObservableCollection<SceneRow> Scenes { get; } = new();
    public ObservableCollection<ProjectRow> Projects { get; } = new();

    public HardwareInfo Device { get; private set; }

    // ---------------- status / preview ----------------
    private string _status = "Ready";
    public string Status { get => _status; set => Set(ref _status, value); }
    private double _progress;
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    private string _progressStage = "";
    public string ProgressStage { get => _progressStage; set => Set(ref _progressStage, value); }
    private bool _busy;
    public bool IsBusy { get => _busy; set => Set(ref _busy, value); }
    private BitmapSource? _preview;
    public BitmapSource? Preview { get => _preview; set => Set(ref _preview, value); }
    private string _outputPath = "";
    public string OutputPath { get => _outputPath; set => Set(ref _outputPath, value); }

    // ---------------- commands ----------------
    public RelayCommand GenerateCommand { get; set; }
    public RelayCommand CancelCommand { get; set; }
    public RelayCommand LoadModelsCommand { get; set; }
    public RelayCommand PlayPauseCommand { get; set; }
    public RelayCommand NewSeedCommand { get; set; }
    public RelayCommand OpenOutputCommand { get; set; }
    public RelayCommand RefreshHardwareCommand { get; set; }
    public RelayCommand SelectSceneCommand { get; set; }
    public RelayCommand OpenProjectCommand { get; set; }
    public RelayCommand DeleteProjectCommand { get; set; }

    private void WireCommands()
    {
        GenerateCommand = new RelayCommand(() => _ = GenerateAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(Topic));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        LoadModelsCommand = new RelayCommand(() => _ = LoadModelsAsync());
        PlayPauseCommand = new RelayCommand(() => { _playing = !_playing; if (!_playing) _previewT = 0; });
        NewSeedCommand = new RelayCommand(() => MasterSeed = new Random().Next(1_000_000));
        OpenOutputCommand = new RelayCommand(() =>
        {
            if (File.Exists(OutputPath)) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{OutputPath}\"");
        }, () => File.Exists(OutputPath));
        RefreshHardwareCommand = new RelayCommand(() => Device = Hardware.Detect());
        SelectSceneCommand = new RelayCommand(param =>
        {
            if (param is not SceneRow r) return;
            foreach (var s in Scenes) s.IsSelected = false;
            r.IsSelected = true;
            var scene = CurrentScript?.Scenes.FirstOrDefault(x => x.SceneId == r.SceneId);
            if (scene != null) { _previewSpec = scene.Spec; _previewSeed = scene.Seed; _previewT = 0; }
        });
        OpenProjectCommand = new RelayCommand(param =>
        {
            if (param is not ProjectRow p) return;
            var proj = Storage.LoadProject(p.Id);
            if (proj == null) return;
            LoadProjectIntoUi(proj);
        });
        DeleteProjectCommand = new RelayCommand(param =>
        {
            if (param is not ProjectRow p) return;
            if (System.Windows.MessageBox.Show("Silinsin mi?", "Proje Sil",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
            { Storage.DeleteProject(p.Id); LoadProjects(); }
        });
    }

    public Script? CurrentScript { get; private set; }
    private Project? _currentProject;

    // ---------------- async loaders ----------------
    private async Task LoadModelsAsync()
    {
        _pipeline.Script.SetBase("http://127.0.0.1:11434");
        bool ok = await _pipeline.Script.IsReachableAsync();
        if (!ok) { Status = "Ollama bağlı değil. Model listesi boş (http://127.0.0.1:11434)."; return; }
        var models = await _pipeline.Script.ListModelsAsync();
        Models.Clear();
        foreach (var m in models) Models.Add(m);
        if (Models.Count > 0 && Models.All(m => m.Name != LlmModel)) LlmModel = Models[0].Name;
        Status = $"Ollama bağlı · {Models.Count} model bulundu.";
    }

    private Task LoadVoicesAsync()
    {
        Voices.Clear();
        foreach (var v in _pipeline.Tts.GetVoices()) Voices.Add(v);
        if (Voices.Count > 0 && string.IsNullOrEmpty(TtsVoice)) TtsVoice = Voices[0].Id;
        return Task.CompletedTask;
    }

    private void LoadProjects()
    {
        Projects.Clear();
        foreach (var p in Storage.ListProjects())
            Projects.Add(new ProjectRow { Id = p.Id, Name = p.Name, UpdatedAt = p.UpdatedAt, Scenes = p.Script?.Scenes.Count ?? 0 });
    }

    private void LoadProjectIntoUi(Project p)
    {
        _currentProject = p;
        Topic = p.Topic; DurationSec = p.DurationSec; Aspect = p.AspectRatio; Language = p.Language;
        Quality = p.Quality; CharMode = p.CharacterMode; LlmModel = p.LlmModelId; TtsVoice = p.TtsVoice;
        TtsSpeed = p.TtsSpeed; MasterSeed = p.MasterSeed; Captions = p.CaptionsEnabled; ChunkSeconds = p.ChunkSeconds;
        _previewBible = p.StyleBible; OutputPath = p.OutputPath ?? "";
        RefreshScenes();
        Status = $"Proje yüklendi: {p.Name}";
    }

    private void RefreshScenes()
    {
        Scenes.Clear();
        if (CurrentScript == null) return;
        foreach (var s in CurrentScript.Scenes)
            Scenes.Add(new SceneRow
            {
                SceneId = s.SceneId,
                Voiceover = s.Voiceover,
                Duration = s.ActualDuration ?? s.DurationEstimate,
                Env = s.Spec.Environment.Type.ToString(),
                Emotion = s.Spec.Characters.Count > 0 ? s.Spec.Characters[0].Emotion.ToString() : "—",
                Camera = s.Spec.Camera.Shot.ToString() + " · " + s.Spec.Camera.Movement.ToString(),
                TtsStatus = s.TtsStatus.ToString(),
            });
    }

    // ---------------- generate ----------------
    private async Task GenerateAsync()
    {
        _cts = new CancellationTokenSource();
        IsBusy = true;
        Progress = 0;
        try
        {
            if (_currentProject == null || string.IsNullOrWhiteSpace(_currentProject.Name) || _currentProject.Name == "Untitled")
            {
                _currentProject = new Project { Id = Guid.NewGuid().ToString("N"), Name = Topic.Length > 40 ? Topic[..40] + "…" : Topic };
            }
            // push bound fields into the project
            var p = _currentProject;
            p.Topic = Topic; p.DurationSec = DurationSec; p.AspectRatio = Aspect; p.Language = Language;
            p.Quality = Quality; p.CharacterMode = CharMode; p.LlmModelId = LlmModel; p.TtsVoice = TtsVoice;
            p.TtsSpeed = TtsSpeed; p.MasterSeed = MasterSeed; p.CaptionsEnabled = Captions; p.ChunkSeconds = ChunkSeconds;
            p.Fps = 30;

            _pipeline.Status += s => Ui(() => Status = s);
            _pipeline.Progress += (f, stage) => Ui(() => { Progress = f; ProgressStage = stage; });
            _pipeline.Error += m => Ui(() => Status = m);

            var (mp4, _) = await _pipeline.GenerateAsync(p, _cts.Token);
            CurrentScript = p.Script;
            OutputPath = mp4;
            _previewBible = p.StyleBible;
            RefreshScenes();
            LoadProjects();
            Status = $"Tamamlandı → {mp4}";
        }
        catch (OperationCanceledException) { Status = "İptal edildi."; }
        catch (Exception e) { Status = "Hata: " + e.Message; }
        finally
        {
            _pipeline.Status -= s => Ui(() => Status = s);
            _pipeline.Progress -= (f, stage) => Ui(() => { Progress = f; ProgressStage = stage; });
            _pipeline.Error -= m => Ui(() => Status = m);
            IsBusy = false;
        }
    }

    private void Ui(Action a)
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true) a();
        else System.Windows.Application.Current?.Dispatcher.BeginInvoke(a);
    }

    // ---------------- preview ----------------
    private void UpdatePreviewSize()
    {
        ( _previewW, _previewH ) = Aspect switch
        {
            AspectRatio.Portrait916 => (360, 640),
            AspectRatio.Landscape169 => (560, 315),
            _ => (480, 480),
        };
    }

    private void RenderPreview()
    {
        if (_previewSpec == null || _previewBible == null) return;
        try
        {
            double prog = Math.Clamp((_previewT % 8) / Math.Max(1, _previewSpec.Duration), 0, 1);
            var rtb = SceneRenderer.Render(_previewSpec, _previewBible, _previewT, prog, _previewW, _previewH, _previewSeed, EngineQuality.medium);
            Preview = rtb;
        }
        catch { /* keep preview alive */ }
    }

    private static SceneSpec DemoScene() => new()
    {
        Environment = new EnvironmentSpec { Type = EnvType.street, Time = TimeOfDay.night, Weather = Weather.rain },
        Lighting = new LightingSpec { Style = LightStyle.neon, Direction = LightDir.window, Intensity = .9 },
        Camera = new CameraSpec { Shot = ShotType.medium, Movement = CameraMove.slow_zoom_in, Target = "character_01" },
        Effects = new List<EffectKind> { EffectKind.rain, EffectKind.floating_dust },
        Duration = 8,
    };
}
