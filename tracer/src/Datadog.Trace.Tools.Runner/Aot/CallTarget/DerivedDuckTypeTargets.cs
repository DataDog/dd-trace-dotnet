// <copyright file="DerivedDuckTypeTargets.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// The classes of the application that derive from the target class of a <c>[DuckType(IncludeDerivedTypes = true)]</c>
/// declaration and that dynamic duck typing binds differently. The proxy of the target class serves the derived classes at
/// runtime, bound to the members of the target class, while dynamic duck typing binds the members of each class: a class that
/// declares a member with a name the proxy binds (a new member, a member hiding the target's with <c>new</c>, a field with the
/// same name) gets a proxy of its own. The others, overrides included (the proxy calls them through virtual dispatch), behave
/// the same with the proxy of the target class.
/// </summary>
internal static class DerivedDuckTypeTargets
{
    private const string DuckTypingNamespace = "Datadog.Trace.DuckTyping";

    /// <summary>
    /// Finds the classes deriving from the target classes of the derived-types mappings that need a proxy of their own.
    /// </summary>
    /// <param name="mappings">The declared mappings.</param>
    /// <param name="modules">The modules of the application.</param>
    /// <param name="findType">Finds a type definition by assembly name and reflection name.</param>
    /// <param name="resolveType">Resolves a type reference across the loaded modules.</param>
    /// <returns>The mappings and the derived classes.</returns>
    public static List<(DuckTypeAotMapping Mapping, TypeSig Target)> Find(
        IEnumerable<DuckTypeAotMapping> mappings,
        IEnumerable<ModuleDef> modules,
        Func<string, string, TypeDef?> findType,
        Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        var declarations = new List<(DuckTypeAotMapping Mapping, TypeDef Target, HashSet<string> Names)>();
        foreach (var mapping in mappings)
        {
            // An interface target: its implementations all declare the members, which the proxy calls through the interface.
            if (!mapping.IncludesDerivedTypes ||
                mapping.Mode != DuckTypeAotMappingMode.Forward ||
                DuckTypeAotRegistryAssemblyEmitter.IsGenericProxyMapping(mapping) ||
                findType(mapping.TargetAssemblyName, mapping.TargetTypeName) is not { IsInterface: false, IsSealed: false, IsValueType: false, HasGenericParameters: false } target ||
                findType(mapping.ProxyAssemblyName, mapping.ProxyTypeName) is not { } proxy)
            {
                continue;
            }

            var names = BoundNames(proxy, resolveType);
            if (names.Count > 0)
            {
                declarations.Add((mapping, target, names));
            }
        }

        var result = new List<(DuckTypeAotMapping Mapping, TypeSig Target)>();
        if (declarations.Count == 0)
        {
            return result;
        }

        foreach (var module in modules)
        {
            foreach (var type in module.GetTypes())
            {
                // The classes that can have instances, and that a registry can name.
                if (type.IsInterface || type.IsAbstract || type.IsValueType || type.HasGenericParameters || type.IsGlobalModuleType)
                {
                    continue;
                }

                foreach (var (mapping, target, names) in declarations)
                {
                    if (type != target && Shadows(type, target, names, resolveType))
                    {
                        result.Add((mapping, type.ToTypeSig()));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets whether a type derives from the target class, with a class between them (the type included) that declares a member
    /// the proxy binds.
    /// </summary>
    private static bool Shadows(TypeDef type, TypeDef target, HashSet<string> names, Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        var declares = false;
        var depth = 0;
        for (var current = type; current is not null && depth < 64; current = current.BaseType is { } baseType ? resolveType(baseType) : null, depth++)
        {
            if (current == target)
            {
                return declares;
            }

            declares |= DeclaresBoundMember(current, names);
        }

        return false;
    }

    private static bool DeclaresBoundMember(TypeDef type, HashSet<string> names)
    {
        // An override is called through virtual dispatch by the proxy of the target class too.
        static bool IsOverride(MethodDef? method) => method is { IsVirtual: true, IsNewSlot: false };

        return type.Fields.Any(field => names.Contains(field.Name)) ||
               type.Properties.Any(property => names.Contains(property.Name) && !IsOverride(property.GetMethod ?? property.SetMethod)) ||
               type.Methods.Any(method => !method.IsSpecialName && names.Contains(method.Name) && !IsOverride(method));
    }

    /// <summary>
    /// Gets the names of the target members a proxy definition binds: its members' (and those of the interfaces it extends), or
    /// the names their duck typing attributes give (with the fallback names).
    /// </summary>
    private static HashSet<string> BoundNames(TypeDef proxy, Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<TypeDef>();
        var seen = new HashSet<TypeDef>();
        pending.Push(proxy);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current) || current.FullName == $"{DuckTypingNamespace}.IDuckType")
            {
                continue;
            }

            foreach (var property in current.Properties)
            {
                AddNames(property, property.Name);
            }

            foreach (var field in current.Fields.Where(field => !field.IsLiteral))
            {
                AddNames(field, field.Name);
            }

            foreach (var method in current.Methods.Where(method => !method.IsSpecialName && !method.IsConstructor))
            {
                AddNames(method, method.Name);
            }

            foreach (var implemented in current.Interfaces)
            {
                if (resolveType(implemented.Interface.ScopeType ?? implemented.Interface) is { } interfaceDefinition)
                {
                    pending.Push(interfaceDefinition);
                }
            }
        }

        return names;

        void AddNames(IHasCustomAttribute member, string name)
        {
            var attributes = member.CustomAttributes.Where(attribute => attribute.TypeFullName.StartsWith(DuckTypingNamespace + ".", StringComparison.Ordinal)).ToList();
            if (attributes.Any(attribute => attribute.TypeFullName == $"{DuckTypingNamespace}.DuckIgnoreAttribute"))
            {
                return;
            }

            var declared = attributes.Select(attribute => attribute.GetProperty("Name")?.Argument.Value as UTF8String)
                                     .FirstOrDefault(value => value is not null);
            if (declared is null)
            {
                names.Add(name);
                return;
            }

            foreach (var fallback in declared.String.Split(','))
            {
                names.Add(fallback.Trim());
            }
        }
    }
}
#endif
