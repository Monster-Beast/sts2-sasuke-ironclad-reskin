namespace SasukeIronclad.SasukeIroncladCode.Visuals;

public static class VisualRegistry
{
    private const string CardMapPath = "res://SasukeIronclad/data/card_visual_map.json";
    private const string CardAnimationPath = "res://SasukeIronclad/data/card_animation_manifest.json";
    private const string ActionMapPath = "res://SasukeIronclad/data/action_profiles.json";
    private const string TierMapPath = "res://SasukeIronclad/data/presentation_tiers.json";
    private const string SurfaceMapPath = "res://SasukeIronclad/data/presentation_surfaces.json";

    private static IReadOnlyDictionary<string, CardVisualSpec> _cards = new Dictionary<string, CardVisualSpec>();
    private static IReadOnlyDictionary<string, CardAnimationSpec> _cardAnimations = new Dictionary<string, CardAnimationSpec>();
    private static IReadOnlyDictionary<string, ActionProfile> _actions = new Dictionary<string, ActionProfile>();
    private static IReadOnlyDictionary<string, PresentationTier> _tiers = new Dictionary<string, PresentationTier>();
    private static IReadOnlyDictionary<string, PresentationSurface> _surfaces = new Dictionary<string, PresentationSurface>();

    public static int CardCount => _cards.Count;
    public static int CardAnimationCount => _cardAnimations.Count;
    public static int ActionCount => _actions.Count;
    public static int TierCount => _tiers.Count;
    public static int SurfaceCount => _surfaces.Count;

    public static void Initialize()
    {
        CardVisualMap cardMap = VisualConfigLoader.Load<CardVisualMap>(CardMapPath);
        CardAnimationManifest animationManifest = VisualConfigLoader.Load<CardAnimationManifest>(CardAnimationPath);
        ActionProfileMap actionMap = VisualConfigLoader.Load<ActionProfileMap>(ActionMapPath);
        PresentationTierMap tierMap = VisualConfigLoader.Load<PresentationTierMap>(TierMapPath);
        PresentationSurfaceMap surfaceMap = VisualConfigLoader.Load<PresentationSurfaceMap>(SurfaceMapPath);
        Validate(cardMap, animationManifest, actionMap, tierMap, surfaceMap);
        _cards = cardMap.Cards.ToDictionary(card => card.CardId, StringComparer.Ordinal);
        _cardAnimations = animationManifest.Animations.ToDictionary(animation => animation.CardId, StringComparer.Ordinal);
        _actions = actionMap.Profiles.ToDictionary(profile => profile.Id, StringComparer.Ordinal);
        _tiers = tierMap.Tiers.ToDictionary(tier => tier.Id, StringComparer.Ordinal);
        _surfaces = surfaceMap.Surfaces.ToDictionary(surface => surface.Id, StringComparer.Ordinal);
    }

    public static bool TryGetCard(string cardId, out CardVisualSpec? spec) => _cards.TryGetValue(cardId, out spec);
    public static bool TryGetCardAnimation(string cardId, out CardAnimationSpec? spec) => _cardAnimations.TryGetValue(cardId, out spec);
    public static bool TryGetAction(string actionId, out ActionProfile? profile) => _actions.TryGetValue(actionId, out profile);
    public static bool TryGetTier(string tierId, out PresentationTier? tier) => _tiers.TryGetValue(tierId, out tier);
    public static bool TryGetSurface(string surfaceId, out PresentationSurface? surface) => _surfaces.TryGetValue(surfaceId, out surface);

