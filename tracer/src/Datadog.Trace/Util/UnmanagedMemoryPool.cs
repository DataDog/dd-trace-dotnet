// <copyright file="UnmanagedMemoryPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Util;

internal unsafe class UnmanagedMemoryPool : IDisposable
{
    private readonly IntPtr* _items;
    private readonly int _length;
    private readonly int _blockSize;
    private int _initialSearchIndex;
    private bool _isDisposed;

    public UnmanagedMemoryPool(int blockSize, int poolSize)
    {
        _blockSize = blockSize;
        _items = (IntPtr*)Marshal.AllocHGlobal(poolSize * sizeof(IntPtr));
        _length = poolSize;
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

        var items = _items;
        var length = _length;
        for (var i = _initialSearchIndex; i < length; i++)
        {
            var inst = items[i];
            if (inst != IntPtr.Zero)
            {
                _initialSearchIndex = i + 1;
                items[i] = IntPtr.Zero;
                return inst;
            }
        }

        return RentSlow();
    }

    private IntPtr RentSlow()
    {
        return Marshal.AllocHGlobal(_blockSize);
    }

    public void Return(IntPtr block)
    {
        if (_isDisposed)
        {
            ThrowObjectDisposedException();
        }

        var items = _items;
        var length = _length;
        for (var i = 0; i < length; i++)
        {
            if (items[i] == IntPtr.Zero)
            {
                items[i] = block;
                _initialSearchIndex = 0;
                return;
            }
        }

        ReturnSlow(block);
    }

    public void Return(IList<IntPtr> blocks)
    {
        if (_isDisposed)
        {
            ThrowObjectDisposedException();
        }

        if (blocks.Count == 0)
        {
            return;
        }

        var items = _items;
        var length = _length;
        var blockIndex = 0;
        for (var i = 0; i < length; i++)
        {
            if (items[i] == IntPtr.Zero)
            {
                items[i] = blocks[blockIndex++];
                if (blockIndex == blocks.Count)
                {
                    _initialSearchIndex = 0;
                    return;
                }
            }
        }

        for (var i = blockIndex; i < blocks.Count; i++)
        {
            ReturnSlow(blocks[i]);
        }
    }

    private void ReturnSlow(IntPtr block)
    {
        Marshal.FreeHGlobal(block);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
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
