namespace Datadog.Tracer
{
    internal interface ITraceContext
    {
        bool Sampled { get; set; }

        void AddSpan(Span span);

        void CloseSpan(Span span);

        SpanContext GetCurrentSpanContext();
    }
}