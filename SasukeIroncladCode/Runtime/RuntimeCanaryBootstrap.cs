using System.Reflection;
using SasukeIronclad.SasukeIroncladCode.Adapters;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeCanaryBootstrapResult(
    bool Enabled,
    bool AnimationsEnabled,
    bool TitlesEnabled,
    IReadOnlyList<string> PatchedBindingIds,
    IReadOnlyList<string> Reasons,
    string? SessionId = null,
    string? EventFileName = null,
    string FailureInjectionScenario = RuntimeCanaryFailureScenarios.None,
    string? FailureInjectionCardId = null,
    string? RequestedSessionLabel = null,
    bool PatchInstallAttempted = false,
    RuntimeCanaryStartupFailureInjectionSnapshot? StartupFailureInjection = null
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
            return Disabled(optIn, ["Runtime canary Mod assembly path is unavailable."]);

        SafeReset(patcher);
        RuntimeBuildFingerprintCollectionResult collection;
        try
        {
            collection = fingerprintProvider.Collect();
        }
        catch (Exception exception)
        {
            return Disabled(optIn, [$"Runtime canary fingerprint provider failed safely: {exception.GetType().Name}."]);
        }
        if (!collection.Success || collection.Fingerprint is null)
            return Disabled(optIn, collection.Reasons);

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
            return Disabled(optIn, gate.Reasons);

        RuntimeCanaryStartupFailureInjectionController startupFailureInjection = new(
            optIn.StartupFailureInjectionScenario,
            optIn.StartupFailureInjectionBindingId);
        RuntimeCanaryResolutionResult resolution;
        try
        {
            resolution = RuntimeCanaryTargetResolver.Resolve(
                gameAssembly,
                observationManifest,
                gate.Decisions,
                startupFailureInjection);
        }
        catch (Exception exception)
        {
            return Disabled(
                optIn,
                [$"Runtime canary target resolution failed safely: {exception.GetType().Name}."],
                startupFailureInjection.Snapshot());
        }
        RuntimeCanaryStartupFailureInjectionSnapshot startupFailureSnapshot =
            startupFailureInjection.Snapshot();
        if (startupFailureSnapshot.Requested)
        {
            bool expectedFailureObserved =
                !resolution.Success &&
                startupFailureSnapshot.Triggered &&
                startupFailureSnapshot.TriggerCount == 1 &&
                startupFailureSnapshot.BaselineMatchConfirmed &&
                resolution.Reasons.Count == 1 &&
                resolution.Reasons[0].Contains(
                    RuntimeCanaryStartupFailureScenarios.ReasonMarker,
                    StringComparison.Ordinal);
            if (expectedFailureObserved)
                return Disabled(optIn, resolution.Reasons, startupFailureSnapshot);

            List<string> invalidReasons = resolution.Reasons.ToList();
            invalidReasons.Add(
                "Startup failure injection did not produce exactly one reviewed method-signature mismatch; patch installation remains disabled.");
            return Disabled(optIn, invalidReasons, startupFailureSnapshot);
        }
        if (!resolution.Success)
            return Disabled(optIn, resolution.Reasons, startupFailureSnapshot);

        RuntimeCanarySession? session = null;
        bool patchInstallAttempted = false;
        try
        {
            session = new RuntimeCanarySession(scope, optIn, modAssemblyPath);
            string? sessionId = session.SessionId;
            string? eventFileName = session.EventFileName;
            patchInstallAttempted = true;
            patcher.Install(session, resolution.Targets);
            session = null;
            List<string> reasons = gate.Reasons.Concat(resolution.Reasons).ToList();
            reasons.Add("Canary adapters are postfix-only and preserve the already-completed original game method.");
            if (!string.IsNullOrWhiteSpace(eventFileName))
                reasons.Add($"Privacy-safe presentation event journal created: {eventFileName}.");
            else
                reasons.Add("Presentation event journal was unavailable; the canary remains fail-safe and continues without diagnostics.");
            if (gate.AnimationsEnabled)
            {
                reasons.Add("The Sasuke overlay remains hidden until a unique local-player combat visual anchor is resolved from a local card-play callback.");
                if (optIn.HideOriginalVisual)
                {
                    reasons.Add("Replacement mode hides only the exact reviewed local Ironclad NCreatureVisuals node after a Sasuke timeline starts successfully.");
                    reasons.Add("Any playback fallback, anchor loss, combat teardown or Mod disposal requests immediate restoration of the captured original visibility.");
                }
                else
                {
                    reasons.Add("Overlay mode leaves the original Ironclad visual visible and unchanged.");
                }
            }
            if (RuntimeCanaryFailureScenarios.IsFailure(optIn.FailureInjectionScenario))
            {
                reasons.Add(
                    $"One-shot failure injection is armed for {optIn.FailureInjectionScenario} on {optIn.FailureInjectionCardId}; " +
                    "the original presentation must be restored and the current combat must fail closed.");
            }
            reasons.Add("Demon Form animation, form removal and character-state presentation remain disabled pending targeted evidence.");
            return new(
                true,
                gate.AnimationsEnabled,
                gate.TitlesEnabled,
                patcher.PatchedBindingIds,
                reasons,
                sessionId,
                eventFileName,
                optIn.FailureInjectionScenario,
                RuntimeCanaryFailureScenarios.IsFailure(optIn.FailureInjectionScenario)
                    ? optIn.FailureInjectionCardId
                    : null,
                RequestedSessionLabel: optIn.SessionLabel,
                PatchInstallAttempted: patchInstallAttempted,
                StartupFailureInjection: startupFailureSnapshot);
        }
        catch (Exception exception)
        {
            SafeReset(patcher);
            try { session?.Dispose(); } catch { }
            return Disabled(
                optIn,
                [$"Runtime canary installation failed closed: {exception.GetType().Name}."],
                startupFailureSnapshot,
                patchInstallAttempted);
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

    private static RuntimeCanaryBootstrapResult Disabled(
        RuntimeCanaryOptIn optIn,
        IReadOnlyList<string> reasons,
        RuntimeCanaryStartupFailureInjectionSnapshot? startupFailureInjection = null,
        bool patchInstallAttempted = false) =>
        new(
            false,
            false,
            false,
            [],
            reasons,
            RequestedSessionLabel: optIn.SessionLabel,
            PatchInstallAttempted: patchInstallAttempted,
            StartupFailureInjection: startupFailureInjection ?? new RuntimeCanaryStartupFailureInjectionController(
                optIn.StartupFailureInjectionScenario,
                optIn.StartupFailureInjectionBindingId).Snapshot());
}
