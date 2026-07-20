using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

/// <summary>
/// Semantic boundary for applying display-only card titles to verified UI surfaces.
/// Implementations must never mutate card IDs, rules text, costs or upgrade state.
/// </summary>
public interface ICardTitleSurfaceAdapter
{
    bool CanApply(string surfaceId);
    void Apply(CardTitlePresentation presentation);
}

public sealed class CardTitlePresentationService
{
    private const string SurfaceConfigPath = "res://SasukeIronclad/data/card_title_surfaces.json";

    private readonly ICardDisplayNameResolver _nameResolver;
    private readonly IReadOnlyDictionary<string, CardTitleSurfaceSpec> _surfaces;
    private readonly string _fallback;

    public CardTitlePresentationService(ICardDisplayNameResolver nameResolver)
    {
        _nameResolver = nameResolver;
        CardTitleSurfaceMap map = VisualConfigLoader.Load<CardTitleSurfaceMap>(SurfaceConfigPath);
        Validate(map);
        _surfaces = map.Surfaces.ToDictionary(item => item.Id, StringComparer.Ordinal);
        _fallback = map.Fallback;
    }

    public bool TryPresent(
        ICardTitleSurfaceAdapter adapter,
        string surfaceId,
        string cardId,
        string? locale,
        bool upgraded,
        string originalName)
    {
        if (!_surfaces.TryGetValue(surfaceId, out CardTitleSurfaceSpec? surface) ||
            !adapter.CanApply(surfaceId))
        {
            return false;
        }

        string resolvedLocale = NormalizeLocale(locale);
        string displayName = _nameResolver.Resolve(cardId, resolvedLocale, upgraded, originalName);
        string originalDisplay = upgraded ? $"{originalName}+" : originalName;
        double widthUnits = surface.WidthUnits.TryGetValue(resolvedLocale, out double units)
            ? units
            : surface.WidthUnits.Values.First();

        adapter.Apply(new CardTitlePresentation(
            cardId,
            surfaceId,
            resolvedLocale,
            displayName,
            originalDisplay,
            upgraded,
            surface.MaxLines,
            surface.MinimumScale,
            widthUnits,
            surface.OverflowOrder,
            _fallback
        ));
        return true;
    }

    private static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return "zh-CN";
        string normalized = locale.Replace('_', '-');
        return normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US";
    }

    private static void Validate(CardTitleSurfaceMap map)
    {
        if (map.SchemaVersion != 1 || map.GameplayChanges || map.Fallback != "original_title")
            throw new InvalidOperationException("Invalid display-only card-title surface configuration.");

        HashSet<string> ids = [];
        foreach (CardTitleSurfaceSpec surface in map.Surfaces)
        {
            if (string.IsNullOrWhiteSpace(surface.Id) || !ids.Add(surface.Id))
                throw new InvalidOperationException($"Missing or duplicate card-title surface: {surface.Id}");
            if (surface.MaxLines is < 1 or > 2 || surface.MinimumScale is < 0.6 or > 1.0)
                throw new InvalidOperationException($"Invalid title layout limits for surface {surface.Id}.");
            if (!surface.WidthUnits.ContainsKey("zh-CN") || !surface.WidthUnits.ContainsKey("en-US"))
                throw new InvalidOperationException($"Surface {surface.Id} lacks locale width budgets.");
            if (!surface.OverflowOrder.Contains("original_title"))
                throw new InvalidOperationException($"Surface {surface.Id} lacks original-title fallback.");
            if (surface.HookStatus != "pending_local_audit")
                throw new InvalidOperationException($"Surface {surface.Id} must remain unbound before local audit.");
        }
    }
}
