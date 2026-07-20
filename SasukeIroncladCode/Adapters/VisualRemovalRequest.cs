namespace SasukeIronclad.SasukeIroncladCode.Adapters;

/// <summary>
/// Semantic local-visual removal request emitted by a future verified game
/// adapter. It contains no game object references and cannot mutate gameplay.
/// </summary>
public sealed record VisualRemovalRequest(
    string? VisualStateId = null,
    string? VisualFormId = null
);
