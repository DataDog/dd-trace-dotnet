// <copyright file="CIVisibilityProtocolPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using Datadog.Trace.Ci.Agent.MessagePack;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal abstract class CIVisibilityProtocolPayload : EventPlatformPayload
    {
        private readonly EventsBuffer<IEvent> _events;
        private readonly IFormatterResolver _formatterResolver;
        private readonly Stopwatch _serializationWatch;

        public CIVisibilityProtocolPayload(CIVisibilitySettings settings, IFormatterResolver formatterResolver = null)
            : base(settings)
        {
            _formatterResolver = formatterResolver ?? CIFormatterResolver.Instance;
            _serializationWatch = new Stopwatch();

            // Because we don't know the size of the events array envelope we left 500kb for that.
            _events = new EventsBuffer<IEvent>(settings.MaximumAgentlessPayloadSize - (500 * 1024), _formatterResolver);
        }

        public override bool HasEvents => _events.Count > 0;

        public override int Count => _events.Count;

        internal EventsBuffer<IEvent> Events => _events;

        public override bool TryProcessEvent(IEvent @event)
        {
            _serializationWatch.Restart();
            var success = _events.TryWrite(@event);
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointEventsSerializationMs(TelemetryEndpoint, _serializationWatch.Elapsed.TotalMilliseconds);
            return success;
        }

        public override void Reset() => _events.Clear();

        public byte[] ToArray() => MessagePackSerializer.Serialize(this, _formatterResolver);

        public int WriteTo(Stream stream)
        {
            var buffer = MessagePackSerializer.SerializeUnsafe(this, _formatterResolver);
            stream.Write(buffer.Array, buffer.Offset, buffer.Count);
            return buffer.Count;
        }
    }
}
