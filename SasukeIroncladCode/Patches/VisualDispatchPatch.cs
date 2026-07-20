namespace SasukeIronclad.SasukeIroncladCode.Patches;

/// <summary>
/// Reserved for the future card-play visual dispatcher.
/// Do not add HarmonyPatch attributes until the exact current-game target,
/// method signature, multiplayer behavior and fallback are documented.
/// </summary>
internal static class VisualDispatchPatch
{
    internal static bool IsReady => false;
}
