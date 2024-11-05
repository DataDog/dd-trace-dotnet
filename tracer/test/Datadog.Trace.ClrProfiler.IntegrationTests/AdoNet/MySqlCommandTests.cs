// <copyright file="MySqlCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
    [Trait("RequiresDockerDependency", "true")]
    [UsesVerify]
    public class MySqlCommandTests : TracingIntegrationTest
    {
        public MySqlCommandTests(ITestOutputHelper output)
            : base("MySql", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetMySqlData(bool newVersionsOnly)
        {
            foreach (var item in PackageVersions.MySqlData)
            {
                var version = (string)item[0];
                var isNewVersion = string.IsNullOrEmpty(version) || Version.Parse(version).Major >= 8;
                if (newVersionsOnly != isNewVersion)
                {
                    continue;
                }

                foreach (var propagation in new[] { string.Empty, "100", "randomValue", "disabled", "service", "full" })
                {
                    yield return [version, "v0", propagation];
                    yield return [version, "v1", propagation];
                }
            }
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsMySql(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetMySqlData), parameters: true)]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTracesInMySql8(string packageVersion, string metadataSchemaVersion, string dbmPropagation)
        {
            await SubmitsTraces(packageVersion, metadataSchemaVersion, dbmPropagation);
        }

        [SkippableTheory]
        [MemberData(nameof(GetMySqlData), parameters: false)]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public async Task SubmitsTracesInOldMySql(string packageVersion, string metadataSchemaVersion, string dbmPropagation)
        {
            await SubmitsTraces(packageVersion, metadataSchemaVersion, dbmPropagation);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public async Task IntegrationDisabled()
        {
            const int totalSpanCount = 21;
            const string expectedOperationName = "mysql.query";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.MySql)}_ENABLED", "false");

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();

            // don't use the first package version which is 6.x and is not supported on ARM64.
            // use the default package version for the sample, currently 8.0.17.
            // string packageVersion = PackageVersions.MySqlData.First()[0] as string;
            using var process = await RunSampleAndWaitForExit(agent /* , packageVersion: packageVersion */);
            var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            telemetry.AssertIntegrationDisabled(IntegrationId.MySql);
        }

        private async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion, string dbmPropagation)
        {
            SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", dbmPropagation);

            // ALWAYS: 75 spans
            // - MySqlCommand: 19 spans (3 groups * 7 spans - 2 missing spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            //
            // NETSTANDARD: +56 spans
            // - DbCommand-netstandard:  42 spans (6 groups * 7 spans)
            // - IDbCommand-netstandard: 14 spans (2 groups * 7 spans)
            //
            // CALLTARGET: +9 spans
            // - MySqlCommand: 2 additional spans
            // - IDbCommandGenericConstrant<MySqlCommand>: 7 spans (1 group * 7 spans)
            //
            // NETSTANDARD + CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<MySqlCommand>-netstandard: 7 spans (1 group * 7 spans)
            var expectedSpanCount = 147;

            const string dbType = "mysql";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-{dbType}" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            var filteredSpans = spans.Where(s => s.ParentId.HasValue && !s.Resource.Equals("SHOW WARNINGS", StringComparison.OrdinalIgnoreCase)).ToList();

            filteredSpans.Count.Should().Be(expectedSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);
            telemetry.AssertIntegrationEnabled(IntegrationId.MySql);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(new Regex("MySql-Test-[a-zA-Z0-9]{32}"), "MySql-Test-GUID");
            settings.AddSimpleScrubber("out.host: localhost", "out.host: mysql");
            settings.AddSimpleScrubber("out.host: mysql57", "out.host: mysql");
            settings.AddSimpleScrubber("out.host: mysql_arm64", "out.host: mysql");

            var fileName = nameof(MySqlCommandTests);

#if NET5_0_OR_GREATER
            fileName = fileName + ".Net";
#elif NET462
            fileName = fileName + ".Net462";
#else
            fileName = fileName + ".NetCore";
#endif
            fileName = fileName + (dbmPropagation switch
            {
                "full" => ".tagged",
                _ => ".untagged",
            });

            await VerifyHelper.VerifySpans(filteredSpans, settings)
                              .DisableRequireUniquePrefix()
                              .UseFileName($"{fileName}.Schema{metadataSchemaVersion.ToUpper()}");
        }
    }
}
