using System.Globalization;
using System.Reflection;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record ResolvedRuntimeCanaryTarget(
    RuntimeCanaryBindingDecision Decision,
    RuntimeObservationTargetSpec ManifestTarget,
    MethodBase Method
);

public sealed record RuntimeCanaryResolutionResult(
    bool Success,
    IReadOnlyList<ResolvedRuntimeCanaryTarget> Targets,
    IReadOnlyList<string> Reasons
);

public static class RuntimeCanaryTargetResolver
{
    public static RuntimeCanaryResolutionResult Resolve(
        Assembly gameAssembly,
        RuntimeObservationManifestMap manifest,
        IReadOnlyList<RuntimeCanaryBindingDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(gameAssembly);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(decisions);

        if (!Guid.TryParse(manifest.Fingerprint.ModuleMvid, out Guid expectedMvid) ||
            gameAssembly.ManifestModule.ModuleVersionId != expectedMvid)
        {
            return new(false, [], ["Loaded game module MVID does not match the reviewed canary manifest."]);
        }

        Dictionary<string, RuntimeObservationTargetSpec> manifestTargets;
        try
        {
            manifestTargets = manifest.Targets.ToDictionary(target => target.Id, StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            return new(false, [], ["Canary observation manifest contains duplicate target IDs."]);
        }

        List<string> reasons = [];
        List<ResolvedRuntimeCanaryTarget> resolved = [];
        HashSet<int> resolvedTokens = [];
        foreach (RuntimeCanaryBindingDecision decision in decisions)
        {
            if (!manifestTargets.TryGetValue(decision.SelectedTargetId, out RuntimeObservationTargetSpec? manifestTarget))
            {
                reasons.Add($"Canary binding {decision.BindingId} references a missing reviewed target.");
                continue;
            }
            if (!manifestTarget.BindingIds.Contains(decision.BindingId, StringComparer.Ordinal) ||
                !string.Equals(manifestTarget.DeclaringType, decision.DeclaringType, StringComparison.Ordinal) ||
                !string.Equals(Normalize(manifestTarget.MethodSignature), Normalize(decision.MethodSignature), StringComparison.Ordinal) ||
                !string.Equals(manifestTarget.MetadataToken, decision.MetadataToken, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"Canary binding {decision.BindingId} no longer matches the reviewed observation target.");
                continue;
            }
            if (!TryParseMethodDefinitionToken(decision.MetadataToken, out int token))
            {
                reasons.Add($"Canary binding {decision.BindingId} does not contain a valid MethodDef token.");
                continue;
            }

            MethodBase? method;
            try
            {
                method = gameAssembly.ManifestModule.ResolveMethod(token);
            }
            catch (Exception exception) when (exception is ArgumentException or BadImageFormatException)
            {
                reasons.Add($"Canary binding {decision.BindingId} could not resolve its reviewed MethodDef token.");
                continue;
            }
            if (method is null || method.MetadataToken != token || method.Module.ModuleVersionId != expectedMvid)
            {
                reasons.Add($"Canary binding {decision.BindingId} resolved outside the reviewed method identity.");
                continue;
            }
            if (!string.Equals(method.DeclaringType?.FullName, decision.DeclaringType, StringComparison.Ordinal))
            {
                reasons.Add($"Canary binding {decision.BindingId} declaring type does not match the reviewed target.");
                continue;
            }
            string actualSignature = AuditedMethodBindingResolver.FormatMethodSignature(method);
            if (!string.Equals(Normalize(actualSignature), Normalize(decision.MethodSignature), StringComparison.Ordinal))
            {
                reasons.Add($"Canary binding {decision.BindingId} method signature does not match the reviewed target.");
                continue;
            }
            if (method.IsConstructor || method.IsAbstract || method.ContainsGenericParameters)
            {
                reasons.Add($"Canary binding {decision.BindingId} resolves to an unsupported method shape.");
                continue;
            }
            if (!resolvedTokens.Add(token))
            {
                reasons.Add($"Canary binding {decision.BindingId} duplicates an already selected MethodDef token.");
                continue;
            }
            resolved.Add(new(decision, manifestTarget, method));
        }

        if (reasons.Count > 0 || resolved.Count != decisions.Count)
            return new(false, [], reasons.Count > 0 ? reasons : ["Not all reviewed canary targets resolved."]);
        return new(true, resolved, [$"Resolved {resolved.Count} reviewed exact-build canary targets."]);
    }

    private static bool TryParseMethodDefinitionToken(string value, out int token)
    {
        token = 0;
        if (!RuntimeObservationGate.IsMethodDefinitionToken(value) ||
            !uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed))
        {
            return false;
        }
        token = unchecked((int)parsed);
        return true;
    }

    private static string Normalize(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
