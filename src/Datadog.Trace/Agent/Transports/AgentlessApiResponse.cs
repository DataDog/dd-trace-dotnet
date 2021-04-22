using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports
{
    internal class AgentlessApiResponse : IApiResponse
    {
        public int StatusCode => 200;

        public long ContentLength => 0;

        public Task<string> ReadAsStringAsync()
        {
            // TODO
            return Task.FromResult(string.Empty);
        }

        public string GetHeader(string headerName)
        {
            // TODO
            return null;
        }

        public void Dispose()
        {
        }
    }
}
