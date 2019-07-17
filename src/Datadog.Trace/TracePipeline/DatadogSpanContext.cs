using System;

namespace Datadog.Trace
{
    internal class DatadogSpanContext
    {
        public int TraceId { get; set; }

        public int ParentSpanId { get; set; }

        public int SpanId { get; set; }

        public Guid UniqueId { get; set; }

        public object Span { get; set; }

        public bool IsFinished { get; set; }

        public bool AlivePastTimeToLive { get; set; }

        public DateTime TimeToForceFlush { get; set; } = DateTime.UtcNow.AddMinutes(10);

        public TimeSpan TimeToLiveAfterTraceClose { get; set; } = TimeSpan.FromMinutes(2);
    }
}
