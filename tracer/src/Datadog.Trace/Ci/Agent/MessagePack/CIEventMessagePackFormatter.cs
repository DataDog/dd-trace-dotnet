// <copyright file="CIEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class CIEventMessagePackFormatter : EventMessagePackFormatter<CIVisibilityProtocolPayload>
    {
        private readonly byte[]? _environmentValueBytes;
        private readonly ArraySegment<byte> _envelopBytes;

        public CIEventMessagePackFormatter(TracerSettings tracerSettings)
        {
            if (!string.IsNullOrEmpty(tracerSettings.Environment))
            {
                _environmentValueBytes = StringEncoding.UTF8.GetBytes(tracerSettings.Environment);
            }

            _envelopBytes = GetEnvelopeArraySegment();
        }

#if NETCOREAPP
        private ReadOnlySpan<byte> MetadataBytes => "metadata"u8;

        private ReadOnlySpan<byte> AsteriskBytes => "*"u8;

        private ReadOnlySpan<byte> RuntimeIdBytes => "runtime-id"u8;

        private ReadOnlySpan<byte> LanguageNameBytes => "language"u8;

        private ReadOnlySpan<byte> LanguageNameValueBytes => "dotnet"u8;

        private ReadOnlySpan<byte> LibraryVersionBytes => "library_version"u8;

        private ReadOnlySpan<byte> EnvironmentBytes => "env"u8;

        private ReadOnlySpan<byte> EventsBytes => "events"u8;

        private byte[] RuntimeIdValueBytes { get; } = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);

        private byte[] LibraryVersionValueBytes { get; } = StringEncoding.UTF8.GetBytes(TracerConstants.AssemblyVersion);
#else
        private byte[] MetadataBytes { get; } = StringEncoding.UTF8.GetBytes("metadata");

        private byte[] AsteriskBytes { get; } = StringEncoding.UTF8.GetBytes("*");

        private byte[] RuntimeIdBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.RuntimeId);

        private byte[] LanguageNameBytes { get; } = StringEncoding.UTF8.GetBytes("language");

        private byte[] LanguageNameValueBytes { get; } = StringEncoding.UTF8.GetBytes("dotnet");

        private byte[] LibraryVersionBytes { get; } = StringEncoding.UTF8.GetBytes(CommonTags.LibraryVersion);

        private byte[] EnvironmentBytes { get; } = StringEncoding.UTF8.GetBytes("env");

        private byte[] EventsBytes { get; } = StringEncoding.UTF8.GetBytes("events");

        private byte[] RuntimeIdValueBytes { get; } = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);

        private byte[] LibraryVersionValueBytes { get; } = StringEncoding.UTF8.GetBytes(TracerConstants.AssemblyVersion);
#endif

        public override int Serialize(ref byte[] bytes, int offset, CIVisibilityProtocolPayload? value, IFormatterResolver formatterResolver)
        {
            if (value is null)
            {
                return 0;
            }

            var originalOffset = offset;

            // Write envelope
            MessagePackBinary.EnsureCapacity(ref bytes, offset, _envelopBytes.Count);
            Buffer.BlockCopy(_envelopBytes.Array!, _envelopBytes.Offset, bytes, offset, _envelopBytes.Count);
            offset += _envelopBytes.Count;

            // Write events
            if (value.Events.Lock())
            {
                var data = value.Events.Data;
                MessagePackBinary.EnsureCapacity(ref bytes, offset, data.Count);
                Buffer.BlockCopy(data.Array!, data.Offset, bytes, offset, data.Count);
                offset += data.Count;
            }
            else
            {
                Log.Error<int>("Error while locking the events buffer with {Count} events.", value.Events.Count);
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
            }

            return offset - originalOffset;
        }

        private ArraySegment<byte> GetEnvelopeArraySegment()
        {
            var offset = 0;
            var bytes = new byte[512];

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 3);

            // # Version

            // Key
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, VersionBytes);

            // Value
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, 1);

            // # Metadata

            // Key
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, MetadataBytes);

            // Value
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);

            // ->  * : {}

            // Key
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, AsteriskBytes);

            // Value (RuntimeId, Language, library_version, Env?)
            int valuesCount = 3;
            if (_environmentValueBytes is not null)
            {
                valuesCount++;
            }

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, valuesCount);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, RuntimeIdBytes);
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, RuntimeIdValueBytes);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, LanguageNameBytes);
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, LanguageNameValueBytes);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, LibraryVersionBytes);
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, LibraryVersionValueBytes);

            if (_environmentValueBytes is not null)
            {
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, EnvironmentBytes);
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _environmentValueBytes);
            }

            // # Events

            // Key
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, EventsBytes);

            return new ArraySegment<byte>(bytes, 0, offset);
        }
    }
}
