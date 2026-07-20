using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

/// <summary>
/// Game-version-specific hooks publish read-only visual events through this
/// boundary. Implementations must not mutate combat state.
/// </summary>
public interface ICardVisualEventSource
{
    event Action<AnimationContext>? CardVisualRequested;
    event Action<string, int>? OriginalImpactRaised;
    event Action? CombatVisualsReleased;
}
