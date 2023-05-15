using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Datadog.Trace.Agent.Events.Writers;

public class NullSpanEventWriter : ISpanEventWriter
{
    public ValueTask WriteAsync(Memory<SpanEvent> spanEvents, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
