// <copyright file="OtelThreadContextReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// Test-only OTEP 4947 reader. Given the address of one thread's exported
    /// <c>otel_thread_ctx_v1</c> TLS slot, it follows the record pointer in the target process and
    /// decodes the record without sharing layout or parsing code with the tracer.
    /// </summary>
    internal static class OtelThreadContextReader
    {
        private const int HeaderSize = 28;
        private const int TraceIdOffset = 0;
        private const int SpanIdOffset = 16;
        private const int ValidOffset = 24;
        private const int TraceFlagsOffset = 25;
        private const int AttrsDataSizeOffset = 26;
        private const int MaxAttrsDataSize = 640 - HeaderSize;

        public static OtelThreadContextSnapshot Read(int processId, ulong tlsSlotAddress)
        {
            var pointerBytes = OtelProcessContextReader.ReadRemoteMemory(processId, tlsSlotAddress, IntPtr.Size);
            var recordAddress = IntPtr.Size == sizeof(ulong)
                                    ? BitConverter.ToUInt64(pointerBytes, 0)
                                    : BitConverter.ToUInt32(pointerBytes, 0);

            if (recordAddress == 0)
            {
                throw new InvalidOperationException(
                    $"The otel_thread_ctx_v1 slot at 0x{tlsSlotAddress:x} contains a null record pointer.");
            }

            if ((recordAddress & 1) != 0)
            {
                throw new InvalidOperationException(
                    $"The OTEP thread-context record at 0x{recordAddress:x} is not 2-byte aligned.");
            }

            var header = OtelProcessContextReader.ReadRemoteMemory(processId, recordAddress, HeaderSize);
            var attrsDataSize = BitConverter.ToUInt16(header, AttrsDataSizeOffset);
            if (attrsDataSize > MaxAttrsDataSize)
            {
                throw new InvalidOperationException(
                    $"The OTEP thread-context record has an invalid attrs-data-size ({attrsDataSize}).");
            }

            var attributes = attrsDataSize == 0
                                 ? Array.Empty<OtelThreadContextAttribute>()
                                 : ParseAttributes(
                                     OtelProcessContextReader.ReadRemoteMemory(
                                         processId,
                                         checked(recordAddress + HeaderSize),
                                         attrsDataSize));

            return new OtelThreadContextSnapshot(
                recordAddress,
                ToHexString(header, TraceIdOffset, 16),
                ToHexString(header, SpanIdOffset, 8),
                header[ValidOffset],
                header[TraceFlagsOffset],
                attrsDataSize,
                attributes);
        }

        private static IReadOnlyList<OtelThreadContextAttribute> ParseAttributes(byte[] data)
        {
            var attributes = new List<OtelThreadContextAttribute>();
            var offset = 0;

            while (offset < data.Length)
            {
                if (data.Length - offset < 2)
                {
                    throw new InvalidOperationException("The OTEP thread-context record contains a truncated attribute header.");
                }

                var keyIndex = data[offset];
                var valueLength = data[offset + 1];
                offset += 2;

                if (data.Length - offset < valueLength)
                {
                    throw new InvalidOperationException(
                        $"The OTEP thread-context record contains a truncated value for attribute index {keyIndex}.");
                }

                attributes.Add(
                    new OtelThreadContextAttribute(
                        keyIndex,
                        Encoding.UTF8.GetString(data, offset, valueLength)));

                offset += valueLength;
            }

            return attributes;
        }

        private static string ToHexString(byte[] bytes, int offset, int count)
        {
            const string HexDigits = "0123456789abcdef";

            var result = new char[count * 2];
            for (var i = 0; i < count; i++)
            {
                var value = bytes[offset + i];
                result[i * 2] = HexDigits[value >> 4];
                result[(i * 2) + 1] = HexDigits[value & 0x0f];
            }

            return new string(result);
        }

        internal sealed class OtelThreadContextSnapshot
        {
            public OtelThreadContextSnapshot(
                ulong recordAddress,
                string traceId,
                string spanId,
                byte valid,
                byte traceFlags,
                ushort attrsDataSize,
                IReadOnlyList<OtelThreadContextAttribute> attributes)
            {
                RecordAddress = recordAddress;
                TraceId = traceId;
                SpanId = spanId;
                Valid = valid;
                TraceFlags = traceFlags;
                AttrsDataSize = attrsDataSize;
                Attributes = attributes;
            }

            public ulong RecordAddress { get; }

            public string TraceId { get; }

            public string SpanId { get; }

            public byte Valid { get; }

            public byte TraceFlags { get; }

            public ushort AttrsDataSize { get; }

            public IReadOnlyList<OtelThreadContextAttribute> Attributes { get; }
        }

        internal sealed class OtelThreadContextAttribute
        {
            public OtelThreadContextAttribute(byte keyIndex, string value)
            {
                KeyIndex = keyIndex;
                Value = value;
            }

            public byte KeyIndex { get; }

            public string Value { get; }
        }
    }
}
