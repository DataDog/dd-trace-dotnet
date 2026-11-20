// <copyright file="DataStreamsMonitoringAzureStorageQueuesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Azure;

[UsesVerify]
public class DataStreamsMonitoringAzureStorageQueuesTests : TracingIntegrationTest
{
    public DataStreamsMonitoringAzureStorageQueuesTests(ITestOutputHelper output)
        : base("Samples.DataStreams.AzureStorageQueues", output)
    {
    }

    public static IEnumerable<object[]> GetPackageVersions() =>
        from packageVersionArray in new string[] { string.Empty }
        select new string[] { packageVersionArray };

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
        span.Tags["span.kind"] switch
        {
            SpanKinds.Producer or SpanKinds.Consumer when span.Tags.TryGetValue("messaging.system", out var messagingSystem) && messagingSystem == "azurestoragequeues"
                => Result.DefaultSuccess,
            _ => throw new ArgumentException($"span.tags are not supported for the Azure Storage Queues integration: {span.Tags["span.kind"]}", nameof(span)),
        };

    [SkippableTheory]
    [MemberData(nameof(GetPackageVersions))]
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [Trait("Category", "EndToEnd")]
    public async Task SubmitsDsmMetrics(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            agent.SpanFilters.Add(static s => s.Tags.TryGetValue("messaging.system", out var value) && value == "azurestoragequeues");

            var spans = await agent.WaitForSpansAsync(6);
            spans.Should().HaveCount(6);

            var taggedSpans = spans.Where(s => s.Tags.ContainsKey("pathway.hash"));
            taggedSpans.Should().HaveCount(5);

            var producerSpans = spans.Where(s => s.Tags["span.kind"] == SpanKinds.Producer).ToList();
            var consumerSpans = spans.Where(s => s.Tags["span.kind"] == SpanKinds.Consumer).ToList();
            producerSpans.Should().HaveCount(3);
            consumerSpans.Should().HaveCount(3);

            var points = PayloadsToPoints(agent.DataStreams);
            points.Should().NotBeEmpty();

            var producePoints = points.Where(p => p.EdgeTags.Contains("direction:out")).ToList();
            var consumePoints = points.Where(p => p.EdgeTags.Contains("direction:in")).ToList();
            producePoints.Should().HaveCount(3);
            consumePoints.Should().HaveCount(3);

            foreach (var point in points)
            {
                point.EdgeTags.Should().Contain("type:azure-storage-queue");
                point.EdgeTags.Should().Contain("topic:dsm-test-queue");
                point.PayloadSize.Should().NotBeEmpty();
            }

            var produceHashes = producePoints.Select(p => p.Hash).ToHashSet();
            var linkedConsumePoints = consumePoints.Where(p => p.ParentHash != 0 && produceHashes.Contains(p.ParentHash)).ToList();
            linkedConsumePoints.Should().HaveCount(2);
        }
    }

    private static IList<MockDataStreamsStatsPoint> PayloadsToPoints(IImmutableList<MockDataStreamsPayload> payloads)
    {
        var points = new List<MockDataStreamsStatsPoint>();
        foreach (var payload in payloads)
        {
            foreach (var bucket in payload.Stats)
            {
                foreach (var point in bucket.Points)
                {
                    points.Add(point);
                }
            }
        }

        return points;
    }
}
