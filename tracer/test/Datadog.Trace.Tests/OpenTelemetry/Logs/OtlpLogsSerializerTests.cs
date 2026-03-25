// <copyright file="OtlpLogsSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Datadog.Trace.OpenTelemetry.Logs;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry.Logs
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class OtlpLogsSerializerTests
    {
        private static readonly OtlpLogsSerializer.ResourceTags DefaultResourceTags =
            new("test-service", null, null, new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));

        [Fact]
        public async Task DatadogSpanActive_KeepPriority_SerializesTraceContext()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            TracerRestorerAttribute.SetTracer(tracer);

            using var scope = tracer.StartActive("test");
            var span = (Span)scope.Span;

            // Force keep sampling priority
            span.Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep);

            var rawTraceId = span.Context.RawTraceId;
            var rawSpanId = span.Context.RawSpanId;

            var logPoint = new LogPoint
            {
                Message = "test message",
                LogLevel = 2, // Information
                Timestamp = DateTimeOffset.UtcNow,
                TraceId = rawTraceId,
                SpanId = rawSpanId,
                Flags = 1,
            };

            var bytes = OtlpLogsSerializer.SerializeLogs([logPoint], DefaultResourceTags);
            var (traceId, spanId, flags) = ExtractLogRecordFields(bytes);

            traceId.Should().Be(rawTraceId);
            spanId.Should().Be(rawSpanId);
            flags.Should().Be(1);
        }

        [Fact]
        public async Task DatadogSpanActive_RejectPriority_SerializesTraceContext()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            TracerRestorerAttribute.SetTracer(tracer);

            using var scope = tracer.StartActive("test");
            var span = (Span)scope.Span;

            // Force reject sampling priority
            span.Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject);

            var rawTraceId = span.Context.RawTraceId;
            var rawSpanId = span.Context.RawSpanId;

            var logPoint = new LogPoint
            {
                Message = "test message",
                LogLevel = 2,
                Timestamp = DateTimeOffset.UtcNow,
                TraceId = rawTraceId,
                SpanId = rawSpanId,
                Flags = 0,
            };

            var bytes = OtlpLogsSerializer.SerializeLogs([logPoint], DefaultResourceTags);
            var (traceId, spanId, flags) = ExtractLogRecordFields(bytes);

            traceId.Should().Be(rawTraceId);
            spanId.Should().Be(rawSpanId);
            flags.Should().Be(0);
        }

#if NET6_0_OR_GREATER
        [Fact]
        public void W3CActivityActive_SerializesTraceContext()
        {
            var activitySource = new ActivitySource("test-source");
            using var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(listener);

            using var activity = activitySource.StartActivity("test-activity", ActivityKind.Internal);
            activity.Should().NotBeNull();

            var traceIdHex = activity!.TraceId.ToHexString();
            var spanIdHex = activity.SpanId.ToHexString();

            var logPoint = new LogPoint
            {
                Message = "activity message",
                LogLevel = 2,
                Timestamp = DateTimeOffset.UtcNow,
                TraceId = traceIdHex,
                SpanId = spanIdHex,
                Flags = 1,
            };

            var bytes = OtlpLogsSerializer.SerializeLogs([logPoint], DefaultResourceTags);
            var (traceId, spanId, flags) = ExtractLogRecordFields(bytes);

            traceId.Should().Be(traceIdHex);
            spanId.Should().Be(spanIdHex);
            flags.Should().Be(1);
        }

