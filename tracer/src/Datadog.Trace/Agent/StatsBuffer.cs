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

        public void Serialize(Stream stream, long duration)
        {
            MessagePackBinary.WriteMapHeader(stream, 9);

            MessagePackBinary.WriteString(stream, "hostname");
            MessagePackBinary.WriteString(stream, _header.HostName);

            MessagePackBinary.WriteString(stream, "env");
            MessagePackBinary.WriteString(stream, _header.Environment);

            MessagePackBinary.WriteString(stream, "version");
            MessagePackBinary.WriteString(stream, _header.Version);

            MessagePackBinary.WriteString(stream, "stats");
            MessagePackBinary.WriteArrayHeader(stream, 1);
            SerializeBuckets(stream, duration);

            MessagePackBinary.WriteString(stream, "lang");
            MessagePackBinary.WriteString(stream, TracerConstants.Language);

            MessagePackBinary.WriteString(stream, "tracerVersion");
            MessagePackBinary.WriteString(stream, TracerConstants.AssemblyVersion);

            MessagePackBinary.WriteString(stream, "runtimeID");
            MessagePackBinary.WriteString(stream, Tracer.RuntimeId);

            MessagePackBinary.WriteString(stream, "sequence");
            MessagePackBinary.WriteInt64(stream, _header.GetSequenceNumber());

            MessagePackBinary.WriteString(stream, "service");
            MessagePackBinary.WriteString(stream, _header.ServiceName);
        }

        private static void SerializeBucket(Stream stream, StatsBucket bucket)
        {
            MessagePackBinary.WriteMapHeader(stream, 11);

            MessagePackBinary.WriteString(stream, "service");
            MessagePackBinary.WriteString(stream, bucket.Key.Service);

            MessagePackBinary.WriteString(stream, "name");
            MessagePackBinary.WriteString(stream, bucket.Key.OperationName);

            MessagePackBinary.WriteString(stream, "resource");
            MessagePackBinary.WriteString(stream, bucket.Key.Resource);

            MessagePackBinary.WriteString(stream, "HTTP_status_code");
            MessagePackBinary.WriteInt32(stream, bucket.Key.HttpStatusCode);

            MessagePackBinary.WriteString(stream, "type");
            MessagePackBinary.WriteString(stream, bucket.Key.Type);

            MessagePackBinary.WriteString(stream, "hits");
            MessagePackBinary.WriteInt64(stream, bucket.Hits);

            MessagePackBinary.WriteString(stream, "errors");
            MessagePackBinary.WriteInt64(stream, bucket.Errors);

            MessagePackBinary.WriteString(stream, "duration");
            MessagePackBinary.WriteInt64(stream, bucket.Duration);

            MessagePackBinary.WriteString(stream, "okSummary");
            SerializeSketch(stream, bucket.OkSummary);

            MessagePackBinary.WriteString(stream, "errorSummary");
            SerializeSketch(stream, bucket.ErrorSummary);

            MessagePackBinary.WriteString(stream, "topLevelHits");
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

        private void SerializeBuckets(Stream stream, long duration)
        {
            MessagePackBinary.WriteMapHeader(stream, 3);

            MessagePackBinary.WriteString(stream, "start");
            MessagePackBinary.WriteInt64(stream, Start);

            MessagePackBinary.WriteString(stream, "duration");
            MessagePackBinary.WriteInt64(stream, duration);

            int count = 0;

            // First pass to count the number of buckets to serialize
            foreach (var bucket in Buckets.Values)
            {
                if (bucket.Hits != 0)
                {
                    count++;
                }
            }

            MessagePackBinary.WriteString(stream, "stats");
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
