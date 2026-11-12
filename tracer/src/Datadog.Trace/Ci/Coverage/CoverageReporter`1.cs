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
using Datadog.Trace.SourceGenerators;
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
    private static IntPtr _discardedCounters;
    private static ModuleValue? _globalModuleValue;

    [TestingOnly]
    internal static bool HasDiscardedCounters => Volatile.Read(ref _discardedCounters) != IntPtr.Zero;

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

    // Allocated on the first probe failure only, so successful runs don't keep a second counters buffer per module.
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
        try
        {
            var byteLength = Math.Max(Layout.MemorySize, 1);
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
                Layout.Metadata,
                Layout.Module,
                Layout.MemorySize,
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
                Layout.Metadata,
                Layout.Module,
                Layout.MemorySize,
                out var module) || module is null)
        {
            return null;
        }

        Interlocked.CompareExchange(ref _globalModuleValue, module, null);
        return Volatile.Read(ref _globalModuleValue)!;
    }

    private static int GetByteOffset(int fileIndex)
        => Layout.Metadata.CoverageMode == 0 ? Layout.Metadata.GetOffset(fileIndex) : Layout.Metadata.GetOffset(fileIndex) * sizeof(int);

    private static unsafe void* GetPointer(ModuleValue module, int fileIndex)
    {
        var filesLines = module.FilesLines;
        if (filesLines == IntPtr.Zero)
        {
            ThrowHelper.ThrowNullReferenceException("Counter memory was disposed.");
        }

        return (byte*)filesLines + GetByteOffset(fileIndex);
    }

    /// <summary>
    /// The module's coverage layout. It is initialized by an explicit static constructor on first use, from inside
    /// <see cref="GetFileCounter"/>'s protected region, instead of by a static constructor of the reporter: if it fails,
    /// every later probe still runs that method and marks the current test's coverage incomplete before the cached
    /// <see cref="TypeInitializationException"/> propagates.
    /// </summary>
    private static class Layout
    {
        public static readonly TMeta Metadata;
        public static readonly Module Module;
        public static readonly int MemorySize;

        static Layout()
        {
            Metadata = new TMeta();
            Module = typeof(TMeta).Module;
            MemorySize = CoverageMetadataValidator.ValidateAndGetRawByteLength(Metadata);
        }
    }
}
