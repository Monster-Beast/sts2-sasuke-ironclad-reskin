using Godot;
using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public partial class GodotVisualSceneHost : Node2D, IVisualSceneHost, IVisualSceneHostNotifications, IRuntimeCanaryFailureSource
{
    private const string DefaultRuntimeScene = "res://SasukeIronclad/scenes/runtime/animation_director.tscn";
    private const string RuntimeAnchorExitedMeta = "sasuke_runtime_anchor_exited";

    private AnimationPlaybackHandle? _activeHandle;
    private AnimationPlaybackHandle? _pendingFailureHandle;
    private string? _pendingFailureCardId;
    private string? _lastCanPlayFailureReason;
    private Node? _runtimeRoot;
    private Node? _director;
    private Node2D? _anchor;
    private Vector2 _anchorOffset;
    private float _anchorScale = 1.0f;
    private bool _anchorInvalidationNotified;

    public event Action<AnimationPlaybackHandle>? PlaybackCompleted;
    public event Action<AnimationPlaybackHandle, string>? PlaybackFailed;
    public event Action<string>? AnchorInvalidated;

    [Export(PropertyHint.File, "*.tscn")]
    public string RuntimeScenePath { get; set; } = DefaultRuntimeScene;

    [Export(PropertyHint.Range, "0.25,1.0,0.25")]
    public float QualityScale { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.25,5.0,0.25")]
    public float ImpactTimeoutSeconds { get; set; } = 1.5f;

    public bool IsAnchorBound => HasValidAnchor();
    public bool RuntimeAnchorExitedSinceBind
    {
        get
        {
            try
            {
                return HasMeta(RuntimeAnchorExitedMeta) && GetMeta(RuntimeAnchorExitedMeta).AsBool();
            }
            catch
            {
                return false;
            }
        }
    }
    public Node2D? AnchorNode => HasValidAnchor() ? _anchor : null;
    public string? AnchorType => HasValidAnchor() ? _anchor!.GetType().FullName : null;
    public string? AnchorName => HasValidAnchor() ? _anchor!.Name.ToString() : null;
    public Vector2? AnchorGlobalPosition => HasValidAnchor() ? _anchor!.GlobalPosition : null;

    public override void _Ready()
    {
        Visible = false;
        SetProcess(true);
    }

    public void ConfigureFailureDiagnostics(string modAssemblyPath, RuntimeCanaryOptIn optIn)
    {
        ArgumentNullException.ThrowIfNull(optIn);
        if (string.IsNullOrWhiteSpace(modAssemblyPath) ||
            !RuntimeCanaryFailureScenarios.IsFailure(optIn.FailureInjectionScenario))
        {
            return;
        }

        RuntimeCanaryFailureDiagnostics.Configure(modAssemblyPath, optIn, this);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        SyncAnchorTransform();
        _ = TryProcessPendingFailureInjection();
    }

    public bool BindToAnchor(Node2D anchor, Vector2 offset, float scale)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (!GodotObject.IsInstanceValid(anchor) || !anchor.IsInsideTree() ||
            !float.IsFinite(scale) || scale is < 0.25f or > 3.0f)
        {
            ClearAnchor();
            return false;
        }

        _anchor = anchor;
        _anchorOffset = offset;
        _anchorScale = scale;
        _anchorInvalidationNotified = false;
        if (!EnsureMounted())
        {
            ClearAnchor();
            return false;
        }
        try
        {
            if (_director is null || !_director.HasMethod("bind_runtime_anchor") ||
                !_director.Call("bind_runtime_anchor", anchor).AsBool())
            {
                ClearAnchor();
                return false;
            }
            SetMeta(RuntimeAnchorExitedMeta, false);
        }
        catch
        {
            ClearAnchor();
            return false;
        }
        SyncAnchorTransform();
        return Visible;
    }

    public void ClearAnchor()
    {
        try
        {
            if (_director?.HasMethod("clear_runtime_anchor") == true)
                _director.Call("clear_runtime_anchor");
        }
        catch { }
        _anchor = null;
        _anchorInvalidationNotified = false;
        Visible = false;
        Position = Vector2.Zero;
        Rotation = 0.0f;
        Scale = Vector2.One;
        RuntimeCanaryFailureDiagnostics.RefreshStatus();
    }

    public bool CanPlay(CardAnimationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!HasValidAnchor() || !EnsureMounted() || _director is null)
            return false;

        bool originalVisualHidden = false;
        try { originalVisualHidden = _anchor is not null && !_anchor.Visible; } catch { }
        if (RuntimeCanaryFailureDiagnostics.TryTriggerMissingTimeline(
                selection.CardId,
                originalVisualHidden))
        {
            _lastCanPlayFailureReason = "fault_injection:missing_timeline";
            return false;
        }

        try
        {
            return _director.Call("has_timeline", selection.AnimationId).AsBool();
        }
        catch
        {
            return false;
        }
    }

    public string? ConsumeCanPlayFailureReason()
    {
        string? reason = _lastCanPlayFailureReason;
        _lastCanPlayFailureReason = null;
        return reason;
    }

    public AnimationPlaybackHandle Play(CardAnimationSelection selection, AnimationContext context)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(context);
        if (!CanPlay(selection) || _director is null)
            throw new InvalidOperationException($"Timeline is unavailable: {selection.AnimationId}");

        ReleaseActiveHandle(cancelDirector: true);
        AnimationPlaybackHandle handle = new(selection.CardId, selection.AnimationId);
        Godot.Collections.Dictionary runtimeContext = new()
        {
            ["low_flash"] = selection.LowFlashMode,
            ["fast_mode"] = selection.FastMode,
            ["quality_scale"] = QualityScale,
            ["external_impact_sync"] = selection.RequiresOriginalImpactSync,
            ["impact_timeout_seconds"] = ImpactTimeoutSeconds,
            ["final_damage"] = context.FinalDamage,
            ["hit_count"] = context.HitCount,
            ["target_count"] = context.TargetCount,
            ["energy_spent"] = context.EnergySpent,
            ["strength"] = context.Strength,
            ["exhausted_card_count"] = context.ExhaustedCardCount,
            ["lethal"] = context.IsLethal
        };

        _activeHandle = handle;
        try
        {
            _director.CallDeferred(
                "play_timeline",
                selection.AnimationId,
                ToVariantName(selection.Variant),
                runtimeContext
            );
            if (RuntimeCanaryFailureDiagnostics.ShouldArmPostHideFailure(selection.CardId))
            {
                _pendingFailureHandle = handle;
                _pendingFailureCardId = selection.CardId;
            }
            return handle;
        }
        catch
        {
            if (_activeHandle?.Id == handle.Id)
                _activeHandle = null;
            ClearPendingFailure(handle);
            handle.MarkReleased();
            TryCancelDirector();
            throw;
        }
    }

    public void RaiseImpact(AnimationPlaybackHandle handle, int impactIndex)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (impactIndex < 0 || handle.IsReleased || _activeHandle?.Id != handle.Id || _director is null)
            return;
        try
        {
            _director.CallDeferred("notify_original_impact", impactIndex);
        }
        catch
        {
            ReleaseActiveHandle(cancelDirector: true);
            throw;
        }
    }

    public void PlayCharacterState(CharacterVisualRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HasValidAnchor() || !EnsureMounted() || _director is null)
            return;
        try
        {
            _director.CallDeferred("play_character_state", request.StateId, new Godot.Collections.Dictionary
            {
                ["intensity"] = Math.Clamp(request.Intensity, 0.4f, 2.0f),
                ["force"] = request.Force
            });
        }
        catch { }
    }

    public void PulseVisualState(string stateId, Godot.Collections.Dictionary? parameters = null)
    {
        if (!HasValidAnchor() || !EnsureMounted() || _director is null || string.IsNullOrWhiteSpace(stateId))
            return;
        try
        {
            _director.CallDeferred("pulse_visual_state", stateId, parameters ?? new Godot.Collections.Dictionary());
        }
        catch { }
    }

    public void ClearVisualState(string stateId)
    {
        if (_director is null || string.IsNullOrWhiteSpace(stateId))
            return;
        try { _director.CallDeferred("clear_visual_state", stateId); } catch { }
    }

    public void ClearVisualForm(string formId)
    {
        if (_director is null || string.IsNullOrWhiteSpace(formId))
            return;
        try { _director.CallDeferred("clear_visual_form", formId); } catch { }
    }

    public void Release(AnimationPlaybackHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.IsReleased)
            return;
        if (_activeHandle?.Id != handle.Id)
        {
            ClearPendingFailure(handle);
            handle.MarkReleased();
            return;
        }
        ReleaseActiveHandle(cancelDirector: true);
    }

    public void ReleaseCombatResources()
    {
        AnimationPlaybackHandle? handle = _activeHandle;
        _activeHandle = null;
        _pendingFailureHandle = null;
        _pendingFailureCardId = null;
        _lastCanPlayFailureReason = null;
        try
        {
            _director?.Call("release_combat_resources");
        }
        catch { }
        finally
        {
            handle?.MarkReleased();
            _runtimeRoot?.QueueFree();
            _runtimeRoot = null;
            _director = null;
            ClearAnchor();
        }
    }

    internal bool TryProcessPendingFailureInjection()
    {
        AnimationPlaybackHandle? handle = _pendingFailureHandle;
        string? cardId = _pendingFailureCardId;
        if (handle is null || string.IsNullOrWhiteSpace(cardId))
            return false;
        if (handle.IsReleased || _activeHandle?.Id != handle.Id)
        {
            ClearPendingFailure(handle);
            return false;
        }
        if (!RuntimeCanaryFailureDiagnostics.TryTakePostHideFailure(cardId, out string scenario))
            return false;

        _pendingFailureHandle = null;
        _pendingFailureCardId = null;
        bool injected = false;
        if (string.Equals(scenario, RuntimeCanaryFailureScenarios.ForcedPlaybackFailure, StringComparison.Ordinal))
        {
            injected = InjectPlaybackFailureForCanary(handle, "fault_injection:forced_playback_failure");
        }
        else if (string.Equals(scenario, RuntimeCanaryFailureScenarios.AnchorInvalidation, StringComparison.Ordinal))
        {
            injected = InjectAnchorInvalidationForCanary("fault_injection:anchor_invalidation");
        }
        RuntimeCanaryFailureDiagnostics.RefreshStatus();
        return injected;
    }

    private bool InjectPlaybackFailureForCanary(AnimationPlaybackHandle handle, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || handle.IsReleased || _activeHandle?.Id != handle.Id)
            return false;
        RetireCompletedHandle(handle.AnimationId, reason);
        return true;
    }

    private bool InjectAnchorInvalidationForCanary(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || !HasValidAnchor())
            return false;
        ReleaseActiveHandle(cancelDirector: true);
        _anchor = null;
        Visible = false;
        Position = Vector2.Zero;
        Rotation = 0.0f;
        Scale = Vector2.One;
        NotifyAnchorInvalidated(reason);
        return true;
    }

    private bool EnsureMounted()
    {
        if (IsInstanceValid(_runtimeRoot) && IsInstanceValid(_director))
            return true;

        PackedScene? scene = GD.Load<PackedScene>(RuntimeScenePath);
        if (scene is null)
            return false;

        Node? mountedRoot = null;
        try
        {
            mountedRoot = scene.Instantiate<Node>();
            AddChild(mountedRoot);
            Node? mountedDirector = mountedRoot.GetNodeOrNull<Node>("AnimationDirector");
            if (mountedDirector is null || !mountedDirector.HasSignal("timeline_failed"))
            {
                mountedRoot.QueueFree();
                return false;
            }

            string completionSignal = mountedDirector.HasSignal("playback_committed")
                ? "playback_committed"
                : "timeline_completed";
            if (!mountedDirector.HasSignal(completionSignal))
            {
                mountedRoot.QueueFree();
                return false;
            }

            mountedDirector.Connect(completionSignal, Callable.From<string>(OnTimelineCompleted));
            mountedDirector.Connect("timeline_failed", Callable.From<string, string>(OnTimelineFailed));
            _runtimeRoot = mountedRoot;
            _director = mountedDirector;
            return true;
        }
        catch
        {
            mountedRoot?.QueueFree();
            _runtimeRoot = null;
            _director = null;
            return false;
        }
    }

    private bool HasValidAnchor() =>
        _anchor is not null && GodotObject.IsInstanceValid(_anchor) && _anchor.IsInsideTree();

    private void SyncAnchorTransform()
    {
        if (!HasValidAnchor())
        {
            bool hadAnchor = _anchor is not null;
            Visible = false;
            if (hadAnchor)
                NotifyAnchorInvalidated("The verified local-player visual anchor left the scene tree.");
            return;
        }

        try
        {
            Node2D anchor = _anchor!;
            Transform2D canvasTransform = anchor.GetGlobalTransformWithCanvas();
            GlobalPosition = canvasTransform.Origin + _anchorOffset;
            GlobalRotation = anchor.GlobalRotation;
            GlobalScale = anchor.GlobalScale * _anchorScale;
            ZAsRelative = false;
            ZIndex = anchor.ZIndex + 1;
            Visible = true;
        }
        catch
        {
            Visible = false;
            NotifyAnchorInvalidated("The verified local-player visual anchor transform could not be synchronized.");
        }
    }

    private void NotifyAnchorInvalidated(string reason)
    {
        if (_anchorInvalidationNotified)
            return;
        _anchorInvalidationNotified = true;
        try
        {
            AnchorInvalidated?.Invoke(reason);
        }
        catch
        {
            // Replacement restoration listeners are cosmetic and fail closed.
        }
    }

    private void OnTimelineCompleted(string animationId)
    {
        RetireCompletedHandle(animationId, null);
    }

    private void OnTimelineFailed(string animationId, string reason)
    {
        RetireCompletedHandle(animationId, reason);
    }

    private void RetireCompletedHandle(string animationId, string? failureReason)
    {
        AnimationPlaybackHandle? handle = _activeHandle;
        if (handle is null || !string.Equals(handle.AnimationId, animationId, StringComparison.Ordinal))
            return;

        _activeHandle = null;
        ClearPendingFailure(handle);
        try
        {
            if (failureReason is null)
                PlaybackCompleted?.Invoke(handle);
            else
                PlaybackFailed?.Invoke(handle, failureReason);
        }
        catch
        {
            // Notification consumers are cosmetic coordinators; their failures
            // cannot be allowed to affect the Godot signal path.
        }
        finally
        {
            handle.MarkReleased();
        }
    }

    private void ReleaseActiveHandle(bool cancelDirector)
    {
        AnimationPlaybackHandle? handle = _activeHandle;
        _activeHandle = null;
        if (handle is not null)
            ClearPendingFailure(handle);
        try
        {
            if (cancelDirector)
                TryCancelDirector();
        }
        finally
        {
            handle?.MarkReleased();
        }
    }

    private void ClearPendingFailure(AnimationPlaybackHandle handle)
    {
        if (_pendingFailureHandle?.Id != handle.Id)
            return;
        _pendingFailureHandle = null;
        _pendingFailureCardId = null;
    }

    private void TryCancelDirector()
    {
        try { _director?.CallDeferred("cancel_current"); } catch { }
    }

    private static string ToVariantName(CardAnimationVariant variant) => variant switch
    {
        CardAnimationVariant.Upgraded => "upgraded",
        CardAnimationVariant.Empowered => "empowered",
        CardAnimationVariant.Lethal => "lethal",
        _ => "base"
    };
}
