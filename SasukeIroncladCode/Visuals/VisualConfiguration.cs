using System.Text.Json.Serialization;

namespace SasukeIronclad.SasukeIroncladCode.Visuals;

public sealed class CardVisualMap
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("profile")] public string Profile { get; init; } = string.Empty;
    [JsonPropertyName("gameplay_changes")] public bool GameplayChanges { get; init; }
    [JsonPropertyName("cards")] public List<CardVisualSpec> Cards { get; init; } = [];
}

public sealed class CardVisualSpec
{
    [JsonPropertyName("card_id")] public string CardId { get; init; } = string.Empty;
    [JsonPropertyName("game_id_verified")] public bool GameIdVerified { get; init; }
    [JsonPropertyName("visual_family")] public string VisualFamily { get; init; } = string.Empty;
    [JsonPropertyName("action_profile")] public string ActionProfile { get; init; } = string.Empty;
    [JsonPropertyName("concept")] public string Concept { get; init; } = string.Empty;
    [JsonPropertyName("art_status")] public string ArtStatus { get; init; } = string.Empty;
    [JsonPropertyName("legal_status")] public string LegalStatus { get; init; } = string.Empty;
}

public sealed class ActionProfileMap
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("gameplay_timing_locked")] public bool GameplayTimingLocked { get; init; }
    [JsonPropertyName("profiles")] public List<ActionProfile> Profiles { get; init; } = [];
}

public sealed class ActionProfile
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("max_duration_ms")] public int MaxDurationMs { get; init; }
    [JsonPropertyName("impact_fraction")] public double ImpactFraction { get; init; }
    [JsonPropertyName("camera_shake")] public string CameraShake { get; init; } = "none";
    [JsonPropertyName("vfx")] public List<string> Vfx { get; init; } = [];
    [JsonPropertyName("accessibility_variant")] public string? AccessibilityVariant { get; init; }
    [JsonPropertyName("fallback")] public string Fallback { get; init; } = string.Empty;
}
