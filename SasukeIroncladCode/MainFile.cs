using Godot;
using MegaCrit.Sts2.Core.Modding;
using SasukeIronclad.SasukeIroncladCode.Adapters;
using SasukeIronclad.SasukeIroncladCode.Runtime;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    private const string IntegrationContractPath = "res://SasukeIronclad/data/game_integration_contract.json";

    public const string ModId = "SasukeIronclad";
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(
        ModId,
        MegaCrit.Sts2.Core.Logging.LogType.Generic
    );

    public static GameIntegrationBootstrapResult? IntegrationStatus { get; private set; }

    public static void Initialize()
    {
        VisualRegistry.Initialize();
        IntegrationStatus = EvaluateGameIntegration();
        GD.Print($"[{ModId}] mappings={VisualRegistry.CardCount}; timelines={VisualRegistry.CardAnimationCount}; names={VisualRegistry.CardNameCount}.");
        GD.Print(
            $"[{ModId}] integration installed={IntegrationStatus.Installed}; " +
            $"visuals={IntegrationStatus.Decision.EnableVisualBindings}; " +
            $"titles={IntegrationStatus.Decision.EnableTitleBindings}; " +
            $"reasons={string.Join("; ", IntegrationStatus.Reasons)}"
        );
    }

    private static GameIntegrationBootstrapResult EvaluateGameIntegration()
    {
        try
        {
            GameIntegrationContractMap contract = VisualConfigLoader.Load<GameIntegrationContractMap>(IntegrationContractPath);
            return GameIntegrationBootstrap.Start(
                contract,
                new CurrentProcessRuntimeBuildFingerprintProvider(),
                new PendingGameIntegrationInstaller()
            );
        }
        catch (Exception exception)
        {
            IReadOnlyList<string> reasons = [$"Integration initialization disabled: {exception.GetType().Name}."];
            return new GameIntegrationBootstrapResult(
                new GameIntegrationDecision(false, false, null, reasons),
                false,
                reasons
            );
        }
    }
}
