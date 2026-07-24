using System.Text;
using System.Text.Json;

namespace SasukeIronclad.GameAudit;

internal static class ReportWriter
{
    public static void WriteScan(AuditReport report, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(
            Path.Combine(outputDirectory, "audit-report.json"),
            JsonSerializer.Serialize(report, AuditJsonContext.Default.AuditReport));
        File.WriteAllText(Path.Combine(outputDirectory, "audit-report.md"), BuildMarkdown(report));
        File.WriteAllText(
            Path.Combine(outputDirectory, "audit-fingerprint.json"),
            JsonSerializer.Serialize(CreateFingerprint(report), AuditJsonContext.Default.AuditFingerprint));
    }

    public static AuditComparison Compare(AuditReport first, AuditReport second)
    {
        HashSet<string> firstSymbols = first.Symbols.Select(SymbolKey).ToHashSet(StringComparer.Ordinal);
        HashSet<string> secondSymbols = second.Symbols.Select(SymbolKey).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string?> firstAssets = first.Assets.ToDictionary(AssetIdentity, asset => asset.Sha256, StringComparer.Ordinal);
        Dictionary<string, string?> secondAssets = second.Assets.ToDictionary(AssetIdentity, asset => asset.Sha256, StringComparer.Ordinal);

        List<string> addedSymbols = secondSymbols.Except(firstSymbols, StringComparer.Ordinal).Order().ToList();
        List<string> removedSymbols = firstSymbols.Except(secondSymbols, StringComparer.Ordinal).Order().ToList();
        List<string> addedAssets = secondAssets.Keys.Except(firstAssets.Keys, StringComparer.Ordinal).Order().ToList();
        List<string> removedAssets = firstAssets.Keys.Except(secondAssets.Keys, StringComparer.Ordinal).Order().ToList();
        List<string> changedAssets = firstAssets.Keys.Intersect(secondAssets.Keys, StringComparer.Ordinal)
            .Where(key => !string.Equals(firstAssets[key], secondAssets[key], StringComparison.Ordinal))
            .Order()
            .ToList();

        bool sameAssembly = string.Equals(first.Game.Sts2AssemblySha256, second.Game.Sts2AssemblySha256, StringComparison.OrdinalIgnoreCase);
        bool sameSteamBuild = string.Equals(first.Game.SteamBuildId, second.Game.SteamBuildId, StringComparison.Ordinal);
        bool sameBaseLib = string.Equals(first.Game.BaseLibVersion, second.Game.BaseLibVersion, StringComparison.Ordinal);
        bool sameSymbols = addedSymbols.Count == 0 && removedSymbols.Count == 0;
        bool sameAssets = addedAssets.Count == 0 && removedAssets.Count == 0 && changedAssets.Count == 0;

        return new AuditComparison
        {
            ComparedAtUtc = DateTimeOffset.UtcNow,
            FirstSession = first.Session,
            SecondSession = second.Session,
            SameAssembly = sameAssembly,
            SameSteamBuild = sameSteamBuild,
            SameBaseLibVersion = sameBaseLib,
            SameSymbols = sameSymbols,
            SameAssets = sameAssets,
            Equivalent = sameAssembly && sameSteamBuild && sameBaseLib && sameSymbols && sameAssets,
            AddedSymbols = addedSymbols,
            RemovedSymbols = removedSymbols,
            AddedAssets = addedAssets,
            RemovedAssets = removedAssets,
            ChangedAssets = changedAssets,
        };
    }

    public static void WriteComparison(AuditComparison comparison, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(
            Path.Combine(outputDirectory, "audit-comparison.json"),
            JsonSerializer.Serialize(comparison, AuditJsonContext.Default.AuditComparison));
        File.WriteAllText(Path.Combine(outputDirectory, "audit-comparison.md"), BuildComparisonMarkdown(comparison));
    }

