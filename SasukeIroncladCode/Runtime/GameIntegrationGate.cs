using System.Globalization;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeBuildFingerprint(
    string Branch,
    string SteamBuildId,
    string Sts2Sha256,
    string ModuleMvid,
    string BaseLibVersion,
    string BaseLibManifestSha256 = ""
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
        if (!string.Equals(runtime.Branch, RollingBetaBranch, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"Runtime branch is {runtime.Branch}; only rolling {RollingBetaBranch} is supported.");
            return new(false, false, null, reasons);
        }

        List<GameIntegrationProfile> matching = contract.Profiles
            .Where(profile => Matches(profile, runtime))
            .ToList();
        if (matching.Count == 0)
        {
            reasons.Add("No exact latest-beta integration profile matches the runtime build fingerprint.");
            return new(false, false, null, reasons);
        }
        if (matching.Count > 1)
            throw new InvalidOperationException("Duplicate build fingerprint exists in the integration contract.");

        GameIntegrationProfile profile = matching[0];
        if (IsSuperseded(contract, profile))
        {
            reasons.Add("The matching public-beta profile is superseded by a newer known Beta build.");
            return new(false, false, profile.Id, reasons);
        }
        if (profile.Status != "verified")
        {
            reasons.Add($"Integration profile {profile.Id} status is {profile.Status}; verified is required.");
            return new(false, false, profile.Id, reasons);
        }
        if (!ValidateAttestation(profile, runtime, contract.Policy, now, reasons))
            return new(false, false, profile.Id, reasons);

        bool enableVisuals = ValidateVisualBindings(contract, profile, reasons);
        bool enableTitles = ValidateTitleBindings(contract, profile, reasons);
        if (enableVisuals)
            reasons.Add("All required visual bindings are verified for the exact latest public-beta build.");
        if (enableTitles)
            reasons.Add("All required title bindings are verified for the exact latest public-beta build.");
        return new(enableVisuals, enableTitles, profile.Id, reasons);
    }

    private static bool ValidateAttestation(
        GameIntegrationProfile profile,
        RuntimeBuildFingerprint runtime,
        GameIntegrationPolicy policy,
        DateTimeOffset now,
        List<string> reasons)
    {
        GameBetaAttestationSpec attestation = profile.BetaAttestation;
        if (attestation.Status != "verified")
        {
            reasons.Add($"Profile {profile.Id} beta attestation is {attestation.Status}; verified is required.");
            return false;
        }
        if (!string.Equals(attestation.Branch, RollingBetaBranch, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(attestation.InstalledBuildId, runtime.SteamBuildId, StringComparison.Ordinal) ||
            !string.Equals(attestation.RemoteBuildId, runtime.SteamBuildId, StringComparison.Ordinal))
        {
            reasons.Add("Profile beta attestation does not prove this exact installed public-beta build is current.");
            return false;
        }
        if (!string.Equals(attestation.Source, "steamcmd_app_info_print", StringComparison.Ordinal) ||
            !IsSha256(attestation.SteamCmdOutputSha256))
        {
            reasons.Add("Profile beta attestation source or SteamCMD digest is invalid.");
            return false;
        }
        if (!DateTimeOffset.TryParse(
                attestation.CheckedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset checkedAt))
        {
            reasons.Add("Profile beta attestation timestamp is invalid.");
            return false;
        }
        if (checkedAt > now.AddMinutes(5))
        {
            reasons.Add("Profile beta attestation timestamp is in the future.");
            return false;
        }
        TimeSpan age = now - checkedAt;
        if (age > TimeSpan.FromHours(policy.MaxBetaAttestationAgeHours))
        {
            reasons.Add($"Profile beta attestation is stale ({age.TotalHours:F1}h old).");
            return false;
        }
        reasons.Add($"Remote public-beta attestation is current ({age.TotalHours:F1}h old).");
        return true;
    }

    private static bool ValidateVisualBindings(
        GameIntegrationContractMap contract,
        GameIntegrationProfile profile,
        List<string> reasons)
    {
        Dictionary<string, GameVisualBindingSpec> bindings = profile.VisualBindings
            .ToDictionary(binding => binding.Id, StringComparer.Ordinal);
        List<string> unavailable = [];
        foreach (string eventId in contract.RequiredVisualEvents)
        {
            if (!bindings.TryGetValue(eventId, out GameVisualBindingSpec? binding) ||
                binding.Status != "verified" ||
                binding.Fallback != "original_visual" ||
                !IsMethodDefinitionToken(binding.MetadataToken) ||
                string.IsNullOrWhiteSpace(binding.DeclaringType) ||
                string.IsNullOrWhiteSpace(binding.MethodSignature))
            {
                unavailable.Add(eventId);
            }
        }
        if (unavailable.Count == 0)
            return true;
        reasons.Add("Visual bindings remain disabled: " + string.Join(", ", unavailable));
        return false;
    }

    private static bool ValidateTitleBindings(
        GameIntegrationContractMap contract,
        GameIntegrationProfile profile,
        List<string> reasons)
    {
        Dictionary<string, GameTitleBindingSpec> bindings = profile.TitleBindings
            .ToDictionary(binding => binding.SurfaceId, StringComparer.Ordinal);
        List<string> unavailable = [];
        foreach (string surfaceId in contract.RequiredTitleSurfaces)
        {
            if (!bindings.TryGetValue(surfaceId, out GameTitleBindingSpec? binding) ||
                binding.Status != "verified" ||
                binding.Fallback != "original_title" ||
                !IsMethodDefinitionToken(binding.MetadataToken) ||
                string.IsNullOrWhiteSpace(binding.DeclaringType) ||
                string.IsNullOrWhiteSpace(binding.MethodSignature))
            {
                unavailable.Add(surfaceId);
            }
        }
        if (unavailable.Count == 0)
            return true;
        reasons.Add("Title bindings remain disabled: " + string.Join(", ", unavailable));
        return false;
    }

    private static bool IsSuperseded(GameIntegrationContractMap contract, GameIntegrationProfile profile)
    {
        if (!ulong.TryParse(profile.Fingerprint.SteamBuildId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong current))
            return true;
        return contract.Profiles.Any(candidate =>
            string.Equals(candidate.Branch, RollingBetaBranch, StringComparison.OrdinalIgnoreCase) &&
            ulong.TryParse(candidate.Fingerprint.SteamBuildId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong build) &&
            build > current);
    }

    private static bool Matches(GameIntegrationProfile profile, RuntimeBuildFingerprint runtime) =>
        string.Equals(profile.Branch, runtime.Branch, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.SteamBuildId, runtime.SteamBuildId, StringComparison.Ordinal) &&
        string.Equals(profile.Fingerprint.Sts2Sha256, runtime.Sts2Sha256, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.ModuleMvid, runtime.ModuleMvid, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.BaseLibVersion, runtime.BaseLibVersion, StringComparison.Ordinal);

    private static void ValidateContract(GameIntegrationContractMap contract)
    {
        if (contract.SchemaVersion != 1)
            throw new InvalidOperationException("Unsupported game integration contract schema.");
        if (contract.GameplayChanges)
            throw new InvalidOperationException("Game integration contract may not enable gameplay changes.");
        GameIntegrationPolicy policy = contract.Policy;
        if (!policy.ExactBuildFingerprintRequired || !policy.UnverifiedBindingsDisabled ||
            !policy.FallbackToOriginalOnMismatch || !policy.MultiplayerLocalVisualsOnly ||
            !policy.LatestBetaOnly || !policy.RemoteBetaAttestationRequired ||
            !policy.StaleProfilesDisabled ||
            !string.Equals(policy.RequiredBranch, RollingBetaBranch, StringComparison.OrdinalIgnoreCase) ||
            policy.MaxBetaAttestationAgeHours is < 1 or > 168)
        {
            throw new InvalidOperationException("Game integration contract safety policy is incomplete.");
        }
        if (contract.RequiredVisualEvents.Count == 0 || contract.RequiredTitleSurfaces.Count == 0)
            throw new InvalidOperationException("Game integration contract lacks required binding surfaces.");
        if (contract.RequiredVisualEvents.Distinct(StringComparer.Ordinal).Count() != contract.RequiredVisualEvents.Count ||
            contract.RequiredTitleSurfaces.Distinct(StringComparer.Ordinal).Count() != contract.RequiredTitleSurfaces.Count)
        {
            throw new InvalidOperationException("Game integration contract contains duplicate binding identifiers.");
        }
        foreach (GameIntegrationProfile profile in contract.Profiles)
            ValidateProfile(profile);
    }

    private static void ValidateProfile(GameIntegrationProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id) ||
            !string.Equals(profile.Branch, RollingBetaBranch, StringComparison.OrdinalIgnoreCase) ||
            !ulong.TryParse(profile.Fingerprint.SteamBuildId, NumberStyles.None, CultureInfo.InvariantCulture, out _) ||
            !IsSha256(profile.Fingerprint.Sts2Sha256) ||
            !Guid.TryParse(profile.Fingerprint.ModuleMvid, out _) ||
            string.IsNullOrWhiteSpace(profile.Fingerprint.BaseLibVersion))
        {
            throw new InvalidOperationException($"Integration profile {profile.Id} has an invalid build fingerprint.");
        }
        if (profile.VisualBindings.Select(binding => binding.Id).Distinct(StringComparer.Ordinal).Count() !=
            profile.VisualBindings.Count ||
            profile.TitleBindings.Select(binding => binding.SurfaceId).Distinct(StringComparer.Ordinal).Count() !=
            profile.TitleBindings.Count)
        {
            throw new InvalidOperationException($"Integration profile {profile.Id} has duplicate bindings.");
        }
    }

    private static bool IsMethodDefinitionToken(string value)
    {
        if (value.Length != 10 || !value.StartsWith("0x06", StringComparison.OrdinalIgnoreCase))
            return false;
        return uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint token) &&
               (token & 0xFF000000u) == 0x06000000u &&
               (token & 0x00FFFFFFu) != 0;
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
}
