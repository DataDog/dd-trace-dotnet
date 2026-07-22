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
    private static ModuleValue? _globalModuleValue;

    static CoverageReporter()
    {
        try
        {
            Metadata = new TMeta();
            Module = typeof(TMeta).Module;
            ModuleMemorySize = CoverageMetadataValidator.ValidateAndGetRawByteLength(Metadata);
        }
        catch
        {
            CoverageReporter.Handler.MarkProbeDataIncomplete(GlobalCoverageFailureReason.ProbeDataIncomplete);
            throw;
        }
    }

    /// <summary>
    /// Gets the coverage counter for the file
    /// </summary>
    /// <param name="fileIndex">File index</param>
    /// <returns>Counters for the file</returns>
    public static unsafe void* GetFileCounter(int fileIndex)
    {
        var handler = CoverageReporter.Handler;
        ModuleValue? module = null;
        try
        {
            if (handler.Container is { } container &&
                container.TryGetOrAddModuleValue(
                    Metadata,
                    Module,
                    ModuleMemorySize,
                    handler.ModuleValueStrategy,
                    CoverageModuleValueOrigin.TestContext,
                    out module))
            {
            }
            else
            {
                module = GetOrCreateGlobalModuleValue(handler);
            }
        }
        catch
        {
            handler.MarkProbeDataIncomplete(GlobalCoverageFailureReason.ProbeDataIncomplete);
            throw;
        }

        if (module is null || module.FilesLines == IntPtr.Zero)
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

    private static ModuleValue GetOrCreateGlobalModuleValue(CoverageEventHandler handler)
    {
        if (Volatile.Read(ref _globalModuleValue) is { } cached)
        {
            return cached;
        }

        if (!handler.GlobalContainer.TryGetOrAddModuleValue(
                Metadata,
                Module,
                ModuleMemorySize,
                handler.ModuleValueStrategy,
                CoverageModuleValueOrigin.GlobalFallback,
                out var module) || module is null)
        {
            throw new InvalidOperationException("The global coverage context is unexpectedly closed.");
        }

        Interlocked.CompareExchange(ref _globalModuleValue, module, null);
        return Volatile.Read(ref _globalModuleValue)!;
    }
}
