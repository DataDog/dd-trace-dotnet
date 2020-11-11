using System.Collections.Generic;

namespace Datadog.Trace.Agent.NamedPipes
{
    /// <summary>
    /// A fake HTTP class
    /// </summary>
    internal class TraceRequest
    {
        /// <summary>
        /// Gets or sets the path to use on the server.
        /// Determines the schema version for the traces.
        /// </summary>
        public string Path { get; set; } = "/v0.4/traces";

        /// <summary>
        /// Gets or sets the HTTP Version.
        /// Version 1.0 of HTTP is "safe".
        /// </summary>
        public string Version { get; set; } = "1.0";

        public string Host { get; set; } = "localhost";

        public string Method { get; set; } = "POST";

        public List<KeyValuePair<string, string>> Headers { get; set; } = new List<KeyValuePair<string, string>>();

        public Span[][] Traces { get; set; } = new Span[0][];
    }
}
