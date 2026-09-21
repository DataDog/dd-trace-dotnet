// <copyright file="OtelProcessContextReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// Test-only implementation of the OTEP 4719 reading protocol. This deliberately shares no parsing
    /// or header-layout code with the tracer: its purpose is to exercise the externally visible contract.
    /// </summary>
    internal static class OtelProcessContextReader
    {
        private const int HeaderSize = 32;
        private const int VersionOffset = 8;
        private const int PayloadSizeOffset = 12;
        private const int TimestampOffset = 16;
        private const int PayloadOffset = 24;
        private const uint SupportedVersion = 2;
        private const int MaxPayloadSize = 1024 * 1024;

        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("OTEL_CTX");

        public static async Task<OtelProcessContextSnapshot> ReadAsync(int processId, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            Exception lastFailure = null;

            do
            {
                try
                {
                    return Read(processId);
                }
                catch (Exception ex) when (ex is IOException
                                        or InvalidOperationException
                                        or Win32Exception
                                        or InvalidProtocolBufferException)
                {
                    lastFailure = ex;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
            while (DateTime.UtcNow < deadline);

            throw new InvalidOperationException(
                $"Could not read a stable OTEP process context from process {processId} within {timeout}.",
                lastFailure);
        }

        private static OtelProcessContextSnapshot Read(int processId)
        {
            var headerAddress = FindMappingAddress(processId);

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var header = ReadRemoteMemory(processId, headerAddress, HeaderSize);
                ValidateHeader(header);

                var timestampBefore = BitConverter.ToUInt64(header, TimestampOffset);
                if (timestampBefore == 0)
                {
                    Thread.Yield();
                    continue;
                }

                Thread.MemoryBarrier();

                var payloadSize = BitConverter.ToUInt32(header, PayloadSizeOffset);
                var payloadAddress = BitConverter.ToUInt64(header, PayloadOffset);
                if (payloadSize == 0 || payloadSize > MaxPayloadSize || payloadAddress == 0)
                {
                    throw new InvalidOperationException(
                        $"The process context has an invalid payload pointer or size ({payloadAddress:x}, {payloadSize}).");
                }

                var payload = ReadRemoteMemory(processId, payloadAddress, (int)payloadSize);

                Thread.MemoryBarrier();

                var timestampBytes = ReadRemoteMemory(processId, headerAddress + TimestampOffset, sizeof(ulong));
                var timestampAfter = BitConverter.ToUInt64(timestampBytes, 0);
                if (timestampBefore == timestampAfter && timestampAfter != 0)
                {
                    return ParsePayload(payload);
                }
            }

            throw new InvalidOperationException("The process context was concurrently updated during every read attempt.");
        }

        private static ulong FindMappingAddress(int processId)
        {
            var matches = new List<ulong>();

            foreach (var line in File.ReadLines($"/proc/{processId}/maps"))
            {
                if (!IsContextMapping(line))
                {
                    continue;
                }

                var separator = line.IndexOf('-');
                if (separator <= 0
                 || !ulong.TryParse(line.Substring(0, separator), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var address))
                {
                    throw new InvalidOperationException($"Could not parse the process-context mapping: {line}");
                }

                matches.Add(address);
            }

            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one OTEP process-context mapping in process {processId}, found {matches.Count}.");
            }

            return matches[0];
        }

        private static bool IsContextMapping(string line)
            => line.IndexOf("[anon_shmem:OTEL_CTX]", StringComparison.Ordinal) >= 0
            || line.IndexOf("[anon:OTEL_CTX]", StringComparison.Ordinal) >= 0
            || line.IndexOf("/memfd:OTEL_CTX", StringComparison.Ordinal) >= 0;

        private static void ValidateHeader(byte[] header)
        {
            for (var i = 0; i < Signature.Length; i++)
            {
                if (header[i] != Signature[i])
                {
                    throw new InvalidOperationException("The process-context mapping does not contain the OTEL_CTX signature.");
                }
            }

            var version = BitConverter.ToUInt32(header, VersionOffset);
            if (version != SupportedVersion)
            {
                throw new InvalidOperationException($"Process-context version {version} is not supported by the test reader.");
            }
        }

        private static byte[] ReadRemoteMemory(int processId, ulong address, int length)
        {
            var buffer = new byte[length];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                var offset = 0;
                while (offset < length)
                {
                    var local = new IoVector
                    {
                        Base = IntPtr.Add(handle.AddrOfPinnedObject(), offset),
                        Length = new UIntPtr((uint)(length - offset)),
                    };

                    var remoteAddress = checked(address + (ulong)offset);
                    if (remoteAddress > long.MaxValue)
                    {
                        throw new InvalidOperationException($"Remote address 0x{remoteAddress:x} cannot be represented by IntPtr.");
                    }

                    var remote = new IoVector
                    {
                        Base = new IntPtr((long)remoteAddress),
                        Length = new UIntPtr((uint)(length - offset)),
                    };

                    var read = ProcessVmReadV(
                                   processId,
                                   ref local,
                                   new UIntPtr(1),
                                   ref remote,
                                   new UIntPtr(1),
                                   UIntPtr.Zero)
                              .ToInt64();

                    if (read <= 0)
                    {
                        throw new Win32Exception(
                            Marshal.GetLastWin32Error(),
                            $"process_vm_readv failed while reading process {processId} at 0x{remoteAddress:x}.");
                    }

                    offset += checked((int)read);
                }
            }
            finally
            {
                handle.Free();
            }

            return buffer;
        }

        private static OtelProcessContextSnapshot ParsePayload(byte[] payload)
        {
            var resourceAttributes = new List<OtelProcessContextAttribute>();
            var additionalAttributes = new List<OtelProcessContextAttribute>();
            var input = new CodedInputStream(payload);
            uint tag;

            while ((tag = input.ReadTag()) != 0)
            {
                switch (tag)
                {
                    case 0x0A: // ProcessContext.resource, field 1
                        ParseResource(input.ReadBytes().ToByteArray(), resourceAttributes);
                        break;
                    case 0x12: // ProcessContext attributes/extra_attributes, field 2
                        additionalAttributes.Add(ParseKeyValue(input.ReadBytes().ToByteArray()));
                        break;
                    default:
                        input.SkipLastField();
                        break;
                }
            }

            return new OtelProcessContextSnapshot(resourceAttributes, additionalAttributes);
        }

        private static void ParseResource(byte[] payload, List<OtelProcessContextAttribute> attributes)
        {
            var input = new CodedInputStream(payload);
            uint tag;

            while ((tag = input.ReadTag()) != 0)
            {
                // Resource.attributes, field 1
                if (tag == 0x0A)
                {
                    attributes.Add(ParseKeyValue(input.ReadBytes().ToByteArray()));
                }
                else
                {
                    input.SkipLastField();
                }
            }
        }

        private static OtelProcessContextAttribute ParseKeyValue(byte[] payload)
        {
            var input = new CodedInputStream(payload);
            var key = string.Empty;
            OtelProcessContextValue value = null;
            uint tag;

            while ((tag = input.ReadTag()) != 0)
            {
                switch (tag)
                {
                    case 0x0A: // KeyValue.key, field 1
                        key = input.ReadString();
                        break;
                    case 0x12: // KeyValue.value, field 2
                        value = ParseAnyValue(input.ReadBytes().ToByteArray());
                        break;
                    default:
                        input.SkipLastField();
                        break;
                }
            }

            return new OtelProcessContextAttribute(key, value);
        }

        private static OtelProcessContextValue ParseAnyValue(byte[] payload)
        {
            var input = new CodedInputStream(payload);
            string stringValue = null;
            IReadOnlyList<string> arrayValue = null;
            uint tag;

            while ((tag = input.ReadTag()) != 0)
            {
                switch (tag)
                {
                    case 0x0A: // AnyValue.string_value, field 1
                        stringValue = input.ReadString();
                        arrayValue = null;
                        break;
                    case 0x2A: // AnyValue.array_value, field 5
                        arrayValue = ParseArrayValue(input.ReadBytes().ToByteArray());
                        stringValue = null;
                        break;
                    default:
                        input.SkipLastField();
                        break;
                }
            }

            return new OtelProcessContextValue(stringValue, arrayValue);
        }

        private static IReadOnlyList<string> ParseArrayValue(byte[] payload)
        {
            var values = new List<string>();
            var input = new CodedInputStream(payload);
            uint tag;

            while ((tag = input.ReadTag()) != 0)
            {
                // ArrayValue.values, field 1
                if (tag == 0x0A)
                {
                    values.Add(ParseAnyValue(input.ReadBytes().ToByteArray()).StringValue);
                }
                else
                {
                    input.SkipLastField();
                }
            }

            return values;
        }

        [DllImport("libc", EntryPoint = "process_vm_readv", SetLastError = true)]
        private static extern IntPtr ProcessVmReadV(
            int processId,
            ref IoVector localIov,
            UIntPtr localIovCount,
            ref IoVector remoteIov,
            UIntPtr remoteIovCount,
            UIntPtr flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct IoVector
        {
            public IntPtr Base;
            public UIntPtr Length;
        }

        internal sealed class OtelProcessContextSnapshot
        {
            public OtelProcessContextSnapshot(
                IReadOnlyList<OtelProcessContextAttribute> resourceAttributes,
                IReadOnlyList<OtelProcessContextAttribute> additionalAttributes)
            {
                ResourceAttributes = resourceAttributes;
                AdditionalAttributes = additionalAttributes;
            }

            public IReadOnlyList<OtelProcessContextAttribute> ResourceAttributes { get; }

            public IReadOnlyList<OtelProcessContextAttribute> AdditionalAttributes { get; }
        }

        internal sealed class OtelProcessContextAttribute
        {
            public OtelProcessContextAttribute(string key, OtelProcessContextValue value)
            {
                Key = key;
                Value = value;
            }

            public string Key { get; }

            public OtelProcessContextValue Value { get; }
        }

        internal sealed class OtelProcessContextValue
        {
            public OtelProcessContextValue(string stringValue, IReadOnlyList<string> arrayValue)
            {
                StringValue = stringValue;
                ArrayValue = arrayValue;
            }

            public string StringValue { get; }

            public IReadOnlyList<string> ArrayValue { get; }
        }
    }
}
