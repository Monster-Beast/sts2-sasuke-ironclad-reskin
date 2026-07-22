using SasukeIronclad.SasukeIroncladCode.Adapters;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

/// <summary>
/// Coordinates the explicitly acknowledged one-shot fault canary without
/// modifying game resources or gameplay state. The Godot host supplies the
/// synthetic failure and the replacement controller confirms restoration.
/// </summary>
public static class RuntimeCanaryFailureDiagnostics
{
    private static readonly object Sync = new();

    private static RuntimeCanaryFailureInjectionController? _controller;
    private static RuntimeCanaryOptIn? _optIn;
    private static string? _modAssemblyPath;
    private static WeakReference<GodotVisualSceneHost>? _host;
    private static RuntimeOriginalVisualReplacementSnapshot? _replacementSnapshot;
    private static bool _originalVisualHiddenSignal;
    private static bool _replacementDisabledForCombat;

    public static void Configure(
        string modAssemblyPath,
        RuntimeCanaryOptIn optIn,
        GodotVisualSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(optIn);
        ArgumentNullException.ThrowIfNull(host);
        lock (Sync)
        {
            _controller = new RuntimeCanaryFailureInjectionController(
                optIn.FailureInjectionScenario,
                optIn.FailureInjectionCardId);
            _optIn = optIn;
            _modAssemblyPath = modAssemblyPath;
            _host = new WeakReference<GodotVisualSceneHost>(host);
            _replacementSnapshot = null;
            _originalVisualHiddenSignal = false;
            _replacementDisabledForCombat = false;
            WriteStatusLocked();
        }
    }

    public static bool TryTriggerMissingTimeline(string cardId, bool originalVisualHidden)
    {
        lock (Sync)
        {
            if (_controller?.TryTrigger(
                    RuntimeCanaryFailureScenarios.MissingTimeline,
                    cardId,
                    "can_play",
                    originalVisualHidden) != true)
            {
                return false;
            }
            WriteStatusLocked();
            return true;
        }
    }

    public static bool ShouldArmPostHideFailure(string cardId)
    {
        lock (Sync)
        {
            return _controller is not null &&
                   (_controller.Matches(RuntimeCanaryFailureScenarios.ForcedPlaybackFailure, cardId) ||
                    _controller.Matches(RuntimeCanaryFailureScenarios.AnchorInvalidation, cardId));
        }
    }

    public static void NotifyOriginalVisualHidden()
    {
        lock (Sync)
        {
            if (_controller?.Requested != true)
                return;
            _originalVisualHiddenSignal = true;
            WriteStatusLocked();
        }
    }

    public static bool TryTakePostHideFailure(string cardId, out string scenario)
    {
        lock (Sync)
        {
            scenario = RuntimeCanaryFailureScenarios.None;
            if (!_originalVisualHiddenSignal || _controller is null)
                return false;

            string candidate = _controller.Scenario;
            if (candidate is not (RuntimeCanaryFailureScenarios.ForcedPlaybackFailure or
                                  RuntimeCanaryFailureScenarios.AnchorInvalidation) ||
                !_controller.TryTrigger(candidate, cardId, "after_replacement_hidden", originalVisualHiddenAtTrigger: true))
            {
                return false;
            }

            _originalVisualHiddenSignal = false;
            scenario = candidate;
            WriteStatusLocked();
            return true;
        }
    }

    public static void NotifyReplacementRestored(RuntimeOriginalVisualReplacementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (Sync)
        {
            _replacementSnapshot = snapshot;
            if (_controller?.Snapshot().Triggered == true)
            {
                _replacementDisabledForCombat = true;
                _controller.ConfirmRecovery(
                    recovered: !snapshot.Active,
                    transition: snapshot.LastTransition);
            }
            WriteStatusLocked();
        }
    }

    public static void RefreshStatus()
    {
        lock (Sync)
            WriteStatusLocked();
    }

    public static RuntimeCanaryFailureInjectionSnapshot? Snapshot()
    {
        lock (Sync)
            return _controller?.Snapshot();
    }

    private static void WriteStatusLocked()
    {
        if (_controller?.Requested != true || _optIn is null || string.IsNullOrWhiteSpace(_modAssemblyPath))
            return;

        GodotVisualSceneHost? host = null;
        _host?.TryGetTarget(out host);
        RuntimeCanaryLocalFiles.WriteFailureStatus(
            _modAssemblyPath,
            _optIn,
            _controller.Snapshot(),
            _replacementSnapshot,
            host,
            _replacementDisabledForCombat);
    }
}
