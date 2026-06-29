// <copyright file="SystemDataSqlClientOtelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [UsesVerify]
    public class SystemDataSqlClientOtelTests : TracingIntegrationTest
    {
        public SystemDataSqlClientOtelTests(ITestOutputHelper output)
            : base("SqlServer", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.IsSqlClient("otel");

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTracesOtel(
            [PackageVersionData(nameof(PackageVersions.SystemDataSqlClient))] string packageVersion,
            [DbmPropagationModesData] string dbmPropagation)
        {
            SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", dbmPropagation);

            const int expectedSpanCount = 178;
            const string dbType = "sql-server";
            const string expectedOperationName = dbType + ".query";

            var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-{dbType}";

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = await agent.WaitForSpansAsync(expectedSpanCount, operationName: expectedOperationName);
            var filteredSpans = spans.Where(s => s.ParentId.HasValue).ToList();

            ValidateIntegrationSpans(spans, metadataSchemaVersion: "otel", expectedServiceName: clientSpanServiceName, isExternalSpan: true);
            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.SqlClient);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddSimpleScrubber("server.address: localhost", "server.address: sqlserver");

            var fileName = nameof(SystemDataSqlClientOtelTests);
#if NETFRAMEWORK
            fileName = fileName + ".Net462";
#endif
            fileName = fileName + (dbmPropagation switch
            {
                "full" => ".tagged",
                _ => ".untagged",
            });

            await VerifyHelper.VerifySpans(filteredSpans, settings)
                              .DisableRequireUniquePrefix()
                              .UseFileName($"{fileName}.OtelSemantics");
        }
    }
}