    private static void Validate(
        CardVisualMap cardMap,
        CardAnimationManifest animationManifest,
        ActionProfileMap actionMap,
        PresentationTierMap tierMap,
        PresentationSurfaceMap surfaceMap)
    {
        if (cardMap.SchemaVersion != 2 || animationManifest.SchemaVersion != 1 || actionMap.SchemaVersion != 2 || tierMap.SchemaVersion != 2 || surfaceMap.SchemaVersion != 1)
            throw new InvalidOperationException("Unsupported visual configuration schema.");
        if (cardMap.GameplayChanges || animationManifest.GameplayChanges || tierMap.GameplayChanges)
            throw new InvalidOperationException("Cosmetic configuration cannot declare gameplay changes.");
        if (!actionMap.GameplayTimingLocked)
            throw new InvalidOperationException("Action profiles must remain locked to original gameplay timing.");
        if (!tierMap.DefaultPolicy.CardIdentityFirst || tierMap.DefaultPolicy.DamageSelectsBaseAnimation || tierMap.DefaultPolicy.HitCountSelectsBaseAnimation)
            throw new InvalidOperationException("Card identity must select the base animation; damage and hit count may only select variants.");
        if (!tierMap.DefaultPolicy.LowFlashAvailable)
            throw new InvalidOperationException("The presentation system must provide a low-flash mode.");

        CardAnimationPolicy policy = animationManifest.Policy;
        if (!policy.CardIdentityIsPrimaryKey || !policy.AllDamageCardsRequireUniqueTimeline || policy.DamageSelectsBaseAnimation || policy.HitCountSelectsBaseAnimation || !policy.DamageAndHitsAreVariantParametersOnly)
            throw new InvalidOperationException("The card animation policy is not card-identity-first.");

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

        HashSet<string> tierIds = [];
        HashSet<int> tierRanks = [];
        foreach (PresentationTier tier in tierMap.Tiers)
        {
            if (string.IsNullOrWhiteSpace(tier.Id) || !tierIds.Add(tier.Id))
                throw new InvalidOperationException($"Missing or duplicate presentation tier: {tier.Id}");
            if (!tierRanks.Add(tier.Rank))
                throw new InvalidOperationException($"Duplicate presentation tier rank: {tier.Rank}");
            if (tier.MaxDurationMs is < 200 or > 2000)
                throw new InvalidOperationException($"Presentation tier {tier.Id} has an invalid duration.");
            foreach (string profileId in tier.DevelopmentFallbackProfiles)
                if (!actionIds.Contains(profileId))
                    throw new InvalidOperationException($"Presentation tier {tier.Id} references unknown fallback action {profileId}.");
        }
        if (!tierIds.Contains(tierMap.DefaultPolicy.FastModeCap))
            throw new InvalidOperationException("Presentation policy references an unknown fast-mode tier cap.");

        HashSet<string> animationIds = [];
        HashSet<string> animationCardIds = [];
        HashSet<string> allowedModes = policy.AllowedAnimationModes.ToHashSet(StringComparer.Ordinal);
        HashSet<string> requiredDamageVariants = policy.RequiredVariantsForDamageCards.ToHashSet(StringComparer.Ordinal);
        foreach (CardAnimationSpec animation in animationManifest.Animations)
        {
            if (string.IsNullOrWhiteSpace(animation.CardId) || !animationCardIds.Add(animation.CardId))
                throw new InvalidOperationException($"Missing or duplicate card animation mapping: {animation.CardId}");
            if (string.IsNullOrWhiteSpace(animation.AnimationId) || !animationIds.Add(animation.AnimationId))
                throw new InvalidOperationException($"Missing or duplicate animation id: {animation.AnimationId}");
            if (!allowedModes.Contains(animation.AnimationMode))
                throw new InvalidOperationException($"Card {animation.CardId} uses unsupported animation mode {animation.AnimationMode}.");
            if (!actionIds.Contains(animation.BaseActionProfile) || !tierIds.Contains(animation.PresentationTier))
                throw new InvalidOperationException($"Card {animation.CardId} references an unknown action profile or tier.");
            if (!animation.UniqueTimeline || animation.DamageRole != "variant_parameter_only" || animation.HitSync != "original_hit_events")
                throw new InvalidOperationException($"Card {animation.CardId} must own a unique timeline bound to original hit events.");
            if (animation.Fallback != "original" || animation.Sequence.Count < 3)
                throw new InvalidOperationException($"Card {animation.CardId} lacks a safe or sufficiently rich animation sequence.");
            if (animation.IsDamageCard && !requiredDamageVariants.IsSubsetOf(animation.Variants))
                throw new InvalidOperationException($"Damage card {animation.CardId} lacks required variants.");
        }

        HashSet<string> surfaceIds = [];
        foreach (PresentationSurface surface in surfaceMap.Surfaces)
        {
            if (string.IsNullOrWhiteSpace(surface.Id) || !surfaceIds.Add(surface.Id))
                throw new InvalidOperationException($"Missing or duplicate presentation surface: {surface.Id}");
            if (surface.Required && string.IsNullOrWhiteSpace(surface.Fallback))
                throw new InvalidOperationException($"Presentation surface {surface.Id} requires a fallback.");
        }

        HashSet<string> cardIds = [];
        foreach (CardVisualSpec card in cardMap.Cards)
        {
            if (string.IsNullOrWhiteSpace(card.CardId) || !cardIds.Add(card.CardId))
                throw new InvalidOperationException($"Missing or duplicate card mapping: {card.CardId}");
            if (!actionIds.Contains(card.ActionProfile) || !tierIds.Contains(card.PresentationTier))
                throw new InvalidOperationException($"Card {card.CardId} references an unknown action or tier.");
            if (!animationCardIds.Contains(card.CardId))
                throw new InvalidOperationException($"Card {card.CardId} has no card-specific animation manifest entry.");
            CardAnimationSpec animation = animationManifest.Animations.Single(item => item.CardId == card.CardId);
            if (card.AnimationId != animation.AnimationId || card.AnimationMode != animation.AnimationMode || card.IsDamageCard != animation.IsDamageCard)
                throw new InvalidOperationException($"Card {card.CardId} disagrees with its animation manifest entry.");
            if (card.IsDamageCard && (!card.SpecialAnimationRequired || !animation.UniqueTimeline))
                throw new InvalidOperationException($"Damage card {card.CardId} must require a unique special animation.");
            if (card.LegalStatus != "original_required")
                throw new InvalidOperationException($"Card {card.CardId} must require original project art.");
        }
        if (!cardIds.SetEquals(animationCardIds))
            throw new InvalidOperationException("Card map and animation manifest must cover the same card IDs.");
    }
}
