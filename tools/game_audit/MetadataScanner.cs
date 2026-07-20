using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace SasukeIronclad.GameAudit;

internal static class MetadataScanner
{
    public static (GameBuildMetadata Build, List<SymbolCandidate> Symbols) Scan(
        string assemblyPath,
        string assemblyRelativePath,
        string dataDirectory,
        string assemblySha256,
        AuditQuerySet queries,
        int maxSymbols,
        string? steamBuildId,
        string? steamLastUpdated,
        string? baseLibVersion,
        string? baseLibManifestSha256)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new(stream, PEStreamOptions.LeaveOpen);
        if (!peReader.HasMetadata)
            throw new InvalidDataException("sts2.dll does not contain managed metadata.");

        MetadataReader reader = peReader.GetMetadataReader();
        AssemblyDefinition assembly = reader.GetAssemblyDefinition();
        ModuleDefinition module = reader.GetModuleDefinition();
        TypeNameProvider provider = new();

        GameBuildMetadata build = new()
        {
            DataDirectory = dataDirectory,
            Sts2AssemblyRelativePath = assemblyRelativePath,
            Sts2AssemblySha256 = assemblySha256,
            AssemblyName = reader.GetString(assembly.Name),
            AssemblyVersion = assembly.Version.ToString(),
            ModuleVersionId = reader.GetGuid(module.Mvid).ToString("D"),
            SteamBuildId = steamBuildId,
            SteamLastUpdated = steamLastUpdated,
            BaseLibVersion = baseLibVersion,
            BaseLibManifestSha256 = baseLibManifestSha256,
        };

