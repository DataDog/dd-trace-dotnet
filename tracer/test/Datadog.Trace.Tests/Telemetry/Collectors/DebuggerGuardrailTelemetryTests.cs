// <copyright file="DebuggerGuardrailTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Collectors;

public class DebuggerGuardrailTelemetryTests
{
    private static readonly string[] SkippedEventTypeTags = ["event_type:snapshot", "event_type:log", "event_type:metric", "event_type:span"];
    private static readonly string[] CaptureEventTypeTags = ["event_type:snapshot", "event_type:log"];
    private static readonly string[] EventsSkippedReasonTags = ["reason:rateLimitGlobal", "reason:rateLimitProbe", "reason:evaluationTimeout"];
    private static readonly string[] EventsDroppedReasonTags = ["reason:queueFull", "reason:payloadTooLarge"];
    private static readonly string[] CaptureIncompleteReasonTags = ["reason:runtimeError", "reason:timeout", "reason:depth", "reason:fieldCount", "reason:collectionSize", "reason:stringLength", "reason:payloadTooLarge", "reason:other"];

    [Fact]
    public async Task MetricsHaveExpectedContractAndAllCombinations()
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);

        foreach (var reason in GetEnumValues<MetricTags.DebuggerEventsSkippedReason>())
        {
            foreach (var eventType in GetEnumValues<MetricTags.DebuggerEventType>())
            {
                collector.RecordCountDebuggerEventsSkipped(reason, eventType);
            }
        }

        foreach (var reason in GetEnumValues<MetricTags.DebuggerEventsDroppedReason>())
        {
            foreach (var eventType in GetEnumValues<MetricTags.DebuggerCaptureEventType>())
            {
                collector.RecordCountDebuggerEventsDropped(reason, eventType);
            }
        }

        foreach (var eventType in GetEnumValues<MetricTags.DebuggerCaptureEventType>())
        {
            foreach (var reason in GetEnumValues<MetricTags.DebuggerCaptureIncompleteReason>())
            {
                collector.RecordCountDebuggerCaptureIncomplete(eventType, reason);
            }
        }

        collector.AggregateMetrics();
        var metrics = collector.GetMetrics().Metrics!;

        using (new AssertionScope())
        {
            metrics.Should().HaveCount(32);
            metrics.Should().OnlyContain(
                metric => metric.Namespace == MetricNamespaceConstants.Debugger
                       && metric.Common
                       && metric.Type == TelemetryMetricType.Count
                       && metric.Points.Count == 1
                       && metric.Points[0].Value == 1);

            GetTagCombinations(metrics, "events.skipped")
               .Should()
               .BeEquivalentTo(GetExpectedCombinations(EventsSkippedReasonTags, SkippedEventTypeTags));
            GetTagCombinations(metrics, "events.dropped")
               .Should()
               .BeEquivalentTo(GetExpectedCombinations(EventsDroppedReasonTags, CaptureEventTypeTags));
            GetTagCombinations(metrics, "capture.incomplete")
               .Should()
               .BeEquivalentTo(GetExpectedCombinations(CaptureEventTypeTags, CaptureIncompleteReasonTags));
        }

        await collector.DisposeAsync();
    }

    private static T[] GetEnumValues<T>()
        => Enum.GetValues(typeof(T)).Cast<T>().ToArray();

    private static IEnumerable<string> GetExpectedCombinations(string[] firstTags, string[] secondTags)
    {
        for (var i = 0; i < firstTags.Length; i++)
        {
            for (var j = 0; j < secondTags.Length; j++)
            {
                yield return string.Concat(firstTags[i], ";", secondTags[j]);
            }
        }
    }

    private static IEnumerable<string> GetTagCombinations(IEnumerable<MetricData> metrics, string metricName)
        => metrics.Where(metric => metric.Metric == metricName).Select(metric => string.Join(";", metric.Tags!));
}
