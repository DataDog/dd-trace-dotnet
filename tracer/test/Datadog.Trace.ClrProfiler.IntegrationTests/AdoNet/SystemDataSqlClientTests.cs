// <copyright file="SystemDataSqlClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [Trait("RequiresDockerDependency", "true")]
    public class SystemDataSqlClientTests : TestHelper
    {
        public SystemDataSqlClientTests(ITestOutputHelper output)
            : base("SqlServer", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.SystemDataSqlClient), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTraces(string packageVersion)
        {
            // ALWAYS: 98 spans
            // - SqlCommand: 21 spans (3 groups * 7 spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            // - SqlCommandVb: 21 spans (3 groups * 7 spans)
            //
            // NETSTANDARD: +56 spans
            // - DbCommand-netstandard:  42 spans (6 groups * 7 spans)
            // - IDbCommand-netstandard: 14 spans (2 groups * 7 spans)
            //
            // CALLSITE + NETSTANDARD + NETCORE: +4 spans
            // - IDbCommandGenericConstrant<SqlCommand>: 4 spans (2 group * 2 spans)
            //
            // CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<SqlCommand>: 7 spans (1 group * 7 spans)
            //
            // NETSTANDARD + CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<SqlCommand>-netstandard: 7 spans (1 group * 7 spans)

            const int expectedSpanCount = 168;
            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.SqlServer-" + dbType;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
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

            string packageVersion = PackageVersions.SystemDataSqlClient.First()[0] as string;
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            telemetry.AssertIntegrationDisabled(IntegrationId.SqlClient);
        }
    }
}
