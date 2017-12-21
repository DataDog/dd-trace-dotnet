using System;

namespace Datadog.Trace
{
    internal interface ITraceContext
    {
        bool Sampled { get; set; }

        string DefaultServiceName { get; }

        void AddSpan(SpanBase span);

        void CloseSpan(SpanBase span);

        SpanContext GetCurrentSpanContext();

        DateTimeOffset UtcNow();
    }
}