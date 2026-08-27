// <copyright file="OtelThreadContextRecord.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// Reads and writes the OTEP 4947 <i>Thread-Local Context Record</i>. This is the only type that knows
/// the byte layout of the record, which is normative and shared with every other reader and writer of
/// the format (libdatadog's <c>libdd-otel-thread-ctx</c>, the Java profiler, the OpenTelemetry eBPF profiler).
/// <para>
/// The record is byte-packed with no padding. Multi-byte scalars use native (host) endianness, while the
/// trace id and span id are stored in W3C Trace Context format, i.e. big endian.
/// </para>
/// <code>
/// offset size  field
///      0   16  trace-id            (W3C format, big endian; all zeroes means "no trace")
///     16    8  span-id             (W3C format, big endian)
///     24    1  valid               (1 when the record may be read, anything else means "ignore me")
///     25    1  trace-flags         (W3C trace-flags byte)
///     26    2  attrs-data-size     (number of meaningful bytes in attrs-data)
///     28    2  attrs-data[0] key + length
///     30   16  attrs-data[0] value (local root span id, 16 lower-case hex characters)
/// </code>
/// <para>
/// We publish exactly one attribute, at key index 0. That index is the cross-language convention for
/// <c>datadog.local_root_span_id</c> (libdatadog's <c>ROOT_SPAN_KEY_INDEX</c>, the Java profiler's
/// <c>LOCAL_ROOT_SPAN_ATTR_INDEX</c>), which keeps our records byte-compatible with the other Datadog writers.
/// </para>
/// </summary>
internal static unsafe class OtelThreadContextRecord
{
    /// <summary>
    /// Total size of the record. Only the first <c>28 + <see cref="AttrsDataSize"/></c> bytes are meaningful,
    /// but the spec recommends 640 bytes as the total size because that is the fixed window the
    /// OpenTelemetry eBPF profiler reads, so the whole block is allocated to keep that read in bounds.
    /// </summary>
    public const int Size = 640;

    /// <summary>
    /// Alignment of the record. The spec only requires 2 bytes; we use a cache line so that the
    /// meaningful prefix of the record never straddles two lines.
    /// </summary>
    public const int Alignment = 64;

    private const int TraceIdOffset = 0;
    private const int SpanIdOffset = 16;
    private const int ValidOffset = 24;
    private const int TraceFlagsOffset = 25;
    private const int AttrsDataSizeOffset = 26;
    private const int AttrsDataOffset = 28;

    private const byte Invalid = 0;
    private const byte Valid = 1;

    /// <summary>
    /// Key index of <c>datadog.local_root_span_id</c> in the process context's <c>threadlocal.attribute_key_map</c>.
    /// </summary>
    private const byte LocalRootSpanIdKeyIndex = 0;

    /// <summary>
    /// Length of the local root span id value: a 64-bit id rendered as hexadecimal characters.
    /// </summary>
    private const byte LocalRootSpanIdLength = sizeof(ulong) * 2;

    /// <summary>
    /// The one attribute we publish never changes size, so attrs-data-size is a constant: one 2-byte
    /// header plus the 16-character value. Because it never changes, the spec's rules for growing and
    /// shrinking attrs-data do not apply to us.
    /// </summary>
    private const ushort AttrsDataSize = 2 + LocalRootSpanIdLength;

    /// <summary>
    /// Prepares a freshly rented block: zeroes it, then writes the parts of the record that never change.
    /// The record is left invalid, so a reader that sees it before the first <see cref="Write"/> reports
    /// "no context" rather than a half-written one.
    /// </summary>
    public static void Initialize(byte* record)
    {
        var span = AsSpan(record);
        span.Clear();

        span[AttrsDataOffset] = LocalRootSpanIdKeyIndex;
        span[AttrsDataOffset + 1] = LocalRootSpanIdLength;
        // attrs-data-size is a native-endianness scalar, unlike the trace and span ids below
        var attrsDataSize = AttrsDataSize;
        MemoryMarshal.Write(span.Slice(AttrsDataSizeOffset), ref attrsDataSize);
    }

    /// <summary>
    /// Publishes a trace context into the record.
    /// <para>
    /// Only the owning thread ever writes the record, and readers are required to observe it while that
    /// thread is stopped or interrupted, so there is no cross-thread race to guard against - the only
    /// hazard is reordering. Clearing <c>valid</c> first, and setting it last, means a reader that samples
    /// mid-update sees an invalid record and skips it instead of reading a torn one.
    /// </para>
    /// </summary>
    public static void Write(byte* record, TraceId traceId, ulong spanId, ulong localRootSpanId, byte traceFlags)
    {
        var span = AsSpan(record);

        Volatile.Write(ref span[ValidOffset], Invalid);

        // trace id and span id are big endian, per the W3C Trace Context format. Note that Upper is
        // always the most significant half regardless of machine endianness, so it is written first.
        BinaryPrimitives.WriteUInt64BigEndian(span.Slice(TraceIdOffset), traceId.Upper);
        BinaryPrimitives.WriteUInt64BigEndian(span.Slice(TraceIdOffset + sizeof(ulong)), traceId.Lower);
        BinaryPrimitives.WriteUInt64BigEndian(span.Slice(SpanIdOffset), spanId);
        span[TraceFlagsOffset] = traceFlags;
        HexString.ToHexBytes(localRootSpanId, span.Slice(AttrsDataOffset + 2));

        Volatile.Write(ref span[ValidOffset], Valid);
    }

    /// <summary>
    /// Marks the record as carrying no context. Per the spec this is a valid way to detach: the alternative
    /// is clearing the thread-local pointer, and a writer must pick one mechanism or the other, not both.
    /// We own a fixed record per thread, so we always use the flag.
    /// </summary>
    public static void Invalidate(byte* record)
    {
        Volatile.Write(ref AsSpan(record)[ValidOffset], Invalid);
    }

    private static Span<byte> AsSpan(byte* record) => new(record, Size);
}
