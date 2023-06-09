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
        private readonly byte[] _metadataBytes = StringEncoding.UTF8.GetBytes("metadata");
        private readonly byte[] _asteriskBytes = StringEncoding.UTF8.GetBytes("*");
        private readonly byte[] _runtimeIdBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.RuntimeId);
        private readonly byte[] _runtimeIdValueBytes = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);
        private readonly byte[] _languageNameBytes = StringEncoding.UTF8.GetBytes("language");
        private readonly byte[] _languageNameValueBytes = StringEncoding.UTF8.GetBytes("dotnet");
        private readonly byte[] _libraryVersionBytes = StringEncoding.UTF8.GetBytes(CommonTags.LibraryVersion);
        private readonly byte[] _libraryVersionValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.AssemblyVersion);
        private readonly byte[] _environmentBytes = StringEncoding.UTF8.GetBytes("env");
        private readonly byte[]? _environmentValueBytes;
        private readonly byte[] _eventsBytes = StringEncoding.UTF8.GetBytes("events");

        private readonly ArraySegment<byte> _envelopBytes;

        public CIEventMessagePackFormatter(TracerSettings tracerSettings)
        {
            if (!string.IsNullOrEmpty(tracerSettings.EnvironmentInternal))
            {
                _environmentValueBytes = StringEncoding.UTF8.GetBytes(tracerSettings.EnvironmentInternal);
            }

            _envelopBytes = GetEnvelopeArraySegment();
        }

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
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, VersionBytes);
            // Value
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, 1);

            // # Metadata

            // Key
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metadataBytes);

            // Value
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);

            // ->  * : {}

            // Key
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _asteriskBytes);

            // Value (RuntimeId, Language, library_version, Env?)
            int valuesCount = 3;
            if (_environmentValueBytes is not null)
            {
                valuesCount++;
            }

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, valuesCount);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdValueBytes);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageNameValueBytes);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _libraryVersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _libraryVersionValueBytes);

            if (_environmentValueBytes is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentValueBytes);
            }

            // # Events

            // Key
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _eventsBytes);

            return new ArraySegment<byte>(bytes, 0, offset);
        }
    }
}
