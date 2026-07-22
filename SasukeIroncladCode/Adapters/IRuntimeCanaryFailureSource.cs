namespace SasukeIronclad.SasukeIroncladCode.Adapters;

/// <summary>
/// Optional canary-only detail exposed when CanPlay intentionally returns false
/// for an explicitly acknowledged in-memory missing-resource scenario.
/// </summary>
public interface IRuntimeCanaryFailureSource
{
    string? ConsumeCanPlayFailureReason();
}
