// <copyright file="SymbolsExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata; // keep vendored versions for now because we access internal members
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

namespace Datadog.Trace.Debugger.Symbols;

internal static class SymbolsExtensions
{
    private static readonly EntityHandle _nilEntityHandle = default;

    internal static ref readonly EntityHandle NilEntityHandle => ref _nilEntityHandle;

    internal static bool IsHidden(this SymbolSequencePoint sq)
    {
        return sq is { Line: 0xFEEFEE, EndLine: 0xFEEFEE };
    }

    internal static string FullName(this TypeDefinitionHandle typeDefHandle, MetadataReader metadataReader)
    {
        return TypeProvider.ParseTypeDefinition(metadataReader, typeDefHandle);
    }

    internal static string FullName(this TypeReferenceHandle typeRef, MetadataReader metadataReader, bool includeResolutionScope)
    {
        return TypeProvider.ParseTypeReference(metadataReader, typeRef, includeResolutionScope);
    }

    internal static string FullName(this TypeSpecificationHandle typeSpecHandle, MetadataReader metadataReader)
    {
        var typeSpec = metadataReader.GetTypeSpecification(typeSpecHandle);
        return typeSpec.DecodeSignature(new TypeProvider(false), 0);
    }

    internal static string FullName(this EntityHandle handle, MetadataReader metadataReader)
    {
        if (handle.IsNil)
        {
            return string.Empty;
        }

        switch (handle)
        {
            case { Kind: HandleKind.TypeDefinition }:
                {
                    return ((TypeDefinitionHandle)handle).FullName(metadataReader);
                }

            case { Kind: HandleKind.TypeReference }:
                {
                    return ((TypeReferenceHandle)handle).FullName(metadataReader, false);
                }

            case { Kind: HandleKind.TypeSpecification }:
                {
                    return ((TypeSpecificationHandle)handle).FullName(metadataReader);
                }

            default:
                {
                    return string.Empty;
                }
        }
    }

    internal static bool IsInterfaceType(this TypeDefinition typeDefinition)
    {
        var typeAttributes = typeDefinition.Attributes;
        return (typeAttributes & TypeAttributes.Interface) == TypeAttributes.Interface;
    }

    internal static bool IsHiddenThis(this Parameter parameter)
    {
        return parameter.SequenceNumber == -2;
    }

    internal static bool IsStaticMethod(this MethodDefinition method)
    {
        return (method.Attributes & System.Reflection.MethodAttributes.Static) > 0;
    }

    internal static EntityHandle GetBaseTypeHandle(this EntityHandle typeHandle, MetadataReader metadataReader)
    {
        switch (typeHandle)
        {
            case { Kind: HandleKind.TypeDefinition }:
                {
                    var typeDef = metadataReader.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
                    return typeDef.BaseType;
                }

            case { Kind: HandleKind.TypeReference }:
                {
                    var typeRef = metadataReader.GetTypeReference((TypeReferenceHandle)typeHandle);
                    return typeRef.ResolutionScope.Kind == HandleKind.TypeReference ? typeRef.ResolutionScope : NilEntityHandle;
                }

            default:
                {
                    return NilEntityHandle;
                }
        }
    }
}
