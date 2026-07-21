using System.Text.Json.Serialization;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed class RuntimeCanaryReviewMap
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("profile_id")] public string ProfileId { get; init; } = string.Empty;
    [JsonPropertyName("runtime")] public RuntimeCanaryFingerprintSpec Runtime { get; init; } = new();
    [JsonPropertyName("policy")] public RuntimeCanaryReviewPolicy Policy { get; init; } = new();
    [JsonPropertyName("readiness")] public RuntimeCanaryReadiness Readiness { get; init; } = new();
    [JsonPropertyName("decisions")] public List<RuntimeCanaryBindingDecision> Decisions { get; init; } = [];
}

public sealed class RuntimeCanaryFingerprintSpec
{
    [JsonPropertyName("branch")] public string Branch { get; init; } = string.Empty;
    [JsonPropertyName("steam_build_id")] public string SteamBuildId { get; init; } = string.Empty;
    [JsonPropertyName("sts2_sha256")] public string Sts2Sha256 { get; init; } = string.Empty;
    [JsonPropertyName("module_mvid")] public string ModuleMvid { get; init; } = string.Empty;
    [JsonPropertyName("baselib_version")] public string BaseLibVersion { get; init; } = string.Empty;
}

public sealed class RuntimeCanaryReviewPolicy
{
    [JsonPropertyName("auto_selection_forbidden")] public bool AutoSelectionForbidden { get; init; }
    [JsonPropertyName("production_contract_remains_disabled")] public bool ProductionContractRemainsDisabled { get; init; }
    [JsonPropertyName("runtime_bindings_remain_disabled")] public bool RuntimeBindingsRemainDisabled { get; init; }
    [JsonPropertyName("canary_requires_explicit_local_opt_in")] public bool CanaryRequiresExplicitLocalOptIn { get; init; }
    [JsonPropertyName("fallback_to_original_required")] public bool FallbackToOriginalRequired { get; init; }
    [JsonPropertyName("multiplayer_local_visual_only_required")] public bool MultiplayerLocalVisualOnlyRequired { get; init; }
}

public sealed class RuntimeCanaryReadiness
{
    [JsonPropertyName("title_canary_candidate_count")] public int TitleCanaryCandidateCount { get; init; }
    [JsonPropertyName("visual_canary_candidate_count")] public int VisualCanaryCandidateCount { get; init; }
    [JsonPropertyName("blocked_binding_count")] public int BlockedBindingCount { get; init; }
    [JsonPropertyName("production_profile_ready")] public bool ProductionProfileReady { get; init; }
}

public sealed class RuntimeCanaryBindingDecision
{
    [JsonPropertyName("binding_id")] public string BindingId { get; init; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("selected_target_id")] public string SelectedTargetId { get; init; } = string.Empty;
    [JsonPropertyName("declaring_type")] public string DeclaringType { get; init; } = string.Empty;
    [JsonPropertyName("method_signature")] public string MethodSignature { get; init; } = string.Empty;
    [JsonPropertyName("metadata_token")] public string MetadataToken { get; init; } = string.Empty;
    [JsonPropertyName("observed_in_all_sessions")] public bool ObservedInAllSessions { get; init; }
}

public sealed class RuntimeCanaryOptIn
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("mode")] public string Mode { get; init; } = string.Empty;
    [JsonPropertyName("expected_branch")] public string ExpectedBranch { get; init; } = string.Empty;
    [JsonPropertyName("expected_build_id")] public string ExpectedBuildId { get; init; } = string.Empty;
    [JsonPropertyName("enable_animations")] public bool EnableAnimations { get; init; } = true;
    [JsonPropertyName("enable_titles")] public bool EnableTitles { get; init; } = true;
    [JsonPropertyName("low_flash")] public bool LowFlash { get; init; } = true;
    [JsonPropertyName("fast_mode")] public bool FastMode { get; init; }
}

public sealed class CurrentBetaCardScopeMap
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("gameplay_changes")] public bool GameplayChanges { get; init; }
    [JsonPropertyName("branch")] public string Branch { get; init; } = string.Empty;
    [JsonPropertyName("steam_build_id")] public string SteamBuildId { get; init; } = string.Empty;
    [JsonPropertyName("active_cards")] public List<CurrentBetaCardSpec> ActiveCards { get; init; } = [];
}

public sealed class CurrentBetaCardSpec
{
    [JsonPropertyName("card_id")] public string CardId { get; init; } = string.Empty;
    [JsonPropertyName("model_type")] public string ModelType { get; init; } = string.Empty;
}

public sealed class RuntimeCanaryStatusDocument
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("generated_at_utc")] public string GeneratedAtUtc { get; init; } = string.Empty;
    [JsonPropertyName("mod_initializer_reached")] public bool ModInitializerReached { get; init; } = true;
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("animations_enabled")] public bool AnimationsEnabled { get; init; }
    [JsonPropertyName("titles_enabled")] public bool TitlesEnabled { get; init; }
    [JsonPropertyName("patched_binding_ids")] public IReadOnlyList<string> PatchedBindingIds { get; init; } = [];
    [JsonPropertyName("reasons")] public IReadOnlyList<string> Reasons { get; init; } = [];
}
