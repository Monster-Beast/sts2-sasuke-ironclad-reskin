using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public interface IRuntimeCanaryPatcher
{
    IReadOnlyList<string> PatchedBindingIds { get; }
    void Install(RuntimeCanarySession session, IReadOnlyList<ResolvedRuntimeCanaryTarget> targets);
    void Reset();
}
