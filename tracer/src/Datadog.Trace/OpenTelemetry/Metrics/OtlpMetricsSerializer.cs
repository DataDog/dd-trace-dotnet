// <copyright file="OtlpMetricsSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Trace.Configuration;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Metrics
{
    /// <summary>
    /// OTLP protobuf serializer that creates binary protobuf payloads
    /// compliant with the OpenTelemetry ExportMetricsServiceRequest schema.
    /// </summary>
    internal sealed class OtlpMetricsSerializer
    {
        // Protobuf wire types
        private const int VarInt = 0;
        private const int Fixed64 = 1;
        private const int LengthDelimited = 2;

        private readonly TracerSettings _settings;
        private byte[] _cachedResourceData;

        public OtlpMetricsSerializer(TracerSettings settings)
        {
            _settings = settings;
            UpdateCachedResourceData(settings.Manager.InitialMutableSettings);
            settings.Manager.SubscribeToChanges(changes =>
            {
                if (changes.UpdatedMutable is { } mutable)
                {
                    UpdateCachedResourceData(mutable);
                }
            });
            [MemberNotNull(nameof(_cachedResourceData))]
            void UpdateCachedResourceData(MutableSettings mutable)
            {
                Interlocked.Exchange(ref _cachedResourceData, SerializeResource(mutable));
            }
        }

        /// <summary>
        /// Creates a deterministic string key from meter tags for grouping.
        /// Sorts a copy to avoid mutating the original array.
        /// </summary>
        private static string JoinMeterTags(KeyValuePair<string, object?>[] tags)
        {
            if (tags.Length == 0)
            {
                return string.Empty;
            }

            var copy = new KeyValuePair<string, object?>[tags.Length];
            Array.Copy(tags, copy, tags.Length);
            Array.Sort(copy, (a, b) => string.CompareOrdinal(a.Key, b.Key));

            var sb = new StringBuilder();
            for (int i = 0; i < copy.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(';');
                }

                sb.Append(copy[i].Key).Append('=');
                if (copy[i].Value is not null)
                {
                    sb.Append(copy[i].Value);
                }
            }

            return sb.ToString();
        }

        private byte[] SerializeResourceMetrics(IEnumerable<MetricPoint> metrics)
        {
            using var buffer = new MemoryStream(512);
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            WriteTag(writer, FieldNumbers.Resource, LengthDelimited);
            var data = Volatile.Read(ref _cachedResourceData);
            WriteVarInt(writer, data.Length);
            writer.Write(data);

            // Group metrics by meter identity (name + version + tags)
            var meterGroups = new Dictionary<string, List<MetricPoint>>();
            foreach (var metric in metrics)
            {
                var meterKey = $"{metric.MeterName}|{metric.MeterVersion}|{JoinMeterTags(metric.MeterTags)}";

                if (!meterGroups.TryGetValue(meterKey, out var meterMetrics))
                {
                    meterMetrics = new List<MetricPoint>();
                    meterGroups[meterKey] = meterMetrics;
                }

                meterMetrics.Add(metric);
            }

            foreach (var meterMetrics in meterGroups.Values)
            {
                var scopeMetricsData = SerializeScopeMetrics(meterMetrics);
                WriteTag(writer, FieldNumbers.ScopeMetrics, LengthDelimited);
                WriteVarInt(writer, scopeMetricsData.Length);
                writer.Write(scopeMetricsData);
            }

            return buffer.ToArray();
        }

        private byte[] SerializeResource(MutableSettings settings)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            var sdkNameAttr = SerializeKeyValue("telemetry.sdk.name", "datadog");
            WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
            WriteVarInt(writer, sdkNameAttr.Length);
            writer.Write(sdkNameAttr);

            var sdkLangAttr = SerializeKeyValue("telemetry.sdk.language", "dotnet");
            WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
            WriteVarInt(writer, sdkLangAttr.Length);
            writer.Write(sdkLangAttr);

            var sdkVersionAttr = SerializeKeyValue("telemetry.sdk.version", TracerConstants.AssemblyVersion);
            WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
            WriteVarInt(writer, sdkVersionAttr.Length);
            writer.Write(sdkVersionAttr);

            var serviceNameAttr = SerializeKeyValue("service.name", settings.DefaultServiceName);
            WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
            WriteVarInt(writer, serviceNameAttr.Length);
            writer.Write(serviceNameAttr);

            if (!string.IsNullOrEmpty(settings.Environment))
            {
                var envAttr = SerializeKeyValue("deployment.environment.name", settings.Environment);
                WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
                WriteVarInt(writer, envAttr.Length);
                writer.Write(envAttr);
            }

            if (!string.IsNullOrEmpty(settings.ServiceVersion))
            {
                var versionAttr = SerializeKeyValue("service.version", settings.ServiceVersion);
                WriteTag(writer, FieldNumbers.Attributes, LengthDelimited);
                WriteVarInt(writer, versionAttr.Length);
                writer.Write(versionAttr);
            }

            if (settings.GlobalTags.Count > 0)
            {
                foreach (var tag in settings.GlobalTags)
                {
                    if (IsHandledResourceAttribute(tag.Key))
                    {
                        continue;
                    }

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
        private bool IsHandledResourceAttribute(string tagKey)
        {
            return tagKey.Equals("service", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("env", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("version", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("service.name", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("deployment.environment.name", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("deployment.environment", StringComparison.OrdinalIgnoreCase) ||
                   tagKey.Equals("service.version", StringComparison.OrdinalIgnoreCase);
        }

        private byte[] SerializeScopeMetrics(IReadOnlyList<MetricPoint> metrics)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            var meterName = string.Empty;
            var meterVersion = string.Empty;
            var meterTags = Array.Empty<KeyValuePair<string, object?>>();

            if (metrics.Count > 0)
            {
                meterName = metrics[0].MeterName;
                meterVersion = metrics[0].MeterVersion;
                meterTags = metrics[0].MeterTags;
            }

            var scopeData = SerializeInstrumentationScope(meterName, meterVersion, meterTags);
            WriteTag(writer, FieldNumbers.Scope, LengthDelimited);
            WriteVarInt(writer, scopeData.Length);
            writer.Write(scopeData);

            var metricGroups = new Dictionary<string, List<MetricPoint>>();
            for (int i = 0; i < metrics.Count; i++)
            {
                var metric = metrics[i];
                var key = $"{metric.InstrumentName}|{metric.InstrumentType}|{metric.Unit}|{metric.Description}";

                if (!metricGroups.TryGetValue(key, out var group))
                {
                    group = new List<MetricPoint>();
                    metricGroups[key] = group;
                }

                group.Add(metric);
            }

            foreach (var group in metricGroups.Values)
            {
                byte[]? metricData = null;
                var firstMetric = group[0];

                if (firstMetric.InstrumentType is InstrumentType.Counter
                    || firstMetric.InstrumentType is InstrumentType.UpDownCounter
                    || firstMetric.InstrumentType is InstrumentType.ObservableCounter
                    || firstMetric.InstrumentType is InstrumentType.ObservableUpDownCounter)
                {
                    metricData = SerializeCounterMetric(group);
                }
                else if (firstMetric.InstrumentType is InstrumentType.Gauge
                         || firstMetric.InstrumentType is InstrumentType.ObservableGauge)
                {
                    metricData = SerializeGaugeMetric(group);
                }
                else if (firstMetric.InstrumentType is InstrumentType.Histogram)
                {
                    metricData = SerializeHistogramMetric(group);
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

        private byte[] SerializeInstrumentationScope(string meterName, string meterVersion, KeyValuePair<string, object?>[] meterTags)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            WriteStringField(writer, FieldNumbers.Name, meterName);
            WriteStringField(writer, FieldNumbers.Version, meterVersion);

            for (int i = 0; i < meterTags.Length; i++)
            {
                var tag = meterTags[i];
                var attributeData = SerializeKeyValue(tag.Key, tag.Value?.ToString() ?? string.Empty);
                WriteTag(writer, FieldNumbers.ScopeAttributes, LengthDelimited);
                WriteVarInt(writer, attributeData.Length);
                writer.Write(attributeData);
            }

            return buffer.ToArray();
        }

        private byte[] SerializeCounterMetric(List<MetricPoint> metrics)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            var firstMetric = metrics[0];
            WriteStringField(writer, FieldNumbers.MetricName, firstMetric.InstrumentName);
            WriteStringField(writer, FieldNumbers.Description, firstMetric.Description);
            WriteStringField(writer, FieldNumbers.Unit, firstMetric.Unit);

            var sumData = SerializeSum(metrics);
            WriteTag(writer, FieldNumbers.Sum, LengthDelimited);
            WriteVarInt(writer, sumData.Length);
            writer.Write(sumData);

            return buffer.ToArray();
        }

        private byte[] SerializeGaugeMetric(List<MetricPoint> metrics)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            var firstMetric = metrics[0];
            WriteStringField(writer, FieldNumbers.MetricName, firstMetric.InstrumentName);
            WriteStringField(writer, FieldNumbers.Description, firstMetric.Description);
            WriteStringField(writer, FieldNumbers.Unit, firstMetric.Unit);

            var gaugeData = SerializeGauge(metrics);
            WriteTag(writer, FieldNumbers.Gauge, LengthDelimited);
            WriteVarInt(writer, gaugeData.Length);
            writer.Write(gaugeData);

            return buffer.ToArray();
        }

        private byte[] SerializeHistogramMetric(List<MetricPoint> metrics)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            var firstMetric = metrics[0];
            WriteStringField(writer, FieldNumbers.MetricName, firstMetric.InstrumentName);
            WriteStringField(writer, FieldNumbers.Description, firstMetric.Description);
            WriteStringField(writer, FieldNumbers.Unit, firstMetric.Unit);

            var histogramData = SerializeHistogram(metrics);
            WriteTag(writer, FieldNumbers.Histogram, LengthDelimited);
            WriteVarInt(writer, histogramData.Length);
            writer.Write(histogramData);

            return buffer.ToArray();
        }

        private byte[] SerializeSum(List<MetricPoint> metrics)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            for (int i = 0; i < metrics.Count; i++)
            {
                var dataPointData = SerializeNumberDataPoint(metrics[i]);
                WriteTag(writer, FieldNumbers.DataPoints, LengthDelimited);
                WriteVarInt(writer, dataPointData.Length);
                writer.Write(dataPointData);
            }

            var firstMetric = metrics[0];
            AggregationTemporality temporality;
            bool isMonotonic;

            if (firstMetric.InstrumentType is InstrumentType.UpDownCounter or InstrumentType.ObservableUpDownCounter)
            {
                temporality = AggregationTemporality.Cumulative;
                isMonotonic = false;
            }
            else
            {
                temporality = _settings.OtlpMetricsTemporalityPreference switch
                {
                    OtlpTemporalityPreference.Delta => AggregationTemporality.Delta,
                    OtlpTemporalityPreference.LowMemory => AggregationTemporality.Delta,
                    OtlpTemporalityPreference.Cumulative => AggregationTemporality.Cumulative,
                    _ => AggregationTemporality.Delta
                };
                isMonotonic = true;
            }

            WriteTag(writer, FieldNumbers.AggregationTemporality, VarInt);
            WriteVarInt(writer, (int)temporality);

            WriteTag(writer, FieldNumbers.IsMonotonic, VarInt);
            WriteVarInt(writer, isMonotonic ? 1 : 0);

            return buffer.ToArray();
        }

        private byte[] SerializeGauge(List<MetricPoint> metrics)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            for (int i = 0; i < metrics.Count; i++)
            {
                var dataPointData = SerializeNumberDataPointForGauge(metrics[i]);
                WriteTag(writer, FieldNumbers.DataPoints, LengthDelimited);
                WriteVarInt(writer, dataPointData.Length);
                writer.Write(dataPointData);
            }

            return buffer.ToArray();
        }

        private byte[] SerializeHistogram(List<MetricPoint> metrics)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            for (int i = 0; i < metrics.Count; i++)
            {
                var dataPointData = SerializeHistogramDataPoint(metrics[i]);
                WriteTag(writer, FieldNumbers.DataPoints, LengthDelimited);
                WriteVarInt(writer, dataPointData.Length);
                writer.Write(dataPointData);
            }

            var temporality = _settings.OtlpMetricsTemporalityPreference switch
            {
                OtlpTemporalityPreference.Delta => AggregationTemporality.Delta,
                OtlpTemporalityPreference.LowMemory => AggregationTemporality.Delta,
                OtlpTemporalityPreference.Cumulative => AggregationTemporality.Cumulative,
                _ => AggregationTemporality.Delta
            };
            WriteTag(writer, FieldNumbers.AggregationTemporality, VarInt);
            WriteVarInt(writer, (int)temporality);

            return buffer.ToArray();
        }

        private byte[] SerializeNumberDataPoint(MetricPoint metric)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            foreach (var tag in metric.Tags)
            {
                var attrData = SerializeKeyValue(tag.Key, tag.Value?.ToString() ?? string.Empty);
                WriteTag(writer, FieldNumbers.NumberDataPointAttributes, LengthDelimited);
                WriteVarInt(writer, attrData.Length);
                writer.Write(attrData);
            }

            WriteTag(writer, FieldNumbers.NumberDataPointStartTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.StartTime.ToUnixTimeNanoseconds());

            WriteTag(writer, FieldNumbers.NumberDataPointTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.EndTime.ToUnixTimeNanoseconds());

            if (metric.IsLongType)
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

        private byte[] SerializeNumberDataPointForGauge(MetricPoint metric)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            foreach (var tag in metric.Tags)
            {
                var attrData = SerializeKeyValue(tag.Key, tag.Value?.ToString() ?? string.Empty);
                WriteTag(writer, FieldNumbers.NumberDataPointAttributes, LengthDelimited);
                WriteVarInt(writer, attrData.Length);
                writer.Write(attrData);
            }

            WriteTag(writer, FieldNumbers.NumberDataPointStartTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.StartTime.ToUnixTimeNanoseconds());

            WriteTag(writer, FieldNumbers.NumberDataPointTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.EndTime.ToUnixTimeNanoseconds());

            WriteTag(writer, FieldNumbers.NumberDataPointAsDouble, Fixed64);
            writer.Write(metric.SnapshotGaugeValue);

            return buffer.ToArray();
        }

        private byte[] SerializeHistogramDataPoint(MetricPoint metric)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            foreach (var tag in metric.Tags)
            {
                var attrData = SerializeKeyValue(tag.Key, tag.Value?.ToString() ?? string.Empty);
                WriteTag(writer, FieldNumbers.HistogramDataPointAttributes, LengthDelimited);
                WriteVarInt(writer, attrData.Length);
                writer.Write(attrData);
            }

            WriteTag(writer, FieldNumbers.HistogramDataPointStartTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.StartTime.ToUnixTimeNanoseconds());

            WriteTag(writer, FieldNumbers.HistogramDataPointTimeUnixNano, Fixed64);
            writer.Write((ulong)metric.EndTime.ToUnixTimeNanoseconds());

            WriteTag(writer, FieldNumbers.HistogramDataPointCount, Fixed64);
            writer.Write((ulong)metric.SnapshotCount);

            WriteTag(writer, FieldNumbers.HistogramDataPointSum, Fixed64);
            writer.Write(metric.SnapshotSum);

            for (int i = 0; i < metric.SnapshotBucketCounts.Length; i++)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointBucketCounts, Fixed64);
                writer.Write((ulong)metric.SnapshotBucketCounts[i]);
            }

            for (int i = 0; i < metric.SnapshotBucketBounds.Length; i++)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointExplicitBounds, Fixed64);
                writer.Write(metric.SnapshotBucketBounds[i]);
            }

            if (metric.SnapshotCount > 0 & metric.SnapshotMin != double.NaN)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointMin, Fixed64);
                writer.Write(metric.SnapshotMin);
            }

            if (metric.SnapshotCount > 0 & metric.SnapshotMax != double.NaN)
            {
                WriteTag(writer, FieldNumbers.HistogramDataPointMax, Fixed64);
                writer.Write(metric.SnapshotMax);
            }

            return buffer.ToArray();
        }

        private byte[] SerializeKeyValue(string key, string value)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            WriteStringField(writer, FieldNumbers.Key, key);

            var anyValueData = SerializeStringAnyValue(value);
            WriteTag(writer, FieldNumbers.Value, LengthDelimited);
            WriteVarInt(writer, anyValueData.Length);
            writer.Write(anyValueData);

            return buffer.ToArray();
        }

        private byte[] SerializeStringAnyValue(string value)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            WriteStringField(writer, FieldNumbers.StringValue, value);

            return buffer.ToArray();
        }

        private void WriteStringField(BinaryWriter writer, int fieldNumber, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                WriteTag(writer, fieldNumber, LengthDelimited);
                WriteString(writer, value);
            }
        }

        private void WriteTag(BinaryWriter writer, int fieldNumber, int wireType)
        {
            WriteVarInt(writer, (fieldNumber << 3) | wireType);
        }

        private void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteVarInt(writer, bytes.Length);
            writer.Write(bytes);
        }

        private void WriteVarInt(BinaryWriter writer, int value)
        {
            WriteVarInt(writer, (uint)value);
        }

        private void WriteVarInt(BinaryWriter writer, uint value)
        {
            while (value >= 0x80)
            {
                writer.Write((byte)(value | 0x80));
                value >>= 7;
            }

            writer.Write((byte)value);
        }

        /// <summary>
        /// Serializes metrics to OTLP MetricsData binary format
        /// </summary>
        /// <param name="metrics">The metrics to serialize</param>
        /// <param name="startPosition">Optional start position to leave empty bytes at the beginning (e.g., for gRPC 5-byte frame header)</param>
        public byte[] SerializeMetrics(IEnumerable<MetricPoint> metrics, int startPosition = 0)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            // Reserve space at the beginning if requested (e.g., for gRPC frame header)
            if (startPosition > 0)
            {
                writer.Write(new byte[startPosition]);
            }

            var resourceMetricsData = SerializeResourceMetrics(metrics);
            WriteTag(writer, FieldNumbers.ResourceMetrics, LengthDelimited);
            WriteVarInt(writer, resourceMetricsData.Length);
            writer.Write(resourceMetricsData);

            return buffer.ToArray();
        }

        public static class FieldNumbers
        {
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
            public const int ScopeAttributes = 3;

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
