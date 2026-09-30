// <copyright file="OtelProcessContextAnnouncerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.OtelThreadContext;
using FluentAssertions;
using Xunit;
#if NETCOREAPP3_1_OR_GREATER
using MemoryExtensions = System.MemoryExtensions;
#else
using MemoryExtensions = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions;
#endif

namespace Datadog.Trace.Tests.OtelThreadContext
{
    public unsafe class OtelProcessContextAnnouncerTests
    {
        private const int VersionOffset = 8;
        private const int PayloadSizeOffset = 12;
        private const int TimestampOffset = 16;
        private const int PayloadOffset = 24;
        private const int HeaderSize = 32;

        [Theory]
        [InlineData("7f3c1a2b4000-7f3c1a2b5000 rw-p 00000000 00:01 12345 /memfd:OTEL_CTX (deleted)", 0x7f3c1a2b4000UL)]
        [InlineData("7f3c1a2b4000-7f3c1a2b5000 rw-p 00000000 00:00 0 [anon_shmem:OTEL_CTX]", 0x7f3c1a2b4000UL)]
        [InlineData("55d0dead0000-55d0dead1000 rw-p 00000000 00:00 0 [anon:OTEL_CTX]", 0x55d0dead0000UL)]
        public void FindsTheProcessContextMapping(string line, ulong expected)
        {
            var maps = string.Join(
                "\n",
                "55a0b0000000-55a0b0021000 r--p 00000000 fd:01 1234 /usr/bin/dotnet",
                "7f0000000000-7f0000021000 rw-p 00000000 00:00 0 [heap]",
                line,
                "7ffd00000000-7ffd00021000 rw-p 00000000 00:00 0 [stack]");

            OtelProcessContextAnnouncer.TryParseMappingAddress(maps, out var address).Should().BeTrue();
            ((ulong)address).Should().Be(expected);
        }

        [Fact]
        public void ReportsNoMappingWhenTheProcessContextIsAbsent()
        {
            var maps = string.Join(
                "\n",
                "55a0b0000000-55a0b0021000 r--p 00000000 fd:01 1234 /usr/bin/dotnet",
                // a similarly named mapping that is not the process context
                "7f0000000000-7f0000021000 rw-p 00000000 00:00 0 /memfd:something-else",
                "7ffd00000000-7ffd00021000 rw-p 00000000 00:00 0 [stack]");

            OtelProcessContextAnnouncer.TryParseMappingAddress(maps, out var address).Should().BeFalse();
            address.Should().Be(IntPtr.Zero);
        }

        [Fact]
        public void AppendsTheThreadLocalAttributesToTheExistingPayload()
        {
            var original = EncodeStringAttribute("datadog.process_tags", "team:tracing");
            using var context = new FakeProcessContext(original, timestamp: 1000);
            var extra = ThreadLocalMetadataPayload.Encode(new[] { ThreadLocalMetadataPayload.LocalRootSpanIdKey });

            OtelProcessContextAnnouncer.TryExtendPayload(context.Header, extra, out var failure)
                                       .Should().BeTrue(failure);

            var payload = context.ReadPayload();

            // the bytes we did not write must survive verbatim - we never parse them, precisely so that
            // fields we do not know about cannot be lost
            payload.Should().HaveCount(original.Length + extra.Length);
            MemoryExtensions.AsSpan(payload, 0, original.Length).ToArray().Should().Equal(original);
            MemoryExtensions.AsSpan(payload, original.Length).ToArray().Should().Equal(extra);
        }

        [Fact]
        public void LeavesTheOriginalPayloadBufferUntouched()
        {
            // libdatadog owns the buffer it published; we must point away from it, not overwrite it
            var original = EncodeStringAttribute("datadog.process_tags", "owner:libdatadog");
            using var context = new FakeProcessContext(original, timestamp: 1000);
            var originalAddress = context.PayloadAddress;

            OtelProcessContextAnnouncer.TryExtendPayload(context.Header, [1, 2, 3], out _).Should().BeTrue();

            context.PayloadAddress.Should().NotBe(originalAddress, "the header must point at the extended copy");
            context.ReadOriginalBuffer().Should().Equal(original);
        }

        [Fact]
        public void PublishesANewerNonZeroTimestamp()
        {
            using var context = new FakeProcessContext(CreateValidPayload(), timestamp: 1000);

            OtelProcessContextAnnouncer.TryExtendPayload(context.Header, [1, 2, 3], out _).Should().BeTrue();

            // readers treat 0 as "being mutated" and use the value as a change token, so it must end
            // non-zero and strictly ahead of what was there before
            context.Timestamp.Should().BeGreaterThan(1000);
        }

        [Fact]
        public void AdvancesATimestampThatIsAlreadyInTheFuture()
        {
            // a CLOCK_BOOTTIME value written by libdatadog can be ahead of our monotonic reading on a
            // machine that has been suspended, and the spec still requires the new value to be later
            using var context = new FakeProcessContext(CreateValidPayload(), timestamp: long.MaxValue - 1);

            OtelProcessContextAnnouncer.TryExtendPayload(context.Header, [1, 2, 3], out _).Should().BeTrue();

            context.Timestamp.Should().Be(long.MaxValue);
        }

