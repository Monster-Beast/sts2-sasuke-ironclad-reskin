using System.Text.Json.Serialization;

namespace SasukeIronclad.SasukeIroncladCode.Visuals;

public sealed class CardTitleSurfaceMap
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("gameplay_changes")] public bool GameplayChanges { get; init; }
    [JsonPropertyName("fallback")] public string Fallback { get; init; } = "original_title";
    [JsonPropertyName("surfaces")] public List<CardTitleSurfaceSpec> Surfaces { get; init; } = [];
}

public sealed class CardTitleSurfaceSpec
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("purpose")] public string Purpose { get; init; } = string.Empty;
    [JsonPropertyName("max_lines")] public int MaxLines { get; init; }
    [JsonPropertyName("minimum_scale")] public double MinimumScale { get; init; }
    [JsonPropertyName("width_units")] public Dictionary<string, double> WidthUnits { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("overflow_order")] public List<string> OverflowOrder { get; init; } = [];
    [JsonPropertyName("hook_status")] public string HookStatus { get; init; } = string.Empty;
}

public sealed record CardTitlePresentation(
    string CardId,
    string SurfaceId,
    string Locale,
    string DisplayName,
    string OriginalName,
    bool Upgraded,
    int MaxLines,
    double MinimumScale,
    double WidthUnits,
    IReadOnlyList<string> OverflowOrder,
    string Fallback
);
