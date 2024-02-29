// <copyright file="CoverageReporter`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.Ci.Coverage.Metadata;
using Datadog.Trace.Util;

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
    private static readonly int ModuleMemorySize;
    private static ModuleValue _globalModuleValue;

    static CoverageReporter()
    {
        Metadata = new TMeta();
        Module = typeof(TMeta).Module;
        ModuleMemorySize = Metadata.CoverageMode == 0 ? Metadata.TotalLines * sizeof(byte) : Metadata.TotalLines * sizeof(int);

        // Caching the module from the global shared container in case an async container is null
        var globalCoverageContextContainer = CoverageReporter.GlobalContainer;
        var globalModuleValue = globalCoverageContextContainer.GetModuleValue(Module);
        if (globalModuleValue is null)
        {
            globalModuleValue = new ModuleValue(Metadata, Module, ModuleMemorySize);
            globalCoverageContextContainer.Add(globalModuleValue);
        }

        _globalModuleValue = globalModuleValue;
    }

    /// <summary>
    /// Gets the coverage counter for the file
    /// </summary>
    /// <param name="fileIndex">File index</param>
    /// <returns>Counters for the file</returns>
    public static unsafe void* GetFileCounter(int fileIndex)
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
                module = new ModuleValue(Metadata, Module, ModuleMemorySize);
                container.Add(module);
            }
        }
        else
        {
            // If there's no async context container then we use the module from the global shared container.
            module = _globalModuleValue;
        }

        if (module.FilesLines == IntPtr.Zero)
        {
            ThrowHelper.ThrowNullReferenceException("Counter memory was disposed.");
        }

        // Gets the file counter by using the file offset over the global module memory segment
        if (Metadata.CoverageMode == 0)
        {
            return ((byte*)module.FilesLines) + Metadata.GetOffset(fileIndex);
        }

        return ((int*)module.FilesLines) + Metadata.GetOffset(fileIndex);
    }
}
