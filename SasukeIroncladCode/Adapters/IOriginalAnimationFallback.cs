using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public interface IOriginalAnimationFallback
{
    void KeepOrPlayOriginal(AnimationContext context, string reason);
}
