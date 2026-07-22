using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeCanaryGateResult(
    bool Enabled,
    bool AnimationsEnabled,
    bool TitlesEnabled,
    IReadOnlyList<RuntimeCanaryBindingDecision> Decisions,
    IReadOnlyList<string> Reasons
);

public static class RuntimeCanaryGate
{
    public const string RequiredReplacementAcknowledgement =
        "public-beta-24251656-local-ironclad-replacement";
    public const string RequiredFailureInjectionAcknowledgement =
        "public-beta-24251656-local-visual-failure-injection";

    private const string RequiredMode = "local_visual_only";
    private const string RequiredBranch = "public-beta";
    private const string DemonFormCardId = "Demon Form";
    private static readonly HashSet<string> RequiredVisualBindings =
    [
        "card_visual_request", "original_impact", "state_removed", "form_removed",
        "combat_ended", "character_state"
    ];
    private static readonly HashSet<string> RequiredTitleBindings =
    [
        "card_art", "hand", "deck_list", "reward", "compendium", "tooltip"
    ];
    private static readonly HashSet<string> BlockedBindings = ["form_removed", "character_state"];

    public static RuntimeCanaryGateResult Evaluate(
        RuntimeCanaryReviewMap review,
        RuntimeObservationManifestMap observationManifest,
        GameIntegrationContractMap productionContract,
        GameIntegrationProfile pendingProfile,
        CurrentBetaCardScopeMap scope,
        RuntimeCanaryOptIn optIn,
        RuntimeBuildFingerprint runtime,
        bool observationEnabled)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(observationManifest);
        ArgumentNullException.ThrowIfNull(productionContract);
        ArgumentNullException.ThrowIfNull(pendingProfile);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(optIn);
        ArgumentNullException.ThrowIfNull(runtime);

        List<string> reasons = [];
        if (observationEnabled)
            return Disabled("Runtime canary and runtime observation cannot be enabled in the same game process.");

        if (review.SchemaVersion != 1 || review.Status != "partial_manual_review" ||
            review.ProfileId != observationManifest.ProfileId || review.ProfileId != pendingProfile.Id)
        {
            return Disabled("Runtime canary review identity is missing or unsupported.");
        }
        RuntimeCanaryReviewPolicy reviewPolicy = review.Policy;
        if (!reviewPolicy.AutoSelectionForbidden || !reviewPolicy.ProductionContractRemainsDisabled ||
            !reviewPolicy.RuntimeBindingsRemainDisabled || !reviewPolicy.CanaryRequiresExplicitLocalOptIn ||
            !reviewPolicy.FallbackToOriginalRequired || !reviewPolicy.MultiplayerLocalVisualOnlyRequired)
        {
            return Disabled("Runtime canary review safety policy is incomplete.");
        }
        if (review.Readiness.ProductionProfileReady || review.Readiness.TitleCanaryCandidateCount != 6 ||
            review.Readiness.VisualCanaryCandidateCount != 4 || review.Readiness.BlockedBindingCount != 2)
        {
            return Disabled("Runtime canary review readiness does not match the approved partial scope.");
        }

        if (productionContract.Status != "pending_local_audit" || productionContract.Profiles.Count != 0)
            return Disabled("Production integration must remain disabled while the exact-build canary is active.");
        if (pendingProfile.Status != "pending_review" ||
            pendingProfile.VisualBindings.Any(binding => binding.Status != "pending_review" || !string.IsNullOrWhiteSpace(binding.MetadataToken)) ||
            pendingProfile.TitleBindings.Any(binding => binding.Status != "pending_review" || !string.IsNullOrWhiteSpace(binding.MetadataToken)))
        {
            return Disabled("The checked-in exact-build profile was promoted or populated outside the canary review.");
        }

        if (scope.SchemaVersion != 1 || scope.GameplayChanges || scope.Status != "pending_review" ||
            scope.Branch != RequiredBranch || scope.ActiveCards.Count != 10 ||
            scope.ActiveCards.Select(card => card.ModelType).Distinct(StringComparer.Ordinal).Count() != 10)
        {
            return Disabled("Current public-beta card scope is invalid for the runtime canary.");
        }

