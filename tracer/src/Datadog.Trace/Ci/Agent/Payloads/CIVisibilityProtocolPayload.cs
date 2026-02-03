// <copyright file="CIVisibilityProtocolPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.IO;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.Payloads;

internal abstract class CIVisibilityProtocolPayload : EventPlatformPayload
{
    private readonly EventsBuffer<IEvent> _events;
    private readonly IFormatterResolver _formatterResolver;

    public CIVisibilityProtocolPayload(TestOptimizationSettings settings, IFormatterResolver formatterResolver)
        : base(settings)
    {
        _formatterResolver = formatterResolver;

        // Because we don't know the size of the events array envelope we left 500kb for that.
        _events = new EventsBuffer<IEvent>(settings.MaximumAgentlessPayloadSize - (500 * 1024), _formatterResolver);
    }

    public override bool HasEvents => _events.Count > 0;

    public override int Count => _events.Count;

    internal EventsBuffer<IEvent> Events => _events;

    public int TestEventsCount { get; private set; }

    public int TestSuiteEventsCount { get; private set; }

    public int TestModuleEventsCount { get; private set; }

    public int TestSessionEventsCount { get; private set; }

    public int SpanEventsCount { get; private set; }

    public override bool TryProcessEvent(IEvent @event)
    {
        var sw = RefStopwatch.Create();
        var success = _events.TryWrite(@event);
        if (success)
        {
            if (@event is TestEvent)
            {
                TestEventsCount++;
            }
            else if (@event is TestSuiteEvent)
            {
                TestSuiteEventsCount++;
            }
            else if (@event is TestModuleEvent)
            {
                TestModuleEventsCount++;
            }
            else if (@event is TestSessionEvent)
            {
                TestSessionEventsCount++;
            }
            else if (@event is EventModel.SpanEvent)
            {
                SpanEventsCount++;
            }
        }

        TelemetryFactory.Metrics.RecordDistributionCIVisibilityEndpointEventsSerializationMs(TelemetryEndpoint, sw.ElapsedMilliseconds);
        return success;
    }

    public override void Reset() => _events.Clear();

    public byte[] ToArray() => MessagePackSerializer.Serialize(this, _formatterResolver);

    public int WriteTo(Stream stream)
    {
        var buffer = MessagePackSerializer.SerializeUnsafe(this, _formatterResolver);
        stream.Write(buffer.Array!, buffer.Offset, buffer.Count);
        return buffer.Count;
    }
}
