using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed class RuntimeObservationEvent
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("timestamp_utc")] public string TimestampUtc { get; init; } = string.Empty;
    [JsonPropertyName("session_id")] public string SessionId { get; init; } = string.Empty;
    [JsonPropertyName("event_sequence")] public long EventSequence { get; init; }
    [JsonPropertyName("call_id")] public long? CallId { get; init; }
    [JsonPropertyName("phase")] public string Phase { get; init; } = string.Empty;
    [JsonPropertyName("target_id")] public string? TargetId { get; init; }
    [JsonPropertyName("binding_ids")] public IReadOnlyList<string> BindingIds { get; init; } = [];
    [JsonPropertyName("metadata_token")] public string? MetadataToken { get; init; }
    [JsonPropertyName("declaring_type")] public string? DeclaringType { get; init; }
    [JsonPropertyName("method_name")] public string? MethodName { get; init; }
    [JsonPropertyName("thread_id")] public int ThreadId { get; init; }
    [JsonPropertyName("elapsed_ms")] public double? ElapsedMs { get; init; }
    [JsonPropertyName("instance_type")] public string? InstanceType { get; init; }
    [JsonPropertyName("arguments")] public IReadOnlyList<RuntimeObservationArgumentSummary> Arguments { get; init; } = [];
    [JsonPropertyName("related_model_types")] public IReadOnlyList<string> RelatedModelTypes { get; init; } = [];
    [JsonPropertyName("caller_stack")] public IReadOnlyList<string> CallerStack { get; init; } = [];
    [JsonPropertyName("runtime")] public RuntimeObservationRuntimeSummary? Runtime { get; init; }
    [JsonPropertyName("target_count")] public int? TargetCount { get; init; }
    [JsonPropertyName("max_events")] public int? MaxEvents { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
}

public sealed class RuntimeObservationArgumentSummary
{
    [JsonPropertyName("index")] public int Index { get; init; }
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("safe_value")] public string? SafeValue { get; init; }
}

public sealed class RuntimeObservationRuntimeSummary
{
    [JsonPropertyName("branch")] public string Branch { get; init; } = string.Empty;
    [JsonPropertyName("steam_build_id")] public string SteamBuildId { get; init; } = string.Empty;
    [JsonPropertyName("sts2_sha256")] public string Sts2Sha256 { get; init; } = string.Empty;
    [JsonPropertyName("module_mvid")] public string ModuleMvid { get; init; } = string.Empty;
    [JsonPropertyName("baselib_version")] public string BaseLibVersion { get; init; } = string.Empty;
}

public interface IRuntimeObservationSink : IDisposable
{
    string OutputPath { get; }
    void Write(RuntimeObservationEvent observationEvent);
}

public sealed class JsonLinesRuntimeObservationSink : IRuntimeObservationSink
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly object _sync = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public JsonLinesRuntimeObservationSink(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Observation output path is required.", nameof(outputPath));
        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Observation output directory could not be derived.");
        Directory.CreateDirectory(directory);
        _writer = new StreamWriter(
            new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
        OutputPath = fullPath;
    }

    public string OutputPath { get; }

    public void Write(RuntimeObservationEvent observationEvent)
    {
        ArgumentNullException.ThrowIfNull(observationEvent);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer.WriteLine(JsonSerializer.Serialize(observationEvent, JsonOptions));
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            _writer.Dispose();
        }
    }
}

public readonly record struct RuntimeObservationCallState(
    bool Recorded,
    long CallId,
    long StartedTimestamp,
    ResolvedRuntimeObservationTarget? Target
);

public sealed class RuntimeObservationSession : IDisposable
{
    private readonly RuntimeObservationManifestMap _manifest;
    private readonly RuntimeBuildFingerprint _runtime;
    private readonly IRuntimeObservationSink _sink;
    private readonly int _maxEvents;
    private readonly int _maxStackFrames;
    private long _eventSequence;
    private long _callSequence;
    private int _disabled;
    private int _disposed;

