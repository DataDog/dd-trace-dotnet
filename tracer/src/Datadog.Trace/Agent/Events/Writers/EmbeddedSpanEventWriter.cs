using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.Events.Serializers;

#nullable enable

namespace Datadog.Trace.Agent.Events.Writers;

public partial class EmbeddedSpanEventWriter : ISpanEventWriter
{
    private readonly ISpanEventSerializer _serializer;

    public EmbeddedSpanEventWriter(ISpanEventSerializer serializer)
    {
        _serializer = serializer;
    }

    public ValueTask WriteAsync(Memory<SpanEvent> spanEvents, CancellationToken cancellationToken)
    {
        // CommunityToolkit.HighPerformance.Buffers.ArrayPoolBufferWriter
        var writer = new ArrayBufferWriter<byte>();
        _serializer.Serialize(spanEvents, writer);

        unsafe
        {
            using var handle = writer.WrittenMemory.Pin();
            _ = Submit(writer.WrittenCount, handle.Pointer);
        }

        return ValueTask.CompletedTask;
    }

    [LibraryImport("ffi", EntryPoint = "submit")]
    private static unsafe partial uint Submit(nint size, void* ptr);
}
