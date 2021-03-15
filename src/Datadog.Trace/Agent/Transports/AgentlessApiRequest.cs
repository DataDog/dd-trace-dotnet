using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports
{
    internal class AgentlessApiRequest : IApiRequest
    {
        public void AddHeader(string name, string value)
        {
        }

        public unsafe Task<IApiResponse> PostAsync(ArraySegment<byte> traces)
        {
            fixed (byte* buffer = traces.Array)
            {
                AgentlessInterop.SendTraces(buffer + traces.Offset, traces.Count);
            }

            return Task.FromResult<IApiResponse>(new AgentlessApiResponse());
        }
    }
}
