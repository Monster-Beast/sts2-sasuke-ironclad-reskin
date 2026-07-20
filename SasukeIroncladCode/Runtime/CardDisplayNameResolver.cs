using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

/// <summary>
/// Resolves cosmetic player-facing card titles while preserving the original
/// card ID, rules text, upgrade state and fallback name.
/// </summary>
public interface ICardDisplayNameResolver
{
    string Resolve(string cardId, string? locale, bool upgraded, string? originalName = null);
}

public sealed class CardDisplayNameResolver : ICardDisplayNameResolver
{
    public string Resolve(string cardId, string? locale, bool upgraded, string? originalName = null)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return upgraded && !string.IsNullOrWhiteSpace(originalName) ? $"{originalName}+" : originalName ?? string.Empty;

        return VisualRegistry.ResolveCardDisplayName(cardId, locale, upgraded, originalName);
    }
}
