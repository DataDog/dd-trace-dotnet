using System.Collections.Generic;
using OpenTracing;

namespace Datadog.Trace
{
    internal interface IDatadogTracer : ITracer
    {
        string DefaultServiceName { get; }

        bool IsDebugEnabled { get; }

        void Write(List<Span> span);

        ITraceContext GetTraceContext();

        void CloseCurrentTraceContext();
    }
}