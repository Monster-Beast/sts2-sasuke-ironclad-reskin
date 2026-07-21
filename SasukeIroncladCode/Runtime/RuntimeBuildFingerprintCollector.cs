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
    private static readonly Regex SteamBuildIdPattern = new(
        "\"buildid\"\\s+\"(?<value>[0-9]+)\"",
        RegexOptions.CultureInvariant | RegexOptions.Compiled
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

        string resolvedBranch = branch
            ?? Environment.GetEnvironmentVariable("STS2_BRANCH")
            ?? "unknown";
        return Collect(assembly.Location, gamePath, resolvedBranch);
    }

    public static RuntimeBuildFingerprintCollectionResult Collect(
        string sts2AssemblyPath,
        string gamePath,
        string branch)
    {
        List<string> reasons = [];
        try
        {
            if (string.IsNullOrWhiteSpace(branch))
                reasons.Add("The game branch is unknown.");
            if (!File.Exists(sts2AssemblyPath))
                reasons.Add("The sts2 assembly file is unavailable.");
            if (!Directory.Exists(gamePath))
                reasons.Add("The game root directory is unavailable.");
            if (reasons.Count > 0)
                return new(false, null, reasons);

            string assemblySha256 = ComputeSha256(sts2AssemblyPath);
            string moduleMvid = ReadModuleMvid(sts2AssemblyPath);
            string? steamBuildId = ReadSteamBuildId(gamePath);
            string? baseLibVersion = ReadBaseLibVersion(gamePath);

            if (string.IsNullOrWhiteSpace(steamBuildId))
                reasons.Add("Steam buildid could not be detected.");
            if (string.IsNullOrWhiteSpace(baseLibVersion))
                reasons.Add("BaseLib version could not be detected.");
            if (string.Equals(branch, "unknown", StringComparison.OrdinalIgnoreCase))
                reasons.Add("The STS2 branch is unknown.");
            if (reasons.Count > 0)
                return new(false, null, reasons);

            RuntimeBuildFingerprint fingerprint = new(
                branch.Trim(),
                steamBuildId!,
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

    private static string? ReadSteamBuildId(string gamePath)
    {
        DirectoryInfo? current = new(Path.GetFullPath(gamePath));
        for (int depth = 0; current is not null && depth < 6; depth++, current = current.Parent)
        {
            string manifest = Path.Combine(current.FullName, "appmanifest_2868840.acf");
            if (!File.Exists(manifest))
                continue;
            string text = File.ReadAllText(manifest);
            Match match = SteamBuildIdPattern.Match(text);
            return match.Success ? match.Groups["value"].Value : null;
        }
        return null;
    }

    private static string? ReadBaseLibVersion(string gamePath)
    {
        string modsPath = Path.Combine(gamePath, "mods");
        if (!Directory.Exists(modsPath))
            return null;

        IEnumerable<string> manifests = Directory.EnumerateFiles(modsPath, "*.json", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).Contains("BaseLib", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains($"{Path.DirectorySeparatorChar}BaseLib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase);
        foreach (string manifest in manifests)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
                if (TryReadProperty(document.RootElement, "version", out string? version) && !string.IsNullOrWhiteSpace(version))
                    return version;
            }
            catch (JsonException)
            {
                // Continue to another BaseLib manifest candidate.
            }
        }
        return null;
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
}