    public RuntimeObservationSession(
        RuntimeObservationManifestMap manifest,
        RuntimeBuildFingerprint runtime,
        RuntimeObservationOptIn optIn,
        int maxEvents,
        int maxStackFrames,
        IRuntimeObservationSink sink)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(optIn);
        ArgumentNullException.ThrowIfNull(sink);
        _manifest = manifest;
        _runtime = runtime;
        _sink = sink;
        _maxEvents = maxEvents;
        _maxStackFrames = maxStackFrames;
        SessionId = $"{optIn.SessionLabel}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}";
        TryWrite(new RuntimeObservationEvent
        {
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            SessionId = SessionId,
            EventSequence = NextEventSequence(),
            Phase = "session_start",
            ThreadId = Environment.CurrentManagedThreadId,
            Runtime = new RuntimeObservationRuntimeSummary
            {
                Branch = runtime.Branch,
                SteamBuildId = runtime.SteamBuildId,
                Sts2Sha256 = runtime.Sts2Sha256,
                ModuleMvid = runtime.ModuleMvid,
                BaseLibVersion = runtime.BaseLibVersion,
            },
            TargetCount = manifest.Targets.Count,
            MaxEvents = maxEvents,
            Reason = "Explicit read_only opt-in accepted; no return values, arguments or game state are modified.",
        });
    }

    public string SessionId { get; }
    public string OutputPath => _sink.OutputPath;
    public bool IsDisabled => Volatile.Read(ref _disabled) != 0;

    public RuntimeObservationCallState BeginInvocation(
        ResolvedRuntimeObservationTarget target,
        MethodBase method,
        object? instance,
        object?[]? args)
    {
        if (IsDisabled || Volatile.Read(ref _disposed) != 0)
            return default;
        long callId = Interlocked.Increment(ref _callSequence);
        RuntimeObservationSnapshot snapshot = RuntimeObservationValueSummarizer.Summarize(
            instance,
            args,
            target.Spec.CaptureStack ? _maxStackFrames : 0);
        bool recorded = TryWrite(new RuntimeObservationEvent
        {
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            SessionId = SessionId,
            EventSequence = NextEventSequence(),
            CallId = callId,
            Phase = "enter",
            TargetId = target.Spec.Id,
            BindingIds = target.Spec.BindingIds,
            MetadataToken = target.Spec.MetadataToken,
            DeclaringType = method.DeclaringType?.FullName,
            MethodName = method.Name,
            ThreadId = Environment.CurrentManagedThreadId,
            InstanceType = snapshot.InstanceType,
            Arguments = snapshot.Arguments,
            RelatedModelTypes = snapshot.RelatedModelTypes,
            CallerStack = snapshot.CallerStack,
        });
        return new(recorded, callId, Stopwatch.GetTimestamp(), target);
    }

    public void EndInvocation(
        RuntimeObservationCallState state,
        MethodBase method,
        object? instance,
        object?[]? args)
    {
        if (!state.Recorded || state.Target is null || IsDisabled || Volatile.Read(ref _disposed) != 0)
            return;
        RuntimeObservationSnapshot snapshot = RuntimeObservationValueSummarizer.Summarize(instance, args, maxStackFrames: 0);
        TryWrite(new RuntimeObservationEvent
        {
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            SessionId = SessionId,
            EventSequence = NextEventSequence(),
            CallId = state.CallId,
            Phase = "return",
            TargetId = state.Target.Spec.Id,
            BindingIds = state.Target.Spec.BindingIds,
            MetadataToken = state.Target.Spec.MetadataToken,
            DeclaringType = method.DeclaringType?.FullName,
            MethodName = method.Name,
            ThreadId = Environment.CurrentManagedThreadId,
            ElapsedMs = Math.Round(Stopwatch.GetElapsedTime(state.StartedTimestamp).TotalMilliseconds, 3),
            InstanceType = snapshot.InstanceType,
            Arguments = snapshot.Arguments,
            RelatedModelTypes = snapshot.RelatedModelTypes,
        });
    }

    private long NextEventSequence()
    {
        long sequence = Interlocked.Increment(ref _eventSequence);
        if (sequence > _maxEvents)
            Interlocked.Exchange(ref _disabled, 1);
        return sequence;
    }

    private bool TryWrite(RuntimeObservationEvent observationEvent)
    {
        if (observationEvent.EventSequence > _maxEvents || IsDisabled)
            return false;
        try
        {
            _sink.Write(observationEvent);
            return true;
        }
        catch
        {
            Interlocked.Exchange(ref _disabled, 1);
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (!IsDisabled)
        {
            TryWrite(new RuntimeObservationEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
                SessionId = SessionId,
                EventSequence = NextEventSequence(),
                Phase = "session_stop",
                ThreadId = Environment.CurrentManagedThreadId,
                Reason = "Observation session disposed and patches are being removed.",
            });
        }
        _sink.Dispose();
    }
}

