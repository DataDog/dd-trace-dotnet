// <copyright file="OtlpMapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Configuration;
using Datadog.Trace.OpenTelemetry.Common;
using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.VendoredMicrosoftCode.System;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.OpenTelemetry;

internal static class OtlpMapper
{
    public static void EmitResourceAttributesFromTraceChunk(in TraceChunkModel traceChunk, Action<KeyValue> writeKeyValue)
    {
        writeKeyValue(new KeyValue("service.name", traceChunk.DefaultServiceName ?? "unknown_service:dotnet"));

        // Breaking change: We are now sending the service version as a resource attribute.
        // This means we're adding version tags to all spans, not just those whose service name is the default service name
        if (traceChunk.ServiceVersion is string version)
        {
            // Note: The `service.version` resource attribute gets written as both a `service.version` span tag
            // and a `version` span tag
            writeKeyValue(new KeyValue("service.version", version));
        }

        if (traceChunk.Environment is string environment)
        {
            // Note: The `deployment.environment.name` resource attribute gets written as both a `deployment.environment.name` span tag
            // and a `env` span tag
            writeKeyValue(new KeyValue("deployment.environment.name", environment));
        }

        // Write telemetry SDK attributes
        writeKeyValue(new KeyValue("telemetry.sdk.language", TracerConstants.Language));
        writeKeyValue(new KeyValue("telemetry.sdk.version", TracerConstants.AssemblyVersion));

        if (traceChunk.GitCommitSha is string gitCommitSha)
        {
            writeKeyValue(new KeyValue("git.commit.sha", gitCommitSha));
        }

        if (traceChunk.GitRepositoryUrl is string gitRepositoryUrl)
        {
            writeKeyValue(new KeyValue("git.repository_url", gitRepositoryUrl));
        }

        writeKeyValue(new KeyValue(Trace.Tags.RuntimeId, Tracer.RuntimeId));
    }

