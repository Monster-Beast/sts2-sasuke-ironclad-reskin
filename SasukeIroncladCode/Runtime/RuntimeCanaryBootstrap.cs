using System.Reflection;
using SasukeIronclad.SasukeIroncladCode.Adapters;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeCanaryBootstrapResult(
    bool Enabled,
    bool AnimationsEnabled,
    bool TitlesEnabled,
    IReadOnlyList<string> PatchedBindingIds,
    IReadOnlyList<string> Reasons
);

public static class RuntimeCanaryBootstrap
{
    public static RuntimeCanaryBootstrapResult Start(
        RuntimeCanaryReviewMap review,
        RuntimeObservationManifestMap observationManifest,
        GameIntegrationContractMap productionContract,
        GameIntegrationProfile pendingProfile,
        CurrentBetaCardScopeMap scope,
        RuntimeCanaryOptIn optIn,
        IRuntimeBuildFingerprintProvider fingerprintProvider,
        Assembly gameAssembly,
        IRuntimeCanaryPatcher patcher,
        string modAssemblyPath,
        bool observationEnabled)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(observationManifest);
        ArgumentNullException.ThrowIfNull(productionContract);
        ArgumentNullException.ThrowIfNull(pendingProfile);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(optIn);
        ArgumentNullException.ThrowIfNull(fingerprintProvider);
        ArgumentNullException.ThrowIfNull(gameAssembly);
        ArgumentNullException.ThrowIfNull(patcher);
        if (string.IsNullOrWhiteSpace(modAssemblyPath))
            return Disabled("Runtime canary Mod assembly path is unavailable.");

        SafeReset(patcher);
        RuntimeBuildFingerprintCollectionResult collection;
        try
        {
            collection = fingerprintProvider.Collect();
        }
        catch (Exception exception)
        {
            return Disabled($"Runtime canary fingerprint provider failed safely: {exception.GetType().Name}.");
        }
        if (!collection.Success || collection.Fingerprint is null)
            return new(false, false, false, [], collection.Reasons);

        RuntimeCanaryGateResult gate = RuntimeCanaryGate.Evaluate(
            review,
            observationManifest,
            productionContract,
            pendingProfile,
            scope,
            optIn,
            collection.Fingerprint,
            observationEnabled);
        if (!gate.Enabled)
            return new(false, false, false, [], gate.Reasons);

        RuntimeCanaryResolutionResult resolution;
        try
        {
            resolution = RuntimeCanaryTargetResolver.Resolve(gameAssembly, observationManifest, gate.Decisions);
        }
        catch (Exception exception)
        {
            return Disabled($"Runtime canary target resolution failed safely: {exception.GetType().Name}.");
        }
        if (!resolution.Success)
            return new(false, false, false, [], resolution.Reasons);

        RuntimeCanarySession? session = null;
        try
        {
            session = new RuntimeCanarySession(scope, optIn, modAssemblyPath);
            patcher.Install(session, resolution.Targets);
            session = null;
            List<string> reasons = gate.Reasons.Concat(resolution.Reasons).ToList();
            reasons.Add("Canary adapters are postfix-only and preserve the already-completed original game method.");
            if (gate.AnimationsEnabled)
            {
                reasons.Add("The Sasuke overlay remains hidden until a unique local-player combat visual anchor is resolved from a local card-play callback.");
                reasons.Add("The original Ironclad visual remains visible during this anchor canary and is never hidden or modified.");
            }
            reasons.Add("Demon Form animation, form removal and character-state presentation remain disabled pending targeted evidence.");
            return new(
                true,
                gate.AnimationsEnabled,
                gate.TitlesEnabled,
                patcher.PatchedBindingIds,
                reasons);
        }
        catch (Exception exception)
        {
            SafeReset(patcher);
            try { session?.Dispose(); } catch { }
            return Disabled($"Runtime canary installation failed closed: {exception.GetType().Name}.");
        }
    }

    private static void SafeReset(IRuntimeCanaryPatcher patcher)
    {
        try { patcher.Reset(); }
        catch
        {
            // Canary reset must never affect game startup or shutdown.
        }
    }

    private static RuntimeCanaryBootstrapResult Disabled(string reason) =>
        new(false, false, false, [], [reason]);
}