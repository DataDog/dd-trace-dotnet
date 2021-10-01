// <copyright file="NpgsqlCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class NpgsqlCommandTests : TestHelper
    {
        public NpgsqlCommandTests()
            : base("Npgsql")
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetNpgsql()
        {
            foreach (object[] item in PackageVersions.Npgsql)
            {
                yield return item.Concat(false);
                yield return item.Concat(true);
            }
        }

        [TestCaseSource(nameof(GetNpgsql))]
        [Property("Category", "EndToEnd")]
        public void SubmitsTracesWithNetStandard(string packageVersion, bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            // ALWAYS: 77 spans
            // - NpgsqlCommand: 21 spans (3 groups * 7 spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            //
            // NETSTANDARD: +56 spans
            // - DbCommand-netstandard:  42 spans (6 groups * 7 spans)
            // - IDbCommand-netstandard: 14 spans (2 groups * 7 spans)
            //
            // CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<NpgsqlCommand>: 7 spans (1 group * 7 spans)
            //
            // NETSTANDARD + CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<NpgsqlCommand>-netstandard: 7 spans (1 group * 7 spans)
#if NET452
            var expectedSpanCount = 77;
#else
            var expectedSpanCount = 133;
#endif

            if (enableCallTarget)
            {
#if NET452
                expectedSpanCount = 84;
#else
                expectedSpanCount = 147;
#endif
            }

            const string dbType = "postgres";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.Npgsql-" + dbType;

            // NOTE: opt into the additional instrumentation of calls into netstandard.dll
            // see https://github.com/DataDog/dd-trace-dotnet/pull/753
            SetEnvironmentVariable("DD_TRACE_NETSTANDARD_ENABLED", "true");

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                int actualSpanCount = spans.Where(s => s.ParentId.HasValue).Count(); // Remove unexpected DB spans from the calculation
                // Assert.Equal(expectedSpanCount, spans.Count); // Assert an exact match once we can correctly instrument the generic constraint case

                if (enableCallTarget)
                {
                    Assert.AreEqual(expectedSpanCount, actualSpanCount);
                }
                else
                {
                    Assert.True(actualSpanCount == expectedSpanCount || actualSpanCount == expectedSpanCount + 4, $"expectedSpanCount={expectedSpanCount}, expectedSpanCount+4={expectedSpanCount + 4}, actualSpanCount={actualSpanCount}");
                }

                foreach (var span in spans)
                {
                    Assert.AreEqual(expectedOperationName, span.Name);
                    Assert.AreEqual(expectedServiceName, span.Service);
                    Assert.AreEqual(SpanTypes.Sql, span.Type);
                    Assert.AreEqual(dbType, span.Tags[Tags.DbType]);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        [Property("Category", "EndToEnd")]
        public void SpansDisabledByAdoNetExcludedTypes(bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            var totalSpanCount = 21;

            const string dbType = "postgres";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable(ConfigurationKeys.AdoNetExcludedTypes, "System.Data.SqlClient.SqlCommand;Microsoft.Data.SqlClient.SqlCommand;MySql.Data.MySqlClient.MySqlCommand;Npgsql.NpgsqlCommand");

            string packageVersion = PackageVersions.Npgsql.First()[0] as string;
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);
                CollectionAssert.IsNotEmpty(spans);
                CollectionAssert.IsEmpty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            }
        }
    }
}
