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
    private static IntPtr _discardedCounters;
    private static ModuleValue? _globalModuleValue;

    static CoverageReporter()
    {
        try
        {
            Metadata = new TMeta();
            Module = typeof(TMeta).Module;
            ModuleMemorySize = CoverageMetadataValidator.ValidateAndGetRawByteLength(Metadata);

            // Allocated up front when possible because the probe failures it absorbs include failing to allocate a
            // counters buffer. A failure here must not fail the type initializer, so it is retried on first use.
            _discardedCounters = TryAllocateDiscardedCounters();
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
            // Probes run inside customer code, including finalizers and type initializers, where an exception would
            // crash the process or break the type. Coverage is already marked incomplete, so let the probe write into
            // counters that are never reported instead.
            handler.MarkProbeDataIncomplete(GlobalCoverageFailureReason.ProbeDataIncomplete);
            var discardedCounters = GetDiscardedCounters();
            if (discardedCounters == IntPtr.Zero)
            {
                throw;
            }

            return (byte*)discardedCounters + GetByteOffset(fileIndex);
        }
    }

    private static IntPtr GetDiscardedCounters()
    {
        var discardedCounters = Volatile.Read(ref _discardedCounters);
        if (discardedCounters != IntPtr.Zero)
        {
            return discardedCounters;
        }

        var allocated = TryAllocateDiscardedCounters();
        if (allocated == IntPtr.Zero)
        {
            // Another thread may have published one in the meantime.
            return Volatile.Read(ref _discardedCounters);
        }

        var published = Interlocked.CompareExchange(ref _discardedCounters, allocated, IntPtr.Zero);
        if (published != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(allocated);
            return published;
        }

        return allocated;
    }

    private static unsafe IntPtr TryAllocateDiscardedCounters()
    {
        // Never freed: probes may keep writing to it for the rest of the process.
        var byteLength = Math.Max(ModuleMemorySize, 1);
        try
        {
            var pointer = Marshal.AllocHGlobal(byteLength);
            Unsafe.InitBlockUnaligned((byte*)pointer, 0, (uint)byteLength);
            return pointer;
        }
        catch
        {
            return IntPtr.Zero;
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
