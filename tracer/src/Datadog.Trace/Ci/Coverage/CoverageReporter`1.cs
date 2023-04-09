// <copyright file="CoverageReporter`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci.Coverage.Metadata;
using Datadog.Trace.Ci.Coverage.Util;

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
    private static ModuleValue? _currentModuleValue;
    private static ModuleValue _globalModuleValue;

    static CoverageReporter()
    {
        Metadata = new TMeta();
        Module = typeof(TMeta).Module;
        _currentCoverageContextContainer = CoverageReporter.Container;
        CoverageReporter.AddContextContainerChangeAction(ctx =>
        {
            Volatile.Write(ref _currentModuleValue, null);
            Volatile.Write(ref _currentCoverageContextContainer, ctx);
        });

        var globalCoverageContextContainer = CoverageReporter.GlobalContainer;
        var globalModuleValue = globalCoverageContextContainer.GetModuleValue(Module);
        if (globalModuleValue is null)
        {
            globalModuleValue = new ModuleValue(Metadata, Module, Metadata.GetMethodsCount());
            globalCoverageContextContainer.Add(globalModuleValue);
        }

        _globalModuleValue = globalModuleValue;
    }

    /// <summary>
    /// Gets the coverage counters for the method
    /// </summary>
    /// <param name="methodIndex">Method index</param>
    /// <returns>Counters array for the method</returns>
    public static int[] GetCounters(int methodIndex)
    {
        var module = _currentModuleValue;
        if (module is null)
        {
            var container = _currentCoverageContextContainer;
            if (container is null)
            {
                module = _globalModuleValue;
            }
            else
            {
                module = container.GetModuleValue(Module);
                if (module is null)
                {
                    module = new ModuleValue(Metadata, Module, Metadata.GetMethodsCount());
                    container.Add(module);
                }

                _currentModuleValue = module;
            }
        }

        ref var method = ref module.Methods.FastGetReference(methodIndex);
        method ??= new MethodValues(Metadata.GetTotalSequencePointsOfMethod(methodIndex));
        return method.SequencePoints;
    }
}
