using System.Linq;
using Datadog.Core.Tools;
using Datadog.Trace.Configuration;
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

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTracesWithNetStandard(bool enableCallTarget, bool enableInlining)
        {
            SetCallTargetSettings(enableCallTarget, enableInlining);

            // Note: The automatic instrumentation currently bails out on the generic wrappers.
            // Once this is implemented, this will add another 1 group for the direct assembly reference
            // and another 1 group for the netstandard assembly reference
#if NET452
            var expectedSpanCount = 50; // 7 queries * 7 groups + 1 internal query
#else
            var expectedSpanCount = 78; // 7 queries * 11 groups + 1 internal query
#endif

            if (enableCallTarget)
            {
#if NET452
                expectedSpanCount = 62;
#else
                expectedSpanCount = 97;
#endif
            }

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

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [Trait("Category", "EndToEnd")]
        public void SpansDisabledByAdoNetExcludedTypes(bool enableCallTarget, bool enableInlining)
        {
            SetCallTargetSettings(enableCallTarget, enableInlining);

            var totalSpanCount = 21;

            const string dbType = "mysql";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable(ConfigurationKeys.AdoNetExcludedTypes, "System.Data.SqlClient.SqlCommand;Microsoft.Data.SqlClient.SqlCommand;MySql.Data.MySqlClient.MySqlCommand;Npgsql.NpgsqlCommand");

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);
                Assert.NotEmpty(spans);
                Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            }
        }
    }
}
