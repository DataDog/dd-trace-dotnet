// <copyright file="AsyncStateMachineAttributeTypeProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Runtime.CompilerServices;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;

namespace Datadog.Trace.Debugger.Symbols;

internal sealed class AsyncStateMachineAttributeTypeProvider : ICustomAttributeTypeProvider<string>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return TypeProvider.DecodePrimitiveType(typeCode);
    }

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        return TypeProvider.ParseTypeDefinition(reader, handle);
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        return TypeProvider.ParseTypeReference(reader, handle, false);
    }

    public string GetSZArrayType(string elementType)
    {
        return elementType + "[]";
    }

    public string GetSystemType()
    {
        return typeof(AsyncStateMachineAttribute).FullName ?? "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
    }

    public bool IsSystemType(string type)
    {
        return type.StartsWith("System.");
    }

    public string GetTypeFromSerializedName(string name)
    {
        return name;
    }

    public PrimitiveTypeCode GetUnderlyingEnumType(string type)
    {
        return TypeProvider.EncodePrimitiveType(type);
    }
}
