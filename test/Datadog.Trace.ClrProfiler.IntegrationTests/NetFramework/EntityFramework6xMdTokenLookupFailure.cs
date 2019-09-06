using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class EntityFramework6xMdTokenLookupFailure : TestHelper
    {
        public EntityFramework6xMdTokenLookupFailure(ITestOutputHelper output)
            : base("EntityFramework6x.MdTokenLookupFailure", "samples-netframework", output)
        {
        }

        [TargetFrameworkVersionsFact("net452")]
        [Trait("Category", "EndToEnd")]
        public void NoExceptions()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(2);
                Assert.True(spans.Count >= 2, $"Expecting at least 2 spans, only received {spans.Count}");
                foreach (var span in spans)
                {
                    Assert.Equal("sql-server.query", span.Name);
                    Assert.Equal($"Samples.EntityFramework6x.MdTokenLookupFailure-sql-server", span.Service);
                    Assert.Equal(SpanTypes.Sql, span.Type);
                }
            }
        }
    }
}
