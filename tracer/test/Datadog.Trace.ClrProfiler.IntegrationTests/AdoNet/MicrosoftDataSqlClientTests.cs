// <copyright file="MicrosoftDataSqlClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [Trait("RequiresDockerDependency", "true")]
    public class MicrosoftDataSqlClientTests : TracingIntegrationTest
    {
        public MicrosoftDataSqlClientTests(ITestOutputHelper output)
            : base("Microsoft.Data.SqlClient", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.MicrosoftDataSqlClient
               from metadataSchemaVersion in new[] { "v0", "v1" }
               from dbmEnabled in new[] { true, false }
               from propagation in new[] { "disabled", "service", "full" }
               select new[] { packageVersionArray[0], metadataSchemaVersion, dbmEnabled, propagation };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsSqlClient(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion, bool dbmEnabled, string propagation)
        {
            // ALWAYS: 133 spans
            // - SqlCommand: 21 spans (3 groups * 7 spans)
            // - DbCommand:  42 spans (6 groups * 7 spans)
            // - IDbCommand: 14 spans (2 groups * 7 spans)
            // - DbCommand-netstandard:  42 spans (6 groups * 7 spans)
            // - IDbCommand-netstandard: 14 spans (2 groups * 7 spans)
            //
            // CALLTARGET: +14 spans
            // - IDbCommandGenericConstraint<SqlCommand>: 7 spans (1 group * 7 spans)
            // - IDbCommandGenericConstraint<SqlCommand>-netstandard: 7 spans (1 group * 7 spans)

            // version 4.0 : 91 spans
            // - SqlCommand: 21 spans (3 groups * 7 spans)
            // - DbCommand:  21 spans (3 groups * 7 spans)
            // - IDbCommand: 7 spans (1 groups * 7 spans)
            // - DbCommand-netstandard:  21 spans (3 groups * 7 spans)
            // - IDbCommand-netstandard: 7 spans (1 groups * 7 spans)
            // - IDbCommandGenericConstraint<SqlCommand>: 7 spans (1 group * 7 spans)
            // - IDbCommandGenericConstraint<SqlCommand>-netstandard: 7 spans (1 group * 7 spans)
            var isVersion4 = !string.IsNullOrWhiteSpace(packageVersion)
                          && new Version(packageVersion) >= new Version("4.0.0");

            if (isVersion4 && FrameworkDescription.Instance.OSPlatform != OSPlatformName.Windows)
            {
                // Version 4.0.0 has an issue on Linux https://github.com/dotnet/SqlClient/issues/1390
                return;
            }

            var expectedSpanCount = isVersion4 ? 91 : 147;
            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, dbmEnabled ? "1" : "0");
            SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", propagation);
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-{dbType}" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            var actualSpanCount = spans.Count(s => s.ParentId.HasValue); // Remove unexpected DB spans from the calculation

            Assert.Equal(expectedSpanCount, actualSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);
            telemetry.AssertIntegrationEnabled(IntegrationId.SqlClient);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task IntegrationDisabled()
        {
            const int totalSpanCount = 21;
            const string expectedOperationName = "sql-server.query";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.SqlClient)}_ENABLED", "false");

            string packageVersion = PackageVersions.MicrosoftDataSqlClient.First()[0] as string;
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
