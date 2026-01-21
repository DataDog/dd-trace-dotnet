// <copyright file="OtlpMapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.CompilerServices;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.VendoredMicrosoftCode.System;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.OpenTelemetry.Traces;

internal static class OtlpMapper
{
    public static void WriteDatadogResourceAttributes(JsonTextWriter writer, in TraceChunkModel traceChunk)
    {
        if (traceChunk.DefaultServiceName is string service)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue("service.name", service));
        }

        if (traceChunk.ServiceVersion is string version)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue("service.version", version));
        }

        if (traceChunk.Environment is string environment)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue("deployment.environment.name", environment));
        }

        if (traceChunk.GitCommitSha is string gitCommitSha)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue("git.commit.sha", gitCommitSha));
        }

        if (traceChunk.GitRepositoryUrl is string gitRepositoryUrl)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue("git.repository_url", gitRepositoryUrl));
        }

        OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.Language, TracerConstants.Language));

        var testOptimization = Ci.TestOptimization.Instance;
        if (!testOptimization.IsRunning || !testOptimization.Settings.Agentless)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.RuntimeId, Tracer.RuntimeId));
        }
    }

    public static int WriteDatadogSpanAttributes(JsonTextWriter writer, in SpanModel spanModel, int limit)
    {
        int count = 0;
        int droppedAttributesCount = 0;

        writer.WriteStartArray();

        // Write span tags
        ITagProcessor[]? tagProcessors = null;
        if (spanModel.Span.Context.TraceContext?.Tracer is Tracer tracer)
        {
            tagProcessors = tracer.TracerManager?.TagProcessors;
        }

        if (tagProcessors is not null && tagProcessors.Length > 0)
        {
            var tagWriter = new TagWriter(writer, tagProcessors, count, limit);
            spanModel.Span.Tags.EnumerateTags(ref tagWriter);
            count += tagWriter.Count;
            droppedAttributesCount += tagWriter.DroppedCount;
        }

        // Write trace tags

        if (!string.IsNullOrEmpty(spanModel.Span.Context.LastParentId))
        {
            if (count < limit)
            {
                OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.LastParentId, spanModel.Span.Context.LastParentId));
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
                OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.RuntimeId, Tracer.RuntimeId));
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
                OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.Origin, spanModel.TraceChunk.Origin));
                count++;
            }
            else
            {
                droppedAttributesCount++;
            }
        }

        // add "env" to all spans
        if (count < limit)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.Env, spanModel.TraceChunk.Environment));
            count++;
        }
        else
        {
            droppedAttributesCount++;
        }

        // add "language=dotnet" tag to all spans
        if (count < limit)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.Language, TracerConstants.Language));
            count++;
        }
        else
        {
            droppedAttributesCount++;
        }

        // add "version" tags to all spans whose service name is the default service name
        // add _dd.base_service tag to spans where the service name has been overrideen
        // Process tags will be sent only once per buffer/payload (one payload can contain many chunks from different traces)
        // SCI tags will be sent only once per trace
        // if (Security.Instance.AppsecEnabled && model.IsLocalRoot && span.Context.TraceContext?.WafExecuted is true)
        // AAS tags need to be set on any span for the backend to properly handle the billing.

        // Write span metrics
        // Note: I could have done this earlier but I wanted to simulate the same behavior as the MessagePack formatter.
        if (tagProcessors is not null && tagProcessors.Length > 0)
        {
            var metricsWriter = new TagWriter(writer, tagProcessors, count, limit);
            spanModel.Span.Tags.EnumerateMetrics(ref metricsWriter);
            count += metricsWriter.Count;
            droppedAttributesCount += metricsWriter.DroppedCount;
        }

        // if (model.IsLocalRoot)
        // add the "apm.enabled" tag with a value of 0
        // if (Security.Instance.AppsecEnabled && model.IsLocalRoot && span.Context.TraceContext?.WafExecuted is true)
        // add "_sampling_priority_v1" tag to all "chunk orphans"
        // add "_dd.top_level" to top-level spans (aka service-entry spans)

        writer.WriteEndArray();
        return droppedAttributesCount;
    }

    internal struct TagWriter : IItemProcessor<string>, IItemProcessor<double>, IItemProcessor<byte[]>
    {
        private readonly JsonTextWriter _writer;
        private readonly ITagProcessor[] _tagProcessors;
        private readonly int _limit;

        public int Count;
        public int DroppedCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TagWriter(JsonTextWriter writer, ITagProcessor[] tagProcessors, int count, int limit)
        {
            _writer = writer;
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

                OtlpTracesSerializer.WriteKeyValue(_writer, new KeyValue(key, value));
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

                OtlpTracesSerializer.WriteKeyValue(_writer, new KeyValue(key, value));
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
                OtlpTracesSerializer.WriteKeyValue(_writer, new KeyValue(item.Key, item.Value));
                Count++;
            }
            else
            {
                DroppedCount++;
            }
        }
    }
}
