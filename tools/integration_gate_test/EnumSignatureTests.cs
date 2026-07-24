using System.Reflection;
using System.Runtime.CompilerServices;
using SasukeIronclad.SasukeIroncladCode.Runtime;

namespace SasukeIronclad.IntegrationGateTest;

internal static class EnumSignatureTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        MethodInfo method = typeof(EnumSignatureTests).GetMethod(
            nameof(EnumTarget),
            BindingFlags.Static | BindingFlags.NonPublic
        ) ?? throw new InvalidOperationException("Enum signature fixture was not found.");

        string enumName = typeof(SignatureFixtureEnum).FullName
            ?? throw new InvalidOperationException("Enum signature fixture lacks a full name.");
        string expected =
            $"void {typeof(EnumSignatureTests).FullName}.{nameof(EnumTarget)}(" +
            $"{enumName}, System.Collections.Generic.List`1<{enumName}>)";
        string actual = AuditedMethodBindingResolver.FormatMethodSignature(method);

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Enum identity was not preserved. expected={expected}; actual={actual}"
            );
        }
        if (actual.Contains("(int,", StringComparison.Ordinal) ||
            actual.Contains("List`1<int>", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Enum parameters collapsed to their integer backing type.");
        }
    }

    private static void EnumTarget(SignatureFixtureEnum direct, List<SignatureFixtureEnum> nested)
    {
        _ = direct;
        _ = nested;
    }

    private enum SignatureFixtureEnum
    {
        Unknown = 0,
        Reviewed = 1,
    }
}
