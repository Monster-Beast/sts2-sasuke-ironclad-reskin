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
    private const string RollingBetaBranch = "public-beta";

    public static GameIntegrationDecision Evaluate(
        GameIntegrationContractMap contract,
        RuntimeBuildFingerprint runtime) =>
        Evaluate(contract, runtime, DateTimeOffset.UtcNow);

    public static GameIntegrationDecision Evaluate(
        GameIntegrationContractMap contract,
        RuntimeBuildFingerprint runtime,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(runtime);
        ValidateContract(contract);

        List<string> reasons = [];
        if (contract.Status != "verified")
        {
            reasons.Add($"Integration contract status is {contract.Status}; verified is required.");
            return new(false, false, null, reasons);
        }

        if (!string.Equals(runtime.Branch, contract.Policy.RequiredBranch, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(
                $"Runtime branch {runtime.Branch} does not match the required rolling beta branch " +
                $"{contract.Policy.RequiredBranch}."
            );
            return new(false, false, null, reasons);
        }

        List<GameIntegrationProfile> matches = contract.Profiles
            .Where(candidate => Matches(candidate, runtime))
            .ToList();
        if (matches.Count == 0)
        {
            reasons.Add("No exact audited build profile matched the current runtime fingerprint.");
            return new(false, false, null, reasons);
        }
        if (matches.Count > 1)
        {
            reasons.Add("Multiple integration profiles matched the same runtime fingerprint; all bindings remain disabled.");
            return new(false, false, null, reasons);
        }

        GameIntegrationProfile profile = matches[0];
        if (profile.Status != "verified")
        {
            reasons.Add($"Matched profile {profile.Id} has status {profile.Status}; verified is required.");
            return new(false, false, profile.Id, reasons);
        }

        if (!IsNewestKnownBetaProfile(contract, profile, reasons))
            return new(false, false, profile.Id, reasons);
        if (!IsFreshLatestBetaProfile(profile, contract.Policy, runtime, now, reasons))
            return new(false, false, profile.Id, reasons);

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
        reasons.Add(enableVisuals
            ? "All required visual bindings match the audited latest-beta build profile."
            : $"Visual bindings are incomplete or unverified: {string.Join(", ", missingVisuals)}");
        reasons.Add(enableTitles
            ? "All required title bindings match the audited latest-beta build profile."
            : $"Title bindings are incomplete or unverified: {string.Join(", ", missingTitles)}");
        return new(enableVisuals, enableTitles, profile.Id, reasons);
    }

    private static bool Matches(GameIntegrationProfile profile, RuntimeBuildFingerprint runtime) =>
        string.Equals(profile.Branch, runtime.Branch, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.SteamBuildId, runtime.SteamBuildId, StringComparison.Ordinal) &&
        string.Equals(profile.Fingerprint.Sts2Sha256, runtime.Sts2Sha256, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.ModuleMvid, runtime.ModuleMvid, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.BaseLibVersion, runtime.BaseLibVersion, StringComparison.Ordinal);

    private static bool IsNewestKnownBetaProfile(
        GameIntegrationContractMap contract,
        GameIntegrationProfile selected,
        List<string> reasons)
    {
        ulong selectedBuild = ParseBuildId(selected.BetaAttestation.RemoteBuildId, selected.Id);
        ulong newestBuild = contract.Profiles
            .Where(profile => string.Equals(
                profile.Branch,
                contract.Policy.RequiredBranch,
                StringComparison.OrdinalIgnoreCase))
            .Select(profile => ParseBuildId(profile.BetaAttestation.RemoteBuildId, profile.Id))
            .Max();

        if (selectedBuild != newestBuild)
        {
            reasons.Add(
                $"Profile {selected.Id} build {selectedBuild} is superseded by a newer known " +
                $"{contract.Policy.RequiredBranch} build {newestBuild}."
            );
            return false;
        }
        return true;
    }

    private static ulong ParseBuildId(string value, string profileId)
    {
        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong buildId) || buildId == 0)
            throw new InvalidOperationException($"Integration profile {profileId} has an invalid Steam buildid.");
        return buildId;
    }

    private static bool IsFreshLatestBetaProfile(
        GameIntegrationProfile profile,
        GameIntegrationPolicy policy,
        RuntimeBuildFingerprint runtime,
        DateTimeOffset now,
        List<string> reasons)
    {
        GameBetaAttestationSpec attestation = profile.BetaAttestation;
        if (attestation.Status != "verified")
        {
            reasons.Add($"Profile {profile.Id} beta attestation status is {attestation.Status}; verified is required.");
            return false;
        }
        if (!string.Equals(attestation.Branch, policy.RequiredBranch, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(profile.Branch, policy.RequiredBranch, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"Profile {profile.Id} is not attested for {policy.RequiredBranch}.");
            return false;
        }
        if (!string.Equals(attestation.InstalledBuildId, attestation.RemoteBuildId, StringComparison.Ordinal) ||
            !string.Equals(attestation.RemoteBuildId, profile.Fingerprint.SteamBuildId, StringComparison.Ordinal) ||
            !string.Equals(attestation.RemoteBuildId, runtime.SteamBuildId, StringComparison.Ordinal))
        {
            reasons.Add($"Profile {profile.Id} does not match the remotely attested latest beta buildid.");
            return false;
        }
        if (!DateTimeOffset.TryParse(
                attestation.CheckedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset checkedAt))
        {
            reasons.Add($"Profile {profile.Id} has an invalid beta attestation timestamp.");
            return false;
        }
        if (checkedAt > now.AddMinutes(5))
        {
            reasons.Add($"Profile {profile.Id} beta attestation timestamp is in the future.");
            return false;
        }
        TimeSpan age = now - checkedAt;
        if (age > TimeSpan.FromHours(policy.MaxBetaAttestationAgeHours))
        {
            reasons.Add(
                $"Profile {profile.Id} beta attestation is stale ({age.TotalHours:F1}h); " +
                $"maximum age is {policy.MaxBetaAttestationAgeHours}h."
            );
            return false;
        }
        if (!IsSha256(attestation.SteamCmdOutputSha256) ||
            !string.Equals(attestation.Source, "steamcmd_app_info_print", StringComparison.Ordinal))
        {
            reasons.Add($"Profile {profile.Id} beta attestation source or digest is invalid.");
            return false;
        }

        reasons.Add(
            $"Profile {profile.Id} is attested to the current {policy.RequiredBranch} build " +
            $"{attestation.RemoteBuildId}; attestation age is {age.TotalHours:F1}h."
        );
        return true;
    }

    private static bool IsVerified(GameVisualBindingSpec binding) =>
        binding.Status == "verified" && !string.IsNullOrWhiteSpace(binding.DeclaringType) &&
        !string.IsNullOrWhiteSpace(binding.MethodSignature) && IsMethodDefinitionToken(binding.MetadataToken) &&
        binding.Fallback == "original_visual";

    private static bool IsVerified(GameTitleBindingSpec binding) =>
        binding.Status == "verified" && !string.IsNullOrWhiteSpace(binding.DeclaringType) &&
        !string.IsNullOrWhiteSpace(binding.MethodSignature) && IsMethodDefinitionToken(binding.MetadataToken) &&
        binding.Fallback == "original_title";

    private static bool IsMethodDefinitionToken(string value) =>
        value.StartsWith("0x06", StringComparison.OrdinalIgnoreCase) && value.Length == 10 &&
        uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint token) &&
        (token & 0xFF000000u) == 0x06000000u && (token & 0x00FFFFFFu) != 0;

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static string FingerprintKey(GameIntegrationProfile profile) => string.Join("|",
        profile.Branch.ToLowerInvariant(), profile.Fingerprint.SteamBuildId,
        profile.Fingerprint.Sts2Sha256.ToLowerInvariant(), profile.Fingerprint.ModuleMvid.ToLowerInvariant(),
        profile.Fingerprint.BaseLibVersion);

    private static void ValidateContract(GameIntegrationContractMap contract)
    {
        if (contract.SchemaVersion != 1 || contract.GameplayChanges)
            throw new InvalidOperationException("Unsupported or gameplay-changing integration contract.");
        if (contract.Status is not ("pending_local_audit" or "verified"))
            throw new InvalidOperationException($"Unsupported integration contract status: {contract.Status}");
        if (!contract.Policy.ExactBuildFingerprintRequired || !contract.Policy.UnverifiedBindingsDisabled ||
            !contract.Policy.FallbackToOriginalOnMismatch || !contract.Policy.MultiplayerLocalVisualsOnly ||
            !contract.Policy.LatestBetaOnly || !contract.Policy.RemoteBetaAttestationRequired ||
            !contract.Policy.StaleProfilesDisabled)
        {
            throw new InvalidOperationException("Integration safety policy is incomplete.");
        }
        if (!string.Equals(contract.Policy.RequiredBranch, RollingBetaBranch, StringComparison.Ordinal) ||
            contract.Policy.MaxBetaAttestationAgeHours is < 1 or > 168)
        {
            throw new InvalidOperationException("Integration policy must target rolling public-beta with a 1-168 hour attestation window.");
        }

        HashSet<string> requiredVisuals = contract.RequiredVisualEvents.ToHashSet(StringComparer.Ordinal);
        HashSet<string> requiredTitles = contract.RequiredTitleSurfaces.ToHashSet(StringComparer.Ordinal);
        if (requiredVisuals.Count == 0 || requiredVisuals.Count != contract.RequiredVisualEvents.Count)
            throw new InvalidOperationException("Required visual event IDs must be non-empty and unique.");
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
            if (!string.Equals(profile.Branch, contract.Policy.RequiredBranch, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(profile.Fingerprint.SteamBuildId) ||
                !IsSha256(profile.Fingerprint.Sts2Sha256) || !Guid.TryParse(profile.Fingerprint.ModuleMvid, out _) ||
                string.IsNullOrWhiteSpace(profile.Fingerprint.BaseLibVersion))
            {
                throw new InvalidOperationException($"Integration profile {profile.Id} lacks a valid latest-beta fingerprint.");
            }
            ParseBuildId(profile.Fingerprint.SteamBuildId, profile.Id);
            if (profile.BetaAttestation.Status is not ("verified" or "pending_review") ||
                !string.Equals(profile.BetaAttestation.Branch, contract.Policy.RequiredBranch, StringComparison.Ordinal) ||
                !string.Equals(profile.BetaAttestation.InstalledBuildId, profile.Fingerprint.SteamBuildId, StringComparison.Ordinal) ||
                !string.Equals(profile.BetaAttestation.RemoteBuildId, profile.Fingerprint.SteamBuildId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(profile.BetaAttestation.CheckedAtUtc) ||
                !IsSha256(profile.BetaAttestation.SteamCmdOutputSha256))
            {
                throw new InvalidOperationException($"Integration profile {profile.Id} lacks a valid latest-beta attestation.");
            }
            ParseBuildId(profile.BetaAttestation.RemoteBuildId, profile.Id);
            if (!fingerprintKeys.Add(FingerprintKey(profile)))
                throw new InvalidOperationException($"Duplicate build fingerprint in integration profile {profile.Id}.");

            HashSet<string> visualIds = [];
            foreach (GameVisualBindingSpec binding in profile.VisualBindings)
            {
                if (string.IsNullOrWhiteSpace(binding.Id) || !visualIds.Add(binding.Id) || !requiredVisuals.Contains(binding.Id))
                    throw new InvalidOperationException($"Profile {profile.Id} contains an invalid visual binding ID: {binding.Id}");
                if (binding.Status is not ("pending_review" or "verified"))
                    throw new InvalidOperationException($"Visual binding {binding.Id} has unsupported status {binding.Status}.");
            }

            HashSet<string> titleIds = [];
            foreach (GameTitleBindingSpec binding in profile.TitleBindings)
            {
                if (string.IsNullOrWhiteSpace(binding.SurfaceId) || !titleIds.Add(binding.SurfaceId) || !requiredTitles.Contains(binding.SurfaceId))
                    throw new InvalidOperationException($"Profile {profile.Id} contains an invalid title surface ID: {binding.SurfaceId}");
                if (binding.Status is not ("pending_review" or "verified"))
                    throw new InvalidOperationException($"Title binding {binding.SurfaceId} has unsupported status {binding.Status}.");
            }
        }
    }
}
