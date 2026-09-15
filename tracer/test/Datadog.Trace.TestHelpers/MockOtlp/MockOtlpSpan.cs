// <copyright file="MockOtlpSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using Datadog.Trace.Util;

// See the comment in MockOtlpEvent.cs about why this alias is necessary.
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// A single OTLP span, decoded from either the JSON or protobuf wire format into one common,
/// typed representation. IDs are 32/16-character lowercase hex strings.
/// </summary>
public sealed class MockOtlpSpan
{
    private MockOtlpSpan(
        string traceId,
        string spanId,
        string parentSpanId,
        string traceState,
        uint flags,
        string name,
        OtlpSpan.Types.SpanKind kind,
        ulong startTimeUnixNano,
        ulong endTimeUnixNano,
        IImmutableList<MockOtlpAttribute> attributes,
        uint droppedAttributesCount,
        IImmutableList<MockOtlpEvent> events,
        uint droppedEventsCount,
        IImmutableList<MockOtlpLink> links,
        uint droppedLinksCount,
        MockOtlpStatus status)
    {
        TraceId = traceId;
        SpanId = spanId;
        ParentSpanId = parentSpanId;
        TraceState = traceState;
        Flags = flags;
        Name = name;
        Kind = kind;
        StartTimeUnixNano = startTimeUnixNano;
        EndTimeUnixNano = endTimeUnixNano;
        Attributes = attributes;
        DroppedAttributesCount = droppedAttributesCount;
        Events = events;
        DroppedEventsCount = droppedEventsCount;
        Links = links;
        DroppedLinksCount = droppedLinksCount;
        Status = status;
    }

    public string TraceId { get; }

    public string SpanId { get; }

    public string ParentSpanId { get; }

    public string TraceState { get; }

    public uint Flags { get; }

    public string Name { get; }

    public OtlpSpan.Types.SpanKind Kind { get; }

    public ulong StartTimeUnixNano { get; }

    public ulong EndTimeUnixNano { get; }

    public IImmutableList<MockOtlpAttribute> Attributes { get; }

    public uint DroppedAttributesCount { get; }

    public IImmutableList<MockOtlpEvent> Events { get; }

    public uint DroppedEventsCount { get; }

    public IImmutableList<MockOtlpLink> Links { get; }

    public uint DroppedLinksCount { get; }

    public MockOtlpStatus Status { get; }

    internal static MockOtlpSpan Create(OtlpSpan span)
        => new(
            HexString.ToHexString(span.TraceId.ToByteArray()),
            HexString.ToHexString(span.SpanId.ToByteArray()),
            span.ParentSpanId.Length == 0 ? string.Empty : HexString.ToHexString(span.ParentSpanId.ToByteArray()),
            span.TraceState,
            span.Flags,
            span.Name,
            span.Kind,
            span.StartTimeUnixNano,
            span.EndTimeUnixNano,
            span.Attributes.Select(MockOtlpAttribute.Create).ToImmutableList(),
            span.DroppedAttributesCount,
            span.Events.Select(MockOtlpEvent.Create).ToImmutableList(),
            span.DroppedEventsCount,
            span.Links.Select(MockOtlpLink.Create).ToImmutableList(),
            span.DroppedLinksCount,
            span.Status is null ? null : MockOtlpStatus.Create(span.Status));
}
