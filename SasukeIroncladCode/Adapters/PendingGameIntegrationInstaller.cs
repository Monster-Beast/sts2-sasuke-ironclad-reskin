using SasukeIronclad.SasukeIroncladCode.Runtime;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

/// <summary>
/// Placeholder used while no audited game adapter is registered. Installation
/// is intentionally refused so a configuration mistake cannot activate hooks.
/// </summary>
public sealed class PendingGameIntegrationInstaller : IGameIntegrationInstaller
{
    public void Reset()
    {
    }

    public void Install(GameIntegrationProfile profile, GameIntegrationDecision decision) =>
        throw new InvalidOperationException(
            "No audited game adapter is registered; integration remains disabled."
        );
}
