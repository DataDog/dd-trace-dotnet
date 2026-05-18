// <copyright file="OtlpTracesProtobufSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.OpenTelemetry.Common;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using static Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer.ProtobufOtlpCommonFieldNumberConstants;
using static Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer.ProtobufOtlpResourceFieldNumberConstants;
using static Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer.ProtobufOtlpTraceFieldNumberConstants;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Traces;

/// <summary>
/// OTLP protobuf serializer producing payloads compliant with
/// opentelemetry.proto.collector.trace.v1.ExportTraceServiceRequest.
/// This is done manually to avoid allocating intermediate objects when
/// serializing Datadog's data model into the protobuf wire format to
/// avoid any assembly references to Google.Protobuf.
/// See: https://github.com/open-telemetry/opentelemetry-proto/blob/v1.2.0/opentelemetry/proto/trace/v1/trace.proto.
/// </summary>
internal sealed class OtlpTracesProtobufSerializer : ISpanBufferSerializer
{
    internal const int SpanAttributeCountLimit = 128;
    internal const int EventCountLimit = 128;
    internal const int LinkCountLimit = 128;
    internal const int AttributePerEventCountLimit = 128;
    internal const int AttributePerLinkCountLimit = 128;

    private const int ReserveSizeForLength = 4;
    private const int TraceIdSize = 16;
    private const int SpanIdSize = 8;

    // Absolute positions of the length placeholders. INVARIANT: these are offsets
    // into the caller's eventual `_buffer` (the destination), NOT into the temporary
    // serialization buffer (`bytes` in SerializeSpans). This works because
    // `SpanBuffer.TryWrite` copies the temporary buffer's contents into `_buffer`
    // starting at `_offset`, which on the first chunk equals `spanBufferOffset`
    // (which on the first chunk equals `HeaderSize`).
    // Set on the first SerializeSpans call and patched in FinishBody.
    private int _resourceSpansLengthPos = -1;
    private int _scopeSpansLengthPos = -1;

    public int HeaderSize => 0;

    public void WriteHeader(ref byte[] bytes, int offset, int traceCount)
    {
        // No fixed header; the outer envelope is opened on the first SerializeSpans call.
    }

    public int SerializeSpans(ref byte[] bytes, int temporaryBufferOffset, TraceChunkModel traceChunk, int spanBufferOffset, int maxSize)
    {
        // Grow the caller's buffer up-front so per-field writes can't IndexOutOfRangeException.
        // Conservative upper bound: this single call won't write more than `maxSize` bytes;
        // if it does, we'll detect the overflow at the end and return 0.
        MessagePackBinary.EnsureCapacity(ref bytes, temporaryBufferOffset, maxSize);

        int writePosition = temporaryBufferOffset;

        // Snapshot length-position fields before mutating them, so we can roll back on overflow.
        int savedResourceSpansLengthPos = _resourceSpansLengthPos;
        int savedScopeSpansLengthPos = _scopeSpansLengthPos;

        if (spanBufferOffset == HeaderSize)
        {
            // Open ExportTraceServiceRequest -> ResourceSpans (field 1, LEN)
            writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, TracesData_Resource_Spans, ProtobufWireType.LEN);
            // Convert the absolute position in the temporary buffer to an absolute position in `_buffer`.
            _resourceSpansLengthPos = (writePosition - temporaryBufferOffset) + spanBufferOffset;
            writePosition += ReserveSizeForLength;

            // ResourceSpans.resource (field 1, LEN)
            writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, ResourceSpans_Resource, ProtobufWireType.LEN);
            int resourceLengthPos = writePosition;
            writePosition += ReserveSizeForLength;

            writePosition = WriteResourceAttributes(bytes, writePosition, in traceChunk);

            ProtobufSerializer.WriteReservedLength(bytes, resourceLengthPos, writePosition - (resourceLengthPos + ReserveSizeForLength));

