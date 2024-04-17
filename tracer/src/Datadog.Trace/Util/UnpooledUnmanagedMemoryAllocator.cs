// <copyright file="UnpooledUnmanagedMemoryAllocator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Util;

/// <summary>
/// Non pooled memory pool (what a paradox)
/// </summary>
internal class UnpooledUnmanagedMemoryAllocator : IUnmanagedMemoryAllocator
{
    private static int _instanceCount;
    private readonly int _blockSize;
    private readonly MetricTags.UnmanagedMemoryPoolComponent _component;

    private bool _isDisposed;

    public UnpooledUnmanagedMemoryAllocator(int blockSize, MetricTags.UnmanagedMemoryPoolComponent component)
    {
        _blockSize = blockSize;
        _component = component;
        Interlocked.Increment(ref _instanceCount);
        TelemetryFactory.Metrics.RecordGaugeUnmanagedMemoryPool(MetricTags.UnmanagedMemoryPoolType.Unpooled, _component, _instanceCount);
    }

    ~UnpooledUnmanagedMemoryAllocator()
    {
        Dispose();
    }

    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// Beware that this method is not thread safe, and needs to be used with [ThreadStatic] in case of multiple thread scenarios
    /// </summary>
    /// <returns>Pointer to a memory block of size specified in the constructor</returns>
    public IntPtr Rent()
    {
        if (IsDisposed)
        {
            ThrowObjectDisposedException();
        }

        return Marshal.AllocCoTaskMem(_blockSize);
    }

    public void Return(IntPtr block)
    {
        if (IsDisposed)
        {
            ThrowObjectDisposedException();
        }

        Marshal.FreeCoTaskMem(block);
    }

    public void Return(IList<IntPtr> blocks)
    {
        if (IsDisposed)
        {
            ThrowObjectDisposedException();
        }

        if (blocks.Count == 0)
        {
            return;
        }

        var blockIndex = 0;

        for (var i = blockIndex; i < blocks.Count; i++)
        {
            Marshal.FreeCoTaskMem(blocks[i]);
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        _isDisposed = true;

        Interlocked.Decrement(ref _instanceCount);
        TelemetryFactory.Metrics.RecordGaugeUnmanagedMemoryPool(MetricTags.UnmanagedMemoryPoolType.Unpooled, _component, _instanceCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException(nameof(UnpooledUnmanagedMemoryAllocator));
    }
}
