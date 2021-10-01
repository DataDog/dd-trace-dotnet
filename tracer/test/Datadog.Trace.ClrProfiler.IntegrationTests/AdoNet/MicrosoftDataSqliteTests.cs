// <copyright file="MicrosoftDataSqliteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET452
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class MicrosoftDataSqliteTests : TestHelper
    {
        public MicrosoftDataSqliteTests()
            : base("Microsoft.Data.Sqlite")
        {
            SetServiceVersion("1.0.0");
        }

        [TestCaseSource(nameof(PackageVersions.MicrosoftDataSqlite))]
        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [Property("Category", "ArmUnsupported")]
        public void SubmitsTracesWithNetStandard(string packageVersion)
        {
            SetCallTargetSettings(enableCallTarget: true);

            var expectedSpanCount = 91;

            const string dbType = "sqlite";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.Microsoft.Data.Sqlite-" + dbType;

            // NOTE: opt into the additional instrumentation of calls into netstandard.dll
            SetEnvironmentVariable("DD_TRACE_NETSTANDARD_ENABLED", "true");

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.AreEqual(expectedSpanCount, spans.Count);

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
        [Property("RunOnWindows", "True")]
        [Property("Category", "ArmUnsupported")]
        public void SpansDisabledByAdoNetExcludedTypes(bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            var totalSpanCount = 21;

            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable(ConfigurationKeys.AdoNetExcludedTypes, "System.Data.SQLite.SQLiteCommand;Microsoft.Data.Sqlite.SqliteCommand");

            string packageVersion = PackageVersions.MicrosoftDataSqlite.First()[0] as string;
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
#endif