        List<SymbolCandidate> candidates = [];
        foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
        {
            TypeDefinition type = reader.GetTypeDefinition(typeHandle);
            string typeName = provider.GetTypeFromDefinition(reader, typeHandle, 0);
            List<(string Category, int Score)> typeMatches = MatchType(typeName, queries);

            if (typeMatches.Count > 0)
            {
                candidates.Add(new SymbolCandidate
                {
                    Kind = "type",
                    Name = typeName,
                    DeclaringType = typeName,
                    Signature = FormatTypeSignature(reader, type, typeName, provider),
                    MetadataToken = FormatToken(MetadataTokens.GetToken(typeHandle)),
                    Visibility = FormatTypeVisibility(type.Attributes),
                    Score = typeMatches.Sum(item => item.Score) + 4,
                    Categories = typeMatches.Select(item => item.Category).Distinct(StringComparer.Ordinal).Order().ToList(),
                });
            }

            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = reader.GetMethodDefinition(methodHandle);
                string methodName = reader.GetString(method.Name);
                List<(string Category, int Score)> matches = MatchMethod(typeName, methodName, queries);
                if (matches.Count == 0)
                    continue;

                string signature;
                try
                {
                    MethodSignature<string> decoded = method.DecodeSignature(provider, genericContext: null);
                    string generic = decoded.GenericParameterCount > 0
                        ? $"<{string.Join(", ", Enumerable.Range(0, decoded.GenericParameterCount).Select(index => $"T{index}"))}>"
                        : string.Empty;
                    signature = $"{decoded.ReturnType} {typeName}.{methodName}{generic}({string.Join(", ", decoded.ParameterTypes)})";
                }
                catch (BadImageFormatException)
                {
                    signature = $"{typeName}.{methodName}(metadata signature unavailable)";
                }

                candidates.Add(new SymbolCandidate
                {
                    Kind = "method",
                    Name = methodName,
                    DeclaringType = typeName,
                    Signature = signature,
                    MetadataToken = FormatToken(MetadataTokens.GetToken(methodHandle)),
                    Visibility = FormatMethodVisibility(method.Attributes),
                    Score = matches.Sum(item => item.Score) + 8,
                    Categories = matches.Select(item => item.Category).Distinct(StringComparer.Ordinal).Order().ToList(),
                });
            }
        }

        List<SymbolCandidate> selected = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.DeclaringType, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .Take(maxSymbols)
            .ToList();

        return (build, selected);
    }

    private static List<(string Category, int Score)> MatchType(string typeName, AuditQuerySet queries)
    {
        List<(string Category, int Score)> matches = [];
        foreach (AuditQuery query in queries.Queries)
        {
            int hits = query.TypeContains.Count(keyword => Contains(typeName, keyword));
            if (hits > 0)
                matches.Add((query.Id, hits * 5));
        }
        return matches;
    }

    private static List<(string Category, int Score)> MatchMethod(
        string typeName,
        string methodName,
        AuditQuerySet queries)
    {
        List<(string Category, int Score)> matches = [];
        foreach (AuditQuery query in queries.Queries)
        {
            int typeHits = query.TypeContains.Count(keyword => Contains(typeName, keyword));
            int methodHits = query.MethodContains.Count(keyword => Contains(methodName, keyword));
            if (typeHits + methodHits > 0)
                matches.Add((query.Id, typeHits * 2 + methodHits * 6));
        }
        return matches;
    }

    private static bool Contains(string value, string keyword) =>
        !string.IsNullOrWhiteSpace(keyword) &&
        value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static string FormatTypeSignature(
        MetadataReader reader,
        TypeDefinition type,
        string typeName,
        TypeNameProvider provider)
    {
        string baseType = type.BaseType.IsNil
            ? string.Empty
            : type.BaseType.Kind switch
            {
                HandleKind.TypeDefinition => provider.GetTypeFromDefinition(reader, (TypeDefinitionHandle)type.BaseType, 0),
                HandleKind.TypeReference => provider.GetTypeFromReference(reader, (TypeReferenceHandle)type.BaseType, 0),
                HandleKind.TypeSpecification => provider.GetTypeFromSpecification(reader, null, (TypeSpecificationHandle)type.BaseType, 0),
                _ => string.Empty,
            };
        return string.IsNullOrEmpty(baseType) ? typeName : $"{typeName} : {baseType}";
    }

    private static string FormatToken(int token) => $"0x{token:X8}";

    private static string FormatMethodVisibility(MethodAttributes attributes) =>
        (attributes & MethodAttributes.MemberAccessMask).ToString();

    private static string FormatTypeVisibility(TypeAttributes attributes) =>
        (attributes & TypeAttributes.VisibilityMask).ToString();

    private sealed class TypeNameProvider : ISignatureTypeProvider<string, object?>
    {
        public string GetArrayType(string elementType, ArrayShape shape) =>
            $"{elementType}[{new string(',', Math.Max(0, shape.Rank - 1))}]";

        public string GetByReferenceType(string elementType) => $"{elementType}&";

        public string GetFunctionPointerType(MethodSignature<string> signature) =>
            $"fnptr({string.Join(", ", signature.ParameterTypes)}) -> {signature.ReturnType}";

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(", ", typeArguments)}>";

        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";

        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

        public string GetPinnedType(string elementType) => $"{elementType} pinned";

        public string GetPointerType(string elementType) => $"{elementType}*";

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Byte => "byte",
            PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.Double => "double",
            PrimitiveTypeCode.Int16 => "short",
            PrimitiveTypeCode.Int32 => "int",
            PrimitiveTypeCode.Int64 => "long",
            PrimitiveTypeCode.IntPtr => "nint",
            PrimitiveTypeCode.Object => "object",
            PrimitiveTypeCode.SByte => "sbyte",
            PrimitiveTypeCode.Single => "float",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.TypedReference => "typedref",
            PrimitiveTypeCode.UInt16 => "ushort",
            PrimitiveTypeCode.UInt32 => "uint",
            PrimitiveTypeCode.UInt64 => "ulong",
            PrimitiveTypeCode.UIntPtr => "nuint",
            PrimitiveTypeCode.Void => "void",
            _ => typeCode.ToString(),
        };

        public string GetSZArrayType(string elementType) => $"{elementType}[]";

        public string GetTypeFromDefinition(
            MetadataReader metadataReader,
            TypeDefinitionHandle handle,
            byte rawTypeKind)
        {
            TypeDefinition definition = metadataReader.GetTypeDefinition(handle);
            string name = metadataReader.GetString(definition.Name);
            TypeDefinitionHandle declaringType = definition.GetDeclaringType();
            if (!declaringType.IsNil)
                return $"{GetTypeFromDefinition(metadataReader, declaringType, rawTypeKind)}+{name}";
            string ns = metadataReader.GetString(definition.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        public string GetTypeFromReference(
            MetadataReader metadataReader,
            TypeReferenceHandle handle,
            byte rawTypeKind)
        {
            TypeReference reference = metadataReader.GetTypeReference(handle);
            string name = metadataReader.GetString(reference.Name);
            if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
                return $"{GetTypeFromReference(metadataReader, (TypeReferenceHandle)reference.ResolutionScope, rawTypeKind)}+{name}";
            string ns = metadataReader.GetString(reference.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        public string GetTypeFromSpecification(
            MetadataReader metadataReader,
            object? genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind) =>
            metadataReader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}
