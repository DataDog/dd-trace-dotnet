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
                PrimitiveTypeCode.Void => "void",
                PrimitiveTypeCode.Boolean => "bool",
                PrimitiveTypeCode.Char => "char",
                PrimitiveTypeCode.SByte => "int8",
                PrimitiveTypeCode.Byte => "unsigned int8",
                PrimitiveTypeCode.Int16 => "int16",
                PrimitiveTypeCode.UInt16 => "unsigned int16",
                PrimitiveTypeCode.Int32 => "int32",
                PrimitiveTypeCode.UInt32 => "unsigned int32",
                PrimitiveTypeCode.Int64 => "int64",
                PrimitiveTypeCode.UInt64 => "unsigned int64",
                PrimitiveTypeCode.Single => "float32",
                PrimitiveTypeCode.Double => "float64",
                PrimitiveTypeCode.String => "string",
                // ReSharper disable once StringLiteralTypo
                PrimitiveTypeCode.TypedReference => "typedref",
                PrimitiveTypeCode.IntPtr => "native int",
                PrimitiveTypeCode.UIntPtr => "unsigned native int",
                PrimitiveTypeCode.Object => "object",
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

        internal static TypeMock ParseTypeReference(MetadataReader reader, TypeReferenceHandle handle)
        {
            var typeRef = reader.GetTypeReference(handle);
            var name = reader.GetString(typeRef.Name);
            var nameSpace = typeRef.Namespace.IsNil ? null : reader.GetString(typeRef.Namespace);
            TypeMock resScope;

            // See II.22.38 in ECMA-335
            if (typeRef.ResolutionScope.IsNil)
            {
                throw new Exception(
                    $"Null resolution scope on type Name: {nameSpace}.{name}. This indicates exported/forwarded types");
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
                        throw new Exception(
                            $"Cross-module reference to type {nameSpace}.{name}. ");
                    }

                default:
                    // Edge cases not handled:
                    // https://github.com/dotnet/runtime/blob/b2e5a89085fcd87e2fa9300b4bb00cd499c5845b/src/libraries/System.Reflection.Metadata/tests/Metadata/Decoding/DisassemblingTypeProvider.cs#L130-L132
                    throw new Exception(
                        $"TypeRef to {typeRef.ResolutionScope.Kind} for type {nameSpace}.{name}");
            }

            if (nameSpace == null)
            {
                return new TypeMock($"{resScope}{name}");
            }

            return new TypeMock($"{resScope}{nameSpace}.{name}");
        }
    }
}
