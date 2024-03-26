// <copyright file="ServiceStackRedisTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Trait("RequiresDockerDependency", "true")]
    [UsesVerify]
    public class ServiceStackRedisTests : TracingIntegrationTest
    {
        public ServiceStackRedisTests(ITestOutputHelper output)
            : base("ServiceStack.Redis", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.ServiceStackRedis
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsServiceStackRedis(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-redis" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
#if NETCOREAPP3_1_OR_GREATER
                var numberOfRuns = 3;
#else
                var numberOfRuns = 2;
#endif

                using var assertionScope = new AssertionScope();
                var expectedSpansPerRun = 13;
                var expectedSpans = numberOfRuns * expectedSpansPerRun;
                var spans = agent.WaitForSpans(expectedSpans)
                                 .OrderBy(s => s.Start)
                                 .ToList();
                spans.Count.Should().Be(expectedSpans);
                ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                var host = Environment.GetEnvironmentVariable("SERVICESTACK_REDIS_HOST") ?? "localhost:6379";
                var port = host.Substring(host.IndexOf(':') + 1);
                host = host.Substring(0, host.IndexOf(':'));

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.UseFileName($"{nameof(ServiceStackRedisTests)}.RunServiceStack" + $".Schema{metadataSchemaVersion.ToUpper()}");
                settings.DisableRequireUniquePrefix();
                settings.AddSimpleScrubber($" {TestPrefix}ServiceStack.Redis.", " ServiceStack.Redis.");
                settings.AddSimpleScrubber($"out.host: {host}", "out.host: servicestackredis");
                settings.AddSimpleScrubber($"peer.service: {host}", "peer.service: servicestackredis");
                settings.AddSimpleScrubber($"out.port: {port}", "out.port: 6379");

                // The test application runs the same RunServiceStack method X number of times
                // Use the snapshot to verify each run of RunServiceStack, instead of testing all of the application spans
                for (int i = 0; i < numberOfRuns; i++)
                {
                    var routineSpans = spans.GetRange(i * expectedSpansPerRun, expectedSpansPerRun).AsReadOnly();
                    await VerifyHelper.VerifySpans(
                        routineSpans,
                        settings,
                        o => o
                            .OrderBy(x => VerifyHelper.GetRootSpanResourceName(x, o))
                            .ThenBy(x => VerifyHelper.GetSpanDepth(x, o))
                            .ThenBy(x => x.Tags.TryGetValue("redis.raw_command", out var value) ? value.Replace(TestPrefix, string.Empty) : null)
                            .ThenBy(x => x.Start)
                            .ThenBy(x => x.Duration));
                }

                telemetry.AssertIntegrationEnabled(IntegrationId.ServiceStackRedis);
            }
        }
    }
}
