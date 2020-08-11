using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IApi
    {
        public void OverrideBaseEndpoint(Uri uri);

        Task<bool> SendTracesAsync(Span[][] traces);
    }
}
