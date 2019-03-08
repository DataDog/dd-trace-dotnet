using System;

namespace Datadog.Trace
{
    internal interface ITraceContext
    {
        DateTimeOffset UtcNow { get; }

        SamplingPriority? SamplingPriority { get; }

        void AddSpan(Span span);

        void CloseSpan(Span span);

        void LockSamplingPriority();
    }
}
