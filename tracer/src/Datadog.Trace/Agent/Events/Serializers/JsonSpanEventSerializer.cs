using System;
using System.Buffers;
using System.Text.Json;

#nullable enable

namespace Datadog.Trace.Agent.Events.Serializers;

public class JsonSpanEventSerializer : ISpanEventSerializer
{
    private readonly JsonSerializerOptions? _options;

    public JsonSpanEventSerializer() : this(null)
    {
    }

    public JsonSpanEventSerializer(JsonSerializerOptions? options)
    {
        _options = options;
    }

    public void Serialize(Memory<SpanEvent> spanEvents, IBufferWriter<byte> writer)
    {
        var jsonWriter = new Utf8JsonWriter(writer);
        JsonSerializer.Serialize(jsonWriter, spanEvents, _options);
    }
}
