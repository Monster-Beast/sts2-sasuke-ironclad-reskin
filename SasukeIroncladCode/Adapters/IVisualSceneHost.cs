using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

/// <summary>
/// Owns local-only Godot visual nodes. Implementations must never block the
/// gameplay action queue while an animation is playing.
/// </summary>
public interface IVisualSceneHost
{
    bool CanPlay(CardAnimationSelection selection);
    AnimationPlaybackHandle Play(CardAnimationSelection selection, AnimationContext context);
    void RaiseImpact(AnimationPlaybackHandle handle, int impactIndex);
    void Release(AnimationPlaybackHandle handle);
    void ClearVisualState(string stateId);
    void ClearVisualForm(string formId);
    void ReleaseCombatResources();
}
