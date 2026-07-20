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
        GameIntegrationProfile? profile = contract.Profiles.SingleOrDefault(candidate => Matches(candidate, runtime));
        if (profile is null)
        {
            reasons.Add("No exact audited build profile matched the current runtime fingerprint.");
            return new GameIntegrationDecision(false, false, null, reasons);
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
        uint.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out _);

    private static void ValidateContract(GameIntegrationContractMap contract)
    {
        if (contract.SchemaVersion != 1 || contract.GameplayChanges)
            throw new InvalidOperationException("Unsupported or gameplay-changing integration contract.");
        if (!contract.Policy.ExactBuildFingerprintRequired ||
            !contract.Policy.UnverifiedBindingsDisabled ||
            !contract.Policy.FallbackToOriginalOnMismatch ||
            !contract.Policy.MultiplayerLocalVisualsOnly)
        {
            throw new InvalidOperationException("Integration safety policy is incomplete.");
        }

        if (contract.RequiredVisualEvents.Count == 0 ||
            contract.RequiredVisualEvents.Count != contract.RequiredVisualEvents.Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidOperationException("Required visual event IDs must be non-empty and unique.");
        }
        if (contract.RequiredTitleSurfaces.Count == 0 ||
            contract.RequiredTitleSurfaces.Count != contract.RequiredTitleSurfaces.Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidOperationException("Required title surface IDs must be non-empty and unique.");
        }

        HashSet<string> profileIds = [];
        foreach (GameIntegrationProfile profile in contract.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id) || !profileIds.Add(profile.Id))
                throw new InvalidOperationException($"Missing or duplicate integration profile ID: {profile.Id}");
            if (string.IsNullOrWhiteSpace(profile.Branch) ||
                string.IsNullOrWhiteSpace(profile.Fingerprint.SteamBuildId) ||
                string.IsNullOrWhiteSpace(profile.Fingerprint.Sts2Sha256) ||
                string.IsNullOrWhiteSpace(profile.Fingerprint.ModuleMvid) ||
                string.IsNullOrWhiteSpace(profile.Fingerprint.BaseLibVersion))
            {
                throw new InvalidOperationException($"Integration profile {profile.Id} lacks an exact build fingerprint.");
            }
        }
    }
}
