using System.Globalization;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeBuildFingerprint(
    string Branch,
    string SteamBuildId,
    string Sts2Sha256,
    string ModuleMvid,
    string BaseLibVersion
);

public sealed record GameIntegrationDecision(
    bool EnableVisualBindings,
    bool EnableTitleBindings,
    string? ProfileId,
    IReadOnlyList<string> Reasons
)
{
    public bool AnyEnabled => EnableVisualBindings || EnableTitleBindings;
}

public static class GameIntegrationGate
{
    private const string ContractPath = "res://SasukeIronclad/data/game_integration_contract.json";

    public static GameIntegrationDecision EvaluateCurrentBuild(RuntimeBuildFingerprint runtime) =>
        Evaluate(VisualConfigLoader.Load<GameIntegrationContractMap>(ContractPath), runtime);

    public static GameIntegrationDecision Evaluate(
        GameIntegrationContractMap contract,
        RuntimeBuildFingerprint runtime)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(runtime);
        ValidateContract(contract);

        List<string> reasons = [];
        if (contract.Status != "verified")
        {
            reasons.Add($"Integration contract status is {contract.Status}; verified is required.");
            return new GameIntegrationDecision(false, false, null, reasons);
        }

        List<GameIntegrationProfile> matches = contract.Profiles
            .Where(candidate => Matches(candidate, runtime))
            .ToList();
        if (matches.Count == 0)
        {
            reasons.Add("No exact audited build profile matched the current runtime fingerprint.");
            return new GameIntegrationDecision(false, false, null, reasons);
        }
        if (matches.Count > 1)
        {
            reasons.Add("Multiple integration profiles matched the same runtime fingerprint; all bindings remain disabled.");
            return new GameIntegrationDecision(false, false, null, reasons);
        }

        GameIntegrationProfile profile = matches[0];
        if (profile.Status != "verified")
        {
            reasons.Add($"Matched profile {profile.Id} has status {profile.Status}; verified is required.");
            return new GameIntegrationDecision(false, false, profile.Id, reasons);
        }

        Dictionary<string, GameVisualBindingSpec> visualBindings = profile.VisualBindings
            .ToDictionary(binding => binding.Id, StringComparer.Ordinal);
        Dictionary<string, GameTitleBindingSpec> titleBindings = profile.TitleBindings
            .ToDictionary(binding => binding.SurfaceId, StringComparer.Ordinal);

        List<string> missingVisuals = contract.RequiredVisualEvents
            .Where(id => !visualBindings.TryGetValue(id, out GameVisualBindingSpec? binding) || !IsVerified(binding))
            .ToList();
        List<string> missingTitles = contract.RequiredTitleSurfaces
            .Where(id => !titleBindings.TryGetValue(id, out GameTitleBindingSpec? binding) || !IsVerified(binding))
            .ToList();

        bool enableVisuals = missingVisuals.Count == 0;
        bool enableTitles = missingTitles.Count == 0;
        if (!enableVisuals)
            reasons.Add($"Visual bindings are incomplete or unverified: {string.Join(", ", missingVisuals)}");
        if (!enableTitles)
            reasons.Add($"Title bindings are incomplete or unverified: {string.Join(", ", missingTitles)}");
        if (enableVisuals)
            reasons.Add("All required visual bindings match the audited build profile.");
        if (enableTitles)
            reasons.Add("All required title bindings match the audited build profile.");

