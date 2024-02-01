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

/// <summary>
/// Beware that this type is not thread safe and should be used with [ThreadStatic]
/// </summary>
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
        _items = (IntPtr*)Marshal.AllocCoTaskMem(poolSize * sizeof(IntPtr));
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

        for (var i = 0; i < _length; i++)
        {
            if (_items[i] == IntPtr.Zero)
            {
                _items[i] = block;
                _initialSearchIndex = 0;
                return;
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
}
