// <copyright file="MicrosoftDataSqlClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class MicrosoftDataSqlClientTests : TestHelper
    {
        public MicrosoftDataSqlClientTests(ITestOutputHelper output)
            : base("Microsoft.Data.SqlClient", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.MicrosoftDataSqlClient), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTraces(string packageVersion)
        {
            // ALWAYS: 133 spans
            // - SqlCommand: 21 spans (3 groups * 7 spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            // - DbCommand-netstandard:  42 spans (6 groups * 7 spans)
            // - IDbCommand-netstandard: 14 spans (2 groups * 7 spans)
            //
            // CALLTARGET: +14 spans
            // - IDbCommandGenericConstraint<SqlCommand>: 7 spans (1 group * 7 spans)
            // - IDbCommandGenericConstraint<SqlCommand>-netstandard: 7 spans (1 group * 7 spans)
            const int expectedSpanCount = 147;
            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.Microsoft.Data.SqlClient-" + dbType;

            int agentPort = TcpPortProvider.GetOpenPort();
            using var agent = new MockTracerAgent(agentPort);
            using var process = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            int actualSpanCount = spans.Count(s => s.ParentId.HasValue); // Remove unexpected DB spans from the calculation

            Assert.Equal(expectedSpanCount, actualSpanCount);

            foreach (var span in spans)
            {
                Assert.Equal(expectedOperationName, span.Name);
                Assert.Equal(expectedServiceName, span.Service);
                Assert.Equal(SpanTypes.Sql, span.Type);
                Assert.Equal(dbType, span.Tags[Tags.DbType]);
                Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void IntegrationDisabled()
        {
            const int totalSpanCount = 21;
            const string expectedOperationName = "sql-server.query";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.SqlClient)}_ENABLED", "false");

            string packageVersion = PackageVersions.MicrosoftDataSqlClient.First()[0] as string;
            int agentPort = TcpPortProvider.GetOpenPort();
            using var agent = new MockTracerAgent(agentPort);
            using var process = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
        }
    }
}
