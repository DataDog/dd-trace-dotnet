using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace
{
    internal class MemorySpanWriter : ISpanWriter
    {
        private readonly List<List<Span>> _traces = new List<List<Span>>();

        public IReadOnlyList<IReadOnlyList<Span>> Traces => _traces;

        public void WriteTrace(List<Span> trace)
        {
            lock (_traces)
            {
                _traces.Add(trace);
            }
        }

        public Task FlushAndCloseAsync()
        {
#if NET45
            return Task.FromResult<object>(null);
#else
            return Task.CompletedTask;
#endif
        }
    }
}
