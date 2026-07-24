using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public sealed class PreserveOriginalAnimationFallback : IOriginalAnimationFallback
{
    public void KeepOrPlayOriginal(AnimationContext context, string reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = reason;
        // The canary never suppresses the original game callback. A no-op here
        // therefore preserves the original animation whenever custom playback is
        // unavailable or fails.
    }
}
