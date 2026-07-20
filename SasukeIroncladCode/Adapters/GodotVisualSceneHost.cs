using Godot;
using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

/// <summary>
/// Bridges the card-specific C# runtime to the local-only Godot visual scene.
/// The scene pauses at each impact gate until RaiseImpact receives the matching
/// authoritative hit event from the game adapter.
/// </summary>
public partial class GodotVisualSceneHost : Node, IVisualSceneHost
{
    private const string DefaultRuntimeScene = "res://SasukeIronclad/scenes/runtime/animation_director.tscn";

    private readonly Dictionary<Guid, AnimationPlaybackHandle> _active = [];
    private Node? _runtimeRoot;
    private Node? _director;

    [Export(PropertyHint.File, "*.tscn")]
    public string RuntimeScenePath { get; set; } = DefaultRuntimeScene;

    [Export(PropertyHint.Range, "0.25,1.0,0.25")]
    public float QualityScale { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.25,5.0,0.25")]
    public float ImpactTimeoutSeconds { get; set; } = 1.5f;

    public override void _Ready()
    {
        EnsureMounted();
    }

    public bool CanPlay(CardAnimationSelection selection)
    {
        if (!EnsureMounted() || _director is null)
            return false;

        return _director.Call("has_timeline", selection.AnimationId).AsBool();
    }

    public AnimationPlaybackHandle Play(CardAnimationSelection selection, AnimationContext context)
    {
        if (!CanPlay(selection) || _director is null)
            throw new InvalidOperationException($"Timeline is unavailable: {selection.AnimationId}");

        AnimationPlaybackHandle handle = new(selection.CardId, selection.AnimationId);
        _active[handle.Id] = handle;

        Godot.Collections.Dictionary runtimeContext = new()
        {
            ["low_flash"] = context.LowFlashMode,
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

        _director.CallDeferred(
            "play_timeline",
            selection.AnimationId,
            ToVariantName(selection.Variant),
            runtimeContext
        );
        return handle;
    }

    public void RaiseImpact(AnimationPlaybackHandle handle, int impactIndex)
    {
        if (handle.IsReleased || !_active.ContainsKey(handle.Id) || _director is null)
            return;

        _director.CallDeferred("notify_original_impact", impactIndex);
    }

    public void PlayCharacterState(CharacterVisualRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnsureMounted() || _director is null)
            return;

        Godot.Collections.Dictionary parameters = new()
        {
            ["intensity"] = Math.Clamp(request.Intensity, 0.4f, 2.0f),
            ["force"] = request.Force
        };
        _director.CallDeferred("play_character_state", request.StateId, parameters);
    }

    /// <summary>
    /// Pulses a previously committed local-only visual state, for example when
    /// Flame Barrier reacts to an authoritative enemy hit.
    /// </summary>
    public void PulseVisualState(string stateId, Godot.Collections.Dictionary? parameters = null)
    {
        if (!EnsureMounted() || _director is null || string.IsNullOrWhiteSpace(stateId))
            return;

        _director.CallDeferred(
            "pulse_visual_state",
            stateId,
            parameters ?? new Godot.Collections.Dictionary()
        );
    }

    public void ClearVisualState(string stateId)
    {
        if (_director is null || string.IsNullOrWhiteSpace(stateId))
            return;
        _director.CallDeferred("clear_visual_state", stateId);
    }

    public void ClearVisualForm(string formId)
    {
        if (_director is null || string.IsNullOrWhiteSpace(formId))
            return;
        _director.CallDeferred("clear_visual_form", formId);
    }

    public void Release(AnimationPlaybackHandle handle)
    {
        if (!_active.Remove(handle.Id))
            return;

        if (_director is not null)
            _director.CallDeferred("cancel_current");
    }

    public void ReleaseCombatResources()
    {
        _active.Clear();
        if (_director is not null)
            _director.Call("release_combat_resources");
        _runtimeRoot?.QueueFree();
        _runtimeRoot = null;
        _director = null;
    }

    private bool EnsureMounted()
    {
        if (IsInstanceValid(_runtimeRoot) && IsInstanceValid(_director))
            return true;

        PackedScene? scene = GD.Load<PackedScene>(RuntimeScenePath);
        if (scene is null)
            return false;

        _runtimeRoot = scene.Instantiate<Node>();
        AddChild(_runtimeRoot);
        _director = _runtimeRoot.GetNodeOrNull<Node>("AnimationDirector");
        if (_director is not null)
            return true;

        _runtimeRoot.QueueFree();
        _runtimeRoot = null;
        return false;
    }

    private static string ToVariantName(CardAnimationVariant variant) => variant switch
    {
        CardAnimationVariant.Upgraded => "upgraded",
        CardAnimationVariant.Empowered => "empowered",
        CardAnimationVariant.Lethal => "lethal",
        CardAnimationVariant.Fast => "fast",
        CardAnimationVariant.LowFlash => "low_flash",
        _ => "base"
    };
}
