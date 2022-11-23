// <copyright file="CoverageReporter`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Ci.Coverage.Metadata;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Coverage Reporter by ModuleCoverageMetadata type
/// </summary>
/// <typeparam name="TMeta">Type of ModuleCoverageMetadata</typeparam>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class CoverageReporter<TMeta>
    where TMeta : ModuleCoverageMetadata, new()
{
    private static readonly TMeta Metadata;
    private static readonly Module Module;
    private static CoverageContextContainer? _currentCoverageContextContainer;
    private static ModuleValue? _cachedModuleValue;

    static CoverageReporter()
    {
        Metadata = new TMeta();
        Module = typeof(TMeta).Module;
        _currentCoverageContextContainer = CoverageReporter.Container;
        CoverageReporter.AddContextContainerChangeAction(ctx =>
        {
            Volatile.Write(ref _cachedModuleValue, null);
            Volatile.Write(ref _currentCoverageContextContainer, ctx);
        });
    }

    /// <summary>
    /// Gets the coverage scope for the method
    /// </summary>
    /// <param name="typeIndex">Type index</param>
    /// <param name="methodIndex">Method index</param>
    /// <param name="counters">Counters array for the method</param>
    /// <returns>True if the coverage is enabled and the scope is available; otherwise, false.</returns>
    public static bool TryGetScope(int typeIndex, int methodIndex, out int[]? counters)
    {
        var module = _cachedModuleValue;
        if (module is null)
        {
            var container = _currentCoverageContextContainer;
            if (container is null)
            {
                counters = default;
                return false;
            }

            module = container.GetModuleValue(Module);
            if (module is null)
            {
                module = new ModuleValue(Module, Metadata.GetTotalTypes());
                container.Add(module);
            }

            _cachedModuleValue = module;
        }

#if NET5_0_OR_GREATER
        // Avoid bound checks
        ref var type = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(module.Types), typeIndex);
#else
        ref var type = ref module.Types[typeIndex];
#endif
        if (type is null)
        {
            Metadata.GetTotalMethodsAndSequencePointsOfMethod(typeIndex, methodIndex, out var totalMethods, out var totalSequencePoints);

            type = new TypeValues(totalMethods);

            var typeMethod = new MethodValues(totalSequencePoints);
#if NET5_0_OR_GREATER
            // Avoid bound checks
            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(type.Methods), methodIndex) = typeMethod;
#else
            type.Methods[methodIndex] = typeMethod;
#endif

            counters = typeMethod.SequencePoints;
            return true;
        }

#if NET5_0_OR_GREATER
        // Avoid bound checks
        ref var method = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(type.Methods), methodIndex);
#else
        ref var method = ref type.Methods[methodIndex];
#endif
        method ??= new MethodValues(Metadata.GetTotalSequencePointsOfMethod(typeIndex, methodIndex));
        counters = method.SequencePoints;
        return true;
    }
}
