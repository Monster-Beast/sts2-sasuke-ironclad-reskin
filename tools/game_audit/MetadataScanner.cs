using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

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
            AddTypeCandidate(reader, provider, typeHandle, type, typeName, queries, candidates);
            AddMethodCandidates(reader, provider, type, typeName, queries, candidates);
            AddFieldCandidates(reader, provider, type, typeName, queries, candidates);
            AddPropertyCandidates(reader, provider, type, typeName, queries, candidates);
            AddEventCandidates(reader, provider, type, typeName, queries, candidates);
        }

        List<SymbolCandidate> selected = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.DeclaringType, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.MetadataToken, StringComparer.Ordinal)
            .Take(maxSymbols)
            .ToList();
        return (build, selected);
    }

    private static void AddTypeCandidate(
        MetadataReader reader,
        TypeNameProvider provider,
        TypeDefinitionHandle handle,
        TypeDefinition type,
        string typeName,
        AuditQuerySet queries,
        List<SymbolCandidate> candidates)
    {
        List<(string Category, int Score)> matches = MatchType(typeName, queries);
        if (matches.Count == 0)
            return;
        candidates.Add(new SymbolCandidate
        {
            Kind = "type",
            Name = typeName,
            DeclaringType = typeName,
            Signature = FormatTypeSignature(reader, type, typeName, provider),
            MetadataToken = FormatToken(MetadataTokens.GetToken(handle)),
            Visibility = FormatTypeVisibility(type.Attributes),
            Score = matches.Sum(item => item.Score) + 4,
            Categories = Categories(matches),
        });
    }

    private static void AddMethodCandidates(
        MetadataReader reader,
        TypeNameProvider provider,
        TypeDefinition type,
        string typeName,
        AuditQuerySet queries,
        List<SymbolCandidate> candidates)
    {
        foreach (MethodDefinitionHandle handle in type.GetMethods())
        {
            MethodDefinition method = reader.GetMethodDefinition(handle);
            string name = reader.GetString(method.Name);
            List<(string Category, int Score)> matches = MatchMember(typeName, name, queries);
            if (matches.Count == 0)
                continue;

            string signature;
            try
            {
                MethodSignature<string> decoded = method.DecodeSignature(provider, genericContext: null);
                string generic = decoded.GenericParameterCount > 0
                    ? $"<{string.Join(", ", Enumerable.Range(0, decoded.GenericParameterCount).Select(index => $"T{index}"))}>"
                    : string.Empty;
                signature = $"{decoded.ReturnType} {typeName}.{name}{generic}({string.Join(", ", decoded.ParameterTypes)})";
            }
            catch (BadImageFormatException)
            {
                signature = $"{typeName}.{name}(metadata signature unavailable)";
            }

            candidates.Add(new SymbolCandidate
            {
                Kind = "method",
                Name = name,
                DeclaringType = typeName,
                Signature = signature,
                MetadataToken = FormatToken(MetadataTokens.GetToken(handle)),
                Visibility = FormatMethodVisibility(method.Attributes),
                Score = matches.Sum(item => item.Score) + 8,
                Categories = Categories(matches),
            });
        }
    }

    private static void AddFieldCandidates(
        MetadataReader reader,
        TypeNameProvider provider,
        TypeDefinition type,
        string typeName,
        AuditQuerySet queries,
        List<SymbolCandidate> candidates)
    {
        foreach (FieldDefinitionHandle handle in type.GetFields())
        {
            FieldDefinition field = reader.GetFieldDefinition(handle);
            string name = reader.GetString(field.Name);
            List<(string Category, int Score)> matches = MatchMember(typeName, name, queries);
            if (matches.Count == 0)
                continue;

            string fieldType;
            try
            {
                fieldType = field.DecodeSignature(provider, genericContext: null);
            }
            catch (BadImageFormatException)
            {
                fieldType = "metadata-type-unavailable";
            }
            string? constantValue = ReadConstantValue(reader, field.GetDefaultValue());
            string signature = $"{fieldType} {typeName}.{name}";
            if (constantValue is not null)
                signature += $" = {FormatLiteral(constantValue)}";

            candidates.Add(new SymbolCandidate
            {
                Kind = "field",
                Name = name,
                DeclaringType = typeName,
                Signature = signature,
                MetadataToken = FormatToken(MetadataTokens.GetToken(handle)),
                Visibility = FormatFieldVisibility(field.Attributes),
                ConstantValue = constantValue,
                Score = matches.Sum(item => item.Score) + (constantValue is null ? 5 : 12),
                Categories = Categories(matches),
            });
        }
    }

    private static void AddPropertyCandidates(
        MetadataReader reader,
        TypeNameProvider provider,
        TypeDefinition type,
        string typeName,
        AuditQuerySet queries,
        List<SymbolCandidate> candidates)
    {
        foreach (PropertyDefinitionHandle handle in type.GetProperties())
        {
            PropertyDefinition property = reader.GetPropertyDefinition(handle);
            string name = reader.GetString(property.Name);
            List<(string Category, int Score)> matches = MatchMember(typeName, name, queries);
            if (matches.Count == 0)
                continue;

            string signature;
            try
            {
                MethodSignature<string> decoded = property.DecodeSignature(provider, genericContext: null);
                string parameters = decoded.ParameterTypes.Length == 0
                    ? string.Empty
                    : $"[{string.Join(", ", decoded.ParameterTypes)}]";
                signature = $"{decoded.ReturnType} {typeName}.{name}{parameters}";
            }
            catch (BadImageFormatException)
            {
                signature = $"{typeName}.{name}(metadata signature unavailable)";
            }

            candidates.Add(new SymbolCandidate
            {
                Kind = "property",
                Name = name,
                DeclaringType = typeName,
                Signature = signature,
                MetadataToken = FormatToken(MetadataTokens.GetToken(handle)),
                Visibility = "property",
                Score = matches.Sum(item => item.Score) + 6,
                Categories = Categories(matches),
            });
        }
    }

    private static void AddEventCandidates(
        MetadataReader reader,
        TypeNameProvider provider,
        TypeDefinition type,
        string typeName,
        AuditQuerySet queries,
        List<SymbolCandidate> candidates)
    {
        foreach (EventDefinitionHandle handle in type.GetEvents())
        {
            EventDefinition eventDefinition = reader.GetEventDefinition(handle);
            string name = reader.GetString(eventDefinition.Name);
            List<(string Category, int Score)> matches = MatchMember(typeName, name, queries);
            if (matches.Count == 0)
                continue;
            string eventType = FormatEntityType(reader, provider, eventDefinition.Type);
            candidates.Add(new SymbolCandidate
            {
                Kind = "event",
                Name = name,
                DeclaringType = typeName,
                Signature = $"event {eventType} {typeName}.{name}",
                MetadataToken = FormatToken(MetadataTokens.GetToken(handle)),
                Visibility = "event",
                Score = matches.Sum(item => item.Score) + 7,
                Categories = Categories(matches),
            });
        }
    }

    private static List<string> Categories(IEnumerable<(string Category, int Score)> matches) =>
        matches.Select(item => item.Category).Distinct(StringComparer.Ordinal).Order().ToList();

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

    private static List<(string Category, int Score)> MatchMember(string typeName, string memberName, AuditQuerySet queries)
    {
        List<(string Category, int Score)> matches = [];
        foreach (AuditQuery query in queries.Queries)
        {
            int typeHits = query.TypeContains.Count(keyword => Contains(typeName, keyword));
            IEnumerable<string> memberTerms = query.MethodContains.Concat(query.MemberContains);
            int memberHits = memberTerms.Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(keyword => Contains(memberName, keyword));
            if (typeHits + memberHits > 0)
                matches.Add((query.Id, typeHits * 2 + memberHits * 6));
        }
        return matches;
    }

    private static bool Contains(string value, string keyword) =>
        !string.IsNullOrWhiteSpace(keyword) && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static string FormatTypeSignature(MetadataReader reader, TypeDefinition type, string typeName, TypeNameProvider provider)
    {
        string baseType = type.BaseType.IsNil ? string.Empty : FormatEntityType(reader, provider, type.BaseType);
        return string.IsNullOrEmpty(baseType) ? typeName : $"{typeName} : {baseType}";
    }

    private static string FormatEntityType(MetadataReader reader, TypeNameProvider provider, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeDefinition => provider.GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0),
        HandleKind.TypeReference => provider.GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0),
        HandleKind.TypeSpecification => provider.GetTypeFromSpecification(reader, null, (TypeSpecificationHandle)handle, 0),
        _ => handle.Kind.ToString(),
    };

    private static string? ReadConstantValue(MetadataReader reader, ConstantHandle handle)
    {
        if (handle.IsNil)
            return null;
        Constant constant = reader.GetConstant(handle);
        byte[] bytes = reader.GetBlobBytes(constant.Value);
        return constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => bytes.Length >= 1 ? (bytes[0] != 0).ToString() : null,
            ConstantTypeCode.Char => bytes.Length >= 2 ? ((char)BinaryPrimitives.ReadUInt16LittleEndian(bytes)).ToString() : null,
            ConstantTypeCode.SByte => bytes.Length >= 1 ? unchecked((sbyte)bytes[0]).ToString() : null,
            ConstantTypeCode.Byte => bytes.Length >= 1 ? bytes[0].ToString() : null,
            ConstantTypeCode.Int16 => bytes.Length >= 2 ? BinaryPrimitives.ReadInt16LittleEndian(bytes).ToString() : null,
            ConstantTypeCode.UInt16 => bytes.Length >= 2 ? BinaryPrimitives.ReadUInt16LittleEndian(bytes).ToString() : null,
            ConstantTypeCode.Int32 => bytes.Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(bytes).ToString() : null,
            ConstantTypeCode.UInt32 => bytes.Length >= 4 ? BinaryPrimitives.ReadUInt32LittleEndian(bytes).ToString() : null,
            ConstantTypeCode.Int64 => bytes.Length >= 8 ? BinaryPrimitives.ReadInt64LittleEndian(bytes).ToString() : null,
            ConstantTypeCode.UInt64 => bytes.Length >= 8 ? BinaryPrimitives.ReadUInt64LittleEndian(bytes).ToString() : null,
            ConstantTypeCode.Single => bytes.Length >= 4 ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes)).ToString("R") : null,
            ConstantTypeCode.Double => bytes.Length >= 8 ? BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes)).ToString("R") : null,
            ConstantTypeCode.String => SanitizeConstant(Encoding.Unicode.GetString(bytes).TrimEnd('\0')),
            ConstantTypeCode.NullReference => "null",
            _ => null,
        };
    }

    private static string SanitizeConstant(string value)
    {
        string sanitized = new(value.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
        sanitized = sanitized.Trim();
        return sanitized.Length <= 160 ? sanitized : sanitized[..157] + "...";
    }

    private static string FormatLiteral(string value) => value == "null" ? value : $"\"{value.Replace("\"", "'")}\"";
    private static string FormatToken(int token) => $"0x{token:X8}";
    private static string FormatMethodVisibility(MethodAttributes attributes) => (attributes & MethodAttributes.MemberAccessMask).ToString();
    private static string FormatFieldVisibility(FieldAttributes attributes) => (attributes & FieldAttributes.FieldAccessMask).ToString();
    private static string FormatTypeVisibility(TypeAttributes attributes) => (attributes & TypeAttributes.VisibilityMask).ToString();

    private sealed class TypeNameProvider : ISignatureTypeProvider<string, object?>
    {
        public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', Math.Max(0, shape.Rank - 1))}]";
        public string GetByReferenceType(string elementType) => $"{elementType}&";
        public string GetFunctionPointerType(MethodSignature<string> signature) => $"fnptr({string.Join(", ", signature.ParameterTypes)}) -> {signature.ReturnType}";
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => $"{genericType}<{string.Join(", ", typeArguments)}>";
        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetPinnedType(string elementType) => $"{elementType} pinned";
        public string GetPointerType(string elementType) => $"{elementType}*";
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Boolean => "bool", PrimitiveTypeCode.Byte => "byte", PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.Double => "double", PrimitiveTypeCode.Int16 => "short", PrimitiveTypeCode.Int32 => "int",
            PrimitiveTypeCode.Int64 => "long", PrimitiveTypeCode.IntPtr => "nint", PrimitiveTypeCode.Object => "object",
            PrimitiveTypeCode.SByte => "sbyte", PrimitiveTypeCode.Single => "float", PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.TypedReference => "typedref", PrimitiveTypeCode.UInt16 => "ushort", PrimitiveTypeCode.UInt32 => "uint",
            PrimitiveTypeCode.UInt64 => "ulong", PrimitiveTypeCode.UIntPtr => "nuint", PrimitiveTypeCode.Void => "void",
            _ => typeCode.ToString(),
        };
        public string GetSZArrayType(string elementType) => $"{elementType}[]";
        public string GetTypeFromDefinition(MetadataReader metadataReader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            TypeDefinition definition = metadataReader.GetTypeDefinition(handle);
            string name = metadataReader.GetString(definition.Name);
            TypeDefinitionHandle declaringType = definition.GetDeclaringType();
            if (!declaringType.IsNil)
                return $"{GetTypeFromDefinition(metadataReader, declaringType, rawTypeKind)}+{name}";
            string ns = metadataReader.GetString(definition.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }
        public string GetTypeFromReference(MetadataReader metadataReader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            TypeReference reference = metadataReader.GetTypeReference(handle);
            string name = metadataReader.GetString(reference.Name);
            if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
                return $"{GetTypeFromReference(metadataReader, (TypeReferenceHandle)reference.ResolutionScope, rawTypeKind)}+{name}";
            string ns = metadataReader.GetString(reference.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }
        public string GetTypeFromSpecification(MetadataReader metadataReader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            metadataReader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}
