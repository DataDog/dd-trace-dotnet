using System.Net;
using System.Net.Http;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class HttpClientTests : TestHelper
    {
        private const int AgentPort = 9002;

        public HttpClientTests(ITestOutputHelper output)
            : base("HttpMessageHandler", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTracesWithHttpClient()
        {
            using (var agent = new MockTracerAgent(AgentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(AgentPort, "HttpClient"))
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
            using (var agent = new MockTracerAgent(AgentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(AgentPort, "WebClient"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span");

                foreach (var span in spans)
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
