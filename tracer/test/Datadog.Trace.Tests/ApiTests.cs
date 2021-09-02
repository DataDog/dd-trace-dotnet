// <copyright file="ApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class ApiTests
    {
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

        [Fact]
        public async Task ExtractAgentVersionHeader()
        {
            const string agentVersion = "1.2.3";

            var tracer = new Mock<IDatadogTracer>();

            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);
            responseMock.Setup(x => x.GetHeader(AgentHttpHeaderNames.AgentVersion)).Returns(agentVersion);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>())).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);

            var api = new Api(new Uri("http://127.0.0.1:1234"), apiRequestFactory: factoryMock.Object, statsd: null, tracer: tracer.Object);

            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1);

            tracer.VerifySet(t => t.AgentVersion = agentVersion, Times.Once);
        }
    }
}
