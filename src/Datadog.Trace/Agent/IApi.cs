using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IApi
    {
        Task<bool> SendTracesAsync(Span[][] traces);

        Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces);
    }
}
