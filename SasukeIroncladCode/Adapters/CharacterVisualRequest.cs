namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public enum CharacterVisualState
{
    CombatEntry,
    IdleNeutral,
    IdleSwordReady,
    IdleSharinganAlert,
    HitLight,
    HitHeavy,
    Death,
    Victory
}

/// <summary>
/// Semantic, local-only character presentation request. It contains no game
/// object references and cannot modify combat state.
/// </summary>
public sealed record CharacterVisualRequest(
    CharacterVisualState State,
    float Intensity = 1.0f,
    bool Force = false
)
{
    public string StateId => State switch
    {
        CharacterVisualState.CombatEntry => "combat_entry",
        CharacterVisualState.IdleNeutral => "idle_neutral",
        CharacterVisualState.IdleSwordReady => "idle_sword_ready",
        CharacterVisualState.IdleSharinganAlert => "idle_sharingan_alert",
        CharacterVisualState.HitLight => "hit_light",
        CharacterVisualState.HitHeavy => "hit_heavy",
        CharacterVisualState.Death => "death",
        CharacterVisualState.Victory => "victory",
        _ => "idle_sword_ready"
    };
}
