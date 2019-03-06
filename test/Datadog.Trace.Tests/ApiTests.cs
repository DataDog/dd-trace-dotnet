using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class ApiTests
    {
        private Mock<IAgentWriter> _writerMock;
        private Tracer _tracer;

        public ApiTests()
        {
            _writerMock = new Mock<IAgentWriter>();
            _tracer = new Tracer(_writerMock.Object, null);
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

            var sw = new Stopwatch();
            sw.Start();
            await api.SendTracesAsync(new List<List<Span>> { new List<Span> { _tracer.StartSpan("Operation") } });
            sw.Stop();

            Assert.Equal(5, handler.RequestsCount);
            Assert.InRange(sw.ElapsedMilliseconds, 1500, 10000); // should be ~ 1600ms

            // TODO:bertrand check that it's properly logged
        }
    }
}
