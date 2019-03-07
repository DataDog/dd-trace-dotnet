using System;
using System.Collections.Generic;
using Datadog.Trace.Sampling;

namespace Datadog.Trace
{
    internal interface IDatadogTracer
    {
        string DefaultServiceName { get; }

        bool IsDebugEnabled { get; }

        AsyncLocalScopeManager ScopeManager { get; }

        ISampler Sampler { get; }

        Span StartSpan(string operationName, SpanContext childOf, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope);

        void Write(List<Span> span);
    }
}
