using SasukeIronclad.SasukeIroncladCode.Adapters;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record GameIntegrationBootstrapResult(
    GameIntegrationDecision Decision,
    bool Installed,
    IReadOnlyList<string> Reasons
);

public static class GameIntegrationBootstrap
{
    public static GameIntegrationBootstrapResult Start(
        GameIntegrationContractMap contract,
        IRuntimeBuildFingerprintProvider fingerprintProvider,
        IGameIntegrationInstaller installer)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(fingerprintProvider);
        ArgumentNullException.ThrowIfNull(installer);

        SafeReset(installer);
        RuntimeBuildFingerprintCollectionResult collection;
        try
        {
            collection = fingerprintProvider.Collect();
        }
        catch (Exception exception)
        {
            return Disabled($"Runtime fingerprint provider failed safely: {exception.GetType().Name}.");
        }

        if (!collection.Success || collection.Fingerprint is null)
            return new(DisabledDecision(collection.Reasons), false, collection.Reasons);

        GameIntegrationDecision decision;
        try
        {
            decision = GameIntegrationGate.Evaluate(contract, collection.Fingerprint);
        }
        catch (Exception exception)
        {
            return Disabled($"Integration contract validation failed safely: {exception.GetType().Name}.");
        }

        if (!decision.AnyEnabled || string.IsNullOrWhiteSpace(decision.ProfileId))
            return new(decision, false, decision.Reasons);

        GameIntegrationProfile? profile = contract.Profiles.SingleOrDefault(
            candidate => string.Equals(candidate.Id, decision.ProfileId, StringComparison.Ordinal)
        );
        if (profile is null)
            return Disabled("The selected integration profile was not found after gate evaluation.");

        try
        {
            installer.Install(profile, decision);
            return new(decision, true, decision.Reasons);
        }
        catch (Exception exception)
        {
            SafeReset(installer);
            List<string> reasons = decision.Reasons.ToList();
            reasons.Add($"Integration installation failed closed: {exception.GetType().Name}.");
            return new(DisabledDecision(reasons, decision.ProfileId), false, reasons);
        }
    }

    private static void SafeReset(IGameIntegrationInstaller installer)
    {
        try
        {
            installer.Reset();
        }
        catch
        {
            // A cosmetic installer reset must never affect game startup.
        }
    }

    private static GameIntegrationBootstrapResult Disabled(string reason)
    {
        IReadOnlyList<string> reasons = [reason];
        return new(DisabledDecision(reasons), false, reasons);
    }

    private static GameIntegrationDecision DisabledDecision(
        IReadOnlyList<string> reasons,
        string? profileId = null) =>
        new(false, false, profileId, reasons);
}
