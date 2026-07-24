namespace SasukeIronclad.SasukeIroncladCode.Adapters;

/// <summary>
/// Version-specific adapters may emit verified combat-end and visual-removal
/// notifications through this interface. The core project intentionally does
/// not reference unverified Slay the Spire 2 types or Harmony targets.
/// </summary>
public interface ICombatVisualLifecycleSource
{
    event Action<VisualRemovalRequest>? VisualRemovalRequested;
    event Action? CombatEnded;

    void Start();
    void Stop();
}
