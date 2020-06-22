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
            SetEnvironmentVariable("DD_HttpSocketsHandler_ENABLED", "true");
            SetServiceVersion("1.0.0");
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void HttpClient()
        {
            int expectedSpanCount = EnvironmentHelper.IsCoreClr() ? 2 : 1;
            const string expectedOperationName = "http.request";
            const string expectedServiceName = "Samples.HttpMessageHandler-http-client";

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"HttpClient Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.True(spans.Count >= expectedSpanCount, $"Expected at least {expectedSpanCount} span, only received {spans.Count}" + System.Environment.NewLine + "IMPORTANT: Make sure Datadog.Trace.ClrProfiler.Managed.dll and its dependencies are in the GAC.");

                foreach (var span in spans)
                {
                    Assert.Equal(expectedOperationName, span.Name);
                    Assert.Equal(expectedServiceName, span.Service);
                    Assert.Equal(SpanTypes.Http, span.Type);
                    Assert.Equal(nameof(HttpMessageHandler), span.Tags[Tags.InstrumentationName]);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }

                var firstSpan = spans.First();
                var traceId = GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);

                Assert.Equal(firstSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
                Assert.Equal(firstSpan.SpanId.ToString(CultureInfo.InvariantCulture), parentSpanId);
            }
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void HttpClient_TracingDisabled()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"HttpClient TracingDisabled Port={httpPort}"))
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
            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"WebClient Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span." + System.Environment.NewLine + "IMPORTANT: Make sure Datadog.Trace.ClrProfiler.Managed.dll and its dependencies are in the GAC.");

                var traceId = GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);

                // inspect the top-level span, underlying spans can be HttpMessageHandler in .NET Core
                var firstSpan = spans.First();
                Assert.Equal("http.request", firstSpan.Name);
                Assert.Equal("Samples.HttpMessageHandler-http-client", firstSpan.Service);
                Assert.Equal(SpanTypes.Http, firstSpan.Type);
                Assert.Equal(nameof(WebRequest), firstSpan.Tags?[Tags.InstrumentationName]);
                Assert.False(firstSpan.Tags?.ContainsKey(Tags.Version), "Http client span should not have service version tag.");

                var lastSpan = spans.Last();
                Assert.Equal(lastSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
                Assert.Equal(lastSpan.SpanId.ToString(CultureInfo.InvariantCulture), parentSpanId);
                Assert.False(lastSpan.Tags?.ContainsKey(Tags.Version), "Http client span should not have service version tag.");
            }
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void WebClient_TracingDisabled()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"WebClient TracingDisabled Port={httpPort}"))
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
