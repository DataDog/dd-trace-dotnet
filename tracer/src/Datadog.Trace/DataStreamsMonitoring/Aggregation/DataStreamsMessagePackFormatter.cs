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
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.DataStreamsMonitoring.Aggregation
{
    internal sealed class DataStreamsMessagePackFormatter
    {
        private readonly long _productMask;
        private readonly bool _isInDefaultState;
        private readonly bool _writeProcessTags;

        // This one class isn't yet handled by Source Generators
        private readonly byte[] _tracerVersionValueBytes = MessagePackSerializer.Serialize(TracerConstants.AssemblyVersion);

        // Runtime value fields (determined at config changes)
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

        public int Serialize(Stream stream, long bucketDurationNs, List<SerializableStatsBucket> statsBuckets, List<SerializableBacklogBucket> backlogsBuckets)
        {
            var withProcessTags = _writeProcessTags && _processTags?.TagsList.Count > 0;
            var bytesWritten = 0;

            // Should be in sync with Java
            // https://github.com/DataDog/dd-trace-java/blob/master/dd-trace-core/src/main/java/datadog/trace/core/datastreams/MsgPackDatastreamsPayloadWriter.java
            // -1 because we don't have a primary tag
            // -1 because service name override is not supported
            bytesWritten += MessagePackBinary.WriteMapHeader(stream, 7 + (withProcessTags ? 1 : 0));

            bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.EnvDSMBytes);
            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _environmentValueBytes);

            bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.ServiceDSMBytes);
            bytesWritten += MessagePackBinary.WriteStringBytes(stream, _serviceValueBytes);

            // We never have a primary tag currently, make sure to increase header size if/when we add it
            // offset += MessagePackBinary.WriteStringBytes(stream, _primaryTagBytes);
            // offset += MessagePackBinary.WriteStringBytes(stream, _primaryTagValueBytes);

            bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.LangBytes);
            bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.DotnetLanguageValueBytes);

            bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.TracerVersionBytes);
            bytesWritten += MessagePackBinary.WriteRaw(stream, _tracerVersionValueBytes);

            bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.StatsBytes);
            bytesWritten += MessagePackBinary.WriteArrayHeader(stream, statsBuckets.Count + backlogsBuckets.Count);

            foreach (var backlogBucket in backlogsBuckets)
            {
                bytesWritten += WriteBucketsHeader(stream, backlogBucket.BucketStartTimeNs, bucketDurationNs, 0, backlogBucket.Bucket.Values.Count);

                foreach (var point in backlogBucket.Bucket.Values)
                {
                    bytesWritten += MessagePackBinary.WriteMapHeader(stream, 2);

                    bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.ValueBytes);
                    bytesWritten += MessagePackBinary.WriteInt64(stream, point.Value);

                    var tags = point.Tags.Split(',');
                    bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.TagsBytes);
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
                                             ? MessagePackConstants.CurrentBytes
                                             : MessagePackConstants.OriginDSMBytes;

                foreach (var point in statsBucket.Bucket.Values)
                {
                    var hasEdges = point.EdgeTags.Length > 0;

                    // 7 entries per StatsPoint:
                    // 6 if no edge tags
                    // https://github.com/DataDog/data-streams-go/blob/6772b163707c0a8ecc8c9a3b28e0dab7e0cf58d4/datastreams/payload.go#L44
                    var itemCount = hasEdges ? 7 : 6;
                    bytesWritten += MessagePackBinary.WriteMapHeader(stream, itemCount);

                    bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.HashBytes);
                    bytesWritten += MessagePackBinary.WriteUInt64(stream, point.Hash.Value);

                    bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.ParentHashBytes);
                    bytesWritten += MessagePackBinary.WriteUInt64(stream, point.ParentHash.Value);

                    bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.TimestampTypeBytes);
                    bytesWritten += MessagePackBinary.WriteRaw(stream, timestampTypeBytes);

                    bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.PathwayLatencyBytes);
                    bytesWritten += SerializeSketch(stream, point.PathwayLatency);

                    bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.EdgeLatencyBytes);
                    bytesWritten += SerializeSketch(stream, point.EdgeLatency);

                    bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.PayloadSizeBytes);
                    bytesWritten += SerializeSketch(stream, point.PayloadSize);

                    if (hasEdges)
                    {
                        bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.EdgeTagsBytes);
                        bytesWritten += MessagePackBinary.WriteArrayHeader(stream, point.EdgeTags.Length);

                        foreach (var edgeTag in point.EdgeTags)
                        {
                            bytesWritten += MessagePackBinary.WriteString(stream, edgeTag);
                        }
                    }
                }
            }

            bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.ProductMaskBytes);
            bytesWritten += MessagePackBinary.WriteInt64(stream, _productMask);

            if (withProcessTags)
            {
                bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.ProcessTagsDSMBytes);
                bytesWritten += MessagePackBinary.WriteArrayHeader(stream, _processTags!.TagsList.Count);
                foreach (var tag in _processTags.TagsList)
                {
                    bytesWritten += MessagePackBinary.WriteString(stream, tag);
                }
            }

            bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.IsInDefaultStateBytes);
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

            bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.StartDSMBytes);
            bytesWritten += MessagePackBinary.WriteInt64(stream, bucketStartTimeNs);

            bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.DurationDSMBytes);
            bytesWritten += MessagePackBinary.WriteInt64(stream, bucketDurationNs);

            if (statsBucketCount > 0)
            {
                bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.StatsBytes);
                bytesWritten += MessagePackBinary.WriteArrayHeader(stream, statsBucketCount);
            }

            if (backlogBucketCount > 0)
            {
                bytesWritten += MessagePackBinary.WriteRaw(stream, MessagePackConstants.BacklogsBytes);
                bytesWritten += MessagePackBinary.WriteArrayHeader(stream, backlogBucketCount);
            }

            return bytesWritten;
        }
    }
}
