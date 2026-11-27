// <copyright file="DataStreamsMonitoringAzureServiceBusLegacyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Azure;

public class DataStreamsMonitoringAzureServiceBusLegacyTests : TestHelper
{
    private const string Queue = "samples-legacy-queue";
    private const string Topic = "samples-legacy-topic";
    private const string Subscription = "samples-legacy-topic/Subscriptions/samples-legacy-subscription";

    public DataStreamsMonitoringAzureServiceBusLegacyTests(ITestOutputHelper output)
        : base("AzureServiceBus.Legacy", output)
    {
        SetServiceVersion("1.0.0");
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.AzureServiceBusLegacy), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    public async Task LinksConsumeToProduce(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var assertionScope = new AssertionScope();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

        processResult.StandardOutput.Should().Contain("Messages with dd-pathway-ctx-base64: 5 of 5");

        var expectedEdges = new[]
        {
            EdgeTags("direction:out", $"topic:{Queue}"),
            EdgeTags("direction:in", $"topic:{Queue}"),
            EdgeTags("direction:out", $"topic:{Topic}"),
            EdgeTags("direction:in", $"topic:{Subscription}"),
        };
        var payloads = await agent.WaitForDataStreamsAsync(
            timeoutInMilliseconds: 20_000,
            waitFunc: received => expectedEdges.All(edge => Points(received).Any(p => p.EdgeTags.SequenceEqual(edge))));
        var points = Points(payloads);

        var queueProduce = SinglePoint(points, "direction:out", $"topic:{Queue}");
        var queueConsume = SinglePoint(points, "direction:in", $"topic:{Queue}");
        var topicProduce = SinglePoint(points, "direction:out", $"topic:{Topic}");
        var subscriptionConsume = SinglePoint(points, "direction:in", $"topic:{Subscription}");

        queueProduce.ParentHash.Should().Be(0);
        topicProduce.ParentHash.Should().Be(0);
        queueConsume.ParentHash.Should().Be(queueProduce.Hash);
        subscriptionConsume.ParentHash.Should().Be(topicProduce.Hash);
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.AzureServiceBusLegacy), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    public async Task InjectsNothingWhenDataStreamsDisabled(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "0");

        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

        processResult.StandardOutput.Should().Contain("Messages with dd-pathway-ctx-base64: 0 of 5");
        agent.DataStreams.Should().BeEmpty();
    }

    private static string[] EdgeTags(string direction, string topic) => [direction, topic, "type:servicebus"];

    private static List<MockDataStreamsStatsPoint> Points(IEnumerable<MockDataStreamsPayload> payloads)
        => payloads.SelectMany(p => p.Stats).SelectMany(b => b.Stats ?? []).ToList();

    private static MockDataStreamsStatsPoint SinglePoint(IEnumerable<MockDataStreamsStatsPoint> points, string direction, string topic)
    {
        var expectedTags = EdgeTags(direction, topic);
        var matching = points.Where(p => p.EdgeTags.SequenceEqual(expectedTags))
                             .GroupBy(p => (p.Hash, p.ParentHash))
                             .Select(g => g.First())
                             .ToList();
        matching.Should().ContainSingle($"one pathway is expected for edge tags {string.Join(",", expectedTags)}");
        return matching.Single();
    }
}
