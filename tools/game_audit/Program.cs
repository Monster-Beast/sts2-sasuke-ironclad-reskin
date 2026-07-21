using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SasukeIronclad.GameAudit;

internal static class Program
{
    private const int DefaultMaxSymbols = 1500;
    private const int DefaultMaxAssets = 2500;
    private const long DefaultMaxHashBytes = 64L * 1024L * 1024L;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
            {
                PrintHelp();
                return 0;
            }

            string command = args[0].StartsWith("--", StringComparison.Ordinal) ? "scan" : args[0].ToLowerInvariant();
            string[] commandArgs = command == "scan" && args[0].StartsWith("--", StringComparison.Ordinal)
                ? args
                : args.Skip(1).ToArray();

            return command switch
            {
                "scan" => RunScan(commandArgs),
                "compare" => RunCompare(commandArgs),
                "self-test" => RunSelfTest(commandArgs),
                _ => throw new AuditUsageException($"Unknown command: {command}"),
            };
        }
        catch (AuditUsageException exception)
        {
            Console.Error.WriteLine($"Usage error: {exception.Message}");
            PrintHelp();
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Audit failed: {exception.Message}");
            return 1;
        }
    }

    private static int RunScan(string[] args)
    {
        ParsedArguments parsed = ParsedArguments.Parse(args);
        string gamePath = parsed.RequireSingle("--game-path");
        string output = parsed.GetSingle("--output") ?? Path.Combine("audit-output", "latest");
        string branch = parsed.GetSingle("--branch") ?? "unknown";
        string session = parsed.GetSingle("--session") ?? DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        string queryPath = parsed.GetSingle("--queries") ?? FindDefaultQueryPath();
        string? megaDotVersion = parsed.GetSingle("--megadot-version");
        int maxSymbols = parsed.GetPositiveInt("--max-symbols", DefaultMaxSymbols);
        int maxAssets = parsed.GetPositiveInt("--max-assets", DefaultMaxAssets);
        long maxHashBytes = parsed.GetPositiveLong("--max-hash-mb", DefaultMaxHashBytes / 1024L / 1024L) * 1024L * 1024L;

        AuditOptions options = new(
            gamePath,
            parsed.GetMany("--asset-root"),
            output,
            branch,
            session,
            megaDotVersion,
            queryPath,
            maxSymbols,
            maxAssets,
            maxHashBytes);

        AuditReport report = AuditScanner.Scan(options);
        ReportWriter.WriteScan(report, output);
        Console.WriteLine(
            $"GAME_AUDIT_OK session={report.Session} symbols={report.Symbols.Count} assets={report.Assets.Count} output={output}");
        return 0;
    }

    private static int RunCompare(string[] args)
    {
        ParsedArguments parsed = ParsedArguments.Parse(args);
        string firstPath = parsed.RequireSingle("--first");
        string secondPath = parsed.RequireSingle("--second");
        string output = parsed.GetSingle("--output") ?? Path.Combine("audit-output", "comparison");

        AuditReport first = ReportWriter.ReadReport(firstPath);
        AuditReport second = ReportWriter.ReadReport(secondPath);
        AuditComparison comparison = ReportWriter.Compare(first, second);
        ReportWriter.WriteComparison(comparison, output);
        Console.WriteLine(
            $"GAME_AUDIT_COMPARE_OK equivalent={comparison.Equivalent} first={comparison.FirstSession} second={comparison.SecondSession}");
        return comparison.Equivalent ? 0 : 3;
    }

    private static int RunSelfTest(string[] args)
    {
        ParsedArguments parsed = ParsedArguments.Parse(args);
        string outputRoot = Path.GetFullPath(parsed.GetSingle("--output") ?? Path.Combine("audit-output", "self-test"));
        if (Directory.Exists(outputRoot))
            Directory.Delete(outputRoot, recursive: true);

        string steamApps = Path.Combine(outputRoot, "fixture", "steamapps");
        string gamePath = Path.Combine(steamApps, "common", "Slay the Spire 2");
        string dataPath = Path.Combine(gamePath, "data_sts2_windows_x86_64");
        string workshopBaseLib = Path.Combine(steamApps, "workshop", "content", "2868840", "self-test-baselib");
        string assetRoot = Path.Combine(outputRoot, "fixture", "recovered");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(workshopBaseLib);
        Directory.CreateDirectory(Path.Combine(assetRoot, "images", "cards", "ironclad"));
        Directory.CreateDirectory(Path.Combine(assetRoot, "animations", "characters", "ironclad"));
        Directory.CreateDirectory(Path.Combine(assetRoot, "audit_fixture"));

        string currentAssembly = Assembly.GetExecutingAssembly().Location;
        File.Copy(currentAssembly, Path.Combine(dataPath, "sts2.dll"));
        File.WriteAllText(
            Path.Combine(workshopBaseLib, "BaseLib.json"),
            "{\"id\":\"BaseLib\",\"version\":\"v0.0.0-self-test\"}");
        File.WriteAllText(
            Path.Combine(steamApps, "appmanifest_2868840.acf"),
            "\"AppState\"\n{\n  \"appid\" \"2868840\"\n  \"buildid\" \"999999\"\n  \"LastUpdated\" \"1777777777\"\n}\n");
        File.WriteAllBytes(Path.Combine(gamePath, "SlayTheSpire2.pck"), Encoding.UTF8.GetBytes("self-test-pck"));
        File.WriteAllBytes(
            Path.Combine(assetRoot, "images", "cards", "ironclad", "strike.png"),
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZQmcAAAAASUVORK5CYII="));
        File.WriteAllText(
            Path.Combine(assetRoot, "animations", "characters", "ironclad", "combat_animation.tres"),
            "[gd_resource type=\"Resource\" format=3]\n");
        File.WriteAllText(Path.Combine(assetRoot, "audit_fixture", "card_title_surface.json"), "{}");

        string firstOutput = Path.Combine(outputRoot, "first");
        string secondOutput = Path.Combine(outputRoot, "second");
        string comparisonOutput = Path.Combine(outputRoot, "comparison");
        string queryPath = FindDefaultQueryPath();

        AuditOptions firstOptions = new(
            gamePath,
            [assetRoot],
            firstOutput,
            "self-test",
            "self-test-1",
            "4.5.1-self-test",
            queryPath,
            DefaultMaxSymbols,
            DefaultMaxAssets,
            DefaultMaxHashBytes);
        AuditOptions secondOptions = firstOptions with
        {
            OutputDirectory = secondOutput,
            Session = "self-test-2",
        };

        AuditReport first = AuditScanner.Scan(firstOptions);
        AuditReport second = AuditScanner.Scan(secondOptions);
        ReportWriter.WriteScan(first, firstOutput);
        ReportWriter.WriteScan(second, secondOutput);
        AuditComparison comparison = ReportWriter.Compare(first, second);
        ReportWriter.WriteComparison(comparison, comparisonOutput);

        string priorityType = typeof(AuditFixtureSymbols).FullName
            ?? throw new InvalidOperationException("Priority fixture type name was unavailable.");
        string priorityQueryPath = Path.Combine(outputRoot, "priority-query.json");
        AuditQuerySet priorityQuery = new()
        {
            SchemaVersion = 1,
            Queries =
            [
                new AuditQuery
                {
                    Id = "priority_self_test",
                    Priority = true,
                    ExactTypeNames = [priorityType],
                },
            ],
        };
        File.WriteAllText(
            priorityQueryPath,
            JsonSerializer.Serialize(priorityQuery, AuditJsonContext.Default.AuditQuerySet));
        AuditReport priorityReport = AuditScanner.Scan(firstOptions with
        {
            QueryPath = priorityQueryPath,
            MaxSymbols = 1,
            Session = "priority-self-test",
            OutputDirectory = Path.Combine(outputRoot, "priority"),
        });

        string firstJson = File.ReadAllText(Path.Combine(firstOutput, "audit-report.json"));
        if (first.Symbols.Count == 0 || first.Assets.Count == 0)
            throw new InvalidOperationException("Self-test did not produce symbol and asset candidates.");
        if (!string.Equals(first.Game.BaseLibVersion, "v0.0.0-self-test", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(first.Game.BaseLibManifestSha256))
        {
            throw new InvalidOperationException("Self-test did not discover the Workshop BaseLib manifest.");
        }
        if (priorityReport.Symbols.Count != 1 ||
            !priorityReport.Symbols[0].DeclaringType.StartsWith(priorityType, StringComparison.Ordinal) ||
            !priorityReport.Symbols[0].Categories.Contains("priority_self_test", StringComparer.Ordinal) ||
            priorityReport.Symbols[0].Score < 100_000)
        {
            throw new InvalidOperationException("Exact priority symbol retention did not survive a one-symbol limit.");
        }
        if (!comparison.Equivalent)
            throw new InvalidOperationException("Two identical self-test scans did not compare as equivalent.");
        if (firstJson.Contains(outputRoot, StringComparison.OrdinalIgnoreCase) ||
            firstJson.Contains(gamePath, StringComparison.OrdinalIgnoreCase) ||
            firstJson.Contains(assetRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Audit report leaked an absolute source path.");
        }

        Console.WriteLine(
            $"GAME_AUDIT_SELF_TEST_OK symbols={first.Symbols.Count} assets={first.Assets.Count} " +
            $"equivalent={comparison.Equivalent} baselib={first.Game.BaseLibVersion} priority=true");
        return 0;
    }

    private static string FindDefaultQueryPath()
    {
        string besideBinary = Path.Combine(AppContext.BaseDirectory, "audit-queries.json");
        if (File.Exists(besideBinary))
            return besideBinary;
        string repositoryPath = Path.Combine("tools", "game_audit", "audit-queries.json");
        return File.Exists(repositoryPath)
            ? repositoryPath
            : throw new FileNotFoundException("audit-queries.json was not found beside the tool or in tools/game_audit.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            SasukeIronclad local game audit

            Commands:
              scan --game-path <path> [--asset-root <path>] [--output <path>]
              compare --first <report> --second <report> [--output <path>]
              self-test [--output <path>]

            Scan options:
              --branch <name>
              --session <label>
              --megadot-version <version>
              --queries <path>
              --max-symbols <count>
              --max-assets <count>
              --max-hash-mb <count>
            """);
    }

    private sealed class ParsedArguments
    {
        private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);

        public static ParsedArguments Parse(IReadOnlyList<string> args)
        {
            ParsedArguments parsed = new();
            for (int index = 0; index < args.Count; index++)
            {
                string key = args[index];
                if (!key.StartsWith("--", StringComparison.Ordinal))
                    throw new AuditUsageException($"Expected an option, got: {key}");
                if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new AuditUsageException($"Missing value for {key}");
                string value = args[++index];
                if (!parsed._values.TryGetValue(key, out List<string>? values))
                {
                    values = [];
                    parsed._values[key] = values;
                }
                values.Add(value);
            }
            return parsed;
        }

        public string RequireSingle(string key) =>
            GetSingle(key) ?? throw new AuditUsageException($"Required option is missing: {key}");

        public string? GetSingle(string key)
        {
            if (!_values.TryGetValue(key, out List<string>? values) || values.Count == 0)
                return null;
            if (values.Count > 1)
                throw new AuditUsageException($"Option may only be specified once: {key}");
            return values[0];
        }

        public IReadOnlyList<string> GetMany(string key) =>
            _values.TryGetValue(key, out List<string>? values) ? values : [];

        public int GetPositiveInt(string key, int fallback)
        {
            string? value = GetSingle(key);
            if (value is null)
                return fallback;
            return int.TryParse(value, out int parsed) && parsed > 0
                ? parsed
                : throw new AuditUsageException($"{key} must be a positive integer.");
        }

        public long GetPositiveLong(string key, long fallback)
        {
            string? value = GetSingle(key);
            if (value is null)
                return fallback;
            return long.TryParse(value, out long parsed) && parsed > 0
                ? parsed
                : throw new AuditUsageException($"{key} must be a positive integer.");
        }
    }
}
