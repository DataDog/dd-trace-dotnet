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
            : base("MongoDB", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static System.Collections.Generic.IEnumerable<object[]> GetMongoDb()
        {
            foreach (var item in PackageVersions.MongoDB)
            {
                yield return item.Concat(false, false);
                yield return item.Concat(true, false);
                yield return item.Concat(true, true);
            }
        }

        [Theory]
        [MemberData(nameof(GetMongoDb))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion, bool enableCallTarget, bool enableInlining)
        {
            SetCallTargetSettings(enableCallTarget, enableInlining);

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

                int spansWithResourceName = 0;

                for (int i = 1; i < spans.Count; i++)
                {
                    if (spans[i].Service == "Samples.MongoDB-mongodb")
                    {
                        Assert.Equal("mongodb.query", spans[i].Name);
                        Assert.Equal(SpanTypes.MongoDb, spans[i].Type);
                        Assert.False(spans[i].Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");

                        if (spans[i].Resource != null && spans[i].Resource != "mongodb.query")
                        {
                            spansWithResourceName++;
                            Assert.True(spans[i].Tags?.ContainsKey(Tags.MongoDbQuery), $"No query found on span {spans[i]}");
                        }
                    }
                    else
                    {
                        // These are manual traces
                        Assert.Equal("Samples.MongoDB", spans[i].Service);
                        Assert.True("1.0.0" == spans[i].Tags?.GetValueOrDefault(Tags.Version), spans[i].ToString());
                    }
                }

                Assert.False(spansWithResourceName == 0, "Extraction of the command failed on all spans");
            }
        }
    }
}
