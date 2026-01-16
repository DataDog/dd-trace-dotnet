// <copyright file="TracesData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Traces;

/// <summary>
/// SpanKind is the type of span. Can be used to specify additional relationships between spans
/// in addition to a parent/child relationship.
/// Corresponds to opentelemetry.proto.trace.v1.Span.SpanKind
/// </summary>
internal enum SpanKind
{
    /// <summary>
    /// Unspecified. Do NOT use as default.
    /// Implementations MAY assume SpanKind to be INTERNAL when receiving UNSPECIFIED.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Indicates that the span represents an internal operation within an application,
    /// as opposed to an operation happening at the boundaries. Default value.
    /// </summary>
    Internal = 1,

    /// <summary>
    /// Indicates that the span covers server-side handling of an RPC or other
    /// remote network request.
    /// </summary>
    Server = 2,

    /// <summary>
    /// Indicates that the span describes a request to some remote service.
    /// </summary>
    Client = 3,

    /// <summary>
    /// Indicates that the span describes a producer sending a message to a broker.
    /// Unlike CLIENT and SERVER, there is often no direct critical path latency relationship
    /// between producer and consumer spans.
    /// </summary>
    Producer = 4,

    /// <summary>
    /// Indicates that the span describes consumer receiving a message from a broker.
    /// Like the PRODUCER kind, there is often no direct critical path latency relationship
    /// between producer and consumer spans.
    /// </summary>
    Consumer = 5
}

/// <summary>
/// Status code enum.
/// For the semantics of status codes see
/// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-status
/// Corresponds to opentelemetry.proto.trace.v1.Status.StatusCode
/// </summary>
internal enum StatusCode
{
    /// <summary>
    /// The default status.
    /// </summary>
    Unset = 0,

    /// <summary>
    /// The Span has been validated by an Application developer or Operator to
    /// have completed successfully.
    /// </summary>
    Ok = 1,

    /// <summary>
    /// The Span contains an error.
    /// </summary>
    Error = 2
}

/// <summary>
/// TracesData represents the traces data that can be stored in a persistent storage,
/// OR can be embedded by other protocols that transfer OTLP traces data but do
/// not implement the OTLP protocol.
/// Corresponds to opentelemetry.proto.trace.v1.TracesData
/// </summary>
internal readonly struct TracesData
{
    /// <summary>
    /// An array of ResourceSpans.
    /// For data coming from a single resource this array will typically contain
    /// one element. Intermediary nodes that receive data from multiple origins
    /// typically batch the data before forwarding further and in that case this
    /// array will contain multiple elements.
    /// Field 1: repeated ResourceSpans resource_spans
    /// </summary>
    public readonly ResourceSpans ResourceSpans;

    public TracesData(ResourceSpans resourceSpans)
    {
        ResourceSpans = resourceSpans;
    }
}

/// <summary>
/// A collection of ScopeSpans from a Resource.
/// Corresponds to opentelemetry.proto.trace.v1.ResourceSpans
/// </summary>
internal readonly struct ResourceSpans
{
    /// <summary>
    /// The resource for the spans in this message.
    /// If this field is not set then no resource info is known.
    /// Field 1: opentelemetry.proto.resource.v1.Resource resource
    /// </summary>
    public readonly OtlpResource? Resource;

    /// <summary>
    /// A list of ScopeSpans that originate from a resource.
    /// Field 2: repeated ScopeSpans scope_spans
    /// </summary>
    public readonly IReadOnlyList<ScopeSpans> ScopeSpans;

    /// <summary>
    /// The Schema URL, if known. This is the identifier of the Schema that the resource data
    /// is recorded in. To learn more about Schema URL see
    /// https://opentelemetry.io/docs/specs/otel/schemas/#schema-url
    /// This schema_url applies to the data in the "resource" field. It does not apply
    /// to the data in the "scope_spans" field which have their own schema_url field.
    /// Field 3: string schema_url
    /// </summary>
    public readonly string? SchemaUrl;

    public ResourceSpans(OtlpResource? resource, IReadOnlyList<ScopeSpans> scopeSpans, string? schemaUrl = null)
    {
        Resource = resource;
        ScopeSpans = scopeSpans;
        SchemaUrl = schemaUrl;
    }
}

