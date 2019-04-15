using System;
using System.Runtime.InteropServices;
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
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTracesWithMongoTags()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(3);
                Assert.True(spans.Count >= 3, "expected at least three spans");

                var firstSpan = spans[0];

                Assert.Equal("Main()", firstSpan.Name);
                Assert.Equal("Samples.MongoDB", firstSpan.Service);
                Assert.Null(firstSpan.Type);

                var secondSpan = spans[1];

                Assert.Equal("sync-calls", secondSpan.Name);
                Assert.Equal("Samples.MongoDB", secondSpan.Service);
                Assert.Null(secondSpan.Type);

                for (int i = 2; i < spans.Count; i++)
                {
                    Assert.Equal("mongodb.query", spans[i].Name);
                    Assert.Equal("Samples.MongoDB-mongodb", spans[i].Service);
                    Assert.Equal(SpanTypes.MongoDb, spans[i].Type);
                }
            }
        }
    }
}
