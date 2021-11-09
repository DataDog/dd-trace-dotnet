// <copyright file="ApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
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

            var api = new Api(new Uri("http://127.0.0.1:1234"), apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: null, isPartialFlushEnabled: false);

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

            var api = new Api(new Uri("http://127.0.0.1:1234"), apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: null, isPartialFlushEnabled: false);

            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1);

            requestMock.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>()), Times.Exactly(5));
        }

        [Fact]
        public async Task ExtractAgentVersionHeaderAndLogsWarning()
        {
            const string agentVersion = "1.2.3";

            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);
            responseMock.Setup(x => x.GetHeader(AgentHttpHeaderNames.AgentVersion)).Returns(agentVersion);

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>())).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);

            var logMock = new Mock<IDatadogLogger>();

            var api = new Api(new Uri("http://127.0.0.1:1234"), apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: null, isPartialFlushEnabled: true, log: logMock.Object);

            // First time should write the warning
            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1);
            // Second time, it won't
            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1);

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

            var responseMock = new Mock<IApiResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);
            responseMock.Setup(x => x.ReadAsStringAsync()).Returns(Task.FromResult(JsonConvert.SerializeObject(responseContent)));

            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>())).ReturnsAsync(responseMock.Object);

            var factoryMock = new Mock<IApiRequestFactory>();
            factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);

            var ratesWereSet = false;
            Action<Dictionary<string, float>> updateSampleRates = _ => ratesWereSet = true;
            var api = new Api(new Uri("http://127.0.0.1:1234"), apiRequestFactory: factoryMock.Object, statsd: null, updateSampleRates: updateSampleRates, isPartialFlushEnabled: false);

            await api.SendTracesAsync(new ArraySegment<byte>(new byte[64]), 1);
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
            var api = new Api(new Uri("http://127.0.0.1:1234"), apiRequestFactory: null, statsd: null, updateSampleRates: null, isPartialFlushEnabled: partialFlushEnabled);

            // First call depends on the parameters of the test
            api.LogPartialFlushWarningIfRequired(agentVersion).Should().Be(expectedResult);

            // Second call should always be false
            api.LogPartialFlushWarningIfRequired(agentVersion).Should().BeFalse();
        }
    }
}
