namespace SasukeIronclad.SasukeIroncladCode.Runtime;

/// <summary>
/// Read-only combat values already calculated by the game. These values may
/// select a variant of the current card animation, but may never select a
/// different card's animation or be written back into gameplay state.
/// </summary>
public sealed record AnimationContext(
    string CardId,
    bool IsUpgraded,
    int FinalDamage,
    int HitCount,
    int TargetCount,
    int EnergySpent,
    int Strength,
    int ExhaustedCardCount,
    bool IsLethal,
    bool FastMode,
    bool LowFlashMode
);
