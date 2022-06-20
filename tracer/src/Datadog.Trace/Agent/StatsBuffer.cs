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
    internal class StatsBuffer
    {
        private readonly List<StatsAggregationKey> _keysToRemove;

        private readonly ClientStatsPayload _header;

        public StatsBuffer(ClientStatsPayload header)
        {
            _header = header;
            _keysToRemove = new();
            Buckets = new();
            Reset();
        }

        public Dictionary<StatsAggregationKey, StatsBucket> Buckets { get; }

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

            Start = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
        }

        public void Serialize(Stream stream, long bucketDuration)
        {
            MessagePackBinary.WriteMapHeader(stream, 8);

            MessagePackBinary.WriteString(stream, "Hostname");
            MessagePackBinary.WriteString(stream, _header.HostName ?? string.Empty);

            MessagePackBinary.WriteString(stream, "Env");
            MessagePackBinary.WriteString(stream, _header.Environment ?? string.Empty);

            MessagePackBinary.WriteString(stream, "Version");
            MessagePackBinary.WriteString(stream, _header.Version ?? string.Empty);

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
