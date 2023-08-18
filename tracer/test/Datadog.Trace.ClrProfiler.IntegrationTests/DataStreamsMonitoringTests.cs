// <copyright file="DataStreamsMonitoringTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
[Collection(nameof(KafkaTests.KafkaTestsCollection))]
[Trait("RequiresDockerDependency", "true")]
public class DataStreamsMonitoringTests : TestHelper
{
    public DataStreamsMonitoringTests(ITestOutputHelper output)
        : base("DataStreams.Kafka", output)
    {
        SetServiceVersion("1.0.0");
    }

    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <summary>
    /// This sample does a series of produces and consumes to create two pipelines:
    ///  - service -> topic 1 -> Consumer 1 -> topic 2 -> Consumer 2 -> topic 3 -> consumer 3
    ///  - service -> topic 2 -> Consumer 2 -> topic 3 -> consumer 3
    /// Each node (apart from 'service') in the pipelines above have a unique hash
    ///
    /// In mermaid (view at https://mermaid.live/), this looks like:
    /// sequenceDiagram
    ///    participant A as Root Service (12926600137239154356)
    ///    participant T1 as Topic 1 (2704081292861755358)
    ///    participant C1 as Consumer 1 (5289074475783863123)
    ///    participant T2a as Topic 2 (2821413369272395429)
    ///    participant C2a as Consumer 2 (9753735904472423641)
    ///    participant T3a as Topic 3 (5363062531028060751)
    ///    participant T2 as Topic 2 (246622801349204431)
    ///    participant C2 as Consumer 2 (3398817358352474903)
    ///    participant T3 as Topic 3 (16689539899325095461 )
    ///
    ///    A->>+T1: Produce
    ///    T1-->>-C1: Consume
    ///    C1->>+T2a: Produce
    ///    T2a-->>-C2a: Consume
    ///    C2a->>+T3a: Produce
    ///
    ///    A->>+T2: Produce
    ///    T2-->>-C2: Consume
    ///    C2->>+T3: Produce
    /// </summary>
    /// <param name="enableConsumerScopeCreation">Is the scope created manually or using built-in support</param>
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    public async Task SubmitsDataStreams(bool enableConsumerScopeCreation)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.KafkaCreateConsumerScopeEnabled, enableConsumerScopeCreation ? "1" : "0");

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

        using var processResult = RunSampleAndWaitForExit(agent);

        using var assertionScope = new AssertionScope();
        var payload = NormalizeDataStreams(agent.DataStreams);
        // using span verifier to add all the default scrubbers
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddSimpleScrubber(TracerConstants.AssemblyVersion, "2.x.x.x");
        settings.ModifySerialization(
            _ =>
            {
                _.MemberConverter<MockDataStreamsStatsPoint, byte[]>(x => x.EdgeLatency, ScrubByteArray);
                _.MemberConverter<MockDataStreamsStatsPoint, byte[]>(x => x.PathwayLatency, ScrubByteArray);
            });
        await Verifier.Verify(payload, settings)
                      .UseFileName($"{nameof(DataStreamsMonitoringTests)}.{nameof(SubmitsDataStreams)}")
                      .DisableRequireUniquePrefix();
    }

    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <summary>
    /// This sample tests a fan in + out scenario:
    ///  - service -> topic 1 -|
    ///  - service -> topic 1 -|---> Consumer 1  -| -> topic 2 -> Consumer 2
    ///  - service -> topic 1 -|                  | -> topic 2 -> Consumer 2
    /// Each node (apart from 'service') in the pipelines above have a unique hash
    ///
    /// In mermaid (view at https://mermaid.live/), this looks a little like:
    /// sequenceDiagram
    ///     participant A as Root Service<br>(12926600137239154356)
    ///     participant T1 as Topic 1<br>(3184837087859198448)
    ///     participant C1 as Consumer 1<br>(428893431238664991)
    ///     participant T2a as Topic 2<br>(4701874528067105417)
    ///     participant C2a as Consumer 2<br>(5603712524956936337)
    ///     participant T3a as Topic 3<br>(713412453862704155)
    ///     participant T2 as Topic 2<br>(9146411116191305908)
    ///     participant C2 as Consumer 2<br>(9288243326407318747)
    ///     participant T3 as Topic 3<br>(17029362228578737937 )
    ///
    ///     A->>+T1: Produce
    ///     T1-->>-C1: Consume
    ///     C1->>+T2a: Produce
    ///     T2a-->>-C2a: Consume
    ///     C2a->>+T3a: Produce
    ///
    ///     A->>+T2: Produce
    ///     T2-->>-C2: Consume
    ///     C2->>+T3: Produce
    /// </summary>
    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    public async Task HandlesFanIn()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.KafkaCreateConsumerScopeEnabled, "0"); // only way to do fan-in properly

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

        using var processResult = RunSampleAndWaitForExit(agent, arguments: "--fan-in");

        using var assertionScope = new AssertionScope();

        var payload = NormalizeDataStreams(agent.DataStreams);

        // using span verifier to add all the default scrubbers
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddSimpleScrubber(TracerConstants.AssemblyVersion, "2.x.x.x");
        settings.ModifySerialization(
            _ =>
            {
                _.MemberConverter<MockDataStreamsStatsPoint, byte[]>(x => x.EdgeLatency, ScrubByteArray);
                _.MemberConverter<MockDataStreamsStatsPoint, byte[]>(x => x.PathwayLatency, ScrubByteArray);
            });

        await Verifier.Verify(payload, settings)
                      .UseFileName($"{nameof(DataStreamsMonitoringTests)}.{nameof(HandlesFanIn)}")
                      .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    public void WhenDisabled_DoesNotSubmitDataStreams()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "0");

        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = RunSampleAndWaitForExit(agent);

        using var assertionScope = new AssertionScope();
        // We don't expect any streams here, so no point waiting for ages
        var dataStreams = agent.WaitForDataStreams(2, timeoutInMilliseconds: 2_000);
        dataStreams.Should().BeEmpty();
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    public void WhenNotSupported_DoesNotSubmitDataStreams()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var agent = EnvironmentHelper.GetMockAgent();
        agent.Configuration = new MockTracerAgent.AgentConfiguration { Endpoints = Array.Empty<string>() };
        using var processResult = RunSampleAndWaitForExit(agent);

        using var assertionScope = new AssertionScope();
        var dataStreams = agent.DataStreams;
        dataStreams.Should().BeEmpty();
    }

    private static byte[] ScrubByteArray(MockDataStreamsStatsPoint target, byte[] value)
    {
        if (value is null || value.Length == 0)
        {
            return value;
        }

        // return a different value so we can identify that we have some data
        return new byte[] { 0xFF };
    }

    private static MockDataStreamsPayload NormalizeDataStreams(IImmutableList<MockDataStreamsPayload> dataStreams)
    {
        // This is nasty and hacky, but it's the only way I could get any semblance
        // of snapshots. We could have more than one payload due to the way flushing works,
        // but if we ignore the start times of the buckets, we can group them in a consistent way
        dataStreams.Should().NotBeEmpty();

        // make sure they all have the same top level properties
        var payload = dataStreams.First();
        dataStreams.Should()
                   .OnlyContain(x => x.Env == payload.Env)
                   .And.OnlyContain(x => x.Lang == payload.Lang)
                   .And.OnlyContain(x => x.Service == payload.Service)
                   .And.OnlyContain(x => x.PrimaryTag == payload.PrimaryTag)
                   .And.OnlyContain(x => x.TracerVersion == payload.TracerVersion);

        var currentBucket = new MockDataStreamsBucket { Duration = 10_000_000_000, Start = 1661520120000000000UL };
        var originBucket = new MockDataStreamsBucket { Duration = 10_000_000_000, Start = 1661520120000000000UL };

        var currentBucketStats = new List<MockDataStreamsStatsPoint>();
        var originBucketStats = new List<MockDataStreamsStatsPoint>();
        foreach (var mockPayload in dataStreams)
        {
            foreach (var bucket in mockPayload.Stats)
            {
                bucket.Duration.Should().Be(10_000_000_000); // 10s in ns
                bucket.Start.Should().BePositive();

                var buckets = bucket.Stats.First().TimestampType == "current" ? currentBucketStats : originBucketStats;
                foreach (var bucketStat in bucket.Stats)
                {
                    if (!buckets.Any(x => x.Hash == bucketStat.Hash && x.ParentHash == bucketStat.ParentHash))
                    {
                        buckets.Add(bucketStat);
                    }
                }
            }
        }

        currentBucket.Stats = StableSort(currentBucketStats);
        originBucket.Stats = StableSort(originBucketStats);
        payload.Stats = new[] { currentBucket, originBucket };
        return payload;

        static MockDataStreamsStatsPoint[] StableSort(IReadOnlyCollection<MockDataStreamsStatsPoint> points)
        {
            // sort each bucket by "depth", then by consumer name
            // Ensure a static ordering for the spans
            return points
                  .OrderBy(x => GetRootHashName(x, points))
                  .ThenBy(x => GetHashDepth(x, points))
                  .ThenBy(x => x.Hash)
                  .ToArray();
        }

        static ulong GetRootHashName(MockDataStreamsStatsPoint point, IReadOnlyCollection<MockDataStreamsStatsPoint> allPoints)
        {
            while (point.ParentHash != 0)
            {
                var parent = allPoints.FirstOrDefault(x => x.Hash == point.ParentHash);
                if (parent is null)
                {
                    // no span with the given Parent Id, so treat this one as the root instead
                    break;
                }

                point = parent;
            }

            return point.Hash;
        }

        static int GetHashDepth(MockDataStreamsStatsPoint point, IReadOnlyCollection<MockDataStreamsStatsPoint> allPoints)
        {
            var depth = 0;
            while (point.ParentHash != 0)
            {
                var parent = allPoints.FirstOrDefault(x => x.Hash == point.ParentHash);
                if (parent is null)
                {
                    // no span with the given Parent Id, so treat this one as the root instead
                    break;
                }

                point = parent;
                depth++;
            }

            return depth;
        }
    }
}
