using Godot;
using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public partial class GodotVisualSceneHost : Node, IVisualSceneHost
{
    private const string DefaultRuntimeScene = "res://SasukeIronclad/scenes/runtime/animation_director.tscn";

    private AnimationPlaybackHandle? _activeHandle;
    private Node? _runtimeRoot;
    private Node? _director;

    [Export(PropertyHint.File, "*.tscn")]
    public string RuntimeScenePath { get; set; } = DefaultRuntimeScene;

    [Export(PropertyHint.Range, "0.25,1.0,0.25")]
    public float QualityScale { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.25,5.0,0.25")]
    public float ImpactTimeoutSeconds { get; set; } = 1.5f;

    public override void _Ready() => EnsureMounted();

    public bool CanPlay(CardAnimationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!EnsureMounted() || _director is null)
            return false;
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
            ["external_impact_sync"] = true,
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
        if (!EnsureMounted() || _director is null)
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
        if (!EnsureMounted() || _director is null || string.IsNullOrWhiteSpace(stateId))
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
            if (mountedDirector is null ||
                !mountedDirector.HasSignal("timeline_completed") ||
                !mountedDirector.HasSignal("timeline_failed"))
            {
                mountedRoot.QueueFree();
                return false;
            }

            mountedDirector.Connect("timeline_completed", Callable.From<string>(OnTimelineCompleted));
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

    private void OnTimelineCompleted(string animationId) => RetireCompletedHandle(animationId);

    private void OnTimelineFailed(string animationId, string reason)
    {
        _ = reason;
        RetireCompletedHandle(animationId);
    }

    private void RetireCompletedHandle(string animationId)
    {
        AnimationPlaybackHandle? handle = _activeHandle;
        if (handle is null || !string.Equals(handle.AnimationId, animationId, StringComparison.Ordinal))
            return;
        _activeHandle = null;
        handle.MarkReleased();
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
