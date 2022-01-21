// <copyright file="CIEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class CIEventMessagePackFormatter : EventMessagePackFormatter<IEnumerable<IEvent>>
    {
        // .
        private readonly byte[] _metadataBytes = StringEncoding.UTF8.GetBytes("metadata");
        private readonly byte[] _containerIdBytes = StringEncoding.UTF8.GetBytes("container_id");
        private readonly byte[] _containerIdValueBytes;
        private readonly byte[] _runtimeIdBytes = StringEncoding.UTF8.GetBytes("runtime_id");
        private readonly byte[] _runtimeIdValueBytes = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);
        private readonly byte[] _languageNameBytes = StringEncoding.UTF8.GetBytes("language_name");
        private readonly byte[] _languageNameValueBytes = StringEncoding.UTF8.GetBytes(".NET");
        private readonly byte[] _languageVersionBytes = StringEncoding.UTF8.GetBytes("language_version");
        private readonly byte[] _languageVersionValueBytes = StringEncoding.UTF8.GetBytes(FrameworkDescription.Instance.ProductVersion);
        private readonly byte[] _languageInterpreterBytes = StringEncoding.UTF8.GetBytes("language_interpreter");
        private readonly byte[] _languageInterpreterValueBytes = StringEncoding.UTF8.GetBytes(FrameworkDescription.Instance.Name);
        private readonly byte[] _tracerVersionBytes = StringEncoding.UTF8.GetBytes("tracer_version");
        private readonly byte[] _tracerVersionValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.AssemblyVersion);
        // .
        private readonly byte[] _environmentBytes = StringEncoding.UTF8.GetBytes("env");
        private readonly byte[] _environmentValueBytes;
        private readonly byte[] _hostnameBytes = StringEncoding.UTF8.GetBytes("hostname");
        private readonly byte[] _hostnameValueBytes = StringEncoding.UTF8.GetBytes(HostMetadata.Instance.Hostname);
        private readonly byte[] _appVersionBytes = StringEncoding.UTF8.GetBytes("app_version");
        private readonly byte[] _appVersionValueBytes;
        // .
        private readonly byte[] _eventsBytes = StringEncoding.UTF8.GetBytes("events");

        public CIEventMessagePackFormatter()
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

            var environment = Tracer.Instance.Settings.Environment;
            if (environment is not null)
            {
                _environmentValueBytes = StringEncoding.UTF8.GetBytes(environment);
            }
            else
            {
                _environmentValueBytes = null;
            }

            var serviceVersion = Tracer.Instance.Settings.ServiceVersion;
            if (serviceVersion is not null)
            {
                _appVersionValueBytes = StringEncoding.UTF8.GetBytes(serviceVersion);
            }
            else
            {
                _appVersionValueBytes = null;
            }
        }

        public override int Serialize(ref byte[] bytes, int offset, IEnumerable<IEvent> value, IFormatterResolver formatterResolver)
        {
            if (value is null)
            {
                return 0;
            }

            var originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 3);

            // .

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, VersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, Version100ValueBytes);

            // .

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metadataBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 9);
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
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageVersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageVersionValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageInterpreterBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageInterpreterValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _tracerVersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _tracerVersionValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentBytes);
            if (_environmentValueBytes is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentValueBytes);
            }
            else
            {
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _hostnameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _hostnameValueBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _appVersionBytes);
            if (_appVersionValueBytes is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _appVersionValueBytes);
            }
            else
            {
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
            }

            // .

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

                    if (eventItem is TestEvent testEvent)
                    {
                        offset += formatterResolver.GetFormatter<TestEvent>().Serialize(ref bytes, offset, testEvent, formatterResolver);
                    }
                    else if (eventItem is SpanEvent spanEvent)
                    {
                        offset += formatterResolver.GetFormatter<SpanEvent>().Serialize(ref bytes, offset, spanEvent, formatterResolver);
                    }
                    else if (eventItem is TraceEvent traceEvent)
                    {
                        offset += formatterResolver.GetFormatter<TraceEvent>().Serialize(ref bytes, offset, traceEvent, formatterResolver);
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
