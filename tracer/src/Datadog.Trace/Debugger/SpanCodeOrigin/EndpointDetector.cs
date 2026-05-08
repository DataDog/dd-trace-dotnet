// <copyright file="EndpointDetector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.Pdb;

#if NETCOREAPP
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
#else
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335;
#endif

namespace Datadog.Trace.Debugger.SpanCodeOrigin;

internal static class EndpointDetector
{
    private const TypeAttributes InvalidTypeAttributes = TypeAttributes.Interface | TypeAttributes.Abstract;
    private const MethodAttributes PublicMethodAttributes = MethodAttributes.Public;
    private const MethodAttributes InvalidMethodAttributes = MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
    private const string MvcNamespace = "Microsoft.AspNetCore.Mvc";
    private const string MvcRoutingNamespace = "Microsoft.AspNetCore.Mvc.Routing";
    private const string RazorPagesNamespace = "Microsoft.AspNetCore.Mvc.RazorPages";
    private const string SignalRNamespace = "Microsoft.AspNetCore.SignalR";
    private const string CompilerServicesNamespace = "System.Runtime.CompilerServices";

    private enum EndpointTypeKind
    {
        None,
        Controller,
        PageModel,
        SignalRHub,
        CompilerGenerated
    }

    private enum KnownNameSet
    {
        ControllerAttribute,
        ActionAttribute,
        NoHandlerAttribute,
        CompilerGeneratedAttribute
    }

    private enum KnownBaseTypeSet
    {
        Controller,
        PageModel,
        SignalRHub
    }

    internal interface IEndpointMethodTokenConsumer
    {
        void OnEndpointMethodToken(int token);
    }

    internal static void GetEndpointMethodTokens<TConsumer>(DatadogMetadataReader datadogMetadataReader, ref TConsumer consumer)
        where TConsumer : struct, IEndpointMethodTokenConsumer
    {
        if (datadogMetadataReader is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(datadogMetadataReader));
        }