/// <summary>
/// A collection of Spans produced by an InstrumentationScope.
/// Corresponds to opentelemetry.proto.trace.v1.ScopeSpans
/// </summary>
internal readonly struct ScopeSpans
{
    /// <summary>
    /// The instrumentation scope information for the spans in this message.
    /// Semantically when InstrumentationScope isn't set, it is equivalent with
    /// an empty instrumentation scope name (unknown).
    /// Field 1: opentelemetry.proto.common.v1.InstrumentationScope scope
    /// </summary>
    public readonly InstrumentationScope? Scope;

    /// <summary>
    /// A list of Spans that originate from an instrumentation scope.
    /// Field 2: repeated Span spans
    /// </summary>
    public readonly IReadOnlyList<OtlpSpan> Spans;

    /// <summary>
    /// The Schema URL, if known. This is the identifier of the Schema that the span data
    /// is recorded in. To learn more about Schema URL see
    /// https://opentelemetry.io/docs/specs/otel/schemas/#schema-url
    /// This schema_url applies to the data in the "scope" field and all spans and span
    /// events in the "spans" field.
    /// Field 3: string schema_url
    /// </summary>
    public readonly string? SchemaUrl;

    public ScopeSpans(InstrumentationScope? scope, IReadOnlyList<OtlpSpan> spans, string? schemaUrl = null)
    {
        Scope = scope;
        Spans = spans;
        SchemaUrl = schemaUrl;
    }
}

/// <summary>
/// A Span represents a single operation performed by a single component of the system.
/// Corresponds to opentelemetry.proto.trace.v1.Span
/// </summary>
internal readonly struct OtlpSpan
{
    /// <summary>
    /// A unique identifier for a trace. All spans from the same trace share
    /// the same trace_id. The ID is a 16-byte array. An ID with all zeroes OR
    /// of length other than 16 bytes is considered invalid.
    /// Field 1: bytes trace_id (REQUIRED)
    /// </summary>
    public readonly byte[] TraceId;

    /// <summary>
    /// A unique identifier for a span within a trace, assigned when the span
    /// is created. The ID is an 8-byte array. An ID with all zeroes OR of length
    /// other than 8 bytes is considered invalid.
    /// Field 2: bytes span_id (REQUIRED)
    /// </summary>
    public readonly byte[] SpanId;

    /// <summary>
    /// trace_state conveys information about request position in multiple distributed tracing graphs.
    /// It is a trace_state in w3c-trace-context format: https://www.w3.org/TR/trace-context/#tracestate-header
    /// Field 3: string trace_state
    /// </summary>
    public readonly string? TraceState;

    /// <summary>
    /// The span_id of this span's parent span. If this is a root span, then this
    /// field must be empty. The ID is an 8-byte array.
    /// Field 4: bytes parent_span_id
    /// </summary>
    public readonly byte[]? ParentSpanId;

    /// <summary>
    /// Flags, a bit field.
    /// Bits 0-7 (8 least significant bits) are the trace flags as defined in W3C Trace
    /// Context specification.
    /// Bits 8 and 9 represent the 3 states of whether a span's parent is remote.
    /// Field 16: fixed32 flags
    /// </summary>
    public readonly uint Flags;

    /// <summary>
    /// A description of the span's operation.
    /// For example, the name can be a qualified method name or a file name
    /// and a line number where the operation is called.
    /// This field is semantically required to be set to non-empty string.
    /// Field 5: string name (REQUIRED)
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// Distinguishes between spans generated in a particular context.
    /// Field 6: SpanKind kind
    /// </summary>
    public readonly SpanKind Kind;

    /// <summary>
    /// The start time of the span.
    /// Value is UNIX Epoch time in nanoseconds since 00:00:00 UTC on 1 January 1970.
    /// This field is semantically required and it is expected that end_time >= start_time.
    /// Field 7: fixed64 start_time_unix_nano
    /// </summary>
    public readonly ulong StartTimeUnixNano;

    /// <summary>
    /// The end time of the span.
    /// Value is UNIX Epoch time in nanoseconds since 00:00:00 UTC on 1 January 1970.
    /// This field is semantically required and it is expected that end_time >= start_time.
    /// Field 8: fixed64 end_time_unix_nano
    /// </summary>
    public readonly ulong EndTimeUnixNano;

    /// <summary>
    /// A collection of key/value pairs.
    /// Attribute keys MUST be unique (it is not allowed to have more than one
    /// attribute with the same key).
    /// Field 9: repeated opentelemetry.proto.common.v1.KeyValue attributes
    /// </summary>
    public readonly IReadOnlyList<KeyValue>? Attributes;

    /// <summary>
    /// The number of attributes that were discarded. Attributes
    /// can be discarded because their keys are too long or because there are too many
    /// attributes. If this value is 0, then no attributes were dropped.
    /// Field 10: uint32 dropped_attributes_count
    /// </summary>
    public readonly uint DroppedAttributesCount;

    /// <summary>
    /// A collection of Event items.
    /// Field 11: repeated Event events
    /// </summary>
    public readonly IReadOnlyList<SpanEvent>? Events;

    /// <summary>
    /// The number of dropped events. If the value is 0, then no events were dropped.
    /// Field 12: uint32 dropped_events_count
    /// </summary>
    public readonly uint DroppedEventsCount;

    /// <summary>
    /// A collection of Links, which are references from this span to a span
    /// in the same or different trace.
    /// Field 13: repeated Link links
    /// </summary>
    public readonly IReadOnlyList<SpanLink>? Links;

    /// <summary>
    /// The number of dropped links after the maximum size was enforced.
    /// If this value is 0, then no links were dropped.
    /// Field 14: uint32 dropped_links_count
    /// </summary>
    public readonly uint DroppedLinksCount;

    /// <summary>
    /// An optional final status for this span. Semantically when Status isn't set, it means
    /// span's status code is unset, i.e. assume STATUS_CODE_UNSET (code = 0).
    /// Field 15: Status status
    /// </summary>
    public readonly SpanStatus? Status;

    public OtlpSpan(
        byte[] traceId,
        byte[] spanId,
        string name,
        SpanKind kind,
        ulong startTimeUnixNano,
        ulong endTimeUnixNano,
        string? traceState = null,
        byte[]? parentSpanId = null,
        uint flags = 0,
        IReadOnlyList<KeyValue>? attributes = null,
        uint droppedAttributesCount = 0,
        IReadOnlyList<SpanEvent>? events = null,
        uint droppedEventsCount = 0,
        IReadOnlyList<SpanLink>? links = null,
        uint droppedLinksCount = 0,
        SpanStatus? status = null)
    {
        TraceId = traceId;
        SpanId = spanId;
        Name = name;
        Kind = kind;
        StartTimeUnixNano = startTimeUnixNano;
        EndTimeUnixNano = endTimeUnixNano;
        TraceState = traceState;
        ParentSpanId = parentSpanId;
        Flags = flags;
        Attributes = attributes;
        DroppedAttributesCount = droppedAttributesCount;
        Events = events;
        DroppedEventsCount = droppedEventsCount;
        Links = links;
        DroppedLinksCount = droppedLinksCount;
        Status = status;
    }
}

