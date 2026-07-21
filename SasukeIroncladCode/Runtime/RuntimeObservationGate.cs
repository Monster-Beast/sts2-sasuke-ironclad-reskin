using System.Globalization;
using System.Text.RegularExpressions;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeObservationGateResult(
    bool Enabled,
    int MaxEvents,
    int MaxStackFrames,
    IReadOnlyList<string> Reasons
);

public static partial class RuntimeObservationGate
{
    private const string RequiredBranch = "public-beta";
    private const string RequiredMode = "read_only";

    public static RuntimeObservationGateResult Evaluate(
        RuntimeObservationManifestMap manifest,
        GameIntegrationProfile pendingProfile,
        RuntimeObservationOptIn optIn,
        RuntimeBuildFingerprint runtime,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(pendingProfile);
        ArgumentNullException.ThrowIfNull(optIn);
        ArgumentNullException.ThrowIfNull(runtime);

        List<string> reasons = [];
        try
        {
            ValidateManifest(manifest);
        }
        catch (Exception exception)
        {
            return Disabled($"Observation manifest validation failed: {exception.Message}");
        }

        if (!optIn.Enabled || optIn.SchemaVersion != 1 ||
            !string.Equals(optIn.Mode, RequiredMode, StringComparison.Ordinal))
        {
            return Disabled("Runtime observation requires an explicit schema-1 read_only opt-in marker.");
        }
        if (!SessionLabelPattern().IsMatch(optIn.SessionLabel))
            return Disabled("Runtime observation session_label must use 1-64 ASCII letters, digits, dots, underscores or hyphens.");

        if (!Matches(manifest, runtime))
            return Disabled("Runtime observation manifest does not match the exact loaded game fingerprint.");
        if (!Matches(pendingProfile, runtime) ||
            !string.Equals(pendingProfile.Id, manifest.ProfileId, StringComparison.Ordinal) ||
            !string.Equals(pendingProfile.Status, "pending_review", StringComparison.Ordinal))
        {
            return Disabled("The pending-review profile does not match the exact loaded game fingerprint and observation manifest.");
        }
        if (!string.Equals(optIn.ExpectedBranch, runtime.Branch, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(optIn.ExpectedBuildId, runtime.SteamBuildId, StringComparison.Ordinal))
        {
            return Disabled("The opt-in marker does not acknowledge the exact loaded public-beta build.");
        }

        if (!ValidateBetaAttestation(manifest, pendingProfile, runtime, now, reasons))
            return new(false, 0, 0, reasons);

        int maxEvents = optIn.MaxEvents ?? manifest.Policy.DefaultMaxEvents;
        if (maxEvents < manifest.Policy.MinimumMaxEvents || maxEvents > manifest.Policy.MaximumMaxEvents)
        {
            return Disabled(
                $"max_events must be between {manifest.Policy.MinimumMaxEvents} and {manifest.Policy.MaximumMaxEvents}."
            );
        }
        int maxStackFrames = optIn.CaptureStacks ? manifest.Policy.MaximumStackFrames : 0;
        reasons.Add(
            $"Read-only runtime observation enabled for {runtime.Branch} build {runtime.SteamBuildId}; " +
            $"session={optIn.SessionLabel}; max_events={maxEvents}."
        );
        return new(true, maxEvents, maxStackFrames, reasons);
    }

    private static bool ValidateBetaAttestation(
        RuntimeObservationManifestMap manifest,
        GameIntegrationProfile profile,
        RuntimeBuildFingerprint runtime,
        DateTimeOffset now,
        List<string> reasons)
    {
        GameBetaAttestationSpec profileAttestation = profile.BetaAttestation;
        RuntimeObservationBetaAttestationSpec observationAttestation = manifest.BetaAttestation;
        if (!string.Equals(profileAttestation.Status, "verified", StringComparison.Ordinal) ||
            !string.Equals(observationAttestation.Status, "verified", StringComparison.Ordinal) ||
            !string.Equals(profileAttestation.Branch, RequiredBranch, StringComparison.Ordinal) ||
            !string.Equals(profileAttestation.InstalledBuildId, runtime.SteamBuildId, StringComparison.Ordinal) ||
            !string.Equals(profileAttestation.RemoteBuildId, runtime.SteamBuildId, StringComparison.Ordinal) ||
            !string.Equals(profileAttestation.Source, "steamcmd_app_info_print", StringComparison.Ordinal) ||
            !string.Equals(observationAttestation.Source, profileAttestation.Source, StringComparison.Ordinal) ||
            !string.Equals(
                observationAttestation.SteamCmdOutputSha256,
                profileAttestation.SteamCmdOutputSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(observationAttestation.CheckedAtUtc, profileAttestation.CheckedAtUtc, StringComparison.Ordinal))
        {
            reasons.Add("Observation beta attestation does not match the verified pending-review profile.");
            return false;
        }
        if (!IsSha256(profileAttestation.SteamCmdOutputSha256) || observationAttestation.MaxAgeHours is < 1 or > 168)
        {
            reasons.Add("Observation beta attestation digest or age policy is invalid.");
            return false;
        }
        if (!DateTimeOffset.TryParse(
                profileAttestation.CheckedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset checkedAt))
        {
            reasons.Add("Observation beta attestation timestamp is invalid.");
            return false;
        }
        if (checkedAt > now.AddMinutes(5))
        {
            reasons.Add("Observation beta attestation timestamp is in the future.");
            return false;
        }
        TimeSpan age = now - checkedAt;
        if (age > TimeSpan.FromHours(observationAttestation.MaxAgeHours))
        {
            reasons.Add(
                $"Observation beta attestation is stale ({age.TotalHours:F1}h); rerun the latest-beta audit before observing."
            );
            return false;
        }
        reasons.Add($"Latest public-beta attestation is fresh ({age.TotalHours:F1}h old).");
        return true;
    }

    private static bool Matches(RuntimeObservationManifestMap manifest, RuntimeBuildFingerprint runtime) =>
        string.Equals(manifest.Branch, runtime.Branch, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(manifest.Fingerprint.SteamBuildId, runtime.SteamBuildId, StringComparison.Ordinal) &&
        string.Equals(manifest.Fingerprint.Sts2Sha256, runtime.Sts2Sha256, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(manifest.Fingerprint.ModuleMvid, runtime.ModuleMvid, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(manifest.Fingerprint.BaseLibVersion, runtime.BaseLibVersion, StringComparison.Ordinal) &&
        string.Equals(
            manifest.Fingerprint.BaseLibManifestSha256,
            runtime.BaseLibManifestSha256,
            StringComparison.OrdinalIgnoreCase);

    private static bool Matches(GameIntegrationProfile profile, RuntimeBuildFingerprint runtime) =>
        string.Equals(profile.Branch, runtime.Branch, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.SteamBuildId, runtime.SteamBuildId, StringComparison.Ordinal) &&
        string.Equals(profile.Fingerprint.Sts2Sha256, runtime.Sts2Sha256, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.ModuleMvid, runtime.ModuleMvid, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(profile.Fingerprint.BaseLibVersion, runtime.BaseLibVersion, StringComparison.Ordinal);

    private static void ValidateManifest(RuntimeObservationManifestMap manifest)
    {
        if (manifest.SchemaVersion != 1 || manifest.GameplayChanges || manifest.Status != "observation_only")
            throw new InvalidOperationException("unsupported or gameplay-changing observation manifest");
        if (!string.Equals(manifest.Branch, RequiredBranch, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.ProfileId) ||
            !ulong.TryParse(manifest.Fingerprint.SteamBuildId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong buildId) ||
            buildId == 0 || !IsSha256(manifest.Fingerprint.Sts2Sha256) ||
            !Guid.TryParse(manifest.Fingerprint.ModuleMvid, out _) ||
            string.IsNullOrWhiteSpace(manifest.Fingerprint.BaseLibVersion) ||
            !IsSha256(manifest.Fingerprint.BaseLibManifestSha256))
        {
            throw new InvalidOperationException("observation manifest lacks an exact build fingerprint");
        }

        RuntimeObservationPolicySpec policy = manifest.Policy;
        if (policy.DefaultEnabled || !policy.ReadOnlyOnly || !policy.ExactFingerprintRequired ||
            !policy.FailClosedOnTargetMismatch || !policy.UnpatchOnReset ||
            policy.CaptureArgumentValues || policy.CaptureAbsolutePaths ||
            policy.MinimumMaxEvents < 1 || policy.MaximumMaxEvents < policy.MinimumMaxEvents ||
            policy.DefaultMaxEvents < policy.MinimumMaxEvents || policy.DefaultMaxEvents > policy.MaximumMaxEvents ||
            policy.MaximumStackFrames is < 0 or > 32 ||
            !IsSafeRelativeName(policy.OptInFileName) || !IsSafeRelativeName(policy.OutputDirectoryName))
        {
            throw new InvalidOperationException("observation safety policy is incomplete");
        }

        HashSet<string> bindingIds = manifest.RequiredBindingIds.ToHashSet(StringComparer.Ordinal);
        if (bindingIds.Count != manifest.RequiredBindingIds.Count || bindingIds.Count != 12)
            throw new InvalidOperationException("observation binding IDs must contain twelve unique review surfaces");
        if (manifest.ActiveCardModelTypes.Count != 10 ||
            manifest.ActiveCardModelTypes.Distinct(StringComparer.Ordinal).Count() != 10 ||
            manifest.ActiveCardModelTypes.Any(type => !type.StartsWith("MegaCrit.Sts2.Core.Models.Cards.", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("observation active-card scope is invalid");
        }

        HashSet<string> targetIds = [];
        HashSet<string> coveredBindings = [];
        foreach (RuntimeObservationTargetSpec target in manifest.Targets)
        {
            if (!TargetIdPattern().IsMatch(target.Id) || !targetIds.Add(target.Id) ||
                string.IsNullOrWhiteSpace(target.Purpose) || string.IsNullOrWhiteSpace(target.DeclaringType) ||
                string.IsNullOrWhiteSpace(target.MethodSignature) || !IsMethodDefinitionToken(target.MetadataToken) ||
                target.BindingIds.Count == 0 || target.BindingIds.Distinct(StringComparer.Ordinal).Count() != target.BindingIds.Count ||
                target.BindingIds.Any(id => !bindingIds.Contains(id)))
            {
                throw new InvalidOperationException($"invalid observation target: {target.Id}");
            }
            foreach (string bindingId in target.BindingIds)
                coveredBindings.Add(bindingId);
        }
        if (!coveredBindings.SetEquals(bindingIds))
            throw new InvalidOperationException("observation targets do not cover all twelve binding-review surfaces");
    }

    internal static bool IsMethodDefinitionToken(string value) =>
        value.Length == 10 && value.StartsWith("0x06", StringComparison.OrdinalIgnoreCase) &&
        uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint token) &&
        (token & 0xFF000000u) == 0x06000000u && (token & 0x00FFFFFFu) != 0;

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool IsSafeRelativeName(string value) =>
        !string.IsNullOrWhiteSpace(value) && value == Path.GetFileName(value) &&
        value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static RuntimeObservationGateResult Disabled(string reason) => new(false, 0, 0, [reason]);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SessionLabelPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{2,95}$", RegexOptions.CultureInvariant)]
    private static partial Regex TargetIdPattern();
}
