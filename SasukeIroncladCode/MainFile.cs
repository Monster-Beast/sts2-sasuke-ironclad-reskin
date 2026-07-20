using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SasukeIronclad";
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        VisualRegistry.Initialize();
        Harmony harmony = new(ModId);
        harmony.PatchAll();
        GD.Print($"[{ModId}] initialized with {VisualRegistry.CardCount} card mappings and {VisualRegistry.ActionCount} action profiles.");
    }
}
