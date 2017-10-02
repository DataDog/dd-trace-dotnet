using OpenTracing;
using System.Collections.Generic;

namespace Datadog.Tracer
{
    internal interface IDatadogTracer : ITracer
    {
        string DefaultServiceName { get; }

        void Write(List<Span> span);

        ITraceContext GetTraceContext();
    }
}