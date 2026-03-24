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

        public void Serialize(Stream stream, long bucketDuration)
        {
            var count = 8;
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

            MessagePackBinary.WriteString(stream, "Hostname");
            MessagePackBinary.WriteString(stream, _header.HostName ?? string.Empty);

            MessagePackBinary.WriteString(stream, "Env");
            MessagePackBinary.WriteString(stream, StringUtil.IsNullOrEmpty(details.Environment) ? "unknown-env" : details.Environment);

            MessagePackBinary.WriteString(stream, "Version");
            MessagePackBinary.WriteString(stream, details.Version ?? string.Empty);

            if (writeTags)
            {
                MessagePackBinary.WriteString(stream, "ProcessTags");
                MessagePackBinary.WriteString(stream, serializedTags);
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

            if (writeGitCommitSha)
            {
                MessagePackBinary.WriteString(stream, "GitCommitSha");
                MessagePackBinary.WriteString(stream, details.GitCommitSha);
            }
        }

        private static void SerializeBucket(Stream stream, StatsBucket bucket)
        {
            var hasServiceSource = !string.IsNullOrEmpty(bucket.Key.ServiceSource);
            var fieldCount = bucket.PeerTags.Count == 0
                                 ? (hasServiceSource ? 19 : 18)
                                 : (hasServiceSource ? 20 : 19);
            MessagePackBinary.WriteMapHeader(stream, fieldCount);

            // TODO: precompute the string constants in this file
            MessagePackBinary.WriteString(stream, "Service");
            MessagePackBinary.WriteString(stream, bucket.Key.Service);

            MessagePackBinary.WriteString(stream, "Name");
            MessagePackBinary.WriteString(stream, bucket.Key.OperationName);

            MessagePackBinary.WriteString(stream, "Resource");
            MessagePackBinary.WriteString(stream, bucket.Key.Resource);

            MessagePackBinary.WriteString(stream, "Synthetics");
            MessagePackBinary.WriteBoolean(stream, bucket.Key.IsSyntheticsRequest);

            MessagePackBinary.WriteString(stream, "HTTPStatusCode");
            MessagePackBinary.WriteInt32(stream, bucket.Key.HttpStatusCode);

            MessagePackBinary.WriteString(stream, "Type");
            MessagePackBinary.WriteString(stream, bucket.Key.Type);

            // Based on https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/stats/weight.go
            // Hits, Errors, TopLevelHits are weighted by 1/sampling_rate.
            // Use stochastic rounding to convert to int64 to prevent systematic bias.
            MessagePackBinary.WriteString(stream, "Hits");
            MessagePackBinary.WriteInt64(stream, StochasticRound(bucket.Hits));

            MessagePackBinary.WriteString(stream, "Errors");
            MessagePackBinary.WriteInt64(stream, StochasticRound(bucket.Errors));

            MessagePackBinary.WriteString(stream, "Duration");
            MessagePackBinary.WriteInt64(stream, bucket.Duration);

            MessagePackBinary.WriteString(stream, "OkSummary");
            SerializeSketch(stream, bucket.OkSummary);

            MessagePackBinary.WriteString(stream, "ErrorSummary");
            SerializeSketch(stream, bucket.ErrorSummary);

            MessagePackBinary.WriteString(stream, "TopLevelHits");
            MessagePackBinary.WriteInt64(stream, StochasticRound(bucket.TopLevelHits));

            // Based on https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/stats/aggregation.go
            MessagePackBinary.WriteString(stream, "SpanKind");
            MessagePackBinary.WriteString(stream, bucket.Key.SpanKind);

            // Spec defines Trilean: NOT_SET=0, TRUE=1, FALSE=2
            MessagePackBinary.WriteString(stream, "IsTraceRoot");
            MessagePackBinary.WriteInt32(stream, bucket.Key.IsTraceRoot ? 1 : 2);

            MessagePackBinary.WriteString(stream, "HTTPMethod");
            MessagePackBinary.WriteString(stream, bucket.Key.HttpMethod);

            MessagePackBinary.WriteString(stream, "HTTPEndpoint");
            MessagePackBinary.WriteString(stream, bucket.Key.HttpEndpoint);

            MessagePackBinary.WriteString(stream, "GRPCStatusCode");
            MessagePackBinary.WriteInt32(stream, bucket.Key.GrpcStatusCode);

            if (hasServiceSource)
            {
                MessagePackBinary.WriteString(stream, "srv_src");
                MessagePackBinary.WriteString(stream, bucket.Key.ServiceSource);
            }

            // Based on https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/stats/span_concentrator.go#L53-L99
            if (bucket.PeerTags.Count != 0)
            {
                MessagePackBinary.WriteString(stream, "PeerTags");
                MessagePackBinary.WriteArrayHeader(stream, bucket.PeerTags.Count);
                foreach (var tag in bucket.PeerTags)
                {
                    MessagePackBinary.WriteStringBytes(stream, tag);
                }
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

        /// <summary>
        /// Converts a floating-point value to long using stochastic rounding.
        /// The fractional part is used as a probability of rounding up, preventing
        /// systematic bias that occurs with simple truncation.
        /// </summary>
        private static long StochasticRound(double value)
        {
            var truncated = (long)value;
            if (ThreadSafeRandom.Shared.NextDouble() < value - truncated)
            {
                return truncated + 1;
            }

            return truncated;
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
