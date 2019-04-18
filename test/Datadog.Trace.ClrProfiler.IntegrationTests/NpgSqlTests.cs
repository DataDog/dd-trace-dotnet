using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class NpgsqlTests : TestHelper
    {
        public NpgsqlTests(ITestOutputHelper output)
            : base("Npgsql", output)
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

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span");
                foreach (var span in spans)
                {
                    Assert.Equal("postgres.query", span.Name);
                    Assert.Equal("Samples.Npgsql-postgres", span.Service);
                    Assert.Equal(SpanTypes.Sql, span.Type);
                }
            }
        }
    }
}
