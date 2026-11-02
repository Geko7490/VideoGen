using LocalAIVideo.Helpers;
using LocalAIVideo.Models;

namespace LocalAIVideo.Core;

/// <summary>
/// Persistent, file-based storage (the native replacement for IndexedDB).
/// Layout under %APPDATA%\LocalAIVideo\ :
///   projects\<id>\project.json
///   projects\<id>\tts\scene_001.wav ...
///   projects\<id>\export\final.mp4
///   models\  (downloaded model weights, managed by the Model Manager)
/// </summary>
public static class Storage
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LocalAIVideo");

    public static string ProjectsDir => Path.Combine(Root, "projects");
    public static string ModelsDir => Path.Combine(Root, "models");

    static Storage()
    {
        Directory.CreateDirectory(ProjectsDir);
        Directory.CreateDirectory(ModelsDir);
    }

    public static string ProjectDir(string id) => Path.Combine(ProjectsDir, id);
    public static string ProjectFile(string id) => Path.Combine(ProjectDir(id), "project.json");
    public static string TtsDir(string id) => Path.Combine(ProjectDir(id), "tts");
    public static string ExportDir(string id) => Path.Combine(ProjectDir(id), "export");

    public static void EnsureProjectDirs(string id)
    {
        Directory.CreateDirectory(ProjectDir(id));
        Directory.CreateDirectory(TtsDir(id));
        Directory.CreateDirectory(ExportDir(id));
    }

    public static void SaveProject(Project p)
    {
        p.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        EnsureProjectDirs(p.Id);
        Json.Write(ProjectFile(p.Id), p);
    }

    public static Project? LoadProject(string id) => Json.Read<Project>(ProjectFile(id));

    public static List<Project> ListProjects()
    {
        var list = new List<Project>();
        if (!Directory.Exists(ProjectsDir)) return list;
        foreach (var dir in Directory.GetDirectories(ProjectsDir))
        {
            var p = Json.Read<Project>(Path.Combine(dir, "project.json"));
            if (p != null) list.Add(p);
        }
        return list.OrderByDescending(p => p.UpdatedAt).ToList();
    }

    public static void DeleteProject(string id)
    {
        var dir = ProjectDir(id);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    public static void RequestPersistentStorage()
    {
        // On Windows the APPDATA tree is already persistent; nothing extra to do.
        // Kept as a hook so the API mirrors the original manager.
    }
}
