using System;
using System.Collections.Generic;
using Datadog.Trace.Sampling;

namespace Datadog.Trace
{
    internal interface IDatadogTracer
    {
        string DefaultServiceName { get; }

        bool IsDebugEnabled { get; }

        IScopeManager ScopeManager { get; }

        ISampler Sampler { get; }

        Span StartSpan(string operationName, ISpanContext parent, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope);

        void Write(List<Span> span);
    }
}
