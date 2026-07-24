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
    private readonly string _modAssemblyPath;
    private readonly RuntimeCanaryEventJournal? _journal;
    private readonly HashSet<string> _journaledTitleKeys = new(StringComparer.Ordinal);
    private readonly SceneTree? _sceneTree;
    private readonly GodotVisualSceneHost? _sceneHost;
    private readonly CardVisualPlaybackService? _playback;
    private readonly CardTitlePresentationService? _titleService;
    private readonly RuntimeOriginalVisualReplacementController? _replacementController;

    private RuntimePlayerAnchorResolution? _lastAnchorResolution;
    private object? _activeCardModel;
    private string? _activeCardId;
    private int _activeImpactIndex;
    private int _combatIndex;
    private bool _combatActive;
    private bool _flameBarrierStateInstalled;
    private bool _replacementDisabledForCombat;
    private int _disposed;

    public RuntimeCanarySession(
        CurrentBetaCardScopeMap scope,
        RuntimeCanaryOptIn optIn,
        string modAssemblyPath)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(optIn);
        if (string.IsNullOrWhiteSpace(modAssemblyPath))
            throw new ArgumentException("Mod assembly path is required for canary diagnostics.", nameof(modAssemblyPath));

        _optIn = optIn;
        _modAssemblyPath = modAssemblyPath;
        _cardIdByModelType = scope.ActiveCards.ToDictionary(card => card.ModelType, card => card.CardId, StringComparer.Ordinal);
        _journal = RuntimeCanaryEventJournal.TryCreate(modAssemblyPath, optIn);

        if (optIn.EnableTitles)
            _titleService = new CardTitlePresentationService(new CardDisplayNameResolver());

        if (optIn.EnableAnimations)
        {
            _sceneTree = Engine.GetMainLoop() as SceneTree
                ?? throw new InvalidOperationException("Godot SceneTree is unavailable for the runtime canary.");
            GodotVisualSceneHost host = new()
            {
                Name = "SasukeIroncladRuntimeCanaryHost",
            };
            host.ConfigureFailureDiagnostics(_modAssemblyPath, optIn);
            _sceneTree.Root.AddChild(host);
            _sceneHost = host;
            _playback = new CardVisualPlaybackService(host, new PreserveOriginalAnimationFallback());
            _playback.FallbackActivated += OnPlaybackFallback;
            host.AnchorInvalidated += OnAnchorInvalidated;
            host.PlaybackCompleted += OnPlaybackCompleted;

            if (optIn.HideOriginalVisual)
            {
                _replacementController = new RuntimeOriginalVisualReplacementController();
                WriteReplacementStatus();
            }

            RuntimePlayerAnchorResolution waiting = CreateWaitingResolution();
            _lastAnchorResolution = waiting;
            RuntimeCanaryLocalFiles.WriteAnchorStatus(
                _modAssemblyPath,
                _optIn,
                attempted: false,
                waiting,
                host,
                originalVisualHidden: false);
        }

        JournalEvent(
            "session_start",
            animationsEnabled: optIn.EnableAnimations,
            titlesEnabled: optIn.EnableTitles,
            replacementRequested: optIn.HideOriginalVisual,
            reason: "exact_build_combined_presentation_canary");
    }

    public string? SessionId => _journal?.SessionId;
    public string? EventFileName => _journal?.EventFileName;

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
                    HandleCardVisualRequest(instance, args);
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
            JournalEvent("adapter_exception", bindingId: target.Decision.BindingId, reason: "canary_adapter_exception");
            // Every canary adapter is postfix-only and cosmetic. An adapter
            // failure must leave the already-completed original method intact.
            FailReplacementForCombat("canary_adapter_exception");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        RestoreOriginalVisual("session_dispose");
        try
        {
            if (_playback is not null)
                _playback.FallbackActivated -= OnPlaybackFallback;
        }
        catch { }
        try
        {
            if (_sceneHost is not null)
            {
                _sceneHost.AnchorInvalidated -= OnAnchorInvalidated;
                _sceneHost.PlaybackCompleted -= OnPlaybackCompleted;
            }
        }
        catch { }
        try
        {
            _playback?.Dispose();
        }
        catch { }
        try
        {
            _replacementController?.Dispose();
            WriteReplacementStatus();
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
        _combatActive = false;
        JournalEvent("session_stop", reason: "session_dispose");
        try { _journal?.Dispose(); } catch { }
    }

    private void HandleCardVisualRequest(object? callbackInstance, object?[]? args)
    {
        if (_playback is null || _sceneHost is null || _sceneTree is null)
            return;
        if (args is null || args.Length < 3)
        {
            JournalEvent("card_play_rejected", bindingId: "card_visual_request", reason: "local_card_play_callback_shape_invalid");
            FailReplacementForCombat("local_card_play_callback_shape_invalid");
            return;
        }

        if (_combatActive && _sceneHost.RuntimeAnchorExitedSinceBind)
        {
            ReleaseCombatResourcesCore(
                "runtime_anchor_left_scene_tree",
                "runtime_anchor_left_scene_tree_before_next_local_card");
        }

        object? model = args[2];
        if (!TryResolveCardId(model, out string cardId))
        {
            JournalEvent("unreviewed_card_fallback", bindingId: "card_visual_request", reason: "card_not_in_reviewed_replacement_scope");
            // Once the original character is hidden, an unreviewed card cannot
            // be allowed to lose its original animation silently. Restore the
            // Ironclad and remain in original-visual fallback for this combat.
            FailReplacementForCombat("card_not_in_reviewed_replacement_scope");
            return;
        }

        EnsureCombatStarted(cardId);
        bool upgraded = TryReadBoolean(args.ElementAtOrDefault(1), "IsShowingUpgradedCard") ??
                        TryReadBoolean(model, "IsUpgraded") ??
                        TryReadBoolean(model, "Upgraded") ?? false;
        JournalEvent(
            "card_play_requested",
            bindingId: "card_visual_request",
            cardId: cardId,
            upgraded: upgraded);

        // Demon Form installs a persistent form. It remains title-only until a
        // targeted run proves the exact form-removal event. Replacement mode
        // therefore restores the original character instead of hiding a card
        // presentation for which no reviewed Sasuke timeline may be committed.
        if (string.Equals(cardId, DemonFormCardId, StringComparison.Ordinal))
        {
            JournalEvent("blocked_card_fallback", cardId: cardId, reason: "demon_form_replacement_remains_blocked");
            FailReplacementForCombat("demon_form_replacement_remains_blocked");
            return;
        }

        if (_optIn.HideOriginalVisual && _replacementDisabledForCombat)
        {
            JournalEvent("card_play_original_fallback", cardId: cardId, reason: "replacement_disabled_for_current_combat");
            return;
        }

        if (!EnsureLocalPlayerAnchor(callbackInstance, args))
        {
            JournalEvent("anchor_resolution_failed", cardId: cardId, reason: "verified_local_player_anchor_unavailable");
            ClearActiveCardState();
            return;
        }

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
            JournalEvent("playback_request_failed", cardId: cardId, reason: "reviewed_timeline_request_failed");
            FailReplacementForCombat("reviewed_timeline_request_failed");
            ClearActiveCardState();
            return;
        }
        JournalEvent("playback_started", cardId: cardId, animationId: handle.AnimationId, upgraded: upgraded);

        if (!TryActivateReplacement(cardId, handle.AnimationId))
        {
            try { _playback.Release(cardId); } catch { }
            ClearActiveCardState();
            return;
        }

        _activeCardModel = model;
        _activeCardId = cardId;
        _activeImpactIndex = 0;
        // Consume an armed post-hide failure in the same managed callback that
        // hid the original visual. Godot's per-frame C# dispatcher is retained
        // as a fallback, but recovery safety must not depend on that callback
        // successfully crossing the MonoMod JIT boundary.
        if (_sceneHost.TryProcessPendingFailureInjection())
            return;
        if (string.Equals(cardId, FlameBarrierCardId, StringComparison.Ordinal))
            _flameBarrierStateInstalled = true;
    }

    private void EnsureCombatStarted(string cardId)
    {
        if (_combatActive)
            return;
        _combatActive = true;
        _combatIndex++;
        JournalEvent("combat_started", bindingId: "card_visual_request", cardId: cardId);
    }

    private bool EnsureLocalPlayerAnchor(object? callbackInstance, object?[] args)
    {
        if (_sceneHost is null || _sceneTree is null || !_optIn.AnchorToLocalPlayer)
            return false;
        if (_sceneHost.IsAnchorBound)
        {
            if (_replacementController?.Active == true && !_replacementController.ValidateActiveTarget())
            {
                JournalEvent("replacement_target_invalid", reason: "original_visual_state_changed_outside_controller");
                FailReplacementForCombat("original_visual_state_changed_outside_controller");
                return false;
            }
            return true;
        }

        RuntimePlayerAnchorResolution resolution = RuntimePlayerVisualAnchorResolver.Resolve(
            _sceneTree,
            callbackInstance,
            args);
        _lastAnchorResolution = resolution;
        if (!resolution.Success || resolution.Anchor is null)
        {
            _sceneHost.ClearAnchor();
            WriteAnchorStatus(resolution, attempted: true);
            JournalEvent("anchor_resolution_failed", reason: string.Join("; ", resolution.Reasons.Take(3)));
            return false;
        }

        bool bound = _sceneHost.BindToAnchor(
            resolution.Anchor,
            new Vector2(_optIn.AnchorOffsetX, _optIn.AnchorOffsetY),
            _optIn.AnchorScale);
        RuntimePlayerAnchorResolution finalResolution = bound
            ? resolution
            : new RuntimePlayerAnchorResolution(
                false,
                null,
                resolution.Strategy,
                resolution.CandidateCount,
                resolution.LocalPlayerReferenceCount,
                resolution.Reasons.Concat(["The resolved node could not be bound by the local overlay host."]).ToArray());
        _lastAnchorResolution = finalResolution;
        WriteAnchorStatus(finalResolution, attempted: true);
        WriteReplacementStatus();
        JournalEvent(
            bound ? "anchor_bound" : "anchor_resolution_failed",
            reason: bound ? resolution.Strategy : "resolved_node_could_not_be_bound");
        return bound;
    }

    private bool TryActivateReplacement(string cardId, string animationId)
    {
        if (!_optIn.HideOriginalVisual)
        {
            JournalEvent("overlay_active", cardId: cardId, animationId: animationId);
            return true;
        }
        if (_replacementDisabledForCombat || _replacementController is null || _sceneHost is null)
            return false;

        Node2D? anchor = _sceneHost.AnchorNode;
        if (anchor is null || !_replacementController.TryHide(anchor))
        {
            JournalEvent("replacement_hide_failed", cardId: cardId, animationId: animationId, reason: "exact_local_ironclad_visual_could_not_be_hidden");
            FailReplacementForCombat("exact_local_ironclad_visual_could_not_be_hidden");
            return false;
        }

        WriteReplacementStatus();
        WriteAnchorStatus(_lastAnchorResolution ?? CreateWaitingResolution(), attempted: true);
        JournalEvent("replacement_hidden", cardId: cardId, animationId: animationId);
        return true;
    }

    private void OnPlaybackCompleted(AnimationPlaybackHandle handle)
    {
        JournalEvent("playback_completed", cardId: handle.CardId, animationId: handle.AnimationId);
    }

    private void OnPlaybackFallback(AnimationContext context, string reason)
    {
        JournalEvent("playback_fallback", cardId: context.CardId, reason: reason);
        FailReplacementForCombat($"playback_fallback:{reason}");
    }

    private void OnAnchorInvalidated(string reason)
    {
        JournalEvent("anchor_invalidated", reason: reason);
        FailReplacementForCombat($"anchor_invalidated:{reason}");
    }

    private void FailReplacementForCombat(string reason)
    {
        if (!_optIn.HideOriginalVisual)
            return;
        _replacementDisabledForCombat = true;
        JournalEvent("replacement_disabled_for_combat", cardId: _activeCardId, reason: reason);
        RestoreOriginalVisual(reason);
        try { _sceneHost?.ClearAnchor(); } catch { }
        WriteReplacementStatus();
        WriteAnchorStatus(AddReason(_lastAnchorResolution ?? CreateWaitingResolution(), reason), attempted: true);
        ClearActiveCardState();
    }

    private void RestoreOriginalVisual(string reason)
    {
        RuntimeOriginalVisualReplacementSnapshot? before = _replacementController?.Snapshot(requested: _optIn.HideOriginalVisual);
        try { _replacementController?.Restore(reason); } catch { }
        RuntimeOriginalVisualReplacementSnapshot? after = _replacementController?.Snapshot(requested: _optIn.HideOriginalVisual);
        WriteReplacementStatus();
        if (before?.Active == true)
        {
            JournalEvent(
                "replacement_restored",
                cardId: _activeCardId,
                restoreCount: after?.RestoreCount,
                reason: reason);
        }
    }

    private void WriteReplacementStatus()
    {
        if (!_optIn.HideOriginalVisual || _replacementController is null)
            return;
        RuntimeCanaryLocalFiles.WriteReplacementStatus(
            _modAssemblyPath,
            _optIn,
            _replacementController.Snapshot(requested: true),
            _sceneHost);
    }

    private void WriteAnchorStatus(RuntimePlayerAnchorResolution resolution, bool attempted)
    {
        RuntimeCanaryLocalFiles.WriteAnchorStatus(
            _modAssemblyPath,
            _optIn,
            attempted,
            resolution,
            _sceneHost,
            originalVisualHidden: _replacementController?.Active == true);
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
        int impactIndex = _activeImpactIndex++;
        _playback.RaiseOriginalImpact(_activeCardId, impactIndex);
        JournalEvent(
            "original_impact_forwarded",
            bindingId: "original_impact",
            cardId: _activeCardId,
            impactIndex: impactIndex);
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
        JournalEvent("state_cleared", bindingId: "state_removed", cardId: FlameBarrierCardId, reason: FlameBarrierStateId);
    }

    private void ReleaseCombatResources()
    {
        ReleaseCombatResourcesCore("combat_ended", "combat_resources_released");
    }

    private void ReleaseCombatResourcesCore(string restoreReason, string journalReason)
    {
        RestoreOriginalVisual(restoreReason);
        try { _playback?.ReleaseCombatResources(); } catch { }
        WriteReplacementStatus();
        WriteAnchorStatus(
            AddReason(_lastAnchorResolution ?? CreateWaitingResolution(), "Combat resources released and the original visual was restored."),
            attempted: _lastAnchorResolution is not null);
        JournalEvent("combat_ended", bindingId: "combat_ended", reason: journalReason);
        _lastAnchorResolution = null;
        _replacementDisabledForCombat = false;
        _combatActive = false;
        ClearActiveCardState();
        _flameBarrierStateInstalled = false;
    }

    private void ClearActiveCardState()
    {
        _activeCardModel = null;
        _activeCardId = null;
        _activeImpactIndex = 0;
    }

    private static RuntimePlayerAnchorResolution CreateWaitingResolution() => new(
        false,
        null,
        "waiting_for_local_card_play",
        0,
        0,
        ["The animation overlay is hidden until a local card-play callback proves a unique local-player visual anchor."]);

    private static RuntimePlayerAnchorResolution AddReason(
        RuntimePlayerAnchorResolution resolution,
        string reason) => new(
        resolution.Success,
        resolution.Anchor,
        resolution.Strategy,
        resolution.CandidateCount,
        resolution.LocalPlayerReferenceCount,
        resolution.Reasons.Concat([reason]).TakeLast(12).ToArray());

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

        bool presented = _titleService.TryPresent(
            new GodotCardTitleSurfaceAdapter(label, surfaceId),
            surfaceId,
            cardId,
            locale,
            upgraded,
            originalTitle);
        if (!presented)
            return;

        string titleKey = $"{surfaceId}|{cardId}|{upgraded}";
        if (_journaledTitleKeys.Add(titleKey))
        {
            JournalEvent(
                "title_applied",
                bindingId: surfaceId,
                cardId: cardId,
                surfaceId: surfaceId,
                upgraded: upgraded);
        }
    }

    private void JournalEvent(
        string eventType,
        string? bindingId = null,
        string? cardId = null,
        string? animationId = null,
        string? surfaceId = null,
        int? impactIndex = null,
        bool? upgraded = null,
        bool? animationsEnabled = null,
        bool? titlesEnabled = null,
        bool? replacementRequested = null,
        int? restoreCount = null,
        string? reason = null)
    {
        RuntimeOriginalVisualReplacementSnapshot? snapshot = _replacementController?.Snapshot(
            requested: _optIn.HideOriginalVisual);
        _journal?.Write(new RuntimeCanaryEventData(
            eventType,
            _combatIndex,
            BindingId: bindingId,
            CardId: cardId,
            AnimationId: animationId,
            SurfaceId: surfaceId,
            ImpactIndex: impactIndex,
            Upgraded: upgraded,
            AnimationsEnabled: animationsEnabled,
            TitlesEnabled: titlesEnabled,
            ReplacementRequested: replacementRequested,
            ReplacementActive: snapshot?.Active,
            AnchorBound: _sceneHost?.IsAnchorBound,
            OverlayVisible: _sceneHost?.Visible,
            RestoreCount: restoreCount ?? snapshot?.RestoreCount,
            Reason: reason));
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
