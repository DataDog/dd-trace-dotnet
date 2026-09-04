// <copyright file="OtelThreadContextRecordPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// A process-wide pool of unmanaged, cache-line aligned blocks holding OTEP 4947 thread context records.
/// <para>
/// Blocks are never returned to the allocator. Threads die at arbitrary points and the address of their
/// record may be held by an out-of-process reader, so releasing the memory would risk a use-after-free
/// across a process boundary. Recycling instead bounds the footprint by the peak number of threads that
/// have carried an active span, at <see cref="OtelThreadContextRecord.Size"/> bytes each.
/// </para>
/// <para>
/// A block is rented once per thread and returned once that thread is gone, so contention is negligible
/// and a plain lock is used rather than a lock-free free list, which would have to contend with ABA.
/// </para>
/// </summary>
internal sealed unsafe class OtelThreadContextRecordPool
{
    public static readonly OtelThreadContextRecordPool Instance = new();

    private readonly object _lock = new();

    // Singly-linked LIFO free list. A released block is reused as its own link node: its first 8 bytes
    // hold the address of the next free block. That is safe because a released block is, by definition,
    // no longer reachable from any thread's TLS slot, and Rent clears the whole block before returning it.
    private byte* _freeList;

    /// <summary>
    /// Gets a block ready to be published, either recycled or freshly allocated. The returned block is
    /// initialized and marked invalid, so it is safe to install before the first context is written.
    /// </summary>
    public byte* Rent()
    {
        byte* block;

        lock (_lock)
        {
            block = _freeList;

            if (block != null)
            {
                _freeList = *(byte**)block;
            }
        }

        if (block == null)
        {
            block = Allocate();
        }

        // this also wipes the bytes the free list used as its link node
        OtelThreadContextRecord.Initialize(block);
        return block;
    }

    /// <summary>
    /// Returns a block to the pool once its owning thread is gone.
    /// <para>
    /// The block is zeroed first. A new thread's TLS slot is zero-initialized by the loader, so it should
    /// never observe a recycled block at all - but if it somehow did, a zeroed block reads as "no context"
    /// rather than as another thread's context.
    /// </para>
    /// </summary>
    public void Return(byte* block)
    {
        if (block == null)
        {
            return;
        }

        new Span<byte>(block, OtelThreadContextRecord.Size).Clear();

        lock (_lock)
        {
            *(byte**)block = _freeList;
            _freeList = block;
        }
    }

    private static byte* Allocate()
    {
        // AllocHGlobal does not guarantee cache-line alignment, so over-allocate and round up. The raw
        // pointer is intentionally dropped: blocks live for the lifetime of the process (see the class
        // remarks), so there is nothing left to free them with.
        var raw = (long)Marshal.AllocHGlobal(OtelThreadContextRecord.Size + OtelThreadContextRecord.Alignment - 1);
        var aligned = (raw + OtelThreadContextRecord.Alignment - 1) & ~((long)OtelThreadContextRecord.Alignment - 1);
        return (byte*)aligned;
    }
}
