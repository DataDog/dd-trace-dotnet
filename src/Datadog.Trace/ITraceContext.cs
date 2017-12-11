using System;

namespace Datadog.Trace
{
    internal interface ITraceContext
    {
        bool Sampled { get; set; }

        string DefaultServiceName { get; }

        void AddSpan(Span span);

        void CloseSpan(Span span);

        SpanContext GetCurrentSpanContext();

        DateTimeOffset UtcNow();
    }
}