// <copyright file="CoverageReporter`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
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
    private static ModuleValue _globalModuleValue;

    static CoverageReporter()
    {
        Metadata = new TMeta();
        Module = typeof(TMeta).Module;

        // Caching the module from the global shared container in case an async container is null
        var globalCoverageContextContainer = CoverageReporter.GlobalContainer;
        var globalModuleValue = globalCoverageContextContainer.GetModuleValue(Module);
        if (globalModuleValue is null)
        {
            globalModuleValue = new ModuleValue(Metadata, Module, Metadata.GetNumberOfFiles());
            globalCoverageContextContainer.Add(globalModuleValue);
        }

        _globalModuleValue = globalModuleValue;
    }

    /// <summary>
    /// Gets the coverage counter for the file
    /// </summary>
    /// <param name="fileIndex">File index</param>
    /// <returns>Counters array for the file</returns>
    public static int[] GetFileCounter(int fileIndex)
    {
        ModuleValue? module;

        // Try to get the async context container
        if (CoverageReporter.Container is { } container)
        {
            // Get the module form the container
            module = container.GetModuleValue(Module);
            if (module is null)
            {
                // If the module is not found, we create a new one for this container
                module = new ModuleValue(Metadata, Module, Metadata.GetNumberOfFiles());
                container.Add(module);
            }
        }
        else
        {
            // If there's no async context container then we use the module from the global shared container.
            module = _globalModuleValue;
        }

        // Get the file value from the module and return the lines array for the method
        ref var fileValue = ref module.Files.FastGetReference(fileIndex);
        if (fileValue.Lines is { } lines)
        {
            return lines;
        }

        fileValue = new FileValue(Metadata.GetLastExecutableLineFromFile(fileIndex));
        return fileValue.Lines!;
    }
}
