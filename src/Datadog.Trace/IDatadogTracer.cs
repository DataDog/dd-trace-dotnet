using OpenTracing;
using System.Collections.Generic;

namespace Datadog.Trace
{
    internal interface IDatadogTracer : ITracer
    {
        string DefaultServiceName { get; }

        void Write(List<Span> span);

        ITraceContext GetTraceContext();

        void CloseCurrentTraceContext();

        bool IsDebugEnabled { get; }
    }
}