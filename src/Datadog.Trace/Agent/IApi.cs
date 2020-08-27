using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IApi
    {
        public void SetBaseEndpoint(Uri baseEndpoint);

        Task<bool> SendTracesAsync(Span[][] traces);
    }
}
