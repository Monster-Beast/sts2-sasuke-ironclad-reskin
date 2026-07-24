using System.Text.Json.Serialization;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed class RuntimeObservationManifestMap
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("gameplay_changes")] public bool GameplayChanges { get; init; }
    [JsonPropertyName("profile_id")] public string ProfileId { get; init; } = string.Empty;
    [JsonPropertyName("branch")] public string Branch { get; init; } = string.Empty;
    [JsonPropertyName("fingerprint")] public RuntimeObservationFingerprintSpec Fingerprint { get; init; } = new();
    [JsonPropertyName("beta_attestation")] public RuntimeObservationBetaAttestationSpec BetaAttestation { get; init; } = new();
    [JsonPropertyName("policy")] public RuntimeObservationPolicySpec Policy { get; init; } = new();
    [JsonPropertyName("required_binding_ids")] public List<string> RequiredBindingIds { get; init; } = [];
    [JsonPropertyName("active_card_model_types")] public List<string> ActiveCardModelTypes { get; init; } = [];
    [JsonPropertyName("targets")] public List<RuntimeObservationTargetSpec> Targets { get; init; } = [];
}

public sealed class RuntimeObservationFingerprintSpec
{
    [JsonPropertyName("steam_build_id")] public string SteamBuildId { get; init; } = string.Empty;
    [JsonPropertyName("sts2_sha256")] public string Sts2Sha256 { get; init; } = string.Empty;
    [JsonPropertyName("module_mvid")] public string ModuleMvid { get; init; } = string.Empty;
    [JsonPropertyName("baselib_version")] public string BaseLibVersion { get; init; } = string.Empty;
    [JsonPropertyName("baselib_manifest_sha256")] public string BaseLibManifestSha256 { get; init; } = string.Empty;
}

public sealed class RuntimeObservationBetaAttestationSpec
{
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("checked_at_utc")] public string CheckedAtUtc { get; init; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;
    [JsonPropertyName("steamcmd_output_sha256")] public string SteamCmdOutputSha256 { get; init; } = string.Empty;
    [JsonPropertyName("max_age_hours")] public int MaxAgeHours { get; init; }
}

public sealed class RuntimeObservationPolicySpec
{
    [JsonPropertyName("default_enabled")] public bool DefaultEnabled { get; init; }
    [JsonPropertyName("read_only_only")] public bool ReadOnlyOnly { get; init; }
    [JsonPropertyName("exact_fingerprint_required")] public bool ExactFingerprintRequired { get; init; }
    [JsonPropertyName("fail_closed_on_target_mismatch")] public bool FailClosedOnTargetMismatch { get; init; }
    [JsonPropertyName("unpatch_on_reset")] public bool UnpatchOnReset { get; init; }
    [JsonPropertyName("opt_in_file_name")] public string OptInFileName { get; init; } = string.Empty;
    [JsonPropertyName("output_directory_name")] public string OutputDirectoryName { get; init; } = string.Empty;
    [JsonPropertyName("default_max_events")] public int DefaultMaxEvents { get; init; }
    [JsonPropertyName("minimum_max_events")] public int MinimumMaxEvents { get; init; }
    [JsonPropertyName("maximum_max_events")] public int MaximumMaxEvents { get; init; }
    [JsonPropertyName("maximum_stack_frames")] public int MaximumStackFrames { get; init; }
    [JsonPropertyName("capture_argument_values")] public bool CaptureArgumentValues { get; init; }
    [JsonPropertyName("capture_absolute_paths")] public bool CaptureAbsolutePaths { get; init; }
}

public sealed class RuntimeObservationTargetSpec
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("binding_ids")] public List<string> BindingIds { get; init; } = [];
    [JsonPropertyName("purpose")] public string Purpose { get; init; } = string.Empty;
    [JsonPropertyName("declaring_type")] public string DeclaringType { get; init; } = string.Empty;
    [JsonPropertyName("method_signature")] public string MethodSignature { get; init; } = string.Empty;
    [JsonPropertyName("metadata_token")] public string MetadataToken { get; init; } = string.Empty;
    [JsonPropertyName("capture_stack")] public bool CaptureStack { get; init; }
    [JsonPropertyName("required")] public bool Required { get; init; }
}

public sealed class RuntimeObservationOptIn
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("mode")] public string Mode { get; init; } = string.Empty;
    [JsonPropertyName("expected_branch")] public string ExpectedBranch { get; init; } = string.Empty;
    [JsonPropertyName("expected_build_id")] public string ExpectedBuildId { get; init; } = string.Empty;
    [JsonPropertyName("session_label")] public string SessionLabel { get; init; } = string.Empty;
    [JsonPropertyName("max_events")] public int? MaxEvents { get; init; }
    [JsonPropertyName("capture_stacks")] public bool CaptureStacks { get; init; } = true;
}

public sealed class PendingGameIntegrationProfileDocument
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("profile")] public GameIntegrationProfile Profile { get; init; } = new();
}
