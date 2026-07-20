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
    [JsonPropertyName("is_damage_card")] public bool IsDamageCard { get; init; }
    [JsonPropertyName("visual_family")] public string VisualFamily { get; init; } = string.Empty;
    [JsonPropertyName("action_profile")] public string ActionProfile { get; init; } = string.Empty;
    [JsonPropertyName("animation_id")] public string AnimationId { get; init; } = string.Empty;
    [JsonPropertyName("animation_mode")] public string AnimationMode { get; init; } = string.Empty;
    [JsonPropertyName("presentation_tier")] public string PresentationTier { get; init; } = string.Empty;
    [JsonPropertyName("special_animation_required")] public bool SpecialAnimationRequired { get; init; }
    [JsonPropertyName("concept")] public string Concept { get; init; } = string.Empty;
    [JsonPropertyName("art_status")] public string ArtStatus { get; init; } = string.Empty;
    [JsonPropertyName("animation_status")] public string AnimationStatus { get; init; } = string.Empty;
    [JsonPropertyName("legal_status")] public string LegalStatus { get; init; } = string.Empty;
}

public sealed class CardAnimationManifest
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("gameplay_changes")] public bool GameplayChanges { get; init; }
    [JsonPropertyName("policy")] public CardAnimationPolicy Policy { get; init; } = new();
    [JsonPropertyName("animations")] public List<CardAnimationSpec> Animations { get; init; } = [];
}

public sealed class CardAnimationPolicy
{
    [JsonPropertyName("card_identity_is_primary_key")] public bool CardIdentityIsPrimaryKey { get; init; }
    [JsonPropertyName("all_damage_cards_require_unique_timeline")] public bool AllDamageCardsRequireUniqueTimeline { get; init; }
    [JsonPropertyName("damage_selects_base_animation")] public bool DamageSelectsBaseAnimation { get; init; }
    [JsonPropertyName("hit_count_selects_base_animation")] public bool HitCountSelectsBaseAnimation { get; init; }
    [JsonPropertyName("damage_and_hits_are_variant_parameters_only")] public bool DamageAndHitsAreVariantParametersOnly { get; init; }
    [JsonPropertyName("fallback_allowed_only_when_unverified_or_asset_failure")] public bool FallbackAllowedOnlyWhenUnverifiedOrAssetFailure { get; init; }
    [JsonPropertyName("allowed_animation_modes")] public List<string> AllowedAnimationModes { get; init; } = [];
    [JsonPropertyName("required_variants_for_damage_cards")] public List<string> RequiredVariantsForDamageCards { get; init; } = [];
    [JsonPropertyName("runtime_inputs_read_only")] public List<string> RuntimeInputsReadOnly { get; init; } = [];
}

public sealed class CardAnimationSpec
{
    [JsonPropertyName("card_id")] public string CardId { get; init; } = string.Empty;
    [JsonPropertyName("game_id_verified")] public bool GameIdVerified { get; init; }
    [JsonPropertyName("is_damage_card")] public bool IsDamageCard { get; init; }
    [JsonPropertyName("animation_id")] public string AnimationId { get; init; } = string.Empty;
    [JsonPropertyName("animation_mode")] public string AnimationMode { get; init; } = string.Empty;
    [JsonPropertyName("presentation_tier")] public string PresentationTier { get; init; } = string.Empty;
    [JsonPropertyName("base_action_profile")] public string BaseActionProfile { get; init; } = string.Empty;
    [JsonPropertyName("unique_timeline")] public bool UniqueTimeline { get; init; }
    [JsonPropertyName("hit_sync")] public string HitSync { get; init; } = string.Empty;
    [JsonPropertyName("damage_role")] public string DamageRole { get; init; } = string.Empty;
    [JsonPropertyName("sequence")] public List<string> Sequence { get; init; } = [];
    [JsonPropertyName("variants")] public List<string> Variants { get; init; } = [];
    [JsonPropertyName("fallback")] public string Fallback { get; init; } = string.Empty;
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
    [JsonPropertyName("category")] public string Category { get; init; } = string.Empty;
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
    [JsonPropertyName("card_identity_first")] public bool CardIdentityFirst { get; init; }
    [JsonPropertyName("damage_selects_base_animation")] public bool DamageSelectsBaseAnimation { get; init; }
    [JsonPropertyName("hit_count_selects_base_animation")] public bool HitCountSelectsBaseAnimation { get; init; }
    [JsonPropertyName("damage_variant_fields")] public List<string> DamageVariantFields { get; init; } = [];
    [JsonPropertyName("hit_count_variant_fields")] public List<string> HitCountVariantFields { get; init; } = [];
    [JsonPropertyName("cutin_enabled")] public bool CutinEnabled { get; init; }
    [JsonPropertyName("fast_mode_cap")] public string FastModeCap { get; init; } = string.Empty;
    [JsonPropertyName("low_flash_available")] public bool LowFlashAvailable { get; init; }
    [JsonPropertyName("fallback_policy")] public string FallbackPolicy { get; init; } = string.Empty;
    [JsonPropertyName("runtime_inputs_read_only")] public List<string> RuntimeInputsReadOnly { get; init; } = [];
}

public sealed class PresentationTier
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("rank")] public int Rank { get; init; }
    [JsonPropertyName("max_duration_ms")] public int MaxDurationMs { get; init; }
    [JsonPropertyName("cutin")] public bool Cutin { get; init; }
    [JsonPropertyName("screen_takeover")] public bool ScreenTakeover { get; init; }
    [JsonPropertyName("purpose")] public string Purpose { get; init; } = string.Empty;
    [JsonPropertyName("development_fallback_profiles")] public List<string> DevelopmentFallbackProfiles { get; init; } = [];
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

public sealed class CardNameOverrideMap
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("gameplay_changes")] public bool GameplayChanges { get; init; }
    [JsonPropertyName("policy")] public CardNameOverridePolicy Policy { get; init; } = new();
    [JsonPropertyName("cards")] public List<CardNameOverrideSpec> Cards { get; init; } = [];
}

public sealed class CardNameOverridePolicy
{
    [JsonPropertyName("display_only")] public bool DisplayOnly { get; init; }
    [JsonPropertyName("internal_card_id_unchanged")] public bool InternalCardIdUnchanged { get; init; }
    [JsonPropertyName("preserve_rules_text")] public bool PreserveRulesText { get; init; }
    [JsonPropertyName("preserve_upgrade_state")] public bool PreserveUpgradeState { get; init; }
    [JsonPropertyName("fallback_to_original_name")] public bool FallbackToOriginalName { get; init; }
    [JsonPropertyName("allow_derived_cards")] public bool AllowDerivedCards { get; init; }
    [JsonPropertyName("supported_locales")] public List<string> SupportedLocales { get; init; } = [];
    [JsonPropertyName("default_locale")] public string DefaultLocale { get; init; } = "zh-CN";
}

public sealed class CardNameOverrideSpec
{
    [JsonPropertyName("card_id")] public string CardId { get; init; } = string.Empty;
    [JsonPropertyName("original_name")] public Dictionary<string, string> OriginalName { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("display_name")] public Dictionary<string, string> DisplayName { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("card_kind")] public string CardKind { get; init; } = "normal";
    [JsonPropertyName("semantic_anchor")] public string SemanticAnchor { get; init; } = string.Empty;
    [JsonPropertyName("art_concept")] public string ArtConcept { get; init; } = string.Empty;
    [JsonPropertyName("rename_status")] public string RenameStatus { get; init; } = string.Empty;
    [JsonPropertyName("game_id_verified")] public bool GameIdVerified { get; init; }
}
