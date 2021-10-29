// <copyright file="MySqlCommandTests.cs" company="Datadog">
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
    public class MySqlCommandTests : TestHelper
    {
        public MySqlCommandTests(ITestOutputHelper output)
            : base("MySql", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetMySql8Data()
        {
            foreach (object[] item in PackageVersions.MySqlData)
            {
                if (!((string)item[0]).StartsWith("8") && !string.IsNullOrEmpty((string)item[0]))
                {
                    continue;
                }

                yield return item;
            }
        }

        public static IEnumerable<object[]> GetOldMySqlData()
        {
            foreach (object[] item in PackageVersions.MySqlData)
            {
                if (((string)item[0]).StartsWith("8"))
                {
                    continue;
                }

                yield return item;
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetMySql8Data))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTracesWithNetStandardInMySql8(string packageVersion)
        {
            SubmitsTracesWithNetStandard(packageVersion);
        }

        [SkippableTheory]
        [MemberData(nameof(GetOldMySqlData))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTracesWithNetStandardInOldMySql(string packageVersion)
        {
            SubmitsTracesWithNetStandard(packageVersion);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public void SpansDisabledByAdoNetExcludedTypes()
        {
            var totalSpanCount = 21;

            const string dbType = "mysql";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable(ConfigurationKeys.AdoNetExcludedTypes, "System.Data.SqlClient.SqlCommand;Microsoft.Data.SqlClient.SqlCommand;MySql.Data.MySqlClient.MySqlCommand;Npgsql.NpgsqlCommand");

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port))
            {
                var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);
                Assert.NotEmpty(spans);
                Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            }
        }

        private void SubmitsTracesWithNetStandard(string packageVersion)
        {
            // ALWAYS: 75 spans
            // - MySqlCommand: 19 spans (3 groups * 7 spans - 2 missing spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            //
            // NETSTANDARD: +56 spans
            // - DbCommand-netstandard:  42 spans (6 groups * 7 spans)
            // - IDbCommand-netstandard: 14 spans (2 groups * 7 spans)
            //
            // CALLTARGET: +9 spans
            // - MySqlCommand: 2 additional spans
            // - IDbCommandGenericConstrant<MySqlCommand>: 7 spans (1 group * 7 spans)
            //
            // NETSTANDARD + CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<MySqlCommand>-netstandard: 7 spans (1 group * 7 spans)
            var expectedSpanCount = 147;

            const string dbType = "mysql";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.MySql-" + dbType;

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                int actualSpanCount = spans.Where(s => s.ParentId.HasValue && !s.Resource.Equals("SHOW WARNINGS", System.StringComparison.OrdinalIgnoreCase)).Count(); // Remove unexpected DB spans from the calculation
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
        }
    }
}
