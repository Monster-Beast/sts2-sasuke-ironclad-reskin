namespace SasukeIronclad.SasukeIroncladCode.Visuals;

public static class VisualRegistry
{
    private const string CardMapPath = "res://SasukeIronclad/data/card_visual_map.json";
    private const string ActionMapPath = "res://SasukeIronclad/data/action_profiles.json";

    private static IReadOnlyDictionary<string, CardVisualSpec> _cards = new Dictionary<string, CardVisualSpec>();
    private static IReadOnlyDictionary<string, ActionProfile> _actions = new Dictionary<string, ActionProfile>();

    public static int CardCount => _cards.Count;
    public static int ActionCount => _actions.Count;

    public static void Initialize()
    {
        CardVisualMap cardMap = VisualConfigLoader.Load<CardVisualMap>(CardMapPath);
        ActionProfileMap actionMap = VisualConfigLoader.Load<ActionProfileMap>(ActionMapPath);
        Validate(cardMap, actionMap);
        _cards = cardMap.Cards.ToDictionary(card => card.CardId, StringComparer.Ordinal);
        _actions = actionMap.Profiles.ToDictionary(profile => profile.Id, StringComparer.Ordinal);
    }

    public static bool TryGetCard(string cardId, out CardVisualSpec? spec) => _cards.TryGetValue(cardId, out spec);
    public static bool TryGetAction(string actionId, out ActionProfile? profile) => _actions.TryGetValue(actionId, out profile);

    private static void Validate(CardVisualMap cardMap, ActionProfileMap actionMap)
    {
        if (cardMap.SchemaVersion != 1 || actionMap.SchemaVersion != 1)
            throw new InvalidOperationException("Unsupported visual configuration schema.");
        if (cardMap.GameplayChanges)
            throw new InvalidOperationException("Cosmetic configuration cannot declare gameplay changes.");
        if (!actionMap.GameplayTimingLocked)
            throw new InvalidOperationException("Action profiles must remain locked to original gameplay timing.");

        HashSet<string> actionIds = [];
        foreach (ActionProfile profile in actionMap.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id) || !actionIds.Add(profile.Id))
                throw new InvalidOperationException($"Missing or duplicate action profile: {profile.Id}");
            if (profile.Fallback != "original")
                throw new InvalidOperationException($"Action profile {profile.Id} must fall back to the original animation.");
            if (profile.MaxDurationMs is < 200 or > 2000)
                throw new InvalidOperationException($"Action profile {profile.Id} has an invalid duration.");
            if (profile.ImpactFraction is < 0 or > 1)
                throw new InvalidOperationException($"Action profile {profile.Id} has an invalid impact fraction.");
        }

        HashSet<string> cardIds = [];
        foreach (CardVisualSpec card in cardMap.Cards)
        {
            if (string.IsNullOrWhiteSpace(card.CardId) || !cardIds.Add(card.CardId))
                throw new InvalidOperationException($"Missing or duplicate card mapping: {card.CardId}");
            if (!actionIds.Contains(card.ActionProfile))
                throw new InvalidOperationException($"Card {card.CardId} references unknown action {card.ActionProfile}.");
            if (card.LegalStatus != "original_required")
                throw new InvalidOperationException($"Card {card.CardId} must require original project art.");
        }
    }
}
