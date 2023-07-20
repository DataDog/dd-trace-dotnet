// <copyright file="ApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class ApiTests
    {
        private const string TracesPath = "/v0.4/traces";
        private const string StatsPath = "/v0.6/stats";

        [Fact]
        public async Task SendTraceAsync_200OK_AllGood()
        {
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack)).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == TracesPath))).Returns(new Uri("http://localhost/traces"));
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == StatsPath))).Returns(new Uri("http://localhost/stats"));

            var api = new Api(apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: null, partialFlushEnabled: false);

            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1, false, 0, 0);

            requestMock.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack), Times.Once());
        }

        [Theory]
        [InlineData(429)]
        [InlineData(413)]
        [InlineData(408)]
        public async Task SendTracesAsync_ShouldNotRetry_ForSpecificResponses(int statusCode)
        {
            // these came from the following Agent PR: https://github.com/DataDog/datadog-agent/pull/17917
            // 429 Too Many Requests used to be sent as a 200 OK, we don't want to retry this request when the agent is already overwhelmed
            // 413 Content Too Large -> no sense in retrying something that will fail again
            // 408 Request Timeout -> sent when agent times out and closes the connection - distinct from the connection timing out
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(statusCode);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack)).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == TracesPath))).Returns(new Uri("http://localhost/traces"));
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == StatsPath))).Returns(new Uri("http://localhost/stats"));

            var api = new Api(apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: null, partialFlushEnabled: false);

            var responseResult = await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1, false, 0, 0);

            requestMock.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack), Times.Once());
            responseResult.Should().Be(false);
        }

        [Fact]
        public async Task SendTracesAsync_500_ErrorIsCaught()
        {
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(500);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack)).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == TracesPath))).Returns(new Uri("http://localhost/traces"));
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == StatsPath))).Returns(new Uri("http://localhost/stats"));

            var api = new Api(apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: null, partialFlushEnabled: false);

            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1, false, 0, 0);

            requestMock.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack), Times.Exactly(5));
        }

        [Fact]
        public async Task SendStatsAsync_200OK_AllGood()
        {
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack)).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == TracesPath))).Returns(new Uri("http://localhost/traces"));
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == StatsPath))).Returns(new Uri("http://localhost/stats"));

            var api = new Api(apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: null, partialFlushEnabled: false);

            var statsBuffer = new StatsBuffer(new ClientStatsPayload());

            await api.SendStatsAsync(statsBuffer, 1);

            requestMock.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack), Times.Once());
        }

        [Fact]
        public async Task SendStatsAsync_500_ErrorIsCaught()
        {
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(500);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack)).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == TracesPath))).Returns(new Uri("http://localhost/traces"));
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == StatsPath))).Returns(new Uri("http://localhost/stats"));

            var api = new Api(apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: null, partialFlushEnabled: false);

            var statsBuffer = new StatsBuffer(new ClientStatsPayload());

            await api.SendStatsAsync(statsBuffer, 1);

            requestMock.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack), Times.Exactly(5));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task StatsHeader(bool statsComputationEnabled)
        {
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack)).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == TracesPath))).Returns(new Uri("http://localhost/traces"));
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == StatsPath))).Returns(new Uri("http://localhost/stats"));

            var api = new Api(apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: null, partialFlushEnabled: false);

            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1, statsComputationEnabled, 0, 0);

            requestMock.Verify(x => x.AddHeader(AgentHttpHeaderNames.StatsComputation, "true"), statsComputationEnabled ? Times.Once : Times.Never);
        }

        [Fact]
        public async Task ExtractAgentVersionHeaderAndLogsWarning()
        {
            const string agentVersion = "1.2.3";

            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);
            responseMock.Setup(x => x.GetHeader(AgentHttpHeaderNames.AgentVersion)).Returns(agentVersion);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), MimeTypes.MsgPack)).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == TracesPath))).Returns(new Uri("http://localhost/traces"));
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == StatsPath))).Returns(new Uri("http://localhost/stats"));

            var logMock = new Mock<IDatadogLogger>();

            var api = new Api(apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: null, partialFlushEnabled: true, log: logMock.Object);

            // First time should write the warning
            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1, false, 0, 0);
            // Second time, it won't
            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1, false, 0, 0);

            // ReSharper disable ExplicitCallerInfoArgument
            logMock.Verify(
                log => log.Warning(
                    It.Is<string>(s => s.Contains("Partial flush should only be enabled with agent 7.26.0+")),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>()),
                Times.Once);
            // ReSharper restore ExplicitCallerInfoArgument
        }

        [Fact]
        public async Task SetsDefaultSamplingRates()
        {
            var ratesByService = new Dictionary<string, float> { { "test", 0.5f } };
            var responseContent = new { rates_by_service = ratesByService };
            var serializedResponse = JsonConvert.SerializeObject(responseContent);

            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);
            responseMock.Setup(x => x.GetStreamAsync()).Returns(() => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(serializedResponse))));
            responseMock.Setup(x => x.ContentEncoding).Returns(() => Encoding.UTF8);
            responseMock.Setup(x => x.ContentLength).Returns(serializedResponse.Length);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>())).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == TracesPath))).Returns(new Uri("http://localhost/traces"));
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == StatsPath))).Returns(new Uri("http://localhost/stats"));

            var ratesWereSet = false;
            Action<Dictionary<string, float>> updateSampleRates = _ => ratesWereSet = true;
            var api = new Api(apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: updateSampleRates, partialFlushEnabled: false);

            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1, false, 0, 0);
            ratesWereSet.Should().BeTrue();
        }

        [Theory]
        [InlineData("7.25.0", true, true)] // Old agent, partial flush enabled
        [InlineData("7.25.0", false, false)] // Old agent, partial flush disabled
        [InlineData("7.26.0", true, false)] // New agent, partial flush enabled
        [InlineData("invalid version", true, true)] // Version check fail, partial flush enabled
        [InlineData("invalid version", false, false)] // Version check fail, partial flush disabled
        [InlineData("", true, true)] // Version check fail, partial flush enabled
        [InlineData("", false, false)] // Version check fail, partial flush disabled
        public void LogPartialFlushWarning(string agentVersion, bool partialFlushEnabled, bool expectedResult)
        {
            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>())).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == TracesPath))).Returns(new Uri("http://localhost/traces"));
            factoryMock.Setup(x => x.GetEndpoint(It.Is<string>(s => s == StatsPath))).Returns(new Uri("http://localhost/stats"));

            var api = new Api(factoryMock.Object, statsd: null, updateSampleRates: null, partialFlushEnabled: partialFlushEnabled);

            // First call depends on the parameters of the test
            api.LogPartialFlushWarningIfRequired(agentVersion).Should().Be(expectedResult);

            // Second call should always be false
            api.LogPartialFlushWarningIfRequired(agentVersion).Should().BeFalse();
        }
    }
}
