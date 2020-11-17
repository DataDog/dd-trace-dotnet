#if NETCOREAPP
using System.Net.Http;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpClientResponse : IApiResponse
    {
        private readonly HttpResponseMessage _response;

        public HttpClientResponse(HttpResponseMessage response)
        {
            _response = response;
        }

        public int StatusCode => (int)_response.StatusCode;

        public long ContentLength => _response.Content.Headers.ContentLength ?? -1;

        public void Dispose()
        {
            _response.Dispose();
        }

        public Task<string> ReadAsStringAsync()
        {
            return _response.Content.ReadAsStringAsync();
        }
    }
}
#endif
