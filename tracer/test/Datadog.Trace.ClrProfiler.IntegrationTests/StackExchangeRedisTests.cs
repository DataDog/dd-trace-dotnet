// <copyright file="StackExchangeRedisTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.ClrProfiler.IntegrationTests.TestCollections;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection(nameof(StackExchangeRedisTestCollection))]
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [UsesVerify]
    public class StackExchangeRedisTests : TracingIntegrationTest
    {
        private readonly StackExchangeRedisFixture _redisFixture;

        public StackExchangeRedisTests(ITestOutputHelper output, StackExchangeRedisFixture redisFixture)
            : base("StackExchange.Redis", output)
        {
            _redisFixture = redisFixture;
            SetServiceVersion("1.0.0");
            ConfigureContainers(redisFixture);
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
            V3_0_0, // ECHO/SLOWLOG/TIME are no longer database-scoped, so don't report db.redis.database_index
            // ReSharper restore InconsistentNaming
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsStackExchangeRedis(metadataSchemaVersion);

        [Flaky("The PING_REPLICA sometimes invokes the master server instead. We believe it's infrastructure related", maxRetries: 3)]
        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(
            [PackageVersionData(nameof(PackageVersions.StackExchangeRedis))] string packageVersion,
            [MetadataSchemaVersionData] string metadataSchemaVersion)
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
                    _ => 203,
                };

                var spans = await agent.WaitForSpansAsync(expectedCount);
                ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                settings.UseFileName($"{nameof(StackExchangeRedisTests)}.{calculatedVersion}" + $".Schema{metadataSchemaVersion.ToUpper()}");
                settings.DisableRequireUniquePrefix();
                settings.AddSimpleScrubber($" {TestPrefix}StackExchange.Redis.", " StackExchange.Redis.");
                AddEndpointScrubbers(settings, _redisFixture.PrimaryHost, _redisFixture.PrimaryPort, "stackexchangeredis");
                AddEndpointScrubbers(settings, _redisFixture.ReplicaHost, _redisFixture.ReplicaPort, "stackexchangeredis-replica");

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

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.StackExchangeRedis);
        }

        private static void AddEndpointScrubbers(VerifyTests.VerifySettings settings, string host, ushort port, string normalizedHost)
        {
            var escapedHost = Regex.Escape(host);
            var endpointWithPeerService = new Regex($@"out\.host: {escapedHost},\r?\n([ \t]+)out\.port: {port},\r?\n\1peer\.service: [^,\r\n]+,");
            var endpoint = new Regex($@"out\.host: {escapedHost},\r?\n([ \t]+)out\.port: {port},");
            settings.AddScrubber(
                builder =>
                {
                    var scrubbed = endpointWithPeerService.Replace(
                        builder.ToString(),
                        $"out.host: {normalizedHost},\n      out.port: 6379,\n      peer.service: {normalizedHost},");
                    scrubbed = endpoint.Replace(scrubbed, $"out.host: {normalizedHost},\n      out.port: 6379,");
                    builder.Clear().Append(scrubbed);
                });
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
                _ when version >= new Version(3, 0, 0) => PackageVersion.V3_0_0,
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
