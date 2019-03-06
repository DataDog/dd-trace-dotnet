using System;

namespace Datadog.Trace
{
    internal interface ITraceContext
    {
        void AddSpan(Span span);

        void CloseSpan(Span span);

        DateTimeOffset UtcNow { get; }
    }
}