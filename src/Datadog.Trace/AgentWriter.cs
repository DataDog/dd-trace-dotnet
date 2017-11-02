using Datadog.Trace.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datadog.Trace
{
    internal class AgentWriter : IAgentWriter
    {
        private static ILog _log = LogProvider.For<AgentWriter>();

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
            var success = _servicesBuffer.Push(serviceInfo);
            if (!success)
            {
                _log.Debug("ServiceInfo buffer is full, dropping it.");
            }
        }

        public void WriteTrace(List<Span> trace)
        {
            var success = _tracesBuffer.Push(trace);
            if (!success)
            {
                _log.Debug("Trace buffer is full, dropping it.");
            }
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
                catch(Exception ex)
                {
                    _log.ErrorException("An unhandled error occurred during the flushing task", ex);
                }
            }
        }
    }
}
