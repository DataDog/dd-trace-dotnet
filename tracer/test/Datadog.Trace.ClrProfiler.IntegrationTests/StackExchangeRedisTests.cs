// <copyright file="StackExchangeRedisTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.TestCollections;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection(nameof(StackExchangeRedisTestCollection))]
    [Trait("RequiresDockerDependency", "true")]
    [UsesVerify]
    public class StackExchangeRedisTests : TestHelper
    {
        public StackExchangeRedisTests(ITestOutputHelper output)
            : base("StackExchange.Redis", output)
        {
            SetServiceVersion("1.0.0");
        }

        private enum PackageVersion
        {
            // ReSharper disable InconsistentNaming
            // All the versions before here give different outputs,
            // but as we never test them, there's not much point in creating the snapshots
            V1_0_414, // Adds support for MIGRATE
            V1_2_0, // Supports GEO* commands
            V1_2_2, // Supports DDCUSTOM, ECHO, SLOWLOG, TIME
            V2_0_495, // First 2.0 version with many breaking changes
            V2_0_571, // Switches to UNLINK (instead of DEL)
            Latest, // Uses different call stacks
            // ReSharper restore InconsistentNaming
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.StackExchangeRedis), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion)
        {
            using var a = new AssertionScope();
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using (RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
                var calculatedVersion = GetPackageVersion(packageVersion);

                var expectedCount = calculatedVersion switch
                {
                    PackageVersion.V1_0_414 => 184,
                    PackageVersion.V1_2_0 => 196,
                    _ => 202,
                };

                var spans = agent.WaitForSpans(expectedCount);
                foreach (var span in spans)
                {
                    var result = span.IsStackExchangeRedis();
                    Assert.True(result.Success, result.ToString());
                }

                var host = Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_HOST") ?? "localhost:6389";
                var port = host.Substring(host.IndexOf(':') + 1);
                host = host.Substring(0, host.IndexOf(':'));

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.UseFileName($"{nameof(StackExchangeRedisTests)}.{calculatedVersion}");
                settings.DisableRequireUniquePrefix();
                settings.AddSimpleScrubber($" {TestPrefix}StackExchange.Redis.", " StackExchange.Redis.");
                settings.AddSimpleScrubber($"out.host: {host}", "out.host: stackexchangeredis");
                settings.AddSimpleScrubber($"out.port: {port}", "out.port: 6379");

                await VerifyHelper.VerifySpans(
                    spans,
                    settings,
                    o => o
                        .OrderBy(x => VerifyHelper.GetRootSpanName(x, o))
                        .ThenBy(x => VerifyHelper.GetSpanDepth(x, o))
                        .ThenBy(x => x.Tags.TryGetValue("redis.raw_command", out var value) ? value.Replace(TestPrefix, string.Empty) : null)
                        .ThenBy(x => x.Start)
                        .ThenBy(x => x.Duration));
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.StackExchangeRedis);
        }

        private static PackageVersion GetPackageVersion(string packageVersionString)
        {
            if (string.IsNullOrEmpty(packageVersionString))
            {
                // Default value specified in Samples.StackExchange.Redis.csproj is 1.2.6
                return PackageVersion.V1_2_2;
            }

            var version = new Version(packageVersionString);
            return version switch
            {
                _ when version >= new Version(2, 6, 45) => PackageVersion.Latest,
                _ when version >= new Version(2, 0, 571) => PackageVersion.V2_0_571,
                _ when version >= new Version(2, 0, 495) => PackageVersion.V2_0_495,
                _ when version >= new Version(1, 2, 2) => PackageVersion.V1_2_2,
                _ when version >= new Version(1, 2, 0) => PackageVersion.V1_2_0,
                _ when version >= new Version(1, 0, 414) => PackageVersion.V1_0_414,
                _ => throw new InvalidOperationException("Snapshot not yet created for version " + packageVersionString),
            };
        }
    }
}
