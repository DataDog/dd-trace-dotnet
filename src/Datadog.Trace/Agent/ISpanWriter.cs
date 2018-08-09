using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface ISpanWriter
    {
        void WriteTrace(List<Span> trace);

        Task FlushAndCloseAsync();
    }
}
