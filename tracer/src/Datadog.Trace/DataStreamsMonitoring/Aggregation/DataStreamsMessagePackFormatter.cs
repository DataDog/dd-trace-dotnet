// <copyright file="DataStreamsMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.DataStreamsMonitoring.Aggregation
{
    internal sealed class DataStreamsMessagePackFormatter
    {
        private readonly byte[] _environmentBytes = StringEncoding.UTF8.GetBytes("Env");
        private readonly byte[] _serviceBytes = StringEncoding.UTF8.GetBytes("Service");
        private readonly long _productMask;
        private readonly bool _isInDefaultState;
        private readonly bool _writeProcessTags;

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
        private readonly byte[] _productMaskBytes = StringEncoding.UTF8.GetBytes("ProductMask");
        private readonly byte[] _processTagsBytes = StringEncoding.UTF8.GetBytes("ProcessTags");
        private readonly byte[] _isInDefaultStateBytes = StringEncoding.UTF8.GetBytes("IsInDefaultState");

        private readonly byte[] _transactions = StringEncoding.UTF8.GetBytes("Transactions");
        private readonly byte[] _transactionCheckpointIds = StringEncoding.UTF8.GetBytes("TransactionCheckpointIds");

        private byte[] _environmentValueBytes;
        private byte[] _serviceValueBytes;

        public DataStreamsMessagePackFormatter(TracerSettings tracerSettings, ProfilerSettings profilerSettings)
        {
            // .NET tracer doesn't yet support primary tag
            // _primaryTagValueBytes = Array.Empty<byte>();
            UpdateSettings(tracerSettings.Manager.InitialMutableSettings);
            // Not disposing the subscription on the basis this is never cleaned up
            tracerSettings.Manager.SubscribeToChanges(changes =>
            {
                if (changes.UpdatedMutable is { } mutable)
                {
                    UpdateSettings(mutable);
                }
            });

            _productMask = GetProductsMask(tracerSettings, profilerSettings);
            _isInDefaultState = tracerSettings.IsDataStreamsMonitoringInDefaultState;
            _writeProcessTags = tracerSettings.PropagateProcessTags;

            [MemberNotNull(nameof(_environmentValueBytes))]
            [MemberNotNull(nameof(_serviceValueBytes))]
            void UpdateSettings(MutableSettings settings)
            {
                var env = StringUtil.IsNullOrEmpty(settings.Environment) ? [] : StringEncoding.UTF8.GetBytes(settings.Environment);
                Interlocked.Exchange(ref _environmentValueBytes!, env);

                var service = StringUtil.IsNullOrEmpty(settings.DefaultServiceName) ? [] : StringEncoding.UTF8.GetBytes(settings.DefaultServiceName);
                Interlocked.Exchange(ref _serviceValueBytes!, service);
            }
        }

        // should be the same across all languages
        [Flags]
        private enum Products : long
        {
            None = 0,
            Apm = 1,            // 00000001
            Dsm = 1 << 1,       // 00000010
            Djm = 1 << 2,       // 00000100
            Profiling = 1 << 3, // 00001000
        }

        private static long GetProductsMask(TracerSettings tracerSettings, ProfilerSettings profilerSettings)
        {
            var productsMask = (long)Products.Apm;
            if (tracerSettings.IsDataStreamsMonitoringEnabled)
            {
                productsMask |= (long)Products.Dsm;
            }

            if (profilerSettings.IsProfilerEnabled)
            {
                productsMask |= (long)Products.Profiling;
            }

            return productsMask;
        }

        public int Serialize(
            Stream stream,
            long bucketDurationNs,
            List<SerializableStatsBucket> statsBuckets,
            List<SerializableBacklogBucket> backlogsBuckets,
            DataStreamsTransactionContainer dataStreamsTransactionContainer)
        {
            var hasTransactions = dataStreamsTransactionContainer.Size() > 0;
            var withProcessTags = _writeProcessTags && !string.IsNullOrEmpty(ProcessTags.SerializedTags);
            var bytesWritten = 0;

            // Should be in sync with Java
            // https://github.com/DataDog/dd-trace-java/blob/master/dd-trace-core/src/main/java/datadog/trace/core/datastreams/MsgPackDatastreamsPayloadWriter.java
            // -1 because we don't have a primary tag
            // -1 because service name override is not supported
            bytesWritten += MessagePackBinary.WriteMapHeader(stream, 7 + (withProcessTags ? 1 : 0));

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
            bytesWritten += MessagePackBinary.WriteArrayHeader(stream, statsBuckets.Count + backlogsBuckets.Count + (hasTransactions ? 1 : 0));

            if (hasTransactions)
            {
                var currentTs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
                var bucketStartTime = currentTs - (currentTs % bucketDurationNs);
                bytesWritten += WriteBucketsHeader(stream, bucketStartTime, bucketDurationNs, 1, 0);

                bytesWritten += MessagePackBinary.WriteMapHeader(stream, 2);

                bytesWritten += MessagePackBinary.WriteStringBytes(stream, _transactions);
                bytesWritten += MessagePackBinary.WriteBytes(stream, dataStreamsTransactionContainer.GetDataAndReset());

                bytesWritten += MessagePackBinary.WriteStringBytes(stream, _transactionCheckpointIds);
                bytesWritten += MessagePackBinary.WriteBytes(stream, DataStreamsTransactionInfo.GetCacheBytes());
            }

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

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _productMaskBytes);
            bytesWritten += MessagePackBinary.WriteInt64(stream, _productMask);

            if (withProcessTags)
            {
                bytesWritten += MessagePackBinary.WriteStringBytes(stream, _processTagsBytes);
                bytesWritten += MessagePackBinary.WriteString(stream, ProcessTags.SerializedTags);
            }

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _isInDefaultStateBytes);
            bytesWritten += MessagePackBinary.WriteBoolean(stream, _isInDefaultState);

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
