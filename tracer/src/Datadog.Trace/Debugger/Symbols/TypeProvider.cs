// <copyright file="TypeProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Text;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;

namespace Datadog.Trace.Debugger.Symbols
{
    internal sealed class TypeProvider : ISignatureTypeProvider<string, int>
    {
        private readonly bool _includeResScope;

        internal TypeProvider(bool includeResScope)
        {
            _includeResScope = includeResScope;
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            return ParseTypeDefinition(reader, handle);
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            return ParseTypeReference(reader, handle, _includeResScope);
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return DecodePrimitiveType(typeCode);
        }

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            return $"{genericType}<{string.Join(", ", typeArguments)}>";
        }

        public string GetGenericMethodParameter(int genericContext, int index)
        {
            return $"!!{index}";
        }

        public string GetGenericTypeParameter(int genericContext, int index)
        {
            return $"!{index}";
        }

        public string GetTypeFromSpecification(MetadataReader reader, int genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }

        public string GetSZArrayType(string elementType)
        {
            return elementType + "[]";
        }

        public string GetArrayType(string elementType, ArrayShape shape)
        {
            var builder = new StringBuilder();

            builder.Append(elementType);
            builder.Append('[');

            for (int i = 0; i < shape.Rank; i++)
            {
                int lowerBound = 0;

                if (i < shape.LowerBounds.Length)
                {
                    lowerBound = shape.LowerBounds[i];
                    builder.Append(lowerBound);
                }

                builder.Append("...");

                if (i < shape.Sizes.Length)
                {
                    builder.Append(lowerBound + shape.Sizes[i] - 1);
                }

                if (i < shape.Rank - 1)
                {
                    builder.Append(',');
                }
            }

            builder.Append(']');
            return builder.ToString();
        }

        public string GetByReferenceType(string elementType)
        {
            return elementType + "&";
        }

        public string GetPointerType(string elementType)
        {
            return elementType + "*";
        }

        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            var parameterTypes = signature.ParameterTypes;

            var requiredParameterCount = signature.RequiredParameterCount;

            var builder = new StringBuilder();
            builder.Append("method ");
            builder.Append(signature.ReturnType);
            builder.Append(" *(");

            int i;
            for (i = 0; i < requiredParameterCount; i++)
            {
                builder.Append(parameterTypes[i]);
                if (i < parameterTypes.Length - 1)
                {
                    builder.Append(", ");
                }
            }

            if (i < parameterTypes.Length)
            {
                builder.Append("..., ");
                for (; i < parameterTypes.Length; i++)
                {
                    builder.Append(parameterTypes[i]);
                    if (i < parameterTypes.Length - 1)
                    {
                        builder.Append(", ");
                    }
                }
            }

            builder.Append(')');
            return builder.ToString();
        }

        public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired)
        {
            var mod = isRequired ? "modreq" : "modopt";
            return $"{unmodifiedType} {mod}({modifierType})";
        }

        public string GetPinnedType(string elementType)
        {
            return elementType + " pinned";
        }

        internal static string ParseTypeReference(MetadataReader reader, TypeReferenceHandle handle, bool includeResScope)
        {
            var reference = reader.GetTypeReference(handle);
            Handle scope = reference.ResolutionScope;

            var name = reference.Namespace.IsNil
                           ? reader.GetString(reference.Name)
                           : reader.GetString(reference.Namespace) + "." + reader.GetString(reference.Name);

            if (!includeResScope || reference.ResolutionScope.IsNil)
            {
                return name;
            }

            switch (scope.Kind)
            {
                case HandleKind.ModuleReference:
                    return "[.module  " + reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)scope).Name) + "]" + name;

                case HandleKind.AssemblyReference:
                    var assemblyReferenceHandle = (AssemblyReferenceHandle)scope;
                    var assemblyReference = reader.GetAssemblyReference(assemblyReferenceHandle);
                    return "[" + reader.GetString(assemblyReference.Name) + "]" + name;

                case HandleKind.TypeReference:
                    return ParseTypeReference(reader, (TypeReferenceHandle)scope, true) + "+" + name;

                default:
                    return name;
            }
        }

        internal static string ParseTypeDefinition(MetadataReader reader, TypeDefinitionHandle handle)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            var name = typeDef.Namespace.IsNil
                           ? reader.GetString(typeDef.Name)
                           : reader.GetString(typeDef.Namespace) + "." + reader.GetString(typeDef.Name);

            if (!typeDef.IsNested)
            {
                return name;
            }

            var enclosing = ParseTypeDefinition(reader, typeDef.GetDeclaringType());
            return $"{enclosing}+{name}";
        }

        internal static string DecodePrimitiveType(PrimitiveTypeCode typeCode)
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

        internal static PrimitiveTypeCode EncodePrimitiveType(string type)
        {
            return type switch
            {
                "System.Void" => PrimitiveTypeCode.Void,
                "System.Bool" => PrimitiveTypeCode.Boolean,
                "System.Char" => PrimitiveTypeCode.Char,
                "System.SByte" => PrimitiveTypeCode.SByte,
                "System.Byte" => PrimitiveTypeCode.Byte,
                "System.Int16" => PrimitiveTypeCode.Int16,
                "System.UInt16" => PrimitiveTypeCode.UInt16,
                "System.Int32" => PrimitiveTypeCode.Int32,
                "System.UInt32" => PrimitiveTypeCode.UInt32,
                "System.Int64" => PrimitiveTypeCode.Int64,
                "System.UInt64" => PrimitiveTypeCode.UInt64,
                "System.Single" => PrimitiveTypeCode.Single,
                "System.Double" => PrimitiveTypeCode.Double,
                "System.String" => PrimitiveTypeCode.String,
                // ReSharper disable once StringLiteralTypo
                "System.typedref" => PrimitiveTypeCode.TypedReference,
                "System.IntPtr" => PrimitiveTypeCode.IntPtr,
                "System.UIntPtr" => PrimitiveTypeCode.UIntPtr,
                "System.Object" => PrimitiveTypeCode.Object,
                _ => PrimitiveTypeCode.Object
            };
        }
    }
}
