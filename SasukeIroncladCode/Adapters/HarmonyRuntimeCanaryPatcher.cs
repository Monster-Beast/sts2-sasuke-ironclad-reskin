using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public sealed class HarmonyRuntimeCanaryPatcher : IRuntimeCanaryPatcher
{
    private const string HarmonyId = "Monster-Beast.SasukeIronclad.RuntimeCanary";
    private Harmony? _harmony;
    private RuntimeCanarySession? _session;
    private IReadOnlyList<string> _patchedBindingIds = [];

    public IReadOnlyList<string> PatchedBindingIds => _patchedBindingIds;

    public void Install(RuntimeCanarySession session, IReadOnlyList<ResolvedRuntimeCanaryTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            throw new InvalidOperationException("At least one reviewed canary target is required.");

        Reset();
        MethodInfo postfix = typeof(RuntimeCanaryPatchBridge).GetMethod(
            nameof(RuntimeCanaryPatchBridge.Postfix),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(RuntimeCanaryPatchBridge), nameof(RuntimeCanaryPatchBridge.Postfix));
        try
        {
            RuntimeCanaryPatchBridge.Configure(session, targets);
            _harmony = HarmonyPostfixInstaller.Install(
                HarmonyId,
                postfix,
                targets.Select(target => target.Method));
            _session = session;
            _patchedBindingIds = targets.Select(target => target.Decision.BindingId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch
        {
            RuntimeCanaryPatchBridge.Reset();
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            try { session.Dispose(); } catch { }
            _patchedBindingIds = [];
            throw;
        }
    }

    public void Reset()
    {
        RuntimeCanaryPatchBridge.Reset();
        Harmony? harmony = Interlocked.Exchange(ref _harmony, null);
        RuntimeCanarySession? session = Interlocked.Exchange(ref _session, null);
        _patchedBindingIds = [];
        try { harmony?.UnpatchAll(HarmonyId); } catch { }
        try { session?.Dispose(); } catch { }
    }
}

public static class RuntimeCanaryPatchBridge
{
    private static readonly ConcurrentDictionary<int, ResolvedRuntimeCanaryTarget> Targets = new();
    private static RuntimeCanarySession? _session;

    public static void Configure(RuntimeCanarySession session, IEnumerable<ResolvedRuntimeCanaryTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(targets);
        Targets.Clear();
        foreach (ResolvedRuntimeCanaryTarget target in targets)
            Targets[target.Method.MetadataToken] = target;
        Volatile.Write(ref _session, session);
    }

    public static void Reset()
    {
        Volatile.Write(ref _session, null);
        Targets.Clear();
    }

    public static void Postfix(MethodBase __originalMethod, object? __instance, object?[]? __args)
    {
        try
        {
            RuntimeCanarySession? session = Volatile.Read(ref _session);
            if (session is null ||
                !Targets.TryGetValue(__originalMethod.MetadataToken, out ResolvedRuntimeCanaryTarget? target))
            {
                return;
            }
            session.HandlePostfix(target, __originalMethod, __instance, __args);
        }
        catch
        {
            // Presentation callbacks are postfix-only and never affect the completed original method.
        }
    }
}