    public static bool IsHandledResourceAttribute(string tagKey)
    {
        return tagKey.Equals("service", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("env", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("version", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("service.name", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("deployment.environment.name", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("deployment.environment", StringComparison.OrdinalIgnoreCase) ||
               tagKey.Equals("service.version", StringComparison.OrdinalIgnoreCase);
    }

    public static int EmitAttributesFromSpan(Action<KeyValue> writeKeyValue, in SpanModel spanModel, int limit)
    {
        int count = 0;
        int droppedAttributesCount = 0;

        // Write span tags
        ITagProcessor[]? tagProcessors = null;
        if (spanModel.Span.Context.TraceContext?.Tracer is Tracer tracer)
        {
            tagProcessors = tracer.TracerManager?.TagProcessors;
        }

        var tagWriter = new TagWriter(writeKeyValue, tagProcessors, count, limit);
        spanModel.Span.Tags.EnumerateTags(ref tagWriter);
        count += tagWriter.Count;
        droppedAttributesCount += tagWriter.DroppedCount;

        // Write trace tags

        if (!string.IsNullOrEmpty(spanModel.Span.Context.LastParentId))
        {
            if (count < limit)
            {
                writeKeyValue(new KeyValue(Trace.Tags.LastParentId, spanModel.Span.Context.LastParentId));
                count++;
            }
            else
            {
                droppedAttributesCount++;
            }
        }

        // TODO: Only write these as resource attributes
        // add "runtime-id" tag to service-entry (aka top-level) spans
        var testOptimization = Ci.TestOptimization.Instance;
        if (spanModel.Span.IsTopLevel && (!testOptimization.IsRunning || !testOptimization.Settings.Agentless))
        {
            if (count < limit)
            {
                writeKeyValue(new KeyValue(Trace.Tags.RuntimeId, Tracer.RuntimeId));
                count++;
            }
            else
            {
                droppedAttributesCount++;
            }
        }

        // add "_dd.origin" tag to all spans
        if (!string.IsNullOrEmpty(spanModel.TraceChunk.Origin))
        {
            if (count < limit)
            {
                writeKeyValue(new KeyValue(Trace.Tags.Origin, spanModel.TraceChunk.Origin));
                count++;
            }
            else
            {
                droppedAttributesCount++;
            }
        }

        // Notes for later:
        // - Do we actually need to add _dd.base_service tag even though the OTLP span shares the same service name?

        // add _dd.base_service tag to spans where the service name has been overrideen
        // Process tags will be sent only once per buffer/payload (one payload can contain many chunks from different traces)
        // SCI tags will be sent only once per trace
        // if (Security.Instance.AppsecEnabled && model.IsLocalRoot && span.Context.TraceContext?.WafExecuted is true)
        // AAS tags need to be set on any span for the backend to properly handle the billing.

        // Write span metrics
        // Note: I could have done this earlier but I wanted to simulate the same behavior as the MessagePack formatter.
        var metricsWriter = new TagWriter(writeKeyValue, tagProcessors, count, limit);
        spanModel.Span.Tags.EnumerateMetrics(ref metricsWriter);
        count += metricsWriter.Count;
        droppedAttributesCount += metricsWriter.DroppedCount;

        // if (model.IsLocalRoot)
        // add the "apm.enabled" tag with a value of 0
        // if (Security.Instance.AppsecEnabled && model.IsLocalRoot && span.Context.TraceContext?.WafExecuted is true)
        // add "_sampling_priority_v1" tag to all "chunk orphans"
        // add "_dd.top_level" to top-level spans (aka service-entry spans)

        return droppedAttributesCount;
    }

#if NET6_0_OR_GREATER
    public static List<Datadog.Trace.OpenTelemetry.Metrics.MetricPoint> ConvertToOtlpMetrics(StatsBuffer buffer, DateTimeOffset endTime)
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

        var metricPoints = new List<Datadog.Trace.OpenTelemetry.Metrics.MetricPoint>();
        foreach (var bucket in buffer.Buckets.Values)
        {
            metricPoints.Add(OtlpSerializeBucket(bucket, buffer.StartTime, endTime));
        }

        return metricPoints;
    }

    private static Datadog.Trace.OpenTelemetry.Metrics.MetricPoint OtlpSerializeBucket(StatsBucket bucket, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        // TODO: Do something with bucket.Key.Service
        // Ignored for now: Service, IsSyntheticsRequest

        var timeseriesAttributes = new Dictionary<string, object?>()
        {
            // { "service", bucket.Key.Service },
            { "Name", bucket.Key.OperationName },
            { "Resource", bucket.Key.Resource },
            { "Type", bucket.Key.Type },
            { "StatusCode", bucket.Key.HttpStatusCode },
            { "TopLevel", bucket.Key.IsTopLevel },
            { "Error", bucket.Key.IsError },
        };

        var metricPoint = new Datadog.Trace.OpenTelemetry.Metrics.MetricPoint(instrumentName: "request.latencies", meterName: "datadog.trace.metrics", meterVersion: string.Empty, meterTags: Array.Empty<KeyValuePair<string, object?>>(), instrumentType: Datadog.Trace.OpenTelemetry.Metrics.InstrumentType.Histogram, temporality: Datadog.Trace.OpenTelemetry.Metrics.AggregationTemporality.Delta, tags: timeseriesAttributes, unit: "ns", description: "Summary of request latencies")
        {
            StartTime = startTime,
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
#endif

    internal struct TagWriter : IItemProcessor<string>, IItemProcessor<double>, IItemProcessor<byte[]>
    {
        private readonly Action<KeyValue> _writeKeyValue;
        private readonly ITagProcessor[]? _tagProcessors;
        private readonly int _limit;

        public int Count;
        public int DroppedCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TagWriter(Action<KeyValue> writeKeyValue, ITagProcessor[]? tagProcessors, int count, int limit)
        {
            _writeKeyValue = writeKeyValue;
            _tagProcessors = tagProcessors;
            _limit = limit;

            Count = count;
            DroppedCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Process(TagItem<string> item)
        {
            if (Count < _limit)
            {
                // We are using the original key since we're not serializing MessagePack
                string key = item.Key;
                string value = item.Value;

                if (_tagProcessors is not null)
                {
                    for (var i = 0; i < _tagProcessors.Length; i++)
                    {
                        _tagProcessors[i]?.ProcessMeta(ref key, ref value);
                    }
                }

                _writeKeyValue(new KeyValue(key, value));
                Count++;
            }
            else
            {
                DroppedCount++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Process(TagItem<double> item)
        {
            if (Count < _limit)
            {
                // We are using the original key since we're not serializing MessagePack
                string key = item.Key;
                double value = item.Value;

                if (_tagProcessors is not null)
                {
                    for (var i = 0; i < _tagProcessors.Length; i++)
                    {
                        _tagProcessors[i]?.ProcessMetric(ref key, ref value);
                    }
                }

                _writeKeyValue(new KeyValue(key, value));
                Count++;
            }
            else
            {
                DroppedCount++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Process(TagItem<byte[]> item)
        {
            if (Count < _limit)
            {
                _writeKeyValue(new KeyValue(item.Key, item.Value));
                Count++;
            }
            else
            {
                DroppedCount++;
            }
        }
    }
}
