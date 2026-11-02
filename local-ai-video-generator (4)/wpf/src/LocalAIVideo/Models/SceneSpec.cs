using System.Text.Json.Serialization;

namespace LocalAIVideo.Models;

// ----------------------------------------------------------------- enums

public enum EnvType
{
    bedroom, living_room, kitchen, office, classroom, hallway, street, city,
    forest, park, beach, hospital, store, warehouse, basement, cave, rooftop, house, space, @abstract
}

public enum TimeOfDay { dawn, day, sunset, night }
public enum Weather { clear, rain, snow, fog, storm, wind }

public enum LightStyle
{
    daylight, night, moonlight, warm, cold, sunset, neon,
    flashlight, dark, horror, dramatic
}

public enum LightDir { left, right, top, window, front, back }

public enum Emotion
{
    neutral, happy, sad, angry, scared, confused, surprised, worried, shocked, evil, tired, excited
}

public enum ActionKind
{
    idle, breathe, blink, look, turn, walk, run, sit, stand, reach,
    grab, drop, open, close, talk, react, fall, jump, attack, flee, hide
}

public enum ScreenPos { far_left, left, center, right, far_right }
public enum ObjPos { far_left, left, center, right, far_right, character_hand, foreground, background }

public enum EffectKind
{
    rain, snow, fog, smoke, fire, sparks, dust, particles,
    lightning, glow, wind, floating_dust, moving_shadows, screen_flicker
}

public enum ShotType
{
    wide, establishing, medium, close_up, extreme_close_up,
    pov, low_angle, high_angle, top_down, over_shoulder, side_view
}

public enum CameraMove
{
    @static, slow_zoom_in, slow_zoom_out, pan_left, pan_right,
    tilt_up, tilt_down, tracking, orbit, handheld, shake
}

public enum TransitionKind { crossfade, fade, fade_black, blur, cut }

public enum VisualStyle
{
    cinematic_2d, cartoon, dark_cartoon, comic, minimal, flat,
    stylized, horror, noir, neon, dreamlike, documentary
}

public enum CharacterKind
{
    human, animal, creature, monster, robot, alien, object, vehicle, shadow, @abstract
}

public enum CharacterMode { auto, none, optional, required }

// ----------------------------------------------------------------- objects

public class EnvironmentSpec
{
    public EnvType Type { get; set; } = EnvType.@abstract;
    public TimeOfDay Time { get; set; } = TimeOfDay.day;
    public Weather Weather { get; set; } = Weather.clear;
}

public class LightingSpec
{
    public LightStyle Style { get; set; } = LightStyle.daylight;
    public LightDir Direction { get; set; } = LightDir.top;
    public double Intensity { get; set; } = 0.8;
}

public class CharacterInScene
{
    public string Id { get; set; } = "character_01";
    public Emotion Emotion { get; set; } = Emotion.neutral;
    public List<ActionKind> Actions { get; set; } = new() { ActionKind.idle };
    public ScreenPos Position { get; set; } = ScreenPos.center;
    public bool FacingLeft { get; set; }
    public double Scale { get; set; } = 1.0;
    public string Note { get; set; } = "";
}

public class ObjectInScene
{
    public string Id { get; set; } = "obj";
    public string Type { get; set; } = "chair";
    public ObjPos Position { get; set; } = ObjPos.center;
    public string State { get; set; } = "default";
    public double Scale { get; set; } = 1.0;
}

public class Interaction
{
    public string Actor { get; set; } = "";
    public string Verb { get; set; } = "look_at";
    public string Target { get; set; } = "";
}

public class CameraSpec
{
    public ShotType Shot { get; set; } = ShotType.medium;
    public CameraMove Movement { get; set; } = CameraMove.@static;
    public string? Target { get; set; }
}

/// <summary>The structured scene description the LLM produces and the engine renders.</summary>
public class SceneSpec
{
    public int Scene { get; set; }
    public double Duration { get; set; } = 5.0;
    public EnvironmentSpec Environment { get; set; } = new();
    public LightingSpec Lighting { get; set; } = new();
    public List<CharacterInScene> Characters { get; set; } = new();
    public List<ObjectInScene> Objects { get; set; } = new();
    public List<Interaction> Interactions { get; set; } = new();
    public List<EffectKind> Effects { get; set; } = new();
    public CameraSpec Camera { get; set; } = new();
    public TransitionKind Transition { get; set; } = TransitionKind.crossfade;
    public string VisualNote { get; set; } = "";
}
