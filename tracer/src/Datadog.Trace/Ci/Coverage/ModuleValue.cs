// <copyright file="ModuleValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Ci.Coverage.Metadata;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class ModuleValue : IDisposable
{
    private const int RetiredMask = int.MinValue;
    private const int ReferenceCountMask = int.MaxValue;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ModuleValue>();
    private static readonly NativeMemoryDebugMetrics ContextBufferMetrics = new();
    private static readonly NativeMemoryDebugMetrics GlobalFallbackBufferMetrics = new();
    private readonly BufferKind _bufferKind;
    private readonly bool _recordNativeMemoryDiagnostics;
    private Action<ModuleValue, bool>? _onRetirementCompleted;
    private IntPtr _filesLines;
    private int _allocatedByteLength;
    private int _lifetimeState = 1; // The container owns one reference until it finishes reading the buffer.
    private int _mergeOnRetirement;
    private int _ownerReleased;
    private int _retirementStarted;

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
            pointer = Marshal.AllocHGlobal(fileLinesMemorySize);
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
                Marshal.FreeHGlobal(pointer);
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

    public IntPtr FilesLines => Interlocked.CompareExchange(ref _filesLines, IntPtr.Zero, IntPtr.Zero);

    public int AllocatedByteLength => Volatile.Read(ref _allocatedByteLength);

    public static void LogNativeMemoryDiagnostics(int processId)
    {
        if (!Log.IsEnabled(LogEventLevel.Debug))
        {
            return;
        }

        LogNativeMemoryDiagnostics(processId, BufferKind.Context, ContextBufferMetrics.GetSnapshot());
        LogNativeMemoryDiagnostics(processId, BufferKind.GlobalFallback, GlobalFallbackBufferMetrics.GetSnapshot());
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

    public unsafe bool TryAcquireProbe(int offset, out void* pointer)
    {
        while (true)
        {
            var state = Volatile.Read(ref _lifetimeState);
            if ((state & RetiredMask) != 0)
            {
                pointer = null;
                return false;
            }

            if ((state & ReferenceCountMask) == ReferenceCountMask)
            {
                ThrowHelper.ThrowInvalidOperationException("The coverage probe reference count exceeded the supported range.");
            }

            if (Interlocked.CompareExchange(ref _lifetimeState, state + 1, state) == state)
            {
                pointer = (byte*)_filesLines + offset;
                return true;
            }
        }
    }

    public void ReleaseProbe() => ReleaseReference();

    /// <summary>
    /// Stops new probes from acquiring the buffer while retaining the container's owner reference.
    /// The owner can safely scan the counters before <see cref="Dispose()"/> releases that reference.
    /// </summary>
    public void Retire(
        Action? onRetirementPending,
        Action<ModuleValue, bool>? onRetirementCompleted,
        bool mergeOnCompletion)
    {
        if (Interlocked.CompareExchange(ref _retirementStarted, 1, 0) != 0)
        {
            return;
        }

        _onRetirementCompleted = onRetirementCompleted;
        if (mergeOnCompletion)
        {
            Volatile.Write(ref _mergeOnRetirement, 1);
        }

        // Register the retiring module before publishing the retired bit. The last probe can
        // complete immediately after that publication, so the handler must already be waiting.
        onRetirementPending?.Invoke();

        while (true)
        {
            var state = Volatile.Read(ref _lifetimeState);
            if ((state & ReferenceCountMask) > 1)
            {
                // At least one invocation overlapped the first scan. Capture its final writes
                // again after all probe references have drained.
                Volatile.Write(ref _mergeOnRetirement, 1);
            }

            var retiredState = state | RetiredMask;
            if (Interlocked.CompareExchange(ref _lifetimeState, retiredState, state) == state)
            {
                // Dispose normally releases the owner after retirement. Handle a concurrent owner
                // release too, so publishing the retired bit at zero references cannot strand a buffer.
                if (retiredState == RetiredMask)
                {
                    CompleteRetirement();
                }

                return;
            }
        }
    }

    public void Dispose()
    {
        Retire(onRetirementPending: null, onRetirementCompleted: null, mergeOnCompletion: false);
        if (Interlocked.Exchange(ref _ownerReleased, 1) == 0)
        {
            ReleaseReference();
        }

        GC.SuppressFinalize(this);
    }

    private void ReleaseReference()
    {
        var state = Interlocked.Decrement(ref _lifetimeState);
        if (state == RetiredMask)
        {
            CompleteRetirement();
        }
    }

    private void CompleteRetirement()
    {
        var onCompleted = Interlocked.Exchange(ref _onRetirementCompleted, null);
        try
        {
            onCompleted?.Invoke(this, Volatile.Read(ref _mergeOnRetirement) != 0);
        }
        finally
        {
            FreeBuffer();
        }
    }

    private void FreeBuffer()
    {
        var filesLines = Interlocked.Exchange(ref _filesLines, IntPtr.Zero);
        if (filesLines != IntPtr.Zero)
        {
            var byteLength = Volatile.Read(ref _allocatedByteLength);
            Marshal.FreeHGlobal(filesLines);
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
