using System.Collections.Generic;

namespace Datadog.Tracer
{
    internal interface IAgentWriter
    {
        void WriteTrace(List<Span> trace);

        void WriteServiceInfo(ServiceInfo serviceInfo);
    }
}
