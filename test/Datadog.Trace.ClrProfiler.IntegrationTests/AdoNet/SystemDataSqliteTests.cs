#if !NET452
using System.Collections.Generic;
using System.Linq;
using Datadog.Core.Tools;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class SystemDataSqliteTests : TestHelper
    {
        public SystemDataSqliteTests(ITestOutputHelper output)
            : base("SQLite.Core", output)
        {
            SetServiceVersion("1.0.0");
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTracesWithNetStandard(bool enableCallTarget, bool enableInlining)
        {
            SetCallTargetSettings(enableCallTarget, enableInlining);

            var expectedSpanCount = 91;

            const string dbType = "sqlite";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.SQLite.Core-" + dbType;

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
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public void SpansDisabledByAdoNetExcludedTypes(bool enableCallTarget, bool enableInlining)
        {
            SetCallTargetSettings(enableCallTarget, enableInlining);

            var totalSpanCount = 21;

            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable(ConfigurationKeys.AdoNetExcludedTypes, "System.Data.SQLite.SQLiteCommand;Microsoft.Data.Sqlite.SqliteCommand");

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
#endif
