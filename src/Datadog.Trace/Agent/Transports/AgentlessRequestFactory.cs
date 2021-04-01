using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports
{
    internal class AgentlessRequestFactory : IApiRequestFactory
    {
        public AgentlessRequestFactory()
        {
            Task.Factory.StartNew(AgentlessInterop.InitializeTraceAgent, TaskCreationOptions.LongRunning);
        }

        public string Info(Uri endpoint) => endpoint.ToString();

        public IApiRequest Create(Uri endpoint)
        {
            return new AgentlessApiRequest();
        }
    }
}
