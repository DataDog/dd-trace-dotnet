using System;
using System.Buffers;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.Events.Serializers;

#nullable enable

namespace Datadog.Trace.Agent.Events.Writers;

public class HttpSpanEventWriter : ISpanEventWriter
{
    private readonly ISpanEventSerializer _serializer;
    private readonly Uri _uri;
    private readonly HttpClient _client;

    public HttpSpanEventWriter(ISpanEventSerializer serializer, string uri)
        : this(serializer, new Uri(uri))
    {
    }

    public HttpSpanEventWriter(ISpanEventSerializer serializer, Uri uri)
    {
        _serializer = serializer;
        _uri = uri;

        _client = new HttpClient();
    }

    public async ValueTask WriteAsync(Memory<SpanEvent> spanEvents, CancellationToken cancellationToken)
    {
        // CommunityToolkit.HighPerformance.Buffers.ArrayPoolBufferWriter
        var writer = new ArrayBufferWriter<byte>();
        _serializer.Serialize(spanEvents, writer);

        var content = new ReadOnlyMemoryContent(writer.WrittenMemory);
        var response = await _client.PutAsync(_uri, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