/// <summary>
/// Event is a time-stamped annotation of the span, consisting of user-supplied
/// text description and key-value pairs.
/// Corresponds to opentelemetry.proto.trace.v1.Span.Event
/// </summary>
internal readonly struct SpanEvent
{
    /// <summary>
    /// The time the event occurred.
    /// Field 1: fixed64 time_unix_nano
    /// </summary>
    public readonly ulong TimeUnixNano;

    /// <summary>
    /// The name of the event.
    /// This field is semantically required to be set to non-empty string.
    /// Field 2: string name
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// A collection of attribute key/value pairs on the event.
    /// Attribute keys MUST be unique (it is not allowed to have more than one
    /// attribute with the same key).
    /// Field 3: repeated opentelemetry.proto.common.v1.KeyValue attributes
    /// </summary>
    public readonly IReadOnlyList<KeyValue>? Attributes;

    /// <summary>
    /// The number of dropped attributes. If the value is 0, then no attributes were dropped.
    /// Field 4: uint32 dropped_attributes_count
    /// </summary>
    public readonly uint DroppedAttributesCount;

    public SpanEvent(ulong timeUnixNano, string name, IReadOnlyList<KeyValue>? attributes = null, uint droppedAttributesCount = 0)
    {
        TimeUnixNano = timeUnixNano;
        Name = name;
        Attributes = attributes;
        DroppedAttributesCount = droppedAttributesCount;
    }
}

