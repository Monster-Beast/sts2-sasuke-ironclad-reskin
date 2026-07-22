using Godot;
using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public partial class GodotVisualSceneHost : Node2D, IVisualSceneHost, IVisualSceneHostNotifications
{
    private const string DefaultRuntimeScene = "res://SasukeIronclad/scenes/runtime/animation_director.tscn";

    private AnimationPlaybackHandle? _activeHandle;
    private Node? _runtimeRoot;
    private Node? _director;
    private Node2D? _anchor;
    private Vector2 _anchorOffset;
    private float _anchorScale = 1.0f;
    private bool _anchorInvalidationNotified;
    private string? _missingTimelineFailureCardId;

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
    public Node2D? AnchorNode => HasValidAnchor() ? _anchor : null;
    public string? AnchorType => HasValidAnchor() ? _anchor!.GetType().FullName : null;
    public string? AnchorName => HasValidAnchor() ? _anchor!.Name.ToString() : null;
    public Vector2? AnchorGlobalPosition => HasValidAnchor() ? _anchor!.GlobalPosition : null;

    public override void _Ready()
    {
        Visible = false;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        SyncAnchorTransform();
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
        SyncAnchorTransform();
        return Visible;
    }

    public void ClearAnchor()
    {
        _anchor = null;
        _anchorInvalidationNotified = false;
        Visible = false;
        Position = Vector2.Zero;
        Rotation = 0.0f;
        Scale = Vector2.One;
    }

    /// <summary>
    /// Arms a one-shot in-memory missing-timeline result. No PCK or game file is
    /// changed; the next matching CanPlay call follows the normal asset fallback.
    /// </summary>
    public void ArmMissingTimelineFailureForCanary(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            throw new ArgumentException("Failure target card ID is required.", nameof(cardId));
        _missingTimelineFailureCardId = cardId;
    }

    public bool InjectPlaybackFailureForCanary(AnimationPlaybackHandle handle, string reason)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (string.IsNullOrWhiteSpace(reason) || handle.IsReleased || _activeHandle?.Id != handle.Id)
            return false;
        RetireCompletedHandle(handle.AnimationId, reason);
        return true;
    }

    public bool InjectAnchorInvalidationForCanary(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || !HasValidAnchor())
            return false;
        _anchor = null;
        Visible = false;
        Position = Vector2.Zero;
        Rotation = 0.0f;
        Scale = Vector2.One;
        NotifyAnchorInvalidated(reason);
        return true;
    }

    public bool CanPlay(CardAnimationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!HasValidAnchor() || !EnsureMounted() || _director is null)
            return false;
        if (string.Equals(_missingTimelineFailureCardId, selection.CardId, StringComparison.Ordinal))
        {
            _missingTimelineFailureCardId = null;
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
            return handle;
        }
        catch
        {
            if (_activeHandle?.Id == handle.Id)
                _activeHandle = null;
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
            handle.MarkReleased();
            return;
        }
        ReleaseActiveHandle(cancelDirector: true);
    }

    public void ReleaseCombatResources()
    {
        AnimationPlaybackHandle? handle = _activeHandle;
        _activeHandle = null;
        _missingTimelineFailureCardId = null;
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
