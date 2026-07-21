using System.Reflection;
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
    private const string ObservationManifestPath = "res://SasukeIronclad/data/runtime_observation_targets.json";
    private const string PendingProfilePath =
        "res://SasukeIronclad/data/integration_profiles/public-beta-24251656-ee45848ff631.pending-review.json";

    private static readonly HarmonyRuntimeObservationPatcher ObservationPatcher = new();
    private static int _observationResetHookInstalled;

    public const string ModId = "SasukeIronclad";
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(
        ModId,
        MegaCrit.Sts2.Core.Logging.LogType.Generic
    );

    public static GameIntegrationBootstrapResult? IntegrationStatus { get; private set; }
    public static RuntimeObservationBootstrapResult? ObservationStatus { get; private set; }

    public static void Initialize()
    {
        VisualRegistry.Initialize();
        EnsureObservationResetHook();
        ObservationStatus = EvaluateRuntimeObservation();
        IntegrationStatus = EvaluateGameIntegration();
        GD.Print($"[{ModId}] mappings={VisualRegistry.CardCount}; timelines={VisualRegistry.CardAnimationCount}; names={VisualRegistry.CardNameCount}.");
        GD.Print(
            $"[{ModId}] observation enabled={ObservationStatus.Enabled}; " +
            $"output={ObservationStatus.OutputPath ?? "none"}; " +
            $"reasons={string.Join("; ", ObservationStatus.Reasons)}"
        );
        GD.Print(
            $"[{ModId}] integration installed={IntegrationStatus.Installed}; " +
            $"visuals={IntegrationStatus.Decision.EnableVisualBindings}; " +
            $"titles={IntegrationStatus.Decision.EnableTitleBindings}; " +
            $"reasons={string.Join("; ", IntegrationStatus.Reasons)}"
        );
    }

    private static RuntimeObservationBootstrapResult EvaluateRuntimeObservation()
    {
        try
        {
            RuntimeObservationManifestMap manifest = VisualConfigLoader.Load<RuntimeObservationManifestMap>(ObservationManifestPath);
            PendingGameIntegrationProfileDocument pendingDocument =
                VisualConfigLoader.Load<PendingGameIntegrationProfileDocument>(PendingProfilePath);
            string modAssemblyPath = typeof(MainFile).Assembly.Location;
            RuntimeObservationOptInLoadResult optInLoad = RuntimeObservationLocalFiles.LoadOptIn(
                modAssemblyPath,
                manifest);
            if (!optInLoad.Found || optInLoad.OptIn is null)
                return new(false, null, null, optInLoad.Reasons);

            Assembly? gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, "sts2", StringComparison.OrdinalIgnoreCase));
            if (gameAssembly is null)
                return new(false, null, null, ["Loaded sts2 assembly was not found; observation remains disabled."]);

            return RuntimeObservationBootstrap.Start(
                manifest,
                pendingDocument.Profile,
                optInLoad.OptIn,
                new CurrentProcessRuntimeBuildFingerprintProvider(),
                gameAssembly,
                ObservationPatcher,
                sessionLabel => RuntimeObservationLocalFiles.CreateSink(
                    modAssemblyPath,
                    manifest,
                    sessionLabel));
        }
        catch (Exception exception)
        {
            ObservationPatcher.Reset();
            IReadOnlyList<string> reasons = [$"Runtime observation disabled safely: {exception.GetType().Name}."];
            return new(false, null, null, reasons);
        }
    }

    private static void EnsureObservationResetHook()
    {
        if (Interlocked.Exchange(ref _observationResetHookInstalled, 1) != 0)
            return;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => ObservationPatcher.Reset();
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
