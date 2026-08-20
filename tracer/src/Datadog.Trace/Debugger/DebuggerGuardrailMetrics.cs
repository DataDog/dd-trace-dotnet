// <copyright file="DebuggerGuardrailMetrics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Debugger;

/// <summary>
/// Records Dynamic Instrumentation guardrail metrics through the existing bounded telemetry collectors.
/// Capture incomplete reasons are accumulated in a per-snapshot bitset so the inner object walk
/// only does a bitwise OR, and each distinct reason is flushed once after the event is retained.
/// A null event type means the caller shares the snapshot pipeline but is not Dynamic Instrumentation
/// (Exception Replay), so it is excluded from these metrics.
/// </summary>
internal static class DebuggerGuardrailMetrics
{
    internal static void RecordEventsSkipped(ProbeType probeType, MetricTags.DebuggerEventsSkippedReason reason)
    {
        if (!TryGetSkippedEventType(probeType, out var eventType))
        {
            return;
        }

        TelemetryFactory.Metrics.RecordCountDebuggerEventsSkipped(reason, eventType);
    }

    internal static void RecordEventsDropped(MetricTags.DebuggerCaptureEventType? eventType, MetricTags.DebuggerEventsDroppedReason reason)
    {
        if (eventType is { } type)
        {
            TelemetryFactory.Metrics.RecordCountDebuggerEventsDropped(reason, type);
        }
    }

    internal static void MarkCaptureIncomplete(ref uint flags, MetricTags.DebuggerCaptureIncompleteReason reason)
    {
        flags |= 1u << (int)reason;
    }

    internal static void RecordCaptureIncomplete(MetricTags.DebuggerCaptureEventType? eventType, uint flags)
    {
        if (eventType is not { } type || flags == 0)
        {
            return;
        }

        // Walk only the bits that are set, so adding a reason cannot fall outside the loop bounds.
        for (var reason = 0; flags != 0; reason++, flags >>= 1)
        {
            if ((flags & 1) != 0)
            {
                TelemetryFactory.Metrics.RecordCountDebuggerCaptureIncomplete(type, (MetricTags.DebuggerCaptureIncompleteReason)reason);
            }
        }
    }

    private static bool TryGetSkippedEventType(ProbeType probeType, out MetricTags.DebuggerEventType eventType)
    {
        switch (probeType)
        {
            case ProbeType.Snapshot:
                eventType = MetricTags.DebuggerEventType.Snapshot;
                return true;
            case ProbeType.Log:
                eventType = MetricTags.DebuggerEventType.Log;
                return true;
            default:
                // The .NET tracer currently reports skipped events only for snapshot and log probes.
                eventType = default;
                return false;
        }
    }
}
