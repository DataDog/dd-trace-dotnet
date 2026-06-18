// <copyright file="NpgsqlCommandOtelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [UsesVerify]
    public class NpgsqlCommandOtelTests : TracingIntegrationTest
    {
        public NpgsqlCommandOtelTests(ITestOutputHelper output)
            : base("Npgsql", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.IsNpgsql("otel");

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTracesOtel(
            [PackageVersionData(nameof(PackageVersions.Npgsql))] string packageVersion,
            [DbmPropagationModesData] string dbmPropagation)
        {
            SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", dbmPropagation);

            const int expectedSpanCount = 147;
            const string dbType = "postgres";
            const string expectedOperationName = dbType + ".query";

            var clientSpanServiceName = $"{EnvironmentHelper.FullSampleName}-{dbType}";

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = await agent.WaitForSpansAsync(expectedSpanCount, operationName: expectedOperationName);
            int actualSpanCount = spans.Count(s => s.ParentId.HasValue);
            var filteredSpans = spans.Where(s => s.ParentId.HasValue).ToList();

            actualSpanCount.Should().Be(expectedSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "otel", expectedServiceName: clientSpanServiceName, isExternalSpan: true);
            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Npgsql);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(new Regex("Npgsql-Test-[a-zA-Z0-9]{32}"), "Npgsql-Test-GUID");
            settings.AddSimpleScrubber("server.address: localhost", "server.address: postgres");
            settings.AddSimpleScrubber("server.address: postgres_arm64", "server.address: postgres");

            var fileName = nameof(NpgsqlCommandOtelTests);
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
