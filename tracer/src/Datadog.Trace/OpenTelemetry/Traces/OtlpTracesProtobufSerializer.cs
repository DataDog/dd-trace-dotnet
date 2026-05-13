// <copyright file="OtlpTracesProtobufSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Buffers.Binary;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.OpenTelemetry.Common;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using static Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer.ProtobufOtlpCommonFieldNumberConstants;
using static Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer.ProtobufOtlpTraceFieldNumberConstants;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Traces;

/// <summary>
/// OTLP protobuf serializer producing payloads compliant with
/// opentelemetry.proto.collector.trace.v1.ExportTraceServiceRequest.
/// See: https://github.com/open-telemetry/opentelemetry-proto/blob/v1.2.0/opentelemetry/proto/trace/v1/trace.proto
/// </summary>
internal sealed class OtlpTracesProtobufSerializer : ISpanBufferSerializer
{
    internal const int SpanAttributeCountLimit = 128;
    internal const int EventCountLimit = 128;
    internal const int LinkCountLimit = 128;
    internal const int AttributePerEventCountLimit = 128;
    internal const int AttributePerLinkCountLimit = 128;

#pragma warning disable CA1823
    private const int ReserveSizeForLength = 4;
    private const int TraceIdSize = 16;
    private const int SpanIdSize = 8;
#pragma warning restore CA1823

    // Positions of the length placeholders inside the active output buffer.
    // Set on the first SerializeSpans call (when spanBufferOffset == HeaderSize) and patched in FinishBody.
#pragma warning disable CS0414, CA1823
    private int _resourceSpansLengthPos = -1;
    private int _scopeSpansLengthPos = -1;
#pragma warning restore CS0414, CA1823

    public int HeaderSize => 0;

    public void WriteHeader(ref byte[] bytes, int offset, int traceCount)
    {
        // No fixed header; the outer envelope is opened on the first SerializeSpans call.
    }

    public int SerializeSpans(ref byte[] bytes, int temporaryBufferOffset, TraceChunkModel traceChunk, int spanBufferOffset, int maxSize)
    {
        throw new NotImplementedException();
    }

    public int FinishBody(ref byte[] bytes, int offset, int maxSize)
    {
        throw new NotImplementedException();
    }
}

#endif
