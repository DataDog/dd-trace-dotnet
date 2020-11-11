using System.Collections.Generic;

namespace Datadog.Trace.Agent
{
    /// <summary>
    /// A fake HTTP class
    /// </summary>
    internal class TraceRequest
    {
        public string Path { get; set; } = "/v0.4/traces";

        public string Host { get; set; } = "localhost:8126";

        public string Method { get; set; } = "POST";

        public string Version { get; set; } = "1.0";

        public List<KeyValuePair<string, string>> Headers { get; set; } = new List<KeyValuePair<string, string>>();

        public Span[][] Traces { get; set; } = new Span[0][];
    }
}
