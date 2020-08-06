using Datadog.Core.Tools;
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
            SetServiceVersion("1.0.0");
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
#if NET452
            var expectedSpanCount = 50; // 7 queries * 7 groups + 1 internal query
#elif NET461
            var expectedSpanCount = 64;
#else
            var expectedSpanCount = 47;
#endif

            const string dbType = "mysql";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.MySql-" + dbType;

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

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTracesWithNetStandard()
        {
#if NET452
            var expectedSpanCount = 50; // 7 queries * 7 groups + 1 internal query
#else
            var expectedSpanCount = 78; // 7 queries * 11 groups + 1 internal query
#endif

            const string dbType = "mysql";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.MySql-" + dbType;

            // NOTE: opt into the additional instrumentation of calls into netstandard.dll
            SetEnvironmentVariable("DD_TRACE_NETSTANDARD_ENABLED", "true");

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
