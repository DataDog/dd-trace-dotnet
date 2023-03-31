// <copyright file="SqlServerDatabaseMonitoringTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
    public class SqlServerDatabaseMonitoringTests : TracingIntegrationTest
    {
        public SqlServerDatabaseMonitoringTests(ITestOutputHelper output)
            : base("SqlServer", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_ENV", "testing");
        }

        public override Result ValidateIntegrationSpan(MockSpan span) => span.IsSqlClient();

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.SystemDataSqlClient), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationNotSet(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, string.Empty);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.SystemDataSqlClient), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationIntValue(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, "100");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.SystemDataSqlClient), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationRandomValue(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, "randomValue");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.SystemDataSqlClient), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationDisabled(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, "disabled");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.SystemDataSqlClient), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationService(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, "service");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.SystemDataSqlClient), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitDbmCommentedSpanspropagationFull(string packageVersion)
        {
            await SubmitDbmCommentedSpans(packageVersion, "full");
        }

        private async Task SubmitDbmCommentedSpans(string packageVersion, string propagationLevel)
        {
            if (propagationLevel != string.Empty)
            {
                SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", propagationLevel);
            }

            // ALWAYS: 98 spans
            // - SqlCommand: 21 spans (3 groups * 7 spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            // - SqlCommandVb: 21 spans (3 groups * 7 spans)
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

            const int expectedSpanCount = 168;
            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);

            spans.Count().Should().Be(expectedSpanCount);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(new Regex("[a-zA-Z0-9]{32}"), "GUID");
            settings.AddSimpleScrubber("out.host: (localdb)\\MSSQLLocalDB", "out.host: sqlsever");
            settings.AddSimpleScrubber("out.host: (localdb)\\MSSQLLocalDB_arm64", "out.host: sqlsever");

            var fileName = nameof(SqlServerDatabaseMonitoringTests);

            fileName = fileName + (propagationLevel switch
            {
                "service" or "full" => ".enabled",
                _ => ".disabled",
            });

            await VerifyHelper.VerifySpans(spans, settings)
                .DisableRequireUniquePrefix()
                .UseFileName(fileName);
        }
    }
}