    public static AuditReport ReadReport(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, AuditJsonContext.Default.AuditReport)
            ?? throw new InvalidDataException($"Audit report was empty: {path}");
    }

    private static AuditFingerprint CreateFingerprint(AuditReport report) => new()
    {
        Session = report.Session,
        Sts2AssemblySha256 = report.Game.Sts2AssemblySha256,
        SteamBuildId = report.Game.SteamBuildId,
        BaseLibVersion = report.Game.BaseLibVersion,
        SymbolKeys = report.Symbols.Select(SymbolKey).Order().ToList(),
        AssetKeys = report.Assets.Select(asset => $"{AssetIdentity(asset)}|{asset.Sha256 ?? asset.HashStatus}").Order().ToList(),
    };

    private static string BuildMarkdown(AuditReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Slay the Spire 2 本地审计报告");
        builder.AppendLine();
        builder.AppendLine("> 该报告只包含路径、尺寸、哈希和托管元数据签名/常量，不包含游戏资源、方法体或反编译源码。");
        builder.AppendLine();
        builder.AppendLine($"- Session：`{Escape(report.Session)}`");
        builder.AppendLine($"- 生成时间：`{report.GeneratedAtUtc:O}`");
        builder.AppendLine($"- 分支：`{Escape(report.Branch)}`");
        builder.AppendLine($"- 操作系统：`{Escape(report.Environment.OperatingSystem)}`");
        builder.AppendLine($"- 架构：`{Escape(report.Environment.Architecture)}`");
        builder.AppendLine($"- .NET：`{Escape(report.Environment.DotnetVersion)}`");
        builder.AppendLine($"- MegaDot：`{Escape(report.Environment.MegaDotVersion ?? "未提供")}`");
        builder.AppendLine();

        builder.AppendLine("## 版本指纹");
        builder.AppendLine();
        builder.AppendLine("| 项目 | 值 |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| 数据目录 | `{Escape(report.Game.DataDirectory)}` |");
        builder.AppendLine($"| sts2.dll | `{Escape(report.Game.Sts2AssemblyRelativePath)}` |");
        builder.AppendLine($"| SHA-256 | `{Escape(report.Game.Sts2AssemblySha256)}` |");
        builder.AppendLine($"| Assembly | `{Escape(report.Game.AssemblyName)} {Escape(report.Game.AssemblyVersion)}` |");
        builder.AppendLine($"| MVID | `{Escape(report.Game.ModuleVersionId)}` |");
        builder.AppendLine($"| Steam buildid | `{Escape(report.Game.SteamBuildId ?? "未检测")}` |");
        builder.AppendLine($"| BaseLib | `{Escape(report.Game.BaseLibVersion ?? "未检测")}` |");
        builder.AppendLine();

        builder.AppendLine($"## 候选托管符号（{report.Symbols.Count}）");
        builder.AppendLine();
        builder.AppendLine("| 分数 | 种类 | 类别 | Token | 元数据常量 | 签名 |");
        builder.AppendLine("|---:|---|---|---|---|---|");
        foreach (SymbolCandidate symbol in report.Symbols.Take(400))
        {
            builder.AppendLine(
                $"| {symbol.Score} | {Escape(symbol.Kind)} | {Escape(string.Join(", ", symbol.Categories))} | " +
                $"`{Escape(symbol.MetadataToken)}` | `{Escape(symbol.ConstantValue ?? "-")}` | `{Escape(symbol.Signature)}` |");
        }
        builder.AppendLine();

        builder.AppendLine($"## 候选资源（{report.Assets.Count}）");
        builder.AppendLine();
        builder.AppendLine("| 分数 | 根 | 相对路径 | 尺寸 | 字节 | SHA-256/状态 |");
        builder.AppendLine("|---:|---|---|---:|---:|---|");
        foreach (AssetCandidate asset in report.Assets.Take(300))
        {
            string dimensions = asset.Width.HasValue && asset.Height.HasValue ? $"{asset.Width}×{asset.Height}" : "-";
            builder.AppendLine(
                $"| {asset.Score} | `{Escape(asset.RootLabel)}` | `{Escape(asset.RelativePath)}` | {dimensions} | " +
                $"{asset.SizeBytes} | `{Escape(asset.Sha256 ?? asset.HashStatus)}` |");
        }

        if (report.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## 警告");
            builder.AppendLine();
            foreach (string warning in report.Warnings)
                builder.AppendLine($"- {Escape(warning)}");
        }
        return builder.ToString();
    }

    private static string BuildComparisonMarkdown(AuditComparison comparison)
    {
        StringBuilder builder = new();
        builder.AppendLine("# 两次本地审计比较");
        builder.AppendLine();
        builder.AppendLine($"- 第一次：`{Escape(comparison.FirstSession)}`");
        builder.AppendLine($"- 第二次：`{Escape(comparison.SecondSession)}`");
        builder.AppendLine($"- 等价：`{comparison.Equivalent}`");
        builder.AppendLine();
        builder.AppendLine("| 检查 | 结果 |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| sts2.dll 指纹 | {comparison.SameAssembly} |");
        builder.AppendLine($"| Steam buildid | {comparison.SameSteamBuild} |");
        builder.AppendLine($"| BaseLib 版本 | {comparison.SameBaseLibVersion} |");
        builder.AppendLine($"| 候选符号集合 | {comparison.SameSymbols} |");
        builder.AppendLine($"| 候选资源集合/哈希 | {comparison.SameAssets} |");
        AppendChanges(builder, "新增符号", comparison.AddedSymbols);
        AppendChanges(builder, "移除符号", comparison.RemovedSymbols);
        AppendChanges(builder, "新增资源", comparison.AddedAssets);
        AppendChanges(builder, "移除资源", comparison.RemovedAssets);
        AppendChanges(builder, "哈希变化资源", comparison.ChangedAssets);
        return builder.ToString();
    }

    private static void AppendChanges(StringBuilder builder, string title, IReadOnlyCollection<string> values)
    {
        if (values.Count == 0)
            return;
        builder.AppendLine();
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        foreach (string value in values.Take(200))
            builder.AppendLine($"- `{Escape(value)}`");
    }

    private static string SymbolKey(SymbolCandidate symbol) =>
        $"{symbol.Kind}|{symbol.MetadataToken}|{symbol.Signature}|{symbol.ConstantValue ?? string.Empty}";

    private static string AssetIdentity(AssetCandidate asset) => $"{asset.RootLabel}|{asset.RelativePath}";
    private static string Escape(string value) => value.Replace("|", "\\|").Replace("`", "'").Replace("\r", " ").Replace("\n", " ");
}
