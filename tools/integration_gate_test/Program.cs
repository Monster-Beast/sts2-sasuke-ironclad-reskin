using SasukeIronclad.SasukeIroncladCode.Runtime;
using SasukeIronclad.SasukeIroncladCode.Visuals;

string[] visualIds =
[
    "card_visual_request", "original_impact", "state_removed",
    "form_removed", "combat_ended", "character_state"
];
string[] titleIds = ["card_art", "hand", "deck_list", "reward", "compendium", "tooltip"];
RuntimeBuildFingerprint runtime = new(
    "stable",
    "123456",
    new string('a', 64),
    "11111111-2222-3333-4444-555555555555",
    "v3.1.8");

GameIntegrationContractMap CreateContract(string contractStatus, string profileStatus, string? pendingTitle = null)
{
    GameIntegrationProfile profile = new()
    {
        Id = "stable-fixture",
        Status = profileStatus,
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
            MetadataToken = $"0x{0x06000001 + index:X8}",
            Status = "verified",
            Fallback = "original_visual",
        }).ToList(),
        TitleBindings = titleIds.Select((id, index) => new GameTitleBindingSpec
        {
            SurfaceId = id,
            DeclaringType = $"Fixture.Title{index}",
            MethodSignature = $"void Fixture.Title{index}.Refresh()",
            MetadataToken = $"0x{0x06000101 + index:X8}",
            Status = id == pendingTitle ? "pending_review" : "verified",
            Fallback = "original_title",
        }).ToList(),
    };

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
        },
        RequiredVisualEvents = visualIds.ToList(),
        RequiredTitleSurfaces = titleIds.ToList(),
        Profiles = [profile],
    };
}

void Expect(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

GameIntegrationDecision pendingContract = GameIntegrationGate.Evaluate(
    CreateContract("pending_local_audit", "verified"), runtime);
Expect(!pendingContract.AnyEnabled, "pending global contract unexpectedly enabled bindings");

GameIntegrationDecision pendingProfile = GameIntegrationGate.Evaluate(
    CreateContract("verified", "pending_review"), runtime);
Expect(!pendingProfile.AnyEnabled, "pending profile unexpectedly enabled bindings");

GameIntegrationDecision enabled = GameIntegrationGate.Evaluate(
    CreateContract("verified", "verified"), runtime);
Expect(enabled.EnableVisualBindings && enabled.EnableTitleBindings, "verified exact profile was not enabled");

RuntimeBuildFingerprint mismatched = runtime with { Sts2Sha256 = new string('b', 64) };
GameIntegrationDecision mismatchDecision = GameIntegrationGate.Evaluate(
    CreateContract("verified", "verified"), mismatched);
Expect(!mismatchDecision.AnyEnabled, "mismatched assembly hash unexpectedly enabled bindings");

GameIntegrationDecision partial = GameIntegrationGate.Evaluate(
    CreateContract("verified", "verified", pendingTitle: "tooltip"), runtime);
Expect(partial.EnableVisualBindings, "verified visual bindings should remain independently enabled");
Expect(!partial.EnableTitleBindings, "pending title surface unexpectedly enabled title bindings");

GameIntegrationContractMap duplicate = CreateContract("verified", "verified");
duplicate.Profiles.Add(duplicate.Profiles[0] with { Id = "duplicate-fixture" });
try
{
    GameIntegrationGate.Evaluate(duplicate, runtime);
    throw new InvalidOperationException("duplicate fingerprint was accepted");
}
catch (InvalidOperationException exception) when (exception.Message.Contains("Duplicate build fingerprint", StringComparison.Ordinal))
{
}

Console.WriteLine("INTEGRATION_GATE_OK pending=false exact=true mismatch=false partial_titles=false duplicate_rejected=true");
