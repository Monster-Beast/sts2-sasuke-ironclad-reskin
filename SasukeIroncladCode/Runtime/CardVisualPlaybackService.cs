using SasukeIronclad.SasukeIroncladCode.Adapters;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

/// <summary>
/// Coordinates card-specific local visuals without owning gameplay timing.
/// The Godot runtime owns one AnimationDirector, so this service intentionally
/// maintains exactly one active playback handle across all cards.
/// </summary>
public sealed class CardVisualPlaybackService : IDisposable
{
    private readonly IVisualSceneHost _sceneHost;
    private readonly IOriginalAnimationFallback _fallback;
    private readonly IVisualSceneHostNotifications? _notifications;

    private AnimationPlaybackHandle? _activeHandle;
    private AnimationContext? _activeContext;
    private bool _disposed;

    public CardVisualPlaybackService(IVisualSceneHost sceneHost, IOriginalAnimationFallback fallback)
    {
        _sceneHost = sceneHost ?? throw new ArgumentNullException(nameof(sceneHost));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _notifications = sceneHost as IVisualSceneHostNotifications;
        if (_notifications is not null)
        {
            _notifications.PlaybackCompleted += OnPlaybackCompleted;
            _notifications.PlaybackFailed += OnPlaybackFailed;
        }
    }

    public AnimationPlaybackHandle? Request(AnimationContext context)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);
        ReleaseActive();

        try
        {
            if (!CardAnimationSelector.TrySelect(context, out CardAnimationSelection? selection) || selection is null)
            {
                SafeFallback(context, "card animation mapping unavailable");
                return null;
            }

            if (!_sceneHost.CanPlay(selection))
            {
                SafeFallback(context, $"visual assets unavailable for {selection.AnimationId}");
                return null;
            }

            AnimationPlaybackHandle handle = _sceneHost.Play(selection, context);
            _activeHandle = handle;
            _activeContext = context;
            return handle;
        }
        catch (Exception exception)
        {
            ReleaseActive();
            SafeFallback(context, $"visual playback failed: {exception.GetType().Name}");
            return null;
        }
    }

    public void RaiseOriginalImpact(string cardId, int impactIndex)
    {
        if (_disposed || impactIndex < 0)
            return;

        AnimationPlaybackHandle? handle = _activeHandle;
        AnimationContext? context = _activeContext;
        if (handle is null || context is null || handle.IsReleased ||
            !string.Equals(cardId, context.CardId, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _sceneHost.RaiseImpact(handle, impactIndex);
        }
        catch (Exception exception)
        {
            ReleaseActive();
            SafeFallback(context, $"visual impact forwarding failed: {exception.GetType().Name}");
        }
    }

    public void Release(string cardId)
    {
        if (_disposed || _activeContext is null ||
            !string.Equals(cardId, _activeContext.CardId, StringComparison.Ordinal))
        {
            return;
        }
        ReleaseActive();
    }

    public void ReleaseCombatResources()
    {
        if (_disposed)
            return;
        ReleaseActive();
        try
        {
            _sceneHost.ReleaseCombatResources();
        }
        catch
        {
            // Cosmetic cleanup failures must never escape into gameplay teardown.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        ReleaseCombatResources();
        if (_notifications is not null)
        {
            _notifications.PlaybackCompleted -= OnPlaybackCompleted;
            _notifications.PlaybackFailed -= OnPlaybackFailed;
        }
        _disposed = true;
    }

    private void OnPlaybackCompleted(AnimationPlaybackHandle handle)
    {
        if (_disposed || _activeHandle?.Id != handle.Id)
            return;
        _activeHandle = null;
        _activeContext = null;
        handle.MarkReleased();
    }

    private void OnPlaybackFailed(AnimationPlaybackHandle handle, string reason)
    {
        if (_disposed || _activeHandle?.Id != handle.Id)
            return;

        AnimationContext? context = _activeContext;
        _activeHandle = null;
        _activeContext = null;
        handle.MarkReleased();
        if (context is not null)
            SafeFallback(context, $"asynchronous visual playback failed: {reason}");
    }

    private void ReleaseActive()
    {
        AnimationPlaybackHandle? handle = _activeHandle;
        _activeHandle = null;
        _activeContext = null;

        if (handle is null || handle.IsReleased)
            return;

        try
        {
            _sceneHost.Release(handle);
        }
        catch
        {
            // The handle is still retired locally so future impacts cannot be
            // routed to a superseded timeline.
        }
        finally
        {
            handle.MarkReleased();
        }
    }

    private void SafeFallback(AnimationContext context, string reason)
    {
        try
        {
            _fallback.KeepOrPlayOriginal(context, reason);
        }
        catch
        {
            // The fallback adapter is also cosmetic. Never allow it to affect the
            // original combat action queue.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
