// <copyright file="CoverageReporter`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    private static readonly AsyncLocal<Tuple<ModuleValue, CoverageContextContainer>?> ModuleContainer;

    static CoverageReporter()
    {
        Metadata = new TMeta();
        Module = typeof(TMeta).Module;
        ModuleContainer = new();
        CoverageContextContainer.AddAsyncLocal(ModuleContainer);
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
        ModuleValue module;
        if (ModuleContainer.Value is { } moduleContainer)
        {
            if (!moduleContainer.Item2.Enabled)
            {
                counters = default;
                return false;
            }

            module = moduleContainer.Item1;
        }
        else
        {
            if (CoverageReporter.Container is not { Enabled: true } container)
            {
                counters = default;
                return false;
            }

            module = new ModuleValue(Module, Metadata.GetTotalTypes());
            container.TryAdd(module);
            ModuleContainer.Value = new Tuple<ModuleValue, CoverageContextContainer>(module, container);
        }

        if (module.Types[typeIndex] is not { } type)
        {
            type = new TypeValues(Metadata.GetTotalMethodsOfType(typeIndex));
            module.Types[typeIndex] = type;
        }

        if (type.Methods[methodIndex] is not { } method)
        {
            method = new MethodValues(Metadata.GetTotalSequencePointsOfMethod(typeIndex, methodIndex));
            type.Methods[methodIndex] = method;
        }

        counters = method.SequencePoints;
        return true;
    }
}
