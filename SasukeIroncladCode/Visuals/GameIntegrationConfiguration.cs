using System.Text.Json.Serialization;

namespace SasukeIronclad.SasukeIroncladCode.Visuals;

public sealed class GameIntegrationContractMap
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("gameplay_changes")] public bool GameplayChanges { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("policy")] public GameIntegrationPolicy Policy { get; init; } = new();
    [JsonPropertyName("required_visual_events")] public List<string> RequiredVisualEvents { get; init; } = [];
    [JsonPropertyName("required_title_surfaces")] public List<string> RequiredTitleSurfaces { get; init; } = [];
    [JsonPropertyName("profiles")] public List<GameIntegrationProfile> Profiles { get; init; } = [];
}

public sealed class GameIntegrationPolicy
{
    [JsonPropertyName("exact_build_fingerprint_required")] public bool ExactBuildFingerprintRequired { get; init; }
    [JsonPropertyName("unverified_bindings_disabled")] public bool UnverifiedBindingsDisabled { get; init; }
    [JsonPropertyName("fallback_to_original_on_mismatch")] public bool FallbackToOriginalOnMismatch { get; init; }
    [JsonPropertyName("multiplayer_local_visuals_only")] public bool MultiplayerLocalVisualsOnly { get; init; }
    [JsonPropertyName("required_branch")] public string RequiredBranch { get; init; } = string.Empty;
    [JsonPropertyName("latest_beta_only")] public bool LatestBetaOnly { get; init; }
    [JsonPropertyName("remote_beta_attestation_required")] public bool RemoteBetaAttestationRequired { get; init; }
    [JsonPropertyName("stale_profiles_disabled")] public bool StaleProfilesDisabled { get; init; }
    [JsonPropertyName("max_beta_attestation_age_hours")] public int MaxBetaAttestationAgeHours { get; init; }
}

public sealed class GameIntegrationProfile
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("branch")] public string Branch { get; init; } = string.Empty;
    [JsonPropertyName("fingerprint")] public GameBuildFingerprintSpec Fingerprint { get; init; } = new();
    [JsonPropertyName("beta_attestation")] public GameBetaAttestationSpec BetaAttestation { get; init; } = new();
    [JsonPropertyName("visual_bindings")] public List<GameVisualBindingSpec> VisualBindings { get; init; } = [];
    [JsonPropertyName("title_bindings")] public List<GameTitleBindingSpec> TitleBindings { get; init; } = [];
}

public sealed class GameBuildFingerprintSpec
{
    [JsonPropertyName("steam_build_id")] public string SteamBuildId { get; init; } = string.Empty;
    [JsonPropertyName("sts2_sha256")] public string Sts2Sha256 { get; init; } = string.Empty;
    [JsonPropertyName("module_mvid")] public string ModuleMvid { get; init; } = string.Empty;
    [JsonPropertyName("baselib_version")] public string BaseLibVersion { get; init; } = string.Empty;
}

public sealed class GameBetaAttestationSpec
{
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("branch")] public string Branch { get; init; } = string.Empty;
    [JsonPropertyName("installed_build_id")] public string InstalledBuildId { get; init; } = string.Empty;
    [JsonPropertyName("remote_build_id")] public string RemoteBuildId { get; init; } = string.Empty;
    [JsonPropertyName("checked_at_utc")] public string CheckedAtUtc { get; init; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;
    [JsonPropertyName("steamcmd_output_sha256")] public string SteamCmdOutputSha256 { get; init; } = string.Empty;
}

public sealed class GameVisualBindingSpec
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("declaring_type")] public string DeclaringType { get; init; } = string.Empty;
    [JsonPropertyName("method_signature")] public string MethodSignature { get; init; } = string.Empty;
    [JsonPropertyName("metadata_token")] public string MetadataToken { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("fallback")] public string Fallback { get; init; } = string.Empty;
}

public sealed class GameTitleBindingSpec
{
    [JsonPropertyName("surface_id")] public string SurfaceId { get; init; } = string.Empty;
    [JsonPropertyName("declaring_type")] public string DeclaringType { get; init; } = string.Empty;
    [JsonPropertyName("method_signature")] public string MethodSignature { get; init; } = string.Empty;
    [JsonPropertyName("metadata_token")] public string MetadataToken { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("fallback")] public string Fallback { get; init; } = string.Empty;
}
