using System.Text.Json.Serialization;

namespace SasukeIronclad.GameAudit;

internal sealed record AuditOptions(
    string GamePath,
    IReadOnlyList<string> AssetRoots,
    string OutputDirectory,
    string Branch,
    string Session,
    string? MegaDotVersion,
    string QueryPath,
    int MaxSymbols,
    int MaxAssets,
    long MaxHashBytes
);

internal sealed class AuditQuerySet
{
    public int SchemaVersion { get; init; }
    public List<AuditQuery> Queries { get; init; } = [];
}

internal sealed class AuditQuery
{
    public string Id { get; init; } = string.Empty;
    public List<string> TypeContains { get; init; } = [];
    public List<string> MethodContains { get; init; } = [];
    public List<string> MemberContains { get; init; } = [];
    public List<string> AssetContains { get; init; } = [];
}

internal sealed class AuditReport
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public string Session { get; init; } = string.Empty;
    public string Branch { get; init; } = "unknown";
    public AuditEnvironment Environment { get; init; } = new();
    public GameBuildMetadata Game { get; init; } = new();
    public List<SymbolCandidate> Symbols { get; init; } = [];
    public List<AssetCandidate> Assets { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
}

internal sealed class AuditEnvironment
{
    public string OperatingSystem { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string DotnetVersion { get; init; } = string.Empty;
    public string? MegaDotVersion { get; init; }
    public List<AuditSourceRoot> SourceRoots { get; init; } = [];
}

internal sealed class AuditSourceRoot
{
    public string Label { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
}

internal sealed class GameBuildMetadata
{
    public string DataDirectory { get; init; } = string.Empty;
    public string Sts2AssemblyRelativePath { get; init; } = string.Empty;
    public string Sts2AssemblySha256 { get; init; } = string.Empty;
    public string AssemblyName { get; init; } = string.Empty;
    public string AssemblyVersion { get; init; } = string.Empty;
    public string ModuleVersionId { get; init; } = string.Empty;
    public string? SteamBuildId { get; init; }
    public string? SteamLastUpdated { get; init; }
    public string? BaseLibVersion { get; init; }
    public string? BaseLibManifestSha256 { get; init; }
}

internal sealed class SymbolCandidate
{
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string DeclaringType { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public string MetadataToken { get; init; } = string.Empty;
    public string Visibility { get; init; } = string.Empty;
    public string? ConstantValue { get; init; }
    public int Score { get; init; }
    public List<string> Categories { get; init; } = [];
}

internal sealed class AssetCandidate
{
    public string RootLabel { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string? Sha256 { get; init; }
    public string HashStatus { get; init; } = string.Empty;
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int Score { get; init; }
    public List<string> Categories { get; init; } = [];
}

internal sealed class AuditFingerprint
{
    public int SchemaVersion { get; init; } = 1;
    public string Session { get; init; } = string.Empty;
    public string Sts2AssemblySha256 { get; init; } = string.Empty;
    public string? SteamBuildId { get; init; }
    public string? BaseLibVersion { get; init; }
    public List<string> SymbolKeys { get; init; } = [];
    public List<string> AssetKeys { get; init; } = [];
}

internal sealed class AuditComparison
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset ComparedAtUtc { get; init; }
    public string FirstSession { get; init; } = string.Empty;
    public string SecondSession { get; init; } = string.Empty;
    public bool SameAssembly { get; init; }
    public bool SameSteamBuild { get; init; }
    public bool SameBaseLibVersion { get; init; }
    public bool SameSymbols { get; init; }
    public bool SameAssets { get; init; }
    public bool Equivalent { get; init; }
    public List<string> AddedSymbols { get; init; } = [];
    public List<string> RemovedSymbols { get; init; } = [];
    public List<string> AddedAssets { get; init; } = [];
    public List<string> RemovedAssets { get; init; } = [];
    public List<string> ChangedAssets { get; init; } = [];
}

internal sealed class AuditUsageException(string message) : Exception(message);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AuditQuerySet))]
[JsonSerializable(typeof(AuditReport))]
[JsonSerializable(typeof(AuditFingerprint))]
[JsonSerializable(typeof(AuditComparison))]
internal partial class AuditJsonContext : JsonSerializerContext;
