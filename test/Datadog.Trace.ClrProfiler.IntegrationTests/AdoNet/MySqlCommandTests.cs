using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class MySqlCommandTests : TestHelper
    {
        public MySqlCommandTests(ITestOutputHelper output)
            : base("MySql", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(count: 12, operationName: "sql-server.query");
                Assert.True(spans.Count > 0, "expected at least one span");

                foreach (var span in spans)
                {
                    Assert.Equal("sql-server.query", span.Name);
                    Assert.Equal("Samples.MySql-sql-server", span.Service);
                    Assert.Equal(SpanTypes.Sql, span.Type);
                    Assert.Equal("mysql", span.Tags[Tags.DbType]);
                }
            }
        }
    }
}