        GetEndpointMethodTokens(datadogMetadataReader.MetadataReader, ref consumer);
    }

    internal static void GetEndpointMethodTokens<TConsumer>(MetadataReader metadataReader, ref TConsumer consumer)
        where TConsumer : struct, IEndpointMethodTokenConsumer
    {
        if (metadataReader is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(metadataReader));
        }

        foreach (var typeHandle in metadataReader.TypeDefinitions)
        {
            var typeDef = metadataReader.GetTypeDefinition(typeHandle);

            if (!IsValidTypeKind(typeDef))
            {
                continue;
            }

            var endpointType = ClassifyType(typeDef, metadataReader);
            if (endpointType == EndpointTypeKind.None)
            {
                continue;
            }

            foreach (var methodHandle in typeDef.GetMethods())
            {
                var methodDef = metadataReader.GetMethodDefinition(methodHandle);

                if (!IsValidMethod(methodDef))
                {
                    continue;
                }

                if (endpointType == EndpointTypeKind.Controller && HasAttributeFromSet(methodDef.GetCustomAttributes(), metadataReader, KnownNameSet.ActionAttribute))
                {
                    consumer.OnEndpointMethodToken(metadataReader.GetToken(methodHandle));
                    continue;
                }

                if (endpointType == EndpointTypeKind.PageModel && IsPageModelHandler(methodDef, metadataReader))
                {
                    consumer.OnEndpointMethodToken(metadataReader.GetToken(methodHandle));
                    continue;
                }

                if (endpointType == EndpointTypeKind.SignalRHub)
                {
                    consumer.OnEndpointMethodToken(metadataReader.GetToken(methodHandle));
                    continue;
                }

                // minimal API endpoints
                if (endpointType == EndpointTypeKind.CompilerGenerated && MightBeEndpoint(methodDef, metadataReader))
                {
                    consumer.OnEndpointMethodToken(metadataReader.GetToken(methodHandle));
                }
            }
        }
    }

    private static bool IsValidTypeKind(TypeDefinition typeDef)
    {
        return (typeDef.Attributes & InvalidTypeAttributes) == 0;
    }

    private static bool IsValidMethod(MethodDefinition methodDef)
    {
        var attributes = methodDef.Attributes;
        return (attributes & PublicMethodAttributes) != 0 &&
               (attributes & InvalidMethodAttributes) == 0;
    }

    private static EndpointTypeKind ClassifyType(TypeDefinition typeDef, MetadataReader reader)
    {
        var typeAttributes = typeDef.GetCustomAttributes();
        if (HasAttributeFromSet(typeAttributes, reader, KnownNameSet.ControllerAttribute))
        {
            return EndpointTypeKind.Controller;
        }

        var fallbackType = EndpointTypeKind.None;
        var isCompilerGenerated = HasAttributeFromSet(typeAttributes, reader, KnownNameSet.CompilerGeneratedAttribute);
        var baseTypeHandle = typeDef.BaseType;
        while (!baseTypeHandle.IsNil)
        {
            if (BaseTypeMatchesAny(baseTypeHandle, reader, KnownBaseTypeSet.Controller))
            {
                return EndpointTypeKind.Controller;
            }

            if (fallbackType == EndpointTypeKind.None)
            {
                if (BaseTypeMatchesAny(baseTypeHandle, reader, KnownBaseTypeSet.PageModel))
                {
                    fallbackType = EndpointTypeKind.PageModel;
                }
                else if (BaseTypeMatchesAny(baseTypeHandle, reader, KnownBaseTypeSet.SignalRHub))
                {
                    fallbackType = EndpointTypeKind.SignalRHub;
                }
            }

            if (baseTypeHandle.Kind != HandleKind.TypeDefinition)
            {
                break;
            }

            var baseType = reader.GetTypeDefinition((TypeDefinitionHandle)baseTypeHandle);
            if (HasAttributeFromSet(baseType.GetCustomAttributes(), reader, KnownNameSet.ControllerAttribute))
            {
                return EndpointTypeKind.Controller;
            }

            baseTypeHandle = baseType.BaseType;
        }

        if (fallbackType != EndpointTypeKind.None)
        {
            return fallbackType;
        }

        return isCompilerGenerated ? EndpointTypeKind.CompilerGenerated : EndpointTypeKind.None;
    }

    private static bool HasAttributeFromSet(CustomAttributeHandleCollection attributes, MetadataReader reader, KnownNameSet nameSet)
    {
        if (attributes.Count == 0)
        {
            return false;
        }

        foreach (var attributeHandle in attributes)
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            if (!TryGetAttributeTypeName(attribute, reader, out var namespaceHandle, out var nameHandle))
            {
                continue;
            }

            if (NameMatches(reader, namespaceHandle, nameHandle, nameSet))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetAttributeTypeName(CustomAttribute attribute, MetadataReader reader, out StringHandle namespaceHandle, out StringHandle nameHandle)
    {
        namespaceHandle = default;
        nameHandle = default;

        if (attribute.Constructor.IsNil)
        {
            return false;
        }

        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                {
                    var ctor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                    return TryGetTypeName(ctor.Parent, reader, out namespaceHandle, out nameHandle);
                }

            case HandleKind.MethodDefinition:
                {
                    var ctor = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                    var declaringType = ctor.GetDeclaringType();
                    if (declaringType.IsNil)
                    {
                        return false;
                    }

                    var typeDef = reader.GetTypeDefinition(declaringType);
                    namespaceHandle = typeDef.Namespace;
                    nameHandle = typeDef.Name;
                    return !nameHandle.IsNil;
                }

            default:
                return false;
        }
    }

    private static bool IsPageModelHandler(MethodDefinition methodDef, MetadataReader reader)
    {
        var name = methodDef.Name;
        var comparer = reader.StringComparer;

        // Razor Pages handler method conventions:
        // https://learn.microsoft.com/en-us/aspnet/core/razor-pages/?view=aspnetcore-8.0#handler-methods
        if (!comparer.StartsWith(name, "On"))
        {
            return false;
        }

        if (!IsKnownPageModelHandlerName(reader, name))
        {
            return false;
        }

        return !HasAttributeFromSet(methodDef.GetCustomAttributes(), reader, KnownNameSet.NoHandlerAttribute);
    }

    private static bool MightBeEndpoint(MethodDefinition methodDef, MetadataReader reader)
    {
        return reader.StringComparer.StartsWith(methodDef.Name, "<");
    }

    private static bool BaseTypeMatchesAny(EntityHandle typeHandle, MetadataReader reader, KnownBaseTypeSet baseTypeSet)
    {
        switch (typeHandle.Kind)
        {
            case HandleKind.TypeDefinition:
            case HandleKind.TypeReference:
                return TryGetTypeName(typeHandle, reader, out var namespaceHandle, out var nameHandle) &&
                       BaseTypeNameMatches(reader, namespaceHandle, nameHandle, baseTypeSet);

            case HandleKind.TypeSpecification:
                return TypeSpecMatchesByOuterName((TypeSpecificationHandle)typeHandle, reader, baseTypeSet);

            default:
                return false;
        }
    }

    private static bool TypeSpecMatchesByOuterName(TypeSpecificationHandle typeHandle, MetadataReader reader, KnownBaseTypeSet baseTypeSet)
    {
        var typeSpec = reader.GetTypeSpecification(typeHandle);
        var blobReader = reader.GetBlobReader(typeSpec.Signature);

        if (blobReader.ReadCompressedInteger() != (int)SignatureTypeCode.GenericTypeInstance)
        {
            return false;
        }

        var rawTypeKind = blobReader.ReadCompressedInteger();
        if (rawTypeKind != (int)SignatureTypeKind.Class &&
            rawTypeKind != (int)SignatureTypeKind.ValueType)
        {
            return false;
        }

        var outerTypeHandle = blobReader.ReadTypeHandle();
        return TryGetTypeName(outerTypeHandle, reader, out var namespaceHandle, out var nameHandle) &&
               BaseTypeNameMatches(reader, namespaceHandle, nameHandle, baseTypeSet);
    }

    private static bool TryGetTypeName(EntityHandle typeHandle, MetadataReader reader, out StringHandle namespaceHandle, out StringHandle nameHandle)
    {
        namespaceHandle = default;
        nameHandle = default;

        switch (typeHandle.Kind)
        {
            case HandleKind.TypeDefinition:
                var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
                namespaceHandle = typeDef.Namespace;
                nameHandle = typeDef.Name;
                return !nameHandle.IsNil;

            case HandleKind.TypeReference:
                var typeRef = reader.GetTypeReference((TypeReferenceHandle)typeHandle);
                namespaceHandle = typeRef.Namespace;
                nameHandle = typeRef.Name;
                return !nameHandle.IsNil;

            default:
                return false;
        }
    }

    private static bool IsKnownPageModelHandlerName(MetadataReader reader, StringHandle name)
    {
        var comparer = reader.StringComparer;
        return comparer.Equals(name, "OnGet") ||
               comparer.Equals(name, "OnGetAsync") ||
               comparer.Equals(name, "OnPost") ||
               comparer.Equals(name, "OnPostAsync") ||
               comparer.Equals(name, "OnPut") ||
               comparer.Equals(name, "OnPutAsync") ||
               comparer.Equals(name, "OnDelete") ||
               comparer.Equals(name, "OnDeleteAsync") ||
               comparer.Equals(name, "OnHead") ||
               comparer.Equals(name, "OnHeadAsync") ||
               comparer.Equals(name, "OnPatch") ||
               comparer.Equals(name, "OnPatchAsync") ||
               comparer.Equals(name, "OnOptions") ||
               comparer.Equals(name, "OnOptionsAsync");
    }

    private static bool NameMatches(MetadataReader reader, StringHandle namespaceHandle, StringHandle nameHandle, KnownNameSet nameSet)
    {
        var comparer = reader.StringComparer;
        switch (nameSet)
        {
            case KnownNameSet.ControllerAttribute:
                return comparer.Equals(namespaceHandle, MvcNamespace) &&
                       (comparer.Equals(nameHandle, "ApiControllerAttribute") ||
                        comparer.Equals(nameHandle, "ControllerAttribute") ||
                        comparer.Equals(nameHandle, "RouteAttribute"));

            case KnownNameSet.ActionAttribute:
                return (comparer.Equals(namespaceHandle, MvcNamespace) &&
                        (comparer.Equals(nameHandle, "HttpGetAttribute") ||
                         comparer.Equals(nameHandle, "HttpPostAttribute") ||
                         comparer.Equals(nameHandle, "HttpPutAttribute") ||
                         comparer.Equals(nameHandle, "HttpDeleteAttribute") ||
                         comparer.Equals(nameHandle, "HttpPatchAttribute") ||
                         comparer.Equals(nameHandle, "HttpHeadAttribute") ||
                         comparer.Equals(nameHandle, "HttpOptionsAttribute"))) ||
                       (comparer.Equals(namespaceHandle, MvcRoutingNamespace) &&
                        comparer.Equals(nameHandle, "HttpMethodAttribute"));

            case KnownNameSet.NoHandlerAttribute:
                return comparer.Equals(namespaceHandle, RazorPagesNamespace) &&
                       comparer.Equals(nameHandle, "NonHandlerAttribute");

            case KnownNameSet.CompilerGeneratedAttribute:
                return comparer.Equals(namespaceHandle, CompilerServicesNamespace) &&
                       comparer.Equals(nameHandle, "CompilerGeneratedAttribute");

            default:
                return false;
        }
    }

    private static bool BaseTypeNameMatches(MetadataReader reader, StringHandle namespaceHandle, StringHandle nameHandle, KnownBaseTypeSet baseTypeSet)
    {
        var comparer = reader.StringComparer;
        switch (baseTypeSet)
        {
            case KnownBaseTypeSet.Controller:
                return comparer.Equals(namespaceHandle, MvcNamespace) &&
                       (comparer.Equals(nameHandle, "Controller") ||
                        comparer.Equals(nameHandle, "ControllerBase"));

            case KnownBaseTypeSet.PageModel:
                return comparer.Equals(namespaceHandle, RazorPagesNamespace) &&
                       comparer.Equals(nameHandle, "PageModel");

            case KnownBaseTypeSet.SignalRHub:
                return comparer.Equals(namespaceHandle, SignalRNamespace) &&
                       (comparer.Equals(nameHandle, "Hub") ||
                        comparer.Equals(nameHandle, "Hub`1"));

            default:
                return false;
        }
    }
}
