using SasukeIronclad.SasukeIroncladCode.Runtime;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public interface IGameIntegrationInstaller
{
    void Reset();
    void Install(GameIntegrationProfile profile, GameIntegrationDecision decision);
}
