// <copyright file="KafkaLibraryMismatchTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[Collection(nameof(KafkaTests.KafkaTestsCollection))]
[Trait("RequiresDockerDependency", "true")]
[Trait("DockerGroup", "1")]
public class KafkaLibraryMismatchTests(ITestOutputHelper output) : TestHelper("Kafka.LibraryMismatch", output)
{
    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    public async Task PreservesInstrumentation(
        [CombinatorialValues("1.6.1", "2.2.0", "2.8.0")] string nativeVersion,
        bool producerFirst,
        bool enableDataStreams)
    {
        Skip.IfNot((EnvironmentTools.IsWindows() || EnvironmentTools.IsLinux()) && EnvironmentTools.GetTestTargetPlatform() == "X64", "The regression uses Windows and Linux x64 librdkafka assets");
#if NETCOREAPP2_1 || NETCOREAPP3_0
        Skip.If(nativeVersion != "2.8.0", "Explicit P/Invoke resolution requires .NET Core 3.1 or later");
#endif
        var topic = $"library-mismatch-{Guid.NewGuid():N}";
        var canConsume = nativeVersion != "1.6.1";
        var supportsClusterId = nativeVersion == "2.8.0";
        SetEnvironmentVariable("DD_TRACE_KAFKA_ENABLED", "true");
        // Unpatched runs should fail through the child process exit code without crash-reporting subprocesses.
        SetEnvironmentVariable("DD_CRASHTRACKING_ENABLED", "false");
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, enableDataStreams ? "1" : "0");
        SetEnvironmentVariable(ConfigurationKeys.KafkaCreateConsumerScopeEnabled, "1");
        SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", "v0");

        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var result = await RunSampleAndWaitForExit(agent, arguments: $"{nativeVersion} {producerFirst} {topic}");
        result.StandardOutput.Should().Contain($"Native version: {nativeVersion}")
              .And.Contain("Both clients constructed")
              .And.Contain("Message produced");

        var spans = await agent.WaitForSpansAsync(canConsume ? 2 : 1);
        var producer = spans.Should().ContainSingle(x => x.Name == "kafka.produce").Subject;
        producer.Error.Should().Be(0);
        if (canConsume)
        {
            result.StandardOutput.Should().Contain("Message produced, consumed, and committed");
            var consumer = spans.Should().ContainSingle(x => x.Name == "kafka.consume").Subject;
            consumer.Error.Should().Be(0);
            consumer.ParentId.Should().Be(producer.SpanId);
            consumer.TraceId.Should().Be(producer.TraceId);
            consumer.Tags[Tags.KafkaConsumerGroup].Should().Be(topic);
        }

        foreach (var span in spans.Where(x => x.Type == SpanTypes.Queue))
        {
            span.Tags[Tags.MessagingDestinationName].Should().Be(topic);
            if (!supportsClusterId)
            {
                span.Tags.Should().NotContainKey(Tags.KafkaClusterId);
            }
            else
            {
                span.Tags[Tags.KafkaClusterId].Should().NotBeNullOrEmpty();
            }
        }

        if (enableDataStreams)
        {
            agent.DataStreams.Should().NotBeEmpty();
            var buckets = agent.DataStreams.SelectMany(x => x.Stats).ToArray();
            var points = buckets.SelectMany(x => x.Stats ?? []).ToArray();
            points.Should().Contain(x => x.EdgeTags.Contains("direction:out"));
            var backlogs = buckets.SelectMany(x => x.Backlogs ?? []).ToArray();
            backlogs.Should().Contain(x => x.Tags.Contains("type:kafka_produce"));
            if (canConsume)
            {
                points.Should().Contain(x => x.EdgeTags.Contains("direction:in"));
                backlogs.Should().Contain(x => x.Tags.Contains("type:kafka_commit"));
            }

            foreach (var tags in points.Select(x => x.EdgeTags).Concat(backlogs.Select(x => x.Tags)))
            {
                tags.Any(x => x.StartsWith("kafka_cluster_id:", StringComparison.Ordinal)).Should().Be(supportsClusterId);
            }
        }

        await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Kafka);
    }
}
