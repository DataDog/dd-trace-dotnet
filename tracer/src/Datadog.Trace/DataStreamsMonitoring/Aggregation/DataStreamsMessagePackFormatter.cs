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
        private readonly long _productMask;
        private readonly bool _isInDefaultState;
        private readonly bool _writeProcessTags;
        private byte[] _environmentValueBytes;
        private byte[] _serviceValueBytes;
        private ProcessTags? _processTags;

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

                Interlocked.Exchange(ref _processTags, settings.ProcessTags);
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

#pragma warning disable SA1516 // Elements should be separated by a blank line
        private static ReadOnlySpan<byte> EnvironmentBytes => "Env"u8;
        private static ReadOnlySpan<byte> ServiceBytes => "Service"u8;
        private static ReadOnlySpan<byte> StatsBytes => "Stats"u8;
        private static ReadOnlySpan<byte> BacklogsBytes => "Backlogs"u8;
        private static ReadOnlySpan<byte> TracerVersionBytes => "TracerVersion"u8;
        private static ReadOnlySpan<byte> TracerVersionValueBytes => TracerConstants.AssemblyVersionBytes;
        private static ReadOnlySpan<byte> LangBytes => "Lang"u8;
        private static ReadOnlySpan<byte> LangValueBytes => "dotnet"u8;
        private static ReadOnlySpan<byte> StartBytes => "Start"u8;
        private static ReadOnlySpan<byte> DurationBytes => "Duration"u8;
        private static ReadOnlySpan<byte> EdgeTagsBytes => "EdgeTags"u8;
        private static ReadOnlySpan<byte> HashBytes => "Hash"u8;
        private static ReadOnlySpan<byte> ParentHashBytes => "ParentHash"u8;
        private static ReadOnlySpan<byte> PathwayLatencyBytes => "PathwayLatency"u8;
        private static ReadOnlySpan<byte> EdgeLatencyBytes => "EdgeLatency"u8;
        private static ReadOnlySpan<byte> PayloadSizeBytes => "PayloadSize"u8;
        private static ReadOnlySpan<byte> TimestampTypeBytes => "TimestampType"u8;
        private static ReadOnlySpan<byte> CurrentTimestampTypeBytes => "current"u8;
        private static ReadOnlySpan<byte> OriginTimestampTypeBytes => "origin"u8;
        private static ReadOnlySpan<byte> BacklogTagsBytes => "Tags"u8;
        private static ReadOnlySpan<byte> BacklogValueBytes => "Value"u8;
        private static ReadOnlySpan<byte> ProductMaskBytes => "ProductMask"u8;
        private static ReadOnlySpan<byte> ProcessTagsBytes => "ProcessTags"u8;
        private static ReadOnlySpan<byte> IsInDefaultStateBytes => "IsInDefaultState"u8;
        private static ReadOnlySpan<byte> TransactionsBytes => "Transactions"u8;
        private static ReadOnlySpan<byte> TransactionCheckpointIdsBytes => "TransactionCheckpointIds"u8;
