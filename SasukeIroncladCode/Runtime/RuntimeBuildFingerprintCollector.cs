using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeBuildFingerprintCollectionResult(
    bool Success,
    RuntimeBuildFingerprint? Fingerprint,
    IReadOnlyList<string> Reasons
);

public interface IRuntimeBuildFingerprintProvider
{
    RuntimeBuildFingerprintCollectionResult Collect();
}

public sealed class CurrentProcessRuntimeBuildFingerprintProvider : IRuntimeBuildFingerprintProvider
{
    private readonly string? _branch;

    public CurrentProcessRuntimeBuildFingerprintProvider(string? branch = null)
    {
        _branch = branch;
    }

    public RuntimeBuildFingerprintCollectionResult Collect() =>
        RuntimeBuildFingerprintCollector.CollectCurrentProcess(_branch);
}

public static class RuntimeBuildFingerprintCollector
{
    private static readonly Regex AcfValuePattern = new(
        "\"(?<key>[^\"]+)\"\\s+\"(?<value>[^\"]*)\"",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public static RuntimeBuildFingerprintCollectionResult CollectCurrentProcess(string? branch = null)
    {
        Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, "sts2", StringComparison.OrdinalIgnoreCase));
        if (assembly is null || string.IsNullOrWhiteSpace(assembly.Location))
            return Failure("The loaded sts2 assembly location is unavailable; game bindings remain disabled.");

        string? dataDirectory = Path.GetDirectoryName(assembly.Location);
        string? gamePath = dataDirectory is null ? null : Directory.GetParent(dataDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(gamePath))
            return Failure("The game root could not be derived from the loaded sts2 assembly; bindings remain disabled.");

        string? requestedBranch = branch ?? Environment.GetEnvironmentVariable("STS2_BRANCH");
        return Collect(assembly.Location, gamePath, requestedBranch);
    }

    public static RuntimeBuildFingerprintCollectionResult Collect(
        string sts2AssemblyPath,
        string gamePath,
        string? branch)
    {
        List<string> reasons = [];
        try
        {
            if (!File.Exists(sts2AssemblyPath))
                reasons.Add("The sts2 assembly file is unavailable.");
            if (!Directory.Exists(gamePath))
                reasons.Add("The game root directory is unavailable.");
            if (reasons.Count > 0)
                return new(false, null, reasons);

            string assemblySha256 = ComputeSha256(sts2AssemblyPath);
            string moduleMvid = ReadModuleMvid(sts2AssemblyPath);
            SteamRuntimeMetadata steam = ReadSteamMetadata(gamePath);
            string resolvedBranch = NormalizeBranch(branch, steam.BetaBranch, steam.ManifestFound);
            string? baseLibVersion = ReadBaseLibVersion(gamePath, steam.SteamAppsPath);

            if (string.IsNullOrWhiteSpace(steam.BuildId))
                reasons.Add("Steam buildid could not be detected.");
            if (string.IsNullOrWhiteSpace(baseLibVersion))
                reasons.Add("BaseLib version could not be detected in local mods or Workshop content.");
            if (string.Equals(resolvedBranch, "unknown", StringComparison.OrdinalIgnoreCase))
                reasons.Add("The STS2 branch is unknown; set STS2_BRANCH or provide a Steam appmanifest.");
            if (reasons.Count > 0)
                return new(false, null, reasons);

            RuntimeBuildFingerprint fingerprint = new(
                resolvedBranch,
                steam.BuildId!,
                assemblySha256,
                moduleMvid,
                baseLibVersion!
            );
            return new(true, fingerprint, ["Exact runtime build fingerprint collected."]);
        }
        catch (Exception exception)
        {
            return Failure($"Runtime fingerprint collection failed safely: {exception.GetType().Name}.");
        }
    }

    private static RuntimeBuildFingerprintCollectionResult Failure(string reason) =>
        new(false, null, [reason]);

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ReadModuleMvid(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using PEReader peReader = new(stream, PEStreamOptions.LeaveOpen);
        if (!peReader.HasMetadata)
            throw new InvalidDataException("sts2 assembly has no managed metadata");
        MetadataReader reader = peReader.GetMetadataReader();
        ModuleDefinition module = reader.GetModuleDefinition();
        return reader.GetGuid(module.Mvid).ToString("D").ToLowerInvariant();
    }

    private static SteamRuntimeMetadata ReadSteamMetadata(string gamePath)
    {
        DirectoryInfo? current = new(Path.GetFullPath(gamePath));
        for (int depth = 0; current is not null && depth < 7; depth++, current = current.Parent)
        {
            string manifest = Path.Combine(current.FullName, "appmanifest_2868840.acf");
            if (!File.Exists(manifest))
                continue;
            string text = File.ReadAllText(manifest);
            Dictionary<string, string> values = ParseAcfValues(text);
            values.TryGetValue("buildid", out string? buildId);
            values.TryGetValue("betakey", out string? betaKey);
            return new(true, current.FullName, buildId, betaKey);
        }
        return new(false, null, null, null);
    }

    private static Dictionary<string, string> ParseAcfValues(string text)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AcfValuePattern.Matches(text))
        {
            string key = match.Groups["key"].Value;
            if (!values.ContainsKey(key))
                values[key] = match.Groups["value"].Value;
        }
        return values;
    }

    private static string NormalizeBranch(string? explicitBranch, string? betaBranch, bool manifestFound)
    {
        string? candidate = string.IsNullOrWhiteSpace(explicitBranch) ? betaBranch : explicitBranch;
        if (!string.IsNullOrWhiteSpace(candidate) &&
            !string.Equals(candidate, "public", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate, "none", StringComparison.OrdinalIgnoreCase))
        {
            return candidate.Trim().ToLowerInvariant();
        }
        return manifestFound ? "stable" : "unknown";
    }

    private static string? ReadBaseLibVersion(string gamePath, string? steamAppsPath)
    {
        List<string> roots =
        [
            Path.Combine(gamePath, "mods"),
            Path.Combine(gamePath, "SlayTheSpire2.app", "Contents", "MacOS", "mods"),
        ];
        if (!string.IsNullOrWhiteSpace(steamAppsPath))
            roots.Add(Path.Combine(steamAppsPath, "workshop", "content", "2868840"));

        foreach (string root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string manifest in EnumerateBaseLibManifestCandidates(root))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
                    if (!LooksLikeBaseLib(document.RootElement, manifest))
                        continue;
                    if (TryReadProperty(document.RootElement, "version", out string? version) && !string.IsNullOrWhiteSpace(version))
                        return version;
                }
                catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
                {
                    // Continue to another BaseLib manifest candidate.
                }
            }
        }
        return null;
    }

    private static IEnumerable<string> EnumerateBaseLibManifestCandidates(string root)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).Take(10000).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
        return files
            .Where(path => Path.GetFileName(path).Contains("BaseLib", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains($"{Path.DirectorySeparatorChar}BaseLib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase);
    }

    private static bool LooksLikeBaseLib(JsonElement element, string path)
    {
        if (Path.GetFileName(path).Contains("BaseLib", StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}BaseLib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return (TryReadProperty(element, "id", out string? id) && string.Equals(id, "BaseLib", StringComparison.OrdinalIgnoreCase)) ||
               (TryReadProperty(element, "name", out string? name) && name?.Contains("BaseLib", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool TryReadProperty(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;
            value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.ToString();
            return true;
        }
        return false;
    }

    private sealed record SteamRuntimeMetadata(
        bool ManifestFound,
        string? SteamAppsPath,
        string? BuildId,
        string? BetaBranch
    );
}
