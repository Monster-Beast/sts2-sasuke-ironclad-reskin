using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public sealed class HarmonyRuntimeCanaryPatcher : IRuntimeCanaryPatcher
{
    public IReadOnlyList<string> PatchedBindingIds => [];
    public void Install(RuntimeCanarySession session, IReadOnlyList<ResolvedRuntimeCanaryTarget> targets) =>
        throw new NotSupportedException("Runtime canary adapter is not yet installed.");
    public void Reset() { }
}
