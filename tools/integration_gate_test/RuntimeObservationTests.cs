using System.Reflection;
using SasukeIronclad.SasukeIroncladCode.Adapters;
using SasukeIronclad.SasukeIroncladCode.Runtime;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.IntegrationGateTest;

internal static class RuntimeObservationTests
{
    private static readonly string[] BindingIds =
    [
        "card_visual_request", "original_impact", "state_removed", "form_removed",
        "combat_ended", "character_state", "card_art", "hand", "deck_list",
        "reward", "compendium", "tooltip",
    ];

    private static readonly string[] ActiveCards =
    [
        "MegaCrit.Sts2.Core.Models.Cards.StrikeIronclad",
        "MegaCrit.Sts2.Core.Models.Cards.DefendIronclad",
        "MegaCrit.Sts2.Core.Models.Cards.Bash",
        "MegaCrit.Sts2.Core.Models.Cards.Anger",
        "MegaCrit.Sts2.Core.Models.Cards.Thunderclap",
        "MegaCrit.Sts2.Core.Models.Cards.FlameBarrier",
        "MegaCrit.Sts2.Core.Models.Cards.Whirlwind",
        "MegaCrit.Sts2.Core.Models.Cards.BurningPact",
        "MegaCrit.Sts2.Core.Models.Cards.DemonForm",
        "MegaCrit.Sts2.Core.Models.Cards.FiendFire",
    ];

