// <copyright file="StackExchangeRedisTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
    public class StackExchangeRedisTests : TracingIntegrationTest
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

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.StackExchangeRedis
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsStackExchangeRedis(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-redis" : EnvironmentHelper.FullSampleName;

            using var a = new AssertionScope();
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using (await RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
                var calculatedVersion = GetPackageVersion(packageVersion);

                var expectedCount = calculatedVersion switch
                {
                    PackageVersion.V1_0_414 => 184,
                    PackageVersion.V1_2_0 => 196,
                    _ => 202,
                };

                var spans = agent.WaitForSpans(expectedCount);
                ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                var host = Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_HOST") ?? "localhost:6389";
                var port = host.Substring(host.IndexOf(':') + 1);
                host = host.Substring(0, host.IndexOf(':'));

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.UseFileName($"{nameof(StackExchangeRedisTests)}.{calculatedVersion}" + $".Schema{metadataSchemaVersion.ToUpper()}");
                settings.DisableRequireUniquePrefix();
                settings.AddSimpleScrubber($" {TestPrefix}StackExchange.Redis.", " StackExchange.Redis.");
                if (EnvironmentTools.IsOsx())
                {
                    settings.AddSimpleScrubber("out.host: localhost", "out.host: stackexchangeredis");
                    settings.AddSimpleScrubber("peer.service: localhost", "peer.service: stackexchangeredis");
                    settings.AddSimpleScrubber("out.host: 127.0.0.1", "out.host: stackexchangeredis-replica");
                    settings.AddSimpleScrubber("peer.service: 127.0.0.1", "peer.service: stackexchangeredis-replica");
                    settings.AddSimpleScrubber("out.port: 6390", "out.port: 6379");
                    settings.AddSimpleScrubber("out.port: 6391", "out.port: 6379");
                    settings.AddSimpleScrubber("out.port: 6392", "out.port: 6379");
                }
                else
                {
                    settings.AddSimpleScrubber($"out.host: {host}", "out.host: stackexchangeredis");
                    settings.AddSimpleScrubber($"peer.service: {host}", "peer.service: stackexchangeredis");
                    settings.AddSimpleScrubber($"out.port: {port}", "out.port: 6379");
                }

                await VerifyHelper.VerifySpans(
                    spans,
                    settings,
                    o => o
                        .OrderBy(x => VerifyHelper.GetRootSpanResourceName(x, o))
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