        [Fact]
        public void RefusesAMappingWithoutTheSignature()
        {
            var original = CreateValidPayload();
            using var context = new FakeProcessContext(original, timestamp: 1000);
            Marshal.WriteByte(context.Header, 0, (byte)'X');

            OtelProcessContextAnnouncer.TryExtendPayload(context.Header, [1], out var failure).Should().BeFalse();
            failure.Should().Contain("signature");
            context.PayloadSize.Should().Be((uint)original.Length, "nothing may be modified when validation fails");
        }

        [Fact]
        public void RefusesAnUnsupportedVersion()
        {
            using var context = new FakeProcessContext(CreateValidPayload(), timestamp: 1000, version: 99);

            OtelProcessContextAnnouncer.TryExtendPayload(context.Header, [1], out var failure).Should().BeFalse();
            failure.Should().Contain("version");
        }

        [Fact]
        public void RefusesAContextThatIsBeingUpdated()
        {
            // zero means another writer is mid-update; a reader would skip it and so do we
            using var context = new FakeProcessContext(CreateValidPayload(), timestamp: 0);

            OtelProcessContextAnnouncer.TryExtendPayload(context.Header, [1], out var failure).Should().BeFalse();
            failure.Should().Contain("being updated");
        }

        [Fact]
        public void DoesNotAnnounceTwiceIfTheKeysAreAlreadyPresent()
        {
            // guards against a future libdatadog that emits the threadlocal.* keys itself
            var already = ThreadLocalMetadataPayload.Encode(new[] { ThreadLocalMetadataPayload.LocalRootSpanIdKey });
            using var context = new FakeProcessContext(already, timestamp: 1000);

            OtelProcessContextAnnouncer.TryExtendPayload(context.Header, already, out var failure).Should().BeFalse();
            failure.Should().Contain("already advertises");
            context.PayloadSize.Should().Be((uint)already.Length);
        }

        private static byte[] CreateValidPayload() => EncodeStringAttribute("datadog.process_tags", "test:value");

        private static byte[] EncodeStringAttribute(string key, string value)
        {
            var anyValue = new List<byte>();
            WriteLengthDelimited(anyValue, fieldNumber: 1, Encoding.UTF8.GetBytes(value));

            var keyValue = new List<byte>();
            WriteLengthDelimited(keyValue, fieldNumber: 1, Encoding.UTF8.GetBytes(key));
            WriteLengthDelimited(keyValue, fieldNumber: 2, anyValue.ToArray());

            var processContext = new List<byte>();
            WriteLengthDelimited(processContext, fieldNumber: 2, keyValue.ToArray());
            return processContext.ToArray();
        }

        private static void WriteLengthDelimited(List<byte> destination, int fieldNumber, byte[] value)
        {
            destination.Add((byte)((fieldNumber << 3) | 2));
            WriteVarInt(destination, (uint)value.Length);
            destination.AddRange(value);
        }

        private static void WriteVarInt(List<byte> destination, uint value)
        {
            while (value >= 0x80)
            {
                destination.Add((byte)(value | 0x80));
                value >>= 7;
            }

            destination.Add((byte)value);
        }

        /// <summary>
        /// A stand-in for the mapping libdatadog publishes: an OTEP 4719 header plus a separately
        /// allocated payload, laid out exactly as the real one.
        /// </summary>
        private sealed class FakeProcessContext : IDisposable
        {
            private readonly IntPtr _originalPayload;
            private readonly int _originalPayloadSize;

            public FakeProcessContext(byte[] payload, long timestamp, uint version = 2)
            {
                _originalPayloadSize = payload.Length;
                _originalPayload = Marshal.AllocHGlobal(payload.Length);
                Marshal.Copy(payload, 0, _originalPayload, payload.Length);

                Header = Marshal.AllocHGlobal(HeaderSize);
                var header = (byte*)Header;

                var signature = Encoding.ASCII.GetBytes("OTEL_CTX");
                for (var i = 0; i < signature.Length; i++)
                {
                    header[i] = signature[i];
                }

                *(uint*)(header + VersionOffset) = version;
                *(uint*)(header + PayloadSizeOffset) = (uint)payload.Length;
                *(long*)(header + TimestampOffset) = timestamp;
                *(IntPtr*)(header + PayloadOffset) = _originalPayload;
            }

            public IntPtr Header { get; }

            public long Timestamp => *(long*)((byte*)Header + TimestampOffset);

            public uint PayloadSize => *(uint*)((byte*)Header + PayloadSizeOffset);

            public IntPtr PayloadAddress => *(IntPtr*)((byte*)Header + PayloadOffset);

            public byte[] ReadPayload()
            {
                var buffer = new byte[PayloadSize];
                Marshal.Copy(PayloadAddress, buffer, 0, buffer.Length);
                return buffer;
            }

            public byte[] ReadOriginalBuffer()
            {
                var buffer = new byte[_originalPayloadSize];
                Marshal.Copy(_originalPayload, buffer, 0, buffer.Length);
                return buffer;
            }

            public void Dispose()
            {
                Marshal.FreeHGlobal(Header);
                Marshal.FreeHGlobal(_originalPayload);
            }
        }
    }
}
