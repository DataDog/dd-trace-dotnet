// <copyright file="CoverageReporter`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
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
    private static ModuleValue? _cachedModuleValue;

    static CoverageReporter()
    {
        Metadata = new TMeta();
        Module = typeof(TMeta).Module;
        CoverageReporter.Handler.AddContextContainerChangeAction(() => _cachedModuleValue = null);
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
            var container = CoverageReporter.Container;
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

        ref var type = ref module.Types[typeIndex];
        if (type is null)
        {
            Metadata.GetTotalMethodsAndSequencePointsOfMethod(typeIndex, methodIndex, out var totalMethods, out var totalSequencePoints);

            type = new TypeValues(totalMethods);

            var typeMethod = new MethodValues(totalSequencePoints);
            type.Methods[methodIndex] = typeMethod;

            counters = typeMethod.SequencePoints;
            return true;
        }

        ref var method = ref type.Methods[methodIndex];
        method ??= new MethodValues(Metadata.GetTotalSequencePointsOfMethod(typeIndex, methodIndex));
        counters = method.SequencePoints;
        return true;
    }
}
