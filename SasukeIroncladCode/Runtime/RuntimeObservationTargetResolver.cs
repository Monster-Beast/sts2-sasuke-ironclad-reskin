using System.Globalization;
using System.Reflection;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record ResolvedRuntimeObservationTarget(
    RuntimeObservationTargetSpec Spec,
    MethodBase Method
);

public sealed record RuntimeObservationResolutionResult(
    bool Success,
    IReadOnlyList<ResolvedRuntimeObservationTarget> Targets,
    IReadOnlyList<string> Reasons
);

public static class RuntimeObservationTargetResolver
{
    public static RuntimeObservationResolutionResult Resolve(
        Assembly gameAssembly,
        RuntimeObservationManifestMap manifest)
    {
        ArgumentNullException.ThrowIfNull(gameAssembly);
        ArgumentNullException.ThrowIfNull(manifest);

        List<string> reasons = [];
        if (!Guid.TryParse(manifest.Fingerprint.ModuleMvid, out Guid expectedMvid) ||
            gameAssembly.ManifestModule.ModuleVersionId != expectedMvid)
        {
            return new(false, [], ["Loaded game module MVID does not match the observation manifest."]);
        }

        List<ResolvedRuntimeObservationTarget> resolved = [];
        HashSet<int> resolvedTokens = [];
        foreach (RuntimeObservationTargetSpec target in manifest.Targets)
        {
            if (!TryParseMethodDefinitionToken(target.MetadataToken, out int token))
            {
                reasons.Add($"Observation target {target.Id} has an invalid MethodDef token.");
                continue;
            }

            MethodBase? method;
            try
            {
                method = gameAssembly.ManifestModule.ResolveMethod(token);
            }
            catch (Exception exception) when (exception is ArgumentException or BadImageFormatException)
            {
                reasons.Add($"Observation target {target.Id} could not resolve its MethodDef token.");
                continue;
            }
            if (method is null || method.MetadataToken != token || method.Module.ModuleVersionId != expectedMvid)
            {
                reasons.Add($"Observation target {target.Id} resolved outside the reviewed method identity.");
                continue;
            }

            string declaringType = method.DeclaringType?.FullName ?? string.Empty;
            if (!string.Equals(declaringType, target.DeclaringType, StringComparison.Ordinal))
            {
                reasons.Add($"Observation target {target.Id} declaring type does not match the reviewed manifest.");
                continue;
            }
            string signature = AuditedMethodBindingResolver.FormatMethodSignature(method);
            if (!string.Equals(Normalize(signature), Normalize(target.MethodSignature), StringComparison.Ordinal))
            {
                reasons.Add($"Observation target {target.Id} method signature does not match the reviewed manifest.");
                continue;
            }
            if (method.IsAbstract || method.ContainsGenericParameters || method.IsConstructor)
            {
                reasons.Add($"Observation target {target.Id} resolves to an unsupported method shape.");
                continue;
            }
            if (!resolvedTokens.Add(token))
            {
                reasons.Add($"Observation target {target.Id} duplicates MethodDef token {target.MetadataToken}.");
                continue;
            }
            resolved.Add(new(target, method));
        }

        int requiredCount = manifest.Targets.Count(target => target.Required);
        int resolvedRequired = resolved.Count(target => target.Spec.Required);
        bool success = reasons.Count == 0 && resolved.Count == manifest.Targets.Count && resolvedRequired == requiredCount;
        if (!success)
            return new(false, [], reasons.Count > 0 ? reasons : ["Not all required observation targets resolved."]);
        return new(true, resolved, [$"Resolved {resolved.Count} exact-build read-only observation targets."]);
    }

    private static bool TryParseMethodDefinitionToken(string value, out int token)
    {
        token = 0;
        if (!RuntimeObservationGate.IsMethodDefinitionToken(value))
            return false;
        if (!uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed))
            return false;
        token = unchecked((int)parsed);
        return true;
    }

    private static string Normalize(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
