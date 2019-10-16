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

        [Theory]
        [MemberData(nameof(PackageVersions.Npgsql), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion)
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                const int expectedSpanCount = 14;
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: "sql-server.query");
                Assert.True(spans.Count > 0, "expected at least one span");

                foreach (var span in spans)
                {
                    Assert.Equal("postgres.query", span.Name);
                    Assert.Equal("Samples.Npgsql-sql-server", span.Service);
                    Assert.Equal(SpanTypes.Sql, span.Type);
                    Assert.Equal("postgres", span.Tags[Tags.DbType]);
                }
            }
        }
    }
}
