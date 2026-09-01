// <copyright file="SystemDataSqliteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [UsesVerify]
    public class SystemDataSqliteTests : TracingIntegrationTest
    {
        public SystemDataSqliteTests(ITestOutputHelper output)
            : base("SQLite.Core", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsSqlite(metadataSchemaVersion);

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public Task SubmitsTracesV0() => RunTest("v0");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public Task SubmitsTracesV1() => RunTest("v1");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public async Task IntegrationDisabled()
        {
            const int totalSpanCount = 21;
            const string expectedOperationName = "sqlite.query";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.Sqlite)}_ENABLED", "false");
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent);
            var spans = await agent.WaitForSpansAsync(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            spans.Where(s => s.Name.Equals(expectedOperationName)).Should().BeEmpty();
            await telemetry.AssertIntegrationDisabledAsync(IntegrationId.Sqlite);
        }

        private async Task RunTest(string metadataSchemaVersion)
        {
            const int expectedSpanCount = 105;
            const string dbType = "sqlite";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-{dbType}" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent);
            var spans = await agent.WaitForSpansAsync(expectedSpanCount, operationName: expectedOperationName);

            Assert.Equal(expectedSpanCount, spans.Count);
            ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);
            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Sqlite);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(new Regex("SQLite-Test-[a-zA-Z0-9]{32}"), "System-Data-SqlClient-Test-GUID");
            settings.AddSimpleScrubber("out.host: localhost", "out.host: sqlserver");
            settings.AddSimpleScrubber("out.host: (localdb)\\MSSQLLocalDB", "out.host: sqlserver");
            settings.AddSimpleScrubber("out.host: sqledge_arm64", "out.host: sqlserver");
            settings.AddSimpleScrubber("peer.service: localhost", "peer.service: sqlserver");
            settings.AddSimpleScrubber("peer.service: (localdb)\\MSSQLLocalDB", "peer.service: sqlserver");
            settings.AddSimpleScrubber("peer.service: sqledge_arm64", "peer.service: sqlserver");
            settings.AddRegexScrubber(new Regex("dd.instrumentation.time_ms: \\d+.\\d+"), "dd.instrumentation.time_ms: 123.456");

            var fileName = nameof(SystemDataSqliteTests);

            await VerifyHelper.VerifySpans(spans, settings)
                  .DisableRequireUniquePrefix()
                  .UseFileName($"{fileName}.Schema{metadataSchemaVersion.ToUpper()}");
        }
    }
}
