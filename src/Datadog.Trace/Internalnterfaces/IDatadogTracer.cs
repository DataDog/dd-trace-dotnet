using System.Collections.Generic;
using OpenTracing;

namespace Datadog.Trace
{
    internal interface IDatadogTracer : ITracer
    {
        string DefaultServiceName { get; }

        bool IsDebugEnabled { get; }

        void Write(List<SpanBase> span);

        ITraceContext GetTraceContext();

        void CloseCurrentTraceContext();
    }
}