#pragma warning restore SA1516

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
            byte[] transactionData)
        {
            var hasTransactions = transactionData.Length > 0;
            var withProcessTags = _writeProcessTags && _processTags?.TagsList.Count > 0;
            var processTags = _writeProcessTags ? _processTags?.TagsList : null;
            var bytesWritten = 0;

            // Should be in sync with Java
            // https://github.com/DataDog/dd-trace-java/blob/master/dd-trace-core/src/main/java/datadog/trace/core/datastreams/MsgPackDatastreamsPayloadWriter.java
            // -1 because we don't have a primary tag
            // -1 because service name override is not supported
            bytesWritten += MessagePackBinary.WriteMapHeader(stream, 7 + (withProcessTags ? 1 : 0));

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, EnvironmentBytes);
            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _environmentValueBytes);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, ServiceBytes);
            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _serviceValueBytes);

            // We never have a primary tag currently, make sure to increase header size if/when we add it
            // offset += MessagePackBinary.WriteStringBytes(stream, _primaryTagBytes);
            // offset += MessagePackBinary.WriteStringBytes(stream, _primaryTagValueBytes);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, LangBytes);
            bytesWritten += MessagePackBinary.WriteStringBytes(stream, LangValueBytes);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, TracerVersionBytes);
            bytesWritten += MessagePackBinary.WriteStringBytes(stream, TracerVersionValueBytes);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, StatsBytes);
            bytesWritten += MessagePackBinary.WriteArrayHeader(stream, statsBuckets.Count + backlogsBuckets.Count + (hasTransactions ? 1 : 0));

            if (hasTransactions)
            {
                var currentTs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
                var bucketStartTime = currentTs - (currentTs % bucketDurationNs);
                bytesWritten += WriteBucketsHeader(stream, bucketStartTime, bucketDurationNs, 0, 0, true);

                bytesWritten += MessagePackBinary.WriteStringBytes(stream, TransactionsBytes);
                bytesWritten += MessagePackBinary.WriteBytes(stream, transactionData);

                bytesWritten += MessagePackBinary.WriteStringBytes(stream, TransactionCheckpointIdsBytes);
                bytesWritten += MessagePackBinary.WriteBytes(stream, DataStreamsTransactionInfo.GetCacheBytes());
            }

            foreach (var backlogBucket in backlogsBuckets)
            {
                bytesWritten += WriteBucketsHeader(stream, backlogBucket.BucketStartTimeNs, bucketDurationNs, 0, backlogBucket.Bucket.Values.Count, false);

                foreach (var point in backlogBucket.Bucket.Values)
                {
                    bytesWritten += MessagePackBinary.WriteMapHeader(stream, 2);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, BacklogValueBytes);
                    bytesWritten += MessagePackBinary.WriteInt64(stream, point.Value);

                    var tags = point.Tags.Split(',');
                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, BacklogTagsBytes);
                    bytesWritten += MessagePackBinary.WriteArrayHeader(stream, tags.Length);
                    foreach (var tag in tags)
                    {
                        bytesWritten += MessagePackBinary.WriteString(stream, tag);
                    }
                }
            }

            foreach (var statsBucket in statsBuckets)
            {
                bytesWritten += WriteBucketsHeader(stream, statsBucket.BucketStartTimeNs, bucketDurationNs, statsBucket.Bucket.Values.Count, 0, false);

                ReadOnlySpan<byte> timestampTypeBytes = statsBucket.TimestampType == TimestampType.Current
                                             ? CurrentTimestampTypeBytes
                                             : OriginTimestampTypeBytes;

                foreach (var point in statsBucket.Bucket.Values)
                {
                    var hasEdges = point.EdgeTags.Length > 0;

                    // 7 entries per StatsPoint:
                    // 6 if no edge tags
                    // https://github.com/DataDog/data-streams-go/blob/6772b163707c0a8ecc8c9a3b28e0dab7e0cf58d4/datastreams/payload.go#L44
                    var itemCount = hasEdges ? 7 : 6;
                    bytesWritten += MessagePackBinary.WriteMapHeader(stream, itemCount);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, HashBytes);
                    bytesWritten += MessagePackBinary.WriteUInt64(stream, point.Hash.Value);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, ParentHashBytes);
                    bytesWritten += MessagePackBinary.WriteUInt64(stream, point.ParentHash.Value);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, TimestampTypeBytes);
                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, timestampTypeBytes);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, PathwayLatencyBytes);
                    bytesWritten += SerializeSketch(stream, point.PathwayLatency);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, EdgeLatencyBytes);
                    bytesWritten += SerializeSketch(stream, point.EdgeLatency);

                    bytesWritten += MessagePackBinary.WriteStringBytes(stream, PayloadSizeBytes);
                    bytesWritten += SerializeSketch(stream, point.PayloadSize);

                    if (hasEdges)
                    {
                        bytesWritten += MessagePackBinary.WriteStringBytes(stream, EdgeTagsBytes);
                        bytesWritten += MessagePackBinary.WriteArrayHeader(stream, point.EdgeTags.Length);

                        foreach (var edgeTag in point.EdgeTags)
                        {
                            bytesWritten += MessagePackBinary.WriteString(stream, edgeTag);
                        }
                    }
                }
            }

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, ProductMaskBytes);
            bytesWritten += MessagePackBinary.WriteInt64(stream, _productMask);

            if (processTags is not null)
            {
                bytesWritten += MessagePackBinary.WriteStringBytes(stream, ProcessTagsBytes);
                bytesWritten += MessagePackBinary.WriteArrayHeader(stream, processTags.Count);
                foreach (var tag in processTags)
                {
                    bytesWritten += MessagePackBinary.WriteString(stream, tag);
                }
            }

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, IsInDefaultStateBytes);
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

        private int WriteBucketsHeader(Stream stream, long bucketStartTimeNs, long bucketDurationNs, int statsBucketCount, int backlogBucketCount, bool hasTransactions)
        {
            int bytesWritten = 0;
            int count = 2;
            count += statsBucketCount > 0 ? 1 : 0;
            count += backlogBucketCount > 0 ? 1 : 0;
            count += hasTransactions ? 2 : 0;

            // 2-4 entries per StatsBucket (Backlogs and Stats are both optional):
            // https://github.com/DataDog/data-streams-go/blob/60ba06aec619850aef8ed0b9b1f0f5e310438362/datastreams/payload.go#L48
            bytesWritten += MessagePackBinary.WriteMapHeader(stream, count);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, StartBytes);
            bytesWritten += MessagePackBinary.WriteInt64(stream, bucketStartTimeNs);

            bytesWritten += MessagePackBinary.WriteStringBytes(stream, DurationBytes);
            bytesWritten += MessagePackBinary.WriteInt64(stream, bucketDurationNs);

            if (statsBucketCount > 0)
            {
                bytesWritten += MessagePackBinary.WriteStringBytes(stream, StatsBytes);
                bytesWritten += MessagePackBinary.WriteArrayHeader(stream, statsBucketCount);
            }

            if (backlogBucketCount > 0)
            {
                bytesWritten += MessagePackBinary.WriteStringBytes(stream, BacklogsBytes);
                bytesWritten += MessagePackBinary.WriteArrayHeader(stream, backlogBucketCount);
            }

            return bytesWritten;
        }
    }
}
