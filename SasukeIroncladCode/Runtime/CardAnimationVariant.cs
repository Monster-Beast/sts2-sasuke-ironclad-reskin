namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public enum CardAnimationVariant
{
    Base,
    Upgraded,
    Empowered,
    Lethal,
    Fast,
    LowFlash
}

public sealed record CardAnimationSelection(
    string CardId,
    string AnimationId,
    CardAnimationVariant Variant,
    bool UsesCutin,
    string Fallback
);
