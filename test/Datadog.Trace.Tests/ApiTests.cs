using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
    public class SetResponseHandler : DelegatingHandler
#pragma warning restore SA1649 // File name must match first type name
#pragma warning restore SA1402 // File may only contain a single class
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

    public class ApiTests
    {
        private Mock<IAgentWriter> _writerMock;
        private Tracer _tracer;

        public ApiTests()
        {
            _writerMock = new Mock<IAgentWriter>();
            _tracer = new Tracer(_writerMock.Object);
        }

        [Fact]
        public async Task SendTraceAsync_200OK_AllGood()
        {
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            };
            var handler = new SetResponseHandler(response);
            var api = new Api(new Uri("http://localhost:1234"), handler);

            await api.SendTracesAsync(new List<List<Span>> { new List<Span> { _tracer.StartSpan("Operation") } });

            Assert.Equal(1, handler.RequestsCount);
        }

        [Fact]
        public async Task SendTracesAsync_500_ErrorIsCaught()
        {
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            };
            var handler = new SetResponseHandler(response);
            var api = new Api(new Uri("http://localhost:1234"), handler);

            await api.SendTracesAsync(new List<List<Span>> { new List<Span> { _tracer.StartSpan("Operation") } });

            Assert.Equal(1, handler.RequestsCount);

            // TODO:bertrand check that it's properly logged
        }
    }
}
