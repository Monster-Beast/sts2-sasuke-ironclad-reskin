using System.Reflection;
using SasukeIronclad.SasukeIroncladCode.Adapters;
using SasukeIronclad.SasukeIroncladCode.Runtime;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.IntegrationGateTest;

internal static class Program
{
    private static readonly string[] VisualIds =
    [
        "card_visual_request", "original_impact", "state_removed",
        "form_removed", "combat_ended", "character_state"
    ];

    private static readonly string[] TitleIds =
        ["card_art", "hand", "deck_list", "reward", "compendium", "tooltip"];

    public static int Main()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RuntimeBuildFingerprint runtime = new(
            "public-beta",
            "123456",
            new string('a', 64),
            "11111111-2222-3333-4444-555555555555",
            "v3.1.8"
        );

        GameIntegrationDecision pendingContract = GameIntegrationGate.Evaluate(
            CreateContract("pending_local_audit", "verified", runtime, now), runtime, now);
        Expect(!pendingContract.AnyEnabled, "pending global contract unexpectedly enabled bindings");

        GameIntegrationDecision pendingProfile = GameIntegrationGate.Evaluate(
            CreateContract("verified", "pending_review", runtime, now), runtime, now);
        Expect(!pendingProfile.AnyEnabled, "pending profile unexpectedly enabled bindings");

        GameIntegrationContractMap verifiedContract = CreateContract("verified", "verified", runtime, now);
        GameIntegrationDecision enabled = GameIntegrationGate.Evaluate(verifiedContract, runtime, now);
        Expect(enabled.EnableVisualBindings && enabled.EnableTitleBindings, "verified latest-beta profile was not enabled");

        RuntimeBuildFingerprint mismatched = runtime with { Sts2Sha256 = new string('b', 64) };
        GameIntegrationDecision mismatchDecision = GameIntegrationGate.Evaluate(verifiedContract, mismatched, now);
        Expect(!mismatchDecision.AnyEnabled, "mismatched assembly hash unexpectedly enabled bindings");

        RuntimeBuildFingerprint stableRuntime = runtime with { Branch = "stable" };
        GameIntegrationDecision stableDecision = GameIntegrationGate.Evaluate(verifiedContract, stableRuntime, now);
        Expect(!stableDecision.AnyEnabled, "stable branch unexpectedly enabled latest-beta bindings");

        GameIntegrationDecision partial = GameIntegrationGate.Evaluate(
            CreateContract("verified", "verified", runtime, now, pendingTitle: "tooltip"), runtime, now);
        Expect(partial.EnableVisualBindings, "verified visual bindings should remain independently enabled");
        Expect(!partial.EnableTitleBindings, "pending title surface unexpectedly enabled title bindings");

        GameIntegrationContractMap staleContract = CreateContract(
            "verified",
            "verified",
            runtime,
            now - TimeSpan.FromHours(73));
        GameIntegrationDecision staleDecision = GameIntegrationGate.Evaluate(staleContract, runtime, now);
        Expect(!staleDecision.AnyEnabled, "stale beta attestation unexpectedly enabled bindings");
        Expect(staleDecision.Reasons.Any(reason => reason.Contains("stale", StringComparison.OrdinalIgnoreCase)),
            "stale beta attestation did not produce an explicit reason");

        GameIntegrationContractMap duplicate = CreateContract("verified", "verified", runtime, now);
        duplicate.Profiles.Add(CreateProfile("duplicate-fixture", "verified", runtime, now));
        ExpectThrows(
            () => GameIntegrationGate.Evaluate(duplicate, runtime, now),
            "Duplicate build fingerprint",
            "duplicate fingerprint was accepted"
        );

        RecordingInstaller installer = new();
        GameIntegrationBootstrapResult bootstrapped = GameIntegrationBootstrap.Start(
            verifiedContract,
            new FixedProvider(new(true, runtime, ["fixture"])),
            installer
        );
        Expect(bootstrapped.Installed, "verified exact latest-beta profile was not installed");
        Expect(installer.InstallCount == 1, "installer was not called exactly once");
        Expect(installer.LastDecision?.EnableVisualBindings == true, "visual decision was not forwarded");
        Expect(installer.LastDecision?.EnableTitleBindings == true, "title decision was not forwarded");

        RecordingInstaller pendingInstaller = new();
        GameIntegrationBootstrapResult pendingBootstrap = GameIntegrationBootstrap.Start(
            CreateContract("pending_local_audit", "verified", runtime, now),
            new FixedProvider(new(true, runtime, ["fixture"])),
            pendingInstaller
        );
        Expect(!pendingBootstrap.Installed && pendingInstaller.InstallCount == 0, "pending contract reached installer");

        RecordingInstaller failedInstaller = new() { ThrowOnInstall = true };
        GameIntegrationBootstrapResult failedBootstrap = GameIntegrationBootstrap.Start(
            verifiedContract,
            new FixedProvider(new(true, runtime, ["fixture"])),
            failedInstaller
        );
        Expect(!failedBootstrap.Installed, "throwing installer was reported as installed");
        Expect(!failedBootstrap.Decision.AnyEnabled, "throwing installer did not fail closed");
        Expect(failedInstaller.ResetCount >= 2, "throwing installer was not reset after failure");

        TestRuntimeFingerprintCollector();

