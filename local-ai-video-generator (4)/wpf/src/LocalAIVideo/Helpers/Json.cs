using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalAIVideo.Helpers;

public static class Json
{
    public static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static T? Read<T>(string path)
    {
        if (!File.Exists(path)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), Opts); }
        catch { return default; }
    }

    public static void Write<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Opts));
    }

    /// Pulls the first JSON object out of an LLM response that may contain prose/markdown fences.
    public static string? ExtractObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        var fence = System.Text.RegularExpressions.Regex.Match(t, @"```(?:json)?\s*([\s\S]*?)```");
        if (fence.Success) t = fence.Groups[1].Value.Trim();

        int start = t.IndexOf('{');
        int end = t.LastIndexOf('}');
        if (start < 0 || end < 0 || end <= start) return null;
        return t.Substring(start, end - start + 1);
    }
}
