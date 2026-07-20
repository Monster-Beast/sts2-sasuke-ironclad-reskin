using SasukeIronclad.SasukeIroncladCode.Adapters;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

/// <summary>
/// Connects a future verified game-version adapter to local-only visual cleanup.
/// It never reads combat values and never writes to gameplay state.
/// </summary>
public sealed class CombatVisualLifecycleCoordinator : IDisposable
{
    private readonly ICombatVisualLifecycleSource _source;
    private readonly IVisualSceneHost _sceneHost;
    private bool _started;

    public CombatVisualLifecycleCoordinator(
        ICombatVisualLifecycleSource source,
        IVisualSceneHost sceneHost)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _sceneHost = sceneHost ?? throw new ArgumentNullException(nameof(sceneHost));
    }

    public void Start()
    {
        if (_started)
            return;

        _source.VisualRemovalRequested += OnVisualRemovalRequested;
        _source.CombatEnded += OnCombatEnded;
        _source.Start();
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
            return;

        _source.VisualRemovalRequested -= OnVisualRemovalRequested;
        _source.CombatEnded -= OnCombatEnded;
        _source.Stop();
        _started = false;
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnVisualRemovalRequested(VisualRemovalRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.VisualStateId))
            _sceneHost.ClearVisualState(request.VisualStateId);
        if (!string.IsNullOrWhiteSpace(request.VisualFormId))
            _sceneHost.ClearVisualForm(request.VisualFormId);
    }

    private void OnCombatEnded()
    {
        _sceneHost.ReleaseCombatResources();
    }
}
