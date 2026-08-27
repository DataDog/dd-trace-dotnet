// <copyright file="OtelThreadContextRecordPoolTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.OtelThreadContext;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OtelThreadContext
{
    public unsafe class OtelThreadContextRecordPoolTests
    {
        [Fact]
        public void HandsOutDistinctBlocks()
        {
            var pool = new OtelThreadContextRecordPool();

            var blocks = Enumerable.Range(0, 16).Select(_ => (IntPtr)pool.Rent()).ToList();

            blocks.Should().OnlyHaveUniqueItems();
            blocks.Should().NotContain(IntPtr.Zero);
        }

        [Fact]
        public void BlocksAreAlignedForTheRecord()
        {
            var pool = new OtelThreadContextRecordPool();

            for (var i = 0; i < 16; i++)
            {
                var block = (long)pool.Rent();
                (block % OtelThreadContextRecord.Alignment).Should().Be(0);
            }
        }

        [Fact]
        public void RecyclesReturnedBlocks()
        {
            var pool = new OtelThreadContextRecordPool();

            var first = pool.Rent();
            pool.Return(first);

            ((IntPtr)pool.Rent()).Should().Be((IntPtr)first, "a returned block must be reused rather than leaked");
        }

        [Fact]
        public void ARecycledBlockCarriesNoContextFromItsPreviousOwner()
        {
            var pool = new OtelThreadContextRecordPool();

            var block = pool.Rent();
            OtelThreadContextRecord.Write(block, new TraceId(ulong.MaxValue, ulong.MaxValue), ulong.MaxValue, ulong.MaxValue, traceFlags: 1);
            pool.Return(block);

            var recycled = pool.Rent();
            var contents = new Span<byte>(recycled, OtelThreadContextRecord.Size);

            // valid must be 0, and nothing of the previous context may remain in the trace or span ids.
            // The free list stores its link in the first 8 bytes of a released block, so this also proves
            // Rent wipes that link before the block is used as a record again.
            contents[24].Should().Be(0);
            contents.Slice(0, 24).ToArray().Should().OnlyContain(b => b == 0);
        }

        [Fact]
        public void IsSafeToUseFromMultipleThreads()
        {
            var pool = new OtelThreadContextRecordPool();
            var blocks = new System.Collections.Concurrent.ConcurrentBag<IntPtr>();

            Parallel.For(0, 64, _ =>
            {
                var block = pool.Rent();
                blocks.Add((IntPtr)block);
                pool.Return(block);
            });

            // every rent/return pair must have produced a usable block, and the free list must not have
            // been corrupted along the way - which a subsequent rent would expose
            blocks.Should().HaveCount(64).And.NotContain(IntPtr.Zero);
            ((IntPtr)pool.Rent()).Should().NotBe(IntPtr.Zero);
        }

        [Fact]
        public void ReturningNullIsIgnored()
        {
            var pool = new OtelThreadContextRecordPool();

            pool.Return(null);

            ((IntPtr)pool.Rent()).Should().NotBe(IntPtr.Zero);
        }

        [Fact]
        public void RentedBlocksAreInitializedAsAnEmptyRecord()
        {
            var pool = new OtelThreadContextRecordPool();
            var expectations = new List<(int Offset, byte Value)>
            {
                (24, 0),   // valid
                (25, 0),   // trace-flags
                (28, 0),   // attrs-data[0].key: datadog.local_root_span_id
                (29, 16),  // attrs-data[0].length
            };

            var contents = new Span<byte>(pool.Rent(), OtelThreadContextRecord.Size);

            foreach (var (offset, value) in expectations)
            {
                contents[offset].Should().Be(value, "offset {0} of a rented record", offset);
            }
        }
    }
}
