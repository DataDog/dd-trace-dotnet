// <copyright file="NpgsqlCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
    public class NpgsqlCommandTests : TracingIntegrationTest
    {
        public NpgsqlCommandTests(ITestOutputHelper output)
            : base("Npgsql", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.Npgsql
               from metadataSchemaVersion in new[] { "v0", "v1" }
               from propagation in new[] { string.Empty, "100", "randomValue", "disabled", "service", "full" }
               select new[] { packageVersionArray[0], metadataSchemaVersion, propagation };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsNpgsql(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion, string dbmPropagation)
        {
            SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", dbmPropagation);

            // ALWAYS: 77 spans
            // - NpgsqlCommand: 21 spans (3 groups * 7 spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            //
            // NETSTANDARD: +56 spans
            // - DbCommand-netstandard:  42 spans (6 groups * 7 spans)
            // - IDbCommand-netstandard: 14 spans (2 groups * 7 spans)
            //
            // CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<NpgsqlCommand>: 7 spans (1 group * 7 spans)
            //
            // NETSTANDARD + CALLTARGET: +7 spans
            // - IDbCommandGenericConstrant<NpgsqlCommand>-netstandard: 7 spans (1 group * 7 spans)
            const int expectedSpanCount = 147;
            const string dbType = "postgres";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-{dbType}" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            int actualSpanCount = spans.Count(s => s.ParentId.HasValue); // Remove unexpected DB spans from the calculation
            var filteredSpans = spans.Where(s => s.ParentId.HasValue).ToList();

            // Assert an exact match once we can correctly instrument the generic constraint case
            actualSpanCount.Should().Be(expectedSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);
            telemetry.AssertIntegrationEnabled(IntegrationId.Npgsql);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(new Regex("Npgsql-Test-[a-zA-Z0-9]{32}"), "Npgsql-Test-GUID");
            settings.AddSimpleScrubber("out.host: localhost", "out.host: postgres");
            settings.AddSimpleScrubber("out.host: postgres_arm64", "out.host: postgres");

            var fileName = nameof(NpgsqlCommandTests);
#if NET462
            fileName = fileName + ".Net462";
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

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public async Task IntegrationDisabled()
        {
            const int totalSpanCount = 21;
            const string expectedOperationName = "postgres.query";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.Npgsql)}_ENABLED", "false");

            string packageVersion = PackageVersions.Npgsql.First()[0] as string;
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            telemetry.AssertIntegrationDisabled(IntegrationId.Npgsql);
        }
    }
}
