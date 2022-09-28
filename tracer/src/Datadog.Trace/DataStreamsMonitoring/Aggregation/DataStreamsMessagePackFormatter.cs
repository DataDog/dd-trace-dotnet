// <copyright file="DataStreamsMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.DataStreamsMonitoring.Aggregation
{
    internal class DataStreamsMessagePackFormatter
    {
        private readonly byte[] _environmentBytes = StringEncoding.UTF8.GetBytes("Env");
        private readonly byte[] _environmentValueBytes;
        private readonly byte[] _serviceBytes = StringEncoding.UTF8.GetBytes("Service");

        private readonly byte[] _serviceValueBytes;

        // private readonly byte[] _primaryTagBytes = StringEncoding.UTF8.GetBytes("PrimaryTag");
        // private readonly byte[] _primaryTagValueBytes;
        private readonly byte[] _statsBytes = StringEncoding.UTF8.GetBytes("Stats");
        private readonly byte[] _tracerVersionBytes = StringEncoding.UTF8.GetBytes("TracerVersion");
        private readonly byte[] _tracerVersionValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.AssemblyVersion);
        private readonly byte[] _langBytes = StringEncoding.UTF8.GetBytes("Lang");
        private readonly byte[] _langValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.Language);

        private readonly byte[] _startBytes = StringEncoding.UTF8.GetBytes("Start");
        private readonly byte[] _durationBytes = StringEncoding.UTF8.GetBytes("Duration");

        private readonly byte[] _edgeTagsBytes = StringEncoding.UTF8.GetBytes("EdgeTags");
        private readonly byte[] _hashBytes = StringEncoding.UTF8.GetBytes("Hash");
        private readonly byte[] _parentHashBytes = StringEncoding.UTF8.GetBytes("ParentHash");
        private readonly byte[] _pathwayLatencyBytes = StringEncoding.UTF8.GetBytes("PathwayLatency");
        private readonly byte[] _edgeLatencyBytes = StringEncoding.UTF8.GetBytes("EdgeLatency");
        private readonly byte[] _timestampTypeBytes = StringEncoding.UTF8.GetBytes("TimestampType");
        private readonly byte[] _currentTimestampTypeBytes = StringEncoding.UTF8.GetBytes("current");
        private readonly byte[] _originTimestampTypeBytes = StringEncoding.UTF8.GetBytes("origin");

        public DataStreamsMessagePackFormatter(ImmutableTracerSettings tracerSettings, string defaultServiceName)
            : this(tracerSettings.Environment, defaultServiceName)
        {
        }

        public DataStreamsMessagePackFormatter(string? environment, string defaultServiceName)
        {
            // .NET tracer doesn't yet support primary tag
            // _primaryTagValueBytes = Array.Empty<byte>();
            _environmentValueBytes = string.IsNullOrEmpty(environment)
                                         ? Array.Empty<byte>()
                                         : StringEncoding.UTF8.GetBytes(environment);
            _serviceValueBytes = StringEncoding.UTF8.GetBytes(defaultServiceName);
        }

        public int Serialize(ref byte[] bytes, int offset, long bucketDurationNs, List<SerializableStatsBucket> statsBuckets)
        {
            var originalOffset = offset;

            // 6 entries in StatsPayload:
            // -1 because we don't have a primary tag
            // https://github.com/DataDog/data-streams-go/blob/6772b163707c0a8ecc8c9a3b28e0dab7e0cf58d4/datastreams/payload.go#L11
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 5);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentValueBytes);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _serviceBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _serviceValueBytes);

            // We never have a primary tag currently, make sure to increase header size if/when we add it
            // offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _primaryTagBytes);
            // offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _primaryTagValueBytes);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _langBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _langValueBytes);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _tracerVersionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _tracerVersionValueBytes);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _statsBytes);
            offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, statsBuckets.Count);

            foreach (var statsBucket in statsBuckets)
            {
                // 3 entries per StatsBucket:
                // https://github.com/DataDog/data-streams-go/blob/6772b163707c0a8ecc8c9a3b28e0dab7e0cf58d4/datastreams/payload.go#L27
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 3);

                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _startBytes);
                offset += MessagePackBinary.WriteInt64(ref bytes, offset, statsBucket.BucketStartTimeNs);

                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _durationBytes);
                offset += MessagePackBinary.WriteInt64(ref bytes, offset, bucketDurationNs);

                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _statsBytes);
                offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, statsBucket.Bucket.Values.Count);

                var timestampTypeBytes = statsBucket.TimestampType == TimestampType.Current
                                         ? _currentTimestampTypeBytes
                                         : _originTimestampTypeBytes;

                foreach (var point in statsBucket.Bucket.Values)
                {
                    var hasEdges = point.EdgeTags.Length > 0;

                    // 6 entries per StatsPoint:
                    // 5 if no edge tags
                    // https://github.com/DataDog/data-streams-go/blob/6772b163707c0a8ecc8c9a3b28e0dab7e0cf58d4/datastreams/payload.go#L44
                    var itemCount = hasEdges ? 6 : 5;
                    offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, itemCount);

                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _hashBytes);
                    offset += MessagePackBinary.WriteUInt64(ref bytes, offset, point.Hash.Value);

                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _parentHashBytes);
                    offset += MessagePackBinary.WriteUInt64(ref bytes, offset, point.ParentHash.Value);

                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _timestampTypeBytes);
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, timestampTypeBytes);

                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _pathwayLatencyBytes);
                    offset += SerializeSketch(ref bytes, offset, point.PathwayLatency);

                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _edgeLatencyBytes);
                    offset += SerializeSketch(ref bytes, offset, point.EdgeLatency);

                    if (hasEdges)
                    {
                        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _edgeTagsBytes);
                        offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, point.EdgeTags.Length);

                        foreach (var edgeTag in point.EdgeTags)
                        {
                            offset += MessagePackBinary.WriteString(ref bytes, offset, edgeTag);
                        }
                    }
                }
            }

            return offset - originalOffset;
        }

        private static int SerializeSketch(ref byte[] bytes, int offset, DDSketch sketch)
        {
            var size = sketch.ComputeSerializedSize();
            var totalBytes = size + 5; // 5 header bytes

            MessagePackBinary.EnsureCapacity(ref bytes, offset, totalBytes);
            bytes[offset] = MessagePackCode.Bin32;
            bytes[offset + 1] = (byte)(size >> 24);
            bytes[offset + 2] = (byte)(size >> 16);
            bytes[offset + 3] = (byte)(size >> 8);
            bytes[offset + 4] = (byte)size;

            using var stream = new MemoryStream(bytes, index: offset + 5, count: size);
            sketch.Serialize(stream);
            return totalBytes;
        }
    }
}
