using System;
using System.Collections.Generic;

namespace Datadog.Trace
{
    internal interface IDatadogTracer
    {
        string DefaultServiceName { get; }

        bool IsDebugEnabled { get; }

        AsyncLocalScopeManager ScopeManager { get; }

        Span StartSpan(string operationName, SpanContext childOf, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope);

        void Write(List<Span> span);
    }
}
