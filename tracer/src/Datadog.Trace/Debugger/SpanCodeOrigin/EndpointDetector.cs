// <copyright file="EndpointDetector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Pdb;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335;

namespace Datadog.Trace.Debugger.SpanCodeOrigin;

internal static class EndpointDetector
{
    private static readonly HashSet<string> ControllerAttributes =
    [
        "Microsoft.AspNetCore.Mvc.ApiControllerAttribute",
        "Microsoft.AspNetCore.Mvc.ControllerAttribute",
        "Microsoft.AspNetCore.Mvc.RouteAttribute"
    ];

    private static readonly HashSet<string> ControllerBaseNames =
    [
        "Microsoft.AspNetCore.Mvc.Controller",
        "Microsoft.AspNetCore.Mvc.ControllerBase"
    ];

    private static readonly HashSet<string> ActionAttributes =
    [
        "Microsoft.AspNetCore.Mvc.HttpGetAttribute",
        "Microsoft.AspNetCore.Mvc.HttpPostAttribute",
        "Microsoft.AspNetCore.Mvc.HttpPutAttribute",
        "Microsoft.AspNetCore.Mvc.HttpDeleteAttribute",
        "Microsoft.AspNetCore.Mvc.HttpPatchAttribute",
        "Microsoft.AspNetCore.Mvc.HttpHeadAttribute",
        "Microsoft.AspNetCore.Mvc.HttpOptionsAttribute",
        "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute"
    ];

    private static readonly HashSet<string> SignalRHubBaseNames =
    [
        "Microsoft.AspNetCore.SignalR.Hub",
        "Microsoft.AspNetCore.SignalR.Hub`1"
    ];

    private static readonly HashSet<string> PageModelBaseNames = ["Microsoft.AspNetCore.Mvc.RazorPages.PageModel"];

    private static readonly HashSet<string> NoHandlerAttributes = ["Microsoft.AspNetCore.Mvc.RazorPages.NonHandlerAttribute"];

    internal static ImmutableHashSet<int> GetEndpointMethodTokens(DatadogMetadataReader datadogMetadataReader)
    {
        if (datadogMetadataReader is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(datadogMetadataReader));
        }

        var builder = ImmutableHashSet.CreateBuilder<int>();
        var metadataReader = datadogMetadataReader.MetadataReader;
        foreach (var typeHandle in metadataReader.TypeDefinitions)
        {
            var typeDef = metadataReader.GetTypeDefinition(typeHandle);

            if (!IsValidTypeKind(typeDef))
            {
                continue;
            }

            bool isPageModel = false;
            bool isSignalRHub = false;
            bool isCompilerGeneratedType = false;
            var isController = IsInheritFromTypesOrHasAttribute(typeDef, metadataReader, ControllerAttributes, ControllerBaseNames);
            if (!isController)
            {
                isPageModel = IsInheritFromTypes(typeDef, metadataReader, PageModelBaseNames);
                if (!isPageModel)
                {
                    isSignalRHub = IsInheritFromTypes(typeDef, metadataReader, SignalRHubBaseNames);
                    if (!isSignalRHub)
                    {
                        isCompilerGeneratedType = datadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(MetadataTokens.GetToken(typeHandle));
                        if (!isCompilerGeneratedType)
                        {
                            continue;
                        }
                    }
                }
            }

            foreach (var methodHandle in typeDef.GetMethods())
            {
                var methodDef = metadataReader.GetMethodDefinition(methodHandle);

                if (!IsValidMethod(methodDef))
                {
                    continue;
                }

                if (isController && HasAttributeFromSet(methodDef.GetCustomAttributes(), metadataReader, ActionAttributes))
                {
                    builder.Add(metadataReader.GetToken(methodHandle));
                    continue;
                }

                if (isPageModel && IsPageModelHandler(methodDef, metadataReader))
                {
                    builder.Add(metadataReader.GetToken(methodHandle));
                    continue;
                }

                if (isSignalRHub)
                {
                    builder.Add(metadataReader.GetToken(methodHandle));
                    continue;
                }

                // minimal API endpoints
                if (isCompilerGeneratedType && MightBeEndpoint(methodDef, metadataReader))
                {
                    builder.Add(metadataReader.GetToken(methodHandle));
                }
            }
        }

