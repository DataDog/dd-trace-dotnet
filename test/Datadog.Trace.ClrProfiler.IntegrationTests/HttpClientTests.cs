using System.Linq;
using System.Net;
using System.Net.Http;
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
        public void SubmitsTracesWithHttpClient()
        {
            const int agentPort = 9006;

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agentPort, "HttpClient"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span");

                foreach (var span in spans)
                {
                    Assert.Equal("http.request", span.Name);
                    Assert.Equal("Samples.HttpMessageHandler", span.Service);
                    Assert.Equal(SpanTypes.Http, span.Type);
                    Assert.Equal(nameof(HttpMessageHandler), span.Tags[Tags.InstrumentationName]);
                }
            }
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTracesWithWebClient()
        {
            const int agentPort = 9007;

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agentPort, "WebClient"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span");

                // inspect the top-level span, underlying spans can be HttpMessageHandler in .NET Core
                foreach (var span in spans.Take(1))
                {
                    Assert.Equal("http.request", span.Name);
                    Assert.Equal("Samples.HttpMessageHandler", span.Service);
                    Assert.Equal(SpanTypes.Http, span.Type);
                    Assert.Equal(nameof(WebRequest), span.Tags[Tags.InstrumentationName]);
                }
            }
        }
    }
}
