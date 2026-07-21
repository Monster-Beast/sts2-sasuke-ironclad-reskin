using System.Reflection;
using HarmonyLib;
using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.SasukeIroncladCode.Adapters;

public interface IRuntimeObservationPatcher
{
    string? OutputPath { get; }
    void Reset();
    void Install(
        RuntimeObservationSession session,
        IReadOnlyList<ResolvedRuntimeObservationTarget> targets);
}

public sealed class HarmonyRuntimeObservationPatcher : IRuntimeObservationPatcher
{
    private const string HarmonyId = "Monster-Beast.SasukeIronclad.RuntimeObservation";
    private Harmony? _harmony;
    private RuntimeObservationSession? _session;

    public string? OutputPath => _session?.OutputPath;

    public void Install(
        RuntimeObservationSession session,
        IReadOnlyList<ResolvedRuntimeObservationTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            throw new InvalidOperationException("At least one exact observation target is required.");

        Reset();
        MethodInfo prefix = typeof(RuntimeObservationPatchBridge).GetMethod(
            nameof(RuntimeObservationPatchBridge.Prefix),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(RuntimeObservationPatchBridge), nameof(RuntimeObservationPatchBridge.Prefix));
        MethodInfo postfix = typeof(RuntimeObservationPatchBridge).GetMethod(
            nameof(RuntimeObservationPatchBridge.Postfix),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(RuntimeObservationPatchBridge), nameof(RuntimeObservationPatchBridge.Postfix));

        Harmony harmony = new(HarmonyId);
        try
        {
            RuntimeObservationPatchBridge.Configure(session, targets);
            foreach (ResolvedRuntimeObservationTarget target in targets)
            {
                harmony.Patch(
                    target.Method,
                    prefix: new HarmonyMethod(prefix),
                    postfix: new HarmonyMethod(postfix));
            }
            _harmony = harmony;
            _session = session;
        }
        catch
        {
            RuntimeObservationPatchBridge.Reset();
            try
            {
                harmony.UnpatchSelf();
            }
            catch
            {
                // A failed cosmetic observation install must not affect game startup.
            }
            session.Dispose();
            throw;
        }
    }

    public void Reset()
    {
        RuntimeObservationPatchBridge.Reset();
        Harmony? harmony = Interlocked.Exchange(ref _harmony, null);
        RuntimeObservationSession? session = Interlocked.Exchange(ref _session, null);
        try
        {
            harmony?.UnpatchSelf();
        }
        catch
        {
            // Reset must remain fail-safe during game shutdown or reload.
        }
        try
        {
            session?.Dispose();
        }
        catch
        {
            // Output cleanup must never affect game startup or shutdown.
        }
    }
}
