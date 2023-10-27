// <copyright file="TypeProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;

namespace Datadog.Trace.Debugger.Symbols
{
    internal sealed class TypeProvider : ISignatureTypeProvider<TypeMock, int>
    {
        public TypeMock GetSZArrayType(TypeMock elementType)
        {
            return elementType;
        }

        public TypeMock GetArrayType(TypeMock elementType, ArrayShape shape)
        {
            return new TypeMock($"{elementType}:{shape.Rank}");
        }

        public TypeMock GetByReferenceType(TypeMock elementType)
        {
            return elementType;
        }

        public TypeMock GetGenericInstantiation(TypeMock genericType, ImmutableArray<TypeMock> typeArguments)
        {
            return new TypeMock($"{genericType}<{string.Join(", ", typeArguments)}>");
        }

        public TypeMock GetPointerType(TypeMock elementType)
        {
            return elementType;
        }

        public TypeMock GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return new TypeMock(GetPrimitiveTypeName(typeCode));
        }

        private string GetPrimitiveTypeName(PrimitiveTypeCode typeCode)
        {
            return typeCode switch
            {
                PrimitiveTypeCode.Void => "System.Void",
                PrimitiveTypeCode.Boolean => "System.Bool",
                PrimitiveTypeCode.Char => "System.Char",
                PrimitiveTypeCode.SByte => "System.SByte",
                PrimitiveTypeCode.Byte => "System.Byte",
                PrimitiveTypeCode.Int16 => "System.Int16",
                PrimitiveTypeCode.UInt16 => "System.UInt16",
                PrimitiveTypeCode.Int32 => "System.Int32",
                PrimitiveTypeCode.UInt32 => "System.UInt32",
                PrimitiveTypeCode.Int64 => "System.Int64",
                PrimitiveTypeCode.UInt64 => "System.UInt64",
                PrimitiveTypeCode.Single => "System.Single",
                PrimitiveTypeCode.Double => "System.Double",
                PrimitiveTypeCode.String => "System.String",
                // ReSharper disable once StringLiteralTypo
                PrimitiveTypeCode.TypedReference => "typedref",
                PrimitiveTypeCode.IntPtr => "System.IntPtr",
                PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
                PrimitiveTypeCode.Object => "System.Object",
                _ => "UNKNOWN"
            };
        }

        public TypeMock GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            return GetTypeFromDefinition(reader, handle);
        }

        public TypeMock GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            return ParseTypeReference(reader, handle);
        }

        public TypeMock GetFunctionPointerType(MethodSignature<TypeMock> signature)
        {
            throw new NotImplementedException();
        }

        public TypeMock GetGenericMethodParameter(int genericContext, int index)
        {
            return new TypeMock($"!!{index}");
        }

        public TypeMock GetGenericTypeParameter(int genericContext, int index)
        {
            return new TypeMock($"!{index}");
        }

        public TypeMock GetModifiedType(TypeMock modifier, TypeMock unmodifiedType, bool isRequired)
        {
            var modName = isRequired ? "modreq" : "modopt";
            return new TypeMock($"{unmodifiedType} {modName}({modifier})");
        }

        public TypeMock GetPinnedType(TypeMock elementType)
        {
            throw new NotImplementedException();
        }

        public TypeMock GetTypeFromSpecification(MetadataReader reader, int genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, 0);
        }

        private static TypeMock GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            var name = reader.GetString(typeDef.Name);
            var ns = typeDef.Namespace.IsNil ? null : reader.GetString(typeDef.Namespace);
            TypeMock enclosing = null;
            if (typeDef.IsNested)
            {
                enclosing = GetTypeFromDefinition(reader, typeDef.GetDeclaringType());
            }

            var typeName = ns != null ? $"{ns}.{name}" : name;

            if (enclosing != null)
            {
                typeName = $"{enclosing}/{name}";
            }

            return new TypeMock(typeName);
        }

        internal static TypeMock ParseTypeReference(MetadataReader reader, TypeReferenceHandle handle, bool includeResScope = false)
        {
            var typeRef = reader.GetTypeReference(handle);
            var name = reader.GetString(typeRef.Name);
            var nameSpace = typeRef.Namespace.IsNil ? null : reader.GetString(typeRef.Namespace);

            var resScope = !includeResScope ? null : GetResolutionScope(reader, typeRef, nameSpace, name);

            if (nameSpace == null)
            {
                return resScope == null ? new TypeMock($"{name}") : new TypeMock($"{resScope}{name}");
            }

            return resScope == null ? new TypeMock($"{nameSpace}.{name}") : new TypeMock($"{resScope}{nameSpace}.{name}");
        }

        private static TypeMock GetResolutionScope(MetadataReader reader, TypeReference typeRef, string nameSpace, string name)
        {
            TypeMock resScope;
            // See II.22.38 in ECMA-335
            if (typeRef.ResolutionScope.IsNil)
            {
               // exported/forwarded types
               return null;
            }

            switch (typeRef.ResolutionScope.Kind)
            {
                case HandleKind.AssemblyReference:
                    {
                        // Different assembly.
                        var assemblyRef = reader.GetAssemblyReference((AssemblyReferenceHandle)typeRef.ResolutionScope);
                        var assemblyName = reader.GetString(assemblyRef.Name);
                        resScope = new TypeMock(assemblyName);
                        break;
                    }

                case HandleKind.TypeReference:
                    {
                        // Nested type.
                        var enclosingType = ParseTypeReference(reader, (TypeReferenceHandle)typeRef.ResolutionScope);
                        resScope = new TypeMock(enclosingType.Name);
                        break;
                    }

                case HandleKind.ModuleReference:
                    {
                        // Same-assembly-different-module
                        return null;
                    }

                default:
                    // Edge cases not handled:
                    // https://github.com/dotnet/runtime/blob/b2e5a89085fcd87e2fa9300b4bb00cd499c5845b/src/libraries/System.Reflection.Metadata/tests/Metadata/Decoding/DisassemblingTypeProvider.cs#L130-L132
                    return null;
            }

            return resScope;
        }
    }
}
