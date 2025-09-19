// <copyright file="OtlpMetricsSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Configuration;

#nullable enable

namespace Datadog.Trace.OTelMetrics
{
    /// <summary>
    /// High-performance OTLP protobuf serializer that creates binary protobuf payloads
    /// compliant with the OpenTelemetry ExportMetricsServiceRequest schema
    /// but does not require Google.Protobuf dependency (DDSketch + OpenTelemetry patterns).
    /// </summary>
    internal static class OtlpMetricsSerializer
    {
        // Protobuf wire types
        private const int VarInt = 0;
        private const int Fixed64 = 1;
        private const int LengthDelimited = 2;

        private static byte[] SerializeResourceMetrics(List<MetricPoint> metrics, TracerSettings settings)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Resource
            var resourceData = SerializeResource(settings);
            WriteTag(writer, FieldNumbers.Resource, LengthDelimited);
            WriteVarInt(writer, resourceData.Length);
            writer.Write(resourceData);

            // ScopeMetrics
            var scopeMetricsData = SerializeScopeMetrics(metrics, settings);
            WriteTag(writer, FieldNumbers.ScopeMetrics, LengthDelimited);
            WriteVarInt(writer, scopeMetricsData.Length);
            writer.Write(scopeMetricsData);

            return buffer.ToArray();
        }

        private static byte[] SerializeResource(TracerSettings settings)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // telemetry.sdk.name attribute (use 'opentelemetry' for ecosystem compatibility)
            var sdkNameAttr = SerializeKeyValue("telemetry.sdk.name", "opentelemetry");
            WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
            WriteVarInt(writer, sdkNameAttr.Length);
            writer.Write(sdkNameAttr);

            // telemetry.sdk.language attribute
            var sdkLangAttr = SerializeKeyValue("telemetry.sdk.language", "dotnet");
            WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
            WriteVarInt(writer, sdkLangAttr.Length);
            writer.Write(sdkLangAttr);

            // telemetry.sdk.version attribute
            var sdkVersionAttr = SerializeKeyValue("telemetry.sdk.version", TracerConstants.AssemblyVersion);
            WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
            WriteVarInt(writer, sdkVersionAttr.Length);
            writer.Write(sdkVersionAttr);

            // service.name (required) - TracerSettings already applies RFC precedence: DD_SERVICE > DD_TAGS["service"] > OTEL_RESOURCE_ATTRIBUTES["service.name"]
            var serviceName = settings.ServiceName ?? "unknown_service:dotnet";
            var serviceNameAttr = SerializeKeyValue("service.name", serviceName);
            WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
            WriteVarInt(writer, serviceNameAttr.Length);
            writer.Write(serviceNameAttr);

            // deployment.environment.name (optional) - TracerSettings already applies RFC precedence: DD_ENV > DD_TAGS["env"] > OTEL_RESOURCE_ATTRIBUTES["deployment.environment.name"] > OTEL_RESOURCE_ATTRIBUTES["deployment.environment"]
            if (!string.IsNullOrEmpty(settings.Environment))
            {
                var envAttr = SerializeKeyValue("deployment.environment.name", settings.Environment);
                WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
                WriteVarInt(writer, envAttr.Length);
                writer.Write(envAttr);
            }

            // service.version (optional) - TracerSettings already applies RFC precedence: DD_VERSION > DD_TAGS["version"] > OTEL_RESOURCE_ATTRIBUTES["service.version"]
            if (!string.IsNullOrEmpty(settings.ServiceVersion))
            {
                var versionAttr = SerializeKeyValue("service.version", settings.ServiceVersion);
                WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
                WriteVarInt(writer, versionAttr.Length);
                writer.Write(versionAttr);
            }

