// <copyright file="UnmanagedMemoryPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Util;

/// <summary>
/// Beware that this type is not thread safe and should be used with [ThreadStatic]
/// </summary>
internal unsafe class UnmanagedMemoryPool : IDisposable
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(UnmanagedMemoryPool));

    // Statistics
    private static int _fastPoolCount = 0;
    private static int _slowPoolCount = 0;

    private readonly IntPtr* _items;
    private readonly int _length;
    private readonly int _blockSize;
    private int _initialSearchIndex;

    private bool _isSlow;
    private bool _isDisposed;

    public UnmanagedMemoryPool(int blockSize, int poolSize)
    {
        OnPoolCreated();

        _blockSize = blockSize;
        if (!_isSlow)
        {
            _items = (IntPtr*)Marshal.AllocCoTaskMem(poolSize * sizeof(IntPtr));
            _length = poolSize;
            _initialSearchIndex = 0;

            for (var i = 0; i < _length; i++)
            {
                _items[i] = IntPtr.Zero;
            }
        }
    }

    ~UnmanagedMemoryPool()
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

        if (!_isSlow)
        {
            for (var i = _initialSearchIndex; i < _length; i++)
            {
                var inst = _items[i];
                if (inst != IntPtr.Zero)
                {
                    _initialSearchIndex = i + 1;
                    _items[i] = IntPtr.Zero;
                    return inst;
                }
            }
        }

        return RentSlow();
    }

    private IntPtr RentSlow()
    {
        return Marshal.AllocCoTaskMem(_blockSize);
    }

    public void Return(IntPtr block)
    {
        if (IsDisposed)
        {
            ThrowObjectDisposedException();
        }

        if (!_isSlow)
        {
            for (var i = 0; i < _length; i++)
            {
                if (_items[i] == IntPtr.Zero)
                {
                    _items[i] = block;
                    _initialSearchIndex = 0;
                    return;
                }
            }
        }

        ReturnSlow(block);
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
        if (!_isSlow)
        {
            for (var i = 0; i < _length; i++)
            {
                if (_items[i] == IntPtr.Zero)
                {
                    _items[i] = blocks[blockIndex++];
                    if (blockIndex == blocks.Count)
                    {
                        _initialSearchIndex = 0;
                        return;
                    }
                }
            }

            _initialSearchIndex = 0;
        }

        for (var i = blockIndex; i < blocks.Count; i++)
        {
            ReturnSlow(blocks[i]);
        }
    }

    private void ReturnSlow(IntPtr block)
    {
        Marshal.FreeCoTaskMem(block);
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        OnPoolDestroyed();
        if (_isSlow)
        {
            return;
        }

        for (var i = 0; i < _length; i++)
        {
            if (_items[i] != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(_items[i]);
                _items[i] = IntPtr.Zero;
            }
        }

        Marshal.FreeCoTaskMem((IntPtr)_items);

        _isDisposed = true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException("UnmanagedMemoryPool");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnPoolCreated()
    {
        if (_fastPoolCount < WafConstants.MaxUnmanagedPools)
        {
            Interlocked.Increment(ref _fastPoolCount);
            Log.Debug<int, int>("Created fast WAF unmanaged pool. Current pools -> Fast: {PoolCount}  Slow: {SlowPoolCount}", _fastPoolCount, _slowPoolCount);
            TelemetryFactory.Metrics.RecordGaugePoolCount(_fastPoolCount);
            _isSlow = false;
        }
        else
        {
            Interlocked.Increment(ref _slowPoolCount);
            Log.Debug<int, int>("Created slow WAF unmanaged pool. Current pools -> Fast: {PoolCount}  Slow: {SlowPoolCount}", _fastPoolCount, _slowPoolCount);
            TelemetryFactory.Metrics.RecordGaugePoolSlowCount(_slowPoolCount);
            _isSlow = true;
        }

        RegisterTheoreticalMaxMemUsage();
    }

    private void OnPoolDestroyed()
    {
        if (_isSlow)
        {
            Interlocked.Decrement(ref _slowPoolCount);
        }
        else
        {
            Interlocked.Decrement(ref _fastPoolCount);
        }

        RegisterTheoreticalMaxMemUsage();
    }

    private void RegisterTheoreticalMaxMemUsage()
    {
        // Calculate theoretical max memory consumed by this pool
        var maxMem = (_blockSize + sizeof(IntPtr)) * _length;
        TelemetryFactory.Metrics.RecordGaugePoolMemory((_fastPoolCount) * maxMem);
    }
}
