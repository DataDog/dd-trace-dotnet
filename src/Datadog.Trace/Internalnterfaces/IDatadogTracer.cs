using System.Collections.Generic;

namespace Datadog.Trace
{
    internal interface IDatadogTracer
    {
        string DefaultServiceName { get; }

        bool IsDebugEnabled { get; }

        IScopeManager ScopeManager { get; }

        void Write(List<Span> span);
    }
}