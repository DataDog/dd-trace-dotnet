using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datadog.Tracer
{
    internal class AgentWriter : IAgentWriter
    {
        private readonly AgentWriterBuffer<List<Span>> _tracesBuffer = new AgentWriterBuffer<List<Span>>(1000);
        private readonly AgentWriterBuffer<ServiceInfo> _servicesBuffer = new AgentWriterBuffer<ServiceInfo>(100);
        private readonly IApi _api;
        private readonly Task _flushTask;

        public AgentWriter(IApi api)
        {
            _api = api;
            _flushTask = Task.Run(FlushTracesTaskLoop);
        }

        public void WriteServiceInfo(ServiceInfo serviceInfo)
        {
            _servicesBuffer.Push(serviceInfo);
        }

        public void WriteTrace(List<Span> trace)
        {
            _tracesBuffer.Push(trace);
        }

        public async Task FlushTracesTaskLoop()
        {
            while (true)
            {
                try
                {
                    // TODO:bertrand trigger on process exit too
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    var traces = _tracesBuffer.Pop();
                    if (traces.Any())
                    {
                        await _api.SendTracesAsync(traces);
                    }
                    var services = _servicesBuffer.Pop();
                    if (services.Any())
                    {
                        // TODO:bertrand batch these calls
                        await Task.WhenAll(services.Select(_api.SendServiceAsync));
                    }
                }
                catch
                {
                    // TODO: log errors
                }
            }
        }
    }
}
