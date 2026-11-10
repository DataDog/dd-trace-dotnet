// <copyright file="ModuleValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Ci.Coverage.Metadata;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class ModuleValue : IDisposable
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ModuleValue>();
    private static readonly NativeMemoryDebugMetrics ContextBufferMetrics = new();
    private static readonly NativeMemoryDebugMetrics GlobalFallbackBufferMetrics = new();
    private readonly BufferKind _bufferKind;
    private readonly bool _recordNativeMemoryDiagnostics;
    private IntPtr _filesLines;
    private int _allocatedByteLength;

    public ModuleValue(ModuleCoverageMetadata metadata, Module module, int fileLinesMemorySize, BufferKind bufferKind)
    {
        Metadata = metadata;
        Module = module;
        _bufferKind = bufferKind;
        // Keep allocation and free accounting paired even if debug logging changes while this value is alive.
        _recordNativeMemoryDiagnostics = Log.IsEnabled(LogEventLevel.Debug);

        var pointer = IntPtr.Zero;
        try
        {
            pointer = bufferKind == BufferKind.Context ? ContextBufferPool.Rent(fileLinesMemorySize) : Marshal.AllocHGlobal(fileLinesMemorySize);
            unsafe
            {
                Unsafe.InitBlockUnaligned((byte*)pointer, 0, (uint)fileLinesMemorySize);
            }

            _allocatedByteLength = fileLinesMemorySize;
            _filesLines = pointer;
            pointer = IntPtr.Zero;
            if (_recordNativeMemoryDiagnostics)
            {
                GetMetrics(bufferKind).OnAllocated(fileLinesMemorySize);
            }
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                ReleaseNativeBuffer(pointer, fileLinesMemorySize, bufferKind);
            }
        }
    }

    ~ModuleValue()
    {
        Dispose();
    }

    public enum BufferKind
    {
        Context,
        GlobalFallback,
    }

    public ModuleCoverageMetadata Metadata { get; }

    public Module Module { get; }

    // Instrumented methods read this on every entry. The test lifecycle owns the buffer lifetime,
    // so keep the read identical to the original lock-free counter path.
    public IntPtr FilesLines => _filesLines;

    public int AllocatedByteLength => _allocatedByteLength;

    public static void LogNativeMemoryDiagnostics(int processId)
    {
        if (!Log.IsEnabled(LogEventLevel.Debug))
        {
            return;
        }

        LogNativeMemoryDiagnostics(processId, BufferKind.Context, ContextBufferMetrics.GetSnapshot());
        LogNativeMemoryDiagnostics(processId, BufferKind.GlobalFallback, GlobalFallbackBufferMetrics.GetSnapshot());
    }

    [TestingOnly]
    internal static bool IsPooledContextBuffer(IntPtr pointer) => ContextBufferPool.Contains(pointer);

    private static void ReleaseNativeBuffer(IntPtr pointer, int byteLength, BufferKind bufferKind)
    {
        if (bufferKind == BufferKind.Context)
        {
            ContextBufferPool.Return(pointer, byteLength);
        }
        else
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private static NativeMemoryDebugMetrics GetMetrics(BufferKind bufferKind)
        => bufferKind == BufferKind.Context ? ContextBufferMetrics : GlobalFallbackBufferMetrics;

    private static void LogNativeMemoryDiagnostics(int processId, BufferKind bufferKind, NativeMemoryDebugSnapshot diagnostics)
    {
        if (bufferKind == BufferKind.Context)
        {
            Log.Debug<int, long, long, long, long>(
                "Global coverage native context-buffer diagnostics: pid={ProcessId}, currentBytes={CurrentBytes}, peakBytes={PeakBytes}, activeBuffers={ActiveBuffers}, peakBuffers={PeakBuffers}.",
                processId,
                diagnostics.CurrentBytes,
                diagnostics.PeakBytes,
                diagnostics.ActiveBuffers,
                diagnostics.PeakBuffers);
            Log.Debug<int, long, long, long>(
                "Global coverage native context-buffer allocation diagnostics: pid={ProcessId}, allocations={Allocations}, frees={Frees}, maximumBufferBytes={MaximumBufferBytes}.",
                processId,
                diagnostics.AllocationCount,
                diagnostics.FreeCount,
                diagnostics.MaximumBufferBytes);
            Log.Debug<int, long, long>(
                "Global coverage native context-buffer pool diagnostics: pid={ProcessId}, pooledBuffers={PooledBuffers}, pooledBytes={PooledBytes}.",
                processId,
                ContextBufferPool.PooledBuffers,
                ContextBufferPool.PooledBytes);
            return;
        }

        Log.Debug<int, long, long, long, long>(
            "Global coverage native fallback-buffer diagnostics: pid={ProcessId}, currentBytes={CurrentBytes}, peakBytes={PeakBytes}, activeBuffers={ActiveBuffers}, peakBuffers={PeakBuffers}.",
            processId,
            diagnostics.CurrentBytes,
            diagnostics.PeakBytes,
            diagnostics.ActiveBuffers,
            diagnostics.PeakBuffers);
        Log.Debug<int, long, long, long>(
            "Global coverage native fallback-buffer allocation diagnostics: pid={ProcessId}, allocations={Allocations}, frees={Frees}, maximumBufferBytes={MaximumBufferBytes}.",
            processId,
            diagnostics.AllocationCount,
            diagnostics.FreeCount,
            diagnostics.MaximumBufferBytes);
    }

    public void Dispose()
    {
        FreeBuffer();
        GC.SuppressFinalize(this);
    }

    private void FreeBuffer()
    {
        var filesLines = Interlocked.Exchange(ref _filesLines, IntPtr.Zero);
        if (filesLines != IntPtr.Zero)
        {
            var byteLength = Volatile.Read(ref _allocatedByteLength);
            ReleaseNativeBuffer(filesLines, byteLength, _bufferKind);
            Volatile.Write(ref _allocatedByteLength, 0);
            if (_recordNativeMemoryDiagnostics)
            {
                GetMetrics(_bufferKind).OnFreed(byteLength);
            }
        }
    }

    private readonly struct NativeMemoryDebugSnapshot
    {
        public NativeMemoryDebugSnapshot(
            long currentBytes,
            long peakBytes,
            long activeBuffers,
            long peakBuffers,
            long allocationCount,
            long freeCount,
            long maximumBufferBytes)
        {
            CurrentBytes = currentBytes;
            PeakBytes = peakBytes;
            ActiveBuffers = activeBuffers;
            PeakBuffers = peakBuffers;
            AllocationCount = allocationCount;
            FreeCount = freeCount;
            MaximumBufferBytes = maximumBufferBytes;
        }

        public long CurrentBytes { get; }

        public long PeakBytes { get; }

        public long ActiveBuffers { get; }

        public long PeakBuffers { get; }

        public long AllocationCount { get; }

        public long FreeCount { get; }

        public long MaximumBufferBytes { get; }
    }

    /// <summary>
    /// Recycles context buffers instead of freeing them.
    /// An instrumented method caches the raw counters pointer for its whole frame, and that frame can outlive the
    /// ExecutionContext accounting that decides when a session buffer is released (for example, while the method is
    /// inside <see cref="ExecutionContext.Run"/> with another context when the session ends). Keeping the memory owned
    /// by coverage means such a late write can at most mark a line in a later session that reuses the buffer, instead
    /// of writing into freed native memory. The pool holds at most the peak number of context buffers of each size
    /// that were alive at the same time.
    /// </summary>
    private static class ContextBufferPool
    {
        private static readonly ConcurrentDictionary<int, ConcurrentStack<IntPtr>> Buffers = new();
        private static long _pooledBuffers;
        private static long _pooledBytes;

        public static long PooledBuffers => Interlocked.Read(ref _pooledBuffers);

        public static long PooledBytes => Interlocked.Read(ref _pooledBytes);

        public static IntPtr Rent(int byteLength)
        {
            if (Buffers.TryGetValue(byteLength, out var buffers) && buffers.TryPop(out var pointer))
            {
                Interlocked.Decrement(ref _pooledBuffers);
                Interlocked.Add(ref _pooledBytes, -byteLength);
                return pointer;
            }

            return Marshal.AllocHGlobal(byteLength);
        }

        public static void Return(IntPtr pointer, int byteLength)
        {
            // Called from Dispose and the finalizer, so it must not throw.
            try
            {
                Buffers.GetOrAdd(byteLength, static _ => new ConcurrentStack<IntPtr>()).Push(pointer);
                Interlocked.Increment(ref _pooledBuffers);
                Interlocked.Add(ref _pooledBytes, byteLength);
            }
            catch
            {
                // The pool bookkeeping could not be allocated. Keep the block allocated instead of freeing it:
                // a frame may still write through its pointer.
            }
        }

        public static bool Contains(IntPtr pointer)
        {
            foreach (var buffers in Buffers.Values)
            {
                foreach (var pooled in buffers)
                {
                    if (pooled == pointer)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    private sealed class NativeMemoryDebugMetrics
    {
        private long _currentBytes;
        private long _peakBytes;
        private long _activeBuffers;
        private long _peakBuffers;
        private long _allocationCount;
        private long _freeCount;
        private long _maximumBufferBytes;

        public void OnAllocated(int byteLength)
        {
            var currentBytes = Interlocked.Add(ref _currentBytes, byteLength);
            var activeBuffers = Interlocked.Increment(ref _activeBuffers);
            Interlocked.Increment(ref _allocationCount);
            SetMaximum(ref _peakBytes, currentBytes);
            SetMaximum(ref _peakBuffers, activeBuffers);
            SetMaximum(ref _maximumBufferBytes, byteLength);
        }

        public void OnFreed(int byteLength)
        {
            Interlocked.Add(ref _currentBytes, -byteLength);
            Interlocked.Decrement(ref _activeBuffers);
            Interlocked.Increment(ref _freeCount);
        }

        public NativeMemoryDebugSnapshot GetSnapshot()
            => new(
                Interlocked.Read(ref _currentBytes),
                Interlocked.Read(ref _peakBytes),
                Interlocked.Read(ref _activeBuffers),
                Interlocked.Read(ref _peakBuffers),
                Interlocked.Read(ref _allocationCount),
                Interlocked.Read(ref _freeCount),
                Interlocked.Read(ref _maximumBufferBytes));

        private static void SetMaximum(ref long target, long value)
        {
            var current = Interlocked.Read(ref target);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }
}
