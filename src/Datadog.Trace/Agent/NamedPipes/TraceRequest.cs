using System.Collections.Generic;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal class TraceRequest
    {
        public Dictionary<string, string> Headers { get; set; }

        public Span[][] Traces { get; set; }
    }
}
