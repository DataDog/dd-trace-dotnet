using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IApi
    {
        Task SendTracesAsync(List<Span> spans);
    }
}
