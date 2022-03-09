// <copyright file="CIEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class CIEventMessagePackFormatter : EventMessagePackFormatter<EventsPayload>
    {
        private readonly byte[] _metadataBytes = StringEncoding.UTF8.GetBytes("metadata");
        private readonly byte[] _containerIdBytes = StringEncoding.UTF8.GetBytes("container_id");
        private readonly byte[]? _containerIdValueBytes;
        private readonly byte[] _runtimeIdBytes = StringEncoding.UTF8.GetBytes("runtime_id");
        private readonly byte[] _runtimeIdValueBytes = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);
        private readonly byte[] _languageNameBytes = StringEncoding.UTF8.GetBytes("language");
        private readonly byte[] _languageNameValueBytes = StringEncoding.UTF8.GetBytes("dotnet");
        private readonly byte[] _languageInterpreterBytes = StringEncoding.UTF8.GetBytes(CommonTags.RuntimeName);
        private readonly byte[] _languageInterpreterValueBytes = StringEncoding.UTF8.GetBytes(FrameworkDescription.Instance.Name);
        private readonly byte[] _languageVersionBytes = StringEncoding.UTF8.GetBytes(CommonTags.RuntimeVersion);
        private readonly byte[] _languageVersionValueBytes = StringEncoding.UTF8.GetBytes(FrameworkDescription.Instance.ProductVersion);

        private readonly byte[] _ciLibraryVersionBytes = StringEncoding.UTF8.GetBytes(CommonTags.LibraryVersion);
        private readonly byte[] _ciLibraryVersionValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.AssemblyVersion);

        private readonly byte[] _environmentBytes = StringEncoding.UTF8.GetBytes("env");
        private readonly byte[]? _environmentValueBytes;
        private readonly byte[] _serviceBytes = StringEncoding.UTF8.GetBytes("service");
        private readonly byte[]? _serviceValueBytes;
        private readonly byte[] _appVersionBytes = StringEncoding.UTF8.GetBytes("app_version");
        private readonly byte[]? _appVersionValueBytes;
        private readonly byte[] _hostnameBytes = StringEncoding.UTF8.GetBytes("hostname");
        private readonly byte[] _hostnameValueBytes = StringEncoding.UTF8.GetBytes(HostMetadata.Instance.Hostname);

        private readonly byte[] _eventsBytes = StringEncoding.UTF8.GetBytes("events");

        private readonly ArraySegment<byte> _envelopBytes;

        public CIEventMessagePackFormatter(TracerSettings tracerSettings)
        {
            var containerId = ContainerMetadata.GetContainerId();
            if (containerId is not null)
            {
                _containerIdValueBytes = StringEncoding.UTF8.GetBytes(containerId);
            }
            else
            {
                _containerIdValueBytes = null;
            }

            var environment = tracerSettings.Environment;
            if (environment is not null)
            {
                _environmentValueBytes = StringEncoding.UTF8.GetBytes(environment);
            }
            else
            {
                _environmentValueBytes = null;
            }

            var service = tracerSettings.ServiceName;
            if (service is not null)
            {
                _serviceValueBytes = StringEncoding.UTF8.GetBytes(service);
            }
            else
            {
                _serviceValueBytes = null;
            }

            var serviceVersion = tracerSettings.ServiceVersion;
            if (serviceVersion is not null)
            {
                _appVersionValueBytes = StringEncoding.UTF8.GetBytes(serviceVersion);
            }
            else
            {
                _appVersionValueBytes = null;
            }

            _envelopBytes = GetEnvelopeArraySegment();
        }

        public override int Serialize(ref byte[] bytes, int offset, EventsPayload? value, IFormatterResolver formatterResolver)
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
                Log.Error<int>("Error while locking the events buffer with {count} events.", value.Events.Count);
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
            }

            return offset - originalOffset;
        }

        private ArraySegment<byte> GetEnvelopeArraySegment()
        {
            var offset = 0;
            var bytes = new byte[1024];

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 3);

            // .

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, VersionBytes);
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, 1);

            // .

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metadataBytes);

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 10);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _containerIdBytes);
            if (_containerIdValueBytes is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _containerIdValueBytes);
            }
            else
            {
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageNameValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageInterpreterBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageInterpreterValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageVersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageVersionValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _ciLibraryVersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _ciLibraryVersionValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentBytes);
            if (_environmentValueBytes is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentValueBytes);
            }
            else
            {
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _serviceBytes);
            if (_serviceValueBytes is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _serviceValueBytes);
            }
            else
            {
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _appVersionBytes);
            if (_appVersionValueBytes is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _appVersionValueBytes);
            }
            else
            {
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _hostnameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _hostnameValueBytes);

            // .

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _eventsBytes);

            return new ArraySegment<byte>(bytes, 0, offset);
        }
    }
}
