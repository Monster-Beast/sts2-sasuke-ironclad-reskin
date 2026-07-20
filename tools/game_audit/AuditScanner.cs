using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SasukeIronclad.GameAudit;

internal static partial class AuditScanner
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".godot", "logs", "saves", "crash", "crashes", "screenshots", "workshop", "shader_cache"
    };

    public static AuditReport Scan(AuditOptions options)
    {
        string gamePath = Path.GetFullPath(options.GamePath);
        if (!Directory.Exists(gamePath))
            throw new DirectoryNotFoundException($"Game path does not exist: {options.GamePath}");

        AuditQuerySet querySet = LoadQueries(options.QueryPath);
        if (querySet.SchemaVersion != 1 || querySet.Queries.Count == 0)
            throw new InvalidDataException("Audit query configuration is missing or unsupported.");

        string dataDirectoryPath = FindDataDirectory(gamePath);
        string assemblyPath = Path.Combine(dataDirectoryPath, "sts2.dll");
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException("sts2.dll was not found in the detected data directory.", assemblyPath);

        string assemblySha256 = ComputeSha256(assemblyPath);
        string dataDirectoryRelative = ToPortableRelativePath(gamePath, dataDirectoryPath);
        string assemblyRelative = ToPortableRelativePath(gamePath, assemblyPath);

        (string? steamBuildId, string? steamLastUpdated) = ReadSteamBuildMetadata(gamePath);
        (string? baseLibVersion, string? baseLibHash) = ReadBaseLibMetadata(gamePath);

        (GameBuildMetadata build, List<SymbolCandidate> symbols) = MetadataScanner.Scan(
            assemblyPath,
            assemblyRelative,
            dataDirectoryRelative,
            assemblySha256,
            querySet,
            options.MaxSymbols,
            steamBuildId,
            steamLastUpdated,
            baseLibVersion,
            baseLibHash);

        List<(string Label, string Kind, string Path)> roots =
        [
            ("game-install", "game", gamePath),
        ];
        for (int index = 0; index < options.AssetRoots.Count; index++)
        {
            string fullPath = Path.GetFullPath(options.AssetRoots[index]);
            if (Directory.Exists(fullPath) && !roots.Any(item => PathsEqual(item.Path, fullPath)))
                roots.Add(($"asset-root-{index + 1}", "recovered-assets", fullPath));
        }

        List<string> warnings = [];
        if (options.AssetRoots.Count == 0)
            warnings.Add("No recovered asset root was supplied; resource candidates are limited to the installed game tree.");
        if (string.IsNullOrWhiteSpace(baseLibVersion))
            warnings.Add("BaseLib manifest version was not detected.");
        if (string.IsNullOrWhiteSpace(steamBuildId))
            warnings.Add("Steam build ID was not detected from appmanifest_2868840.acf.");

        List<AssetCandidate> assets = ScanAssets(roots, querySet, options.MaxAssets, options.MaxHashBytes, warnings);

        return new AuditReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Session = options.Session,
            Branch = options.Branch,
            Environment = new AuditEnvironment
            {
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                DotnetVersion = Environment.Version.ToString(),
                MegaDotVersion = options.MegaDotVersion,
                SourceRoots = roots.Select(root => new AuditSourceRoot
                {
                    Label = root.Label,
                    Kind = root.Kind,
                }).ToList(),
            },
            Game = build,
            Symbols = symbols,
            Assets = assets,
            Warnings = warnings,
        };
    }

    private static AuditQuerySet LoadQueries(string queryPath)
    {
        string resolved = Path.GetFullPath(queryPath);
        if (!File.Exists(resolved))
            throw new FileNotFoundException("Audit query file was not found.", resolved);
        string json = File.ReadAllText(resolved);
        return JsonSerializer.Deserialize(json, AuditJsonContext.Default.AuditQuerySet)
            ?? throw new InvalidDataException("Audit query file was empty.");
    }

    private static string FindDataDirectory(string gamePath)
    {
        string[] candidates =
        [
            Path.Combine(gamePath, "data_sts2_windows_x86_64"),
            Path.Combine(gamePath, "data_sts2_linuxbsd_x86_64"),
            Path.Combine(gamePath, "SlayTheSpire2.app", "Contents", "Resources", "data_sts2_macos_x86_64"),
        ];
        foreach (string candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "sts2.dll")))
                return candidate;
        }

        string? discovered = SafeEnumerateDirectories(gamePath)
            .FirstOrDefault(directory =>
                Path.GetFileName(directory).StartsWith("data_sts2_", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(directory, "sts2.dll")));
        return discovered ?? throw new DirectoryNotFoundException("No data_sts2_* directory containing sts2.dll was found.");
    }

    private static List<AssetCandidate> ScanAssets(
        IReadOnlyList<(string Label, string Kind, string Path)> roots,
        AuditQuerySet querySet,
        int maxAssets,
        long maxHashBytes,
        List<string> warnings)
    {
        List<AssetCandidate> candidates = [];
        foreach (var root in roots)
        {
            string label = root.Label;
            string rootPath = root.Path;
            foreach (string file in SafeEnumerateFiles(rootPath, warnings))
            {
                string relative = ToPortableRelativePath(rootPath, file);
                if (IsExcluded(relative))
                    continue;

                List<(string Category, int Score)> matches = MatchAsset(relative, querySet);
                bool critical = IsCriticalFile(relative);
                if (matches.Count == 0 && !critical)
                    continue;

                FileInfo info;
                try
                {
                    info = new FileInfo(file);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"Unable to inspect asset metadata: {label}/{relative}");
                    continue;
                }

                string? sha256 = null;
                string hashStatus;
                if (info.Length <= maxHashBytes)
                {
                    try
                    {
                        sha256 = ComputeSha256(file);
                        hashStatus = "hashed";
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        hashStatus = "unreadable";
                    }
                }
                else
                {
                    hashStatus = "skipped_large";
                }

                (int? width, int? height) = ReadDimensions(file, info.Extension);
                candidates.Add(new AssetCandidate
                {
                    RootLabel = label,
                    RelativePath = relative,
                    Extension = info.Extension.ToLowerInvariant(),
                    SizeBytes = info.Length,
                    Sha256 = sha256,
                    HashStatus = hashStatus,
                    Width = width,
                    Height = height,
                    Score = matches.Sum(item => item.Score) + (critical ? 10 : 0),
                    Categories = matches.Select(item => item.Category).Distinct(StringComparer.Ordinal).Order().ToList(),
                });
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.RootLabel, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.RelativePath, StringComparer.Ordinal)
            .Take(maxAssets)
            .ToList();
    }

    private static List<(string Category, int Score)> MatchAsset(string relativePath, AuditQuerySet querySet)
    {
        List<(string Category, int Score)> matches = [];
        foreach (AuditQuery query in querySet.Queries)
        {
            int hits = query.AssetContains.Count(keyword =>
                !string.IsNullOrWhiteSpace(keyword) &&
                relativePath.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (hits > 0)
                matches.Add((query.Id, hits * 5));
        }
        return matches;
    }

    private static bool IsCriticalFile(string relativePath)
    {
        string fileName = Path.GetFileName(relativePath);
        string extension = Path.GetExtension(relativePath);
        return fileName.Equals("sts2.dll", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("BaseLib.json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".pck", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcluded(string relativePath)
    {
        string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => ExcludedDirectoryNames.Contains(segment)))
            return true;
        return segments.Length > 1 && segments[0].Equals("mods", StringComparison.OrdinalIgnoreCase) &&
               !relativePath.Contains("BaseLib.json", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root, List<string> warnings)
    {
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string current = pending.Pop();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Unable to enumerate one directory below the {Path.GetFileName(root)} source root.");
                continue;
            }

            foreach (string file in files)
                yield return file;

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string directory in directories)
            {
                if (!ExcludedDirectoryNames.Contains(Path.GetFileName(directory)))
                    pending.Push(directory);
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static (string? BuildId, string? LastUpdated) ReadSteamBuildMetadata(string gamePath)
    {
        DirectoryInfo? steamApps = Directory.GetParent(gamePath)?.Parent;
        if (steamApps is null)
            return (null, null);
        string manifestPath = Path.Combine(steamApps.FullName, "appmanifest_2868840.acf");
        if (!File.Exists(manifestPath))
            return (null, null);

        string content = File.ReadAllText(manifestPath);
        return (ReadAcfValue(content, "buildid"), ReadAcfValue(content, "LastUpdated"));
    }

    private static string? ReadAcfValue(string content, string key)
    {
        Match match = AcfValueRegex().Match(content);
        while (match.Success)
        {
            if (string.Equals(match.Groups["key"].Value, key, StringComparison.OrdinalIgnoreCase))
                return match.Groups["value"].Value;
            match = match.NextMatch();
        }
        return null;
    }

    private static (string? Version, string? Hash) ReadBaseLibMetadata(string gamePath)
    {
        string[] candidates =
        [
            Path.Combine(gamePath, "mods", "BaseLib", "BaseLib.json"),
            Path.Combine(gamePath, "SlayTheSpire2.app", "Contents", "MacOS", "mods", "BaseLib", "BaseLib.json"),
        ];
        string? manifestPath = candidates.FirstOrDefault(File.Exists);
        if (manifestPath is null)
            return (null, null);

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            string? version = document.RootElement.TryGetProperty("version", out JsonElement value)
                ? value.GetString()
                : null;
            return (version, ComputeSha256(manifestPath));
        }
        catch (JsonException)
        {
            return (null, ComputeSha256(manifestPath));
        }
    }

    private static (int? Width, int? Height) ReadDimensions(string path, string extension)
    {
        try
        {
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                Span<byte> header = stackalloc byte[24];
                using FileStream stream = File.OpenRead(path);
                if (stream.Read(header) == header.Length &&
                    header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                {
                    return (
                        BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4)),
                        BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4)));
                }
            }

            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                string text = File.ReadAllText(path);
                Match width = SvgWidthRegex().Match(text);
                Match height = SvgHeightRegex().Match(text);
                if (width.Success && height.Success &&
                    int.TryParse(width.Groups[1].Value, out int parsedWidth) &&
                    int.TryParse(height.Groups[1].Value, out int parsedHeight))
                {
                    return (parsedWidth, parsedHeight);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
        return (null, null);
    }

    internal static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ToPortableRelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    [GeneratedRegex("\\\"(?<key>[^\\\"]+)\\\"\\s+\\\"(?<value>[^\\\"]*)\\\"")]
    private static partial Regex AcfValueRegex();

    [GeneratedRegex("\\bwidth=\\\"([0-9]+)")]
    private static partial Regex SvgWidthRegex();

    [GeneratedRegex("\\bheight=\\\"([0-9]+)")]
    private static partial Regex SvgHeightRegex();
}
