// <copyright file="MicrosoftDataSqliteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP2_1

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [UsesVerify]
    public class MicrosoftDataSqliteTests : TracingIntegrationTest
    {
        public MicrosoftDataSqliteTests(ITestOutputHelper output)
            : base("Microsoft.Data.Sqlite", output)
        {
            SetServiceVersion("1.0.0");
            UseNativeLibraryAlpineWorkaround();
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsSqlite(metadataSchemaVersion);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public async Task SubmitsTraces(
            [PackageVersionData(nameof(PackageVersions.MicrosoftDataSqlite))] string packageVersion,
            [MetadataSchemaVersionData] string metadataSchemaVersion)
        {
#if NETCOREAPP3_0
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IsAlpine")) // set in dockerfile
             && !string.IsNullOrEmpty(packageVersion)
             && new Version(packageVersion) >= new Version("6.0.0"))
            {
                Output.WriteLine("Skipping as Microsoft.Data.Sqlite hanqs on Alpine .NET Core 3.0 with 6.0.0 package");
                return;
            }
#endif

            if (EnvironmentTools.IsLinux()
             && !EnvironmentHelper.IsAlpine()
             && GetRequiredGlibcVersion(packageVersion) is { } requiredGlibc
             && EnvironmentTools.GetGlibcVersion() is { } hostGlibc
             && hostGlibc < requiredGlibc)
            {
                throw new SkipException($"Skipping as this Microsoft.Data.Sqlite version requires glibc >= {requiredGlibc} " +
                                  $"(via SQLitePCLRaw.lib.e_sqlite3), but the host only has glibc {hostGlibc}");
            }

            const int expectedSpanCount = 105;
            const string dbType = "sqlite";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-{dbType}" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = await agent.WaitForSpansAsync(expectedSpanCount, operationName: expectedOperationName);

            Assert.Equal(expectedSpanCount, spans.Count);
            ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Sqlite);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(new Regex("Sqlite-Test-[a-zA-Z0-9]{32}"), "System-Data-SqlClient-Test-GUID");
            settings.AddSimpleScrubber("out.host: localhost", "out.host: sqlserver");
            settings.AddSimpleScrubber("out.host: (localdb)\\MSSQLLocalDB", "out.host: sqlserver");
            settings.AddSimpleScrubber("out.host: sqledge_arm64", "out.host: sqlserver");
            settings.AddSimpleScrubber("peer.service: localhost", "peer.service: sqlserver");
            settings.AddSimpleScrubber("peer.service: (localdb)\\MSSQLLocalDB", "peer.service: sqlserver");
            settings.AddSimpleScrubber("peer.service: sqledge_arm64", "peer.service: sqlserver");
            settings.AddRegexScrubber(new Regex("dd.instrumentation.time_ms: \\d+.\\d+"), "dd.instrumentation.time_ms: 123.456");

            var fileName = nameof(MicrosoftDataSqliteTests);

            await VerifyHelper.VerifySpans(spans, settings)
                  .DisableRequireUniquePrefix()
                  .UseFileName($"{fileName}.Schema{metadataSchemaVersion.ToUpper()}");
        }

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
            string packageVersion = PackageVersions.MicrosoftDataSqlite.First()[0] as string;
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = await agent.WaitForSpansAsync(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            spans.Where(s => s.Name.Equals(expectedOperationName)).Should().BeEmpty();
            await telemetry.AssertIntegrationDisabledAsync(IntegrationId.Sqlite);
        }

        private static Version GetRequiredGlibcVersion(string rawPackageVersion)
        {
            if (string.IsNullOrEmpty(rawPackageVersion))
            {
                return null;
            }

            var packageVersion = new Version(rawPackageVersion);
            return packageVersion switch
            {
                { Major: 8 } when packageVersion >= new Version(8, 0, 30) => new(2, 33),
                { Major: 9 } when packageVersion >= new Version(9, 0, 19) => new(2, 33),
                { Major: 10 } when packageVersion >= new Version(10, 0, 11) => new(2, 33),
                _ => null,
            };
        }
    }
}

#endif
