using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Configuration;
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
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            _tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        }

        [Fact]
        public async Task SendTraceAsync_200OK_AllGood()
        {
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<Span[][]>(), It.IsAny<FormatterResolverWrapper>())).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);

            var api = new Api(new Uri("http://localhost:1234"), apiRequestFactory: factoryMock.Object, statsd: null);

            var span = _tracer.StartSpan("Operation");
            var traces = new[] { new[] { span } };
            await api.SendTracesAsync(traces);

            requestMock.Verify(x => x.PostAsync(It.IsAny<Span[][]>(), It.IsAny<FormatterResolverWrapper>()), Times.Once());
        }

        [Fact]
        public async Task SendTracesAsync_500_ErrorIsCaught()
        {
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(500);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<Span[][]>(), It.IsAny<FormatterResolverWrapper>())).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);

            var api = new Api(new Uri("http://localhost:1234"), apiRequestFactory: factoryMock.Object, statsd: null);

            var sw = new Stopwatch();
            sw.Start();
            var span = _tracer.StartSpan("Operation");
            var traces = new[] { new[] { span } };
            await api.SendTracesAsync(traces);
            sw.Stop();

            requestMock.Verify(x => x.PostAsync(It.IsAny<Span[][]>(), It.IsAny<FormatterResolverWrapper>()), Times.Exactly(5));
            Assert.InRange(sw.ElapsedMilliseconds, 1000, 16000); // should be ~ 3200ms

            // TODO:bertrand check that it's properly logged
        }
    }
}