            // Additional attributes from DD_TAGS/OTEL_RESOURCE_ATTRIBUTES (RFC requirement)
            // These are already processed by TracerSettings and available in GlobalTags
            if (settings.GlobalTags.Count > 0)
            {
                foreach (var tag in settings.GlobalTags)
                {
                    // Skip tags that we've already handled as specific resource attributes
                    // Note: We need to check both the original tag key and the mapped resource attribute names
                    if (IsHandledResourceAttribute(tag.Key))
                    {
                        continue;
                    }

                    // Add other tags as resource attributes
                    var tagAttr = SerializeKeyValue(tag.Key, tag.Value);
                    WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
                    WriteVarInt(writer, tagAttr.Length);
                    writer.Write(tagAttr);
                }
            }

            return buffer.ToArray();
        }

        /// <summary>
        /// Checks if a tag key represents a resource attribute that has already been handled
        /// </summary>
        private static bool IsHandledResourceAttribute(string tagKey)
        {
            return tagKey.Equals("service", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("env", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("version", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("service.name", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("deployment.environment.name", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("deployment.environment", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("service.version", StringComparison.OrdinalIgnoreCase);
        }

        private static byte[] SerializeScopeMetrics(List<MetricPoint> metrics, TracerSettings settings)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Get the meter name from the first metric (they should all be from the same meter)
            // Direct access since we now receive List<MetricPoint> (maximum performance!)
            string meterName = "OpenTelemetryMetricsMeter";
            if (metrics.Count > 0)
            {
                meterName = metrics[0].MeterName;
            }

            // InstrumentationScope
            var scopeData = SerializeInstrumentationScope(meterName);
            WriteTag(writer, FieldNumbers.Scope, LengthDelimited);
            WriteVarInt(writer, scopeData.Length);
            writer.Write(scopeData);

            // Metrics (use for loop for maximum performance, skip unsupported types)
            for (int i = 0; i < metrics.Count; i++)
            {
                var metric = metrics[i];
                byte[]? metricData = null;

                if (metric.InstrumentType == InstrumentType.Counter || metric.InstrumentType == InstrumentType.UpDownCounter ||
                    metric.InstrumentType == InstrumentType.ObservableCounter || metric.InstrumentType == InstrumentType.ObservableUpDownCounter)
                {
                    metricData = SerializeCounterMetric(metric, settings);
                }
                else if (metric.InstrumentType == InstrumentType.Gauge || metric.InstrumentType == InstrumentType.ObservableGauge)
                {
                    metricData = SerializeGaugeMetric(metric);
                }
                else if (metric.InstrumentType == InstrumentType.Histogram)
                {
                    metricData = SerializeHistogramMetric(metric, settings);
                }

                if (metricData != null)
                {
                    WriteTag(writer, FieldNumbers.Metrics, LengthDelimited);
                    WriteVarInt(writer, metricData.Length);
                    writer.Write(metricData);
                }
            }

            return buffer.ToArray();
        }

        private static byte[] SerializeInstrumentationScope(string meterName)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Name (use actual meter name)
            WriteStringField(writer, FieldNumbers.Name, meterName);

            // Version (empty like OTel does)
            WriteStringField(writer, FieldNumbers.Version, string.Empty);

            return buffer.ToArray();
        }

        private static byte[] SerializeCounterMetric(MetricPoint metric, TracerSettings settings)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Name
            WriteStringField(writer, FieldNumbers.MetricName, metric.InstrumentName);

            // Description (empty for now)
            WriteStringField(writer, FieldNumbers.Description, string.Empty);

            // Unit (empty for now)
            WriteStringField(writer, FieldNumbers.Unit, string.Empty);

            // Sum
            var sumData = SerializeSum(metric, settings);
            WriteTag(writer, FieldNumbers.Sum, LengthDelimited);
            WriteVarInt(writer, sumData.Length);
            writer.Write(sumData);

            return buffer.ToArray();
        }

        private static byte[] SerializeGaugeMetric(MetricPoint metric)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Name
            WriteStringField(writer, FieldNumbers.MetricName, metric.InstrumentName);

            // Description (empty for now)
            WriteStringField(writer, FieldNumbers.Description, string.Empty);

            // Unit (empty for now)
            WriteStringField(writer, FieldNumbers.Unit, string.Empty);

            // Gauge (no temporality for gauges)
            var gaugeData = SerializeGauge(metric);
            WriteTag(writer, FieldNumbers.Gauge, LengthDelimited);
            WriteVarInt(writer, gaugeData.Length);
            writer.Write(gaugeData);

            return buffer.ToArray();
        }

        private static byte[] SerializeHistogramMetric(MetricPoint metric, TracerSettings settings)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Name
            WriteStringField(writer, FieldNumbers.MetricName, metric.InstrumentName);

            // Description (empty for now)
            WriteStringField(writer, FieldNumbers.Description, string.Empty);

            // Unit (empty for now)
            WriteStringField(writer, FieldNumbers.Unit, string.Empty);

            // Histogram
            var histogramData = SerializeHistogram(metric, settings);
            WriteTag(writer, FieldNumbers.Histogram, LengthDelimited);
            WriteVarInt(writer, histogramData.Length);
            writer.Write(histogramData);

            return buffer.ToArray();
        }

        private static byte[] SerializeSum(MetricPoint metric, TracerSettings settings)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // DataPoints
            var dataPointData = SerializeNumberDataPoint(metric);
            WriteTag(writer, FieldNumbers.DataPoints, LengthDelimited);
            WriteVarInt(writer, dataPointData.Length);
            writer.Write(dataPointData);

            // AggregationTemporality (RFC-compliant mapping based on instrument type and preference)
            int temporality;
            bool isMonotonic;

            if (metric.InstrumentType == InstrumentType.UpDownCounter || metric.InstrumentType == InstrumentType.ObservableUpDownCounter)
            {
                // UpDownCounter/ObservableUpDownCounter: always Cumulative (per RFC table), not monotonic
                temporality = 2; // Cumulative
                isMonotonic = false;
            }
            else
            {
                // Regular Counter/ObservableCounter: Delta for Delta/LowMemory, Cumulative for Cumulative preference
                temporality = settings.OtlpMetricsTemporalityPreference switch
                {
                    OtlpTemporality.Delta => 1,      // Delta
                    OtlpTemporality.LowMemory => 1,  // Delta (per RFC)
                    OtlpTemporality.Cumulative => 2, // Cumulative
                    _ => 1 // Default to Delta
                };
                isMonotonic = true;
            }

            WriteTag(writer, FieldNumbers.AggregationTemporality, VarInt);
            WriteVarInt(writer, temporality);

            WriteTag(writer, FieldNumbers.IsMonotonic, VarInt);
            WriteVarInt(writer, isMonotonic ? 1 : 0);

            return buffer.ToArray();
        }

        private static byte[] SerializeGauge(MetricPoint metric)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // DataPoints (Gauge only has data points, no temporality)
            var dataPointData = SerializeNumberDataPointForGauge(metric);
            WriteTag(writer, FieldNumbers.DataPoints, LengthDelimited);
            WriteVarInt(writer, dataPointData.Length);
            writer.Write(dataPointData);

            return buffer.ToArray();
        }

        private static byte[] SerializeHistogram(MetricPoint metric, TracerSettings settings)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // DataPoints
            var dataPointData = SerializeHistogramDataPoint(metric);
            WriteTag(writer, FieldNumbers.DataPoints, LengthDelimited);
            WriteVarInt(writer, dataPointData.Length);
            writer.Write(dataPointData);

            // AggregationTemporality (RFC-compliant: Delta for Delta/LowMemory, Cumulative for Cumulative)
            var temporality = settings.OtlpMetricsTemporalityPreference switch
            {
                OtlpTemporality.Delta => 1,      // Delta
                OtlpTemporality.LowMemory => 1,  // Delta (per RFC)
                OtlpTemporality.Cumulative => 2, // Cumulative
                _ => 1 // Default to Delta
            };
            WriteTag(writer, FieldNumbers.AggregationTemporality, VarInt);
            WriteVarInt(writer, temporality);

            return buffer.ToArray();
        }

        private static byte[] SerializeNumberDataPoint(MetricPoint metric)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Attributes (tags)
            foreach (var tag in metric.Tags)
            {
                var attrData = SerializeKeyValue(tag.Key, tag.Value?.ToString() ?? string.Empty);
                WriteTag(writer, FieldNumbers.NumberDataPointAttributes, LengthDelimited);
                WriteVarInt(writer, attrData.Length);
                writer.Write(attrData);
            }

            // StartTimeUnixNano
            WriteTag(writer, FieldNumbers.NumberDataPointStartTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.StartTime.ToUnixTimeNanoseconds());

            // TimeUnixNano
            WriteTag(writer, FieldNumbers.NumberDataPointTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.EndTime.ToUnixTimeNanoseconds());

            // Value (use AsInt for integer measurements, AsDouble for floating point)
            if (metric.IsIntegerValue)
            {
                WriteTag(writer, FieldNumbers.NumberDataPointAsInt, Fixed64);
                writer.Write((long)metric.SnapshotSum);
            }
            else
            {
                WriteTag(writer, FieldNumbers.NumberDataPointAsDouble, Fixed64);
                writer.Write(metric.SnapshotSum);
            }

            return buffer.ToArray();
        }

        private static byte[] SerializeNumberDataPointForGauge(MetricPoint metric)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Attributes (tags)
            foreach (var tag in metric.Tags)
            {
                var attrData = SerializeKeyValue(tag.Key, tag.Value?.ToString() ?? string.Empty);
                WriteTag(writer, FieldNumbers.NumberDataPointAttributes, LengthDelimited);
                WriteVarInt(writer, attrData.Length);
                writer.Write(attrData);
            }

            // StartTimeUnixNano
            WriteTag(writer, FieldNumbers.NumberDataPointStartTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.StartTime.ToUnixTimeNanoseconds());

            // TimeUnixNano
            WriteTag(writer, FieldNumbers.NumberDataPointTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.EndTime.ToUnixTimeNanoseconds());

            // Value (use gauge value, not sum)
            if (metric.IsIntegerValue)
            {
                WriteTag(writer, FieldNumbers.NumberDataPointAsInt, Fixed64);
                writer.Write((long)metric.SnapshotGaugeValue);
            }
            else
            {
                WriteTag(writer, FieldNumbers.NumberDataPointAsDouble, Fixed64);
                writer.Write(metric.SnapshotGaugeValue);
            }

            return buffer.ToArray();
        }

        private static byte[] SerializeHistogramDataPoint(MetricPoint metric)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Attributes (tags)
            foreach (var tag in metric.Tags)
            {
                var attrData = SerializeKeyValue(tag.Key, tag.Value?.ToString() ?? string.Empty);
                WriteTag(writer, FieldNumbers.HistogramDataPointAttributes, LengthDelimited);
                WriteVarInt(writer, attrData.Length);
                writer.Write(attrData);
            }

            // StartTimeUnixNano
            WriteTag(writer, FieldNumbers.HistogramDataPointStartTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.StartTime.ToUnixTimeNanoseconds());

            // TimeUnixNano
            WriteTag(writer, FieldNumbers.HistogramDataPointTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.EndTime.ToUnixTimeNanoseconds());

            // Count
            WriteTag(writer, FieldNumbers.HistogramDataPointCount, Fixed64);
            writer.Write((ulong)metric.SnapshotCount);

            // Sum (always write for histograms, even if 0)
            WriteTag(writer, FieldNumbers.HistogramDataPointSum, Fixed64);
            writer.Write(metric.SnapshotSum);

            // BucketCounts (always write all buckets)
            for (int i = 0; i < metric.SnapshotBucketCounts.Length; i++)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointBucketCounts, Fixed64);
                writer.Write((ulong)metric.SnapshotBucketCounts[i]);
            }

            // ExplicitBounds (always write all bounds)
            var bounds = MetricPoint.DefaultHistogramBounds;
            for (int i = 0; i < bounds.Length; i++)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointExplicitBounds, Fixed64);
                writer.Write(bounds[i]);
            }

            // Min (write if we have any measurements)
            if (metric.SnapshotCount > 0)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointMin, Fixed64);
                writer.Write(metric.SnapshotMin);
            }

            // Max (write if we have any measurements)
            if (metric.SnapshotCount > 0)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointMax, Fixed64);
                writer.Write(metric.SnapshotMax);
            }

            return buffer.ToArray();
        }

        private static byte[] SerializeKeyValue(string key, string value)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Key
            WriteStringField(writer, FieldNumbers.Key, key);

            // Value (AnyValue with StringValue)
            var anyValueData = SerializeStringAnyValue(value);
            WriteTag(writer, FieldNumbers.Value, LengthDelimited);
            WriteVarInt(writer, anyValueData.Length);
            writer.Write(anyValueData);

            return buffer.ToArray();
        }

        private static byte[] SerializeStringAnyValue(string value)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            WriteStringField(writer, FieldNumbers.StringValue, value);

            return buffer.ToArray();
        }

        private static void WriteStringField(BinaryWriter writer, int fieldNumber, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                WriteTag(writer, fieldNumber, LengthDelimited);
                WriteString(writer, value);
            }
        }

        private static void WriteTag(BinaryWriter writer, int fieldNumber, int wireType)
        {
            WriteVarInt(writer, (fieldNumber << 3) | wireType);
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteVarInt(writer, bytes.Length);
            writer.Write(bytes);
        }

        private static void WriteVarInt(BinaryWriter writer, int value)
        {
            WriteVarInt(writer, (uint)value);
        }

        private static void WriteVarInt(BinaryWriter writer, uint value)
        {
            while (value >= 0x80)
            {
                writer.Write((byte)(value | 0x80));
                value >>= 7;
            }

            writer.Write((byte)value);
        }

        /// <summary>
        /// Serializes metrics to OTLP ExportMetricsServiceRequest binary format
        /// High-performance: synchronous serialization, no external dependencies (Datadog pattern)
        /// Supports: Counter, Gauge metric types
        /// </summary>
        public static byte[] SerializeMetrics(List<MetricPoint> metrics, TracerSettings settings)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Build ResourceMetrics message (synchronous serialization - Datadog pattern!)
            var resourceMetricsData = SerializeResourceMetrics(metrics, settings);

            // Write ExportMetricsServiceRequest
            WriteTag(writer, FieldNumbers.ResourceMetrics, LengthDelimited);
            WriteVarInt(writer, resourceMetricsData.Length);
            writer.Write(resourceMetricsData);

            return buffer.ToArray();
        }

        // Field numbers from OTLP proto schema
        public static class FieldNumbers
        {
            // ExportMetricsServiceRequest
            public const int ResourceMetrics = 1;

            // ResourceMetrics
            public const int Resource = 1;
            public const int ScopeMetrics = 2;
            public const int SchemaUrl = 3;

            // Resource
            public const int Attributes = 1;
            public const int DroppedAttributesCount = 2;

            // ScopeMetrics
            public const int Scope = 1;
            public const int Metrics = 2;

            // InstrumentationScope
            public const int Name = 1;
            public const int Version = 2;

            // Metric
            public const int MetricName = 1;
            public const int Description = 2;
            public const int Unit = 3;
            public const int Gauge = 5;
            public const int Sum = 7;
            public const int Histogram = 9;

            // Sum
            public const int DataPoints = 1;
            public const int AggregationTemporality = 2;
            public const int IsMonotonic = 3;

            // NumberDataPoint
            public const int NumberDataPointAttributes = 7;
            public const int NumberDataPointStartTimeUnixNano = 2;
            public const int NumberDataPointTimeUnixNano = 3;
            public const int NumberDataPointAsDouble = 4;
            public const int NumberDataPointAsInt = 6;

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

            // AnyValue
            public const int StringValue = 1;
        }
    }
}

#endif
