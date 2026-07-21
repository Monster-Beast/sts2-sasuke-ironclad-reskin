using System.Collections;
using System.Reflection;
using Godot;
using SasukeIronclad.SasukeIroncladCode.Adapters;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed class RuntimeCanarySession : IDisposable
{
    private const string NCardTypeName = "MegaCrit.Sts2.Core.Nodes.Cards.NCard";
    private const string DemonFormCardId = "Demon Form";
    private const string FlameBarrierCardId = "Flame Barrier";
    private const string FlameBarrierStateId = "flame_barrier_guard";
    private const int MaxTraversalObjects = 512;
    private const int MaxTraversalDepth = 7;

    private static readonly string[] CardNodeMembers = ["CardNode", "_cardNode", "Card", "_card"];
    private static readonly string[] CardModelMembers = ["Model", "CardModel", "_model", "_cardModel"];
    private static readonly string[] TitleLabelMembers = ["_titleLabel", "TitleLabel"];
    private static readonly string[] TraversalMembers =
    [
        "CardNode", "_cardNode", "CardHolder", "_cardHolder", "CardHolders", "_cardHolders",
        "CurrentlyDisplayedCardHolders", "Cards", "_cards", "CardNodes", "_cardNodes", "Grid", "_grid"
    ];

    private readonly Dictionary<string, string> _cardIdByModelType;
    private readonly RuntimeCanaryOptIn _optIn;
    private readonly GodotVisualSceneHost? _sceneHost;
    private readonly CardVisualPlaybackService? _playback;
    private readonly CardTitlePresentationService? _titleService;

    private object? _activeCardModel;
    private string? _activeCardId;
    private int _activeImpactIndex;
    private bool _flameBarrierStateInstalled;
    private int _disposed;

    public RuntimeCanarySession(CurrentBetaCardScopeMap scope, RuntimeCanaryOptIn optIn)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(optIn);
        _optIn = optIn;
        _cardIdByModelType = scope.ActiveCards.ToDictionary(card => card.ModelType, card => card.CardId, StringComparer.Ordinal);

        if (optIn.EnableTitles)
            _titleService = new CardTitlePresentationService(new CardDisplayNameResolver());

        if (optIn.EnableAnimations)
        {
            SceneTree tree = Engine.GetMainLoop() as SceneTree
                ?? throw new InvalidOperationException("Godot SceneTree is unavailable for the runtime canary.");
            GodotVisualSceneHost host = new()
            {
                Name = "SasukeIroncladRuntimeCanaryHost",
            };
            tree.Root.AddChild(host);
            _sceneHost = host;
            _playback = new CardVisualPlaybackService(host, new PreserveOriginalAnimationFallback());
        }
    }

    public void HandlePostfix(
        ResolvedRuntimeCanaryTarget target,
        MethodBase originalMethod,
        object? instance,
        object?[]? args)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(originalMethod);

        try
        {
            switch (target.Decision.BindingId)
            {
                case "card_visual_request":
                    HandleCardVisualRequest(args);
                    break;
                case "original_impact":
                    HandleOriginalImpact(args);
                    break;
                case "state_removed":
                    HandleStateRemoved(instance, args);
                    break;
                case "combat_ended":
                    ReleaseCombatResources();
                    break;
                case "card_art":
                    ApplyTitleToCandidate(instance, "card_art");
                    break;
                case "hand":
                    ApplyTitlesFromSurface(instance, args, "hand");
                    break;
                case "deck_list":
                    ApplyTitlesFromSurface(instance, args, "deck_list");
                    break;
                case "reward":
                    ApplyTitlesFromSurface(instance, args, "reward");
                    break;
                case "compendium":
                    ApplyTitlesFromSurface(instance, args, "compendium");
                    break;
                case "tooltip":
                    ApplyTitlesFromSurface(instance, args, "tooltip");
                    break;
            }
        }
        catch
        {
            // Every canary adapter is postfix-only and cosmetic. An adapter
            // failure must leave the already-completed original method intact.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try
        {
            _playback?.Dispose();
        }
        catch { }
        try
        {
            if (_sceneHost is not null && GodotObject.IsInstanceValid(_sceneHost))
                _sceneHost.QueueFree();
        }
        catch { }
        _activeCardModel = null;
        _activeCardId = null;
        _flameBarrierStateInstalled = false;
    }

    private void HandleCardVisualRequest(object?[]? args)
    {
        if (_playback is null || args is null || args.Length < 3)
            return;
        object? model = args[2];
        if (!TryResolveCardId(model, out string cardId))
            return;

        // Demon Form installs a persistent form. It remains title-only until a
        // targeted run proves the exact form-removal event.
        if (string.Equals(cardId, DemonFormCardId, StringComparison.Ordinal))
            return;

        bool upgraded = TryReadBoolean(args.ElementAtOrDefault(1), "IsShowingUpgradedCard") ??
                        TryReadBoolean(model, "IsUpgraded") ??
                        TryReadBoolean(model, "Upgraded") ?? false;
        AnimationContext context = new(
            cardId,
            upgraded,
            FinalDamage: 0,
            HitCount: 1,
            TargetCount: 1,
            EnergySpent: 0,
            Strength: 0,
            ExhaustedCardCount: 0,
            IsLethal: false,
            FastMode: _optIn.FastMode,
            LowFlashMode: _optIn.LowFlash);

        AnimationPlaybackHandle? handle = _playback.Request(context);
        if (handle is null)
        {
            _activeCardModel = null;
            _activeCardId = null;
            _activeImpactIndex = 0;
            return;
        }

        _activeCardModel = model;
        _activeCardId = cardId;
        _activeImpactIndex = 0;
        if (string.Equals(cardId, FlameBarrierCardId, StringComparison.Ordinal))
            _flameBarrierStateInstalled = true;
    }

    private void HandleOriginalImpact(object?[]? args)
    {
        if (_playback is null || args is null || args.Length < 5 ||
            _activeCardModel is null || string.IsNullOrWhiteSpace(_activeCardId))
        {
            return;
        }
        object? sourceCardModel = args[4];
        if (!ReferenceEquals(sourceCardModel, _activeCardModel))
            return;
        _playback.RaiseOriginalImpact(_activeCardId, _activeImpactIndex++);
    }

    private void HandleStateRemoved(object? container, object?[]? args)
    {
        if (_sceneHost is null || !_flameBarrierStateInstalled || args is null || args.Length == 0)
            return;
        object? power = args[0];
        if (!string.Equals(power?.GetType().FullName, "MegaCrit.Sts2.Core.Models.Powers.ThornsPower", StringComparison.Ordinal))
            return;
        if (!LooksOwnedByPlayer(power) && !LooksOwnedByPlayer(container))
            return;
        _sceneHost.ClearVisualState(FlameBarrierStateId);
        _flameBarrierStateInstalled = false;
    }

    private void ReleaseCombatResources()
    {
        try { _playback?.ReleaseCombatResources(); } catch { }
        _activeCardModel = null;
        _activeCardId = null;
        _activeImpactIndex = 0;
        _flameBarrierStateInstalled = false;
    }

    private void ApplyTitlesFromSurface(object? instance, object?[]? args, string surfaceId)
    {
        if (_titleService is null)
            return;

        Queue<(object Value, int Depth)> queue = new();
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        Enqueue(instance, 0);
        if (args is not null)
        {
            foreach (object? argument in args)
                Enqueue(argument, 0);
        }

        int inspected = 0;
        while (queue.Count > 0 && inspected++ < MaxTraversalObjects)
        {
            (object value, int depth) = queue.Dequeue();
            if (!visited.Add(value))
                continue;
            ApplyTitleToCandidate(value, surfaceId);
            if (depth >= MaxTraversalDepth)
                continue;

            if (value is Node node)
            {
                foreach (Node child in node.GetChildren())
                    Enqueue(child, depth + 1);
            }
            if (value is IEnumerable enumerable && value is not string)
            {
                int count = 0;
                foreach (object? item in enumerable)
                {
                    Enqueue(item, depth + 1);
                    if (++count >= 128)
                        break;
                }
            }
            foreach (string memberName in TraversalMembers)
                Enqueue(ReadMember(value, memberName), depth + 1);
        }

        void Enqueue(object? value, int depth)
        {
            if (value is not null && !IsSimpleValue(value.GetType()))
                queue.Enqueue((value, depth));
        }
    }

    private void ApplyTitleToCandidate(object? value, string surfaceId)
    {
        if (_titleService is null || value is null)
            return;
        object? cardNode = IsTypeOrSubclass(value.GetType(), NCardTypeName)
            ? value
            : CardNodeMembers.Select(name => ReadMember(value, name)).FirstOrDefault(candidate =>
                candidate is not null && IsTypeOrSubclass(candidate.GetType(), NCardTypeName));
        if (cardNode is null)
            return;

        object? model = CardModelMembers.Select(name => ReadMember(cardNode, name)).FirstOrDefault(candidate => candidate is not null);
        if (!TryResolveCardId(model, out string cardId))
            return;
        object? label = TitleLabelMembers.Select(name => ReadMember(cardNode, name)).FirstOrDefault(candidate => candidate is not null) ??
                        ReadGodotObjectProperty(cardNode, "_titleLabel");
        if (label is null)
            return;
        string? originalTitle = ReadText(label);
        if (string.IsNullOrWhiteSpace(originalTitle))
            return;
        bool upgraded = originalTitle.EndsWith('+') ||
                        TryReadBoolean(model, "IsUpgraded") == true ||
                        TryReadBoolean(model, "Upgraded") == true;
        string locale;
        try { locale = TranslationServer.GetLocale(); }
        catch { locale = "en-US"; }

        _titleService.TryPresent(
            new GodotCardTitleSurfaceAdapter(label, surfaceId),
            surfaceId,
            cardId,
            locale,
            upgraded,
            originalTitle);
    }

    private bool TryResolveCardId(object? model, out string cardId)
    {
        cardId = string.Empty;
        string? typeName = model?.GetType().FullName;
        return typeName is not null && _cardIdByModelType.TryGetValue(typeName, out cardId!);
    }

    private static bool LooksOwnedByPlayer(object? value)
    {
        if (value is null)
            return false;
        if (IsPlayerType(value.GetType()))
            return true;
        foreach (string memberName in new[] { "Owner", "Creature", "Target", "Player", "_owner", "_creature", "_player" })
        {
            object? owner = ReadMember(value, memberName);
            if (owner is not null && IsPlayerType(owner.GetType()))
                return true;
        }
        return false;
    }

    private static bool IsPlayerType(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            string name = current.FullName ?? string.Empty;
            if (name == "MegaCrit.Sts2.Core.Entities.Players.Player" ||
                name.StartsWith("MegaCrit.Sts2.Core.Entities.Players.Player+", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTypeOrSubclass(Type type, string fullName)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
            if (string.Equals(current.FullName, fullName, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static object? ReadMember(object? value, string memberName)
    {
        if (value is null)
            return null;
        for (Type? current = value.GetType(); current is not null; current = current.BaseType)
        {
            try
            {
                PropertyInfo? property = current.GetProperty(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property is not null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(value);
            }
            catch { }
            try
            {
                FieldInfo? field = current.GetField(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field is not null)
                    return field.GetValue(value);
            }
            catch { }
        }
        return null;
    }

    private static bool? TryReadBoolean(object? value, string memberName)
    {
        object? result = ReadMember(value, memberName);
        return result is bool boolean ? boolean : null;
    }

    private static object? ReadGodotObjectProperty(object value, string propertyName)
    {
        if (value is not GodotObject godotObject)
            return null;
        try
        {
            Variant variant = godotObject.Get(propertyName);
            return variant.VariantType == Variant.Type.Object ? variant.AsGodotObject() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadText(object label)
    {
        object? value = ReadMember(label, "Text") ?? ReadMember(label, "text");
        if (value is string text)
            return text;
        if (label is GodotObject godotObject)
        {
            try { return godotObject.Get("text").AsString(); }
            catch { }
        }
        return null;
    }

    private static void WriteText(object label, string text)
    {
        for (Type? current = label.GetType(); current is not null; current = current.BaseType)
        {
            try
            {
                PropertyInfo? property = current.GetProperty(
                    "Text",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property?.CanWrite == true && property.PropertyType == typeof(string))
                {
                    property.SetValue(label, text);
                    return;
                }
            }
            catch { }
        }
        if (label is GodotObject godotObject)
        {
            try { godotObject.Set("text", text); }
            catch { }
        }
    }

    private static bool IsSimpleValue(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime);

    private sealed class GodotCardTitleSurfaceAdapter : ICardTitleSurfaceAdapter
    {
        private readonly object _label;
        private readonly string _surfaceId;

        public GodotCardTitleSurfaceAdapter(object label, string surfaceId)
        {
            _label = label;
            _surfaceId = surfaceId;
        }

        public bool CanApply(string surfaceId) =>
            string.Equals(surfaceId, _surfaceId, StringComparison.Ordinal);

        public void Apply(CardTitlePresentation presentation)
        {
            ArgumentNullException.ThrowIfNull(presentation);
            WriteText(_label, presentation.DisplayName);
        }
    }
}
