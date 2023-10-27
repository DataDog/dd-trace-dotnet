// <copyright file="SymbolsExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

namespace Datadog.Trace.Debugger.Symbols;

internal static class SymbolsExtensions
{
    public static bool IsHidden(this SymbolSequencePoint sq)
    {
        return sq is { Line: 0xFEEFEE, EndLine: 0xFEEFEE };
    }

    public static string FullName(this TypeDefinition typeDef, MetadataReader metadataReader)
    {
        string @namespace = string.Empty;
        string name = string.Empty;
        if (!typeDef.Namespace.IsNil)
        {
            @namespace = metadataReader.GetString(typeDef.Namespace);
        }

        if (!typeDef.Name.IsNil)
        {
            name = metadataReader.GetString(typeDef.Name);
        }

        return $"{@namespace}.{name}";
    }

    public static string FullName(this TypeReference typeRef, MetadataReader metadataReader)
    {
        string @namespace = string.Empty;
        string name = string.Empty;
        if (!typeRef.Namespace.IsNil)
        {
            @namespace = metadataReader.GetString(typeRef.Namespace);
        }

        if (!typeRef.Name.IsNil)
        {
            name = metadataReader.GetString(typeRef.Name);
        }

        return $"{@namespace}.{name}";
    }

    public static string FullName(this TypeSpecification typeSpec)
    {
        var specSig = typeSpec.DecodeSignature(new TypeProvider(), 0);
        return specSig.Name;
    }
}
