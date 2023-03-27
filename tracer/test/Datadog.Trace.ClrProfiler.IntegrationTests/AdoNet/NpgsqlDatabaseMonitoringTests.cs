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

        public override Result ValidateIntegrationSpan(MockSpan span) => span.IsNpgsql();

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Npgsql), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationNotSet(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, string.Empty);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Npgsql), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationIntValue(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, "100");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Npgsql), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationRandomValue(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, "randomValue");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Npgsql), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationDisabled(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, "disabled");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Npgsql), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationService(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, "service");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Npgsql), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationFull(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, "full");
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

        private async Task SubmitDbmCommentedSpans(string packageVersion, string propagationLevel)
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

            var fileName = nameof(NpgsqlDatabaseMonitoringTests);
#if NET462
            fileName = fileName + "Net462";
#endif
            fileName = fileName + (propagationLevel switch
            {
                "service" or "full" => ".enabled",
                _ => ".disabled",
            });

            await VerifyHelper.VerifySpans(filteredSpans, settings)
                .DisableRequireUniquePrefix()
                .UseFileName(fileName);
        }
    }
}
