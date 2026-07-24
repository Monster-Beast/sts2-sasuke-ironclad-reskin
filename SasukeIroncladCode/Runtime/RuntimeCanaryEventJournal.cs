using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record RuntimeCanaryEventData(
    string EventType,
    int CombatIndex,
    string? BindingId = null,
    string? CardId = null,
    string? AnimationId = null,
    string? SurfaceId = null,
    int? ImpactIndex = null,
    bool? Upgraded = null,
    bool? AnimationsEnabled = null,
    bool? TitlesEnabled = null,
    bool? ReplacementRequested = null,
    bool? ReplacementActive = null,
    bool? AnchorBound = null,
    bool? OverlayVisible = null,
    int? RestoreCount = null,
    string? FailureScenario = null,
    string? FailureCardId = null,
    string? Reason = null
);

public sealed class RuntimeCanaryEventDocument
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("generated_at_utc")] public string GeneratedAtUtc { get; init; } = string.Empty;
    [JsonPropertyName("session_id")] public string SessionId { get; init; } = string.Empty;
    [JsonPropertyName("sequence")] public int Sequence { get; init; }
    [JsonPropertyName("combat_index")] public int CombatIndex { get; init; }
    [JsonPropertyName("event_type")] public string EventType { get; init; } = string.Empty;
    [JsonPropertyName("binding_id")] public string? BindingId { get; init; }
    [JsonPropertyName("card_id")] public string? CardId { get; init; }
    [JsonPropertyName("animation_id")] public string? AnimationId { get; init; }
    [JsonPropertyName("surface_id")] public string? SurfaceId { get; init; }
    [JsonPropertyName("impact_index")] public int? ImpactIndex { get; init; }
    [JsonPropertyName("upgraded")] public bool? Upgraded { get; init; }
    [JsonPropertyName("animations_enabled")] public bool? AnimationsEnabled { get; init; }
    [JsonPropertyName("titles_enabled")] public bool? TitlesEnabled { get; init; }
    [JsonPropertyName("replacement_requested")] public bool? ReplacementRequested { get; init; }
    [JsonPropertyName("replacement_active")] public bool? ReplacementActive { get; init; }
    [JsonPropertyName("anchor_bound")] public bool? AnchorBound { get; init; }
    [JsonPropertyName("overlay_visible")] public bool? OverlayVisible { get; init; }
    [JsonPropertyName("restore_count")] public int? RestoreCount { get; init; }
    [JsonPropertyName("failure_scenario")] public string? FailureScenario { get; init; }
    [JsonPropertyName("failure_card_id")] public string? FailureCardId { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
}

/// <summary>
/// Writes a bounded, privacy-safe JSONL event stream for an explicitly enabled
/// exact-build presentation canary. The journal records presentation identity
/// and state transitions only; it never serializes game objects, arguments,
/// card rules, absolute paths or gameplay values.
/// </summary>
public sealed class RuntimeCanaryEventJournal : IDisposable
{
    public const string OutputDirectoryName = "canary-output";
    private const int MaxEvents = 20_000;
    private const int MaxTextLength = 240;

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly object _sync = new();
    private readonly string _path;
    private int _sequence;
    private bool _writeFailed;
    private int _disposed;

    private RuntimeCanaryEventJournal(string path, string sessionId, string eventFileName)
    {
        _path = path;
        SessionId = sessionId;
        EventFileName = eventFileName;
    }

    public string SessionId { get; }
    public string EventFileName { get; }
    public int EventCount => Volatile.Read(ref _sequence);

    public static RuntimeCanaryEventJournal? TryCreate(string modAssemblyPath, RuntimeCanaryOptIn optIn)
    {
        ArgumentNullException.ThrowIfNull(optIn);
        try
        {
            string? modDirectory = ResolveModDirectory(modAssemblyPath);
            if (string.IsNullOrWhiteSpace(modDirectory))
                return null;

            string outputDirectory = Path.Combine(modDirectory, OutputDirectoryName);
            Directory.CreateDirectory(outputDirectory);
            string label = NormalizeSessionLabel(optIn.SessionLabel);
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'");
            string sessionId = $"{label}-{timestamp}-{Guid.NewGuid():N}";
            string eventFileName = $"runtime-canary-{sessionId}.jsonl";
            string path = Path.Combine(outputDirectory, eventFileName);
            File.WriteAllText(path, string.Empty, Utf8NoBom);
            return new RuntimeCanaryEventJournal(path, sessionId, eventFileName);
        }
        catch
        {
            return null;
        }
    }

    public void Write(RuntimeCanaryEventData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(data.EventType) || Volatile.Read(ref _disposed) != 0)
            return;

        lock (_sync)
        {
            if (_writeFailed || _sequence >= MaxEvents || Volatile.Read(ref _disposed) != 0)
                return;

            try
            {
                int sequence = ++_sequence;
                RuntimeCanaryEventDocument document = new()
                {
                    GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    SessionId = SessionId,
                    Sequence = sequence,
                    CombatIndex = Math.Max(0, data.CombatIndex),
                    EventType = Sanitize(data.EventType) ?? "unknown",
                    BindingId = Sanitize(data.BindingId),
                    CardId = Sanitize(data.CardId),
                    AnimationId = Sanitize(data.AnimationId),
                    SurfaceId = Sanitize(data.SurfaceId),
                    ImpactIndex = data.ImpactIndex,
                    Upgraded = data.Upgraded,
                    AnimationsEnabled = data.AnimationsEnabled,
                    TitlesEnabled = data.TitlesEnabled,
                    ReplacementRequested = data.ReplacementRequested,
                    ReplacementActive = data.ReplacementActive,
                    AnchorBound = data.AnchorBound,
                    OverlayVisible = data.OverlayVisible,
                    RestoreCount = data.RestoreCount,
                    FailureScenario = Sanitize(data.FailureScenario),
                    FailureCardId = Sanitize(data.FailureCardId),
                    Reason = Sanitize(data.Reason),
                };
                File.AppendAllText(
                    _path,
                    JsonSerializer.Serialize(document, WriteOptions) + Environment.NewLine,
                    Utf8NoBom);
            }
            catch
            {
                _writeFailed = true;
            }
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
    }

    private static string NormalizeSessionLabel(string? value)
    {
        string source = string.IsNullOrWhiteSpace(value) ? "runtime-canary" : value.Trim();
        StringBuilder builder = new(capacity: Math.Min(source.Length, 48));
        foreach (char character in source)
        {
            if (builder.Length >= 48)
                break;
            if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
                builder.Append(character);
        }
        if (builder.Length == 0)
            builder.Append("runtime-canary");
        if (!char.IsLetterOrDigit(builder[0]))
            builder.Insert(0, "run-");
        return builder.ToString();
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= MaxTextLength ? sanitized : sanitized[..MaxTextLength];
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
