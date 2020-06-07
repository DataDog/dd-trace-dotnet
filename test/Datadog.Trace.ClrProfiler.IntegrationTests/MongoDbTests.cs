using Datadog.Core.Tools;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class MongoDbTests : TestHelper
    {
        public MongoDbTests(ITestOutputHelper output)
            : base("Samples.MongoDB", output)
        {
            SetServiceVersion("1.0.0");
        }

        [Theory]
        [MemberData(nameof(PackageVersions.MongoDB), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion)
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(3, 500);
                Assert.True(spans.Count >= 3, $"Expecting at least 3 spans, only received {spans.Count}");

                var firstSpan = spans[0];

                // Check for manual trace
                Assert.Equal("Main()", firstSpan.Name);
                Assert.Equal("Samples.MongoDB", firstSpan.Service);
                Assert.Null(firstSpan.Type);

                for (int i = 1; i < spans.Count; i++)
                {
                    if (spans[i].Service == "Samples.MongoDB-mongodb")
                    {
                        Assert.Equal("mongodb.query", spans[i].Name);
                        Assert.Equal(SpanTypes.MongoDb, spans[i].Type);
                        Assert.False(spans[i].Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                    }
                    else
                    {
                        // These are manual traces
                        Assert.Equal("Samples.MongoDB", spans[i].Service);
                        Assert.Equal("1.0.0", spans[i].Tags?.GetValueOrDefault(Tags.Version));
                    }
                }
            }
        }
    }
}
