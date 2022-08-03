// <copyright file="FakeCommandTests.cs" company="Datadog">
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
            // ALWAYS: 91 spans
            // - FakeCommand: 21 spans (3 groups * 7 spans)
            // - DbCommand: 21 spans (3 groups * 7 spans)
            // - IDbCommand: 7 spans (1 groups * 7 spans)
            // - IDbCommandGenericConstraint<FakeCommand>: 7 spans (1 group * 7 spans)
            // - DbCommand-netstandard:  21 spans (3 groups * 7 spans)
            // - IDbCommand-netstandard: 7 spans (1 groups * 7 spans)
            // - IDbCommandGenericConstraint<SqlCommand>-netstandard: 7 spans (1 group * 7 spans)

            const int expectedSpanCount = 91;
            const string dbType = "fake";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.FakeDbCommand-fake";

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            int actualSpanCount = spans.Count(s => s.ParentId.HasValue); // Remove unexpected DB spans from the calculation

            Assert.Equal(expectedSpanCount, actualSpanCount);

            foreach (var span in spans)
            {
                var result = span.IsAdoNet();
                Assert.True(result.Success, result.ToString());

                Assert.Equal(expectedOperationName, span.Name);
                Assert.Equal(expectedServiceName, span.Service);
                Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.AdoNet);
        }
    }
}
