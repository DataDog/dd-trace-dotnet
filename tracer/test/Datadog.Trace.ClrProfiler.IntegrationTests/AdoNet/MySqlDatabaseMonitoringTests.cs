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
        public MySqlDatabaseMonitoringTests(ITestOutputHelper output)
            : base("MySql", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_ENV", "testing");
        }

        public static IEnumerable<object[]> GetMySql8Data()
        {
            var propagation = new[] { string.Empty, "100", "randomValue", "disabled", "service", "full" };

            foreach (object[] item in PackageVersions.MySqlData)
            {
                if (!((string)item[0]).StartsWith("8") && !string.IsNullOrEmpty((string)item[0]))
                {
                    continue;
                }

                var result = propagation.SelectMany(prop => new[]
                {
                    new[] { item[0], "v0", prop },
                    new[] { item[0], "v1", prop }
                });

                foreach (var row in result)
                {
                    yield return row;
                }
            }
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsMySql(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetMySql8Data))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTracesInMySql8(string packageVersion, string metadataSchemaVersion, string dbmPropagation)
        {
            await SubmitDbmCommentedSpans(packageVersion, metadataSchemaVersion, dbmPropagation);
        }

        private async Task SubmitDbmCommentedSpans(string packageVersion, string metadataSchemaVersion, string propagationLevel)
        {
            SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", propagationLevel);

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
                "service" => ".service",
                "full" => ".full",
                _ => ".disabled",
            });

            fileName += $".Schema{metadataSchemaVersion.ToUpper()}";

            await VerifyHelper.VerifySpans(filteredSpans, settings)
                .DisableRequireUniquePrefix()
                .UseFileName(fileName);
        }
    }
}
