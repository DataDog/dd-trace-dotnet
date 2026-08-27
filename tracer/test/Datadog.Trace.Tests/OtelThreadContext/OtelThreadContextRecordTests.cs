// <copyright file="OtelThreadContextRecordTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.OtelThreadContext;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OtelThreadContext
{
    /// <summary>
    /// The record layout is an inter-process contract, so these tests deliberately hard-code the offsets
    /// and sizes straight from OTEP 4947 rather than reading them back from the implementation.
    /// </summary>
    public unsafe class OtelThreadContextRecordTests
    {
        private const int TraceIdOffset = 0;
        private const int SpanIdOffset = 16;
        private const int ValidOffset = 24;
        private const int TraceFlagsOffset = 25;
        private const int AttrsDataSizeOffset = 26;
        private const int AttrsDataOffset = 28;

        // one 2-byte attribute header plus the 16 characters of the local root span id
        private const int ExpectedAttrsDataSize = 18;

        public static TheoryData<ulong, ulong, ulong, ulong, byte> Contexts => new()
        {
            // traceIdUpper, traceIdLower, spanId, localRootSpanId, traceFlags
            { 0x0123456789abcdefUL, 0xfedcba9876543210UL, 0x1122334455667788UL, 0x8877665544332211UL, 1 },
            { 0UL, 0xfedcba9876543210UL, 0x1122334455667788UL, 0x1122334455667788UL, 0 }, // 64-bit trace id
            { 0UL, 0UL, 0UL, 0UL, 0 }, // all zeroes, i.e. no trace
            { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 1 },
            { 1UL, 1UL, 1UL, 1UL, 3 }, // sampled + random-trace-id flags
        };

        [Fact]
        public void RecordIsTheSizeAndAlignmentTheSpecExpects()
        {
            // the spec recommends 640 bytes, which is the fixed window the OTel eBPF profiler reads
            OtelThreadContextRecord.Size.Should().Be(640);

            // the spec requires at least 2-byte alignment; we go further and use a cache line
            OtelThreadContextRecord.Alignment.Should().BeGreaterOrEqualTo(2);
            (OtelThreadContextRecord.Size % OtelThreadContextRecord.Alignment).Should().Be(0);
        }

        [Fact]
        public void InitializeZeroesTheRecordAndLeavesItInvalid()
        {
            var buffer = NewBuffer();

            fixed (byte* record = buffer)
            {
                // dirty the buffer first, so we can tell that Initialize really clears it
                buffer.AsSpan().Fill(0xcd);
                OtelThreadContextRecord.Initialize(record);
            }

            buffer[ValidOffset].Should().Be(0, "a record must not be readable before a context is written");
            buffer.AsSpan(TraceIdOffset, 24).ToArray().Should().OnlyContain(b => b == 0);
            buffer[TraceFlagsOffset].Should().Be(0);
            ReadAttrsDataSize(buffer).Should().Be(ExpectedAttrsDataSize);

            // key index 0 is the cross-language convention for datadog.local_root_span_id
            buffer[AttrsDataOffset].Should().Be(0);
            buffer[AttrsDataOffset + 1].Should().Be(16);

            // nothing beyond the one attribute we publish
            buffer.AsSpan(AttrsDataOffset + ExpectedAttrsDataSize).ToArray().Should().OnlyContain(b => b == 0);
        }

        [Theory]
        [MemberData(nameof(Contexts))]
        public void WriteProducesTheLayoutTheSpecDescribes(ulong traceIdUpper, ulong traceIdLower, ulong spanId, ulong localRootSpanId, byte traceFlags)
        {
            var traceId = new TraceId(traceIdUpper, traceIdLower);
            var buffer = NewBuffer();

            fixed (byte* record = buffer)
            {
                OtelThreadContextRecord.Initialize(record);
                OtelThreadContextRecord.Write(record, traceId, spanId, localRootSpanId, traceFlags);
            }

            buffer[ValidOffset].Should().Be(1, "the record is complete and readable");
            buffer[TraceFlagsOffset].Should().Be(traceFlags);
            ReadAttrsDataSize(buffer).Should().Be(ExpectedAttrsDataSize);

            // trace id and span id are big endian, as in the W3C traceparent header. Cross-check against
            // the tracer's own hex rendering, which is defined to use network byte order.
            HexString.ToHexString(buffer.AsSpan(TraceIdOffset, 16))
                     .Should().Be(HexString.ToHexString(traceId, pad16To32: true));

            HexString.ToHexString(buffer.AsSpan(SpanIdOffset, 8))
                     .Should().Be(HexString.ToHexString(spanId));

            // the local root span id is published as 16 lower-case hex characters at key index 0
            buffer[AttrsDataOffset].Should().Be(0);
            buffer[AttrsDataOffset + 1].Should().Be(16);
            Encoding.ASCII.GetString(buffer, AttrsDataOffset + 2, 16)
                    .Should().Be(HexString.ToHexString(localRootSpanId));
        }

        [Fact]
        public void InvalidateClearsOnlyTheValidFlag()
        {
            var traceId = new TraceId(0x0123456789abcdefUL, 0xfedcba9876543210UL);
            var buffer = NewBuffer();

            fixed (byte* record = buffer)
            {
                OtelThreadContextRecord.Initialize(record);
                OtelThreadContextRecord.Write(record, traceId, 42, 43, traceFlags: 1);
            }

            var written = (byte[])buffer.Clone();

            fixed (byte* record = buffer)
            {
                OtelThreadContextRecord.Invalidate(record);
            }

            buffer[ValidOffset].Should().Be(0);

            // detaching via the valid flag must not disturb anything else: the spec lets a writer either
            // clear the thread-local pointer or clear this flag, and we always use the flag
            written[ValidOffset] = 0;
            buffer.Should().Equal(written);
        }

        [Fact]
        public void SuccessiveWritesOverwriteTheWholeContext()
        {
            var buffer = NewBuffer();

            fixed (byte* record = buffer)
            {
                OtelThreadContextRecord.Initialize(record);
                OtelThreadContextRecord.Write(record, new TraceId(ulong.MaxValue, ulong.MaxValue), ulong.MaxValue, ulong.MaxValue, traceFlags: 1);
                OtelThreadContextRecord.Write(record, TraceId.Zero, 0, 0, traceFlags: 0);
            }

            buffer[ValidOffset].Should().Be(1);
            buffer[TraceFlagsOffset].Should().Be(0);
            buffer.AsSpan(TraceIdOffset, 24).ToArray().Should().OnlyContain(b => b == 0, "no bytes of the previous context may survive");
            Encoding.ASCII.GetString(buffer, AttrsDataOffset + 2, 16).Should().Be("0000000000000000");
        }

        private static byte[] NewBuffer() => new byte[OtelThreadContextRecord.Size];

        private static ushort ReadAttrsDataSize(byte[] buffer)
        {
            // attrs-data-size uses native endianness, unlike the trace and span ids
            return BitConverter.IsLittleEndian
                       ? (ushort)(buffer[AttrsDataSizeOffset] | (buffer[AttrsDataSizeOffset + 1] << 8))
                       : (ushort)((buffer[AttrsDataSizeOffset] << 8) | buffer[AttrsDataSizeOffset + 1]);
        }
    }
}
