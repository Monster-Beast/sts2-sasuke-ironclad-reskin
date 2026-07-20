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

public sealed class PresentationTierMap
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("gameplay_changes")] public bool GameplayChanges { get; init; }
    [JsonPropertyName("default_policy")] public PresentationDefaultPolicy DefaultPolicy { get; init; } = new();
    [JsonPropertyName("tiers")] public List<PresentationTier> Tiers { get; init; } = [];
}

public sealed class PresentationDefaultPolicy
{
    [JsonPropertyName("basic_common_cap")] public string BasicCommonCap { get; init; } = string.Empty;
    [JsonPropertyName("cutin_enabled")] public bool CutinEnabled { get; init; }
    [JsonPropertyName("finisher_policy")] public string FinisherPolicy { get; init; } = string.Empty;
    [JsonPropertyName("fast_mode_cap")] public string FastModeCap { get; init; } = string.Empty;
    [JsonPropertyName("low_flash_available")] public bool LowFlashAvailable { get; init; }
    [JsonPropertyName("runtime_inputs_read_only")] public List<string> RuntimeInputsReadOnly { get; init; } = [];
}

public sealed class PresentationTier
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("rank")] public int Rank { get; init; }
    [JsonPropertyName("max_duration_ms")] public int MaxDurationMs { get; init; }
    [JsonPropertyName("cutin")] public bool Cutin { get; init; }
    [JsonPropertyName("screen_takeover")] public bool ScreenTakeover { get; init; }
    [JsonPropertyName("default_profiles")] public List<string> DefaultProfiles { get; init; } = [];
}

public sealed class PresentationSurfaceMap
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("profile")] public string Profile { get; init; } = string.Empty;
    [JsonPropertyName("surfaces")] public List<PresentationSurface> Surfaces { get; init; } = [];
}

public sealed class PresentationSurface
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("phase")] public string Phase { get; init; } = string.Empty;
    [JsonPropertyName("asset_kind")] public string AssetKind { get; init; } = string.Empty;
    [JsonPropertyName("required")] public bool Required { get; init; }
    [JsonPropertyName("target")] public string Target { get; init; } = string.Empty;
    [JsonPropertyName("fallback")] public string Fallback { get; init; } = string.Empty;
}
