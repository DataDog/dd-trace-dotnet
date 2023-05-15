using System;
using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace.Agent.Events;

public abstract record SpanEvent(
    ulong TraceId,
    ulong SpanId);

public record StartSpanEvent(
        ulong TraceId,
        ulong SpanId,
        ulong ParentId,
        DateTimeOffset Timestamp,
        string Service,
        string Name,
        string Resource,
        string Type,
        ReadOnlyMemory<KeyValuePair<string, string>> Meta,
        ReadOnlyMemory<KeyValuePair<string, double>> Metrics)
    : SpanEvent(TraceId, SpanId);

public record FinishSpanEvent(
        ulong TraceId,
        ulong SpanId,
        DateTimeOffset Timestamp,
        ReadOnlyMemory<KeyValuePair<string, string>> Meta,
        ReadOnlyMemory<KeyValuePair<string, double>> Metrics)
    : SpanEvent(TraceId, SpanId);

public record AddTagsSpanEvent(
        ulong TraceId,
        ulong SpanId,
        ReadOnlyMemory<KeyValuePair<string, string>> Meta,
        ReadOnlyMemory<KeyValuePair<string, double>> Metrics)
    : SpanEvent(TraceId, SpanId);
