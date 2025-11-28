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
internal sealed unsafe class UnmanagedMemoryPool : IDisposable
{
    private readonly IntPtr* _items;
    private readonly int _length;
    private readonly int _blockSize;
    private int _initialSearchIndex;
    private bool _isDisposed;

    public UnmanagedMemoryPool(int blockSize, int poolSize)
    {
        if (blockSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize));
        }

        if (poolSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(poolSize));
        }

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
        Dispose(disposing: false);
    }

    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// Beware that this method is not thread safe, and needs to be used with [ThreadStatic] in case of multiple thread scenarios
    /// </summary>
    /// <returns>Pointer to a memory block of size specified in the constructor</returns>
    public IntPtr Rent()
    {
        if (_isDisposed)
        {
            ThrowObjectDisposedException();
        }

        var start = _initialSearchIndex;
        for (var scanned = 0; scanned < _length; scanned++)
        {
            var i = (start + scanned) % _length;
            var inst = _items[i];
            if (inst != IntPtr.Zero)
            {
                _items[i] = IntPtr.Zero;
                _initialSearchIndex = (i + 1) % _length;
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
        if (block == IntPtr.Zero)
        {
            return;
        }

        if (_isDisposed)
        {
            // If the pool is disposed, we free the block to avoid memory leaks.
            ReturnSlow(block);
            return;
        }

        for (var i = 0; i < _length; i++)
        {
            if (_items[i] == IntPtr.Zero)
            {
                _items[i] = block;

                // Preserve round‑robin behaviour: only rewind the
                // search pointer when we insert a block that sits
                // *before* the current search index.
                if (i < _initialSearchIndex)
                {
                    _initialSearchIndex = i;
                }

                return;
            }
        }

        ReturnSlow(block);
    }

    public void Return(IList<IntPtr> blocks)
    {
        if (blocks.Count == 0)
        {
            return;
        }

        if (_isDisposed)
        {
            foreach (var block in blocks)
            {
                if (block != IntPtr.Zero)
                {
                    // If the pool is disposed, we free each block to avoid memory leaks.
                    ReturnSlow(block);
                }
            }

            return;
        }

        var blockIndex = 0;
        var earliestInserted = _length; // track the left‑most slot we refill

        for (var i = 0; i < _length && blockIndex < blocks.Count; i++)
        {
            if (_items[i] == IntPtr.Zero)
            {
                _items[i] = blocks[blockIndex++];
                earliestInserted = Math.Min(earliestInserted, i);
            }
        }

        if (earliestInserted < _initialSearchIndex)
        {
            _initialSearchIndex = earliestInserted;
        }

        for (var i = blockIndex; i < blocks.Count; i++)
        {
            if (blocks[i] != IntPtr.Zero)
            {
                ReturnSlow(blocks[i]);
            }
        }
    }

    private void ReturnSlow(IntPtr block)
    {
        Marshal.FreeCoTaskMem(block);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        // Mark as disposed first so re‑entry is a no‑op even if an exception
        // is thrown while releasing unmanaged resources.
        _isDisposed = true;

        try
        {
            // Free each rented block (if any) first
            for (var i = 0; i < _length; i++)
            {
                if (_items[i] != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(_items[i]);
                    _items[i] = IntPtr.Zero;
                }
            }
        }
        finally
        {
            // Always release the pointer table itself, even if an earlier
            // FreeCoTaskMem throws (extremely unlikely, but defensive).
            Marshal.FreeCoTaskMem((IntPtr)_items);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException(nameof(UnmanagedMemoryPool));
    }
}
