// <copyright file="NativeBufferPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Native buffers grouped by size that are recycled instead of freed.
/// Coverage counter buffers are written through raw pointers that instrumented methods cache for their whole frame,
/// and such a frame can outlive the owner that releases the buffer (for example, while the method runs code inside
/// <see cref="ExecutionContext.Run"/> with another context when its test session ends). Because returned buffers are
/// only ever handed out again as coverage counters, a late write through a stale pointer lands in coverage-owned
/// memory instead of corrupting freed native memory. Use one pool per counter layout: then a late write can only add
/// to a later owner's counters (apart from the existing wraparound of 32-bit call counts), while a write of a different
/// width could also clear the counters next to it.
/// The pool holds at most the peak number of buffers of each size that were rented at the same time.
/// </summary>
internal sealed class NativeBufferPool
{
    private static readonly Func<int, ConcurrentStack<IntPtr>> CreateBuffersForSize = static _ => new ConcurrentStack<IntPtr>();

    private readonly ConcurrentDictionary<int, ConcurrentStack<IntPtr>> _buffersBySize = new();
    private readonly Func<int, ConcurrentStack<IntPtr>> _createBuffersForSize;
    private long _pooledBuffers;
    private long _pooledBytes;

    public NativeBufferPool()
    {
        _createBuffersForSize = CreateBuffersForSize;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeBufferPool"/> class that uses
    /// <paramref name="createBuffersForSize"/> to create the storage for each new buffer size.
    /// </summary>
    [TestingOnly]
    internal NativeBufferPool(Func<int, ConcurrentStack<IntPtr>> createBuffersForSize)
    {
        _createBuffersForSize = createBuffersForSize;
    }

    /// <summary>
    /// Gets the number of buffers currently available for reuse.
    /// </summary>
    public long PooledBuffers => Interlocked.Read(ref _pooledBuffers);

    /// <summary>
    /// Gets the total size, in bytes, of the buffers currently available for reuse.
    /// </summary>
    public long PooledBytes => Interlocked.Read(ref _pooledBytes);

    /// <summary>
    /// Returns a zero-filled buffer of <paramref name="byteLength"/> bytes, reusing a returned buffer of the same size
    /// when one is available.
    /// </summary>
    /// <param name="byteLength">The size of the buffer, in bytes.</param>
    /// <returns>A pointer to the buffer.</returns>
    public unsafe IntPtr Rent(int byteLength)
    {
        if (byteLength < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(byteLength));
        }

        if (!_buffersBySize.TryGetValue(byteLength, out var buffers) || !buffers.TryPop(out var buffer))
        {
            buffer = Marshal.AllocHGlobal(byteLength);
        }
        else
        {
            Interlocked.Decrement(ref _pooledBuffers);
            Interlocked.Add(ref _pooledBytes, -byteLength);
        }

        Unsafe.InitBlockUnaligned((byte*)buffer, 0, (uint)byteLength);
        return buffer;
    }

    /// <summary>
    /// Makes a buffer obtained from <see cref="Rent"/> available for reuse. The caller must not use it afterwards.
    /// Never throws, because it runs from disposal and finalizers: if the pool cannot record the buffer, the buffer is
    /// left allocated rather than freed.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <param name="byteLength">The size the buffer was rented with, in bytes.</param>
    public void Return(IntPtr buffer, int byteLength)
    {
        try
        {
            _buffersBySize.GetOrAdd(byteLength, _createBuffersForSize).Push(buffer);
            Interlocked.Increment(ref _pooledBuffers);
            Interlocked.Add(ref _pooledBytes, byteLength);
        }
        catch
        {
            // Freeing the buffer here would bring back the use-after-free this pool exists to prevent.
        }
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="buffer"/> is currently available for reuse.
    /// </summary>
    [TestingOnly]
    internal bool Contains(IntPtr buffer)
    {
        foreach (var buffers in _buffersBySize.Values)
        {
            foreach (var pooled in buffers)
            {
                if (pooled == buffer)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
