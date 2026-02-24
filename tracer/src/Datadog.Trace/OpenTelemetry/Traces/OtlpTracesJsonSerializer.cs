// <copyright file="OtlpTracesJsonSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.OpenTelemetry.Common;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Traces;

/// <summary>
/// OTLP JSON serializer that creates UTF-8 JSON payloads
/// compliant with the OpenTelemetry ExportTracesServiceRequest schema.
/// See: https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/trace/v1/trace.proto
/// </summary>
/// <example>
/// Usage example:
/// <code>
/// // Create trace and span IDs
/// var traceId = new byte[16]; // 16-byte trace ID
/// var spanId = new byte[8];   // 8-byte span ID
///
/// // Create a span
/// var span = new OtlpSpan(
///     traceId: traceId,
///     spanId: spanId,
///     name: "my-operation",
///     kind: SpanKind.Server,
///     startTimeUnixNano: 1234567890000000000UL,
///     endTimeUnixNano: 1234567891000000000UL,
///     attributes: new[] { new KeyValue("http.method", "GET") }
/// );
///
/// // Create scope and resource
/// var scope = new InstrumentationScope("my-library", "1.0.0");
/// var scopeSpans = new ScopeSpans(scope, new[] { span });
///
/// var resource = new OtlpResource(new[] { new KeyValue("service.name", "my-service") });
/// var resourceSpans = new ResourceSpans(resource, new[] { scopeSpans });
///
/// var tracesData = new TracesData(new[] { resourceSpans });
///
/// // Serialize to JSON
/// byte[] jsonBytes = OtlpTracesJsonSerializer.SerializeToJson(tracesData);
/// </code>
/// </example>
internal sealed class OtlpTracesJsonSerializer : ISpanBufferSerializer
{
    internal static readonly int SpanAttributeCountLimit = 128;
    internal static readonly int EventCountLimit = 128;
    internal static readonly int LinkCountLimit = 128;
    internal static readonly int AttributePerEventCountLimit = 128;
    internal static readonly int AttributePerLinkCountLimit = 128;

    public int HeaderSize => 0;

    internal static void ExportTraceServiceRequest(JsonTextWriter writer, in TraceChunkModel traceChunk)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("resourceSpans");
        writer.WriteStartArray();

        writer.WriteStartObject();
        writer.WritePropertyName("resource");
        WriteResource(writer, in traceChunk);

        // if (!string.IsNullOrEmpty(resourceSpans.SchemaUrl))
        // {
        //     writer.WritePropertyName("schema_url");
        //     writer.WriteValue(resourceSpans.SchemaUrl);
        // }

        // Note: Since the Datadog tracer only allows one Instrumentation Scope, we collapse them all for now
        // TODO: Allow individual scopes by name/version
        writer.WritePropertyName("scopeSpans");
        writer.WriteStartArray();

        WriteScopeSpans(writer, in traceChunk);

