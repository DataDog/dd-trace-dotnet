using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class NpgsqlCommandTests : TestHelper
    {
        public NpgsqlCommandTests(ITestOutputHelper output)
            : base("Npgsql", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            var expectedSpanCount = 34;
            const string dbType = "postgres";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.Npgsql-" + dbType;

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.Equal(expectedSpanCount, spans.Count);

                foreach (var span in spans)
                {
                    Assert.Equal(expectedOperationName, span.Name);
                    Assert.Equal(expectedServiceName, span.Service);
                    Assert.Equal(SpanTypes.Sql, span.Type);
                    Assert.Equal(dbType, span.Tags[Tags.DbType]);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }
            }
        }
    }
}
