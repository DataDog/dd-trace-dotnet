using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.HttpMessageHandlers;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class ApiTests
    {
        private readonly Tracer _tracer;

        public ApiTests()
        {
            var writerMock = new Mock<IAgentWriter>();
            var sampler = new SimpleSampler(SamplingPriority.UserKeep);
            _tracer = new Tracer(writerMock.Object, sampler);
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

            var span = _tracer.StartSpan("Operation");
            var traces = new List<List<Span>> { new List<Span> { span } };
            await api.SendTracesAsync(traces);

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
            var span = _tracer.StartSpan("Operation");
            var traces = new List<List<Span>> { new List<Span> { span } };
            await api.SendTracesAsync(traces);
            sw.Stop();

            Assert.Equal(5, handler.RequestsCount);
            Assert.InRange(sw.ElapsedMilliseconds, 1500, 10000); // should be ~ 1600ms

            // TODO:bertrand check that it's properly logged
        }
    }
}
