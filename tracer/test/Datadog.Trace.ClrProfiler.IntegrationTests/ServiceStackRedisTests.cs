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
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Trait("RequiresDockerDependency", "true")]
    [UsesVerify]
    public class ServiceStackRedisTests : TestHelper
    {
        public ServiceStackRedisTests(ITestOutputHelper output)
            : base("ServiceStack.Redis", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.ServiceStackRedis), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion)
        {
            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
#if NETCOREAPP3_1_OR_GREATER
                var numberOfRuns = 3;
#else
                var numberOfRuns = 2;
#endif

                var expectedSpansPerRun = 12;
                var spans = agent.WaitForSpans(numberOfRuns * expectedSpansPerRun)
                                 .OrderBy(s => s.Start)
                                 .ToList();
                spans.Count.Should().Be(numberOfRuns * expectedSpansPerRun);

                var host = Environment.GetEnvironmentVariable("SERVICESTACK_REDIS_HOST") ?? "localhost:6379";
                var port = host.Substring(host.IndexOf(':') + 1);
                host = host.Substring(0, host.IndexOf(':'));

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.UseFileName($"{nameof(ServiceStackRedisTests)}.RunServiceStack");
                settings.DisableRequireUniquePrefix();
                settings.AddSimpleScrubber($" {TestPrefix}ServiceStack.Redis.", " ServiceStack.Redis.");
                settings.AddSimpleScrubber($"out.host: {host}", "out.host: servicestackredis");
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
                            .OrderBy(x => VerifyHelper.GetRootSpanName(x, o))
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
