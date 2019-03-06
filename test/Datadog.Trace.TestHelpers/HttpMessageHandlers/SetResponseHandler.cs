using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers.HttpMessageHandlers
{
    public class SetResponseHandler : DelegatingHandler
    {
        private readonly HttpResponseMessage _response;

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
