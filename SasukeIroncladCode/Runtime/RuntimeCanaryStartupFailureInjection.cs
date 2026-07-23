namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public static class RuntimeCanaryStartupFailureScenarios
{
    public const string None = "none";
    public const string MethodSignatureMismatch = "method_signature_mismatch";
    public const string TargetBindingId = "card_visual_request";
    public const string TriggerStage = "method_signature_comparison";
    public const string ReasonMarker = "fault_injection:method_signature_mismatch";

    public static bool IsSupported(string? scenario) =>
        scenario is None or MethodSignatureMismatch;

    public static bool IsFailure(string? scenario) =>
        string.Equals(scenario, MethodSignatureMismatch, StringComparison.Ordinal);
}

public sealed record RuntimeCanaryStartupFailureInjectionSnapshot(
    bool Requested,
    string Scenario,
    string? BindingId,
    bool Armed,
    bool Triggered,
    int TriggerCount,
    string? TriggerStage,
    bool BaselineMatchConfirmed
);

/// <summary>
/// Supplies one explicitly acknowledged, in-memory-only startup diagnostic.
/// It changes only a local comparison string after the real reflected method
/// signature has already matched the reviewed signature. Review data, game
/// assemblies and game resources remain untouched.
/// </summary>
public sealed class RuntimeCanaryStartupFailureInjectionController
{
    private readonly object _sync = new();
    private bool _triggered;

    public RuntimeCanaryStartupFailureInjectionController(
        string scenario,
        string targetBindingId)
    {
        Scenario = RuntimeCanaryStartupFailureScenarios.IsSupported(scenario)
            ? scenario
            : RuntimeCanaryStartupFailureScenarios.None;
        BindingId = RuntimeCanaryStartupFailureScenarios.IsFailure(Scenario)
            ? targetBindingId
            : string.Empty;
    }

    public string Scenario { get; }
    public string BindingId { get; }
    public bool Requested => RuntimeCanaryStartupFailureScenarios.IsFailure(Scenario);

    public bool TryInjectMethodSignatureMismatch(
        string bindingId,
        string baselineActualSignature,
        out string comparisonActualSignature)
    {
        ArgumentNullException.ThrowIfNull(bindingId);
        ArgumentNullException.ThrowIfNull(baselineActualSignature);
        comparisonActualSignature = baselineActualSignature;

        lock (_sync)
        {
            if (!Requested || _triggered ||
                !string.Equals(bindingId, BindingId, StringComparison.Ordinal))
            {
                return false;
            }

            _triggered = true;
            comparisonActualSignature =
                baselineActualSignature + " [fault_injection_method_signature_mismatch]";
            return true;
        }
    }

    public RuntimeCanaryStartupFailureInjectionSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new(
                Requested,
                Scenario,
                Requested ? BindingId : null,
                Requested && !_triggered,
                _triggered,
                _triggered ? 1 : 0,
                _triggered ? RuntimeCanaryStartupFailureScenarios.TriggerStage : null,
                _triggered);
        }
    }
}
