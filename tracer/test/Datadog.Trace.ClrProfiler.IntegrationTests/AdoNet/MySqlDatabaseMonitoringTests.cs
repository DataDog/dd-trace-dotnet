// <copyright file="MySqlDatabaseMonitoringTests.cs" company="Datadog">
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
    public class MySqlDatabaseMonitoringTests : TracingIntegrationTest
    {
        private const string ServiceName = "Samples.MySql";

        public MySqlDatabaseMonitoringTests(ITestOutputHelper output)
            : base("MySql", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_ENV", "testing");
        }

        public static IEnumerable<object[]> GetMySql8Data()
        {
            foreach (object[] item in PackageVersions.MySqlData)
            {
                if (!((string)item[0]).StartsWith("8") && !string.IsNullOrEmpty((string)item[0]))
                {
                    continue;
                }

                yield return new[] { item[0], "v0" };
                yield return new[] { item[0], "v1" };
            }
        }

        public override Result ValidateIntegrationSpan(MockSpan span) => span.IsMySql();

        [SkippableTheory]
        [MemberData(nameof(GetMySql8Data))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationNotSet(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, string.Empty);
        }

        [SkippableTheory]
        [MemberData(nameof(GetMySql8Data))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationIntValue(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, "100");
        }

        [SkippableTheory]
        [MemberData(nameof(GetMySql8Data))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationRandomValue(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, "randomValue");
        }

        [SkippableTheory]
        [MemberData(nameof(GetMySql8Data))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationDisabled(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, "disabled");
        }

        [SkippableTheory]
        [MemberData(nameof(GetMySql8Data))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationService(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, "service");
        }

        [SkippableTheory]
        [MemberData(nameof(GetMySql8Data))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationFull(string packageVersion, string metadataSchemaVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, "full");
        }

        // Check that the spans have been tagged after the comment was propagated
        private void ValidatePresentDbmTag(IReadOnlyCollection<MockSpan> spans, string propagationLevel)
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

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            var filteredSpans = spans.Where(s => s.ParentId.HasValue && !s.Resource.Equals("SHOW WARNINGS", StringComparison.OrdinalIgnoreCase)).ToList();

            filteredSpans.Count().Should().Be(expectedSpanCount);
            ValidatePresentDbmTag(spans, propagationLevel);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(new Regex("[a-zA-Z0-9]{32}"), "GUID");
            settings.AddSimpleScrubber("out.host: localhost", "out.host: mysql");
            settings.AddSimpleScrubber("out.host: mysql_arm64", "out.host: mysql");

            var fileName = nameof(MySqlDatabaseMonitoringTests);
#if NET5_0_OR_GREATER
            fileName = fileName + "Net";
#elif NET462
            fileName = fileName + "Net462";
#else
            fileName = fileName + "NetCore";
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