#endif
        [Fact]
        public void NoTraceContext_SerializesWithoutTraceIds()
        {
            var logPoint = new LogPoint
            {
                Message = "no trace context",
                LogLevel = 2,
                Timestamp = DateTimeOffset.UtcNow,
                TraceId = null,
                SpanId = null,
                Flags = 0,
            };

            var bytes = OtlpLogsSerializer.SerializeLogs([logPoint], DefaultResourceTags);
            var (traceId, spanId, flags) = ExtractLogRecordFields(bytes);

            traceId.Should().BeNull();
            spanId.Should().BeNull();
            flags.Should().Be(0);
        }

        /// <summary>
        /// Walks the protobuf wire format to extract trace_id (field 9), span_id (field 10),
        /// and flags (field 8) from the first LogRecord in the serialized OTLP LogsData message.
        /// </summary>
        private static (string? TraceId, string? SpanId, uint Flags) ExtractLogRecordFields(byte[] data)
        {
            const int wireTypeLEN = 2;
            const int wireTypeVarint = 0;
            const int wireTypeI64 = 1;
            const int wireTypeI32 = 5;
            const int flagsField = 8;
            const int traceIdField = 9;
            const int spanIdField = 10;
            const int traceIdByteLength = 16;
            const int spanIdByteLength = 8;

            var logRecord = FindLogRecord(data.AsSpan());
            if (logRecord.Length == 0)
            {
                return (null, null, 0);
            }

            string? traceId = null;
            string? spanId = null;
            uint flags = 0;
            int pos = 0;

            while (pos < logRecord.Length)
            {
                var (fieldNumber, wireType, newPos) = ReadTag(logRecord, pos);
                pos = newPos;

                switch (wireType)
                {
                    case wireTypeLEN:
                    {
                        var (length, afterLen) = ReadVarint(logRecord, pos);
                        pos = afterLen;

                        if (fieldNumber == traceIdField && length == traceIdByteLength)
                        {
                            traceId = HexString.ToHexString(logRecord.Slice(pos, (int)length));
                        }
                        else if (fieldNumber == spanIdField && length == spanIdByteLength)
                        {
                            spanId = HexString.ToHexString(logRecord.Slice(pos, (int)length));
                        }

                        pos += (int)length;
                        break;
                    }

                    case wireTypeVarint:
                    {
                        var (_, afterVarint) = ReadVarint(logRecord, pos);
                        pos = afterVarint;
                        break;
                    }

                    case wireTypeI64:
                        pos += 8;
                        break;

                    case wireTypeI32:
                        if (fieldNumber == flagsField)
                        {
                            flags = BitConverter.ToUInt32(logRecord.Slice(pos, 4));
                        }

                        pos += 4;
                        break;
                }
            }

            return (traceId, spanId, flags);
        }

        private static ReadOnlySpan<byte> FindLogRecord(ReadOnlySpan<byte> data)
        {
            var resourceLogs = FindField(data, fieldNumber: 1);
            if (resourceLogs.Length == 0)
            {
                return ReadOnlySpan<byte>.Empty;
            }

            var scopeLogs = FindField(resourceLogs, fieldNumber: 2);
            if (scopeLogs.Length == 0)
            {
                return ReadOnlySpan<byte>.Empty;
            }

            return FindField(scopeLogs, fieldNumber: 2);
        }

        private static ReadOnlySpan<byte> FindField(ReadOnlySpan<byte> data, int fieldNumber)
        {
            const int wireTypeLEN = 2;
            const int wireTypeVarint = 0;
            const int wireTypeI64 = 1;
            const int wireTypeI32 = 5;

            int pos = 0;
            while (pos < data.Length)
            {
                var (fn, wireType, newPos) = ReadTag(data, pos);
                pos = newPos;

                switch (wireType)
                {
                    case wireTypeLEN:
                    {
                        var (length, afterLen) = ReadVarint(data, pos);
                        pos = afterLen;

                        if (fn == fieldNumber)
                        {
                            return data.Slice(pos, (int)length);
                        }

                        pos += (int)length;
                        break;
                    }

                    case wireTypeVarint:
                    {
                        var (_, afterVarint) = ReadVarint(data, pos);
                        pos = afterVarint;
                        break;
                    }

                    case wireTypeI64:
                        pos += 8;
                        break;

                    case wireTypeI32:
                        pos += 4;
                        break;
                }
            }

            return ReadOnlySpan<byte>.Empty;
        }

        private static (int FieldNumber, int WireType, int NewPosition) ReadTag(ReadOnlySpan<byte> data, int pos)
        {
            var (tag, newPos) = ReadVarint(data, pos);
            return ((int)(tag >> 3), (int)(tag & 0x7), newPos);
        }

        private static (ulong Value, int NewPosition) ReadVarint(ReadOnlySpan<byte> data, int pos)
        {
            ulong result = 0;
            int shift = 0;
            while (pos < data.Length)
            {
                byte b = data[pos++];
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
            }

            return (result, pos);
        }
    }
}

#endif
