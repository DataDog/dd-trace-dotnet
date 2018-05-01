using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace
{
    internal interface IAgentWriter
    {
        void WriteTrace(List<Span> trace);

        Task FlushAndCloseAsync();
    }
}
