#if (NET452 || NET461)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class SqlServerMultipleAppDomainsTests : TestHelper
    {
        public SqlServerMultipleAppDomainsTests(ITestOutputHelper output)
            : base("SqlServer.MultipleAppDomains", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTraces()
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(5);
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
