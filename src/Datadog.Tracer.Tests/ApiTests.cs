using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Datadog.Tracer.Tests
{
    public class SetResponseHandler : DelegatingHandler
    {
        private HttpResponseMessage _response;
        public int RequestsCount { get; set; }

        public SetResponseHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestsCount++;
            return Task.FromResult(_response);
        }
    }

    public class ApiTests
    {
        [Fact]
        public async Task SendServiceAsync_200OK_AllGood()
        {
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            };
            var handler = new SetResponseHandler(response);
            var api = new Api(new Uri("http://localhost:1234"), handler);

            await api.SendServiceAsync(new ServiceInfo());

            Assert.Equal(1, handler.RequestsCount);
        }

        [Fact]
        public async Task SendServiceAsync_500_ErrorIsCaught()
        {
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            };
            var handler = new SetResponseHandler(response);
            var api = new Api(new Uri("http://localhost:1234"), handler);

            await api.SendServiceAsync(new ServiceInfo());

            Assert.Equal(1, handler.RequestsCount);
            // TODO:bertrand check that it's properly logged
        }
    }
}
