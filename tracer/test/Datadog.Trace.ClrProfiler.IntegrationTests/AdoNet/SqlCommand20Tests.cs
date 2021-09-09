// <copyright file="SqlCommand20Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET452
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class SqlCommand20Tests : TestHelper
    {
        public SqlCommand20Tests(ITestOutputHelper output)
        : base("SqlServer.NetFramework20", output)
        {
            SetServiceVersion("1.0.0");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTraces(bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            var expectedSpanCount = 21; // 7 queries * 3 groups (The group of generic constraint calls is not currently supported)

            if (enableCallTarget)
            {
                expectedSpanCount = 28; // CallTarget support instrumenting a constrained generic caller.
            }

            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.SqlServer.NetFramework20-" + dbType;

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
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SpansDisabledByAdoNetExcludedTypes(bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            var totalSpanCount = 21;

            const string dbType = "sql-server";
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
#endif
