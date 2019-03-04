using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

// EFCore targets netstandard2.0, so it requires net461+ or netcoreapp2.0+
#if !NET452

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class SqlServerTests : TestHelper
    {
        private const int AgentPort = 9002;

        public SqlServerTests(ITestOutputHelper output)
            : base("SqlServer", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            using (var agent = new MockTracerAgent(AgentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(AgentPort))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(1);
                Assert.True(spans.Count > 0, "expected at least one span");
                foreach (var span in spans)
                {
                    Assert.Equal("sql-server.query", span.Name);
                    Assert.Equal($"Samples.SqlServer-sql-server", span.Service);
                    Assert.Equal(SpanTypes.Sql, span.Type);
                }
            }
        }
    }
}

#endif
