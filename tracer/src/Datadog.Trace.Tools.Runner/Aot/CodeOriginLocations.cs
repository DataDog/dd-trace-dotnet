// <copyright file="CodeOriginLocations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Pdb;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// The source locations of the endpoint methods of the application's assemblies, which Code Origin for spans reads from their
/// PDBs at runtime: a NativeAOT application has neither the PDBs nor metadata tokens, so its module initializer passes them
/// to <c>SpanCodeOrigin.AddBuildTimeLocations</c>, keyed by method (<c>SpanCodeOrigin.GetBuildTimeKey</c>).
/// </summary>
internal static class CodeOriginLocations
{
    /// <summary>
    /// Adds the locations of the endpoint methods of an assembly to <paramref name="entries"/>: assembly name, method key, file,
    /// line and column, for each. Datadog and known third-party assemblies are skipped, as Code Origin skips them at runtime.
    /// </summary>
    /// <returns>The number of locations added.</returns>
    public static int Collect(ModuleDef module, string path, List<string> entries)
    {
        var assemblyName = module.Assembly?.Name.String;
        return assemblyName is null || AssemblyFilter.IsDatadogOrThirdPartyAssembly(assemblyName) ? 0 : CollectEndpoints(module, path, assemblyName, entries);
    }

    /// <summary>
    /// Adds the locations of the endpoint methods of an assembly to <paramref name="entries"/>, whatever the assembly.
    /// </summary>
    /// <returns>The number of locations added.</returns>
    internal static int CollectEndpoints(ModuleDef module, string path, string assemblyName, List<string> entries)
    {
        using var reader = DatadogMetadataReader.CreatePdbReader(path, metadataOnly: true);
        if (reader is not { IsPdbExist: true })
        {
            return 0;
        }

        var count = 0;
        foreach (var (token, location) in SpanCodeOrigin.GetEndpointSequencePoints(reader, assemblyName).OrderBy(p => p.Key))
        {
            if (module.ResolveToken(token) is MethodDef method && GetKey(method) is { } key)
            {
                entries.AddRange([assemblyName, key, location.Url, location.Line, location.Column]);
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Gets the key <c>SpanCodeOrigin.GetBuildTimeKey</c> computes by reflection: declaring type, name and parameter type names.
    /// </summary>
    internal static string? GetKey(MethodDef method)
    {
        if (method.DeclaringType is not { } declaringType)
        {
            return null;
        }

        var parameters = method.MethodSig.Params.Select(p => TypeName(p, method));
        return $"{declaringType.ReflectionFullName}::{method.Name}({string.Join(",", parameters)})";
    }

    // Type.Name of a parameter type.
    private static string TypeName(TypeSig sig, MethodDef method)
        => sig switch
        {
            ByRefSig byRef => TypeName(byRef.Next, method) + "&",
            PtrSig pointer => TypeName(pointer.Next, method) + "*",
            SZArraySig array => TypeName(array.Next, method) + "[]",
            ArraySig array => TypeName(array.Next, method) + (array.Rank == 1 ? "[*]" : "[" + new string(',', (int)array.Rank - 1) + "]"),
            ModifierSig modifier => TypeName(modifier.Next, method),
            PinnedSig pinned => TypeName(pinned.Next, method),
            GenericInstSig generic => generic.GenericType.TypeDefOrRef.Name.String,
            GenericVar typeParameter => method.DeclaringType.GenericParameters.FirstOrDefault(p => p.Number == typeParameter.Number)?.Name.String ?? sig.TypeName,
            GenericMVar methodParameter => method.GenericParameters.FirstOrDefault(p => p.Number == methodParameter.Number)?.Name.String ?? sig.TypeName,
            TypeDefOrRefSig type => type.TypeDefOrRef.Name.String,
            _ => sig.TypeName,
        };
}
#endif
