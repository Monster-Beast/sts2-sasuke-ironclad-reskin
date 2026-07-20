using System.Text.Json;
using Godot;

namespace SasukeIronclad.SasukeIroncladCode.Visuals;

internal static class VisualConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    internal static T Load<T>(string resourcePath) where T : class
    {
        using FileAccess file = FileAccess.Open(resourcePath, FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException($"Unable to open visual configuration: {resourcePath}");
        string json = file.GetAsText();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Visual configuration was empty: {resourcePath}");
    }
}
