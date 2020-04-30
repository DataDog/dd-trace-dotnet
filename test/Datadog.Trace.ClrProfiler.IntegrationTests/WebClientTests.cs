using System.Globalization;
using System.Linq;
using System.Net;
using Datadog.Core.Tools;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class WebClientTests : TestHelper
    {
        public WebClientTests(ITestOutputHelper output)
            : base("HttpClientDriver", output)
        {
            SetEnvironmentVariable("DD_TRACE_DOMAIN_NEUTRAL_INSTRUMENTATION", "true");
            SetEnvironmentVariable("DD_HttpSocketsHandler_ENABLED", "true");
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

                var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);

                // inspect the top-level span, underlying spans can be HttpClient in .NET Core
                var firstSpan = spans.First();
                Assert.Equal("WebClientRequest", firstSpan.Name);
                Assert.Equal("Samples.HttpClientDriver", firstSpan.Service);
                Assert.Equal(SpanTypes.Web, firstSpan.Type);
                Assert.Equal(nameof(WebRequest), firstSpan.Tags[Tags.InstrumentationName]);

                var lastSpan = spans.Last();
                Assert.Equal(lastSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
                Assert.Equal(lastSpan.SpanId.ToString(CultureInfo.InvariantCulture), parentSpanId);
            }
        }

        [Fact(Skip = "Until WebClient instrumentation is available")]
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

                var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);
                var tracingEnabled = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TracingEnabled);

                Assert.Null(traceId);
                Assert.Null(parentSpanId);
                Assert.Equal("false", tracingEnabled);
            }
        }
    }
}
