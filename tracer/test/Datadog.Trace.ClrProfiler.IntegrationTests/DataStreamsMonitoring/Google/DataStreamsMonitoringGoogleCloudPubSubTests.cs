// <copyright file="DataStreamsMonitoringGoogleCloudPubSubTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Google
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    public class DataStreamsMonitoringGoogleCloudPubSubTests : TestHelper
    {
        private const int MessageCount = 5;

        public DataStreamsMonitoringGoogleCloudPubSubTests(ITestOutputHelper output)
            : base("DataStreams.GoogleCloudPubSub", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetPackageVersions()
            => from packageVersionArray in new string[] { string.Empty }
               select new[] { packageVersionArray };

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsDataStreams()
        {
            var runId = $"{nameof(SubmitsDataStreams)}-{Guid.NewGuid():N}";
            var topicId = $"dsm-pubsub-topic-{runId}";
            var subscriptionId = $"dsm-pubsub-subscription-{runId}";

            SetPubSubEnvironmentVariables();

            using var agent = EnvironmentHelper.GetMockAgent();

            using var processResult = await RunSampleAndWaitForExit(agent, arguments: runId);

            using var assertionScope = new AssertionScope();
            agent.SpanFilters.Add(s => s.Tags.TryGetValue("messaging.system", out var value) && value == "google-pubsub");
            var spans = await agent.WaitForSpansAsync(MessageCount * 2, timeoutInMilliseconds: 60_000);
            spans.Should().HaveCount(MessageCount * 2);

            var producerSpans = spans.Where(s => s.Tags["span.kind"] == SpanKinds.Producer);
            var consumerSpans = spans.Where(s => s.Tags["span.kind"] == SpanKinds.Consumer);
            producerSpans.Should().HaveCount(MessageCount);
            consumerSpans.Should().HaveCount(MessageCount);
            spans.Should().Contain(s => s.Tags.ContainsKey("pathway.hash"));

            processResult.StandardOutput.Should().Contain("baggage: dsm-pubsub-baggage-value");

            var points = GetPoints(agent.DataStreams).ToList();
            points.Should().NotBeEmpty();

            var producePoints = points.Where(p => p.EdgeTags.Contains("direction:out")).ToList();
            var consumePoints = points.Where(p => p.EdgeTags.Contains("direction:in")).ToList();
            producePoints.Should().HaveCountGreaterThan(0);
            consumePoints.Should().HaveCountGreaterThan(0);

            foreach (var point in producePoints)
            {
                point.EdgeTags.Should().Contain("type:google-pubsub");
                point.EdgeTags.Should().Contain($"topic:{topicId}");
            }

            foreach (var point in consumePoints)
            {
                point.EdgeTags.Should().Contain("type:google-pubsub");
                point.EdgeTags.Should().Contain($"subscription:{subscriptionId}");
            }

            // every consume point descends from a produce point: the pathway is linked by parent hash
            var produceHashes = producePoints.Select(p => p.Hash).ToImmutableHashSet();
            consumePoints.Should().OnlyContain(c => c.ParentHash != 0 && produceHashes.Contains(c.ParentHash));
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public async Task RemapsPeerServiceOnProducerSpans()
        {
            var runId = $"{nameof(RemapsPeerServiceOnProducerSpans)}-{Guid.NewGuid():N}";
            var topicId = $"dsm-pubsub-topic-{runId}";

            SetPubSubEnvironmentVariables();
            SetEnvironmentVariable("DD_TRACE_PEER_SERVICE_DEFAULTS_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_PEER_SERVICE_MAPPING", $"{topicId}:orders-service");

            using var agent = EnvironmentHelper.GetMockAgent();

            using var processResult = await RunSampleAndWaitForExit(agent, arguments: runId);

            using var assertionScope = new AssertionScope();
            agent.SpanFilters.Add(s => s.Tags.TryGetValue("messaging.system", out var value) && value == "google-pubsub");
            var spans = await agent.WaitForSpansAsync(MessageCount * 2, timeoutInMilliseconds: 60_000);
            spans.Should().HaveCount(MessageCount * 2);

            var producerSpans = spans.Where(s => s.Tags["span.kind"] == SpanKinds.Producer).ToList();
            producerSpans.Should().HaveCount(MessageCount);
            producerSpans.Should().OnlyContain(s => s.GetTag("peer.service") == "orders-service");
            producerSpans.Should().OnlyContain(s => s.GetTag("peer.service.remapped_from") == topicId);
        }

        private static IEnumerable<MockDataStreamsStatsPoint> GetPoints(IImmutableList<MockDataStreamsPayload> payloads)
        {
            foreach (var payload in payloads)
            {
                foreach (var bucket in payload.Stats)
                {
                    if (bucket.Stats != null)
                    {
                        foreach (var point in bucket.Stats)
                        {
                            yield return point;
                        }
                    }
                }
            }
        }

        private void SetPubSubEnvironmentVariables()
        {
            var emulatorHost = Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST");
            SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
            SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", string.IsNullOrEmpty(emulatorHost) ? "localhost:8085" : emulatorHost);
        }
    }
}
