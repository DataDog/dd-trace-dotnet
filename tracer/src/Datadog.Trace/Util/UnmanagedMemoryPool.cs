// <copyright file="UnmanagedMemoryPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Datadog.Trace.Util;

internal unsafe class UnmanagedMemoryPool : IDisposable
{
    private readonly IntPtr* _items;
    private readonly int _length;
    private readonly int _blockSize;
    private IntPtr _firstItem;
    private int _initialSearchIndex;
    private bool _isDisposed;

    public UnmanagedMemoryPool(int blockSize, int poolSize)
    {
        _blockSize = blockSize;
        _items = (IntPtr*)Marshal.AllocHGlobal(poolSize * sizeof(IntPtr));
        _length = poolSize;
        _firstItem = IntPtr.Zero;
        _initialSearchIndex = 0;

        for (var i = 0; i < _length; i++)
        {
            _items[i] = IntPtr.Zero;
        }
    }

    ~UnmanagedMemoryPool()
    {
        Dispose();
    }

    public IntPtr Rent()
    {
        if (_isDisposed)
        {
            ThrowObjectDisposedException();
        }

        var inst = Interlocked.Exchange(ref _firstItem, IntPtr.Zero);
        if (inst == IntPtr.Zero)
        {
            inst = RentSlow();
        }

        return inst;
    }

    private IntPtr RentSlow()
    {
        var items = _items;
        var length = _length;
        for (var i = _initialSearchIndex; i < length; i++)
        {
            var inst = Interlocked.Exchange(ref items[i], IntPtr.Zero);
            if (inst != IntPtr.Zero)
            {
                _initialSearchIndex = i + 1;
                return inst;
            }
        }

        return Marshal.AllocHGlobal(_blockSize);
    }

    public void Return(IntPtr block)
    {
        if (_isDisposed)
        {
            ThrowObjectDisposedException();
        }

        if (Interlocked.CompareExchange(ref _firstItem, block, IntPtr.Zero) != IntPtr.Zero)
        {
            ReturnSlow(block);
        }
    }

    private void ReturnSlow(IntPtr block)
    {
        var items = _items;
        var length = _length;
        for (var i = 0; i < length; i++)
        {
            if (Interlocked.CompareExchange(ref items[i], block, IntPtr.Zero) == IntPtr.Zero)
            {
                _initialSearchIndex = 0;
                return;
            }
        }

        Marshal.FreeHGlobal(block);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        var firstItem = _firstItem;
        if (firstItem != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(firstItem);
            _firstItem = IntPtr.Zero;
        }

        var items = _items;
        var length = _length;
        for (var i = 0; i < length; i++)
        {
            if (items[i] != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(items[i]);
                items[i] = IntPtr.Zero;
            }
        }

        Marshal.FreeHGlobal((IntPtr)_items);

        _isDisposed = true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException("UnmanagedMemoryPool");
    }
}
