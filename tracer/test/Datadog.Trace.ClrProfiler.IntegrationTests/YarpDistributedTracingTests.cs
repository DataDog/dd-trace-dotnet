// <copyright file="YarpDistributedTracingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class YarpDistributedTracingTests : TracingIntegrationTest
    {
        private const string ServiceVersion = "1.0.0";

        public YarpDistributedTracingTests(ITestOutputHelper output)
            : base("Yarp.DistributedTracing", output)
        {
            SetServiceVersion(ServiceVersion);
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => Result.DefaultSuccess;

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(PackageVersions.Yarp), MemberType = typeof(PackageVersions))]
        public async Task SubmitsTraces(string packageVersion)
        {
            // We expect the following trace to be generated:
            // http.request => aspnet_core.request => http.request => aspnet_core.request
            const int expectedSpans = 4;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, aspNetCorePort: 0))
            {
                var spans = agent.WaitForSpans(expectedSpans, 1_000);
                spans.Count.Should().Be(expectedSpans);

                // All Datadog spans should be in the same trace
                spans.Select(x => x.TraceId).Distinct().Count().Should().Be(1);

                // All parent-id's should correspond to Datadog spans that were sent to the agent
                var spanIds = spans.Select(s => s.SpanId);
                var parentIds = spans.Where(s => s.ParentId is not null).Select(s => s.ParentId).Cast<ulong>();
                parentIds.Should().BeSubsetOf(spanIds);

                var settings = VerifyHelper.GetSpanVerifierSettings();

                // TODO: Remove this fix. The aspnet_core.endpoint tag value is different starting with net7.0
                // Since this test is not interested in generating the aspnet_core span, we'll just apply a band-aid
                // solution to the snapshot testing
                settings.AddSimpleScrubber("aspnet_core.endpoint: / HTTP: GET", "aspnet_core.endpoint: HTTP: GET /");

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(YarpDistributedTracingTests))
                                  .DisableRequireUniquePrefix();
            }
        }
    }
}

#endif
