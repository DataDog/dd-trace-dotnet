using System.Collections.Specialized;
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
            : base("WebClientDriver", output)
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
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span." + System.Environment.NewLine + "IMPORTANT: Make sure Datadog.Trace.ClrProfiler.Managed.dll and its dependencies are in the GAC.");

                var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);

                int nHttpTypeSpans = 0;
                StringCollection others = new StringCollection();

                foreach (var s in spans)
                {
                    if (s.Type != null)
                    {
                       if (s.Type.Equals("http"))
                       {
                            nHttpTypeSpans++;
                       }
                       else
                        {
                            others.Add(s.Type);
                        }
                    }

                    if (s.Tags != null && s.Tags.Count > 0)
                    {
                        Assert.True(s.Tags[Tags.InstrumentationName].Equals("WebRequest") ||
                                    s.Tags[Tags.InstrumentationName].Equals("WebClient"));
                    }
                }

                Assert.Equal(24, nHttpTypeSpans);

                // inspect the top-level span, underlying spans can be HttpClient in .NET Core
                var firstSpan = spans.First();
                Assert.Equal("WebClientRequest", firstSpan.Name);
                Assert.Equal("Samples.WebClientDriver", firstSpan.Service);

                var lastSpan = spans.Last();
                Assert.Equal(lastSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
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
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"TracingDisabled Port={httpPort}"))
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