        if (!optIn.Enabled || optIn.SchemaVersion != 1 || optIn.Mode != RequiredMode ||
            (!optIn.EnableAnimations && !optIn.EnableTitles))
        {
            return Disabled("Runtime canary requires an explicit schema-1 local_visual_only marker with at least one presentation layer enabled.");
        }
        if (!IsValidSessionLabel(optIn.SessionLabel))
            return Disabled("Runtime canary session_label must contain 1-48 ASCII letters, digits, dots, underscores or hyphens and start with a letter or digit.");
        if (optIn.EnableAnimations &&
            (!optIn.AnchorToLocalPlayer ||
             !float.IsFinite(optIn.AnchorScale) || optIn.AnchorScale is < 0.25f or > 3.0f ||
             !float.IsFinite(optIn.AnchorOffsetX) || Math.Abs(optIn.AnchorOffsetX) > 1000.0f ||
             !float.IsFinite(optIn.AnchorOffsetY) || Math.Abs(optIn.AnchorOffsetY) > 1000.0f))
        {
            return Disabled("Animation canary requires bounded local-player anchoring, scale and offsets.");
        }
        if (optIn.HideOriginalVisual)
        {
            if (!optIn.EnableAnimations || !optIn.AnchorToLocalPlayer)
                return Disabled("Original visual replacement requires the anchored animation layer.");
            if (!string.Equals(
                    optIn.ReplacementAcknowledgement,
                    RequiredReplacementAcknowledgement,
                    StringComparison.Ordinal))
            {
                return Disabled("Original visual replacement lacks the exact-build acknowledgement written by the explicit replacement switch.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(optIn.ReplacementAcknowledgement))
        {
            return Disabled("Replacement acknowledgement is present while original visual replacement is disabled.");
        }

        if (!RuntimeCanaryFailureScenarios.IsSupported(optIn.FailureInjectionScenario))
            return Disabled("Runtime canary failure_injection_scenario is unsupported.");
        bool failureRequested = RuntimeCanaryFailureScenarios.IsFailure(optIn.FailureInjectionScenario);
        if (failureRequested)
        {
            if (!optIn.EnableAnimations || !optIn.AnchorToLocalPlayer || !optIn.HideOriginalVisual)
                return Disabled("Failure injection requires anchored animation replacement so original-visual recovery can be verified.");
            if (!optIn.FailureInjectionOnce)
                return Disabled("Failure injection must remain one-shot for the current process.");
            if (!string.Equals(
                    optIn.FailureInjectionAcknowledgement,
                    RequiredFailureInjectionAcknowledgement,
                    StringComparison.Ordinal))
            {
                return Disabled("Failure injection lacks the exact-build acknowledgement written by the explicit failure switch.");
            }
            if (string.IsNullOrWhiteSpace(optIn.FailureInjectionCardId) ||
                string.Equals(optIn.FailureInjectionCardId, DemonFormCardId, StringComparison.Ordinal) ||
                !scope.ActiveCards.Any(card =>
                    string.Equals(card.CardId, optIn.FailureInjectionCardId, StringComparison.Ordinal)))
            {
                return Disabled("Failure injection target must be a reviewed active card other than Demon Form.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(optIn.FailureInjectionCardId) ||
                 !string.IsNullOrWhiteSpace(optIn.FailureInjectionAcknowledgement))
        {
            return Disabled("Failure injection fields are populated while failure_injection_scenario is none.");
        }

        if (!string.Equals(optIn.ExpectedBranch, runtime.Branch, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(optIn.ExpectedBuildId, runtime.SteamBuildId, StringComparison.Ordinal))
        {
            return Disabled("Runtime canary marker does not acknowledge the exact loaded branch and Steam build.");
        }

        if (!Matches(review.Runtime, runtime) || !Matches(observationManifest, runtime) || !Matches(pendingProfile, runtime) ||
            !string.Equals(scope.Branch, runtime.Branch, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(scope.SteamBuildId, runtime.SteamBuildId, StringComparison.Ordinal))
        {
            return Disabled("Runtime canary evidence does not match the exact loaded game fingerprint.");
        }

        HashSet<string> requiredBindings = RequiredVisualBindings.Concat(RequiredTitleBindings).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, RuntimeCanaryBindingDecision> decisions;
        try
        {
            decisions = review.Decisions.ToDictionary(decision => decision.BindingId, StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            return Disabled("Runtime canary review contains duplicate binding decisions.");
        }
        if (!decisions.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(requiredBindings))
            return Disabled("Runtime canary review does not cover all twelve integration surfaces.");

        foreach (string blocked in BlockedBindings)
        {
            RuntimeCanaryBindingDecision decision = decisions[blocked];
            if (decision.Status != "needs_targeted_observation" ||
                !string.IsNullOrWhiteSpace(decision.SelectedTargetId) ||
                !string.IsNullOrWhiteSpace(decision.MetadataToken))
            {
                return Disabled($"Blocked runtime binding {blocked} was selected without targeted evidence.");
            }
        }

        List<RuntimeCanaryBindingDecision> selected = [];
        foreach (RuntimeCanaryBindingDecision decision in review.Decisions)
        {
            if (decision.Status != "approved_for_canary")
                continue;
            if (!decision.ObservedInAllSessions || string.IsNullOrWhiteSpace(decision.SelectedTargetId) ||
                !RuntimeObservationGate.IsMethodDefinitionToken(decision.MetadataToken))
            {
                return Disabled($"Canary binding {decision.BindingId} lacks exact two-run MethodDef evidence.");
            }
            bool visual = RequiredVisualBindings.Contains(decision.BindingId);
            bool title = RequiredTitleBindings.Contains(decision.BindingId);
            if ((visual && optIn.EnableAnimations) || (title && optIn.EnableTitles))
                selected.Add(decision);
        }

        int expectedCount = (optIn.EnableAnimations ? 4 : 0) + (optIn.EnableTitles ? 6 : 0);
        if (selected.Count != expectedCount)
            return Disabled("Runtime canary selected binding count does not match the approved partial review.");

        reasons.Add(
            $"Exact-build runtime canary enabled for {runtime.Branch} build {runtime.SteamBuildId}; " +
            $"session={optIn.SessionLabel}; animations={optIn.EnableAnimations}; titles={optIn.EnableTitles}; " +
            $"low_flash={optIn.LowFlash}; fast_mode={optIn.FastMode}."
        );
        if (optIn.EnableAnimations)
        {
            reasons.Add(
                $"Local-player anchor required; scale={optIn.AnchorScale:R}; " +
                $"offset=({optIn.AnchorOffsetX:R},{optIn.AnchorOffsetY:R}); replacement_requested={optIn.HideOriginalVisual}."
            );
        }
        if (optIn.HideOriginalVisual)
        {
            reasons.Add(
                "Original visual replacement is explicit, exact-build-only and restricted to the uniquely resolved local Ironclad NCreatureVisuals node."
            );
            reasons.Add(
                "The original visibility value must be captured and restored on playback fallback, anchor loss, combat end and Mod disposal."
            );
        }
        if (failureRequested)
        {
            reasons.Add(
                $"One-shot presentation failure injection armed: scenario={optIn.FailureInjectionScenario}; " +
                $"target_card={optIn.FailureInjectionCardId}."
            );
            reasons.Add("Failure injection is diagnostic-only and may not mutate card rules, combat state or game resources on disk.");
        }
        reasons.Add("Only approved_for_canary postfix adapters are eligible; form_removed and character_state remain disabled.");
        reasons.Add("Original game methods, arguments, return values, card IDs and gameplay state remain untouched.");
        return new(true, optIn.EnableAnimations, optIn.EnableTitles, selected, reasons);
    }

    private static bool IsValidSessionLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 48 || !char.IsAsciiLetterOrDigit(value[0]))
            return false;
        foreach (char character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'))
                return false;
        }
        return true;
    }

    private static bool Matches(RuntimeCanaryFingerprintSpec expected, RuntimeBuildFingerprint runtime) =>
        string.Equals(expected.Branch, runtime.Branch, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.SteamBuildId, runtime.SteamBuildId, StringComparison.Ordinal) &&
        string.Equals(expected.Sts2Sha256, runtime.Sts2Sha256, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.ModuleMvid, runtime.ModuleMvid, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.BaseLibVersion, runtime.BaseLibVersion, StringComparison.Ordinal);

    private static bool Matches(RuntimeObservationManifestMap expected, RuntimeBuildFingerprint runtime) =>
        string.Equals(expected.Branch, runtime.Branch, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.Fingerprint.SteamBuildId, runtime.SteamBuildId, StringComparison.Ordinal) &&
        string.Equals(expected.Fingerprint.Sts2Sha256, runtime.Sts2Sha256, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.Fingerprint.ModuleMvid, runtime.ModuleMvid, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.Fingerprint.BaseLibVersion, runtime.BaseLibVersion, StringComparison.Ordinal) &&
        string.Equals(expected.Fingerprint.BaseLibManifestSha256, runtime.BaseLibManifestSha256, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(GameIntegrationProfile expected, RuntimeBuildFingerprint runtime) =>
        string.Equals(expected.Branch, runtime.Branch, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.Fingerprint.SteamBuildId, runtime.SteamBuildId, StringComparison.Ordinal) &&
        string.Equals(expected.Fingerprint.Sts2Sha256, runtime.Sts2Sha256, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.Fingerprint.ModuleMvid, runtime.ModuleMvid, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.Fingerprint.BaseLibVersion, runtime.BaseLibVersion, StringComparison.Ordinal);

    private static RuntimeCanaryGateResult Disabled(string reason) => new(false, false, false, [], [reason]);
}
