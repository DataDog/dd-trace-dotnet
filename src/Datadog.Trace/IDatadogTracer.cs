using System.Collections.Generic;

namespace Datadog.Trace
{
    internal interface IDatadogTracer
    {
        string DefaultServiceName { get; }

        bool IsDebugEnabled { get; }

        AsyncLocalScopeManager ScopeManager { get; }

        void Write(List<Span> span);
    }
}