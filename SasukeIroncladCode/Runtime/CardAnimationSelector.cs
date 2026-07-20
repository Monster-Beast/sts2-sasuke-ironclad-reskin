using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

/// <summary>
/// Selects only variants of the card-specific animation identified by CardId.
/// Accessibility and speed remain independent flags on the selection.
/// </summary>
public static class CardAnimationSelector
{
    public static bool TrySelect(AnimationContext context, out CardAnimationSelection? selection)
    {
        selection = null;
        if (string.IsNullOrWhiteSpace(context.CardId) ||
            !VisualRegistry.TryGetCardAnimation(context.CardId, out CardAnimationSpec? spec) ||
            spec is null)
        {
            return false;
        }

        CardAnimationVariant variant = SelectContentVariant(spec, context);
        bool usesCutin = spec.PresentationTier is "cutin" or "finisher";

        selection = new CardAnimationSelection(
            spec.CardId,
            spec.AnimationId,
            variant,
            usesCutin && !context.FastMode && !context.LowFlashMode,
            spec.Fallback
        )
        {
            FastMode = context.FastMode,
            LowFlashMode = context.LowFlashMode
        };
        return true;
    }

    internal static CardAnimationVariant SelectContentVariant(CardAnimationSpec spec, AnimationContext context)
    {
        HashSet<string> variants = spec.Variants.ToHashSet(StringComparer.Ordinal);

        if (context.IsLethal && variants.Contains("lethal"))
            return CardAnimationVariant.Lethal;

        if (HasEmpoweredVariant(spec, variants) && IsCardLocallyEmpowered(spec.CardId, context))
            return CardAnimationVariant.Empowered;

        if (context.IsUpgraded && variants.Contains("upgraded"))
            return CardAnimationVariant.Upgraded;

        return CardAnimationVariant.Base;
    }

    private static bool HasEmpoweredVariant(CardAnimationSpec spec, HashSet<string> variants) =>
        variants.Contains("empowered") || spec.CardId switch
        {
            "Heavy Blade" => variants.Contains("high_strength"),
            "Whirlwind" => variants.Contains("x_energy"),
            "Fiend Fire" => variants.Contains("hand_count"),
            "Limit Break" => variants.Contains("high_strength"),
            _ => false
        };

    private static bool IsCardLocallyEmpowered(string cardId, AnimationContext context) => cardId switch
    {
        "Heavy Blade" => context.Strength > 0,
        "Whirlwind" => context.EnergySpent > 1,
        "Fiend Fire" => context.ExhaustedCardCount > 1,
        "Limit Break" => context.Strength >= 5,
        _ => false
    };
}
