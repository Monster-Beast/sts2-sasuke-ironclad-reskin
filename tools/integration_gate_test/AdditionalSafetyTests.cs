using System.Reflection;
using System.Runtime.CompilerServices;
using SasukeIronclad.SasukeIroncladCode.Runtime;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.IntegrationGateTest;

internal static class AdditionalSafetyTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyNonMethodTokenIsRejected();
        VerifyBetaBranchAndWorkshopBaseLibDetection();
        VerifyAuditedMethodResolution();
        VerifyFailureInjectionOneShot();
        VerifyRuntimeCanaryStartupFailureInjection();
    }

    private static void VerifyRuntimeCanaryStartupFailureInjection()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        MethodInfo method = typeof(AdditionalSafetyTests).GetMethod(
            nameof(AuditedResolverTarget),
            BindingFlags.Static | BindingFlags.NonPublic
        ) ?? throw new InvalidOperationException("Canary resolver test method was not found.");
        string signature = AuditedMethodBindingResolver.FormatMethodSignature(method);
        string token = $"0x{method.MetadataToken:X8}";
        RuntimeCanaryBindingDecision decision = new()
        {
            BindingId = RuntimeCanaryStartupFailureScenarios.TargetBindingId,
            Kind = "visual",
            Status = "approved_for_canary",
            SelectedTargetId = "fixture.card-visual-request",
            DeclaringType = method.DeclaringType?.FullName ?? string.Empty,
            MethodSignature = signature,
            MetadataToken = token,
            ObservedInAllSessions = true,
        };
        RuntimeObservationManifestMap manifest = new()
        {
            SchemaVersion = 1,
            Fingerprint = new RuntimeObservationFingerprintSpec
            {
                ModuleMvid = assembly.ManifestModule.ModuleVersionId.ToString("D"),
            },
            Targets =
            [
                new RuntimeObservationTargetSpec
                {
                    Id = decision.SelectedTargetId,
                    BindingIds = [decision.BindingId],
                    DeclaringType = decision.DeclaringType,
                    MethodSignature = signature,
                    MetadataToken = token,
                    Required = true,
                }
            ],
        };

        RuntimeCanaryResolutionResult baseline = RuntimeCanaryTargetResolver.Resolve(
            assembly,
            manifest,
            [decision]);
        if (!baseline.Success || baseline.Targets.Count != 1 || baseline.Targets[0].Method != method)
            throw new InvalidOperationException("The unmodified runtime canary baseline did not resolve.");

        RuntimeCanaryStartupFailureInjectionController controller = new(
            RuntimeCanaryStartupFailureScenarios.MethodSignatureMismatch,
            RuntimeCanaryStartupFailureScenarios.TargetBindingId);
        RuntimeCanaryResolutionResult injected = RuntimeCanaryTargetResolver.Resolve(
            assembly,
            manifest,
            [decision],
            controller);
        RuntimeCanaryStartupFailureInjectionSnapshot snapshot = controller.Snapshot();
        string expectedReason =
            $"Canary binding {RuntimeCanaryStartupFailureScenarios.TargetBindingId} method signature does not match the reviewed target ({RuntimeCanaryStartupFailureScenarios.ReasonMarker}).";
        if (injected.Success || injected.Targets.Count != 0 ||
            injected.Reasons.Count != 1 || injected.Reasons[0] != expectedReason)
        {
            throw new InvalidOperationException("The startup diagnostic did not fail at the exact signature comparison.");
        }
        if (!snapshot.Requested || snapshot.Armed || !snapshot.Triggered ||
            snapshot.TriggerCount != 1 ||
            snapshot.TriggerStage != RuntimeCanaryStartupFailureScenarios.TriggerStage ||
            !snapshot.BaselineMatchConfirmed)
        {
            throw new InvalidOperationException("The startup diagnostic snapshot lost one-shot baseline evidence.");
        }

        string unchanged = signature;
        if (controller.TryInjectMethodSignatureMismatch(
                decision.BindingId,
                signature,
                out unchanged) || unchanged != signature)
        {
            throw new InvalidOperationException("The startup diagnostic triggered more than once.");
        }
    }

    private static void VerifyFailureInjectionOneShot()
    {
        RuntimeCanaryFailureInjectionController controller = new(
            RuntimeCanaryFailureScenarios.ForcedPlaybackFailure,
            "Strike");
        if (!controller.Requested ||
            !controller.Matches(RuntimeCanaryFailureScenarios.ForcedPlaybackFailure, "Strike") ||
            controller.Matches(RuntimeCanaryFailureScenarios.ForcedPlaybackFailure, "Defend"))
        {
            throw new InvalidOperationException("Failure injection target matching is not exact.");
        }
        if (!controller.TryTrigger(
                RuntimeCanaryFailureScenarios.ForcedPlaybackFailure,
                "Strike",
                "after_replacement_hidden",
                originalVisualHiddenAtTrigger: true))
        {
            throw new InvalidOperationException("The reviewed one-shot failure could not be triggered.");
        }
        if (controller.TryTrigger(
                RuntimeCanaryFailureScenarios.ForcedPlaybackFailure,
                "Strike",
                "after_replacement_hidden",
                originalVisualHiddenAtTrigger: true))
        {
            throw new InvalidOperationException("The one-shot failure triggered more than once.");
        }
        if (!controller.ConfirmRecovery(
                recovered: true,
                transition: "original_visual_restored:fixture"))
        {
            throw new InvalidOperationException("Failure recovery was not confirmed.");
        }
        RuntimeCanaryFailureInjectionSnapshot snapshot = controller.Snapshot();
        if (!snapshot.Triggered || snapshot.TriggerCount != 1 || snapshot.Armed ||
            !snapshot.OriginalVisualHiddenAtTrigger || !snapshot.RecoveryConfirmed)
        {
            throw new InvalidOperationException("Failure injection snapshot lost one-shot recovery evidence.");
        }

        RuntimeCanaryFailureInjectionController disabled = new(
            RuntimeCanaryFailureScenarios.None,
            string.Empty);
        if (disabled.Requested || disabled.TryTrigger(
                RuntimeCanaryFailureScenarios.MissingTimeline,
                "Strike",
                "can_play",
                originalVisualHiddenAtTrigger: false))
        {
            throw new InvalidOperationException("Disabled failure injection accepted a trigger.");
        }
    }

    private static void VerifyNonMethodTokenIsRejected()
    {
        string[] visualIds =
        [
            "card_visual_request", "original_impact", "state_removed",
            "form_removed", "combat_ended", "character_state"
        ];
        string[] titleIds = ["card_art", "hand", "deck_list", "reward", "compendium", "tooltip"];
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RuntimeBuildFingerprint runtime = new(
            "public-beta",
            "654321",
            new string('c', 64),
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "v9.9.9"
        );
        GameIntegrationProfile profile = new()
        {
            Id = "method-token-fixture",
            Status = "verified",
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
                CheckedAtUtc = now.ToString("O"),
                Source = "steamcmd_app_info_print",
                SteamCmdOutputSha256 = new string('e', 64),
            },
            VisualBindings = visualIds.Select((id, index) => new GameVisualBindingSpec
            {
                Id = id,
                DeclaringType = $"Fixture.Visual{index}",
                MethodSignature = $"void Fixture.Visual{index}.Invoke()",
                MetadataToken = $"0x{0x06001001 + index:X8}",
                Status = "verified",
                Fallback = "original_visual",
            }).ToList(),
            TitleBindings = titleIds.Select((id, index) => new GameTitleBindingSpec
            {
                SurfaceId = id,
                DeclaringType = $"Fixture.Title{index}",
                MethodSignature = $"string Fixture.Title{index}.Value",
                MetadataToken = index == 0 ? "0x17000001" : $"0x{0x06002001 + index:X8}",
                Status = "verified",
                Fallback = "original_title",
            }).ToList(),
        };
        GameIntegrationContractMap contract = new()
        {
            SchemaVersion = 1,
            GameplayChanges = false,
            Status = "verified",
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
            RequiredVisualEvents = visualIds.ToList(),
            RequiredTitleSurfaces = titleIds.ToList(),
            Profiles = [profile],
        };
        GameIntegrationDecision decision = GameIntegrationGate.Evaluate(contract, runtime, now);
        if (!decision.EnableVisualBindings || decision.EnableTitleBindings)
            throw new InvalidOperationException("A non-MethodDef title token was not rejected independently.");
    }

    private static void VerifyBetaBranchAndWorkshopBaseLibDetection()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sasuke-workshop-{Guid.NewGuid():N}");
        try
        {
            string steamApps = Path.Combine(root, "steamapps");
            string gamePath = Path.Combine(steamApps, "common", "Slay the Spire 2");
            string dataPath = Path.Combine(gamePath, "data_sts2_windows_x86_64");
            string workshopPath = Path.Combine(steamApps, "workshop", "content", "2868840", "1234567890");
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(workshopPath);
            string assemblyPath = Path.Combine(dataPath, "sts2.dll");
            File.Copy(Assembly.GetExecutingAssembly().Location, assemblyPath);
            File.WriteAllText(
                Path.Combine(workshopPath, "BaseLib.json"),
                "{\"id\":\"BaseLib\",\"version\":\"v4.0.0-beta\"}"
            );
            File.WriteAllText(
                Path.Combine(steamApps, "appmanifest_2868840.acf"),
                "\"AppState\"\n{\n  \"buildid\" \"654321\"\n  \"UserConfig\"\n  {\n    \"BetaKey\" \"public-beta\"\n  }\n}\n"
            );

            RuntimeBuildFingerprintCollectionResult result = RuntimeBuildFingerprintCollector.Collect(
                assemblyPath,
                gamePath,
                branch: null
            );
            if (!result.Success || result.Fingerprint is null)
                throw new InvalidOperationException("Workshop BaseLib or Steam beta branch was not detected.");
            if (result.Fingerprint.Branch != "public-beta")
                throw new InvalidOperationException("Steam beta branch was not normalized correctly.");
            if (result.Fingerprint.BaseLibVersion != "v4.0.0-beta")
                throw new InvalidOperationException("Workshop BaseLib version was not detected.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void VerifyAuditedMethodResolution()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        MethodInfo method = typeof(AdditionalSafetyTests).GetMethod(
            nameof(AuditedResolverTarget),
            BindingFlags.Static | BindingFlags.NonPublic
        ) ?? throw new InvalidOperationException("Resolver test method was not found.");
        string signature = AuditedMethodBindingResolver.FormatMethodSignature(method);
        GameIntegrationProfile profile = CreateResolverProfile(
            assembly.ManifestModule.ModuleVersionId,
            method,
            signature
        );
        GameIntegrationDecision decision = new(
            EnableVisualBindings: true,
            EnableTitleBindings: false,
            ProfileId: profile.Id,
            Reasons: ["fixture"]
        );

        AuditedMethodResolutionResult resolved = AuditedMethodBindingResolver.Resolve(assembly, profile, decision);
        if (!resolved.Success || resolved.Methods.Count != 1 || resolved.Methods[0].Method != method)
            throw new InvalidOperationException("Exact audited MethodDef did not resolve.");

        GameIntegrationProfile badSignature = CreateResolverProfile(
            assembly.ManifestModule.ModuleVersionId,
            method,
            signature.Replace("int)", "string)", StringComparison.Ordinal)
        );
        AuditedMethodResolutionResult signatureFailure = AuditedMethodBindingResolver.Resolve(
            assembly,
            badSignature,
            decision with { ProfileId = badSignature.Id }
        );
        if (signatureFailure.Success || signatureFailure.Methods.Count != 0)
            throw new InvalidOperationException("A mismatched method signature was accepted.");

        GameIntegrationProfile badMvid = CreateResolverProfile(Guid.NewGuid(), method, signature);
        AuditedMethodResolutionResult mvidFailure = AuditedMethodBindingResolver.Resolve(
            assembly,
            badMvid,
            decision with { ProfileId = badMvid.Id }
        );
        if (mvidFailure.Success || mvidFailure.Methods.Count != 0)
            throw new InvalidOperationException("A mismatched module MVID was accepted.");
    }

    private static GameIntegrationProfile CreateResolverProfile(Guid mvid, MethodInfo method, string signature) => new()
    {
        Id = $"resolver-{mvid:N}",
        Status = "verified",
        Branch = "public-beta",
        Fingerprint = new GameBuildFingerprintSpec
        {
            SteamBuildId = "fixture",
            Sts2Sha256 = new string('d', 64),
            ModuleMvid = mvid.ToString("D"),
            BaseLibVersion = "fixture",
        },
        BetaAttestation = new GameBetaAttestationSpec
        {
            Status = "verified",
            Branch = "public-beta",
            InstalledBuildId = "fixture",
            RemoteBuildId = "fixture",
            CheckedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Source = "steamcmd_app_info_print",
            SteamCmdOutputSha256 = new string('f', 64),
        },
        VisualBindings =
        [
            new GameVisualBindingSpec
            {
                Id = "card_visual_request",
                DeclaringType = method.DeclaringType?.FullName ?? string.Empty,
                MethodSignature = signature,
                MetadataToken = $"0x{method.MetadataToken:X8}",
                Status = "verified",
                Fallback = "original_visual",
            }
        ],
        TitleBindings = [],
    };

    private static void AuditedResolverTarget(int impactIndex)
    {
        _ = impactIndex;
    }
}
