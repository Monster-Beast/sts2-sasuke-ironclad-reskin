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
    }

    private static void VerifyNonMethodTokenIsRejected()
    {
        string[] visualIds =
        [
            "card_visual_request", "original_impact", "state_removed",
            "form_removed", "combat_ended", "character_state"
        ];
        string[] titleIds = ["card_art", "hand", "deck_list", "reward", "compendium", "tooltip"];
        RuntimeBuildFingerprint runtime = new(
            "stable",
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
            },
            RequiredVisualEvents = visualIds.ToList(),
            RequiredTitleSurfaces = titleIds.ToList(),
            Profiles = [profile],
        };
        GameIntegrationDecision decision = GameIntegrationGate.Evaluate(contract, runtime);
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
        Branch = "fixture",
        Fingerprint = new GameBuildFingerprintSpec
        {
            SteamBuildId = "fixture",
            Sts2Sha256 = new string('d', 64),
            ModuleMvid = mvid.ToString("D"),
            BaseLibVersion = "fixture",
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
