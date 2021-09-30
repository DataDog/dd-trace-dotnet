// <copyright file="SystemDataSqlClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class SystemDataSqlClientTests : TestHelper
    {
        public SystemDataSqlClientTests(ITestOutputHelper output)
            : base("SqlServer", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetSystemDataSqlClient()
        {
            foreach (object[] item in PackageVersions.SystemDataSqlClient)
            {
                yield return item.Concat(false);
                yield return item.Concat(true);
            }
        }

        [Theory]
        [MemberData(nameof(GetSystemDataSqlClient))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTracesWithNetStandard(string packageVersion, bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

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
#if NET452
            var expectedSpanCount = 98;
#elif NET461
            var expectedSpanCount = 154;
#else
            var expectedSpanCount = 158;
#endif

            if (enableCallTarget)
            {
#if NET452
                expectedSpanCount = 105;
#else
                expectedSpanCount = 168;
#endif
            }

            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.SqlServer-" + dbType;

            // NOTE: opt into the additional instrumentation of calls into netstandard.dll
            SetEnvironmentVariable("DD_TRACE_NETSTANDARD_ENABLED", "true");

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
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

            string packageVersion = PackageVersions.SystemDataSqlClient.First()[0] as string;
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);
                Assert.NotEmpty(spans);
                Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            }
        }
    }
}
