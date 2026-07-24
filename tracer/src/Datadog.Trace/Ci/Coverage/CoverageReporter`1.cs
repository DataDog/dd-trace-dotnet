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
        try
        {
            var module = GetModuleValue(handler);
            if (module is null)
            {
                ThrowHelper.ThrowInvalidOperationException("The global coverage buffer is unexpectedly closed.");
            }

            return GetPointer(module, fileIndex);
        }
        catch
        {
            handler.MarkProbeDataIncomplete(GlobalCoverageFailureReason.ProbeDataIncomplete);
            throw;
        }
    }

    private static ModuleValue? GetModuleValue(CoverageEventHandler handler)
    {
        if (handler.Container is { } container &&
            container.TryGetOrAddModuleValue(
                Metadata,
                Module,
                ModuleMemorySize,
                out var module) &&
            module is not null)
        {
            return module;
        }

        return TryGetOrCreateGlobalModuleValue(handler);
    }

    private static ModuleValue? TryGetOrCreateGlobalModuleValue(CoverageEventHandler handler)
    {
        if (Volatile.Read(ref _globalModuleValue) is { } cached)
        {
            return cached;
        }

        if (!handler.GlobalContainer.TryGetOrAddModuleValue(
                Metadata,
                Module,
                ModuleMemorySize,
                out var module) || module is null)
        {
            return null;
        }

        Interlocked.CompareExchange(ref _globalModuleValue, module, null);
        return Volatile.Read(ref _globalModuleValue)!;
    }

    private static int GetByteOffset(int fileIndex)
        => Metadata.CoverageMode == 0 ? Metadata.GetOffset(fileIndex) : Metadata.GetOffset(fileIndex) * sizeof(int);

    private static unsafe void* GetPointer(ModuleValue module, int fileIndex)
    {
        var filesLines = module.FilesLines;
        if (filesLines == IntPtr.Zero)
        {
            ThrowHelper.ThrowNullReferenceException("Counter memory was disposed.");
        }

        return (byte*)filesLines + GetByteOffset(fileIndex);
    }
}
