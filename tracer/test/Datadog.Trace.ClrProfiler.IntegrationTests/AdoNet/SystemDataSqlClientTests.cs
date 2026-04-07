// <copyright file="SystemDataSqlClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [UsesVerify]
    public class SystemDataSqlClientTests : TracingIntegrationTest
    {
        public SystemDataSqlClientTests(ITestOutputHelper output)
            : base("SqlServer", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsSqlClient(metadataSchemaVersion);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces(
            [PackageVersionData(nameof(PackageVersions.SystemDataSqlClient))] string packageVersion,
            [MetadataSchemaVersionData] string metadataSchemaVersion,
            [DbmPropagationModesData] string dbmPropagation,
            bool injectStoredProc)
        {
            SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", dbmPropagation);
            SetEnvironmentVariable("DD_TRACE_INJECT_CONTEXT_INTO_STORED_PROCEDURES_ENABLED", injectStoredProc.ToString());

            // ALWAYS: 98 spans
            // - SqlCommand: 21 spans (3 groups * 7 spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            // - SqlCommandVb: 21 spans (3 groups * 7 spans)
            // - StoredProcedures: 10 spans (1 group * 10 spans)
            //
            // NETSTANDARD: +56 spans
            // - DbCommand-netstandard:  42 spans (6 groups * 7 spans)
            // - IDbCommand-netstandard: 14 spans (2 groups * 7 spans)
            //
            // CALLSITE + NETSTANDARD + NETCORE: +4 spans
            // - IDbCommandGenericConstrant<SqlCommand>: 4 spans (2 group * 2 spans)
            //
            // CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<SqlCommand>: 7 spans (1 group * 7 spans)
            //
            // NETSTANDARD + CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<SqlCommand>-netstandard: 7 spans (1 group * 7 spans)

            const int expectedSpanCount = 178;
            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-{dbType}" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = await agent.WaitForSpansAsync(expectedSpanCount, operationName: expectedOperationName);

            spans.Count.Should().Be(expectedSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);
            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.SqlClient);

            // Testing that spans yield the expected output when using DBM

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(new Regex("System-Data-SqlClient-Test-[a-zA-Z0-9]{32}"), "System-Data-SqlClient-Test-GUID");
            settings.AddSimpleScrubber("out.host: localhost", "out.host: sqlserver");
            settings.AddSimpleScrubber("out.host: (localdb)\\MSSQLLocalDB", "out.host: sqlserver");
            settings.AddSimpleScrubber("out.host: sqledge_arm64", "out.host: sqlserver");
            settings.AddSimpleScrubber("peer.service: localhost", "peer.service: sqlserver");
            settings.AddSimpleScrubber("peer.service: (localdb)\\MSSQLLocalDB", "peer.service: sqlserver");
            settings.AddSimpleScrubber("peer.service: sqledge_arm64", "peer.service: sqlserver");
            settings.AddRegexScrubber(new Regex("dd.instrumentation.time_ms: \\d+.\\d+"), "dd.instrumentation.time_ms: 123.456");

            var fileName = nameof(SystemDataSqlClientTests);

            fileName = fileName + (dbmPropagation switch
            {
                "full" => ".tagged",
                _ => ".untagged",
            });

            if (injectStoredProc)
            {
                // we inject comment into the stored procedures by changing them to EXEC statements
                // only works for SQL Server, not for other databases
                // only works for stored procedures that do not have: Output, Return, InputOutput
                fileName += ".storedproc";
            }
            else
            {
                fileName += ".nostoredproc";
            }

            await VerifyHelper.VerifySpans(spans, settings)
                              .DisableRequireUniquePrefix()
                              .UseFileName($"{fileName}.Schema{metadataSchemaVersion.ToUpper()}");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task IntegrationDisabled()
        {
            const int totalSpanCount = 21;
            const string expectedOperationName = "sql-server.query";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.SqlClient)}_ENABLED", "false");

            string packageVersion = PackageVersions.SystemDataSqlClient.First()[0] as string;
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = await agent.WaitForSpansAsync(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            spans.Where(s => s.Name.Equals(expectedOperationName)).Should().BeEmpty();
            await telemetry.AssertIntegrationDisabledAsync(IntegrationId.SqlClient);
        }
    }
}