internal sealed record RuntimeObservationSnapshot(
    string? InstanceType,
    IReadOnlyList<RuntimeObservationArgumentSummary> Arguments,
    IReadOnlyList<string> RelatedModelTypes,
    IReadOnlyList<string> CallerStack
);

internal static class RuntimeObservationValueSummarizer
{
    private const int MaxRelatedTypes = 16;
    private const int MaxFieldDepth = 2;

    public static RuntimeObservationSnapshot Summarize(
        object? instance,
        object?[]? args,
        int maxStackFrames)
    {
        List<RuntimeObservationArgumentSummary> arguments = [];
        if (args is not null)
        {
            for (int index = 0; index < args.Length; index++)
            {
                object? value = args[index];
                arguments.Add(new RuntimeObservationArgumentSummary
                {
                    Index = index,
                    Type = value?.GetType().FullName ?? "null",
                    SafeValue = SafePrimitiveValue(value),
                });
            }
        }

        HashSet<string> relatedTypes = new(StringComparer.Ordinal);
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        CollectRelatedTypes(instance, relatedTypes, visited, depth: 0);
        if (args is not null)
        {
            foreach (object? argument in args)
                CollectRelatedTypes(argument, relatedTypes, visited, depth: 0);
        }

        return new(
            instance?.GetType().FullName,
            arguments,
            relatedTypes.Order(StringComparer.Ordinal).Take(MaxRelatedTypes).ToArray(),
            maxStackFrames > 0 ? CaptureCallerStack(maxStackFrames) : []
        );
    }

