// <copyright file="OtlpSpanStatsSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent
{
    /// <summary>
    /// Serializes a <see cref="StatsBuffer"/> to an OTLP ExportMetricsServiceRequest payload
    /// (binary protobuf or JSON) containing a single <c>traces.span.sdk.metrics.duration</c> histogram metric.
    /// </summary>
    internal sealed class OtlpSpanStatsSerializer
    {
        // Protobuf wire types (used by protobuf serialization — added in a follow-up commit)
        private const int WireTypeVarInt = 0;
        private const int WireTypeFixed64 = 1;
        private const int WireTypeLengthDelimited = 2;

        // OTLP AggregationTemporality: DELTA = 1
        private const int AggregationTemporalityDelta = 1;

        // OTel status code for ERROR (proto enum value 2, also accepted as string "ERROR" / "STATUS_CODE_ERROR")
        private const long StatusCodeError = 2;

        internal const string MetricName = "traces.span.sdk.metrics.duration";
        private const string MetricUnit = "s";
        private const double NsToS = 1.0 / 1_000_000_000.0;

        // 16 explicit bounds (seconds) → 17 buckets.  Mirrors the OTel spanmetrics-connector defaults.
        private static readonly double[] BoundsS =
        [
            0.002, 0.004, 0.006, 0.008, 0.01, 0.05, 0.1, 0.2, 0.4, 0.8, 1, 1.4, 2, 5, 10, 15,
        ];

        // Same bounds converted to nanoseconds for comparison with sketch bin values.
        private static readonly double[] BoundsNs;

        static OtlpSpanStatsSerializer()
        {
            BoundsNs = new double[BoundsS.Length];
            for (int i = 0; i < BoundsS.Length; i++)
            {
                BoundsNs[i] = BoundsS[i] * 1_000_000_000.0;
            }
        }

        // ── JSON serialization entry point ─────────────────────────────────────────

        /// <summary>
        /// Returns a JSON-encoded <c>ExportMetricsServiceRequest</c> for the given stats buffer.
        /// Returns <c>null</c> when the buffer has no hits.
        /// </summary>
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

            WriteStringKvJson(writer, "telemetry.sdk.name", "datadog");
            WriteStringKvJson(writer, "telemetry.sdk.language", "dotnet");
            WriteStringKvJson(writer, "telemetry.sdk.version", TracerConstants.AssemblyVersion);
            WriteStringKvJson(writer, "service.name", details.DefaultServiceName);

            if (!StringUtil.IsNullOrEmpty(details.Environment))
            {
                WriteStringKvJson(writer, "deployment.environment.name", details.Environment!);
            }

            if (!StringUtil.IsNullOrEmpty(details.Version))
            {
                WriteStringKvJson(writer, "service.version", details.Version!);
            }

            if (!otelSemanticsEnabled)
            {
                WriteStringKvJson(writer, "datadog.runtime_id", Tracer.RuntimeId);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteScopeMetricsJson(JsonTextWriter writer, StatsBuffer buffer, long bucketDurationNs, bool otelSemanticsEnabled)
        {
            writer.WriteStartObject();
            // Intentionally no scope object per spec
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

            if (!StringUtil.IsNullOrEmpty(key.GrpcStatusCode) && int.TryParse(key.GrpcStatusCode, out var grpcCode))
            {
                WriteIntKvJson(writer, "rpc.response.status_code", grpcCode);
            }

            if (key.IsError)
            {
                WriteIntKvJson(writer, "status.code", (int)StatusCodeError);
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

            var count = (ulong)Math.Round(bucket.Hits);
            writer.WritePropertyName("count");
            writer.WriteValue(count.ToString());

            writer.WritePropertyName("sum");
            writer.WriteValue(bucket.Duration * NsToS);

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

            if (bucket.MinDuration < double.MaxValue)
            {
                writer.WritePropertyName("min");
                writer.WriteValue(bucket.MinDuration * NsToS);
            }

            if (bucket.MaxDuration > 0)
            {
                writer.WritePropertyName("max");
                writer.WriteValue(bucket.MaxDuration * NsToS);
            }

            writer.WriteEndObject();
        }

        // ── JSON attribute helpers ────────────────────────────────────────────────

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

        // ── DDSketch projection ───────────────────────────────────────────────────

        /// <summary>
        /// Projects the DDSketch distribution onto the 17 fixed OTLP histogram buckets.
        /// The result is approximate — see spec §3.1 exactness nuance.
        /// </summary>
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

            return counts;
        }

        private static int FindBucketIndex(double valueNs)
        {
            if (valueNs < BoundsNs[0])
            {
                return 0;
            }

            for (int i = 1; i < BoundsNs.Length; i++)
            {
                if (valueNs < BoundsNs[i])
                {
                    return i;
                }
            }

            return BoundsNs.Length; // overflow
        }
    }
}
