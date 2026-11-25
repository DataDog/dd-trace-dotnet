// <copyright file="MockDatadogAgentTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class MockDatadogAgentTests
    {
        private const string TelemetryEndpoint = "/telemetry/proxy/api/v2/apmtelemetry";
        private const string SampleJson = "{\"payload\":[]}";

        private readonly ITestOutputHelper _output;

        public MockDatadogAgentTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("gzip")]
        [InlineData("GZIP")]
        [InlineData(null)]
        public void HttpRequestBodyReader_ReadBodyAsText_ReturnsOriginalJson(string contentEncodingHeader)
        {
            using var body = new MemoryStream(EncodeBody(SampleJson, contentEncodingHeader));
            body.Position = 0;

            var text = HttpRequestBodyReader.ReadBodyAsText(body, contentEncodingHeader, Encoding.UTF8);

            text.Should().Be(SampleJson);
            // The reader must leave the stream open on both the gzip and plain paths;
            // HttpListener still owns the request stream.
            body.CanRead.Should().BeTrue();
        }

        [Theory]
        [InlineData("/profiling/v1/input")]
        [InlineData("/v0.4/traces")]
        [InlineData(TelemetryEndpoint)]
        public async Task HttpAgent_SurvivesThrowingHandler_AndSurfacesFailures(string endpoint)
        {
            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            agent.IsReady.Should().BeTrue();

            var handlerException = new InvalidOperationException("handler failed on purpose");
            agent.ProfilerRequestReceived += (_, _) => throw handlerException;
            agent.TracerRequestReceived += (_, _) => throw handlerException;
            agent.TelemetryMetricsRequestReceived += (_, _) => throw handlerException;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            for (var i = 0; i < 2; i++)
            {
                var content = new ByteArrayContent(EncodeBody(SampleJson, "gzip"));
                content.Headers.Add("Content-Encoding", "gzip");
                var response = await client.PostAsync($"http://127.0.0.1:{agent.Port}{endpoint}", content);
                response.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            agent.HandlerExceptions.Should().HaveCount(2);
            agent.HandlerExceptions.Should().OnlyContain(x => x is InvalidOperationException);

            var exception = Assert.Throws<AggregateException>(() => agent.ThrowIfHandlerFailed());
            exception.InnerExceptions.Should().Equal(handlerException, handlerException);
            // Checking failures must not consume them and allow a later check to pass.
            Assert.Throws<AggregateException>(() => agent.ThrowIfHandlerFailed());
        }

        [Fact]
        public async Task HttpAgent_GzipTelemetryHandler_ParsesJson()
        {
            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            agent.IsReady.Should().BeTrue();

            var receivedBody = string.Empty;
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                receivedBody = HttpRequestBodyReader.ReadBodyAsText(ctx.Value.Request);
            };

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var content = new ByteArrayContent(EncodeBody(SampleJson, "gzip"));
            content.Headers.Add("Content-Encoding", "gzip");
            var response = await client.PostAsync($"http://127.0.0.1:{agent.Port}{TelemetryEndpoint}", content);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            receivedBody.Should().Be(SampleJson);
            agent.HandlerExceptions.Should().BeEmpty();
            agent.ThrowIfHandlerFailed();
        }

        private static byte[] EncodeBody(string text, string contentEncodingHeader)
        {
            if (contentEncodingHeader is null)
            {
                return Encoding.UTF8.GetBytes(text);
            }

            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            using (var writer = new StreamWriter(gzip, Encoding.UTF8))
            {
                writer.Write(text);
            }

            return output.ToArray();
        }
    }
}
