using System;

namespace Datadog.Trace
{
    internal interface ITraceContext
    {
        bool Sampled { get; set; }

        void AddSpan(Span span);

        void CloseSpan(Span span);

        SpanContext GetCurrentSpanContext();

        DateTimeOffset UtcNow();

        string DefaultServiceName { get; }
    }
}