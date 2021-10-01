// <copyright file="DapperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET452
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class DapperTests : TestHelper
    {
        public DapperTests()
            : base("Dapper")
        {
            SetServiceVersion("1.0.0");
        }

        [Test]
        [Property("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            const int expectedSpanCount = 17;
            const string dbType = "postgres";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.Dapper-" + dbType;

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
                    Assert.AreEqual(dbType, span.Tags?[Tags.DbType]);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }
            }
        }

        [Test]
        [Property("Category", "EndToEnd")]
        public void SubmitsTracesWithNetStandard()
        {
            const int expectedSpanCount = 17;
            const string dbType = "postgres";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.Dapper-" + dbType;

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
                    Assert.AreEqual(dbType, span.Tags?[Tags.DbType]);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }
            }
        }
    }
}
#endif
