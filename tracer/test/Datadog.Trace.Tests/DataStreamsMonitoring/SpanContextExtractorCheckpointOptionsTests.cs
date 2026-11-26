// <copyright file="SpanContextExtractorCheckpointOptionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class SpanContextExtractorCheckpointOptionsTests
{
    [Fact]
    public async Task CarrierWithoutTraceHeaders_StillSetsTheCheckpoint()
    {
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.DataStreamsMonitoring.Enabled, "true" },
                { ConfigurationKeys.Environment, "foo" },
                { ConfigurationKeys.ServiceName, "bar" },
                { ConfigurationKeys.PropagateProcessTags, "false" },
            });
        await using var tracer = TracerHelper.Create(settings);
        var sourceMs = DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeMilliseconds();
        var emptyRow = new Dictionary<string, string>();

        var extracted = SpanContextExtractor.ExtractWithCheckpointOptions(
            tracer,
            emptyRow,
            static (carrier, key) => carrier.TryGetValue(key, out var value) ? [value] : [],
            messageType: "sqlserver",
            source: "replica-db",
            timestampMs: 0,
            sourceTimestampMs: sourceMs,
            chainFromPrevious: false);

        extracted.Should().BeNull();

        // The produce that follows (e.g. the auto-instrumented Kafka produce) links to the recorded checkpoint.
        var produce = tracer.TracerManager.DataStreamsManager.SetCheckpoint(parentPathway: null, CheckpointKind.Produce, new EdgeTags(["direction:out", "topic:orders", "type:kafka"]), payloadSizeBytes: 0, timeInQueueMs: 0);
        produce.Should().NotBeNull();
        produce!.Value.PathwayStart.Should().Be(sourceMs * 1_000_000);
    }
}
