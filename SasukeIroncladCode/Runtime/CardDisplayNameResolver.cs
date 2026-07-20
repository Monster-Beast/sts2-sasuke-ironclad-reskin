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
        string fallback = RemoveUpgradeSuffix(originalName) ?? cardId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cardId) || !TryNormalizeSupportedLocale(locale, out string normalizedLocale))
            return ApplyUpgradeSuffix(fallback, upgraded);

        // Ask the registry for the base title, then apply the upgrade suffix once
        // at this boundary. This avoids duplicate "++" when a UI supplies an
        // original title that already contains the upgrade marker.
        string resolved = VisualRegistry.ResolveCardDisplayName(
            cardId,
            normalizedLocale,
            upgraded: false,
            originalFallback: fallback
        );
        return ApplyUpgradeSuffix(resolved, upgraded);
    }

    internal static bool TryNormalizeSupportedLocale(string? locale, out string normalizedLocale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            normalizedLocale = "zh-CN";
            return true;
        }

        string normalized = locale.Replace('_', '-');
        if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            normalizedLocale = "zh-CN";
            return true;
        }
        if (normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            normalizedLocale = "en-US";
            return true;
        }

        normalizedLocale = string.Empty;
        return false;
    }

    internal static string ApplyUpgradeSuffix(string name, bool upgraded)
    {
        if (!upgraded || string.IsNullOrEmpty(name) || name.EndsWith('+'))
            return name;
        return $"{name}+";
    }

    internal static string? RemoveUpgradeSuffix(string? name)
    {
        if (string.IsNullOrEmpty(name) || !name.EndsWith('+'))
            return name;
        return name[..^1];
    }
}
