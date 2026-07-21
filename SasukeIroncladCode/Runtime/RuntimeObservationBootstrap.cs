using System.Reflection;
using System.Text.Json;
using SasukeIronclad.SasukeIroncladCode.Adapters;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeObservationBootstrapResult(
    bool Enabled,
    string? SessionId,
    string? OutputPath,
    IReadOnlyList<string> Reasons
);

public static class RuntimeObservationBootstrap
{
    public static RuntimeObservationBootstrapResult Start(
        RuntimeObservationManifestMap manifest,
        GameIntegrationProfile pendingProfile,
        RuntimeObservationOptIn optIn,
        IRuntimeBuildFingerprintProvider fingerprintProvider,
        Assembly gameAssembly,
        IRuntimeObservationPatcher patcher,
        Func<string, IRuntimeObservationSink> sinkFactory,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(pendingProfile);
        ArgumentNullException.ThrowIfNull(optIn);
        ArgumentNullException.ThrowIfNull(fingerprintProvider);
        ArgumentNullException.ThrowIfNull(gameAssembly);
        ArgumentNullException.ThrowIfNull(patcher);
        ArgumentNullException.ThrowIfNull(sinkFactory);

        SafeReset(patcher);
        RuntimeBuildFingerprintCollectionResult collection;
        try
        {
            collection = fingerprintProvider.Collect();
        }
        catch (Exception exception)
        {
            return Disabled($"Observation fingerprint provider failed safely: {exception.GetType().Name}.");
        }
        if (!collection.Success || collection.Fingerprint is null)
            return new(false, null, null, collection.Reasons);

        RuntimeObservationGateResult gate = RuntimeObservationGate.Evaluate(
            manifest,
            pendingProfile,
            optIn,
            collection.Fingerprint,
            now ?? DateTimeOffset.UtcNow);
        if (!gate.Enabled)
            return new(false, null, null, gate.Reasons);

        RuntimeObservationResolutionResult resolution;
        try
        {
            resolution = RuntimeObservationTargetResolver.Resolve(gameAssembly, manifest);
        }
        catch (Exception exception)
        {
            return Disabled($"Observation target resolution failed safely: {exception.GetType().Name}.");
        }
        if (!resolution.Success)
            return new(false, null, null, resolution.Reasons);

        IRuntimeObservationSink? sink = null;
        RuntimeObservationSession? session = null;
        try
        {
            sink = sinkFactory(optIn.SessionLabel);
            session = new RuntimeObservationSession(
                manifest,
                collection.Fingerprint,
                optIn,
                gate.MaxEvents,
                gate.MaxStackFrames,
                sink);
            sink = null;
            patcher.Install(session, resolution.Targets);
            session = null;
            List<string> reasons = gate.Reasons.Concat(resolution.Reasons).ToList();
            reasons.Add("All probes are prefix/postfix observers; original arguments and return values are untouched.");
            return new(true, patcher.SessionId, patcher.OutputPath, reasons);
        }
        catch (Exception exception)
        {
            SafeReset(patcher);
            try
            {
                session?.Dispose();
                sink?.Dispose();
            }
            catch
            {
                // Cleanup remains best effort.
            }
            return Disabled($"Observation installation failed closed: {exception.GetType().Name}.");
        }
    }

    private static void SafeReset(IRuntimeObservationPatcher patcher)
    {
        try
        {
            patcher.Reset();
        }
        catch
        {
            // Observation reset must never affect game startup.
        }
    }

    private static RuntimeObservationBootstrapResult Disabled(string reason) => new(false, null, null, [reason]);
}

public sealed record RuntimeObservationOptInLoadResult(
    bool Found,
    RuntimeObservationOptIn? OptIn,
    IReadOnlyList<string> Reasons
);

public static class RuntimeObservationLocalFiles
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static RuntimeObservationOptInLoadResult LoadOptIn(
        string modAssemblyPath,
        RuntimeObservationManifestMap manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modAssemblyPath);
        ArgumentNullException.ThrowIfNull(manifest);
        string? modDirectory = Path.GetDirectoryName(Path.GetFullPath(modAssemblyPath));
        if (string.IsNullOrWhiteSpace(modDirectory))
            return new(false, null, ["Mod directory could not be derived; observation remains disabled."]);
        string markerPath = Path.Combine(modDirectory, manifest.Policy.OptInFileName);
        if (!File.Exists(markerPath))
            return new(false, null, ["Observation opt-in marker is absent; observation remains disabled by default."]);

        try
        {
            FileInfo info = new(markerPath);
            if (info.Length is <= 0 or > 64 * 1024)
                return new(true, null, ["Observation opt-in marker size is invalid."]);
            RuntimeObservationOptIn? optIn = JsonSerializer.Deserialize<RuntimeObservationOptIn>(
                File.ReadAllText(markerPath),
                JsonOptions);
            return optIn is null
                ? new(true, null, ["Observation opt-in marker was empty."])
                : new(true, optIn, ["Explicit observation opt-in marker loaded."]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(true, null, [$"Observation opt-in marker could not be read: {exception.GetType().Name}."]);
        }
    }

    public static IRuntimeObservationSink CreateSink(
        string modAssemblyPath,
        RuntimeObservationManifestMap manifest,
        string sessionLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modAssemblyPath);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionLabel);
        string? modDirectory = Path.GetDirectoryName(Path.GetFullPath(modAssemblyPath));
        if (string.IsNullOrWhiteSpace(modDirectory))
            throw new InvalidOperationException("Mod directory could not be derived for observation output.");
        string outputDirectory = Path.Combine(modDirectory, manifest.Policy.OutputDirectoryName);
        string fileName = $"runtime-observation-{sessionLabel}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.jsonl";
        return new JsonLinesRuntimeObservationSink(Path.Combine(outputDirectory, fileName));
    }
}
