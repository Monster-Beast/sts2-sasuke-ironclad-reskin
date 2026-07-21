using System.Text.Json;
using Godot;
using SasukeIronclad.SasukeIroncladCode.Adapters;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeCanaryOptInLoadResult(
    bool Found,
    RuntimeCanaryOptIn? OptIn,
    IReadOnlyList<string> Reasons
);

public static class RuntimeCanaryLocalFiles
{
    public const string OptInFileName = "SasukeIronclad.canary.json";
    public const string StatusFileName = "runtime-canary-status.json";
    public const string AnchorStatusFileName = "runtime-canary-anchor-status.json";
    public const string ReplacementStatusFileName = "runtime-canary-replacement-status.json";
    public const string ObservationOptInFileName = "SasukeIronclad.observe.json";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    public static RuntimeCanaryOptInLoadResult LoadOptIn(string modAssemblyPath)
    {
        string? modDirectory = ResolveModDirectory(modAssemblyPath);
        if (string.IsNullOrWhiteSpace(modDirectory))
            return new(false, null, ["Mod directory could not be derived; runtime canary remains disabled."]);

        string markerPath = Path.Combine(modDirectory, OptInFileName);
        if (!File.Exists(markerPath))
            return new(false, null, ["Runtime canary opt-in marker is absent; canary remains disabled by default."]);
        if (File.Exists(Path.Combine(modDirectory, ObservationOptInFileName)))
            return new(true, null, ["Runtime observation marker is present; remove it before enabling the canary."]);

        try
        {
            FileInfo info = new(markerPath);
            if (info.Length is <= 0 or > 64 * 1024)
                return new(true, null, ["Runtime canary opt-in marker size is invalid."]);
            RuntimeCanaryOptIn? optIn = JsonSerializer.Deserialize<RuntimeCanaryOptIn>(
                File.ReadAllText(markerPath),
                ReadOptions);
            return optIn is null
                ? new(true, null, ["Runtime canary opt-in marker was empty."])
                : new(true, optIn, ["Explicit runtime canary opt-in marker loaded."]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(true, null, [$"Runtime canary opt-in marker could not be read: {exception.GetType().Name}."]);
        }
    }

    public static void WriteStatus(string modAssemblyPath, RuntimeCanaryBootstrapResult status)
    {
        ArgumentNullException.ThrowIfNull(status);
        string? modDirectory = ResolveModDirectory(modAssemblyPath);
        if (string.IsNullOrWhiteSpace(modDirectory))
            return;

        RuntimeCanaryStatusDocument document = new()
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Enabled = status.Enabled,
            AnimationsEnabled = status.AnimationsEnabled,
            TitlesEnabled = status.TitlesEnabled,
            PatchedBindingIds = status.PatchedBindingIds.Order(StringComparer.Ordinal).ToArray(),
            Reasons = status.Reasons,
        };
        TryWriteAtomic(Path.Combine(modDirectory, StatusFileName), document);
    }

    public static void WriteAnchorStatus(
        string modAssemblyPath,
        RuntimeCanaryOptIn optIn,
        bool attempted,
        RuntimePlayerAnchorResolution resolution,
        GodotVisualSceneHost? host,
        bool originalVisualHidden)
    {
        ArgumentNullException.ThrowIfNull(optIn);
        ArgumentNullException.ThrowIfNull(resolution);
        string? modDirectory = ResolveModDirectory(modAssemblyPath);
        if (string.IsNullOrWhiteSpace(modDirectory))
            return;

        Vector2? globalPosition = host?.AnchorGlobalPosition;
        bool bound = attempted && resolution.Success && host?.IsAnchorBound == true;
        RuntimeCanaryAnchorStatusDocument document = new()
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Attempted = attempted,
            Bound = bound,
            OverlayVisible = bound && host?.Visible == true,
            Strategy = resolution.Strategy,
            CandidateCount = resolution.CandidateCount,
            LocalPlayerReferenceCount = resolution.LocalPlayerReferenceCount,
            AnchorType = bound ? host?.AnchorType : null,
            AnchorName = bound ? host?.AnchorName : null,
            AnchorGlobalX = bound && globalPosition.HasValue ? globalPosition.Value.X : null,
            AnchorGlobalY = bound && globalPosition.HasValue ? globalPosition.Value.Y : null,
            AnchorScale = optIn.AnchorScale,
            AnchorOffsetX = optIn.AnchorOffsetX,
            AnchorOffsetY = optIn.AnchorOffsetY,
            ReplacementRequested = optIn.HideOriginalVisual,
            OriginalVisualHidden = originalVisualHidden,
            Reasons = resolution.Reasons,
        };
        TryWriteAtomic(Path.Combine(modDirectory, AnchorStatusFileName), document);
    }

    public static void WriteReplacementStatus(
        string modAssemblyPath,
        RuntimeCanaryOptIn optIn,
        RuntimeOriginalVisualReplacementSnapshot snapshot,
        GodotVisualSceneHost? host)
    {
        ArgumentNullException.ThrowIfNull(optIn);
        ArgumentNullException.ThrowIfNull(snapshot);
        string? modDirectory = ResolveModDirectory(modAssemblyPath);
        if (string.IsNullOrWhiteSpace(modDirectory))
            return;

        RuntimeCanaryReplacementStatusDocument document = new()
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Requested = snapshot.Requested,
            Active = snapshot.Active,
            EverHidden = snapshot.EverHidden,
            RestoreCount = snapshot.RestoreCount,
            LastTransition = snapshot.LastTransition,
            TargetType = snapshot.TargetType,
            TargetName = snapshot.TargetName,
            OriginalVisibleBeforeHide = snapshot.OriginalVisibleBeforeHide,
            AnchorBound = host?.IsAnchorBound == true,
            OverlayVisible = host?.Visible == true,
            AnchorScale = optIn.AnchorScale,
            AnchorOffsetX = optIn.AnchorOffsetX,
            AnchorOffsetY = optIn.AnchorOffsetY,
            Reasons = snapshot.Reasons,
        };
        TryWriteAtomic(Path.Combine(modDirectory, ReplacementStatusFileName), document);
    }

    private static void TryWriteAtomic<T>(string statusPath, T document)
    {
        try
        {
            string temporaryPath = statusPath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(document, WriteOptions) + System.Environment.NewLine,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, statusPath, overwrite: true);
        }
        catch
        {
            // Canary diagnostics are cosmetic and must never affect game startup.
        }
    }

    private static string? ResolveModDirectory(string modAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(modAssemblyPath))
            return null;
        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(modAssemblyPath));
        }
        catch
        {
            return null;
        }
    }
}
