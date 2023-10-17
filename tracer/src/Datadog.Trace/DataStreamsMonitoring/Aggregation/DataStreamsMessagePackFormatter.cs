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
        private readonly byte[] _backlogsBytes = StringEncoding.UTF8.GetBytes("Backlogs");
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
        private readonly byte[] _payloadSizeBytes = StringEncoding.UTF8.GetBytes("PayloadSize");
        private readonly byte[] _timestampTypeBytes = StringEncoding.UTF8.GetBytes("TimestampType");
        private readonly byte[] _currentTimestampTypeBytes = StringEncoding.UTF8.GetBytes("current");
        private readonly byte[] _originTimestampTypeBytes = StringEncoding.UTF8.GetBytes("origin");

        private readonly byte[] _backlogTagsBytes = StringEncoding.UTF8.GetBytes("Tags");
        private readonly byte[] _backlogValueBytes = StringEncoding.UTF8.GetBytes("Value");

        public DataStreamsMessagePackFormatter(ImmutableTracerSettings tracerSettings, string defaultServiceName)
            : this(tracerSettings.EnvironmentInternal, defaultServiceName)
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

        public int Serialize(Stream stream, long bucketDurationNs, List<SerializableStatsBucket> statsBuckets, List<SerializableBacklogBucket> backlogsBuckets)
        {
            var bytesWritten = 0;

            // 6 entries in StatsPayload:
            // -1 because we don't have a primary tag
            // https://github.com/DataDog/data-streams-go/blob/6772b163707c0a8ecc8c9a3b28e0dab7e0cf58d4/datastreams/payload.go#L11
            bytesWritten += MessagePackBinary.WriteMapHeader(stream, 5);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _environmentBytes);
            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _environmentValueBytes);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _serviceBytes);
            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _serviceValueBytes);

            // We never have a primary tag currently, make sure to increase header size if/when we add it
            // offset += MessagePackBinary.WriteStringBytes(stream, _primaryTagBytes);
            // offset += MessagePackBinary.WriteStringBytes(stream, _primaryTagValueBytes);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _langBytes);
            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _langValueBytes);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _tracerVersionBytes);
            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _tracerVersionValueBytes);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _statsBytes);
            bytesWritten += MessagePackBinary.WriteArrayHeader(stream, statsBuckets.Count + backlogsBuckets.Count);

            foreach (var backlogBucket in backlogsBuckets)
            {
                bytesWritten += WriteBucketsHeader(stream, backlogBucket.BucketStartTimeNs, bucketDurationNs, 0, backlogBucket.Bucket.Values.Count);

                foreach (var point in backlogBucket.Bucket.Values)
                {
                    bytesWritten += MessagePackBinary.WriteMapHeader(stream, 2);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, _backlogValueBytes);
                    bytesWritten += MessagePackBinary.WriteInt64(stream, point.Value);

                    var tags = point.Tags.Split(',');
                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, _backlogTagsBytes);
                    bytesWritten += MessagePackBinary.WriteArrayHeader(stream, tags.Length);
                    foreach (var tag in tags)
                    {
                        bytesWritten += MessagePackBinary.WriteString(stream, tag);
                    }
                }
            }

            foreach (var statsBucket in statsBuckets)
            {
                bytesWritten += WriteBucketsHeader(stream, statsBucket.BucketStartTimeNs, bucketDurationNs, statsBucket.Bucket.Values.Count, 0);

                var timestampTypeBytes = statsBucket.TimestampType == TimestampType.Current
                                             ? _currentTimestampTypeBytes
                                             : _originTimestampTypeBytes;

                foreach (var point in statsBucket.Bucket.Values)
                {
                    var hasEdges = point.EdgeTags.Length > 0;

                    // 7 entries per StatsPoint:
                    // 6 if no edge tags
                    // https://github.com/DataDog/data-streams-go/blob/6772b163707c0a8ecc8c9a3b28e0dab7e0cf58d4/datastreams/payload.go#L44
                    var itemCount = hasEdges ? 7 : 6;
                    bytesWritten += MessagePackBinary.WriteMapHeader(stream, itemCount);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, _hashBytes);
                    bytesWritten += MessagePackBinary.WriteUInt64(stream, point.Hash.Value);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, _parentHashBytes);
                    bytesWritten += MessagePackBinary.WriteUInt64(stream, point.ParentHash.Value);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, _timestampTypeBytes);
                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, timestampTypeBytes);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, _pathwayLatencyBytes);
                    bytesWritten += SerializeSketch(stream, point.PathwayLatency);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, _edgeLatencyBytes);
                    bytesWritten += SerializeSketch(stream, point.EdgeLatency);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, _payloadSizeBytes);
                    bytesWritten += SerializeSketch(stream, point.PayloadSize);

                    if (hasEdges)
                    {
                        bytesWritten += MessagePackBinary.WriteStringBytes(stream, _edgeTagsBytes);
                        bytesWritten += MessagePackBinary.WriteArrayHeader(stream, point.EdgeTags.Length);

                        foreach (var edgeTag in point.EdgeTags)
                        {
                            bytesWritten += MessagePackBinary.WriteString(stream, edgeTag);
                        }
                    }
                }
            }

            return bytesWritten;
        }

        private static int SerializeSketch(Stream stream, DDSketch sketch)
        {
            var size = sketch.ComputeSerializedSize();
            stream.WriteByte(MessagePackCode.Bin32);
            stream.WriteByte((byte)(size >> 24));
            stream.WriteByte((byte)(size >> 16));
            stream.WriteByte((byte)(size >> 8));
            stream.WriteByte((byte)size);

            sketch.Serialize(stream);
            return size + 5; // 5 headers
        }

        private int WriteBucketsHeader(Stream stream, long bucketStartTimeNs, long bucketDurationNs, int statsBucketCount, int backlogBucketCount)
        {
            int bytesWritten = 0;
            int count = 2;
            count += statsBucketCount > 0 ? 1 : 0;
            count += backlogBucketCount > 0 ? 1 : 0;

            // 2-4 entries per StatsBucket (Backlogs and Stats are both optional):
            // https://github.com/DataDog/data-streams-go/blob/60ba06aec619850aef8ed0b9b1f0f5e310438362/datastreams/payload.go#L48
            bytesWritten += MessagePackBinary.WriteMapHeader(stream, count);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _startBytes);
            bytesWritten += MessagePackBinary.WriteInt64(stream, bucketStartTimeNs);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _durationBytes);
            bytesWritten += MessagePackBinary.WriteInt64(stream, bucketDurationNs);

            if (statsBucketCount > 0)
            {
                bytesWritten += MessagePackBinary.WriteStringBytes(stream, _statsBytes);
                bytesWritten += MessagePackBinary.WriteArrayHeader(stream, statsBucketCount);
            }

            if (backlogBucketCount > 0)
            {
                bytesWritten += MessagePackBinary.WriteStringBytes(stream, _backlogsBytes);
                bytesWritten += MessagePackBinary.WriteArrayHeader(stream, backlogBucketCount);
            }

            return bytesWritten;
        }
    }
}
