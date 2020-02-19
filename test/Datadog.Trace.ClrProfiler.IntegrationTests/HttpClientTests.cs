using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Datadog.Core.Tools;
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
        [Trait("RunOnWindows", "True")]
        public void HttpClient()
        {
            var agentPortClaim = PortHelper.GetTcpPortClaim();
            var httpPort = PortHelper.GetTcpPortClaim();

            using (var agent = new MockTracerAgent(agentPortClaim))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"HttpClient Port={httpPort.Unlock().Port}"))
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
        [Trait("RunOnWindows", "True")]
        public void HttpClient_TracingDisabled()
        {
            var agentPortClaim = PortHelper.GetTcpPortClaim();
            var httpPort = PortHelper.GetTcpPortClaim();

            using (var agent = new MockTracerAgent(agentPortClaim))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"HttpClient TracingDisabled Port={httpPort.Unlock().Port}"))
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
        [Trait("RunOnWindows", "True")]
        public void WebClient()
        {
            var agentPortClaim = PortHelper.GetTcpPortClaim();
            var httpPort = PortHelper.GetTcpPortClaim();

            using (var agent = new MockTracerAgent(agentPortClaim))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"WebClient Port={httpPort.Unlock().Port}"))
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
        [Trait("RunOnWindows", "True")]
        public void WebClient_TracingDisabled()
        {
            var agentPortClaim = PortHelper.GetTcpPortClaim();
            var httpPort = PortHelper.GetTcpPortClaim();

            using (var agent = new MockTracerAgent(agentPortClaim))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"WebClient TracingDisabled Port={httpPort.Unlock().Port}"))
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
