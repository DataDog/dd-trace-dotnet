using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class MissingDotnetTracerHomeEnvTest : TestHelper
    {
        private static readonly List<string> EnvironmentVariablesToUnset = new List<string>()
        {
            "DD_DOTNET_TRACER_HOME"
        };

        public MissingDotnetTracerHomeEnvTest(ITestOutputHelper output)
            : base("HttpClient.MissingDotnetTracerHomeEnv", output, environmentVariablesToUnset: EnvironmentVariablesToUnset)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTraces()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(1, 500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var firstSpan = spans.First();
                Assert.Equal("http.request", firstSpan.Name);
                Assert.Equal("Samples.HttpClient.MissingDotnetTracerHomeEnv-http-client", firstSpan.Service);
                Assert.Equal(SpanTypes.Http, firstSpan.Type);
                Assert.Equal(nameof(HttpMessageHandler), firstSpan.Tags[Tags.InstrumentationName]);
            }
        }
    }
}
