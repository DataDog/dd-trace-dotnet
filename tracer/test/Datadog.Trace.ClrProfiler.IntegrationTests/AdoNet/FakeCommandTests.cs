// <copyright file="FakeCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class FakeCommandTests : TestHelper
    {
        public FakeCommandTests()
            : base("FakeDbCommand")
        {
            SetServiceVersion("1.0.0");
        }

        [TestCase(true)]
        [Property("Category", "EndToEnd")]
        public void SubmitsTracesWithNetStandard(bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

#if NET452
            var expectedSpanCount = 28;
#else
            var expectedSpanCount = 42;
#endif

            const string dbType = "fake";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.FakeDbCommand-" + dbType;

            // NOTE: opt into the additional instrumentation of calls into netstandard.dll
            SetEnvironmentVariable("DD_TRACE_NETSTANDARD_ENABLED", "true");

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port))
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

        [TestCase(true)]
        [TestCase(false)]
        [Property("Category", "EndToEnd")]
        public void SpansDisabledByAdoNetExcludedTypes(bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            var totalSpanCount = 21;

            const string dbType = "fake";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable(ConfigurationKeys.AdoNetExcludedTypes, "Samples.FakeDbCommand.FakeCommand;System.Data.Common.DbCommand;System.Data.SqlClient.SqlCommand;Microsoft.Data.SqlClient.SqlCommand;MySql.Data.MySqlClient.MySqlCommand;Npgsql.NpgsqlCommand");

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port))
            {
                var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);
                CollectionAssert.IsNotEmpty(spans);
                CollectionAssert.IsEmpty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            }
        }
    }
}
