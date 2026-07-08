// <copyright file="OtlpSpanStatsSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent
{
    internal sealed class OtlpSpanStatsSerializer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<OtlpSpanStatsSerializer>();

        internal const string MetricName = "traces.span.sdk.metrics.duration";

        private const int WireTypeVarInt = 0;
        private const int WireTypeFixed64 = 1;
        private const int WireTypeLengthDelimited = 2;
        private const int AggregationTemporalityDelta = 1;
        private const long StatusCodeErrorProto = 2;
        private const string StatusCodeErrorJson = "STATUS_CODE_ERROR";

        private const string MetricUnit = "s";

        // 16 explicit bounds (seconds) → 17 buckets.  Mirrors the OTel spanmetrics-connector defaults.
        private static readonly double[] BoundsS =
        [
            0.002, 0.004, 0.006, 0.008, 0.01, 0.05, 0.1, 0.2, 0.4, 0.8, 1, 1.4, 2, 5, 10, 15,
        ];

        // Same bounds converted to nanoseconds for comparison with sketch bin values.
        private static readonly double[] BoundsNs;

        // Canonical gRPC status names indexed by numeric code (0–16), per https://grpc.github.io/grpc/core/md_doc_statuscodes.html
        private static readonly string[] GrpcStatusNames =
        [
            "OK", "CANCELLED", "UNKNOWN", "INVALID_ARGUMENT", "DEADLINE_EXCEEDED",
            "NOT_FOUND", "ALREADY_EXISTS", "PERMISSION_DENIED", "RESOURCE_EXHAUSTED",
            "FAILED_PRECONDITION", "ABORTED", "OUT_OF_RANGE", "UNIMPLEMENTED",
            "INTERNAL", "UNAVAILABLE", "DATA_LOSS", "UNAUTHENTICATED",
        ];

        static OtlpSpanStatsSerializer()
        {
            BoundsNs = new double[BoundsS.Length];
            for (int i = 0; i < BoundsS.Length; i++)
            {
                BoundsNs[i] = BoundsS[i] * 1_000_000_000.0;
            }
        }

        public static byte[]? Serialize(StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            if (!buffer.HasHits())
            {
                return null;
            }

            using var stream = new MemoryStream(1024);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            var resourceMetricsData = SerializeResourceMetrics(buffer, bucketDurationNs, otelSemanticsEnabled);
            WriteTag(writer, FieldNumbers.ResourceMetrics, WireTypeLengthDelimited);
            WriteVarInt(writer, resourceMetricsData.Length);
            writer.Write(resourceMetricsData);

            writer.Flush();
            return stream.ToArray();
        }

        public static byte[]? SerializeJson(StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            if (!buffer.HasHits())
            {
                return null;
            }

            using var memoryStream = new MemoryStream(1024);
            using var streamWriter = new StreamWriter(memoryStream, EncodingHelpers.Utf8NoBom, bufferSize: 4096, leaveOpen: true);
            using var writer = new JsonTextWriter(streamWriter) { CloseOutput = false };

            writer.WriteStartObject();
            writer.WritePropertyName("resourceMetrics");
            writer.WriteStartArray();
            WriteResourceMetricsJson(writer, buffer, bucketDurationNs, otelSemanticsEnabled);
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.Flush();
            streamWriter.Flush();
            return memoryStream.ToArray();
        }

        private static void WriteResourceMetricsJson(JsonTextWriter writer, StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("resource");
            WriteResourceJson(writer, buffer, otelSemanticsEnabled);

            writer.WritePropertyName("scopeMetrics");
            writer.WriteStartArray();
            WriteScopeMetricsJson(writer, buffer, bucketDurationNs, otelSemanticsEnabled);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        private static void WriteResourceJson(JsonTextWriter writer, StatsBuffer buffer, bool otelSemanticsEnabled)
        {
            var details = buffer.Header.Details;

            writer.WriteStartObject();
            writer.WritePropertyName("attributes");
            writer.WriteStartArray();

            WriteStringKvJson(writer, "service.name", details.DefaultServiceName ?? "unknown_service:dotnet");

            if (!StringUtil.IsNullOrEmpty(details.Version))
            {
                WriteStringKvJson(writer, "service.version", details.Version!);
            }

            if (!StringUtil.IsNullOrEmpty(details.Environment))
            {
                WriteStringKvJson(writer, "deployment.environment.name", details.Environment!);
            }

            WriteStringKvJson(writer, "telemetry.sdk.name", TracerConstants.TelemetrySdkName);
            WriteStringKvJson(writer, "telemetry.sdk.language", TracerConstants.Language);
            WriteStringKvJson(writer, "telemetry.sdk.version", TracerConstants.AssemblyVersion);

            if (!otelSemanticsEnabled)
            {
                WriteStringKvJson(writer, "datadog.runtime_id", Tracer.RuntimeId);

                foreach (var tag in details.DdTags)
                {
                    if (TryParseTag(tag, out var key, out var value) && !OtlpMapper.IsHandledResourceAttribute(key))
                    {
                        WriteStringKvJson(writer, key, value);
                    }
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteScopeMetricsJson(JsonTextWriter writer, StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("metrics");
            writer.WriteStartArray();
            WriteMetricJson(writer, buffer, bucketDurationNs, otelSemanticsEnabled);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteMetricJson(JsonTextWriter writer, StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteValue(MetricName);
            writer.WritePropertyName("unit");
            writer.WriteValue(MetricUnit);
            writer.WritePropertyName("histogram");
            WriteHistogramJson(writer, buffer, bucketDurationNs, otelSemanticsEnabled);
            writer.WriteEndObject();
        }

        private static void WriteHistogramJson(JsonTextWriter writer, StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            var startTimeUnixNano = (ulong)buffer.Start;
            var endTimeUnixNano = (ulong)(buffer.Start + bucketDurationNs);
            var defaultService = buffer.Header.Details.DefaultServiceName;

            writer.WriteStartObject();
            writer.WritePropertyName("aggregationTemporality");
            writer.WriteValue(AggregationTemporalityDelta);
            writer.WritePropertyName("dataPoints");
            writer.WriteStartArray();

            foreach (var kvp in buffer.Buckets)
            {
                var bucket = kvp.Value;
                if (bucket.Hits == 0)
                {
                    continue;
                }

                WriteDataPointJson(writer, kvp.Key, bucket, startTimeUnixNano, endTimeUnixNano, otelSemanticsEnabled, defaultService);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteDataPointJson(
            JsonTextWriter writer,
            StatsAggregationKey key,
            StatsBucket bucket,
            ulong startTimeUnixNano,
            ulong endTimeUnixNano,
            bool otelSemanticsEnabled,
            string defaultServiceName)
        {
            writer.WriteStartObject();

            // Attributes
            writer.WritePropertyName("attributes");
            writer.WriteStartArray();

            if (!StringUtil.IsNullOrEmpty(key.Resource))
            {
                WriteStringKvJson(writer, "span.name", key.Resource);
            }

            if (!StringUtil.IsNullOrEmpty(key.SpanKind))
            {
                WriteStringKvJson(writer, "span.kind", key.SpanKind);
            }

            if (!StringUtil.IsNullOrEmpty(key.HttpMethod))
            {
                WriteStringKvJson(writer, "http.request.method", key.HttpMethod);
            }

            if (key.HttpStatusCode != 0)
            {
                WriteIntKvJson(writer, "http.response.status_code", key.HttpStatusCode);
            }

            if (!StringUtil.IsNullOrEmpty(key.HttpEndpoint))
            {
                WriteStringKvJson(writer, "http.route", key.HttpEndpoint);
            }

            var grpcStatusName = NormalizeGrpcStatusName(key.GrpcStatusCode);
            if (grpcStatusName is not null)
            {
                WriteStringKvJson(writer, "rpc.response.status_code", grpcStatusName);
            }

            if (key.IsError)
            {
                WriteStringKvJson(writer, "status.code", StatusCodeErrorJson);
            }

            if (!StringUtil.IsNullOrEmpty(key.Service) && !string.Equals(key.Service, defaultServiceName, StringComparison.OrdinalIgnoreCase))
            {
                WriteStringKvJson(writer, "service.name", key.Service);
            }

            if (!otelSemanticsEnabled)
            {
                if (!StringUtil.IsNullOrEmpty(key.OperationName))
                {
                    WriteStringKvJson(writer, "datadog.operation.name", key.OperationName);
                }

                if (!StringUtil.IsNullOrEmpty(key.Type))
                {
                    WriteStringKvJson(writer, "datadog.span.type", key.Type);
                }

                WriteBoolKvJson(writer, "datadog.span.top_level", key.IsTopLevel);

                if (key.IsSyntheticsRequest)
                {
                    WriteStringKvJson(writer, "datadog.origin", "synthetics");
                }
            }

            writer.WriteEndArray();

            // uint64 fields are encoded as strings in proto3 JSON
            writer.WritePropertyName("startTimeUnixNano");
            writer.WriteValue(startTimeUnixNano.ToString());
            writer.WritePropertyName("timeUnixNano");
            writer.WriteValue(endTimeUnixNano.ToString());

            var count = (ulong)bucket.Hits;
            writer.WritePropertyName("count");
            writer.WriteValue(count.ToString());

            writer.WritePropertyName("sum");
            writer.WriteValue(TimeHelpers.NanosecondsToSeconds((long)bucket.Duration));

            writer.WritePropertyName("bucketCounts");
            writer.WriteStartArray();
            var bucketCounts = ProjectSketch(bucket.OkSummary);
            foreach (var bc in bucketCounts)
            {
                writer.WriteValue(bc.ToString());
            }

            writer.WriteEndArray();

            writer.WritePropertyName("explicitBounds");
            writer.WriteStartArray();
            foreach (var bound in BoundsS)
            {
                writer.WriteValue(bound);
            }

            writer.WriteEndArray();

            if (bucket.MinDuration < long.MaxValue)
            {
                writer.WritePropertyName("min");
                writer.WriteValue(TimeHelpers.NanosecondsToSeconds(bucket.MinDuration));
            }

            if (bucket.MaxDuration > 0)
            {
                writer.WritePropertyName("max");
                writer.WriteValue(TimeHelpers.NanosecondsToSeconds(bucket.MaxDuration));
            }

            writer.WriteEndObject();
        }

        private static void WriteStringKvJson(JsonTextWriter writer, string key, string value)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("key");
            writer.WriteValue(key);
            writer.WritePropertyName("value");
            writer.WriteStartObject();
            writer.WritePropertyName("stringValue");
            writer.WriteValue(value);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteIntKvJson(JsonTextWriter writer, string key, long value)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("key");
            writer.WriteValue(key);
            writer.WritePropertyName("value");
            writer.WriteStartObject();
            writer.WritePropertyName("intValue");
            writer.WriteValue(value.ToString());
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteBoolKvJson(JsonTextWriter writer, string key, bool value)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("key");
            writer.WriteValue(key);
            writer.WritePropertyName("value");
            writer.WriteStartObject();
            writer.WritePropertyName("boolValue");
            writer.WriteValue(value);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static byte[] SerializeResourceMetrics(StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            using var stream = new MemoryStream(512);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            var resourceData = SerializeResource(buffer, otelSemanticsEnabled);
            WriteTag(writer, FieldNumbers.Resource, WireTypeLengthDelimited);
            WriteVarInt(writer, resourceData.Length);
            writer.Write(resourceData);

            var scopeMetricsData = SerializeScopeMetrics(buffer, bucketDurationNs, otelSemanticsEnabled);
            WriteTag(writer, FieldNumbers.ScopeMetrics, WireTypeLengthDelimited);
            WriteVarInt(writer, scopeMetricsData.Length);
            writer.Write(scopeMetricsData);

            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] SerializeResource(StatsBuffer buffer, bool otelSemanticsEnabled)
        {
            using var stream = new MemoryStream(256);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            var details = buffer.Header.Details;

            WriteAttribute(writer, "service.name", details.DefaultServiceName ?? "unknown_service:dotnet");

            if (!StringUtil.IsNullOrEmpty(details.Version))
            {
                WriteAttribute(writer, "service.version", details.Version!);
            }

            if (!StringUtil.IsNullOrEmpty(details.Environment))
            {
                WriteAttribute(writer, "deployment.environment.name", details.Environment!);
            }

            WriteAttribute(writer, "telemetry.sdk.name", TracerConstants.TelemetrySdkName);
            WriteAttribute(writer, "telemetry.sdk.language", TracerConstants.Language);
            WriteAttribute(writer, "telemetry.sdk.version", TracerConstants.AssemblyVersion);

            if (!otelSemanticsEnabled)
            {
                WriteAttribute(writer, "datadog.runtime_id", Tracer.RuntimeId);

                foreach (var tag in details.DdTags)
                {
                    if (TryParseTag(tag, out var key, out var value) && !OtlpMapper.IsHandledResourceAttribute(key))
                    {
                        WriteAttribute(writer, key, value);
                    }
                }
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] SerializeScopeMetrics(StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            using var stream = new MemoryStream(512);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            var metricData = SerializeMetric(buffer, bucketDurationNs, otelSemanticsEnabled);
            WriteTag(writer, FieldNumbers.Metrics, WireTypeLengthDelimited);
            WriteVarInt(writer, metricData.Length);
            writer.Write(metricData);

            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] SerializeMetric(StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            using var stream = new MemoryStream(512);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            WriteStringField(writer, FieldNumbers.MetricName, MetricName);
            WriteStringField(writer, FieldNumbers.MetricUnit, MetricUnit);

            var histogramData = SerializeHistogram(buffer, bucketDurationNs, otelSemanticsEnabled);
            WriteTag(writer, FieldNumbers.Histogram, WireTypeLengthDelimited);
            WriteVarInt(writer, histogramData.Length);
            writer.Write(histogramData);

            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] SerializeHistogram(StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            using var stream = new MemoryStream(512);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            var startTimeUnixNano = (ulong)buffer.Start;
            var endTimeUnixNano = (ulong)(buffer.Start + bucketDurationNs);

            foreach (var kvp in buffer.Buckets)
            {
                var bucket = kvp.Value;
                if (bucket.Hits == 0)
                {
                    continue;
                }

                var dataPointData = SerializeDataPoint(kvp.Key, bucket, startTimeUnixNano, endTimeUnixNano, otelSemanticsEnabled, buffer.Header.Details.DefaultServiceName);
                WriteTag(writer, FieldNumbers.DataPoints, WireTypeLengthDelimited);
                WriteVarInt(writer, dataPointData.Length);
                writer.Write(dataPointData);
            }

            WriteTag(writer, FieldNumbers.AggregationTemporality, WireTypeVarInt);
            WriteVarInt(writer, AggregationTemporalityDelta);

            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] SerializeDataPoint(
            StatsAggregationKey key,
            StatsBucket bucket,
            ulong startTimeUnixNano,
            ulong endTimeUnixNano,
            bool otelSemanticsEnabled,
            string defaultServiceName)
        {
            using var stream = new MemoryStream(256);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            if (!StringUtil.IsNullOrEmpty(key.Resource))
            {
                WriteAttribute(writer, "span.name", key.Resource, FieldNumbers.HistogramDataPointAttributes);
            }

            if (!StringUtil.IsNullOrEmpty(key.SpanKind))
            {
                WriteAttribute(writer, "span.kind", key.SpanKind, FieldNumbers.HistogramDataPointAttributes);
            }

            if (!StringUtil.IsNullOrEmpty(key.HttpMethod))
            {
                WriteAttribute(writer, "http.request.method", key.HttpMethod, FieldNumbers.HistogramDataPointAttributes);
            }

            if (key.HttpStatusCode != 0)
            {
                WriteIntAttribute(writer, "http.response.status_code", key.HttpStatusCode, FieldNumbers.HistogramDataPointAttributes);
            }

            if (!StringUtil.IsNullOrEmpty(key.HttpEndpoint))
            {
                WriteAttribute(writer, "http.route", key.HttpEndpoint, FieldNumbers.HistogramDataPointAttributes);
            }

            var grpcStatusNameProto = NormalizeGrpcStatusName(key.GrpcStatusCode);
            if (grpcStatusNameProto is not null)
            {
                WriteAttribute(writer, "rpc.response.status_code", grpcStatusNameProto, FieldNumbers.HistogramDataPointAttributes);
            }

            if (key.IsError)
            {
                WriteIntAttribute(writer, "status.code", StatusCodeErrorProto, FieldNumbers.HistogramDataPointAttributes);
            }

            if (!StringUtil.IsNullOrEmpty(key.Service) && !string.Equals(key.Service, defaultServiceName, StringComparison.OrdinalIgnoreCase))
            {
                WriteAttribute(writer, "service.name", key.Service, FieldNumbers.HistogramDataPointAttributes);
            }

            if (!otelSemanticsEnabled)
            {
                if (!StringUtil.IsNullOrEmpty(key.OperationName))
                {
                    WriteAttribute(writer, "datadog.operation.name", key.OperationName, FieldNumbers.HistogramDataPointAttributes);
                }

                if (!StringUtil.IsNullOrEmpty(key.Type))
                {
                    WriteAttribute(writer, "datadog.span.type", key.Type, FieldNumbers.HistogramDataPointAttributes);
                }

                WriteBoolAttribute(writer, "datadog.span.top_level", key.IsTopLevel, FieldNumbers.HistogramDataPointAttributes);

                if (key.IsSyntheticsRequest)
                {
                    WriteAttribute(writer, "datadog.origin", "synthetics", FieldNumbers.HistogramDataPointAttributes);
                }
            }

            WriteTag(writer, FieldNumbers.HistogramDataPointStartTimeUnixNano, WireTypeFixed64);
            writer.Write(startTimeUnixNano);

            WriteTag(writer, FieldNumbers.HistogramDataPointTimeUnixNano, WireTypeFixed64);
            writer.Write(endTimeUnixNano);

            WriteTag(writer, FieldNumbers.HistogramDataPointCount, WireTypeFixed64);
            writer.Write((ulong)bucket.Hits);

            WriteTag(writer, FieldNumbers.HistogramDataPointSum, WireTypeFixed64);
            writer.Write(TimeHelpers.NanosecondsToSeconds((long)bucket.Duration));

            // In OTLP mode errors go into a separate aggregation key, so OkSummary holds the full distribution.
            var bucketCounts = ProjectSketch(bucket.OkSummary);
            foreach (var bc in bucketCounts)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointBucketCounts, WireTypeFixed64);
                writer.Write(bc);
            }

            foreach (var bound in BoundsS)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointExplicitBounds, WireTypeFixed64);
                writer.Write(bound);
            }

            if (bucket.MinDuration < long.MaxValue)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointMin, WireTypeFixed64);
                writer.Write(TimeHelpers.NanosecondsToSeconds(bucket.MinDuration));
            }

            if (bucket.MaxDuration > 0)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointMax, WireTypeFixed64);
                writer.Write(TimeHelpers.NanosecondsToSeconds(bucket.MaxDuration));
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static ulong[] ProjectSketch(DDSketch sketch)
        {
            // 17 buckets: index 0 = underflow (<BoundsNs[0]), indices 1-15 = between consecutive bounds,
            // index 16 = overflow (≥BoundsNs[15]).
            var counts = new ulong[BoundsNs.Length + 1];

            // Zeros are durations that fell below the sketch's minimum indexed value.
            if (sketch.ZeroCount > 0)
            {
                counts[0] += (ulong)Math.Round(sketch.ZeroCount);
            }

            foreach (var bin in sketch.PositiveValueStore.EnumerateAscending())
            {
                if (bin.Count <= 0)
                {
                    continue;
                }

                var valueNs = sketch.IndexMapping.GetValue(bin.Index);
                var bucket = FindBucketIndex(valueNs);
                counts[bucket] += (ulong)Math.Round(bin.Count);
            }

            if (sketch.NegativeValueStore.TotalCount > 0)
            {
                Log.Debug("Span with negative duration detected (possible clock skew); excluded from OTLP histogram.");
            }

            return counts;
        }

        private static int FindBucketIndex(double valueNs)
        {
            // OTel histogram upper bounds are inclusive per spec.
            if (valueNs <= BoundsNs[0])
            {
                return 0;
            }

            for (int i = 1; i < BoundsNs.Length; i++)
            {
                if (valueNs <= BoundsNs[i])
                {
                    return i;
                }
            }

            return BoundsNs.Length; // overflow
        }

        private static string? NormalizeGrpcStatusName(string grpcStatusCode)
        {
            if (StringUtil.IsNullOrEmpty(grpcStatusCode))
            {
                return null;
            }

            // Numeric code path: "0"→"OK", "5"→"NOT_FOUND", etc.
            if (int.TryParse(grpcStatusCode, out var code) && (uint)code <= 16)
            {
                return GrpcStatusNames[code];
            }

            // Named code path: strip optional "StatusCode." prefix (e.g. "StatusCode.NotFound")
            var name = grpcStatusCode;
            if (name.Length > 11 && name.StartsWith("StatusCode.", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(11);
            }

            name = name.ToUpperInvariant();

            // Accept common single-L alias and compact form
            if (name == "CANCELED")
            {
                name = "CANCELLED";
            }
            else if (name == "NOTFOUND")
            {
                name = "NOT_FOUND";
            }

            for (int i = 0; i < GrpcStatusNames.Length; i++)
            {
                if (GrpcStatusNames[i] == name)
                {
                    return GrpcStatusNames[i];
                }
            }

            return null;
        }

        private static bool TryParseTag(byte[] tag, out string key, out string value)
        {
            var sep = Array.IndexOf(tag, (byte)':');
            if (sep <= 0)
            {
                key = value = string.Empty;
                return false;
            }

            key = Encoding.UTF8.GetString(tag, 0, sep);
            value = Encoding.UTF8.GetString(tag, sep + 1, tag.Length - sep - 1);
            return true;
        }

        private static void WriteAttribute(BinaryWriter writer, string key, string value, int fieldNumber = FieldNumbers.Attributes)
        {
            var kv = SerializeKeyValue(key, value);
            WriteTag(writer, fieldNumber, WireTypeLengthDelimited);
            WriteVarInt(writer, kv.Length);
            writer.Write(kv);
        }

        private static void WriteIntAttribute(BinaryWriter writer, string key, long value, int fieldNumber)
        {
            var kv = SerializeIntKeyValue(key, value);
            WriteTag(writer, fieldNumber, WireTypeLengthDelimited);
            WriteVarInt(writer, kv.Length);
            writer.Write(kv);
        }

        private static void WriteBoolAttribute(BinaryWriter writer, string key, bool value, int fieldNumber)
        {
            var kv = SerializeBoolKeyValue(key, value);
            WriteTag(writer, fieldNumber, WireTypeLengthDelimited);
            WriteVarInt(writer, kv.Length);
            writer.Write(kv);
        }

        private static byte[] SerializeKeyValue(string key, string value)
        {
            using var stream = new MemoryStream(64);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            WriteStringField(writer, FieldNumbers.Key, key);

            using var anyStream = new MemoryStream(32);
            using var anyWriter = new BinaryWriter(anyStream, Encoding.UTF8, leaveOpen: true);
            WriteStringField(anyWriter, AnyValueFieldNumbers.StringValue, value);
            anyWriter.Flush();
            var anyData = anyStream.ToArray();

            WriteTag(writer, FieldNumbers.Value, WireTypeLengthDelimited);
            WriteVarInt(writer, anyData.Length);
            writer.Write(anyData);

            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] SerializeIntKeyValue(string key, long value)
        {
            using var stream = new MemoryStream(32);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            WriteStringField(writer, FieldNumbers.Key, key);

            using var anyStream = new MemoryStream(16);
            using var anyWriter = new BinaryWriter(anyStream, Encoding.UTF8, leaveOpen: true);
            WriteTag(anyWriter, AnyValueFieldNumbers.IntValue, WireTypeVarInt);
            WriteVarInt(anyWriter, value);
            anyWriter.Flush();
            var anyData = anyStream.ToArray();

            WriteTag(writer, FieldNumbers.Value, WireTypeLengthDelimited);
            WriteVarInt(writer, anyData.Length);
            writer.Write(anyData);

            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] SerializeBoolKeyValue(string key, bool value)
        {
            using var stream = new MemoryStream(32);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            WriteStringField(writer, FieldNumbers.Key, key);

            using var anyStream = new MemoryStream(8);
            using var anyWriter = new BinaryWriter(anyStream, Encoding.UTF8, leaveOpen: true);
            WriteTag(anyWriter, AnyValueFieldNumbers.BoolValue, WireTypeVarInt);
            WriteVarInt(anyWriter, value ? 1 : 0);
            anyWriter.Flush();
            var anyData = anyStream.ToArray();

            WriteTag(writer, FieldNumbers.Value, WireTypeLengthDelimited);
            WriteVarInt(writer, anyData.Length);
            writer.Write(anyData);

            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteStringField(BinaryWriter writer, int fieldNumber, string value)
        {
            if (!StringUtil.IsNullOrEmpty(value))
            {
                WriteTag(writer, fieldNumber, WireTypeLengthDelimited);
                var bytes = Encoding.UTF8.GetBytes(value);
                WriteVarInt(writer, bytes.Length);
                writer.Write(bytes);
            }
        }

        private static void WriteTag(BinaryWriter writer, int fieldNumber, int wireType)
            => WriteVarInt(writer, (fieldNumber << 3) | wireType);

        private static void WriteVarInt(BinaryWriter writer, int value)
            => WriteVarInt(writer, (ulong)(uint)value);

        private static void WriteVarInt(BinaryWriter writer, long value)
            => WriteVarInt(writer, (ulong)value);

        private static void WriteVarInt(BinaryWriter writer, ulong value)
        {
            while (value >= 0x80)
            {
                writer.Write((byte)(value | 0x80));
                value >>= 7;
            }

            writer.Write((byte)value);
        }

        private static class FieldNumbers
        {
            // ExportMetricsServiceRequest
            public const int ResourceMetrics = 1;

            // ResourceMetrics
            public const int Resource = 1;
            public const int ScopeMetrics = 2;

            // Resource / KeyValue list
            public const int Attributes = 1;

            // ScopeMetrics
            public const int Metrics = 2;

            // Metric
            public const int MetricName = 1;
            public const int MetricUnit = 3;
            public const int Histogram = 9;

            // Histogram
            public const int DataPoints = 1;
            public const int AggregationTemporality = 2;

            // HistogramDataPoint
            public const int HistogramDataPointAttributes = 9;
            public const int HistogramDataPointStartTimeUnixNano = 2;
            public const int HistogramDataPointTimeUnixNano = 3;
            public const int HistogramDataPointCount = 4;
            public const int HistogramDataPointSum = 5;
            public const int HistogramDataPointBucketCounts = 6;
            public const int HistogramDataPointExplicitBounds = 7;
            public const int HistogramDataPointMin = 11;
            public const int HistogramDataPointMax = 12;

            // KeyValue
            public const int Key = 1;
            public const int Value = 2;
        }

        private static class AnyValueFieldNumbers
        {
            public const int StringValue = 1;
            public const int BoolValue = 2;
            public const int IntValue = 3;
        }
    }
}
