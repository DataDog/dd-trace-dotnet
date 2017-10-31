using System.Collections.Generic;

namespace Datadog.Trace
{
    internal interface IAgentWriter
    {
        void WriteTrace(List<Span> trace);

        void WriteServiceInfo(ServiceInfo serviceInfo);
    }
}