    public static void Run(DateTimeOffset now)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        MethodInfo method = typeof(ObservationFixture).GetMethod(
            nameof(ObservationFixture.Observe),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("observation fixture method is missing");
        RuntimeBuildFingerprint runtime = new(
            "public-beta",
            "123456",
            new string('a', 64),
            assembly.ManifestModule.ModuleVersionId.ToString("D").ToLowerInvariant(),
            "v3.3.7");
        RuntimeObservationManifestMap manifest = CreateManifest(runtime, method, now);
        GameIntegrationProfile profile = CreatePendingProfile(runtime, now);
        RuntimeObservationOptIn optIn = CreateOptIn(runtime);

        RuntimeObservationGateResult enabled = RuntimeObservationGate.Evaluate(
            manifest, profile, optIn, runtime, now);
        Expect(enabled.Enabled, "exact-build observation marker was not accepted");
        Expect(enabled.MaxEvents == 500, "observation max event limit was not forwarded");
        Expect(enabled.MaxStackFrames == 4, "observation stack policy was not forwarded");

        RuntimeObservationGateResult noOptIn = RuntimeObservationGate.Evaluate(
            manifest,
            profile,
            CreateOptIn(runtime, enabled: false),
            runtime,
            now);
        Expect(!noOptIn.Enabled, "observation enabled without explicit opt-in");

        RuntimeObservationGateResult mismatch = RuntimeObservationGate.Evaluate(
            manifest,
            profile,
            optIn,
            runtime with { SteamBuildId = "123457" },
            now);
        Expect(!mismatch.Enabled, "mismatched build unexpectedly enabled observation");

        RuntimeObservationManifestMap staleManifest = CreateManifest(runtime, method, now - TimeSpan.FromHours(73));
        GameIntegrationProfile staleProfile = CreatePendingProfile(runtime, now - TimeSpan.FromHours(73));
        RuntimeObservationGateResult stale = RuntimeObservationGate.Evaluate(
            staleManifest,
            staleProfile,
            optIn,
            runtime,
            now);
        Expect(!stale.Enabled, "stale beta proof unexpectedly enabled observation");
        Expect(stale.Reasons.Any(reason => reason.Contains("stale", StringComparison.OrdinalIgnoreCase)),
            "stale observation proof did not produce a clear reason");

        RuntimeObservationResolutionResult resolved = RuntimeObservationTargetResolver.Resolve(assembly, manifest);
        Expect(resolved.Success && resolved.Targets.Count == 1, "exact observation MethodDef was not resolved");

        InMemorySink directSink = new("memory://direct.jsonl");
        using (RuntimeObservationSession session = new(
                   manifest,
                   runtime,
                   optIn,
                   enabled.MaxEvents,
                   enabled.MaxStackFrames,
                   directSink))
        {
            object?[] args = [42, "sensitive localized title"];
            RuntimeObservationCallState state = session.BeginInvocation(
                resolved.Targets[0], method, instance: null, args);
            session.EndInvocation(state, method, instance: null, args);
        }
        Expect(directSink.Events.Count == 4, "observation session did not write start, enter, return and stop events");
        RuntimeObservationEvent enter = directSink.Events.Single(item => item.Phase == "enter");
        Expect(enter.Arguments[0].SafeValue == "42", "numeric observation value was not summarized safely");
        Expect(enter.Arguments[1].Type == typeof(string).FullName, "string argument type was not recorded");
        Expect(enter.Arguments[1].SafeValue is null, "raw string argument value leaked into observation output");
        Expect(enter.CallerStack.All(frame => !frame.Contains('/') && !frame.Contains('\\')),
            "observation stack leaked a file path");

        RecordingObservationPatcher patcher = new();
        InMemorySink? bootstrapSink = null;
        RuntimeObservationBootstrapResult bootstrapped = RuntimeObservationBootstrap.Start(
            manifest,
            profile,
            optIn,
            new FixedProvider(new(true, runtime, ["fixture"])),
            assembly,
            patcher,
            _ => bootstrapSink = new InMemorySink("memory://bootstrap.jsonl"),
            now);
        Expect(bootstrapped.Enabled, "valid observation bootstrap did not install probes");
        Expect(patcher.InstallCount == 1 && patcher.TargetCount == 1, "observation patcher was not invoked exactly once");
        Expect(!string.IsNullOrWhiteSpace(bootstrapped.SessionId), "observation bootstrap did not return a session ID");
        Expect(bootstrapped.OutputPath == "memory://bootstrap.jsonl", "observation output path was not forwarded");
        patcher.Reset();
        Expect(bootstrapSink?.Disposed == true, "observation bootstrap session was not disposed on reset");

        RecordingObservationPatcher throwingPatcher = new() { ThrowOnInstall = true };
        RuntimeObservationBootstrapResult failed = RuntimeObservationBootstrap.Start(
            manifest,
            profile,
            optIn,
            new FixedProvider(new(true, runtime, ["fixture"])),
            assembly,
            throwingPatcher,
            _ => new InMemorySink("memory://failure.jsonl"),
            now);
        Expect(!failed.Enabled, "throwing observation patcher was reported as enabled");
        Expect(throwingPatcher.ResetCount >= 2, "failed observation install did not reset patches");

        Console.WriteLine(
            "RUNTIME_OBSERVATION_GATE_OK exact=true opt_in_required=true mismatch=false stale=false " +
            "privacy=true resolver=true bootstrap=true fail_closed=true");
    }

