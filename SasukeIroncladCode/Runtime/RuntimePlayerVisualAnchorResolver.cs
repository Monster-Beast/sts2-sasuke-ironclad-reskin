using System.Collections;
using System.Reflection;
using Godot;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimePlayerAnchorResolution(
    bool Success,
    Node2D? Anchor,
    string Strategy,
    int CandidateCount,
    int LocalPlayerReferenceCount,
    IReadOnlyList<string> Reasons
);

/// <summary>
/// Finds the local player's combat visual node without selecting an enemy or a
/// remote player. Resolution is bounded, read-only and deliberately fails when
/// the local player relationship is ambiguous.
/// </summary>
public static class RuntimePlayerVisualAnchorResolver
{
    private const string PlayerTypeName = "MegaCrit.Sts2.Core.Entities.Players.Player";
    private const string CreatureVisualsTypeName = "MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals";
    private const int MaxSceneNodes = 4096;
    private const int MaxReferenceObjects = 256;
    private const int MaxReferenceDepth = 4;

    private static readonly string[] RelationshipMembers =
    [
        "Player", "LocalPlayer", "Owner", "Creature", "Source", "Target", "Entity", "Model",
        "PlayerChoiceContext", "Context", "CombatState", "PlayerCombatState", "Visuals",
        "_player", "_localPlayer", "_owner", "_creature", "_source", "_target", "_entity", "_model",
        "_playerChoiceContext", "_context", "_combatState", "_playerCombatState", "_visuals"
    ];

    public static RuntimePlayerAnchorResolution Resolve(
        SceneTree tree,
        object? callbackInstance,
        object?[]? callbackArguments)
    {
        ArgumentNullException.ThrowIfNull(tree);

        IReadOnlyList<object> localPlayers = FindPlayerReferences(
            callbackInstance,
            callbackArguments ?? []);
        List<AnchorCandidate> allCandidates = CollectCandidates(tree, localPlayers);

        if (allCandidates.Count == 0)
        {
            return new(
                false,
                null,
                "none",
                0,
                localPlayers.Count,
                ["No combat creature visual node with a provable Player relationship was found."]);
        }

        List<AnchorCandidate> eligible;
        string strategy;
        if (localPlayers.Count > 0)
        {
            eligible = allCandidates.Where(candidate => candidate.MatchesLocalPlayer).ToList();
            strategy = "callback_local_player_reference";
            if (eligible.Count == 0)
            {
                return new(
                    false,
                    null,
                    strategy,
                    allCandidates.Count,
                    localPlayers.Count,
                    ["Combat visual candidates were found, but none referenced the local Player object carried by the local card-play callback."]);
            }
        }
        else
        {
            eligible = allCandidates.Where(candidate => candidate.AssociatedPlayers.Count > 0).ToList();
            strategy = "single_player_visual_candidate";
            if (eligible.Count != 1)
            {
                return new(
                    false,
                    null,
                    strategy,
                    allCandidates.Count,
                    0,
                    [$"Local Player could not be recovered from the callback and {eligible.Count} Player-owned visual candidates were present; anchoring remains disabled."]);
            }
        }

        eligible = eligible
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Anchor.GetType().FullName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Anchor.Name.ToString(), StringComparer.Ordinal)
            .ToList();

        AnchorCandidate selected = eligible[0];
        if (eligible.Count > 1 && eligible[1].Score >= selected.Score - 10)
        {
            return new(
                false,
                null,
                strategy,
                allCandidates.Count,
                localPlayers.Count,
                ["Multiple similarly ranked local-player visual nodes were found; the overlay remains hidden rather than guessing an anchor."]);
        }

        if (selected.Score < 150)
        {
            return new(
                false,
                null,
                strategy,
                allCandidates.Count,
                localPlayers.Count,
                ["The best local-player visual candidate did not meet the conservative anchor confidence threshold."]);
        }