            // ResourceSpans.scope_spans (field 2, LEN)
            writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, ResourceSpans_Scope_Spans, ProtobufWireType.LEN);
            // Convert the absolute position in the temporary buffer to an absolute position in `_buffer`.
            _scopeSpansLengthPos = (writePosition - temporaryBufferOffset) + spanBufferOffset;
            writePosition += ReserveSizeForLength;

            // Note: we intentionally skip emitting ScopeSpans.scope (instrumentation scope) to match the JSON serializer's behavior.
        }

        for (var i = 0; i < traceChunk.SpanCount; i++)
        {
            var spanModel = traceChunk.GetSpanModel(i);
            writePosition = WriteSpan(bytes, writePosition, in spanModel);
        }

        var bytesWritten = writePosition - temporaryBufferOffset;

        if (bytesWritten >= maxSize)
        {
            // We've reached or exceeded the maximum size; signal overflow to the caller.
            // Roll back length-position fields so a retry into a fresh buffer is unaffected.
            _resourceSpansLengthPos = savedResourceSpansLengthPos;
            _scopeSpansLengthPos = savedScopeSpansLengthPos;
            return 0;
        }

        return bytesWritten;
    }

    public int FinishBody(ref byte[] bytes, int offset, int maxSize)
    {
        if (_resourceSpansLengthPos < 0)
        {
            return 0;
        }

        // Edge case (unsure how this would happen): An initial SerializeSpans call succeeded
        // (returned a non-zero size but the buffer was not large enough and failed on SpanBuffer.EnsureCapacity.
        //
        // This would result in the buffer being full buth when this is called, the logic to patch
        // the resource spans length and scope spans length would result in a negative length.
        // Skip patching in this case by returning 0, so the resulting data would be empty.
        if (offset <= _resourceSpansLengthPos + ReserveSizeForLength)
        {
            _resourceSpansLengthPos = -1;
            _scopeSpansLengthPos = -1;
            return 0;
        }

        // Patch ScopeSpans length: from end of its 4-byte placeholder up to current write position (offset).
        var scopeSpansBodyLength = offset - (_scopeSpansLengthPos + ReserveSizeForLength);
        ProtobufSerializer.WriteReservedLength(bytes, _scopeSpansLengthPos, scopeSpansBodyLength);

        // Patch ResourceSpans length.
        var resourceSpansBodyLength = offset - (_resourceSpansLengthPos + ReserveSizeForLength);
        ProtobufSerializer.WriteReservedLength(bytes, _resourceSpansLengthPos, resourceSpansBodyLength);

        // Reset for next flush cycle (same serializer instance can be reused on the same buffer).
        _resourceSpansLengthPos = -1;
        _scopeSpansLengthPos = -1;
        return 0;
    }

    private static int WriteResourceAttributes(byte[] bytes, int writePosition, in TraceChunkModel traceChunk)
    {
        int pos = writePosition;
        OtlpMapper.EmitResourceAttributesFromTraceChunk(
            in traceChunk,
            kv => pos = WriteKeyValueAttribute(bytes, pos, Resource_Attributes, kv));
        return pos;
    }

    private static int WriteSpan(byte[] bytes, int writePosition, in SpanModel spanModel)
    {
        writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, ScopeSpans_Spans, ProtobufWireType.LEN);
        int spanLengthPos = writePosition;
        writePosition += ReserveSizeForLength;

        // trace_id (field 1, LEN, 16 bytes)
        writePosition = WriteTraceIdField(bytes, writePosition, Span_Trace_Id, spanModel.Span.Context.RawTraceId);

        // span_id (field 2, LEN, 8 bytes)
        writePosition = WriteSpanIdField(bytes, writePosition, Span_Span_Id, spanModel.Span.SpanId);

        // parent_span_id (field 4, LEN, 8 bytes) — only if parent exists and is non-zero
        if (spanModel.Span.Context.ParentId is ulong parentId && parentId > 0)
        {
            writePosition = WriteSpanIdField(bytes, writePosition, Span_Parent_Span_Id, parentId);
        }

        // name (field 5, string)
        writePosition = ProtobufSerializer.WriteStringWithTag(bytes, writePosition, Span_Name, spanModel.Span.ResourceName);

        // kind (field 6, enum)
        var spanKind = spanModel.Span.GetTag(Tags.SpanKind) switch
        {
            SpanKinds.Server => Span_Kind_Server,
            SpanKinds.Client => Span_Kind_Client,
            SpanKinds.Producer => Span_Kind_Producer,
            SpanKinds.Consumer => Span_Kind_Consumer,
            _ => Span_Kind_Internal,
        };
        writePosition = ProtobufSerializer.WriteEnumWithTag(bytes, writePosition, Span_Kind, spanKind);

        // start_time_unix_nano (field 7, fixed64)
        var startNs = (ulong)spanModel.Span.StartTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(bytes, writePosition, Span_Start_Time_Unix_Nano, startNs);

        // end_time_unix_nano (field 8, fixed64)
        var endNs = (ulong)(spanModel.Span.StartTime + spanModel.Span.Duration).ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(bytes, writePosition, Span_End_Time_Unix_Nano, endNs);

        // attributes (field 9, repeated KeyValue)
        int posCapture = writePosition;
        int droppedAttributes = OtlpMapper.EmitAttributesFromSpan(
            kv => posCapture = WriteKeyValueAttribute(bytes, posCapture, Span_Attributes, kv),
            in spanModel,
            SpanAttributeCountLimit);
        writePosition = posCapture;

        if (droppedAttributes > 0)
        {
            writePosition = ProtobufSerializer.WriteInt64WithTag(bytes, writePosition, Span_Dropped_Attributes_Count, (ulong)droppedAttributes);
        }

        // events (field 11, repeated Event)
        int droppedEvents = 0;
        if (spanModel.Span.SpanEvents is { Count: > 0 } events)
        {
            int written = 0;
            foreach (var evt in events)
            {
                if (written < EventCountLimit)
                {
                    writePosition = WriteSpanEvent(bytes, writePosition, evt);
                    written++;
                }
                else
                {
                    droppedEvents++;
                }
            }

            if (droppedEvents > 0)
            {
                writePosition = ProtobufSerializer.WriteInt64WithTag(bytes, writePosition, Span_Dropped_Events_Count, (ulong)droppedEvents);
            }
        }

        // links (field 13, repeated Link)
        int droppedLinks = 0;
        if (spanModel.Span.SpanLinks is { Count: > 0 } links)
        {
            int written = 0;
            foreach (var link in links)
            {
                if (written < LinkCountLimit)
                {
                    writePosition = WriteSpanLink(bytes, writePosition, link);
                    written++;
                }
                else
                {
                    droppedLinks++;
                }
            }

            if (droppedLinks > 0)
            {
                writePosition = ProtobufSerializer.WriteInt64WithTag(bytes, writePosition, Span_Dropped_Links_Count, (ulong)droppedLinks);
            }
        }

        // status (field 15)
        var errorMsg = spanModel.Span.GetTag(Tags.ErrorMsg);
        int? statusCode = spanModel.Span.GetTag("otel.status_code") switch
        {
            "STATUS_CODE_OK" => Status_Code_Ok,
            "STATUS_CODE_ERROR" => Status_Code_Error,
            _ => null,
        };
        if (statusCode is not null)
        {
            writePosition = WriteSpanStatus(bytes, writePosition, statusCode.Value, errorMsg);
        }

        // flags (field 16, fixed32) — only when sampling priority is known
        if (spanModel.Span.Context.SamplingPriority is int samplingPriority)
        {
            // Bit 0: trace flag for "sampled" (set when priority keeps the trace)
            uint flags = SamplingPriorityValues.IsKeep(samplingPriority) ? 1u : 0u;
            writePosition = ProtobufSerializer.WriteFixed32WithTag(bytes, writePosition, Span_Flags, flags);
        }

        // Patch the Span length
        ProtobufSerializer.WriteReservedLength(bytes, spanLengthPos, writePosition - (spanLengthPos + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteSpanEvent(byte[] bytes, int writePosition, Datadog.Trace.SpanEvent evt)
    {
        writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, Span_Events, ProtobufWireType.LEN);
        int lengthPos = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteFixed64WithTag(bytes, writePosition, Event_Time_Unix_Nano, (ulong)evt.Timestamp.ToUnixTimeNanoseconds());
        writePosition = ProtobufSerializer.WriteStringWithTag(bytes, writePosition, Event_Name, evt.Name);

        int dropped = 0;
        if (evt.Attributes is { Count: > 0 })
        {
            int count = 0;
            foreach (var kv in evt.Attributes)
            {
                if (count < AttributePerEventCountLimit)
                {
                    writePosition = WriteKeyValueAttribute(bytes, writePosition, Event_Attributes, new KeyValue(kv.Key, kv.Value));
                    count++;
                }
                else
                {
                    dropped++;
                }
            }
        }

        if (dropped > 0)
        {
            writePosition = ProtobufSerializer.WriteInt64WithTag(bytes, writePosition, Event_Dropped_Attributes_Count, (ulong)dropped);
        }

        ProtobufSerializer.WriteReservedLength(bytes, lengthPos, writePosition - (lengthPos + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteSpanLink(byte[] bytes, int writePosition, Datadog.Trace.SpanLink link)
    {
        writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, Span_Links, ProtobufWireType.LEN);
        int lengthPos = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = WriteTraceIdField(bytes, writePosition, Link_Trace_Id, link.Context.RawTraceId);
        writePosition = WriteSpanIdField(bytes, writePosition, Link_Span_Id, link.Context.SpanId);

        if (link.Context.IsRemote)
        {
            var traceState = W3CTraceContextPropagator.CreateTraceStateHeader(link.Context);
            if (!StringUtil.IsNullOrEmpty(traceState))
            {
                writePosition = ProtobufSerializer.WriteStringWithTag(bytes, writePosition, Link_Trace_State, traceState!);
            }
        }

        int dropped = 0;
        if (link.Attributes is { Count: > 0 })
        {
            int count = 0;
            foreach (var kv in link.Attributes)
            {
                if (count < AttributePerLinkCountLimit)
                {
                    writePosition = WriteKeyValueAttribute(bytes, writePosition, Link_Attributes, new KeyValue(kv.Key, kv.Value));
                    count++;
                }
                else
                {
                    dropped++;
                }
            }
        }

        if (dropped > 0)
        {
            writePosition = ProtobufSerializer.WriteInt64WithTag(bytes, writePosition, Link_Dropped_Attributes_Count, (ulong)dropped);
        }

        // flags (field 6, fixed32) — match JSON encoding behavior
        var samplingPriority = link.Context.TraceContext?.SamplingPriority ?? link.Context.SamplingPriority;
        uint traceFlags = samplingPriority switch
        {
            null => 0u,
            > 0 => 1u | (1u << 31),
            <= 0 => 1u << 31,
        };
        if (traceFlags > 0)
        {
            writePosition = ProtobufSerializer.WriteFixed32WithTag(bytes, writePosition, Link_Flags, traceFlags);
        }

        ProtobufSerializer.WriteReservedLength(bytes, lengthPos, writePosition - (lengthPos + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteSpanStatus(byte[] bytes, int writePosition, int statusCode, string? message)
    {
        writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, Span_Status, ProtobufWireType.LEN);
        int lengthPos = writePosition;
        writePosition += ReserveSizeForLength;

        if (!StringUtil.IsNullOrEmpty(message))
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(bytes, writePosition, Status_Message, message!);
        }

        writePosition = ProtobufSerializer.WriteEnumWithTag(bytes, writePosition, Status_Code, statusCode);

        ProtobufSerializer.WriteReservedLength(bytes, lengthPos, writePosition - (lengthPos + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteKeyValueAttribute(byte[] bytes, int writePosition, int fieldNumber, KeyValue kv)
    {
        writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, fieldNumber, ProtobufWireType.LEN);
        int lengthPos = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteStringWithTag(bytes, writePosition, KeyValue_Key, kv.Key);

        // KeyValue.value (field 2, AnyValue)
        writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, KeyValue_Value, ProtobufWireType.LEN);
        int valueLengthPos = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = WriteAnyValue(bytes, writePosition, kv.Value);

        ProtobufSerializer.WriteReservedLength(bytes, valueLengthPos, writePosition - (valueLengthPos + ReserveSizeForLength));
        ProtobufSerializer.WriteReservedLength(bytes, lengthPos, writePosition - (lengthPos + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteAnyValue(byte[] bytes, int writePosition, object? value)
    {
        switch (value)
        {
            case null:
                return writePosition; // empty AnyValue
            case string s:
                return ProtobufSerializer.WriteStringWithTag(bytes, writePosition, AnyValue_String_Value, s);
            case bool b:
                return ProtobufSerializer.WriteBoolWithTag(bytes, writePosition, AnyValue_Bool_Value, b);
            case int i:
                return ProtobufSerializer.WriteInt64WithTag(bytes, writePosition, AnyValue_Int_Value, unchecked((ulong)(long)i));
            case long l:
                return ProtobufSerializer.WriteInt64WithTag(bytes, writePosition, AnyValue_Int_Value, unchecked((ulong)l));
            case double d:
                return ProtobufSerializer.WriteDoubleWithTag(bytes, writePosition, AnyValue_Double_Value, d);
            case float f:
                return ProtobufSerializer.WriteDoubleWithTag(bytes, writePosition, AnyValue_Double_Value, f);
            case byte[] bytesValue:
                return ProtobufSerializer.WriteByteArrayWithTag(bytes, writePosition, AnyValue_Bytes_Value, bytesValue);
            default:
                return ProtobufSerializer.WriteStringWithTag(bytes, writePosition, AnyValue_String_Value, value.ToString() ?? string.Empty);
        }
    }

    private static int WriteTraceIdField(byte[] bytes, int writePosition, int fieldNumber, string rawTraceIdHex)
    {
        writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, fieldNumber, ProtobufWireType.LEN);
        writePosition = ProtobufSerializer.WriteLength(bytes, writePosition, TraceIdSize);
        WriteHexBytes(bytes, writePosition, rawTraceIdHex, TraceIdSize);
        return writePosition + TraceIdSize;
    }

    private static int WriteSpanIdField(byte[] bytes, int writePosition, int fieldNumber, ulong spanId)
    {
        writePosition = ProtobufSerializer.WriteTag(bytes, writePosition, fieldNumber, ProtobufWireType.LEN);
        writePosition = ProtobufSerializer.WriteLength(bytes, writePosition, SpanIdSize);
        BinaryPrimitives.WriteUInt64BigEndian(new Span<byte>(bytes, writePosition, SpanIdSize), spanId);
        return writePosition + SpanIdSize;
    }

    private static void WriteHexBytes(byte[] bytes, int writePosition, string hex, int byteCount)
    {
        // RawTraceId is zero-padded to 32 chars; RawSpanId is zero-padded to 16. Defensively
        // normalize any non-canonical input: left-pad with zero nibbles if too short, or take
        // the lowest-order bytes if too long. Works for both even and odd lengths.
        var span = new Span<byte>(bytes, writePosition, byteCount);
        int expectedChars = byteCount * 2;
        int shift = expectedChars - hex.Length; // > 0 to pad; < 0 to truncate from the left

        for (int i = 0; i < byteCount; i++)
        {
            int highPos = (2 * i) - shift;
            int lowPos = highPos + 1;
            int high = highPos < 0 ? 0 : FromHex(hex[highPos]);
            int low = lowPos < 0 ? 0 : FromHex(hex[lowPos]);
            span[i] = (byte)((high << 4) | low);
        }

        static int FromHex(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => 10 + (c - 'a'),
            >= 'A' and <= 'F' => 10 + (c - 'A'),
            _ => 0,
        };
    }
}
