namespace SasukeIronclad.GameAudit;

/// <summary>
/// Synthetic metadata used only by the audit self-test. The self-test copies its
/// own assembly to a fixture game directory, so these members exercise method,
/// event, property and constant-field discovery without real game files.
/// </summary>
internal static class AuditFixtureSymbols
{
    public const string StrikeCardId = "STRIKE";
    public const string DefendCardId = "DEFEND";
    public static string CardArtTitle => "fixture-card-art";
    public static string HandTitle => "fixture-hand";
    public static string DeckListTitle => "fixture-deck-list";
    public static string RewardTitle => "fixture-reward";
    public static string CompendiumTitle => "fixture-compendium";
    public static string TooltipTitle => "fixture-tooltip";

    public static event Action? CardVisualRequested;
    public static event Action<int>? OriginalImpact;
    public static event Action<string>? StateRemoved;
    public static event Action<string>? FormRemoved;
    public static event Action? CombatEnded;
    public static event Action<string>? CharacterStateChanged;

    public static void RaiseFixtureEvents()
    {
        CardVisualRequested?.Invoke();
        OriginalImpact?.Invoke(0);
        StateRemoved?.Invoke("fixture-state");
        FormRemoved?.Invoke("fixture-form");
        CharacterStateChanged?.Invoke("fixture-state");
        CombatEnded?.Invoke();
    }

    public static void RefreshCardTitle() { }
    public static void RefreshHandTitle() { }
    public static void RefreshDeckListTitle() { }
    public static void RefreshRewardTitle() { }
    public static void RefreshCompendiumTitle() { }
    public static void RefreshTooltipTitle() { }
}
