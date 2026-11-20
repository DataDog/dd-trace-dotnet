// <copyright file="NativeBufferPoolTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Coverage;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class NativeBufferPoolTests
{
    [Fact]
    public void RentAllocatesAZeroedBufferWhenNoneWasReturned()
    {
        var pool = new NativeBufferPool();

        var buffer = pool.Rent(64);
        try
        {
            ReadBytes(buffer, 64).Should().OnlyContain(value => value == 0);
            pool.PooledBuffers.Should().Be(0);
            pool.PooledBytes.Should().Be(0);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public void ReturnedBufferIsReusedForTheSameSizeAndZeroed()
    {
        var pool = new NativeBufferPool();
        var buffer = pool.Rent(32);
        var reused = IntPtr.Zero;
        try
        {
            FillBytes(buffer, 32, 0xAB);

            pool.Return(buffer, 32);

            pool.Contains(buffer).Should().BeTrue();
            pool.PooledBuffers.Should().Be(1);
            pool.PooledBytes.Should().Be(32);

            reused = pool.Rent(32);
            reused.Should().Be(buffer);
            ReadBytes(reused, 32).Should().OnlyContain(value => value == 0);
            pool.Contains(buffer).Should().BeFalse();
            pool.PooledBuffers.Should().Be(0);
            pool.PooledBytes.Should().Be(0);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            if (reused != IntPtr.Zero && reused != buffer)
            {
                Marshal.FreeHGlobal(reused);
            }
        }
    }

    [Fact]
    public void BuffersAreOnlyReusedForTheirOwnSize()
    {
        var pool = new NativeBufferPool();
        var small = pool.Rent(16);
        pool.Return(small, 16);

        var large = pool.Rent(32);
        try
        {
            large.Should().NotBe(small);
            pool.Contains(small).Should().BeTrue();
            pool.PooledBuffers.Should().Be(1);
            pool.PooledBytes.Should().Be(16);
        }
        finally
        {
            Marshal.FreeHGlobal(large);
            Marshal.FreeHGlobal(pool.Rent(16));
        }
    }

    [Fact]
    public void RentRejectsANegativeSize()
    {
        var pool = new NativeBufferPool();

        pool.Invoking(p => p.Rent(-1)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReturnKeepsTheBufferAllocatedWhenThePoolCannotRecordIt()
    {
        var pool = new NativeBufferPool(_ => throw new OutOfMemoryException());
        var buffer = pool.Rent(8);
        try
        {
            pool.Invoking(p => p.Return(buffer, 8)).Should().NotThrow();

            pool.Contains(buffer).Should().BeFalse();
            pool.PooledBuffers.Should().Be(0);
            pool.PooledBytes.Should().Be(0);

            // The buffer is still allocated: writing to it and freeing it once must be valid.
            FillBytes(buffer, 8, 0x01);
            ReadBytes(buffer, 8).Should().OnlyContain(value => value == 0x01);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public async Task ConcurrentRentersNeverShareABuffer()
    {
        const int workers = 8;
        const int iterations = 500;
        var pool = new NativeBufferPool();
        var owners = new ConcurrentDictionary<IntPtr, int>();
        var rentedBuffers = new ConcurrentDictionary<IntPtr, bool>();
        var sharedBuffers = 0;
        using var start = new Barrier(workers);

        var tasks = Enumerable.Range(1, workers).Select(
            worker => Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait();
                    for (var i = 0; i < iterations; i++)
                    {
                        var buffer = pool.Rent(16);
                        rentedBuffers.TryAdd(buffer, true);
                        if (!owners.TryAdd(buffer, worker))
                        {
                            Interlocked.Increment(ref sharedBuffers);
                        }

                        FillBytes(buffer, 16, (byte)worker);
                        if (ReadBytes(buffer, 16).Any(value => value != worker))
                        {
                            Interlocked.Increment(ref sharedBuffers);
                        }

                        owners.TryRemove(buffer, out _);
                        pool.Return(buffer, 16);
                    }
                },
                TaskCreationOptions.LongRunning));

        try
        {
            await Task.WhenAll(tasks);

            sharedBuffers.Should().Be(0);
            pool.PooledBuffers.Should().BeInRange(1, workers);
            pool.PooledBytes.Should().Be(pool.PooledBuffers * 16);
        }
        finally
        {
            // Every worker has finished, so the pool is no longer used and its buffers can be freed directly.
            foreach (var buffer in rentedBuffers.Keys)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static byte[] ReadBytes(IntPtr buffer, int length)
    {
        var bytes = new byte[length];
        Marshal.Copy(buffer, bytes, 0, length);
        return bytes;
    }

    private static void FillBytes(IntPtr buffer, int length, byte value)
    {
        var bytes = Enumerable.Repeat(value, length).ToArray();
        Marshal.Copy(bytes, 0, buffer, length);
    }
}
