using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

/// <summary>
/// Selects only variants of the card-specific animation identified by CardId.
/// Damage and hit count never participate in selecting AnimationId.
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

        CardAnimationVariant variant = SelectVariant(spec, context);
        bool usesCutin = spec.PresentationTier is "cutin" or "finisher";

        selection = new CardAnimationSelection(
            spec.CardId,
            spec.AnimationId,
            variant,
            usesCutin,
            spec.Fallback
        );
        return true;
    }

    internal static CardAnimationVariant SelectVariant(CardAnimationSpec spec, AnimationContext context)
    {
        HashSet<string> variants = spec.Variants.ToHashSet(StringComparer.Ordinal);

        // Accessibility and speed settings have the highest priority because
        // they are explicit user choices. They still keep the same AnimationId.
        if (context.LowFlashMode && variants.Contains("low_flash"))
            return CardAnimationVariant.LowFlash;
        if (context.FastMode && variants.Contains("fast"))
            return CardAnimationVariant.Fast;
        if (context.IsLethal && variants.Contains("lethal"))
            return CardAnimationVariant.Lethal;

        // Empowerment is card-local. Manifests may use a semantic alias such as
        // high_strength or x_energy, but the runtime sends one stable variant
        // name (empowered) to Godot. Raw damage alone remains insufficient.
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
            _ => false
        };

    private static bool IsCardLocallyEmpowered(string cardId, AnimationContext context) => cardId switch
    {
        "Heavy Blade" => context.Strength > 0,
        "Whirlwind" => context.EnergySpent > 1,
        "Fiend Fire" => context.ExhaustedCardCount > 1,
        _ => false
    };
}
