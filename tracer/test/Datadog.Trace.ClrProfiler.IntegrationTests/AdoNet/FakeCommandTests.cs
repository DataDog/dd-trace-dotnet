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
            const int expectedSpanCount = 21;
            const string dbType = "fake";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.FakeDbCommand";

            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(EnvironmentHelper.AgentPort);
            var spans = agent.WaitForSpans(expectedSpanCount);

            Assert.Equal(expectedSpanCount, spans.Count);

            foreach (var span in spans)
            {
                Assert.Equal(expectedServiceName, span.Service);

                // we do NOT expect any `fake.query` spans
                Assert.NotEqual(expectedOperationName, span.Name);
                Assert.NotEqual(SpanTypes.Sql, span.Type);
                Assert.False(span.Tags?.ContainsKey(Tags.DbType));
            }
        }
    }
}
