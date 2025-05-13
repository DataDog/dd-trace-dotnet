// <copyright file="FakeCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [UsesVerify]
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

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public Task SubmitTracesDisabledFakeCommandV0() => RunDisabledFakeCommandTest("v0");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public Task SubmitTracesDisabledFakeCommandV1() => RunDisabledFakeCommandTest("v1");

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

            var settings = VerifyHelper.GetSpanVerifierSettings();
            var dbResourceRegex = new Regex("Test-[0-9a-fA-F]+");
            settings.AddRegexScrubber(dbResourceRegex, "Test-GUID");
            await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(FakeCommandTests) + $"_{metadataSchemaVersion}");

            telemetry.AssertIntegrationEnabled(IntegrationId.AdoNet);
        }

        private async Task RunDisabledFakeCommandTest(string metadataSchemaVersion)
        {
            // ALWAYS: 21 spans - these are the custom spans from RelationalDatabaseTestHarness
            // There should be NO SQL spans in the snapshots
            const int expectedSpanCount = 21;

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_DISABLED_ADONET_COMMAND_TYPES", "FakeCommand");
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(expectedSpanCount);
            int actualSpanCount = spans.Count();

            Assert.Equal(expectedSpanCount, actualSpanCount);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            var dbResourceRegex = new Regex("Test-[0-9a-fA-F]+");
            settings.AddRegexScrubber(dbResourceRegex, "Test-GUID");
            await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(FakeCommandTests) + $"_{metadataSchemaVersion}_disabledFakeCommand");

            // We should have created 0 spans from ADO.NET integration - spans created are from RelationaTestHarness and are manual spans
            telemetry.AssertIntegrationDisabled(IntegrationId.AdoNet);
        }
    }
}
