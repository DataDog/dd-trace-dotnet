// <copyright file="CIEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class CIEventMessagePackFormatter : IMessagePackFormatter<IEnumerable<IEvent>>
    {
        private byte[] _versionBytes = StringEncoding.UTF8.GetBytes("version");
        private byte[] _versionValueBytes = StringEncoding.UTF8.GetBytes("1.0.0");
        // .
        private byte[] _metadataBytes = StringEncoding.UTF8.GetBytes("metadata");
        private byte[] _containerIdBytes = StringEncoding.UTF8.GetBytes("container_id");
        private byte[] _containerIdValueBytes = StringEncoding.UTF8.GetBytes(ContainerMetadata.GetContainerId() ?? string.Empty);
        private byte[] _runtimeIdBytes = StringEncoding.UTF8.GetBytes("runtime_id");
        private byte[] _runtimeIdValueBytes = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);
        private byte[] _languageNameBytes = StringEncoding.UTF8.GetBytes("language_name");
        private byte[] _languageNameValueBytes = StringEncoding.UTF8.GetBytes(".NET");
        private byte[] _languageVersionBytes = StringEncoding.UTF8.GetBytes("language_version");
        private byte[] _languageVersionValueBytes = StringEncoding.UTF8.GetBytes(FrameworkDescription.Instance.ProductVersion);
        private byte[] _languageInterpreterBytes = StringEncoding.UTF8.GetBytes("language_interpreter");
        private byte[] _languageInterpreterValueBytes = StringEncoding.UTF8.GetBytes(FrameworkDescription.Instance.Name);
        private byte[] _tracerVersionBytes = StringEncoding.UTF8.GetBytes("tracer_version");
        private byte[] _tracerVersionValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.AssemblyVersion);
        private byte[] _hostnameBytes = StringEncoding.UTF8.GetBytes("hostname");
        private byte[] _hostnameValueBytes = StringEncoding.UTF8.GetBytes(HostMetadata.Instance.Hostname);
        private byte[] _environmentBytes = StringEncoding.UTF8.GetBytes(ConfigurationKeys.Environment);
        private byte[] _environmentValueBytes = StringEncoding.UTF8.GetBytes(Tracer.Instance.Settings.Environment ?? string.Empty);
        private byte[] _appVersionBytes = StringEncoding.UTF8.GetBytes(ConfigurationKeys.ServiceVersion);
        private byte[] _appVersionValueBytes = StringEncoding.UTF8.GetBytes(Tracer.Instance.Settings.ServiceVersion ?? string.Empty);
        // .
        private byte[] _eventsBytes = StringEncoding.UTF8.GetBytes("events");

        public IEnumerable<IEvent> Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }

        public int Serialize(ref byte[] bytes, int offset, IEnumerable<IEvent> value, IFormatterResolver formatterResolver)
        {
            if (value is null)
            {
                return 0;
            }

            var originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 3);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _versionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _versionValueBytes);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metadataBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 9);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _containerIdBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _containerIdValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageNameValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageVersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageVersionValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageInterpreterBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageInterpreterValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _tracerVersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _tracerVersionValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _hostnameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _hostnameValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _appVersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _appVersionValueBytes);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _eventsBytes);

            if (value is null)
            {
                offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, 0);
            }
            else
            {
                var lstValue = value is List<IEvent> lValue ? lValue : new List<IEvent>(value);

                offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, lstValue.Count);
                for (var i = 0; i < lstValue.Count; i++)
                {
                    var eventItem = lstValue[i];

                    if (eventItem is TraceEvent traceEvent)
                    {
                        offset += formatterResolver.GetFormatter<TraceEvent>().Serialize(ref bytes, offset, traceEvent, formatterResolver);
                    }
                    else if (eventItem is TestEvent testEvent)
                    {
                        offset += formatterResolver.GetFormatter<TestEvent>().Serialize(ref bytes, offset, testEvent, formatterResolver);
                    }
                    else
                    {
                        offset += MessagePackBinary.WriteNil(ref bytes, offset);
                    }
                }
            }

            return offset - originalOffset;
        }
    }
}
