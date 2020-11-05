using System.Threading.Tasks;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal class NamedPipesResponse : IApiResponse
    {
        public int StatusCode { get; set; }

        public long ContentLength { get; set; }

        public string Content { get; set; }

        public Task<string> ReadAsStringAsync()
        {
            return Task.FromResult(Content);
        }

        public void Dispose()
        {
            // no-op
        }
    }
}
