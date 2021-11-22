// <copyright file="MySqlConnectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class MySqlConnectorTests : TestHelper
    {
        public MySqlConnectorTests(ITestOutputHelper output)
            : base("MySqlConnector", output)
        {
            SetServiceVersion("1.0.0");
        }

        [Theory]
        [MemberData(nameof(PackageVersions.MySqlConnector), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion)
        {
            // ALWAYS: 75 spans
            // - MySqlCommand: 21 spans (3 groups * 7 spans - 6 missing spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            //
            // NETSTANDARD: +56 spans
            // - DbCommand-netstandard:  42 spans (6 groups * 7 spans)
            // - IDbCommand-netstandard: 14 spans (2 groups * 7 spans)
            //
            // CALLTARGET: +9 spans
            // - MySqlCommand: 6 additional spans
            // - IDbCommandGenericConstrant<MySqlCommand>: 7 spans (1 group * 7 spans)
            //
            // NETSTANDARD + CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<MySqlCommand>-netstandard: 7 spans (1 group * 7 spans)
            const int expectedSpanCount = 147;
            const string dbType = "mysql";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.MySqlConnector-" + dbType;

            int agentPort = TcpPortProvider.GetOpenPort();
            using var agent = new MockTracerAgent(agentPort);
            using var process = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            int actualSpanCount = spans.Count(s => s.ParentId.HasValue && !s.Resource.Equals("SHOW WARNINGS", StringComparison.OrdinalIgnoreCase)); // Remove unexpected DB spans from the calculation

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
        public void IntegrationDisabled()
        {
            const int totalSpanCount = 21;
            const string expectedOperationName = "mysql.query";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.MySql)}_ENABLED", "false");

            string packageVersion = PackageVersions.MySqlConnector.First()[0] as string;
            int agentPort = TcpPortProvider.GetOpenPort();
            using var agent = new MockTracerAgent(agentPort);
            using var process = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
        }
    }
}