    private static string? SafePrimitiveValue(object? value)
    {
        if (value is null)
            return null;
        Type type = value.GetType();
        if (type.IsEnum)
            return Enum.GetName(type, value) ?? Convert.ToInt64(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return value switch
        {
            bool boolean => boolean ? "true" : "false",
            byte number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            sbyte number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            short number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ushort number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            int number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            uint number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            long number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ulong number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            float number => number.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            double number => number.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            decimal number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    private static void CollectRelatedTypes(
        object? value,
        HashSet<string> relatedTypes,
        HashSet<object> visited,
        int depth)
    {
        if (value is null || depth > MaxFieldDepth || relatedTypes.Count >= MaxRelatedTypes)
            return;
        Type type = value.GetType();
        if (IsRelatedModelType(type))
            relatedTypes.Add(type.FullName ?? type.Name);
        if (type.IsValueType || value is string || !visited.Add(value))
            return;

        foreach (FieldInfo field in EnumerateSafeFields(type))
        {
            object? nested;
            try
            {
                nested = field.GetValue(value);
            }
            catch
            {
                continue;
            }
            CollectRelatedTypes(nested, relatedTypes, visited, depth + 1);
            if (relatedTypes.Count >= MaxRelatedTypes)
                return;
        }
    }

    private static IEnumerable<FieldInfo> EnumerateSafeFields(Type type)
    {
        for (Type? current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.IsStatic || field.FieldType.IsPointer || field.FieldType.IsByRef)
                    continue;
                string fieldName = field.Name;
                string fieldType = field.FieldType.FullName ?? field.FieldType.Name;
                if (LooksLikeRelatedField(fieldName, fieldType))
                    yield return field;
            }
        }
    }

    private static bool LooksLikeRelatedField(string fieldName, string fieldType)
    {
        string normalizedName = fieldName.ToLowerInvariant();
        return fieldType.StartsWith("MegaCrit.Sts2.Core.", StringComparison.Ordinal) &&
               (normalizedName.Contains("card", StringComparison.Ordinal) ||
                normalizedName.Contains("model", StringComparison.Ordinal) ||
                normalizedName.Contains("power", StringComparison.Ordinal) ||
                normalizedName.Contains("state", StringComparison.Ordinal) ||
                normalizedName.Contains("holder", StringComparison.Ordinal));
    }

    private static bool IsRelatedModelType(Type type)
    {
        string fullName = type.FullName ?? string.Empty;
        return fullName.StartsWith("MegaCrit.Sts2.Core.Models.Cards.", StringComparison.Ordinal) ||
               fullName.StartsWith("MegaCrit.Sts2.Core.Models.Powers.", StringComparison.Ordinal) ||
               fullName is "MegaCrit.Sts2.Core.Models.CardModel" or
                   "MegaCrit.Sts2.Core.Models.PowerModel" or
                   "MegaCrit.Sts2.Core.Entities.Cards.CardPlay" or
                   "MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState";
    }

    private static IReadOnlyList<string> CaptureCallerStack(int maxFrames)
    {
        List<string> frames = [];
        StackTrace trace = new(skipFrames: 2, fNeedFileInfo: false);
        foreach (StackFrame frame in trace.GetFrames())
        {
            MethodBase? method = frame.GetMethod();
            string? typeName = method?.DeclaringType?.FullName;
            if (string.IsNullOrWhiteSpace(typeName) ||
                (!typeName.StartsWith("MegaCrit.Sts2.", StringComparison.Ordinal) &&
                 !typeName.StartsWith("SasukeIronclad.", StringComparison.Ordinal)))
            {
                continue;
            }
            frames.Add($"{typeName}.{method!.Name}");
            if (frames.Count >= maxFrames)
                break;
        }
        return frames;
    }
}

public static class RuntimeObservationPatchBridge
{
    private static readonly ConcurrentDictionary<int, ResolvedRuntimeObservationTarget> Targets = new();
    private static RuntimeObservationSession? _session;

    public static void Configure(
        RuntimeObservationSession session,
        IEnumerable<ResolvedRuntimeObservationTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(targets);
        Targets.Clear();
        foreach (ResolvedRuntimeObservationTarget target in targets)
            Targets[target.Method.MetadataToken] = target;
        Volatile.Write(ref _session, session);
    }

    public static void Reset()
    {
        Volatile.Write(ref _session, null);
        Targets.Clear();
    }

    public static void Prefix(
        MethodBase __originalMethod,
        object? __instance,
        object?[]? __args,
        out RuntimeObservationCallState __state)
    {
        __state = default;
        try
        {
            RuntimeObservationSession? session = Volatile.Read(ref _session);
            if (session is null || !Targets.TryGetValue(__originalMethod.MetadataToken, out ResolvedRuntimeObservationTarget? target))
                return;
            __state = session.BeginInvocation(target, __originalMethod, __instance, __args);
        }
        catch
        {
            __state = default;
        }
    }

    public static void Postfix(
        MethodBase __originalMethod,
        object? __instance,
        object?[]? __args,
        RuntimeObservationCallState __state)
    {
        try
        {
            RuntimeObservationSession? session = Volatile.Read(ref _session);
            session?.EndInvocation(__state, __originalMethod, __instance, __args);
        }
        catch
        {
            // Observation must never affect the original method.
        }
    }
}