        return builder.ToImmutable();
    }

    private static bool IsValidTypeKind(TypeDefinition typeDef)
    {
        var attributes = typeDef.Attributes;
        return (attributes & TypeAttributes.Interface) == 0 &&
               (attributes & TypeAttributes.Abstract) == 0;
    }

    private static bool IsValidMethod(MethodDefinition methodDef)
    {
        var attributes = methodDef.Attributes;
        return (attributes & MethodAttributes.Public) != 0 &&
               (attributes & MethodAttributes.Static) == 0;
    }

    private static bool IsInheritFromTypesOrHasAttribute(TypeDefinition typeDef, MetadataReader reader, HashSet<string> attributesNames, HashSet<string> baseTypeNames)
    {
        if (HasAttributeFromSet(typeDef.GetCustomAttributes(), reader, attributesNames))
        {
            return true;
        }

        var baseTypeHandle = typeDef.BaseType;
        while (!baseTypeHandle.IsNil)
        {
            if (TryGetBaseTypeInfo(baseTypeHandle, reader, out var baseTypeName, out var nextBaseTypeHandle))
            {
                if (baseTypeNames.Contains(baseTypeName))
                {
                    return true;
                }
            }

            if (baseTypeHandle.Kind != HandleKind.TypeDefinition)
            {
                break;
            }

            var baseType = reader.GetTypeDefinition((TypeDefinitionHandle)baseTypeHandle);
            if (HasAttributeFromSet(baseType.GetCustomAttributes(), reader, attributesNames))
            {
                return true;
            }

            baseTypeHandle = nextBaseTypeHandle;
        }

        return false;
    }

    private static bool HasAttributeFromSet(CustomAttributeHandleCollection attributes, MetadataReader reader, HashSet<string> attributeNames)
    {
        foreach (var attributeHandle in attributes)
        {
            string fullName;
            var attribute = reader.GetCustomAttribute(attributeHandle);
            switch (attribute.Constructor.Kind)
            {
                case HandleKind.MemberReference:
                    {
                        var ctor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                        switch (ctor.Parent.Kind)
                        {
                            case HandleKind.TypeReference:
                                var tr = reader.GetTypeReference((TypeReferenceHandle)ctor.Parent);
                                fullName = GetFullTypeName(tr.Namespace, tr.Name, reader);
                                break;

                            case HandleKind.TypeDefinition:
                                var td = reader.GetTypeDefinition((TypeDefinitionHandle)ctor.Parent);
                                fullName = GetFullTypeName(td.Namespace, td.Name, reader);
                                break;

                            default:
                                continue;
                        }

                        break;
                    }

                case HandleKind.MethodDefinition:
                    {
                        var ctor = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                        var td = reader.GetTypeDefinition(ctor.GetDeclaringType());
                        fullName = GetFullTypeName(td.Namespace, td.Name, reader);
                        break;
                    }

                default:
                    continue;
            }

            if (attributeNames.Contains(fullName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInheritFromTypes(TypeDefinition typeDef, MetadataReader reader, HashSet<string> baseTypeNames)
    {
        var baseTypeHandle = typeDef.BaseType;
        while (!baseTypeHandle.IsNil)
        {
            if (TryGetBaseTypeInfo(baseTypeHandle, reader, out var baseTypeName, out var nextBaseTypeHandle))
            {
                var indexOfTypeArg = baseTypeName.IndexOf('<');
                if (indexOfTypeArg > 0)
                {
                    baseTypeName = baseTypeName.Substring(0, indexOfTypeArg);
                }

                if (baseTypeNames.Contains(baseTypeName))
                {
                    return true;
                }
            }

            if (baseTypeHandle.Kind != HandleKind.TypeDefinition)
            {
                break;
            }

            baseTypeHandle = nextBaseTypeHandle;
        }

        return false;
    }

    private static bool IsPageModelHandler(MethodDefinition methodDef, MetadataReader reader)
    {
        // First check if the method has [NonHandler] attribute
        if (HasAttributeFromSet(methodDef.GetCustomAttributes(), reader, NoHandlerAttributes))
        {
            return false;
        }

        var name = reader.GetString(methodDef.Name);
        // https://learn.microsoft.com/en-us/dotnet/api/system.web.mvc.httpverbs
        return name.StartsWith("On", StringComparison.Ordinal) &&
               (name.Equals("OnGet", StringComparison.Ordinal) ||
                name.Equals("OnGetAsync", StringComparison.Ordinal) ||
                name.Equals("OnPost", StringComparison.Ordinal) ||
                name.Equals("OnPostAsync", StringComparison.Ordinal) ||
                name.Equals("OnPut", StringComparison.Ordinal) ||
                name.Equals("OnPutAsync", StringComparison.Ordinal) ||
                name.Equals("OnDelete", StringComparison.Ordinal) ||
                name.Equals("OnDeleteAsync", StringComparison.Ordinal) ||
                name.Equals("OnHead", StringComparison.Ordinal) ||
                name.Equals("OnHeadAsync", StringComparison.Ordinal) ||
                name.Equals("OnPatch", StringComparison.Ordinal) ||
                name.Equals("OnPatchAsync", StringComparison.Ordinal) ||
                name.Equals("OnOptions", StringComparison.Ordinal) ||
                name.Equals("OnOptionsAsync", StringComparison.Ordinal));
    }

    private static bool MightBeEndpoint(MethodDefinition methodDef, MetadataReader reader)
    {
        var name = reader.GetString(methodDef.Name);
        if (name.StartsWith("<", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetBaseTypeInfo(EntityHandle typeHandle, MetadataReader reader, [NotNullWhen(true)] out string? baseTypeName, out EntityHandle baseType)
    {
        switch (typeHandle.Kind)
        {
            case HandleKind.TypeDefinition:
                var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
                baseTypeName = GetFullTypeName(typeDef.Namespace, typeDef.Name, reader);
                baseType = typeDef.BaseType;
                break;

            case HandleKind.TypeReference:
                var typeRef = reader.GetTypeReference((TypeReferenceHandle)typeHandle);
                baseTypeName = GetFullTypeName(typeRef.Namespace, typeRef.Name, reader);
                baseType = default;
                break;

            case HandleKind.TypeSpecification:
                baseTypeName = ((TypeSpecificationHandle)typeHandle).FullName(reader);
                baseType = default;
                break;

            default:
                baseTypeName = null;
                baseType = default;
                break;
        }

        return !string.IsNullOrEmpty(baseTypeName);
    }

    private static string GetFullTypeName(StringHandle namespaceHandle, StringHandle nameHandle, MetadataReader reader)
    {
        var nameSpace = reader.GetString(namespaceHandle);
        var name = reader.GetString(nameHandle);
        return $"{nameSpace}.{name}";
    }

    private sealed record Entity(string Name, EntityHandle Handle);
}
