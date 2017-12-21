namespace Datadog.Trace
{
    public interface IDDSpanContext
    {
        ulong TraceId { get; }

        ulong SpanId { get; }

        ulong ParentId { get; }
    }
}