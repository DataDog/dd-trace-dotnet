using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection(nameof(WebRequestTests))]
    public class ServiceMappingTests : TestHelper
    {
        public ServiceMappingTests(ITestOutputHelper output)
            : base("WebRequest", output)
        {
            SetEnvironmentVariable("DD_TRACE_SERVICE_MAPPING", "some-trace:not-used,http-client:my-custom-client");
            SetServiceVersion("1.0.0");
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void RenamesService()
        {
            int expectedSpanCount = EnvironmentHelper.IsCoreClr() ? 71 : 27; // .NET Framework automatic instrumentation doesn't cover Async / TaskAsync operations

            var ignoreAsync = EnvironmentHelper.IsCoreClr() ? string.Empty : "IgnoreAsync ";

            const string expectedOperationName = "http.request";
            const string expectedServiceName = "my-custom-client";

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"{ignoreAsync}Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.Equal(expectedSpanCount, spans.Count);

                foreach (var span in spans)
                {
                    Assert.Equal(expectedOperationName, span.Name);
                    Assert.Equal(expectedServiceName, span.Service);
                    Assert.Equal(SpanTypes.Http, span.Type);
                    Assert.Matches("WebRequest|HttpMessageHandler", span.Tags[Tags.InstrumentationName]);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }
            }
        }
    }
}
