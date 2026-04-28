// <copyright file="OcelotDistributedTracingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class OcelotDistributedTracingTests : TracingIntegrationTest
    {
        private const string ServiceVersion = "1.0.0";

        public OcelotDistributedTracingTests(ITestOutputHelper output)
            : base("Ocelot.DistributedTracing", output)
        {
            SetServiceVersion(ServiceVersion);
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => Result.DefaultSuccess;

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [CombinatorialOrPairwiseData]
        public async Task SubmitsTraces([PackageVersionData(nameof(PackageVersions.Ocelot))] string packageVersion)
        {
            // We expect the following trace to be generated:
            // http.request => aspnet_core.request => http.request => aspnet_core.request
            const int expectedSpans = 4;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, aspNetCorePort: 0))
            {
                var spans = await agent.WaitForSpansAsync(expectedSpans);

                var settings = VerifyHelper.GetSpanVerifierSettings();

                // The aspnet_core.endpoint tag value is different starting with net7.0
                // Since this test is not interested in generating the aspnet_core span, we'll just apply a band-aid
                // solution to the snapshot testing
                settings.AddSimpleScrubber("aspnet_core.endpoint: / HTTP: GET", "aspnet_core.endpoint: HTTP: GET /");

                // server.port is a dynamic port that changes between runs
                settings.AddRegexScrubber(new System.Text.RegularExpressions.Regex(@"server\.port: \d+\.0"), "server.port: 00000");

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(OcelotDistributedTracingTests))
                                  .DisableRequireUniquePrefix();
            }
        }
    }
}

#endif
