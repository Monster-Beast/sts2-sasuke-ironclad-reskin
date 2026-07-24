using SasukeIronclad.SasukeIroncladCode.Adapters;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

/// <summary>
/// Routes semantic character presentation events to the local-only scene host.
/// It never waits for visual completion and never touches gameplay state.
/// </summary>
public sealed class CharacterVisualEventCoordinator : IDisposable
{
    private readonly ICharacterVisualEventSource _source;
    private readonly IVisualSceneHost _sceneHost;
    private bool _disposed;

    public CharacterVisualEventCoordinator(
        ICharacterVisualEventSource source,
        IVisualSceneHost sceneHost
    )
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _sceneHost = sceneHost ?? throw new ArgumentNullException(nameof(sceneHost));
        _source.CharacterVisualRequested += OnCharacterVisualRequested;
    }

    private void OnCharacterVisualRequested(CharacterVisualRequest request)
    {
        if (_disposed)
            return;

        _sceneHost.PlayCharacterState(request);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _source.CharacterVisualRequested -= OnCharacterVisualRequested;
    }
}
