using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace.TestUtils
{
    public class MockWriter : IAgentWriter
    {
        public MockWriter()
        {
            Traces = new List<List<Span>>();
        }

        public List<List<Span>> Traces { get; set; }

        public Task FlushAndCloseAsync()
        {
            return Task.FromResult(true);
        }

        public void WriteTrace(List<Span> trace)
        {
            Traces.Add(trace);
        }
    }
}
