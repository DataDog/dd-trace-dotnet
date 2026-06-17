// <copyright file="StatsBuffer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Agent
{
    internal sealed class StatsBuffer
    {
        private readonly List<StatsAggregationKey> _keysToRemove;

        private ClientStatsPayload _header;

        public StatsBuffer(ClientStatsPayload header, StatsCardinalityLimiter cardinalityLimiter, StatsCardinalityReporter cardinalityReporter)
        {
            _header = header;
            CardinalityLimiter = cardinalityLimiter;
            _keysToRemove = new();
            Buckets = new();
            Reset();
        }

        public Dictionary<StatsAggregationKey, StatsBucket> Buckets { get; }

        public StatsCardinalityLimiter CardinalityLimiter { get; }

        public long Start { get; private set; }

        /// <summary>
        /// Gets the number of buckets that have received at least one hit in the current flush window.
        /// This excludes zero-hit buckets retained across resets for sketch reuse, so the whole-key
        /// cardinality cap reflects only buckets actually admitted this window.
        /// </summary>
        public int ActiveBucketCount { get; private set; }

        // UTF-8 bytes for the constant map keys and values are embedded in the PE as static data via u8
        // literals. Using ReadOnlySpan<byte> property getters and WriteStringBytes avoids re-encoding the
        // same strings to UTF-8 on every serialization, matching the approach in SpanMessagePackFormatter.
#pragma warning disable SA1516 // Elements should be separated by blank line
        // payload header keys
        private static ReadOnlySpan<byte> HostnameKeyBytes => "Hostname"u8;
        private static ReadOnlySpan<byte> EnvKeyBytes => "Env"u8;
        private static ReadOnlySpan<byte> VersionKeyBytes => "Version"u8;
        private static ReadOnlySpan<byte> ProcessTagsKeyBytes => "ProcessTags"u8;
        private static ReadOnlySpan<byte> LangKeyBytes => "Lang"u8;
        private static ReadOnlySpan<byte> TracerVersionKeyBytes => "TracerVersion"u8;
        private static ReadOnlySpan<byte> RuntimeIdKeyBytes => "RuntimeID"u8;
        private static ReadOnlySpan<byte> SequenceKeyBytes => "Sequence"u8;
        private static ReadOnlySpan<byte> TracerDdTags => "TracerDdTags"u8;
        private static ReadOnlySpan<byte> GitCommitShaKeyBytes => "GitCommitSha"u8;

        // bucket keys
        private static ReadOnlySpan<byte> ServiceKeyBytes => "Service"u8;
        private static ReadOnlySpan<byte> NameKeyBytes => "Name"u8;
        private static ReadOnlySpan<byte> ResourceKeyBytes => "Resource"u8;
        private static ReadOnlySpan<byte> SyntheticsKeyBytes => "Synthetics"u8;
        private static ReadOnlySpan<byte> HttpStatusCodeKeyBytes => "HTTPStatusCode"u8;
        private static ReadOnlySpan<byte> TypeKeyBytes => "Type"u8;
        private static ReadOnlySpan<byte> HitsKeyBytes => "Hits"u8;
        private static ReadOnlySpan<byte> ErrorsKeyBytes => "Errors"u8;
        private static ReadOnlySpan<byte> OkSummaryKeyBytes => "OkSummary"u8;
        private static ReadOnlySpan<byte> ErrorSummaryKeyBytes => "ErrorSummary"u8;
        private static ReadOnlySpan<byte> TopLevelHitsKeyBytes => "TopLevelHits"u8;
        private static ReadOnlySpan<byte> SpanKindKeyBytes => "SpanKind"u8;
        private static ReadOnlySpan<byte> IsTraceRootKeyBytes => "IsTraceRoot"u8;
        private static ReadOnlySpan<byte> HttpMethodKeyBytes => "HTTPMethod"u8;
        private static ReadOnlySpan<byte> HttpEndpointKeyBytes => "HTTPEndpoint"u8;
        private static ReadOnlySpan<byte> GrpcStatusCodeKeyBytes => "GRPCStatusCode"u8;
        private static ReadOnlySpan<byte> ServiceSourceKeyBytes => "srv_src"u8; // Wire name per the Go agent's generated msgpack code
        private static ReadOnlySpan<byte> PeerTagsKeyBytes => "PeerTags"u8;
        private static ReadOnlySpan<byte> AdditionalMetricTagsKeyBytes => "AdditionalMetricTags"u8;

        // shared keys (used in multiple maps)
        private static ReadOnlySpan<byte> StatsKeyBytes => "Stats"u8;
        private static ReadOnlySpan<byte> StartKeyBytes => "Start"u8;
        private static ReadOnlySpan<byte> DurationKeyBytes => "Duration"u8;

        // constant values
        private static ReadOnlySpan<byte> UnknownEnvValueBytes => "unknown-env"u8;
        private static ReadOnlySpan<byte> LangValueBytes => "dotnet"u8; // TracerConstants.Language
#pragma warning restore SA1516

        /// <summary>
        /// Returns true if any bucket has received hits in the current interval.
        /// Buckets with zero hits are retained for sketch reuse but should not trigger a flush.
        /// </summary>
        public bool HasHits()
        {
            foreach (var bucket in Buckets.Values)
            {
                if (bucket.Hits != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Records that a bucket has become active (received its first hit) in the current flush window.
        /// Not thread-safe: only called from the single-threaded span-processing path, like bucket hit counting.
        /// </summary>
        public void IncrementActiveBucketCount() => ActiveBucketCount++;

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
            ActiveBucketCount = 0;

            // Reset the per-field admission sets so each flush window admits a fresh set of distinct values.
            CardinalityLimiter.Reset();

            // Align to 10-second boundary to match the Go tracer's alignTs: ts - ts % bucketSize
            var nowNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
            Start = nowNs - (nowNs % 10_000_000_000);
        }

        public void Serialize(Stream stream, long bucketDuration)
        {
            var count = 10; // Base: Hostname, Env, Version, Stats, Lang, TracerVersion, RuntimeID, Sequence, Service, TracerDdTags
            var details = _header.Details;

            var serializedTags = details.ProcessTags?.SerializedTags;
            var writeTags = !StringUtil.IsNullOrEmpty(serializedTags);
            if (writeTags)
            {
                count++;
            }

            var writeGitCommitSha = !StringUtil.IsNullOrEmpty(details.GitCommitSha);
            if (writeGitCommitSha)
            {
                count++;
            }

            MessagePackBinary.WriteMapHeader(stream, count);

            MessagePackBinary.WriteStringBytes(stream, HostnameKeyBytes);
            MessagePackBinary.WriteString(stream, _header.HostName ?? string.Empty);

            MessagePackBinary.WriteStringBytes(stream, EnvKeyBytes);
            if (StringUtil.IsNullOrEmpty(details.Environment))
            {
                MessagePackBinary.WriteStringBytes(stream, UnknownEnvValueBytes);
            }
            else
            {
                MessagePackBinary.WriteString(stream, details.Environment);
            }

            MessagePackBinary.WriteStringBytes(stream, VersionKeyBytes);
            MessagePackBinary.WriteString(stream, details.Version ?? string.Empty);

            if (writeTags)
            {
                MessagePackBinary.WriteStringBytes(stream, ProcessTagsKeyBytes);
                MessagePackBinary.WriteString(stream, serializedTags);
            }

            MessagePackBinary.WriteStringBytes(stream, StatsKeyBytes);
            MessagePackBinary.WriteArrayHeader(stream, 1);
            SerializeBuckets(stream, bucketDuration);

            MessagePackBinary.WriteStringBytes(stream, LangKeyBytes);
            MessagePackBinary.WriteStringBytes(stream, LangValueBytes);

            MessagePackBinary.WriteStringBytes(stream, TracerVersionKeyBytes);
            MessagePackBinary.WriteStringBytes(stream, TracerConstants.AssemblyVersionBytes);

            MessagePackBinary.WriteStringBytes(stream, RuntimeIdKeyBytes);
            MessagePackBinary.WriteString(stream, Tracer.RuntimeId);

            MessagePackBinary.WriteStringBytes(stream, SequenceKeyBytes);
            MessagePackBinary.WriteInt64(stream, _header.GetSequenceNumber());

            MessagePackBinary.WriteStringBytes(stream, ServiceKeyBytes);
            MessagePackBinary.WriteString(stream, details.DefaultServiceName ?? string.Empty);

            var ddTags = details.DdTags;
            MessagePackBinary.WriteStringBytes(stream, TracerDdTags);
            MessagePackBinary.WriteArrayHeader(stream, ddTags.Length);
            foreach (var tag in ddTags)
            {
                MessagePackBinary.WriteStringBytes(stream, tag);
            }

            if (writeGitCommitSha)
            {
                MessagePackBinary.WriteStringBytes(stream, GitCommitShaKeyBytes);
                MessagePackBinary.WriteString(stream, details.GitCommitSha);
            }
        }

        private static void SerializeBucket(Stream stream, StatsBucket bucket)
        {
            var fieldCount = 19;
            if (bucket.PeerTags.Count != 0)
            {
                fieldCount++;
            }

            MessagePackBinary.WriteMapHeader(stream, fieldCount);

            MessagePackBinary.WriteStringBytes(stream, ServiceKeyBytes);
            MessagePackBinary.WriteString(stream, bucket.Key.Service);

            MessagePackBinary.WriteStringBytes(stream, NameKeyBytes);
            MessagePackBinary.WriteString(stream, bucket.Key.OperationName);

            MessagePackBinary.WriteStringBytes(stream, ResourceKeyBytes);
            MessagePackBinary.WriteString(stream, bucket.Key.Resource);

            MessagePackBinary.WriteStringBytes(stream, SyntheticsKeyBytes);
            MessagePackBinary.WriteBoolean(stream, bucket.Key.IsSyntheticsRequest);

            MessagePackBinary.WriteStringBytes(stream, HttpStatusCodeKeyBytes);
            MessagePackBinary.WriteInt32(stream, bucket.Key.HttpStatusCode);

            MessagePackBinary.WriteStringBytes(stream, TypeKeyBytes);
            MessagePackBinary.WriteString(stream, bucket.Key.Type);

            MessagePackBinary.WriteStringBytes(stream, HitsKeyBytes);
            MessagePackBinary.WriteInt64(stream, bucket.Hits);

            MessagePackBinary.WriteStringBytes(stream, ErrorsKeyBytes);
            MessagePackBinary.WriteInt64(stream, bucket.Errors);

            MessagePackBinary.WriteStringBytes(stream, DurationKeyBytes);
            MessagePackBinary.WriteInt64(stream, bucket.Duration);

            MessagePackBinary.WriteStringBytes(stream, OkSummaryKeyBytes);
            SerializeSketch(stream, bucket.OkSummary);

            MessagePackBinary.WriteStringBytes(stream, ErrorSummaryKeyBytes);
            SerializeSketch(stream, bucket.ErrorSummary);

            MessagePackBinary.WriteStringBytes(stream, TopLevelHitsKeyBytes);
            MessagePackBinary.WriteInt64(stream, bucket.TopLevelHits);

            // Based on https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/stats/aggregation.go
            MessagePackBinary.WriteStringBytes(stream, SpanKindKeyBytes);
            MessagePackBinary.WriteString(stream, bucket.Key.SpanKind);

            // Spec defines Trilean: NOT_SET=0, TRUE=1, FALSE=2
            MessagePackBinary.WriteStringBytes(stream, IsTraceRootKeyBytes);
            MessagePackBinary.WriteInt32(stream, bucket.Key.IsTraceRoot switch { true => 1, false => 2, null => 0 });

            MessagePackBinary.WriteStringBytes(stream, HttpMethodKeyBytes);
            MessagePackBinary.WriteString(stream, bucket.Key.HttpMethod);

            MessagePackBinary.WriteStringBytes(stream, HttpEndpointKeyBytes);
            MessagePackBinary.WriteString(stream, bucket.Key.HttpEndpoint);

            MessagePackBinary.WriteStringBytes(stream, GrpcStatusCodeKeyBytes);
            MessagePackBinary.WriteString(stream, bucket.Key.GrpcStatusCode);

            MessagePackBinary.WriteStringBytes(stream, ServiceSourceKeyBytes);
            MessagePackBinary.WriteString(stream, bucket.Key.ServiceSource);

            // Based on https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/stats/span_concentrator.go#L53-L99
            if (bucket.PeerTags.Count != 0)
            {
                MessagePackBinary.WriteStringBytes(stream, PeerTagsKeyBytes);
                MessagePackBinary.WriteArrayHeader(stream, bucket.PeerTags.Count);
                foreach (var tag in bucket.PeerTags)
                {
                    MessagePackBinary.WriteStringBytes(stream, tag);
                }
            }

            MessagePackBinary.WriteStringBytes(stream, AdditionalMetricTagsKeyBytes);
            MessagePackBinary.WriteArrayHeader(stream, bucket.AdditionalMetricTags.Count);
            foreach (var tag in bucket.AdditionalMetricTags)
            {
                MessagePackBinary.WriteStringBytes(stream, tag);
            }
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

            MessagePackBinary.WriteStringBytes(stream, StartKeyBytes);
            MessagePackBinary.WriteInt64(stream, Start);

            MessagePackBinary.WriteStringBytes(stream, DurationKeyBytes);
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

            MessagePackBinary.WriteStringBytes(stream, StatsKeyBytes);
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
