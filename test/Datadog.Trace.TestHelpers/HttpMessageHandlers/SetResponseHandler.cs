using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Tests
{
    public class SetResponseHandler : DelegatingHandler
    {
        private HttpResponseMessage _response;

        public SetResponseHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public int RequestsCount { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestsCount++;
            return Task.FromResult(_response);
        }
    }
}
