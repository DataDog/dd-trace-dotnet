using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.Agent;

namespace Benchmarks.Trace
{
    class DummyAgentWriter : IAgentWriter
    {
        static readonly Task<bool> _pingTask = Task.FromResult(true);

        public Task FlushAndCloseAsync()
        {
            return Task.CompletedTask;
        }

        public Task FlushTracesAsync()
        {
            return Task.CompletedTask;
        }

        public void OverrideApi(IApi api)
        {
        }

        public Task<bool> Ping()
        {
            return _pingTask;
        }

        public void WriteTrace(Span[] trace)
        {
        }
    }
}
