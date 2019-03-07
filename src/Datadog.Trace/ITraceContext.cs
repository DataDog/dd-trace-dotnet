using System;

namespace Datadog.Trace
{
    internal interface ITraceContext
    {
        DateTimeOffset UtcNow { get; }

        void AddSpan(Span span);

        void CloseSpan(Span span);
    }
}