    private static RuntimeObservationManifestMap CreateManifest(
        RuntimeBuildFingerprint runtime,
        MethodInfo method,
        DateTimeOffset attestedAt)
    {
        return new RuntimeObservationManifestMap
        {
            SchemaVersion = 1,
            Status = "observation_only",
            GameplayChanges = false,
            ProfileId = "public-beta-fixture",
            Branch = runtime.Branch,
            Fingerprint = new RuntimeObservationFingerprintSpec
            {
                SteamBuildId = runtime.SteamBuildId,
                Sts2Sha256 = runtime.Sts2Sha256,
                ModuleMvid = runtime.ModuleMvid,
                BaseLibVersion = runtime.BaseLibVersion,
                BaseLibManifestSha256 = new string('b', 64),
            },
            BetaAttestation = new RuntimeObservationBetaAttestationSpec
            {
                Status = "verified",
                CheckedAtUtc = attestedAt.ToUniversalTime().ToString("O"),
                Source = "steamcmd_app_info_print",
                SteamCmdOutputSha256 = new string('d', 64),
                MaxAgeHours = 72,
            },
            Policy = new RuntimeObservationPolicySpec
            {
                DefaultEnabled = false,
                ReadOnlyOnly = true,
                ExactFingerprintRequired = true,
                FailClosedOnTargetMismatch = true,
                UnpatchOnReset = true,
                OptInFileName = "SasukeIronclad.observe.json",
                OutputDirectoryName = "observation-output",
                DefaultMaxEvents = 1000,
                MinimumMaxEvents = 100,
                MaximumMaxEvents = 50000,
                MaximumStackFrames = 4,
                CaptureArgumentValues = false,
                CaptureAbsolutePaths = false,
            },
            RequiredBindingIds = BindingIds.ToList(),
            ActiveCardModelTypes = ActiveCards.ToList(),
            Targets =
            [
                new RuntimeObservationTargetSpec
                {
                    Id = "fixture.observe",
                    BindingIds = BindingIds.ToList(),
                    Purpose = "Exercise exact-build read-only observation behavior.",
                    DeclaringType = method.DeclaringType?.FullName ?? string.Empty,
                    MethodSignature = AuditedMethodBindingResolver.FormatMethodSignature(method),
                    MetadataToken = $"0x{method.MetadataToken:X8}",
                    CaptureStack = true,
                    Required = true,
                },
            ],
        };
    }

    private static GameIntegrationProfile CreatePendingProfile(
        RuntimeBuildFingerprint runtime,
        DateTimeOffset attestedAt)
    {
        return new GameIntegrationProfile
        {
            Id = "public-beta-fixture",
            Status = "pending_review",
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
                Branch = runtime.Branch,
                InstalledBuildId = runtime.SteamBuildId,
                RemoteBuildId = runtime.SteamBuildId,
                CheckedAtUtc = attestedAt.ToUniversalTime().ToString("O"),
                Source = "steamcmd_app_info_print",
                SteamCmdOutputSha256 = new string('d', 64),
            },
        };
    }

    private static RuntimeObservationOptIn CreateOptIn(
        RuntimeBuildFingerprint runtime,
        bool enabled = true)
    {
        return new RuntimeObservationOptIn
        {
            SchemaVersion = 1,
            Enabled = enabled,
            Mode = "read_only",
            ExpectedBranch = runtime.Branch,
            ExpectedBuildId = runtime.SteamBuildId,
            SessionLabel = "fixture-run",
            MaxEvents = 500,
            CaptureStacks = true,
        };
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static class ObservationFixture
    {
        public static int Observe(int value, string title) => value + title.Length;
    }

    private sealed class FixedProvider(RuntimeBuildFingerprintCollectionResult result) : IRuntimeBuildFingerprintProvider
    {
        public RuntimeBuildFingerprintCollectionResult Collect() => result;
    }

    private sealed class InMemorySink(string outputPath) : IRuntimeObservationSink
    {
        public List<RuntimeObservationEvent> Events { get; } = [];
        public bool Disposed { get; private set; }
        public string OutputPath { get; } = outputPath;

        public void Write(RuntimeObservationEvent observationEvent)
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(InMemorySink));
            Events.Add(observationEvent);
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class RecordingObservationPatcher : IRuntimeObservationPatcher
    {
        private RuntimeObservationSession? _session;

        public int ResetCount { get; private set; }
        public int InstallCount { get; private set; }
        public int TargetCount { get; private set; }
        public bool ThrowOnInstall { get; init; }
        public string? SessionId => _session?.SessionId;
        public string? OutputPath => _session?.OutputPath;

        public void Install(
            RuntimeObservationSession session,
            IReadOnlyList<ResolvedRuntimeObservationTarget> targets)
        {
            InstallCount++;
            TargetCount = targets.Count;
            if (ThrowOnInstall)
            {
                session.Dispose();
                throw new InvalidOperationException("fixture observation install failure");
            }
            _session = session;
        }

        public void Reset()
        {
            ResetCount++;
            RuntimeObservationSession? session = Interlocked.Exchange(ref _session, null);
            session?.Dispose();
        }
    }
}