        Console.WriteLine(
            "INTEGRATION_GATE_OK pending=false exact=true mismatch=false stable=false stale=false " +
            "partial_titles=false duplicate_rejected=true bootstrap=true collector=true fail_closed=true"
        );
        return 0;
    }

    private static void TestRuntimeFingerprintCollector()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sasuke-integration-{Guid.NewGuid():N}");
        try
        {
            string steamApps = Path.Combine(root, "steamapps");
            string gamePath = Path.Combine(steamApps, "common", "Slay the Spire 2");
            string dataPath = Path.Combine(gamePath, "data_sts2_windows_x86_64");
            string baseLibPath = Path.Combine(gamePath, "mods", "BaseLib");
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(baseLibPath);
            string assemblyPath = Path.Combine(dataPath, "sts2.dll");
            File.Copy(Assembly.GetExecutingAssembly().Location, assemblyPath);
            File.WriteAllText(Path.Combine(baseLibPath, "BaseLib.json"), "{\"version\":\"v3.1.8\"}");
            File.WriteAllText(
                Path.Combine(steamApps, "appmanifest_2868840.acf"),
                "\"AppState\"\n{\n  \"appid\" \"2868840\"\n  \"buildid\" \"123456\"\n  \"UserConfig\"\n  {\n    \"BetaKey\" \"public-beta\"\n  }\n}\n"
            );

            RuntimeBuildFingerprintCollectionResult collected = RuntimeBuildFingerprintCollector.Collect(
                assemblyPath,
                gamePath,
                branch: null
            );
            Expect(collected.Success && collected.Fingerprint is not null, "runtime fingerprint fixture failed");
            Expect(collected.Fingerprint!.Branch == "public-beta", "public-beta branch was not collected");
            Expect(collected.Fingerprint.SteamBuildId == "123456", "Steam buildid was not collected");
            Expect(collected.Fingerprint.BaseLibVersion == "v3.1.8", "BaseLib version was not collected");
            Expect(collected.Fingerprint.Sts2Sha256.Length == 64, "assembly SHA-256 was not collected");
            Expect(Guid.TryParse(collected.Fingerprint.ModuleMvid, out _), "assembly MVID was not collected");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static GameIntegrationContractMap CreateContract(
        string contractStatus,
        string profileStatus,
        RuntimeBuildFingerprint runtime,
        DateTimeOffset attestedAt,
        string? pendingTitle = null)
    {
        return new GameIntegrationContractMap
        {
            SchemaVersion = 1,
            GameplayChanges = false,
            Status = contractStatus,
            Policy = new GameIntegrationPolicy
            {
                ExactBuildFingerprintRequired = true,
                UnverifiedBindingsDisabled = true,
                FallbackToOriginalOnMismatch = true,
                MultiplayerLocalVisualsOnly = true,
                RequiredBranch = "public-beta",
                LatestBetaOnly = true,
                RemoteBetaAttestationRequired = true,
                StaleProfilesDisabled = true,
                MaxBetaAttestationAgeHours = 72,
            },
            RequiredVisualEvents = VisualIds.ToList(),
            RequiredTitleSurfaces = TitleIds.ToList(),
            Profiles = [CreateProfile("public-beta-fixture", profileStatus, runtime, attestedAt, pendingTitle)],
        };
    }

    private static GameIntegrationProfile CreateProfile(
        string id,
        string status,
        RuntimeBuildFingerprint runtime,
        DateTimeOffset attestedAt,
        string? pendingTitle = null)
    {
        return new GameIntegrationProfile
        {
            Id = id,
            Status = status,
            Branch = runtime.Branch,
            Fingerprint = new GameBuildFingerprintSpec
            {
                SteamBuildId = runtime.SteamBuildId,
                Sts2Sha256 = runtime.Sts2Sha256,
                ModuleMvid = runtime.ModuleMvid,
                BaseLibVersion = runtime.BaseLibVersion,
            },
            BetaAttestation = new GameBetaAttestationSpec
            {
                Status = "verified",
                Branch = "public-beta",
                InstalledBuildId = runtime.SteamBuildId,
                RemoteBuildId = runtime.SteamBuildId,
                CheckedAtUtc = attestedAt.ToUniversalTime().ToString("O"),
                Source = "steamcmd_app_info_print",
                SteamCmdOutputSha256 = new string('d', 64),
            },
            VisualBindings = VisualIds.Select((bindingId, index) => new GameVisualBindingSpec
            {
                Id = bindingId,
                DeclaringType = $"Fixture.Visual{index}",
                MethodSignature = $"void Fixture.Visual{index}.Invoke()",
                MetadataToken = $"0x{0x06000001 + index:X8}",
                Status = "verified",
                Fallback = "original_visual",
            }).ToList(),
            TitleBindings = TitleIds.Select((surfaceId, index) => new GameTitleBindingSpec
            {
                SurfaceId = surfaceId,
                DeclaringType = $"Fixture.Title{index}",
                MethodSignature = $"void Fixture.Title{index}.Refresh()",
                MetadataToken = $"0x{0x06000101 + index:X8}",
                Status = surfaceId == pendingTitle ? "pending_review" : "verified",
                Fallback = "original_title",
            }).ToList(),
        };
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void ExpectThrows(Action action, string expectedMessage, string failureMessage)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
        {
            return;
        }
        throw new InvalidOperationException(failureMessage);
    }

    private sealed class FixedProvider(RuntimeBuildFingerprintCollectionResult result) : IRuntimeBuildFingerprintProvider
    {
        public RuntimeBuildFingerprintCollectionResult Collect() => result;
    }

    private sealed class RecordingInstaller : IGameIntegrationInstaller
    {
        public int ResetCount { get; private set; }
        public int InstallCount { get; private set; }
        public bool ThrowOnInstall { get; init; }
        public GameIntegrationDecision? LastDecision { get; private set; }

        public void Reset() => ResetCount++;

        public void Install(GameIntegrationProfile profile, GameIntegrationDecision decision)
        {
            _ = profile;
            InstallCount++;
            LastDecision = decision;
            if (ThrowOnInstall)
                throw new InvalidOperationException("fixture install failure");
        }
    }
}