/// <summary>
/// A pointer from the current span to another span in the same trace or in a
/// different trace. For example, this can be used in batching operations,
/// where a single batch handler processes multiple requests from different
/// traces or when the handler receives a request from a different project.
/// Corresponds to opentelemetry.proto.trace.v1.Span.Link
/// </summary>
internal readonly struct SpanLink
{
    /// <summary>
    /// A unique identifier of a trace that this linked span is part of. The ID is a
    /// 16-byte array.
    /// Field 1: bytes trace_id
    /// </summary>
    public readonly byte[] TraceId;

    /// <summary>
    /// A unique identifier for the linked span. The ID is an 8-byte array.
    /// Field 2: bytes span_id
    /// </summary>
    public readonly byte[] SpanId;

    /// <summary>
    /// The trace_state associated with the link.
    /// Field 3: string trace_state
    /// </summary>
    public readonly string? TraceState;

    /// <summary>
    /// A collection of attribute key/value pairs on the link.
    /// Attribute keys MUST be unique (it is not allowed to have more than one
    /// attribute with the same key).
    /// Field 4: repeated opentelemetry.proto.common.v1.KeyValue attributes
    /// </summary>
    public readonly IReadOnlyList<KeyValue>? Attributes;

    /// <summary>
    /// The number of dropped attributes. If the value is 0, then no attributes were dropped.
    /// Field 5: uint32 dropped_attributes_count
    /// </summary>
    public readonly uint DroppedAttributesCount;

    /// <summary>
    /// Flags, a bit field.
    /// Bits 0-7 (8 least significant bits) are the trace flags as defined in W3C Trace
    /// Context specification.
    /// Bits 8 and 9 represent the 3 states of whether the link is remote.
    /// Field 6: fixed32 flags
    /// </summary>
    public readonly uint Flags;

    public SpanLink(
        byte[] traceId,
        byte[] spanId,
        string? traceState = null,
        IReadOnlyList<KeyValue>? attributes = null,
        uint droppedAttributesCount = 0,
        uint flags = 0)
    {
        TraceId = traceId;
        SpanId = spanId;
        TraceState = traceState;
        Attributes = attributes;
        DroppedAttributesCount = droppedAttributesCount;
        Flags = flags;
    }
}

/// <summary>
/// The Status type defines a logical error model that is suitable for different
/// programming environments, including REST APIs and RPC APIs.
/// Corresponds to opentelemetry.proto.trace.v1.Status
/// </summary>
internal readonly struct SpanStatus
{
    /// <summary>
    /// A developer-facing human readable error message.
    /// Field 2: string message
    /// </summary>
    public readonly string? Message;

    /// <summary>
    /// The status code.
    /// Field 3: StatusCode code
    /// </summary>
    public readonly StatusCode Code;

    public SpanStatus(StatusCode code, string? message = null)
    {
        Code = code;
        Message = message;
    }
}

/// <summary>
/// Placeholder for opentelemetry.proto.resource.v1.Resource
/// This should be defined in a separate file or referenced from existing Resource struct.
/// </summary>
internal readonly struct OtlpResource
{
    public readonly IReadOnlyList<KeyValue>? Attributes;
    public readonly uint DroppedAttributesCount;

    public OtlpResource(IReadOnlyList<KeyValue>? attributes = null, uint droppedAttributesCount = 0)
    {
        Attributes = attributes;
        DroppedAttributesCount = droppedAttributesCount;
    }
}

/// <summary>
/// Placeholder for opentelemetry.proto.common.v1.InstrumentationScope
/// This should be defined in a separate file or referenced from existing InstrumentationScope struct.
/// </summary>
internal readonly struct InstrumentationScope
{
    public readonly string Name;
    public readonly string? Version;
    public readonly IReadOnlyList<KeyValue>? Attributes;
    public readonly uint DroppedAttributesCount;

    public InstrumentationScope(string name, string? version = null, IReadOnlyList<KeyValue>? attributes = null, uint droppedAttributesCount = 0)
    {
        Name = name;
        Version = version;
        Attributes = attributes;
        DroppedAttributesCount = droppedAttributesCount;
    }
}

/// <summary>
/// Placeholder for opentelemetry.proto.common.v1.KeyValue
/// This should be defined in a separate file or referenced from existing KeyValue struct.
/// </summary>
internal readonly struct KeyValue
{
    public readonly string Key;
    public readonly object? Value;

    public KeyValue(string key, object? value)
    {
        Key = key;
        Value = value;
    }
}

/// <summary>
/// SpanFlags represents constants used to interpret the Span.flags field.
/// Corresponds to opentelemetry.proto.trace.v1.SpanFlags
/// </summary>
internal static class SpanFlags
{
    /// <summary>
    /// The zero value for the enum. Should not be used for comparisons.
    /// Instead use bitwise "and" with the appropriate mask.
    /// </summary>
    public const uint DoNotUse = 0x00000000;

    /// <summary>
    /// Bits 0-7 are used for trace flags.
    /// </summary>
    public const uint TraceFlagsMask = 0x000000FF;

    /// <summary>
    /// Bit 8 indicates whether the value is known.
    /// </summary>
    public const uint ContextHasIsRemoteMask = 0x00000100;

    /// <summary>
    /// Bit 9 indicates whether the span or link is remote.
    /// </summary>
    public const uint ContextIsRemoteMask = 0x00000200;
}
