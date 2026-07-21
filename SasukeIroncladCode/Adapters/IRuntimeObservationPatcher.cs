using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public interface IRuntimeObservationPatcher
{
    string? SessionId { get; }
    string? OutputPath { get; }
    void Reset();
    void Install(
        RuntimeObservationSession session,
        IReadOnlyList<ResolvedRuntimeObservationTarget> targets);
}
