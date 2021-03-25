using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
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
            var writerMock = new Mock<ITraceWriter>();
            var samplerMock = new Mock<ISampler>();

            _tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        }

        [Fact]
        public async Task SendTraceAsync_200OK_AllGood()
        {
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>())).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);

            var api = new Api(new Uri("http://127.0.0.1:1234"), apiRequestFactory: factoryMock.Object, statsd: null);

            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1);

            requestMock.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>()), Times.Once());
        }

        [Fact]
        public async Task SendTracesAsync_500_ErrorIsCaught()
        {
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(500);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>())).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);

            var api = new Api(new Uri("http://127.0.0.1:1234"), apiRequestFactory: factoryMock.Object, statsd: null);

            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1);

            requestMock.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>()), Times.Exactly(5));
        }
    }
}
