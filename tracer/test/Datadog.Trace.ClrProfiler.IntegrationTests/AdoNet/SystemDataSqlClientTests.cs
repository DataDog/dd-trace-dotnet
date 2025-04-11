// <copyright file="SystemDataSqlClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    [UsesVerify]
    public class SystemDataSqlClientTests : TracingIntegrationTest
    {
        public SystemDataSqlClientTests(ITestOutputHelper output)
            : base("SqlServer", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetFullConfig()
            => from packageVersionArray in PackageVersions.SystemDataSqlClient
               from metadataSchemaVersion in new[] { "v0", "v1" }
               from propagation in new[] { string.Empty, "100", "randomValue", "disabled", "service", "full" }
               from injectStoredProc in new[] { "true", "false" }
               select new[] { packageVersionArray[0], metadataSchemaVersion, propagation, injectStoredProc };

        public static IEnumerable<object[]> GetReducedConfig()
        {
            object minPackage = string.Empty;
            object maxPackage = string.Empty;
            if (PackageVersions.SystemDataSqlClient.Any())
            {
                // we get the min and max supported versions to test the "full" range
                minPackage = PackageVersions.SystemDataSqlClient.First()[0];
                maxPackage = PackageVersions.SystemDataSqlClient.Last()[0];
            }

            return
            [
                [maxPackage, "v1", "full", "true"],
                [minPackage, "v1", "full", "true"],
                [maxPackage, "v1", "service", "true"],

                // do not change the stored procedure command test
                [maxPackage, "v1", "service", "false"],

                // test with disabled propagation
                [maxPackage, "v1", "disabled", "true"],

                // metadata is not as important as others, so just one test
                [maxPackage, "v0", "full", "true"],
            ];
        }

        // Determine which configuration to use based on branch (and whether this is run locally)
        public static IEnumerable<object[]> GetTestConfiguration()
        {
            return IsMainOrReleaseBranch() ? GetFullConfig() : GetReducedConfig();
        }

        public static bool IsMainOrReleaseBranch()
        {
            // TODO: consider interaction with RequiresThoroughTesting()
            var isMainOrReleaseBranch = Environment.GetEnvironmentVariable("isMainOrReleaseBranch") ?? string.Empty;

            if (string.IsNullOrEmpty(isMainOrReleaseBranch))
            {
                // Default to true if the environment variable is not set - locally just run everything
                return true;
            }

            // otherwise, only run the full suite if we are on master / release
            return string.Equals(isMainOrReleaseBranch, "True", StringComparison.OrdinalIgnoreCase);
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsSqlClient(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetTestConfiguration))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion, string dbmPropagation, string injectStoredProc)
        {
            SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", dbmPropagation);
            SetEnvironmentVariable("DD_TRACE_INJECT_CONTEXT_INTO_STORED_PROCEDURES_ENABLED", injectStoredProc);

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
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);

            spans.Count.Should().Be(expectedSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);
            telemetry.AssertIntegrationEnabled(IntegrationId.SqlClient);

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

            if (injectStoredProc == "true")
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
            var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            telemetry.AssertIntegrationDisabled(IntegrationId.SqlClient);
        }
    }
}
