using SasukeIronclad.SasukeIroncladCode.Adapters;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

/// <summary>
/// Coordinates card-specific local visuals without owning gameplay timing.
/// Original hit events remain authoritative and are forwarded to the active
/// visual handle only when available.
/// </summary>
public sealed class CardVisualPlaybackService
{
    private readonly IVisualSceneHost _sceneHost;
    private readonly IOriginalAnimationFallback _fallback;
    private readonly Dictionary<string, AnimationPlaybackHandle> _activeByCard = new(StringComparer.Ordinal);

    public CardVisualPlaybackService(IVisualSceneHost sceneHost, IOriginalAnimationFallback fallback)
    {
        _sceneHost = sceneHost;
        _fallback = fallback;
    }

    public AnimationPlaybackHandle? Request(AnimationContext context)
    {
        try
        {
            if (!CardAnimationSelector.TrySelect(context, out CardAnimationSelection? selection) || selection is null)
            {
                _fallback.KeepOrPlayOriginal(context, "card animation mapping unavailable");
                return null;
            }

            if (!_sceneHost.CanPlay(selection))
            {
                _fallback.KeepOrPlayOriginal(context, $"visual assets unavailable for {selection.AnimationId}");
                return null;
            }

            Release(context.CardId);
            AnimationPlaybackHandle handle = _sceneHost.Play(selection, context);
            _activeByCard[context.CardId] = handle;
            return handle;
        }
        catch (Exception exception)
        {
            _fallback.KeepOrPlayOriginal(context, $"visual playback failed: {exception.GetType().Name}");
            Release(context.CardId);
            return null;
        }
    }

    public void RaiseOriginalImpact(string cardId, int impactIndex)
    {
        if (_activeByCard.TryGetValue(cardId, out AnimationPlaybackHandle? handle) && !handle.IsReleased)
            _sceneHost.RaiseImpact(handle, impactIndex);
    }

    public void Release(string cardId)
    {
        if (!_activeByCard.Remove(cardId, out AnimationPlaybackHandle? handle))
            return;

        _sceneHost.Release(handle);
        handle.MarkReleased();
    }

    public void ReleaseCombatResources()
    {
        foreach (string cardId in _activeByCard.Keys.ToArray())
            Release(cardId);
        _sceneHost.ReleaseCombatResources();
    }
}
