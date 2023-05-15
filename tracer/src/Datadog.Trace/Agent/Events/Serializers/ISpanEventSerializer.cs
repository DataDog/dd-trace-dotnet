using System;
using System.Buffers;

#nullable enable

namespace Datadog.Trace.Agent.Events.Serializers;

public interface ISpanEventSerializer
{
    void Serialize(Memory<SpanEvent> spanEvents, IBufferWriter<byte> writer);
}
