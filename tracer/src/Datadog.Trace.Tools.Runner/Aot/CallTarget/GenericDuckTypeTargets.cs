// <copyright file="GenericDuckTypeTargets.cs" company="Datadog">
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
/// The instantiations over value types of the open generic targets of the declared duck typing mappings (e.g. Kafka's
/// <c>Producer&lt;int, string&gt;</c>). The generic proxy of such a mapping only serves reference type arguments: NativeAOT has
/// no code for the other instantiations unless it compiled them. The registry gives the ones the application creates proxies
/// of their own, like for a recorded mapping; the search of generic instantiations finds them from the code that calls the
/// constructors of the targets.
/// </summary>
internal static class GenericDuckTypeTargets
{
    /// <summary>
    /// Finds the open generic target types of the forward mappings that have a generic proxy.
    /// </summary>
    /// <param name="mappings">The declared mappings.</param>
    /// <param name="findType">Finds a type definition by assembly name and reflection name.</param>
    /// <returns>The mappings of each target type.</returns>
    public static Dictionary<TypeDef, List<DuckTypeAotMapping>> Find(IEnumerable<DuckTypeAotMapping> mappings, Func<string, string, TypeDef?> findType)
    {
        var targets = new Dictionary<TypeDef, List<DuckTypeAotMapping>>();
        foreach (var mapping in mappings)
        {
            if (!DuckTypeAotRegistryAssemblyEmitter.IsGenericProxyMapping(mapping) ||
                findType(mapping.TargetAssemblyName, mapping.TargetTypeName) is not { HasGenericParameters: true } target)
            {
                continue;
            }

            if (!targets.TryGetValue(target, out var targetMappings))
            {
                targets[target] = targetMappings = new List<DuckTypeAotMapping>();
            }

            targetMappings.Add(mapping);
        }

        return targets;
    }

    /// <summary>
    /// Gets the methods the search of generic instantiations starts from: the constructors of the targets, which create their
    /// instances (also those of the classes deriving from them, through the base constructor).
    /// </summary>
    /// <param name="targets">The target types.</param>
    /// <returns>The constructors.</returns>
    public static IEnumerable<MethodDef> Seeds(IEnumerable<TypeDef> targets)
        => targets.SelectMany(target => target.Methods.Where(method => method.IsInstanceConstructor));

    /// <summary>
    /// Gets the closed target types over value types the search found, with the mappings to register for each.
    /// </summary>
    /// <param name="targets">The mappings of each target type.</param>
    /// <param name="instantiations">The closed instantiations the search found, by method.</param>
    /// <returns>The mappings and the closed target types.</returns>
    public static List<(DuckTypeAotMapping Mapping, TypeSig Target)> ValueTypeInstantiations(
        IReadOnlyDictionary<TypeDef, List<DuckTypeAotMapping>> targets,
        IReadOnlyDictionary<MethodDef, List<GenericInstantiationDiscovery.Instantiation>> instantiations)
    {
        var result = new List<(DuckTypeAotMapping Mapping, TypeSig Target)>();
        foreach (var (target, mappings) in targets)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var constructor in Seeds([target]))
            {
                if (!instantiations.TryGetValue(constructor, out var found))
                {
                    continue;
                }

                foreach (var instantiation in found)
                {
                    // The generic proxy serves the instantiations over reference types.
                    var arguments = instantiation.TypeArguments;
                    if (arguments.Count != target.GenericParameters.Count ||
                        !arguments.Any(argument => argument.IsValueType) ||
                        target.ToTypeSig() is not ClassOrValueTypeSig definition)
                    {
                        continue;
                    }

                    var closed = new GenericInstSig(definition, arguments);
                    if (seen.Add(closed.FullName))
                    {
                        result.AddRange(mappings.Select(mapping => (mapping, (TypeSig)closed)));
                    }
                }
            }
        }

        return result;
    }
}
#endif