        // Unclosed repeated ScopeSpans spans field
        // Unclosed ResourceSpans message
        // Unclosed repeated ResourceSpans field
        // Unclosed ExportTraceServiceRequest message
    }

    internal static void WriteScopeSpans(JsonTextWriter writer, in TraceChunkModel traceChunk)
    {
        writer.WriteStartObject();

        // if (scopeSpans.Scope.HasValue)
        // {
        //     writer.WritePropertyName("scope");
        //     WriteInstrumentationScope(writer, scopeSpans.Scope.Value);
        // }

        // if (!string.IsNullOrEmpty(scopeSpans.SchemaUrl))
        // {
        //     writer.WritePropertyName("schema_url");
        //     writer.WriteValue(scopeSpans.SchemaUrl);
        // }

        writer.WritePropertyName("spans");
        writer.WriteStartArray();

        // Unclosed repeated Span spans field
        // Unclosed ScopeSpans message
    }

    internal static void WriteSpans(JsonTextWriter writer, in TraceChunkModel traceChunk, bool emitStartingComma)
    {
        for (var i = 0; i < traceChunk.SpanCount; i++)
        {
            // If we are emitting a starting comma, then our JSON writer is re-entrant and the state is not
            // synchronized to the fact that we're in the `spans` array
            if (emitStartingComma)
            {
                writer.WriteRaw(",");
            }

            // when serializing each span, we need additional information that is not
            // available in the span object itself, like its position in the trace chunk
            // or if its parent can also be found in the same chunk, so we use SpanModel
            // to pass that information to the serializer
            var spanModel = traceChunk.GetSpanModel(i);
            WriteSpan(writer, spanModel);
        }
    }

    internal static void WriteSpan(JsonTextWriter writer, SpanModel spanModel)
    {
        static Action<KeyValue> WriteKeyValue(JsonTextWriter writer)
            => keyValue => OtlpTracesJsonSerializer.WriteKeyValue(writer, keyValue);

        writer.WriteStartObject();

        // traceId (required) - encoded as hex string in JSON
        writer.WritePropertyName("traceId");
        writer.WriteValue(spanModel.Span.Context.RawTraceId);

        // spanId (required) - encoded as hex string in JSON
        writer.WritePropertyName("spanId");
        writer.WriteValue(spanModel.Span.Context.RawSpanId);

        // traceState (optional)
        // if (!string.IsNullOrEmpty(spanModel.Span.TraceState))
        // {
        //     writer.WritePropertyName("traceState");
        //     writer.WriteValue(spanModel.Span.TraceState);
        // }

        // parentSpanId (optional) - encoded as hex string in JSON
        if (spanModel.Span.Context.ParentId is ulong parentId && parentId > 0)
        {
            writer.WritePropertyName("parentSpanId");
            writer.WriteValue(HexString.ToHexString(parentId));
        }

        // flags (optional)
        if (spanModel.Span.Context.SamplingPriority is int samplingPriority)
        {
            writer.WritePropertyName("flags");
            writer.WriteValue(SamplingPriorityValues.IsKeep(samplingPriority) ? 1 : 0);
        }

        // name (required)
        writer.WritePropertyName("name");
        writer.WriteValue(spanModel.Span.ResourceName);

        // kind (optional, default should be SPAN_KIND_UNSPECIFIED but we use SPAN_KIND_INTERNAL instead)
        var spanKind = spanModel.Span.GetTag(Tags.SpanKind) switch
        {
            SpanKinds.Server => SpanKind.Server,
            SpanKinds.Client => SpanKind.Client,
            SpanKinds.Producer => SpanKind.Producer,
            SpanKinds.Consumer => SpanKind.Consumer,
            _ => SpanKind.Internal,
        };
        writer.WritePropertyName("kind");
        writer.WriteValue((int)spanKind);

        // startTimeUnixNano (required) - string representation of uint64
        writer.WritePropertyName("startTimeUnixNano");
        writer.WriteValue(spanModel.Span.StartTime.ToUnixTimeNanoseconds().ToString());

        // endTimeUnixNano (required) - string representation of uint64
        writer.WritePropertyName("endTimeUnixNano");
        writer.WriteValue((spanModel.Span.StartTime + spanModel.Span.Duration).ToUnixTimeNanoseconds().ToString());

        // attributes (optional)
        // TODO: Actually implement
        int droppedAttributesCount = 0;
        writer.WritePropertyName("attributes");
        writer.WriteStartArray();
        droppedAttributesCount = OtlpMapper.EmitAttributesFromSpan(WriteKeyValue(writer), in spanModel, SpanAttributeCountLimit);
        writer.WriteEndArray();

        // droppedAttributesCount (optional)
        if (droppedAttributesCount > 0)
        {
            writer.WritePropertyName("droppedAttributesCount");
            writer.WriteValue(droppedAttributesCount);
        }

        // events (optional)
        int eventCount = 0;
        int droppedEventCount = 0;
        if (spanModel.Span.SpanEvents != null && spanModel.Span.SpanEvents.Count > 0)
        {
            writer.WritePropertyName("events");
            writer.WriteStartArray();

            foreach (var evt in spanModel.Span.SpanEvents)
            {
                if (eventCount < EventCountLimit)
                {
                    WriteSpanEvent(writer, evt);
                    eventCount++;
                }
                else
                {
                    droppedEventCount++;
                }
            }

            writer.WriteEndArray();
        }

        // droppedEventsCount (optional)
        if (droppedEventCount > 0)
        {
            writer.WritePropertyName("droppedEventsCount");
            writer.WriteValue(droppedEventCount);
        }

        // links (optional)
        int linkCount = 0;
        int droppedLinkCount = 0;
        if (spanModel.Span.SpanLinks != null && spanModel.Span.SpanLinks.Count > 0)
        {
            writer.WritePropertyName("links");
            writer.WriteStartArray();

            foreach (var link in spanModel.Span.SpanLinks)
            {
                if (linkCount < LinkCountLimit)
                {
                    WriteSpanLink(writer, link);
                    linkCount++;
                }
                else
                {
                    droppedLinkCount++;
                }
            }

            writer.WriteEndArray();
        }

        // droppedLinksCount (optional)
        if (droppedLinkCount > 0)
        {
            writer.WritePropertyName("droppedLinksCount");
            writer.WriteValue(droppedLinkCount);
        }

        // status (optional)
        var errorMsg = spanModel.Span.GetTag(Tags.ErrorMsg);
        SpanStatus? spanStatus = spanModel.Span.GetTag("otel.status_code") switch
        {
            "STATUS_CODE_OK" => new SpanStatus(StatusCode.Ok, errorMsg),
            "STATUS_CODE_ERROR" => new SpanStatus(StatusCode.Error, errorMsg),
            _ => null,
        };
        if (spanStatus is not null)
        {
            writer.WritePropertyName("status");
            WriteSpanStatus(writer, spanStatus.Value);
        }

        writer.WriteEndObject();
    }

    internal static void WriteSpanEvent(JsonTextWriter writer, Datadog.Trace.SpanEvent evt)
    {
        writer.WriteStartObject();

        // timeUnixNano - string representation of uint64
        writer.WritePropertyName("timeUnixNano");
        writer.WriteValue(evt.Timestamp.ToUnixTimeNanoseconds().ToString());

        // name
        writer.WritePropertyName("name");
        writer.WriteValue(evt.Name);

        // attributes (optional)
        int droppedAttributesCount = 0;
        if (evt.Attributes != null && evt.Attributes.Count > 0)
        {
            writer.WritePropertyName("attributes");
            droppedAttributesCount = WriteKeyValueArrayWithLimit(writer, evt.Attributes, AttributePerEventCountLimit);
        }

        // droppedAttributesCount (optional)
        if (droppedAttributesCount > 0)
        {
            writer.WritePropertyName("droppedAttributesCount");
            writer.WriteValue(droppedAttributesCount);
        }

        writer.WriteEndObject();
    }

    internal static void WriteSpanLink(JsonTextWriter writer, Datadog.Trace.SpanLink link)
    {
        writer.WriteStartObject();

        // traceId - encoded as hex string in JSON
        writer.WritePropertyName("traceId");
        writer.WriteValue(link.Context.RawTraceId);

        // spanId - encoded as hex string in JSON
        writer.WritePropertyName("spanId");
        writer.WriteValue(link.Context.RawSpanId);

        // traceState (optional)
        if (link.Context.IsRemote)
        {
            writer.WritePropertyName("traceState");
            writer.WriteValue(W3CTraceContextPropagator.CreateTraceStateHeader(link.Context));
        }

        // attributes (optional)
        int droppedAttributesCount = 0;
        if (link.Attributes != null && link.Attributes.Count > 0)
        {
            writer.WritePropertyName("attributes");
            droppedAttributesCount = WriteKeyValueArrayWithLimit(writer, link.Attributes, AttributePerLinkCountLimit);
        }

        // droppedAttributesCount (optional)
        if (droppedAttributesCount > 0)
        {
            writer.WritePropertyName("droppedAttributesCount");
            writer.WriteValue(droppedAttributesCount);
        }

        // flags (optional)
        var samplingPriority = link.Context.TraceContext?.SamplingPriority ?? link.Context.SamplingPriority;
        var traceFlags = samplingPriority switch
        {
            null => 0u, // not set
            > 0 => 1u + (1u << 31), // keep
            <= 0 => 1u << 31, // drop
        };
        if (traceFlags > 0)
        {
            writer.WritePropertyName("flags");
            writer.WriteValue(traceFlags);
        }

        writer.WriteEndObject();
    }

    internal static void WriteSpanStatus(JsonTextWriter writer, SpanStatus status)
    {
        writer.WriteStartObject();

        // message (optional)
        if (!string.IsNullOrEmpty(status.Message))
        {
            writer.WritePropertyName("message");
            writer.WriteValue(status.Message);
        }

        // code (optional, default is STATUS_CODE_UNSET)
        if (status.Code != StatusCode.Unset)
        {
            writer.WritePropertyName("code");
            writer.WriteValue((int)status.Code);
        }

        writer.WriteEndObject();
    }

    internal static void WriteResource(JsonTextWriter writer, in TraceChunkModel traceChunk)
    {
        static Action<KeyValue> WriteKeyValue(JsonTextWriter writer)
            => keyValue => OtlpTracesJsonSerializer.WriteKeyValue(writer, keyValue);

        writer.WriteStartObject();

        // attributes (optional)
        int droppedAttributesCount = 0;

        writer.WritePropertyName("attributes");
        writer.WriteStartArray();

        OtlpMapper.EmitResourceAttributesFromTraceChunk(in traceChunk, WriteKeyValue(writer));

        writer.WriteEndArray();

        // droppedAttributesCount (optional)
        if (droppedAttributesCount > 0)
        {
            writer.WritePropertyName("droppedAttributesCount");
            writer.WriteValue(droppedAttributesCount);
        }

        writer.WriteEndObject();
    }

    // TODO: Emit an instrumentation scope
    /*
    internal static void WriteInstrumentationScope(JsonTextWriter writer, InstrumentationScope scope)
    {
        writer.WriteStartObject();

        // name
        writer.WritePropertyName("name");
        writer.WriteValue(scope.Name);

        // version (optional)
        if (!string.IsNullOrEmpty(scope.Version))
        {
            writer.WritePropertyName("version");
            writer.WriteValue(scope.Version);
        }

        // attributes (optional)
        if (scope.Attributes != null && scope.Attributes.Count > 0)
        {
            writer.WritePropertyName("attributes");
            WriteKeyValueArray(writer, scope.Attributes);
        }

        // droppedAttributesCount (optional)
        if (scope.DroppedAttributesCount > 0)
        {
            writer.WritePropertyName("droppedAttributesCount");
            writer.WriteValue(scope.DroppedAttributesCount);
        }

        writer.WriteEndObject();
    }
    */

    internal static void WriteKeyValueArray(JsonTextWriter writer, IReadOnlyList<KeyValue> keyValues)
    {
        writer.WriteStartArray();

        foreach (var kv in keyValues)
        {
            WriteKeyValue(writer, kv);
        }

        writer.WriteEndArray();
    }

    internal static void WriteKeyValueArray(JsonTextWriter writer, IEnumerable<KeyValuePair<string, string>> keyValues)
    {
        writer.WriteStartArray();

        foreach (var kv in keyValues)
        {
            WriteKeyValue(writer, new KeyValue(kv.Key, kv.Value));
        }

        writer.WriteEndArray();
    }

    private static int WriteKeyValueArrayWithLimit(JsonTextWriter writer, IEnumerable<KeyValuePair<string, object>> keyValues, int limit)
    {
        var count = 0;
        var droppedCount = 0;
        writer.WriteStartArray();

        foreach (var kv in keyValues)
        {
            if (count < limit)
            {
                WriteKeyValue(writer, new KeyValue(kv.Key, kv.Value));
                count++;
            }
            else
            {
                droppedCount++;
            }
        }

        writer.WriteEndArray();
        return droppedCount;
    }

    private static int WriteKeyValueArrayWithLimit(JsonTextWriter writer, IEnumerable<KeyValuePair<string, string>> keyValues, int limit)
    {
        var count = 0;
        var droppedCount = 0;
        writer.WriteStartArray();

        foreach (var kv in keyValues)
        {
            if (count < limit)
            {
                WriteKeyValue(writer, new KeyValue(kv.Key, kv.Value));
                count++;
            }
            else
            {
                droppedCount++;
            }
        }

        writer.WriteEndArray();
        return droppedCount;
    }

    internal static void WriteKeyValue(JsonTextWriter writer, KeyValue keyValue)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("key");
        writer.WriteValue(keyValue.Key);

        writer.WritePropertyName("value");
        WriteAnyValue(writer, keyValue.Value);

        writer.WriteEndObject();
    }

    internal static void WriteAnyValue(JsonTextWriter writer, object? value)
    {
        writer.WriteStartObject();

        if (value == null)
        {
            // Empty AnyValue
            writer.WriteEndObject();
            return;
        }

        switch (value)
        {
            case string stringValue:
                writer.WritePropertyName("stringValue");
                writer.WriteValue(stringValue);
                break;

            case bool boolValue:
                writer.WritePropertyName("boolValue");
                writer.WriteValue(boolValue);
                break;

            case int intValue:
                writer.WritePropertyName("intValue");
                writer.WriteValue(intValue.ToString());
                break;

            case long longValue:
                writer.WritePropertyName("intValue");
                writer.WriteValue(longValue.ToString());
                break;

            case double doubleValue:
                writer.WritePropertyName("doubleValue");
                writer.WriteValue(doubleValue);
                break;

            case float floatValue:
                writer.WritePropertyName("doubleValue");
                writer.WriteValue(floatValue);
                break;

            case byte[] bytesValue:
                writer.WritePropertyName("bytesValue");
                writer.WriteValue(Convert.ToBase64String(bytesValue));
                break;

            default:
                // For other types, try to convert to string
                writer.WritePropertyName("stringValue");
                writer.WriteValue(value.ToString());
                break;
        }

        writer.WriteEndObject();
    }

    private static string GetStatusCodeString(StatusCode code)
    {
        return code switch
        {
            StatusCode.Unset => "STATUS_CODE_UNSET",
            StatusCode.Ok => "STATUS_CODE_OK",
            StatusCode.Error => "STATUS_CODE_ERROR",
            _ => "STATUS_CODE_UNSET"
        };
    }

    public int SerializeSpans(ref byte[] bytes, int offset, TraceChunkModel traceChunk, int spanBufferOffset, int maxSize)
    {
        return SerializeSpans(ref bytes, offset, in traceChunk, spanBufferOffset, maxSize);
    }

    public void WriteHeader(ref byte[] bytes, int offset, int traceCount)
    {
        // No-op for JSON serialization
        return;
    }

    public int FinishBody(ref byte[] bytes, int offset, int maxSize)
    {
        // If no spans were written, there's nothing to do
        if (offset == HeaderSize)
        {
            return 0;
        }

        // jsonWriter.WriteEndArray(); // close repeated Span spans field
        // jsonWriter.WriteEndObject(); // Close ScopeSpans message
        // jsonWriter.WriteEndArray(); // close repeated ScopeSpans field
        // jsonWriter.WriteEndObject(); // Close ResourceSpans message
        // jsonWriter.WriteEndArray(); // Close repeated ResourceSpans field
        // jsonWriter.WriteEndObject(); // Close ExportTraceServiceRequest message
        var buffer = Encoding.UTF8.GetBytes("]}]}]}");

        // Get the length of the written JSON
        var length = buffer.Length;

        if (length + offset - HeaderSize >= maxSize)
        {
            // We've already reached the maximum size, give up
            return 0;
        }

        // Ensure the target buffer has enough space
        MessagePackBinary.EnsureCapacity(ref bytes, offset, length);
        Array.Copy(buffer, 0, bytes, offset, length);
        return length;
    }

    /// <summary>
    /// Serializes the input trace chunks to a UTF-8 JSON byte array following the OTLP JSON encoding specification.
    /// The JSON format follows the official OTLP specification with proper encoding:
    /// - Byte arrays (traceId, spanId) are encoded as lowercase hex strings
    /// - uint64 values (timestamps) are encoded as strings
    /// - Enum values use their integer values (e.g., "SPAN_KIND_SERVER" is encoded as 2)
    /// - Field names use camelCase (e.g., "resourceSpans", "startTimeUnixNano")
    ///
    /// Note: This method is stateful. The first time a trace chunk is serialized (the provided offset is the same as the header size),
    /// the method will write the enclosing ResourceSpans and ScopeSpans message and then write the spans into the `repeated Span spans` field.
    /// Subsequent calls to the method will only append to the `repeated Span spans` field.
    /// </summary>
    /// <param name="bytes">The temporary byte buffer</param>
    /// <param name="offset">The offset of the temporary byte buffer</param>
    /// <param name="traceChunk">The trace chunk to serialize</param>
    /// <param name="spanBufferOffset">The offset of the overall span buffer</param>
    /// <param name="maxSize">Maximum allowed size of the trace chunk</param>
    /// <returns>UTF-8 encoded JSON byte array</returns>
    internal int SerializeSpans(ref byte[] bytes, int offset, in TraceChunkModel traceChunk, int spanBufferOffset, int maxSize)
    {
        using var memoryStream = new MemoryStream();
        using var streamWriter = new StreamWriter(memoryStream, EncodingHelpers.Utf8NoBom, bufferSize: 4096, leaveOpen: true);
        using var jsonWriter = new JsonTextWriter(streamWriter)
        {
            CloseOutput = false
        };

        // This implementation assumes that there is at least one span in the trace chunk
        if (spanBufferOffset == HeaderSize)
        {
            ExportTraceServiceRequest(jsonWriter, in traceChunk);
            WriteSpans(jsonWriter, in traceChunk, emitStartingComma: false);
        }
        else
        {
            WriteSpans(jsonWriter, in traceChunk, emitStartingComma: true);
        }

        jsonWriter.Flush();
        streamWriter.Flush();
        memoryStream.Flush();

        // Get the length of the written JSON
        var length = (int)memoryStream.Position;

        if (length - offset >= maxSize)
        {
            // We've already reached the maximum size, give up
            return 0;
        }

        // Ensure the target buffer has enough space
        MessagePackBinary.EnsureCapacity(ref bytes, offset, length);

        // Copy the internal buffer to the target buffer
        // MemoryStream.GetBuffer() returns the internal buffer without copying
        var buffer = memoryStream.GetBuffer();
        Array.Copy(buffer, 0, bytes, offset, length);

        return length;
    }
}
