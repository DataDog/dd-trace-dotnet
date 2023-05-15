using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.Events.Serializers;

#nullable enable

namespace Datadog.Trace.Agent.Events.Writers;

public class StreamSpanEventWriter : ISpanEventWriter
{
    private readonly ISpanEventSerializer _serializer;
    private readonly Stream _stream;

    public StreamSpanEventWriter(ISpanEventSerializer serializer, Stream stream)
    {
        _serializer = serializer;
        _stream = stream;
    }

    public async ValueTask WriteAsync(Memory<SpanEvent> spanEvents, CancellationToken cancellationToken)
    {
        // CommunityToolkit.HighPerformance.Buffers.ArrayPoolBufferWriter
        var writer = new ArrayBufferWriter<byte>();
        _serializer.Serialize(spanEvents, writer);

        await _stream.WriteAsync(writer.WrittenMemory, cancellationToken).ConfigureAwait(false);
    }
}
