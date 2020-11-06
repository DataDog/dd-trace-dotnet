using System.Collections.Generic;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal class TraceRequest
    {
        public List<KeyValuePair<string, string>> Headers { get; set; } = new List<KeyValuePair<string, string>>();

        public Span[][] Traces { get; set; }
    }
}
