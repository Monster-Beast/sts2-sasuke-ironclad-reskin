using System.Reflection;
using SasukeIronclad.SasukeIroncladCode.Visuals;

namespace SasukeIronclad.SasukeIroncladCode.Runtime;

public sealed record ResolvedAuditedMethod(
    string BindingId,
    string BindingKind,
    MethodBase Method
);

public sealed record AuditedMethodResolutionResult(
    bool Success,
    IReadOnlyList<ResolvedAuditedMethod> Methods,
    IReadOnlyList<string> Reasons
);

/// <summary>
/// Resolves reviewed MethodDef tokens against the exact loaded game module.
/// It performs no patching and returns no result when any enabled binding fails
/// token, declaring-type, signature or MVID validation.
/// </summary>
public static class AuditedMethodBindingResolver
{
    public static AuditedMethodResolutionResult Resolve(
        Assembly assembly,
        GameIntegrationProfile profile,
        GameIntegrationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(decision);

        List<string> reasons = [];
        if (!Guid.TryParse(profile.Fingerprint.ModuleMvid, out Guid expectedMvid) ||
            assembly.ManifestModule.ModuleVersionId != expectedMvid)
        {
            reasons.Add("The loaded game module MVID does not match the reviewed profile.");
            return new(false, [], reasons);
        }

        List<(string Id, string Kind, string DeclaringType, string Signature, string Token)> requested = [];
        if (decision.EnableVisualBindings)
        {
            requested.AddRange(profile.VisualBindings.Select(binding => (
                binding.Id,
                "visual",
                binding.DeclaringType,
                binding.MethodSignature,
                binding.MetadataToken
            )));
        }
        if (decision.EnableTitleBindings)
        {
            requested.AddRange(profile.TitleBindings.Select(binding => (
                binding.SurfaceId,
                "title",
                binding.DeclaringType,
                binding.MethodSignature,
                binding.MetadataToken
            )));
        }
        if (requested.Count == 0)
            return new(true, [], ["No audited methods were enabled by the integration decision."]);

        List<ResolvedAuditedMethod> resolved = [];
        foreach (var binding in requested)
        {
            if (!TryParseMethodDefinitionToken(binding.Token, out int token))
            {
                reasons.Add($"Binding {binding.Id} does not contain a valid MethodDef token.");
                continue;
            }

            MethodBase? method;
            try
            {
                method = assembly.ManifestModule.ResolveMethod(token);
            }
            catch (Exception exception) when (exception is ArgumentException or BadImageFormatException)
            {
                reasons.Add($"Binding {binding.Id} could not resolve its MethodDef token.");
                continue;
            }
            if (method is null)
            {
                reasons.Add($"Binding {binding.Id} resolved to no method.");
                continue;
            }
            if (method.MetadataToken != token || method.Module.ModuleVersionId != expectedMvid)
            {
                reasons.Add($"Binding {binding.Id} resolved outside the reviewed method identity.");
                continue;
            }

            string declaringType = method.DeclaringType?.FullName ?? string.Empty;
            if (!string.Equals(declaringType, binding.DeclaringType, StringComparison.Ordinal))
            {
                reasons.Add($"Binding {binding.Id} declaring type does not match the reviewed profile.");
                continue;
            }

            string actualSignature = FormatMethodSignature(method);
            if (!string.Equals(
                    NormalizeSignature(actualSignature),
                    NormalizeSignature(binding.Signature),
                    StringComparison.Ordinal))
            {
                reasons.Add($"Binding {binding.Id} method signature does not match the reviewed profile.");
                continue;
            }
            if (method.IsAbstract || method.ContainsGenericParameters)
            {
                reasons.Add($"Binding {binding.Id} resolves to an abstract or open generic method.");
                continue;
            }

            resolved.Add(new ResolvedAuditedMethod(binding.Id, binding.Kind, method));
        }

        if (reasons.Count > 0)
            return new(false, [], reasons);
        return new(true, resolved, [$"Resolved {resolved.Count} audited MethodDef bindings."]);
    }

    public static string FormatMethodSignature(MethodBase method)
    {
        ArgumentNullException.ThrowIfNull(method);
        string returnType = method is MethodInfo methodInfo
            ? FormatType(methodInfo.ReturnType)
            : "void";
        string declaringType = method.DeclaringType?.FullName ?? "<global>";
        string generic = method.IsGenericMethodDefinition
            ? $"<{string.Join(", ", Enumerable.Range(0, method.GetGenericArguments().Length).Select(index => $"T{index}"))}>"
            : string.Empty;
        string parameters = string.Join(", ", method.GetParameters().Select(parameter => FormatType(parameter.ParameterType)));
        return $"{returnType} {declaringType}.{method.Name}{generic}({parameters})";
    }

    private static bool TryParseMethodDefinitionToken(string value, out int token)
    {
        token = 0;
        if (value.Length != 10 || !value.StartsWith("0x06", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!uint.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out uint parsed) ||
            (parsed & 0xFF000000u) != 0x06000000u ||
            (parsed & 0x00FFFFFFu) == 0)
        {
            return false;
        }
        token = unchecked((int)parsed);
        return true;
    }

    private static string NormalizeSignature(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string FormatType(Type type)
    {
        if (type.IsByRef)
            return $"{FormatType(type.GetElementType()!)}&";
        if (type.IsPointer)
            return $"{FormatType(type.GetElementType()!)}*";
        if (type.IsArray)
        {
            int rank = type.GetArrayRank();
            return $"{FormatType(type.GetElementType()!)}[{new string(',', Math.Max(0, rank - 1))}]";
        }
        if (type.IsGenericParameter)
            return type.DeclaringMethod is null ? $"!{type.GenericParameterPosition}" : $"!!{type.GenericParameterPosition}";

        string? primitive = Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => "bool",
            TypeCode.Byte => "byte",
            TypeCode.Char => "char",
            TypeCode.Double => "double",
            TypeCode.Int16 => "short",
            TypeCode.Int32 => "int",
            TypeCode.Int64 => "long",
            TypeCode.SByte => "sbyte",
            TypeCode.Single => "float",
            TypeCode.String => "string",
            TypeCode.UInt16 => "ushort",
            TypeCode.UInt32 => "uint",
            TypeCode.UInt64 => "ulong",
            _ => null,
        };
        if (primitive is not null)
            return primitive;
        if (type == typeof(void))
            return "void";
        if (type == typeof(object))
            return "object";
        if (type == typeof(IntPtr))
            return "nint";
        if (type == typeof(UIntPtr))
            return "nuint";

        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            string name = definition.FullName ?? definition.Name;
            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(FormatType))}>";
        }
        return type.FullName ?? type.Name;
    }
}
