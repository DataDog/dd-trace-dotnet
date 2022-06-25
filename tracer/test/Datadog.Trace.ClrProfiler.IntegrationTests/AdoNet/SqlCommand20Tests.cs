// <copyright file="SqlCommand20Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Linq;
using Datadog.Trace.Configuration;
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

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTraces()
        {
            const int expectedSpanCount = 28; // 7 queries * 3 groups + CallTarget support instrumenting a constrained generic caller.
            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.SqlServer.NetFramework20-" + dbType;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);

            Assert.Equal(expectedSpanCount, spans.Count);

            foreach (var span in spans)
            {
                var result = span.IsSqlClient();
                Assert.True(result.Success, result.ToString());

                Assert.Equal(expectedServiceName, span.Service);
                Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.SqlClient);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void IntegrationDisabled()
        {
            const int totalSpanCount = 21;
            const string expectedOperationName = "sql-server.query";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.SqlClient)}_ENABLED", "false");

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            telemetry.AssertIntegrationDisabled(IntegrationId.SqlClient);
        }
    }
}
#endif
