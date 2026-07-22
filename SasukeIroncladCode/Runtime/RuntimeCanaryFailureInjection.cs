namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public static class RuntimeCanaryFailureScenarios
{
    public const string None = "none";
    public const string MissingTimeline = "missing_timeline";
    public const string ForcedPlaybackFailure = "forced_playback_failure";
    public const string AnchorInvalidation = "anchor_invalidation";

    private static readonly HashSet<string> Supported =
    [
        None,
        MissingTimeline,
        ForcedPlaybackFailure,
        AnchorInvalidation,
    ];

    public static bool IsSupported(string? scenario) =>
        scenario is not null && Supported.Contains(scenario);

    public static bool IsFailure(string? scenario) =>
        IsSupported(scenario) && !string.Equals(scenario, None, StringComparison.Ordinal);
}

public sealed record RuntimeCanaryFailureInjectionSnapshot(
    bool Requested,
    string Scenario,
    string TargetCardId,
    bool Armed,
    bool Triggered,
    int TriggerCount,
    string? TriggerStage,
    bool OriginalVisualHiddenAtTrigger,
    bool RecoveryConfirmed,
    string LastTransition,
    IReadOnlyList<string> Reasons
);

/// <summary>
/// Tracks one explicitly acknowledged, one-shot presentation failure. It never
/// changes gameplay data and cannot select a failure outside the checked-in set.
/// </summary>
public sealed class RuntimeCanaryFailureInjectionController
{
    private readonly object _sync = new();
    private readonly List<string> _reasons = [];
    private bool _triggered;
    private bool _recoveryConfirmed;
    private string? _triggerStage;
    private bool _originalVisualHiddenAtTrigger;
    private string _lastTransition;

    public RuntimeCanaryFailureInjectionController(string scenario, string targetCardId)
    {
        Scenario = RuntimeCanaryFailureScenarios.IsSupported(scenario)
            ? scenario
            : RuntimeCanaryFailureScenarios.None;
        TargetCardId = RuntimeCanaryFailureScenarios.IsFailure(Scenario)
            ? targetCardId
            : string.Empty;
        _lastTransition = Requested ? "failure_injection_armed" : "failure_injection_not_requested";
        if (Requested)
            _reasons.Add($"One-shot presentation failure armed: scenario={Scenario}; card={TargetCardId}.");
    }

    public string Scenario { get; }
    public string TargetCardId { get; }
    public bool Requested => RuntimeCanaryFailureScenarios.IsFailure(Scenario);

    public bool Matches(string scenario, string cardId) =>
        Requested &&
        string.Equals(Scenario, scenario, StringComparison.Ordinal) &&
        string.Equals(TargetCardId, cardId, StringComparison.Ordinal);

    public bool TryTrigger(
        string scenario,
        string cardId,
        string stage,
        bool originalVisualHiddenAtTrigger)
    {
        if (!Matches(scenario, cardId) || string.IsNullOrWhiteSpace(stage))
            return false;

        lock (_sync)
        {
            if (_triggered)
                return false;
            _triggered = true;
            _triggerStage = stage;
            _originalVisualHiddenAtTrigger = originalVisualHiddenAtTrigger;
            _lastTransition = $"failure_injection_triggered:{Scenario}:{stage}";
            _reasons.Add(
                $"Failure injection triggered once at {stage}; original_visual_hidden={originalVisualHiddenAtTrigger}.");
            return true;
        }
    }

    public bool ConfirmRecovery(bool recovered, string transition)
    {
        if (!Requested || string.IsNullOrWhiteSpace(transition))
            return false;

        lock (_sync)
        {
            if (!_triggered)
                return false;
            if (!recovered)
            {
                _lastTransition = $"failure_recovery_pending:{transition}";
                _reasons.Add($"Failure recovery was not yet confirmed at {transition}.");
                return false;
            }
            if (_recoveryConfirmed)
                return false;

            _recoveryConfirmed = true;
            _lastTransition = $"failure_recovery_confirmed:{transition}";
            _reasons.Add($"Original presentation recovery confirmed at {transition}.");
            return true;
        }
    }

    public RuntimeCanaryFailureInjectionSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new RuntimeCanaryFailureInjectionSnapshot(
                Requested,
                Scenario,
                TargetCardId,
                Armed: Requested && !_triggered,
                Triggered: _triggered,
                TriggerCount: _triggered ? 1 : 0,
                TriggerStage: _triggerStage,
                OriginalVisualHiddenAtTrigger: _originalVisualHiddenAtTrigger,
                RecoveryConfirmed: _recoveryConfirmed,
                LastTransition: _lastTransition,
                Reasons: _reasons.TakeLast(16).ToArray());
        }
    }
}
