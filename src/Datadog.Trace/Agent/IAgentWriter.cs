using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IAgentWriter
    {
        void WriteTrace(List<Span> trace);

        Task FlushAndCloseAsync();
    }
}
