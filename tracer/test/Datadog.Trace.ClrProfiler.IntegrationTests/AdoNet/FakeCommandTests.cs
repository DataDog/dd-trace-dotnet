// <copyright file="FakeCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class FakeCommandTests : TracingIntegrationTest
    {
        public FakeCommandTests(ITestOutputHelper output)
            : base("FakeDbCommand", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsAdoNet(metadataSchemaVersion);

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public Task SubmitsTracesV0() => RunTest("v0");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public Task SubmitsTracesV1() => RunTest("v1");

        private async Task RunTest(string metadataSchemaVersion)
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

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-{dbType}" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            int actualSpanCount = spans.Count(s => s.ParentId.HasValue); // Remove unexpected DB spans from the calculation

            Assert.Equal(expectedSpanCount, actualSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

            foreach (var span in spans)
            {
                Assert.Equal(expectedOperationName, span.Name);
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.AdoNet);
        }
    }
}
