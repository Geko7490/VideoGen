using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalAIVideo.Helpers;
using LocalAIVideo.Models;

namespace LocalAIVideo.Core;

/// <summary>
/// Scene planner. Asks the local LLM (Ollama on localhost) for a structured storyboard and
/// validates/repairs it. LLM-agnostic: any model that follows the JSON schema works.
/// </summary>
public class ScriptEngine : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(30) };
    private string _baseUrl = "http://127.0.0.1:11434";

    public void SetBase(string url) => _baseUrl = (url ?? "http://127.0.0.1:11434").TrimEnd('/');

    public record OllamaModel(string Name, long Size);

    public async Task<List<OllamaModel>> ListModelsAsync()
    {
        using var r = await _http.GetAsync(_baseUrl + "/api/tags");
        if (!r.IsSuccessStatusCode) return new List<OllamaModel>();
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        var list = new List<OllamaModel>();
        if (doc.RootElement.TryGetProperty("models", out var models))
            foreach (var m in models.EnumerateArray())
                list.Add(new OllamaModel(m.GetProperty("name").GetString() ?? "", m.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0));
        return list;
    }

    public async Task<bool> IsReachableAsync()
    {
        try { using var r = await _http.GetAsync(_baseUrl + "/api/version", CancellationToken.None); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<string> ChatAsync(string model, string system, string user, int numPredict, Action<string>? onToken)
    {
        var payload = new
        {
            model,
            stream = true,
            format = "json",
            messages = new[] { new { role = "system", content = system }, new { role = "user", content = user } },
            options = new { num_predict = numPredict, temperature = 0.7 },
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var resp = await _http.PostAsync(_baseUrl + "/api/chat", content);
        resp.EnsureSuccessStatusCode();

        var sb = new StringBuilder();
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var c))
                {
                    var text = c.GetString() ?? "";
                    sb.Append(text);
                    onToken?.Invoke(sb.ToString());
                }
            }
            catch { /* ignore partial lines */ }
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- prompts

    private static string SystemPrompt(Language lang) =>
        $"You are a film director and storyboard artist for a 2D animation engine. You output ONLY valid JSON. " +
        $"Spoken narration must be in {(lang == Language.tr ? "Turkish" : "English")}. " +
        $"Every visual field must use exactly one of the provided enum values. Scenes do NOT need characters.";

    private static string UserPrompt(Project p, StyleBible bible, double durationSec)
    {
        double words = durationSec * (p.Language == Language.tr ? 2.1 : 2.5);
        int minS = Math.Max(3, (int)Math.Round(durationSec / 9));
        int maxS = Math.Max(minS + 2, (int)Math.Round(durationSec / 4.5));
        string cast = bible.Characters.Count > 0
            ? string.Join(", ", bible.Characters.Select(c => c.Id))
            : "none";
        string modeNote = p.CharacterMode switch
        {
            CharacterMode.none => "NEVER include characters; use places, objects and motion.",
            CharacterMode.required => "Every scene must include at least one character.",
            CharacterMode.optional => "Include a character only when the narration needs one.",
            _ => "Use characters only where they help the story; empty-room shots are encouraged.",
        };
        return $$"""
Write a storyboard for a {{durationSec:0}}-second narrated 2D video about: "{{p.Topic}}".
Cast ids you may use: {{cast}}. {{modeNote}}
Write {{minS}}-{{maxS}} scenes covering ~{{words:0}} words of narration.

Return ONLY JSON of this shape:
{
  "title": "string",
  "hook": "first spoken sentence",
  "music_mood": "e.g. dark ambient",
  "scenes": [
    {
      "duration": 5.5,
      "voiceover": "the sentence spoken in this scene",
      "visual_note": "short note on what we see",
      "environment": { "type": "bedroom|living_room|office|street|city|forest|park|beach|cave|space|abstract|...", "time": "dawn|day|sunset|night", "weather": "clear|rain|snow|fog|storm|wind" },
      "lighting": { "style": "daylight|night|moonlight|warm|cold|sunset|neon|flashlight|dark|horror|dramatic", "direction": "left|right|top|window|front|back", "intensity": 0.6 },
      "characters": [ { "id": "character_01", "emotion": "neutral|happy|sad|angry|scared|confused|surprised|worried|shocked|evil|tired|excited", "actions": ["idle","walk","talk","react"], "position": "far_left|left|center|right|far_right", "facing_left": false } ],
      "objects": [ { "type": "chair|table|bed|window|lamp|door|tree|streetlight|car|box", "position": "left|center|right|character_hand|foreground|background", "state": "default|on|off|open|closed|vibrating", "scale": 1.0 } ],
      "effects": ["rain","snow","fog","dust","lightning","particles"],
      "camera": { "shot": "wide|establishing|medium|close_up|extreme_close_up|pov|low_angle|high_angle|top_down|over_shoulder|side_view", "movement": "static|slow_zoom_in|slow_zoom_out|pan_left|pan_right|tilt_up|tilt_down|tracking|orbit|handheld|shake", "target": "character_01 or object id or null" },
      "transition": "crossfade|fade|fade_black|blur|cut"
    }
  ]
}
""";
    }

    // ---------------------------------------------------------------- plan

    public async Task<Script> GenerateAsync(Project p, StyleBible bible, string model, Action<string>? onProgress)
    {
        var raw = await ChatAsync(model, SystemPrompt(p.Language), UserPrompt(p, bible, p.DurationSec), 4096, onProgress);
        var jsonText = Json.ExtractObject(raw)
            ?? throw new InvalidOperationException("LLM responded with no JSON object.");
        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        var script = new Script
        {
            Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? p.Topic : p.Topic,
            Hook = root.TryGetProperty("hook", out var h) ? h.GetString() ?? "" : "",
            MusicMood = root.TryGetProperty("music_mood", out var mm) ? mm.GetString() ?? "" : "",
        };

        var sceneArr = root.TryGetProperty("scenes", out var s) ? s.EnumerateArray().ToList() : new List<JsonElement>();
        var allowed = new[] { EnvType.bedroom, EnvType.office, EnvType.street, EnvType.city, EnvType.forest, EnvType.park, EnvType.beach, EnvType.cave, EnvType.space, EnvType.@abstract, EnvType.living_room, EnvType.kitchen, EnvType.classroom, EnvType.hospital, EnvType.store, EnvType.warehouse, EnvType.hallway, EnvType.basement, EnvType.rooftop, EnvType.house };
        var envNames = Enum.GetNames<EnvType>().ToHashSet();
        string lastEnv = "";
        var lastMoves = new List<string>();

        for (int i = 0; i < sceneArr.Count; i++)
        {
            var el = sceneArr[i];
            var scene = new Scene { SceneId = i + 1, Seed = p.MasterSeed + i };

            scene.Voiceover = el.TryGetProperty("voiceover", out var vo) ? vo.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(scene.Voiceover)) continue;
            scene.CaptionText = el.TryGetProperty("visual_note", out var vn) && !string.IsNullOrEmpty(vn.GetString()) ? scene.Voiceover : scene.Voiceover;
            scene.DurationEstimate = el.TryGetProperty("duration", out var du) ? du.GetDouble() : Math.Max(1.6, scene.Voiceover.Split(' ').Length / 2.5);
            scene.Spec.Duration = scene.DurationEstimate;

            // environment
            if (el.TryGetProperty("environment", out var env))
            {
                var ty = env.TryGetProperty("type", out var te) ? te.GetString() ?? "" : "";
                var envT = envNames.Contains(ty) ? (EnvType)Enum.Parse(typeof(EnvType), ty) : (ty == "" && lastEnv != "" ? (EnvType)Enum.Parse(typeof(EnvType), lastEnv) : GuessEnv(scene.Voiceover));
                scene.Spec.Environment.Type = envT;
                lastEnv = envT.ToString();
                scene.Spec.Environment.Time = ParseEnum<TimeOfDay>(env.TryGetProperty("time", out var tt) ? tt.GetString() : null, TimeOfDay.day);
                scene.Spec.Environment.Weather = ParseEnum<Weather>(env.TryGetProperty("weather", out var we) ? we.GetString() : null, Weather.clear);
            }
            else scene.Spec.Environment.Type = GuessEnv(scene.Voiceover);

            // lighting
            if (el.TryGetProperty("lighting", out var li))
            {
                scene.Spec.Lighting.Style = ParseEnum<LightStyle>(li.TryGetProperty("style", out var ls) ? ls.GetString() : null, scene.Spec.Environment.Time == TimeOfDay.night ? LightStyle.night : LightStyle.daylight);
                scene.Spec.Lighting.Direction = ParseEnum<LightDir>(li.TryGetProperty("direction", out var ld) ? ld.GetString() : null, LightDir.top);
                scene.Spec.Lighting.Intensity = li.TryGetProperty("intensity", out var li2) ? li2.GetDouble() : 0.8;
            }

            // characters
            if (el.TryGetProperty("characters", out var chs) && chs.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in chs.EnumerateArray())
                {
                    var cin = new CharacterInScene
                    {
                        Id = c.TryGetProperty("id", out var cid) ? cid.GetString() ?? "character_01" : "character_01",
                        Emotion = ParseEnum<Emotion>(c.TryGetProperty("emotion", out var ce) ? ce.GetString() : null, Emotion.neutral),
                        Position = ParseEnum<ScreenPos>(c.TryGetProperty("position", out var cp) ? cp.GetString() : null, ScreenPos.center),
                        FacingLeft = c.TryGetProperty("facing_left", out var cf) && cf.GetBoolean(),
                        Scale = c.TryGetProperty("scale", out var cs) ? cs.GetDouble() : 1.0,
                    };
                    if (c.TryGetProperty("actions", out var acts) && acts.ValueKind == JsonValueKind.Array)
                        cin.Actions = acts.EnumerateArray().Select(a => ParseEnum<ActionKind>(a.GetString(), ActionKind.idle)).Where(a => a != ActionKind.idle).Distinct().ToList();
                    if (cin.Actions.Count == 0) cin.Actions = new List<ActionKind> { ActionKind.idle };
                    scene.Spec.Characters.Add(cin);
                }
            }

            // objects
            if (el.TryGetProperty("objects", out var obs) && obs.ValueKind == JsonValueKind.Array)
            {
                int oi = 0;
                foreach (var o in obs.EnumerateArray())
                {
                    var ob = new ObjectInScene
                    {
                        Id = o.TryGetProperty("id", out var oid) ? oid.GetString() ?? "obj" + oi : "obj" + oi,
                        Type = o.TryGetProperty("type", out var ot) ? ot.GetString() ?? "chair" : "chair",
                        Position = ParseEnum<ObjPos>(o.TryGetProperty("position", out var op) ? op.GetString() : null, ObjPos.center),
                        State = o.TryGetProperty("state", out var os) ? os.GetString() ?? "default" : "default",
                        Scale = o.TryGetProperty("scale", out var osc) ? osc.GetDouble() : 1.0,
                    };
                    scene.Spec.Objects.Add(ob); oi++;
                }
            }

            // effects
            if (el.TryGetProperty("effects", out var fx) && fx.ValueKind == JsonValueKind.Array)
                scene.Spec.Effects = fx.EnumerateArray().Select(f => ParseEnum<EffectKind>(f.GetString(), EffectKind.dust)).Where(f => f != EffectKind.dust).Distinct().ToList();
            if (scene.Spec.Environment.Weather == Weather.rain && !scene.Spec.Effects.Contains(EffectKind.rain)) scene.Spec.Effects.Add(EffectKind.rain);
            if (scene.Spec.Environment.Weather == Weather.snow && !scene.Spec.Effects.Contains(EffectKind.snow)) scene.Spec.Effects.Add(EffectKind.snow);

            // camera
            if (el.TryGetProperty("camera", out var cam))
            {
                scene.Spec.Camera.Shot = ParseEnum<ShotType>(cam.TryGetProperty("shot", out var cs2) ? cs2.GetString() : null, i == 0 ? ShotType.establishing : ShotType.medium);
                var mv = ParseEnum<CameraMove>(cam.TryGetProperty("movement", out var cm2) ? cm2.GetString() : null, CameraMove.@static);
                // avoid repeating the same movement 3x in a row
                if (lastMoves.Count >= 2 && lastMoves[^1] == mv.ToString() && lastMoves[^2] == mv.ToString())
                    mv = new[] { CameraMove.slow_zoom_in, CameraMove.pan_right, CameraMove.slow_zoom_out, CameraMove.pan_left, CameraMove.handheld }[i % 5];
                scene.Spec.Camera.Movement = mv;
                lastMoves.Add(mv.ToString());
                if (lastMoves.Count > 6) lastMoves.RemoveAt(0);
                var tgt = cam.TryGetProperty("target", out var ct) ? ct.GetString() : null;
                scene.Spec.Camera.Target = string.IsNullOrEmpty(tgt) ? null : tgt;
            }

            scene.Spec.Transition = ParseEnum<TransitionKind>(el.TryGetProperty("transition", out var tr) ? tr.GetString() : null, TransitionKind.crossfade);
            if (i == 0 && scene.Spec.Camera.Shot == ShotType.medium) scene.Spec.Camera.Shot = ShotType.establishing;

            // infer emotion from text if left neutral
            if (scene.Spec.Characters.Count > 0 && scene.Spec.Characters[0].Emotion == Emotion.neutral)
            {
                var inferred = GuessEmotion(scene.Voiceover);
                if (inferred != Emotion.neutral) scene.Spec.Characters[0].Emotion = inferred;
            }

            script.Scenes.Add(scene);
        }

        if (script.Scenes.Count == 0)
            throw new InvalidOperationException("LLM produced no usable scenes. Try a simpler topic or a larger model.");
        return script;
    }

    private static T ParseEnum<T>(string? s, T fallback) where T : struct =>
        s != null && Enum.TryParse<T>(s, true, out var v) ? v : fallback;

    private static EnvType GuessEnv(string text)
    {
        string t = text.ToLowerInvariant();
        if (t.Contains("office") || t.Contains("desk") || t.Contains("meeting")) return EnvType.office;
        if (t.Contains("bed") || t.Contains("sleep") || t.Contains("room") && t.Contains("bed")) return EnvType.bedroom;
        if (t.Contains("kitchen") || t.Contains("cook") || t.Contains("food")) return EnvType.kitchen;
        if (t.Contains("school") || t.Contains("class") || t.Contains("student")) return EnvType.classroom;
        if (t.Contains("hospital") || t.Contains("doctor") || t.Contains("patient")) return EnvType.hospital;
        if (t.Contains("store") || t.Contains("shop") || t.Contains("market")) return EnvType.store;
        if (t.Contains("warehouse") || t.Contains("factory")) return EnvType.warehouse;
        if (t.Contains("street") || t.Contains("road") || t.Contains("alley")) return EnvType.street;
        if (t.Contains("city") || t.Contains("urban") || t.Contains("building")) return EnvType.city;
        if (t.Contains("forest") || t.Contains("tree") || t.Contains("jungle")) return EnvType.forest;
        if (t.Contains("park") || t.Contains("garden")) return EnvType.park;
        if (t.Contains("beach") || t.Contains("sea") || t.Contains("ocean")) return EnvType.beach;
        if (t.Contains("cave") || t.Contains("tunnel")) return EnvType.cave;
        if (t.Contains("space") || t.Contains("star") || t.Contains("planet") || t.Contains("galaxy")) return EnvType.space;
        if (t.Contains("rooftop") || t.Contains("roof")) return EnvType.rooftop;
        if (t.Contains("house") || t.Contains("home")) return EnvType.house;
        if (t.Contains("living room") || t.Contains("sofa") || t.Contains("couch")) return EnvType.living_room;
        return EnvType.@abstract;
    }

    private static Emotion GuessEmotion(string text)
    {
        string t = text.ToLowerInvariant();
        if (t.Contains("happy") || t.Contains("joy") || t.Contains("great")) return Emotion.happy;
        if (t.Contains("sad") || t.Contains("cry") || t.Contains("lonely")) return Emotion.sad;
        if (t.Contains("angry") || t.Contains("rage") || t.Contains("furious")) return Emotion.angry;
        if (t.Contains("scared") || t.Contains("fear") || t.Contains("afraid")) return Emotion.scared;
        if (t.Contains("surpris") || t.Contains("shock")) return Emotion.surprised;
        if (t.Contains("worried") || t.Contains("anxious") || t.Contains("nervous")) return Emotion.worried;
        if (t.Contains("confus") || t.Contains("wonder")) return Emotion.confused;
        if (t.Contains("tired") || t.Contains("exhaust")) return Emotion.tired;
        if (t.Contains("excite") || t.Contains("amaz")) return Emotion.excited;
        return Emotion.neutral;
    }
    public void Dispose() => _http.Dispose();
}
