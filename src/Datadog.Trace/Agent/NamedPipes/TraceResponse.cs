using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.NamedPipes
{
    /// <summary>
    /// A fake HTTP class
    /// </summary>
    internal class TraceResponse : IApiResponse
    {
        public TraceRequest Request { get; set; }

        public List<KeyValuePair<string, string>> Headers { get; set; } = new List<KeyValuePair<string, string>>();

        public int StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public long ContentLength { get; set; }

        public string Body { get; set; }

        public Task<string> ReadAsStringAsync()
        {
            return Task.FromResult(Body);
        }

        public void Dispose()
        {
            // no-op
        }
    }
}
