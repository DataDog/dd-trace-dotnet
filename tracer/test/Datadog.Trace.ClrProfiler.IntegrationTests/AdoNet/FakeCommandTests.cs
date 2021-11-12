// <copyright file="FakeCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class FakeCommandTests : TestHelper
    {
        public FakeCommandTests(ITestOutputHelper output)
            : base("FakeDbCommand", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            const int expectedSpanCount = 42;
            const string dbType = "fake";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.FakeDbCommand-" + dbType;

            int agentPort = TcpPortProvider.GetOpenPort();

            using var agent = new MockTracerAgent(agentPort);
            using var process = RunSampleAndWaitForExit(agent.Port);
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
}
