using System;

namespace Datadog.Trace
{
    internal interface ITraceContext
    {
        void AddSpan(SpanBase span);

        void CloseSpan(SpanBase span);

        DateTimeOffset UtcNow();
    }
}