        return new GameIntegrationDecision(enableVisuals, enableTitles, profile.Id, reasons);
    }

    private static bool Matches(GameIntegrationProfile profile, RuntimeBuildFingerprint runtime) =>
        string.Equals(profile.Branch, runtime.Branch, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.SteamBuildId, runtime.SteamBuildId, StringComparison.Ordinal) &&
        string.Equals(profile.Fingerprint.Sts2Sha256, runtime.Sts2Sha256, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.ModuleMvid, runtime.ModuleMvid, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.BaseLibVersion, runtime.BaseLibVersion, StringComparison.Ordinal);

    private static bool IsVerified(GameVisualBindingSpec binding) =>
        binding.Status == "verified" &&
        !string.IsNullOrWhiteSpace(binding.DeclaringType) &&
        !string.IsNullOrWhiteSpace(binding.MethodSignature) &&
        IsMetadataToken(binding.MetadataToken) &&
        !string.IsNullOrWhiteSpace(binding.Fallback);

    private static bool IsVerified(GameTitleBindingSpec binding) =>
        binding.Status == "verified" &&
        !string.IsNullOrWhiteSpace(binding.DeclaringType) &&
        !string.IsNullOrWhiteSpace(binding.MethodSignature) &&
        IsMetadataToken(binding.MetadataToken) &&
        binding.Fallback == "original_title";

    private static bool IsMetadataToken(string value) =>
        value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
        value.Length == 10 &&
        uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => Uri.IsHexDigit(character));

    private static string FingerprintKey(GameIntegrationProfile profile) =>
        string.Join(
            "|",
            profile.Branch.ToLowerInvariant(),
            profile.Fingerprint.SteamBuildId,
            profile.Fingerprint.Sts2Sha256.ToLowerInvariant(),
            profile.Fingerprint.ModuleMvid.ToLowerInvariant(),
            profile.Fingerprint.BaseLibVersion);

    private static void ValidateContract(GameIntegrationContractMap contract)
    {
        if (contract.SchemaVersion != 1 || contract.GameplayChanges)
            throw new InvalidOperationException("Unsupported or gameplay-changing integration contract.");
        if (contract.Status is not ("pending_local_audit" or "verified"))
            throw new InvalidOperationException($"Unsupported integration contract status: {contract.Status}");
        if (!contract.Policy.ExactBuildFingerprintRequired ||
            !contract.Policy.UnverifiedBindingsDisabled ||
            !contract.Policy.FallbackToOriginalOnMismatch ||
            !contract.Policy.MultiplayerLocalVisualsOnly)
        {
            throw new InvalidOperationException("Integration safety policy is incomplete.");
        }

        HashSet<string> requiredVisuals = contract.RequiredVisualEvents.ToHashSet(StringComparer.Ordinal);
        if (requiredVisuals.Count == 0 || requiredVisuals.Count != contract.RequiredVisualEvents.Count)
            throw new InvalidOperationException("Required visual event IDs must be non-empty and unique.");

        HashSet<string> requiredTitles = contract.RequiredTitleSurfaces.ToHashSet(StringComparer.Ordinal);
        if (requiredTitles.Count == 0 || requiredTitles.Count != contract.RequiredTitleSurfaces.Count)
            throw new InvalidOperationException("Required title surface IDs must be non-empty and unique.");

        HashSet<string> profileIds = [];
        HashSet<string> fingerprintKeys = [];
        foreach (GameIntegrationProfile profile in contract.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id) || !profileIds.Add(profile.Id))
                throw new InvalidOperationException($"Missing or duplicate integration profile ID: {profile.Id}");
            if (profile.Status is not ("pending_review" or "verified"))
                throw new InvalidOperationException($"Integration profile {profile.Id} has unsupported status {profile.Status}.");
            if (string.IsNullOrWhiteSpace(profile.Branch) ||
                string.IsNullOrWhiteSpace(profile.Fingerprint.SteamBuildId) ||
                !IsSha256(profile.Fingerprint.Sts2Sha256) ||
                !Guid.TryParse(profile.Fingerprint.ModuleMvid, out _) ||
                string.IsNullOrWhiteSpace(profile.Fingerprint.BaseLibVersion))
            {
                throw new InvalidOperationException($"Integration profile {profile.Id} lacks a valid exact build fingerprint.");
            }
            if (!fingerprintKeys.Add(FingerprintKey(profile)))
                throw new InvalidOperationException($"Duplicate build fingerprint in integration profile {profile.Id}.");

            HashSet<string> visualIds = [];
            foreach (GameVisualBindingSpec binding in profile.VisualBindings)
            {
                if (string.IsNullOrWhiteSpace(binding.Id) || !visualIds.Add(binding.Id))
                    throw new InvalidOperationException($"Profile {profile.Id} has a missing or duplicate visual binding ID.");
                if (!requiredVisuals.Contains(binding.Id))
                    throw new InvalidOperationException($"Profile {profile.Id} references unknown visual event {binding.Id}.");
                if (binding.Status is not ("pending_review" or "verified"))
                    throw new InvalidOperationException($"Visual binding {binding.Id} has unsupported status {binding.Status}.");
            }

            HashSet<string> titleIds = [];
            foreach (GameTitleBindingSpec binding in profile.TitleBindings)
            {
                if (string.IsNullOrWhiteSpace(binding.SurfaceId) || !titleIds.Add(binding.SurfaceId))
                    throw new InvalidOperationException($"Profile {profile.Id} has a missing or duplicate title surface ID.");
                if (!requiredTitles.Contains(binding.SurfaceId))
                    throw new InvalidOperationException($"Profile {profile.Id} references unknown title surface {binding.SurfaceId}.");
                if (binding.Status is not ("pending_review" or "verified"))
                    throw new InvalidOperationException($"Title binding {binding.SurfaceId} has unsupported status {binding.Status}.");
            }
        }
    }
}
