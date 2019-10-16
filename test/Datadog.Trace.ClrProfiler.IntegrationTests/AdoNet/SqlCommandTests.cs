using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class SqlCommandTests : TestHelper
    {
        public SqlCommandTests(ITestOutputHelper output)
            : base("SqlServer", output)
        {
        }

        [Theory]
        [MemberData(nameof(PackageVersions.SqlClient), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTraces(string packageVersion)
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                const int expectedSpanCount = 28;
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: "sql-server.query");

                Assert.Equal(expectedSpanCount, spans.Count);

                foreach (var span in spans)
                {
                    Assert.Equal("sql-server.query", span.Name);
                    Assert.Equal("Samples.SqlServer-sql-server", span.Service);
                    Assert.Equal(SpanTypes.Sql, span.Type);
                    Assert.Equal("sql-server", span.Tags[Tags.DbType]);
                }
            }
        }
    }
}
