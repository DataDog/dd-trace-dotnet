// <copyright file="SnapshotSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Tests.Telemetry;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

[Collection(nameof(TelemetryFactoryTests))]
public class SnapshotSinkTests
{
    [Fact]
    public void CaptureIncompleteReasons_FitInBitset()
    {
        foreach (var reason in (MetricTags.DebuggerCaptureIncompleteReason[])Enum.GetValues(typeof(MetricTags.DebuggerCaptureIncompleteReason)))
        {
            Assert.InRange((int)reason, 0, (sizeof(uint) * 8) - 1);
        }
    }

    [Fact]
    public void Add_WhenQueueIsFull_RecordsEventsDroppedQueueFull()
    {
        using var metricsScope = DebuggerGuardrailMetricTestHelpers.OverrideMetrics(out var collector);
        var sink = CreateSink(queueLimit: 1, MetricTags.DebuggerCaptureEventType.Log);

        sink.Add("probe-1", """{"debugger":{"snapshot":{}}}""");
        sink.Add(
            "probe-1",
            """{"debugger":{"snapshot":{}}}""",
            IncompleteReasons(MetricTags.DebuggerCaptureIncompleteReason.Depth));

        var queued = sink.GetSnapshots();
        Assert.Single(queued);
        collector.AssertHasCount("events.dropped", "reason:queueFull", "event_type:log");
        collector.AssertDoesNotHave("capture.incomplete");
    }

    [Fact]
    public void Add_WhenSlicerPrunesIncompletePayload_RecordsCombinedReasonsAfterEnqueue()
    {
        using var metricsScope = DebuggerGuardrailMetricTestHelpers.OverrideMetrics(out var collector);
        var payload = new string('x', 400);
        var snapshot = "{\"debugger\":{\"snapshot\":{\"captures\":{\"entry\":{\"fields\":{\"big\":{\"type\":\"String\",\"value\":\"" + payload + "\",\"notCapturedReason\":\"depth\"}}}}}}}";
        var settings = new DebuggerSettings(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.MaxDepthToSerialize, "0" } }),
            NullConfigurationTelemetry.Instance);
        var slicer = SnapshotSlicer.Create(settings, maxSnapshotSize: snapshot.Length - 50);
        var sink = new SnapshotSink(batchSize: 10, slicer, MetricTags.DebuggerCaptureEventType.Snapshot, queueLimit: 10);

        sink.Add(
            "probe-1",
            snapshot,
            IncompleteReasons(MetricTags.DebuggerCaptureIncompleteReason.Depth));

        Assert.Single(sink.GetSnapshots());
        collector.AssertHasCounts("capture.incomplete", "event_type:snapshot", "reason:payloadTooLarge", "reason:depth");
        collector.AssertDoesNotHave("events.dropped");
    }

    [Fact]
    public void Add_WhenPayloadIsExactlyAtLimit_DropsAndDoesNotEnqueue()
    {
        using var metricsScope = DebuggerGuardrailMetricTestHelpers.OverrideMetrics(out var collector);
        var settings = new DebuggerSettings(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.MaxDepthToSerialize, "1" } }),
            NullConfigurationTelemetry.Instance);
        var slicer = SnapshotSlicer.Create(settings, maxSnapshotSize: 16);
        var sink = new SnapshotSink(batchSize: 10, slicer, MetricTags.DebuggerCaptureEventType.Snapshot, queueLimit: 10);

        sink.Add(
            "probe-1",
            new string('x', 16),
            IncompleteReasons(MetricTags.DebuggerCaptureIncompleteReason.Depth));

        Assert.Empty(sink.GetSnapshots());
        collector.AssertHasCount("events.dropped", "reason:payloadTooLarge", "event_type:snapshot");
        collector.AssertDoesNotHave("capture.incomplete");
    }

    [Fact]
    public void Add_WhenProductIsNotDynamicInstrumentation_DoesNotRecordGuardrailMetrics()
    {
        using var metricsScope = DebuggerGuardrailMetricTestHelpers.OverrideMetrics(out var collector);
        var sink = CreateSink(queueLimit: 1, eventType: null);

        sink.Add(
            "probe-1",
            """{"debugger":{"snapshot":{}}}""",
            IncompleteReasons(MetricTags.DebuggerCaptureIncompleteReason.StringLength));
        sink.Add("probe-1", """{"debugger":{"snapshot":{}}}""");

        Assert.Single(sink.GetSnapshots());
        collector.AssertDoesNotHave("capture.incomplete");
        collector.AssertDoesNotHave("events.dropped");
    }

    private static SnapshotSink CreateSink(int queueLimit, MetricTags.DebuggerCaptureEventType? eventType)
    {
        var settings = new DebuggerSettings(new NameValueConfigurationSource(new()), NullConfigurationTelemetry.Instance);
        var slicer = SnapshotSlicer.Create(settings);
        return new SnapshotSink(batchSize: 10, slicer, eventType, queueLimit);
    }

    private static uint IncompleteReasons(MetricTags.DebuggerCaptureIncompleteReason reason) => 1u << (int)reason;
}
