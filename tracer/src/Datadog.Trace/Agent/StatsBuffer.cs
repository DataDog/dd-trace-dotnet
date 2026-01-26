// <copyright file="StatsBuffer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Agent
{
    internal sealed class StatsBuffer
    {
        private readonly List<StatsAggregationKey> _keysToRemove;

        private ClientStatsPayload _header;

        public StatsBuffer(ClientStatsPayload header)
        {
            _header = header;
            _keysToRemove = new();
            Buckets = new();
            Reset();
        }

        public Dictionary<StatsAggregationKey, StatsBucket> Buckets { get; }

        public DateTimeOffset StartTime { get; private set; }

        public long Start { get; private set; }

        public void Reset()
        {
            // We need to do some cleanup because the application could have an unlimited number of endpoints,
            // but at the same time we don't want to reallocate all the sketches every time.
            // The compromise here is to remove only the endpoints that received no hit during the last iteration.
            foreach (var kvp in Buckets)
            {
                if (kvp.Value.Hits == 0)
                {
                    _keysToRemove.Add(kvp.Key);
                }
                else
                {
                    kvp.Value.Clear();
                }
            }

            foreach (var key in _keysToRemove)
            {
                Buckets.Remove(key);
            }

            _keysToRemove.Clear();

            StartTime = DateTimeOffset.UtcNow;
            Start = StartTime.ToUnixTimeNanoseconds();
        }

#if NET6_0_OR_GREATER
        public List<Datadog.Trace.OpenTelemetry.Metrics.MetricPoint> ConvertToOtlpMetrics(DateTimeOffset endTime)
        {
            // Potentially add resource attributes here
            // - telemetry.sdk.name
            // - telemetry.sdk.language
            // - telemetry.sdk.version
            // - language
            // - hostname
            // - runtime.id
            // - service.name
            // - deployment.environment.name
            // - deployment.environment
            // - service.version
            // - process.tags
            // - tracer.version
            // - tracer.runtime.id

            return OtlpSerializeBuckets(endTime);
        }

        private Datadog.Trace.OpenTelemetry.Metrics.MetricPoint OtlpSerializeBucket(StatsBucket bucket, DateTimeOffset endTime)
        {
            // TODO: Do something with bucket.Key.Service
            // Ignored for now: Service, IsSyntheticsRequest

            var timeseriesAttributes = new Dictionary<string, object>()
            {
                // { "service", bucket.Key.Service },
                { "Name", bucket.Key.OperationName },
                { "Resource", bucket.Key.Resource },
                { "Type", bucket.Key.Type },
                { "StatusCode", bucket.Key.HttpStatusCode },
                { "TopLevel", bucket.Key.IsTopLevel },
                { "Error", bucket.Key.IsError },
            };

            var metricPoint = new Datadog.Trace.OpenTelemetry.Metrics.MetricPoint(instrumentName: "request.latencies", meterName: "datadog.trace.metrics", meterVersion: string.Empty, meterTags: Array.Empty<KeyValuePair<string, object>>(), instrumentType: Datadog.Trace.OpenTelemetry.Metrics.InstrumentType.Histogram, temporality: Datadog.Trace.OpenTelemetry.Metrics.AggregationTemporality.Delta, tags: timeseriesAttributes, unit: "ns", description: "Summary of request latencies")
            {
                StartTime = StartTime,
                EndTime = endTime,
                SnapshotSum = bucket.Duration,
                SnapshotCount = bucket.Hits,
                SnapshotMin = double.NaN,
                SnapshotMax = double.NaN,
                SnapshotBucketCounts = GetCounts(bucket.OkSummary).ToArray(),
                SnapshotBucketBounds = GetBoundaries(bucket.OkSummary).ToArray(),
            };

            static List<double> GetBoundaries(DDSketch sketch)
            {
                var boundaries = new List<double>();

                // OTel boundaries are defined as a sequence of upper bounds. When our sketch contains gaps we
                // must introduce extra bounds to represent the lower bound of any bins after a gap; otherwise
                // it would look like that bin covers not just its original range, but the gap as well.
                int lastBinIndex = -1;
                var indexMapping = sketch.IndexMapping;
                foreach (var bin in sketch.PositiveValueStore.EnumerateAscending())
                {
                    int binIndex = bin.Index;
                    if (lastBinIndex < binIndex - 1)
                    {
                        // gap detected, introduce boundary representing current bin's lower bound
                        boundaries.Add(indexMapping.GetLowerBound(binIndex));
                    }

                    boundaries.Add(indexMapping.GetLowerBound(binIndex + 1));
                    lastBinIndex = binIndex;
                }

                return boundaries;
            }

            static List<long> GetCounts(DDSketch sketch)
            {
                var counts = new List<long>();

                // to maintain alignment with getBoundaries we must introduce zero counts for
                // boundaries inserted to represent the lower bound of any bins after a gap.
                int lastBinIndex = -1;
                foreach (var bin in sketch.PositiveValueStore.EnumerateAscending())
                {
                    int binIndex = bin.Index;
                    if (lastBinIndex < binIndex - 1)
                    {
                        // gap detected, insert zero count for boundary introduced by getBoundaries
                        counts.Add(0);
                    }

                    counts.Add((long)bin.Count);
                    lastBinIndex = binIndex;
                }

                return counts;
            }

            return metricPoint;
        }

        private List<Datadog.Trace.OpenTelemetry.Metrics.MetricPoint> OtlpSerializeBuckets(DateTimeOffset endTime)
        {
            var metricPoints = new List<Datadog.Trace.OpenTelemetry.Metrics.MetricPoint>();
            foreach (var bucket in Buckets.Values)
            {
                metricPoints.Add(OtlpSerializeBucket(bucket, endTime));
            }

            return metricPoints;
        }
#endif

        public void Serialize(Stream stream, long bucketDuration)
        {
            var count = 8;
            if (!string.IsNullOrEmpty(_header.ProcessTags))
            {
                count++;
            }

            MessagePackBinary.WriteMapHeader(stream, count);

            MessagePackBinary.WriteString(stream, "Hostname");
            MessagePackBinary.WriteString(stream, _header.HostName ?? string.Empty);

            var details = _header.Details;
            MessagePackBinary.WriteString(stream, "Env");
            MessagePackBinary.WriteString(stream, details.Environment ?? string.Empty);

            MessagePackBinary.WriteString(stream, "Version");
            MessagePackBinary.WriteString(stream, details.Version ?? string.Empty);

            if (!string.IsNullOrEmpty(_header.ProcessTags))
            {
                MessagePackBinary.WriteString(stream, "ProcessTags");
                MessagePackBinary.WriteString(stream, _header.ProcessTags);
            }

            MessagePackBinary.WriteString(stream, "Stats");
            MessagePackBinary.WriteArrayHeader(stream, 1);
            SerializeBuckets(stream, bucketDuration);

            MessagePackBinary.WriteString(stream, "Lang");
            MessagePackBinary.WriteString(stream, TracerConstants.Language);

            MessagePackBinary.WriteString(stream, "TracerVersion");
            MessagePackBinary.WriteString(stream, TracerConstants.AssemblyVersion);

            MessagePackBinary.WriteString(stream, "RuntimeID");
            MessagePackBinary.WriteString(stream, Tracer.RuntimeId);

            MessagePackBinary.WriteString(stream, "Sequence");
            MessagePackBinary.WriteInt64(stream, _header.GetSequenceNumber());
        }

        private static void SerializeBucket(Stream stream, StatsBucket bucket)
        {
            MessagePackBinary.WriteMapHeader(stream, 12);

            MessagePackBinary.WriteString(stream, "Service");
            MessagePackBinary.WriteString(stream, bucket.Key.Service ?? string.Empty);

            MessagePackBinary.WriteString(stream, "Name");
            MessagePackBinary.WriteString(stream, bucket.Key.OperationName ?? string.Empty);

            MessagePackBinary.WriteString(stream, "Resource");
            MessagePackBinary.WriteString(stream, bucket.Key.Resource ?? string.Empty);

            MessagePackBinary.WriteString(stream, "Synthetics");
            MessagePackBinary.WriteBoolean(stream, bucket.Key.IsSyntheticsRequest);

            MessagePackBinary.WriteString(stream, "HTTPStatusCode");
            MessagePackBinary.WriteInt32(stream, bucket.Key.HttpStatusCode);

            MessagePackBinary.WriteString(stream, "Type");
            MessagePackBinary.WriteString(stream, bucket.Key.Type ?? string.Empty);

            MessagePackBinary.WriteString(stream, "Hits");
            MessagePackBinary.WriteInt64(stream, bucket.Hits);

            MessagePackBinary.WriteString(stream, "Errors");
            MessagePackBinary.WriteInt64(stream, bucket.Errors);

            MessagePackBinary.WriteString(stream, "Duration");
            MessagePackBinary.WriteInt64(stream, bucket.Duration);

            MessagePackBinary.WriteString(stream, "OkSummary");
            SerializeSketch(stream, bucket.OkSummary);

            MessagePackBinary.WriteString(stream, "ErrorSummary");
            SerializeSketch(stream, bucket.ErrorSummary);

            MessagePackBinary.WriteString(stream, "TopLevelHits");
            MessagePackBinary.WriteInt64(stream, bucket.TopLevelHits);
        }

        private static void SerializeSketch(Stream stream, DDSketch sketch)
        {
            var size = sketch.ComputeSerializedSize();
            stream.WriteByte(MessagePackCode.Bin32);

            stream.WriteByte((byte)(size >> 24));
            stream.WriteByte((byte)(size >> 16));
            stream.WriteByte((byte)(size >> 8));
            stream.WriteByte((byte)size);

            sketch.Serialize(stream);
        }

        private void SerializeBuckets(Stream stream, long bucketDuration)
        {
            MessagePackBinary.WriteMapHeader(stream, 3);

            MessagePackBinary.WriteString(stream, "Start");
            MessagePackBinary.WriteInt64(stream, Start);

            MessagePackBinary.WriteString(stream, "Duration");
            MessagePackBinary.WriteInt64(stream, bucketDuration);

            int count = 0;

            // First pass to count the number of buckets to serialize
            foreach (var bucket in Buckets.Values)
            {
                if (bucket.Hits != 0)
                {
                    count++;
                }
            }

            MessagePackBinary.WriteString(stream, "Stats");
            MessagePackBinary.WriteArrayHeader(stream, count);

            // Second pass for the actual serialization
            foreach (var bucket in Buckets.Values)
            {
                if (bucket.Hits != 0)
                {
                    SerializeBucket(stream, bucket);
                }
            }
        }
    }
}
