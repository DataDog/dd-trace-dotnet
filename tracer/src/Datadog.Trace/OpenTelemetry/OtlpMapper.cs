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
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.OpenTelemetry.Common;
using Datadog.Trace.Processors;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.OpenTelemetry;

internal static class OtlpMapper
{
    internal delegate void KeyValueWriter<TState>(ref TState state, KeyValue keyValue);

    public static void EmitResourceAttributesFromTraceChunk(in TraceChunkModel traceChunk, Action<KeyValue> writeKeyValue, OtlpResourceGlobalTags? resourceGlobalTags = null)
    {
        EmitResourceAttributesFromTraceChunk(
            in traceChunk,
            ref writeKeyValue,
            static (ref Action<KeyValue> action, KeyValue keyValue) => action(keyValue),
            resourceGlobalTags);
    }

    public static void EmitResourceAttributesFromTraceChunk<TState>(in TraceChunkModel traceChunk, ref TState state, KeyValueWriter<TState> writeKeyValue, OtlpResourceGlobalTags? resourceGlobalTags = null)
    {
        writeKeyValue(ref state, new KeyValue("service.name", traceChunk.DefaultServiceName ?? "unknown_service:dotnet"));

        // Breaking change: We are now sending the service version as a resource attribute.
        // This means we're adding version tags to all spans, not just those whose service name is the default service name
        if (traceChunk.ServiceVersion is string version)
        {
            // Note: The `service.version` resource attribute gets written as both a `service.version` span tag
            // and a `version` span tag
            writeKeyValue(ref state, new KeyValue("service.version", version));
        }

        if (traceChunk.Environment is string environment)
        {
            // Note: The `deployment.environment.name` resource attribute gets written as both a `deployment.environment.name` span tag
            // and a `env` span tag
            writeKeyValue(ref state, new KeyValue("deployment.environment.name", environment));
        }

        // Write telemetry SDK attributes
        writeKeyValue(ref state, new KeyValue("telemetry.sdk.name", TracerConstants.TelemetrySdkName));
        writeKeyValue(ref state, new KeyValue("telemetry.sdk.language", TracerConstants.Language));
        writeKeyValue(ref state, new KeyValue("telemetry.sdk.version", TracerConstants.SemanticVersion));

        if (traceChunk.GitCommitSha is string gitCommitSha)
        {
            writeKeyValue(ref state, new KeyValue("git.commit.sha", gitCommitSha));
        }

        if (traceChunk.GitRepositoryUrl is string gitRepositoryUrl)
        {
            writeKeyValue(ref state, new KeyValue("git.repository_url", gitRepositoryUrl));
        }

        writeKeyValue(ref state, new KeyValue(Trace.Tags.RuntimeId, Tracer.RuntimeId));

        writeKeyValue(ref state, new KeyValue(Trace.Tags.SdkOtlpExport, "true"));
        writeKeyValue(ref state, new KeyValue(Trace.Tags.SdkSemantics, traceChunk.OtelSemanticsEnabled ? "otel" : "datadog"));

        if (traceChunk.ClientComputedStats)
        {
            writeKeyValue(ref state, new KeyValue("_dd.stats_computed", "true"));
        }

        // Global tags (DD_TAGS, leftover OTEL_RESOURCE_ATTRIBUTES, or the remote config "tracing_tags" override)
        // are written once on the resource. Span attributes with the same key and value are omitted (see TagWriter).
        if (resourceGlobalTags is not null)
        {
            var attributes = resourceGlobalTags.Attributes;
            for (var i = 0; i < attributes.Count; i++)
            {
                writeKeyValue(ref state, new KeyValue(attributes[i].Key, attributes[i].Value));
            }
        }
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

    public static int EmitAttributesFromSpan(Action<KeyValue> writeKeyValue, in SpanModel spanModel, int limit, OtlpResourceGlobalTags? resourceGlobalTags = null)
    {
        return EmitAttributesFromSpan(
            in spanModel,
            limit,
            ref writeKeyValue,
            static (ref Action<KeyValue> action, KeyValue keyValue) => action(keyValue),
            resourceGlobalTags);
    }

    public static int EmitAttributesFromSpan<TState>(in SpanModel spanModel, int limit, ref TState state, KeyValueWriter<TState> writeKeyValue, OtlpResourceGlobalTags? resourceGlobalTags = null)
    {
        int count = 0;
        int droppedAttributesCount = 0;
        bool openTelemetrySemanticsEnabled = spanModel.Span.OpenTelemetrySemanticsEnabled;

        if (!openTelemetrySemanticsEnabled)
        {
            if (count < limit)
            {
                writeKeyValue(ref state, new KeyValue("service.name", spanModel.Span.ServiceName));
                count++;
            }
            else
            {
                droppedAttributesCount++;
            }

            if (count < limit)
            {
                writeKeyValue(ref state, new KeyValue("operation.name", spanModel.Span.OperationName));
                count++;
            }
            else
            {
                droppedAttributesCount++;
            }

            if (count < limit)
            {
                writeKeyValue(ref state, new KeyValue("resource.name", spanModel.Span.ResourceName));
                count++;
            }
            else
            {
                droppedAttributesCount++;
            }

            if (count < limit)
            {
                writeKeyValue(ref state, new KeyValue("span.type", spanModel.Span.Type));
                count++;
            }
            else
            {
                droppedAttributesCount++;
            }

            // add "runtime-id" tag to service-entry (aka top-level) spans
            var testOptimization = Ci.TestOptimization.Instance;
            if (spanModel.Span.IsTopLevel && (!testOptimization.IsRunning || !testOptimization.Settings.Agentless))
            {
                if (count < limit)
                {
                    writeKeyValue(ref state, new KeyValue(Trace.Tags.RuntimeId, Tracer.RuntimeId));
                    count++;
                }
                else
                {
                    droppedAttributesCount++;
                }
            }
        }

        // Write trace tags
        if (!string.IsNullOrEmpty(spanModel.Span.Context.LastParentId))
        {
            if (count < limit)
            {
                writeKeyValue(ref state, new KeyValue(Trace.Tags.LastParentId, spanModel.Span.Context.LastParentId));
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
                writeKeyValue(ref state, new KeyValue(Trace.Tags.Origin, spanModel.TraceChunk.Origin));
                count++;
            }
            else
            {
                droppedAttributesCount++;
            }
        }

        if (spanModel.IsLocalRoot &&
            spanModel.Span.Context.TraceContext?.FeatureFlagEnrichment is { } featureFlagEnrichment &&
            featureFlagEnrichment.HasData())
        {
            var ffeTags = featureFlagEnrichment.BuildSpanTags();

            if (!StringUtil.IsNullOrEmpty(ffeTags.FlagsEnc))
            {
                if (count < limit)
                {
                    writeKeyValue(ref state, new KeyValue(SpanEnrichmentState.TagFlagsEnc, ffeTags.FlagsEnc!));
                    count++;
                }
                else
                {
                    droppedAttributesCount++;
                }
            }

            if (!StringUtil.IsNullOrEmpty(ffeTags.SubjectsEnc))
            {
                if (count < limit)
                {
                    writeKeyValue(ref state, new KeyValue(SpanEnrichmentState.TagSubjectsEnc, ffeTags.SubjectsEnc!));
                    count++;
                }
                else
                {
                    droppedAttributesCount++;
                }
            }

            if (!StringUtil.IsNullOrEmpty(ffeTags.RuntimeDefaults))
            {
                if (count < limit)
                {
                    writeKeyValue(ref state, new KeyValue(SpanEnrichmentState.TagRuntimeDefaults, ffeTags.RuntimeDefaults!));
                    count++;
                }
                else
                {
                    droppedAttributesCount++;
                }
            }
        }

        ITagProcessor[]? tagProcessors = null;
        if (spanModel.Span.Context.TraceContext?.Tracer is Tracer tracer)
        {
            tagProcessors = tracer.TracerManager?.TagProcessors;
        }

        // Sampling decision attributes (same tags and placement as the MessagePack path) let the Agent
        // honor the tracer's decision instead of applying its own OTLP sampler. They are written before
        // span tags so customer tags are dropped first when the attribute limit is reached.

        // add "_sampling_priority_v1" to all "chunk orphans" (spans whose parents are not found in the same chunk).
        // Written first so a large number of trace tags can't push it past the attribute limit.
        // Written as an OTLP int; the Agent converts int and double attributes to the same span metric.
        if (spanModel is { IsChunkOrphan: true, TraceChunk.SamplingPriority: { } samplingPriority })
        {
            WriteAttribute(ref state, writeKeyValue, new KeyValue(Trace.Metrics.SamplingPriority, samplingPriority), limit, ref count, ref droppedAttributesCount);
        }

        // Preserve security reports and serializer-generated metadata before customer tags can exhaust the limit.
        var securityWriter = new TagWriter<TState>(state, writeKeyValue, tagProcessors: null, count, limit);
        if (!spanModel.TraceChunk.IsApmEnabled)
        {
            securityWriter.WriteSecurityAttribute(new KeyValue(Trace.Metrics.ApmEnabled, 0.0));
        }

        if (spanModel.IsLocalRoot && spanModel.Span.Context.TraceContext?.WafExecuted is true && Security.Instance.AppsecEnabled)
        {
            securityWriter.WriteSecurityAttribute(new KeyValue(Trace.Metrics.AppSecEnabled, 1.0));
            securityWriter.WriteSecurityAttribute(new KeyValue(Trace.Tags.RuntimeFamily, TracerConstants.Language));
            if (Security.Instance.WafRuleFileVersion is { } ruleFileVersion)
            {
                securityWriter.WriteSecurityAttribute(new KeyValue(Trace.Tags.AppSecRuleFileVersion, ruleFileVersion));
            }
        }

        spanModel.Span.Tags.EnumerateMetaStruct(ref securityWriter);
        count = securityWriter.Count;
        droppedAttributesCount += securityWriter.DroppedCount;
        state = securityWriter.State;

        if (spanModel.TraceChunk.Tags is { Count: > 0 } traceTags &&
            (spanModel.IsFirstSpanInChunk || spanModel.IsLocalRoot))
        {
            var traceTagWriter = new TraceTagWriter<TState>(state, writeKeyValue, tagProcessors, spanModel.IsLocalRoot, spanModel.IsFirstSpanInChunk, count, limit, securityWriter.ReservedKeys);
            traceTags.Enumerate(ref traceTagWriter);
            count = traceTagWriter.Count;
            droppedAttributesCount += traceTagWriter.DroppedCount;
            state = traceTagWriter.State;
        }

        if (spanModel.IsLocalRoot)
        {
            // TODO: unlike SpanMessagePackFormatter, we don't write "process_id" or "_dd.tracer_kr" on the local root yet.
            // "process_id" likely belongs on the OTLP resource instead (OTel semantic convention "process.pid").
            // It's still open whether "_dd.tracer_kr" (tracer keep rate: moving average of the share of traces
            // sent vs. dropped before reaching the Agent) should be exported over OTLP at all.

            // add agent or rule sampling rate
            if (spanModel.TraceChunk is { AppliedSamplingRate: { } samplingRate, SamplingMechanism: { } samplingMechanism })
            {
                var samplingRateTagName = samplingMechanism switch
                {
                    SamplingMechanism.AgentRate => Trace.Metrics.SamplingAgentDecision, // "_dd.agent_psr"
                    SamplingMechanism.LocalTraceSamplingRule
                        or SamplingMechanism.RemoteAdaptiveSamplingRule
                        or SamplingMechanism.RemoteUserSamplingRule => Trace.Metrics.SamplingRuleDecision, // "_dd.rule_psr"
                    _ => null
                };

                if (samplingRateTagName is not null)
                {
                    WriteAttribute(ref state, writeKeyValue, new KeyValue(samplingRateTagName, samplingRate), limit, ref count, ref droppedAttributesCount);
                }
            }

            // add rate limiter rate
            if (spanModel.TraceChunk.RateLimiterRate is { } limitSamplingRate)
            {
                WriteAttribute(ref state, writeKeyValue, new KeyValue(Trace.Metrics.SamplingLimitDecision, limitSamplingRate), limit, ref count, ref droppedAttributesCount);
            }
        }

        // Notes for later:
        // - Do we actually need to add _dd.base_service tag even though the OTLP span shares the same service name?

        // add _dd.base_service tag to spans where the service name has been overrideen
        // Process tags will be sent only once per buffer/payload (one payload can contain many chunks from different traces)
        // SCI tags will be sent only once per trace
        // AAS tags need to be set on any span for the backend to properly handle the billing.

        // Span tags and metrics share the remaining slots and must not repeat security attribute keys.
        var tagWriter = new TagWriter<TState>(state, writeKeyValue, tagProcessors, count, limit, openTelemetrySemanticsEnabled, resourceGlobalTags, securityWriter.ReservedKeys);
        spanModel.Span.Tags.EnumerateTags(ref tagWriter, openTelemetrySemanticsEnabled);
        spanModel.Span.Tags.EnumerateMetrics(ref tagWriter);
        droppedAttributesCount += tagWriter.DroppedCount;
        state = tagWriter.State;

        // add "_dd.top_level" to top-level spans (aka service-entry spans)

        return droppedAttributesCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAttribute<TState>(ref TState state, KeyValueWriter<TState> writeKeyValue, KeyValue keyValue, int limit, ref int count, ref int droppedCount)
    {
        if (count < limit)
        {
            writeKeyValue(ref state, keyValue);
            count++;
        }
        else
        {
            droppedCount++;
        }
    }

    // Keep the security keys emitted by today's tracer in a bit mask. Other MetaStruct keys
    // still participate in collision handling without allocating on the common paths.
    internal struct SecurityAttributeKeys
    {
        private byte _known;
        private HashSet<string>? _other;

        public void Add(string key)
        {
            var bit = GetBit(key);
            if (bit != 0)
            {
                _known |= bit;
            }
            else
            {
                (_other ??= new(StringComparer.Ordinal)).Add(key);
            }
        }

        public readonly bool Contains(string key) =>
            (_known != 0 && (_known & GetBit(key)) != 0) || _other?.Contains(key) == true;

        private static byte GetBit(string key) => key switch
        {
            Trace.Metrics.ApmEnabled => 1 << 0,
            Trace.Metrics.AppSecEnabled => 1 << 1,
            Trace.Tags.RuntimeFamily => 1 << 2,
            Trace.Tags.AppSecRuleFileVersion => 1 << 3,
            "appsec" => 1 << 4,
            "iast" => 1 << 5,
            "_dd.stack" => 1 << 6,
            _ => 0,
        };
    }

    internal struct TraceTagWriter<TState> : TraceTagCollection.ITagEnumerator
    {
        private readonly KeyValueWriter<TState> _writeKeyValue;
        private readonly ITagProcessor[]? _tagProcessors;
        private readonly SecurityAttributeKeys _reservedKeys;
        private readonly bool _isLocalRoot;
        private readonly bool _isFirstSpanInChunk;
        private readonly int _limit;

        public TState State;
        public int Count;
        public int DroppedCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TraceTagWriter(TState state, KeyValueWriter<TState> writeKeyValue, ITagProcessor[]? tagProcessors, bool isLocalRoot, bool isFirstSpanInChunk, int count, int limit, SecurityAttributeKeys reservedKeys = default)
        {
            State = state;
            _writeKeyValue = writeKeyValue;
            _tagProcessors = tagProcessors;
            _reservedKeys = reservedKeys;
            _isLocalRoot = isLocalRoot;
            _isFirstSpanInChunk = isFirstSpanInChunk;
            _limit = limit;

            Count = count;
            DroppedCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Next(KeyValuePair<string, string> item)
        {
            // TagPropagation.ParseHeader accepts the "_dd.p." prefix case-insensitively and keeps the original casing,
            // so classify keys the same way here. Note: SpanMessagePackFormatter.TraceTagWriter and the header
            // injection in TagPropagation still compare ordinally.

            // the upper 64 bits of the trace id are already part of the 128-bit OTLP traceId
            if (string.Equals(item.Key, Tags.Propagated.TraceIdUpper, StringComparison.OrdinalIgnoreCase) || _reservedKeys.Contains(item.Key))
            {
                return;
            }

            var isPropagatedTag = item.Key.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.OrdinalIgnoreCase);

            // add propagated trace tags to the first span in every chunk,
            // add non-propagated trace tags to the root span
            if ((isPropagatedTag && _isFirstSpanInChunk) || (!isPropagatedTag && _isLocalRoot))
            {
                if (Count < _limit)
                {
                    var key = item.Key;
                    var value = item.Value;

                    if (_tagProcessors is not null)
                    {
                        for (var i = 0; i < _tagProcessors.Length; i++)
                        {
                            _tagProcessors[i]?.ProcessMeta(ref key, ref value);
                        }
                    }

                    _writeKeyValue(ref State, new KeyValue(key, value));
                    Count++;
                }
                else
                {
                    DroppedCount++;
                }
            }
        }
    }

    internal struct TagWriter<TState> : IItemProcessor<string>, IItemProcessor<int>, IItemProcessor<double>, IItemProcessor<byte[]>
    {
        private readonly KeyValueWriter<TState> _writeKeyValue;
        private readonly ITagProcessor[]? _tagProcessors;
        private readonly int _limit;
        private readonly bool _openTelemetrySemanticsEnabled;
        private readonly OtlpResourceGlobalTags? _resourceGlobalTags;
        public SecurityAttributeKeys ReservedKeys;

        public TState State;
        public int Count;
        public int DroppedCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TagWriter(TState state, KeyValueWriter<TState> writeKeyValue, ITagProcessor[]? tagProcessors, int count, int limit, bool openTelemetrySemanticsEnabled = false, OtlpResourceGlobalTags? resourceGlobalTags = null, SecurityAttributeKeys reservedKeys = default)
        {
            State = state;
            _writeKeyValue = writeKeyValue;
            _tagProcessors = tagProcessors;
            _limit = limit;
            _openTelemetrySemanticsEnabled = openTelemetrySemanticsEnabled;
            _resourceGlobalTags = resourceGlobalTags;
            ReservedKeys = reservedKeys;

            Count = count;
            DroppedCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Process(TagItem<string> item)
        {
            // We are using the original key since we're not serializing MessagePack
            string key = item.Key;
            string value = item.Value;

            // Do not record the resource-level attributes telemetry SDK attributes
            // as span attributes. Silently drop them.
            if (key == "telemetry.sdk.name"
                || key == "telemetry.sdk.language"
                || key == "telemetry.sdk.version")
            {
                return;
            }

            // When OTel trace compatibility is enabled, suppress tags that would duplicate
            // OTLP-native fields or Datadog-specific attributes not relevant to OTel consumers.
            if (_openTelemetrySemanticsEnabled
                && (key == Tags.ErrorMsg
                    || key == Tags.ErrorStack
                    // Do not exclude "error.type" because it is a stable OTel attribute,
                    // see https://opentelemetry.io/docs/specs/semconv/registry/attributes/error/
                    || key == "otel.status_code"
                    || key == "otel.status_description"
                    || key == Tags.SpanKind
                    || key == "service.name"
                    || key == "service.version"
                    || key == "service.instance.id"
                    || key == Tags.InstrumentationName
                    || key == "http-client-handler-type"))
            {
                return;
            }

            // Global tags are written once as resource attributes. Skip the span attribute when the resource
            // already carries the same key and value, before it counts against the attribute limit.
            // A span-level override (different value) is kept. Only string tags are compared,
            // so numeric tags keep their type on the span.
            if (_resourceGlobalTags?.Contains(key, value) == true || ReservedKeys.Contains(key))
            {
                return;
            }

            if (Count < _limit)
            {
                if (_tagProcessors is not null)
                {
                    for (var i = 0; i < _tagProcessors.Length; i++)
                    {
                        _tagProcessors[i]?.ProcessMeta(ref key, ref value);
                    }
                }

                _writeKeyValue(ref State, new KeyValue(key, value));
                Count++;
            }
            else
            {
                DroppedCount++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Process(TagItem<int> item)
        {
            if (ReservedKeys.Contains(item.Key))
            {
                return;
            }

            if (Count < _limit)
            {
                // We are using the original key since we're not serializing MessagePack
                string key = item.Key;
                int value = item.Value;

                if (_tagProcessors is not null)
                {
                    for (var i = 0; i < _tagProcessors.Length; i++)
                    {
                        _tagProcessors[i]?.ProcessMeta(ref key, ref value);
                    }
                }

                _writeKeyValue(ref State, new KeyValue(key, value));
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
            if (ReservedKeys.Contains(item.Key))
            {
                return;
            }

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

                _writeKeyValue(ref State, new KeyValue(key, value));
                Count++;
            }
            else
            {
                DroppedCount++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Process(TagItem<byte[]> item) => WriteSecurityAttribute(new KeyValue(item.Key, item.Value));

        public void WriteSecurityAttribute(KeyValue attribute)
        {
            // A collision isn't a second dropped attribute, even when the limit drops the report itself.
            ReservedKeys.Add(attribute.Key);
            WriteAttribute(ref State, _writeKeyValue, attribute, _limit, ref Count, ref DroppedCount);
        }
    }
}