        return new(
            true,
            selected.Anchor,
            strategy,
            allCandidates.Count,
            localPlayers.Count,
            ["A unique local-player combat visual anchor was resolved without changing the original player node."]);
    }

    private static List<AnchorCandidate> CollectCandidates(SceneTree tree, IReadOnlyList<object> localPlayers)
    {
        Node root = tree.CurrentScene ?? tree.Root;
        Queue<Node> queue = new();
        queue.Enqueue(root);
        List<AnchorCandidate> candidates = [];
        int inspected = 0;

        while (queue.Count > 0 && inspected++ < MaxSceneNodes)
        {
            Node node = queue.Dequeue();
            foreach (Node child in node.GetChildren())
                queue.Enqueue(child);

            if (node is not Node2D anchor ||
                !GodotObject.IsInstanceValid(anchor) ||
                !anchor.IsInsideTree() ||
                IsModOwnedNode(anchor) ||
                !IsPotentialVisualAnchor(anchor))
            {
                continue;
            }

            IReadOnlyList<object> associatedPlayers = FindPlayerReferencesFromNodeAndParents(anchor);
            bool matchesLocal = localPlayers.Any(local =>
                associatedPlayers.Any(associated => ReferenceEquals(local, associated)));

            if (localPlayers.Count > 0 && associatedPlayers.Count > 0 && !matchesLocal)
                continue;

            int score = Score(anchor, associatedPlayers.Count, matchesLocal);
            candidates.Add(new(anchor, associatedPlayers, matchesLocal, score));
        }

        return candidates;
    }

    private static bool IsPotentialVisualAnchor(Node2D node)
    {
        string typeName = node.GetType().FullName ?? string.Empty;
        string nodeName = node.Name.ToString();
        return string.Equals(typeName, CreatureVisualsTypeName, StringComparison.Ordinal) ||
               typeName.Contains("NCreatureVisuals", StringComparison.Ordinal) ||
               nodeName.Contains("CreatureVisuals", StringComparison.OrdinalIgnoreCase) ||
               (typeName.Contains(".Nodes.Combat.", StringComparison.Ordinal) &&
                typeName.Contains("Player", StringComparison.OrdinalIgnoreCase) &&
                typeName.Contains("Visual", StringComparison.OrdinalIgnoreCase)) ||
               (nodeName.Contains("Player", StringComparison.OrdinalIgnoreCase) &&
                nodeName.Contains("Visual", StringComparison.OrdinalIgnoreCase));
    }

    private static int Score(Node2D anchor, int associatedPlayerCount, bool matchesLocalPlayer)
    {
        string typeName = anchor.GetType().FullName ?? string.Empty;
        string nodeName = anchor.Name.ToString();
        int score = 0;

        if (string.Equals(typeName, CreatureVisualsTypeName, StringComparison.Ordinal))
            score += 180;
        else if (typeName.Contains("NCreatureVisuals", StringComparison.Ordinal))
            score += 150;
        if (nodeName.Contains("CreatureVisuals", StringComparison.OrdinalIgnoreCase))
            score += 50;
        if (typeName.Contains("Player", StringComparison.OrdinalIgnoreCase))
            score += 25;
        if (nodeName.Contains("Player", StringComparison.OrdinalIgnoreCase))
            score += 25;
        if (nodeName.Contains("Ironclad", StringComparison.OrdinalIgnoreCase))
            score += 30;
        if (associatedPlayerCount == 1)
            score += 40;
        else if (associatedPlayerCount > 1)
            score -= 20;
        if (matchesLocalPlayer)
            score += 250;
        try
        {
            if (anchor.IsVisibleInTree())
                score += 10;
        }
        catch { }

        return score;
    }

    private static IReadOnlyList<object> FindPlayerReferencesFromNodeAndParents(Node node)
    {
        List<object?> seeds = [];
        Node? current = node;
        for (int depth = 0; current is not null && depth < 5; depth++, current = current.GetParent())
            seeds.Add(current);
        return FindPlayerReferences(seeds.FirstOrDefault(), seeds.Skip(1).ToArray());
    }

    private static IReadOnlyList<object> FindPlayerReferences(object? first, IReadOnlyList<object?> remaining)
    {
        Queue<(object Value, int Depth)> queue = new();
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        HashSet<object> players = new(ReferenceEqualityComparer.Instance);
        Enqueue(first, 0);
        foreach (object? value in remaining)
            Enqueue(value, 0);

        int inspected = 0;
        while (queue.Count > 0 && inspected++ < MaxReferenceObjects)
        {
            (object value, int depth) = queue.Dequeue();
            if (!visited.Add(value))
                continue;
            Type type = value.GetType();
            if (IsPlayerType(type))
            {
                players.Add(value);
                continue;
            }
            if (depth >= MaxReferenceDepth || IsSimpleValue(type))
                continue;

            if (value is IEnumerable enumerable && value is not string)
            {
                int count = 0;
                foreach (object? item in enumerable)
                {
                    Enqueue(item, depth + 1);
                    if (++count >= 32)
                        break;
                }
            }

            foreach (string memberName in RelationshipMembers)
                Enqueue(ReadMember(value, memberName), depth + 1);
        }

        return players.ToArray();

        void Enqueue(object? value, int depth)
        {
            if (value is not null && !IsSimpleValue(value.GetType()))
                queue.Enqueue((value, depth));
        }
    }

    private static bool IsPlayerType(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            string fullName = current.FullName ?? string.Empty;
            if (string.Equals(fullName, PlayerTypeName, StringComparison.Ordinal) ||
                fullName.StartsWith(PlayerTypeName + "+", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsModOwnedNode(Node node)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
        {
            string typeName = current.GetType().FullName ?? string.Empty;
            string nodeName = current.Name.ToString();
            if (typeName.StartsWith("SasukeIronclad.", StringComparison.Ordinal) ||
                nodeName.StartsWith("SasukeIronclad", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static object? ReadMember(object value, string memberName)
    {
        for (Type? current = value.GetType(); current is not null; current = current.BaseType)
        {
            try
            {
                FieldInfo? field = current.GetField(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field is not null && !field.IsStatic)
                    return field.GetValue(value);
            }
            catch { }
            try
            {
                PropertyInfo? property = current.GetProperty(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property is not null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(value);
            }
            catch { }
        }
        return null;
    }

    private static bool IsSimpleValue(Type type) =>
        type.IsPrimitive || type.IsEnum || type.IsPointer || type.IsByRef ||
        type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset);

    private sealed record AnchorCandidate(
        Node2D Anchor,
        IReadOnlyList<object> AssociatedPlayers,
        bool MatchesLocalPlayer,
        int Score
    );
}