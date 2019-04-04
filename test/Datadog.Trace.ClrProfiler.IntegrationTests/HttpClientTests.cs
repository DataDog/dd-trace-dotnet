using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class HttpClientTests : TestHelper
    {
        public HttpClientTests(ITestOutputHelper output)
            : base("HttpMessageHandler", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void HttpClient()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, $"HttpClient Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span");

                var traceId = GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);

                var firstSpan = spans.First();
                Assert.Equal("http.request", firstSpan.Name);
                Assert.Equal("Samples.HttpMessageHandler-http-client", firstSpan.Service);
                Assert.Equal(SpanTypes.Http, firstSpan.Type);
                Assert.Equal(nameof(HttpMessageHandler), firstSpan.Tags[Tags.InstrumentationName]);

                var lastSpan = spans.Last();
                Assert.Equal(lastSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
                Assert.Equal(lastSpan.SpanId.ToString(CultureInfo.InvariantCulture), parentSpanId);
            }
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void HttpClient_TracingDisabled()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, $"HttpClient TracingDisabled Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(1, 500);
                Assert.Equal(0, spans.Count);

                var traceId = GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);
                var tracingEnabled = GetHeader(processResult.StandardOutput, HttpHeaderNames.TracingEnabled);

                Assert.Null(traceId);
                Assert.Null(parentSpanId);
                Assert.Equal("false", tracingEnabled);
            }
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void WebClient()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, $"WebClient Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span");

                var traceId = GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);

                // inspect the top-level span, underlying spans can be HttpMessageHandler in .NET Core
                var firstSpan = spans.First();
                Assert.Equal("http.request", firstSpan.Name);
                Assert.Equal("Samples.HttpMessageHandler-http-client", firstSpan.Service);
                Assert.Equal(SpanTypes.Http, firstSpan.Type);
                Assert.Equal(nameof(WebRequest), firstSpan.Tags[Tags.InstrumentationName]);

                var lastSpan = spans.Last();
                Assert.Equal(lastSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
                Assert.Equal(lastSpan.SpanId.ToString(CultureInfo.InvariantCulture), parentSpanId);
            }
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void WebClient_TracingDisabled()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, $"WebClient TracingDisabled Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(1, 500);
                Assert.Equal(0, spans.Count);

                var traceId = GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);
                var tracingEnabled = GetHeader(processResult.StandardOutput, HttpHeaderNames.TracingEnabled);

                Assert.Null(traceId);
                Assert.Null(parentSpanId);
                Assert.Equal("false", tracingEnabled);
            }
        }

        private string GetHeader(string stdout, string name)
        {
            var pattern = $@"^\[HttpListener\] request header: {name}=(\w+)\r?$";
            var match = Regex.Match(stdout, pattern, RegexOptions.Multiline);

            return match.Success
                       ? match.Groups[1].Value
                       : null;
        }
    }
}
