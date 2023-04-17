// <copyright file="NpgsqlDatabaseMonitoringTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [Trait("RequiresDockerDependency", "true")]
    [UsesVerify]
    public class NpgsqlDatabaseMonitoringTests : TracingIntegrationTest
    {
        public NpgsqlDatabaseMonitoringTests(ITestOutputHelper output)
            : base("Npgsql", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_ENV", "testing");
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.Npgsql
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span) => span.IsNpgsql();

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationNotSet(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, string.Empty);
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationIntValue(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, "100");
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationRandomValue(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, "randomValue");
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationDisabled(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, "disabled");
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationService(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, "service");
        }

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationFull(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, "full");
        }

        // Check that the spans have been tagged after the comment was propagated
        private void ValidatePresentDbmTag(IEnumerable<MockSpan> spans, string propagationLevel)
        {
            if (propagationLevel == "service" || propagationLevel == "full")
            {
                foreach (var span in spans)
                {
                    span.Tags?.Should().ContainKey(Tags.DbmDataPropagated);
                }
            }
            else
            {
                foreach (var span in spans)
                {
                    span.Tags?.Should().NotContainKey(Tags.DbmDataPropagated);
                }
            }
        }

        private async Task SubmitDbmCommentedSpans(string packageVersion, string metadataSchemaVersion, string propagationLevel)
        {
            if (propagationLevel != string.Empty)
            {
                SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", propagationLevel);
            }

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

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            var filteredSpans = spans.Where(s => s.ParentId.HasValue).ToList();

            filteredSpans.Count().Should().Be(expectedSpanCount);
            ValidatePresentDbmTag(spans, propagationLevel);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(new Regex("[a-zA-Z0-9]{32}"), "GUID");
            settings.AddSimpleScrubber("out.host: localhost", "out.host: postgres");
            settings.AddSimpleScrubber("out.host: postgres_arm64", "out.host: postgres");

            var fileName = nameof(NpgsqlDatabaseMonitoringTests);
#if NET462
            fileName = fileName + "Net462";
#endif
            fileName = fileName + (propagationLevel switch
            {
                "service" or "full" => ".enabled",
                _ => ".disabled",
            });

            fileName += $".Schema{metadataSchemaVersion.ToUpper()}";

            await VerifyHelper.VerifySpans(filteredSpans, settings)
                .DisableRequireUniquePrefix()
                .UseFileName(fileName);
        }
    }
}
