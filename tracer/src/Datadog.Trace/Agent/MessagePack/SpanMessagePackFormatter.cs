// <copyright file="SpanMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Processors;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class SpanMessagePackFormatter : IMessagePackFormatter<TraceChunkModel>
    {
        public static readonly SpanMessagePackFormatter Instance = new();

        // Cache the UTF-8 bytes for string constants (like tag names)
        // and values that are constant within the lifetime of a service (like process id).
        //
        // Don't make these static to avoid the additional redirection when this
        // assembly is loaded in the shared domain. We only create a single instance of
        // this class so that's fine.

        // top-level span fields
        private readonly byte[] _traceIdBytes = StringEncoding.UTF8.GetBytes("trace_id");
        private readonly byte[] _traceIdHighBytes = StringEncoding.UTF8.GetBytes("trace_id_high");
        private readonly byte[] _spanIdBytes = StringEncoding.UTF8.GetBytes("span_id");
        private readonly byte[] _nameBytes = StringEncoding.UTF8.GetBytes("name");
        private readonly byte[] _resourceBytes = StringEncoding.UTF8.GetBytes("resource");
        private readonly byte[] _serviceBytes = StringEncoding.UTF8.GetBytes("service");
        private readonly byte[] _typeBytes = StringEncoding.UTF8.GetBytes("type");
        private readonly byte[] _startBytes = StringEncoding.UTF8.GetBytes("start");
        private readonly byte[] _durationBytes = StringEncoding.UTF8.GetBytes("duration");
        private readonly byte[] _parentIdBytes = StringEncoding.UTF8.GetBytes("parent_id");
        private readonly byte[] _errorBytes = StringEncoding.UTF8.GetBytes("error");
        private readonly byte[] _metaStructBytes = StringEncoding.UTF8.GetBytes("meta_struct");

        // span links metadata
        private readonly byte[] _spanLinkBytes = StringEncoding.UTF8.GetBytes("span_links");
        private readonly byte[] _traceStateBytes = StringEncoding.UTF8.GetBytes("tracestate");
        private readonly byte[] _traceFlagBytes = StringEncoding.UTF8.GetBytes("flags");
        private readonly byte[] _attributesBytes = StringEncoding.UTF8.GetBytes("attributes");

        // string tags
        private readonly byte[] _metaBytes = StringEncoding.UTF8.GetBytes("meta");

        private readonly byte[] _languageNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Language);
        private readonly byte[] _languageValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.Language);

        private readonly byte[] _runtimeIdNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.RuntimeId);
        private readonly byte[] _runtimeIdValueBytes = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);

        private readonly byte[] _environmentNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Env);

        private readonly byte[] _gitCommitShaNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.GitCommitSha);
        private readonly byte[] _gitRepositoryUrlNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.GitRepositoryUrl);

        private readonly byte[] _versionNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Version);

        private readonly byte[] _originNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Origin);
        private readonly byte[] _lastParentIdBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.LastParentId);

        // numeric tags
        private readonly byte[] _metricsBytes = StringEncoding.UTF8.GetBytes("metrics");

        private readonly byte[] _samplingPriorityNameBytes = StringEncoding.UTF8.GetBytes(Metrics.SamplingPriority);

        private readonly byte[] _processIdNameBytes = StringEncoding.UTF8.GetBytes(Metrics.ProcessId);

        private readonly byte[] _apmEnabledBytes = StringEncoding.UTF8.GetBytes(Metrics.ApmEnabled);

        // Azure App Service tag names and values
        private byte[] _aasSiteNameTagNameBytes;
        private byte[] _aasSiteKindTagNameBytes;
        private byte[] _aasSiteTypeTagNameBytes;
        private byte[] _aasResourceGroupTagNameBytes;
        private byte[] _aasSubscriptionIdTagNameBytes;
        private byte[] _aasResourceIdTagNameBytes;
        private byte[] _aasInstanceIdTagNameBytes;
        private byte[] _aasInstanceNameTagNameBytes;
        private byte[] _aasOperatingSystemTagNameBytes;
        private byte[] _aasRuntimeTagNameBytes;
        private byte[] _aasExtensionVersionTagNameBytes;

        private SpanMessagePackFormatter()
        {
        }

        int IMessagePackFormatter<TraceChunkModel>.Serialize(ref byte[] bytes, int offset, TraceChunkModel traceChunk, IFormatterResolver formatterResolver)
        {
            return Serialize(ref bytes, offset, traceChunk, formatterResolver);
        }

        // overload of IMessagePackFormatter<TraceChunkModel>.Serialize() with `in` modifier on `TraceChunkModel` parameter
        public int Serialize(ref byte[] bytes, int offset, in TraceChunkModel traceChunk, IFormatterResolver formatterResolver, int? maxSize = null)
        {
            if (traceChunk.SpanCount > 0)
            {
                var spanModel = traceChunk.GetSpanModel(0);
                traceChunk.Tags?.FixTraceIdTag(spanModel.Span.TraceId128);
            }

            int originalOffset = offset;

            // start writing span[]
            offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, traceChunk.SpanCount);

            // serialize each span
            for (var i = 0; i < traceChunk.SpanCount; i++)
            {
                if (maxSize != null && offset - originalOffset >= maxSize)
                {
                    // We've already reached the maximum size, give up
                    return 0;
                }

                // when serializing each span, we need additional information that is not
                // available in the span object itself, like its position in the trace chunk
                // or if its parent can also be found in the same chunk, so we use SpanModel
                // to pass that information to the serializer
                var spanModel = traceChunk.GetSpanModel(i);
                offset += Serialize(ref bytes, offset, in spanModel);
            }

            return offset - originalOffset;
        }

        private int Serialize(ref byte[] bytes, int offset, in SpanModel spanModel)
        {
            var span = spanModel.Span;

            // First, pack array length (or map length).
            // It should be the number of members of the object to be serialized.
            var len = 8;

            if (span.Context.ParentIdInternal > 0)
            {
                len++;
            }

            if (span.Error)
            {
                len++;
            }

            var hasMetaStruct = span.Tags.HasMetaStruct();
            if (hasMetaStruct)
            {
                len++;
            }

            var hasSpanLinks = span.SpanLinks is { Count: > 0 };
            if (hasSpanLinks)
            {
                len++;
            }

            len += 2; // Tags and metrics

            int originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

            // trace_id field is 64-bits, truncate by using TraceId128.Lower
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, span.Context.TraceId128.Lower);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spanIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, span.Context.SpanId);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _nameBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.OperationName);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _resourceBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.ResourceName);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _serviceBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.ServiceName);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _typeBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.Type);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _startBytes);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, span.StartTime.ToUnixTimeNanoseconds());

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _durationBytes);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, span.Duration.ToNanoseconds());

            if (span.Context.ParentIdInternal > 0)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _parentIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, (ulong)span.Context.ParentIdInternal);
            }

            if (span.Error)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _errorBytes);
                offset += MessagePackBinary.WriteByte(ref bytes, offset, 1);
            }

            ITagProcessor[] tagProcessors = null;
            if (span.Context.TraceContext?.Tracer is Tracer tracer)
            {
                tagProcessors = tracer.TracerManager?.TagProcessors;
            }

            offset += WriteTags(ref bytes, offset, in spanModel, tagProcessors);
            offset += WriteMetrics(ref bytes, offset, in spanModel, tagProcessors);

            if (hasMetaStruct)
            {
                offset += WriteMetaStruct(ref bytes, offset, in spanModel);
            }

            if (hasSpanLinks)
            {
                offset += WriteSpanLink(ref bytes, offset, in spanModel);
            }

            return offset - originalOffset;
        }

        private int WriteSpanLink(ref byte[] bytes, int offset, in SpanModel spanModel)
        {
            int originalOffset = offset;

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spanLinkBytes);
            offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, spanModel.Span.SpanLinks.Count);

            foreach (var spanLink in spanModel.Span.SpanLinks)
            {
                var context = spanLink.Context;
                var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority;

                var traceFlags = samplingPriority switch
                {
                    null => 0u,             // not set
                    > 0 => 1u + (1u << 31), // keep
                    <= 0 => 1u << 31,       // drop
                };

                var len = 3;

                // check to serialize tracestate
                if (context.IsRemote)
                {
                    len++;
                }

                if (traceFlags > 0)
                {
                    len++;
                }

                var hasAttributes = spanLink.Attributes is { Count: > 0 };
                if (hasAttributes)
                {
                    len++;
                }

                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);
                // individual key-value pairs - traceid - lower
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.TraceId128.Lower);
                // individual key-value pairs - traceid - higher
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceIdHighBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.TraceId128.Upper);
                // spanid
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spanIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.SpanId);
                // optional serialization
                if (hasAttributes)
                {
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _attributesBytes);
                    offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, spanLink.Attributes.Count);
                    foreach (var attribute in spanLink.Attributes)
                    {
                        offset += MessagePackBinary.WriteString(ref bytes, offset, attribute.Key);
                        offset += MessagePackBinary.WriteString(ref bytes, offset, attribute.Value);
                    }
                }

                // CreateTraceStateHeader will never return null or empty
                if (context.IsRemote)
                {
                    var traceState = W3CTraceContextPropagator.CreateTraceStateHeader(context);
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceStateBytes);
                    offset += MessagePackBinary.WriteString(ref bytes, offset, traceState);
                }

                if (traceFlags > 0)
                {
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceFlagBytes);
                    offset += MessagePackBinary.WriteUInt32(ref bytes, offset, traceFlags);
                }
            }

            return offset - originalOffset;
        }

        private int WriteMetaStruct(ref byte[] bytes, int offset, in SpanModel model)
        {
            int originalOffset = offset;
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metaStructBytes);

            // We don't know the final count yet, depending on it, a different amount of bytes will be used for the header
            // of the dictionary, so we need a temporary buffer

            var temporaryBytes = new byte[256];
            var tagWriter = new TagWriter(this, null, temporaryBytes, 0);
            model.Span.Tags.EnumerateMetaStruct(ref tagWriter);
            temporaryBytes = tagWriter.Bytes;
            Array.Resize(ref temporaryBytes, tagWriter.Offset);

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, tagWriter.Count);
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, temporaryBytes);

            return offset - originalOffset;
        }

        private void WriteMetaStruct(ref byte[] bytes, ref int offset, string key, byte[] value)
        {
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, StringEncoding.UTF8.GetBytes(key));
            offset += MessagePackBinary.WriteBytes(ref bytes, offset, value);
        }

        // TAGS

        private int WriteTags(ref byte[] bytes, int offset, in SpanModel model, ITagProcessor[] tagProcessors)
        {
            var span = model.Span;
            int originalOffset = offset;

            // Start of "meta" dictionary. Do not add any string tags before this line.
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metaBytes);

            int count = 0;

            // We don't know the final count yet, write a fixed-size header and note the offset
            var countOffset = offset;
            offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0);

            // Write span tags
            var tagWriter = new TagWriter(this, tagProcessors, bytes, offset);
            span.Tags.EnumerateTags(ref tagWriter);
            bytes = tagWriter.Bytes;
            offset = tagWriter.Offset;
            count += tagWriter.Count;

            // Write trace tags
            if (model is { TraceChunk.Tags: { Count: > 0 } traceTags })
            {
                var traceTagWriter = new TraceTagWriter(
                    this,
                    tagProcessors,
                    isLocalRoot: model.IsLocalRoot,
                    isChunkOrphan: model.IsChunkOrphan,
                    isFirstSpanInChunk: model.IsFirstSpanInChunk,
                    bytes,
                    offset);

                traceTags.Enumerate(ref traceTagWriter);
                bytes = traceTagWriter.Bytes;
                offset = traceTagWriter.Offset;
                count += traceTagWriter.Count;
            }

            if (!string.IsNullOrEmpty(span.Context.LastParentId))
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _lastParentIdBytes);
                offset += MessagePackBinary.WriteString(ref bytes, offset, span.Context.LastParentId);
            }

            // add "runtime-id" tag to service-entry (aka top-level) spans
            if (span.IsTopLevel && (!Ci.CIVisibility.IsRunning || !Ci.CIVisibility.Settings.Agentless))
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdNameBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdValueBytes);
            }

            // add "_dd.origin" tag to all spans
            var originRawBytes = MessagePackStringCache.GetOriginBytes(model.TraceChunk.Origin);

            if (originRawBytes is not null)
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _originNameBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, originRawBytes);
            }

            // add "env" to all spans
            var envRawBytes = MessagePackStringCache.GetEnvironmentBytes(model.TraceChunk.Environment);

            if (envRawBytes is not null)
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentNameBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, envRawBytes);
            }

            // add "language=dotnet" tag to all spans
            count++;
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageValueBytes);

            // add "version" tags to all spans whose service name is the default service name
            if (string.Equals(span.Context.ServiceNameInternal, model.TraceChunk.DefaultServiceName, StringComparison.OrdinalIgnoreCase))
            {
                var versionRawBytes = MessagePackStringCache.GetVersionBytes(model.TraceChunk.ServiceVersion);

                if (versionRawBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _versionNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, versionRawBytes);
                }
            }

            // SCI tags will be sent only once per trace chunk
            if (model.IsFirstSpanInChunk)
            {
                var gitCommitShaRawBytes = MessagePackStringCache.GetGitCommitShaBytes(model.TraceChunk.GitCommitSha);
                if (gitCommitShaRawBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _gitCommitShaNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, gitCommitShaRawBytes);
                }

                var gitRepositoryUrlRawBytes = MessagePackStringCache.GetGitRepositoryUrlBytes(model.TraceChunk.GitRepositoryUrl);
                if (gitRepositoryUrlRawBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _gitRepositoryUrlNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, gitRepositoryUrlRawBytes);
                }
            }

            // AAS tags need to be set on any span for the backend to properly handle the billing.
            // That said, it's more intuitive to find it on the local root for the customer.
            if (model.TraceChunk.IsRunningInAzureAppService && model.TraceChunk.AzureAppServiceSettings is { } azureAppServiceSettings)
            {
                // Done here to avoid initializing in most cases
                InitializeAasTags();
                byte[] tagBytes;

                if (model.IsLocalRoot || model.IsChunkOrphan)
                {
                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesSiteKind, azureAppServiceSettings.SiteKind);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasSiteKindTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesResourceGroup, azureAppServiceSettings.ResourceGroup);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasResourceGroupTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesSubscriptionId, azureAppServiceSettings.SubscriptionId);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasSubscriptionIdTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesResourceId, azureAppServiceSettings.ResourceId);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasResourceIdTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesInstanceId, azureAppServiceSettings.InstanceId);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasInstanceIdTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesInstanceName, azureAppServiceSettings.InstanceName);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasInstanceNameTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesOperatingSystem, azureAppServiceSettings.OperatingSystem);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasOperatingSystemTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesRuntime, azureAppServiceSettings.Runtime);

                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasRuntimeTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesExtensionVersion, azureAppServiceSettings.SiteExtensionVersion);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasExtensionVersionTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }
                }

                tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesSiteName, azureAppServiceSettings.SiteName);
                // the front-end identify AAS spans using aas.site.name and aas.site.type, so we need them on all spans
                if (tagBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasSiteNameTagNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                }

                tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesSiteType, azureAppServiceSettings.SiteType);
                if (tagBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _aasSiteTypeTagNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                }
            }

            if (count > 0)
            {
                // Back-patch the count. End of "meta" dictionary. Do not add any string tags after this line.
                MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, countOffset, (uint)count);
            }

            return offset - originalOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteTag(ref byte[] bytes, ref int offset, string key, string value, ITagProcessor[] tagProcessors)
        {
            if (tagProcessors is not null)
            {
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors[i]?.ProcessMeta(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteString(ref bytes, offset, key);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteTag(ref byte[] bytes, ref int offset, ReadOnlySpan<byte> keyBytes, string value, ITagProcessor[] tagProcessors)
        {
            if (tagProcessors is not null)
            {
                string key = null;
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors[i]?.ProcessMeta(ref key, ref value);
                }
            }

            MessagePackBinary.EnsureCapacity(ref bytes, offset, keyBytes.Length + StringEncoding.UTF8.GetMaxByteCount(value.Length) + 5);
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, keyBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value);
        }

        // METRICS

        private int WriteMetrics(ref byte[] bytes, int offset, in SpanModel model, ITagProcessor[] tagProcessors)
        {
            var span = model.Span;
            int originalOffset = offset;

            // Start of "metrics" dictionary. Do not add any numeric tags before this line.
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metricsBytes);

            int count = 0;

            // We don't know the final count yet, write a fixed-size header and note the offset
            var countOffset = offset;
            offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0);

            // Write span metrics
            var tagWriter = new TagWriter(this, tagProcessors, bytes, offset);
            span.Tags.EnumerateMetrics(ref tagWriter);
            bytes = tagWriter.Bytes;
            offset = tagWriter.Offset;
            count += tagWriter.Count;

            // add "process_id" tag to local root span (if present)
            var processId = DomainMetadata.Instance.ProcessId;

            if (processId != 0 && model.IsLocalRoot)
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _processIdNameBytes);
                offset += MessagePackBinary.WriteDouble(ref bytes, offset, processId);
            }

            // add the "apm.enabled" tag with a value of 0
            // to the first span in the chunk when APM is disabled
            if (!model.TraceChunk.IsApmEnabled && model.IsLocalRoot)
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _apmEnabledBytes);
                offset += MessagePackBinary.WriteDouble(ref bytes, offset, 0);
            }

            // add "_sampling_priority_v1" tag to all "chunk orphans"
            // (spans whose parents are not found in the same chunk)
            if (model.IsChunkOrphan && model.TraceChunk.SamplingPriority is { } samplingPriority)
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _samplingPriorityNameBytes);

                // sampling priority must be serialized as msgpack float64 (Double in .NET).
                offset += MessagePackBinary.WriteDouble(ref bytes, offset, samplingPriority);
            }

            if (span.IsTopLevel && (!Ci.CIVisibility.IsRunning || !Ci.CIVisibility.Settings.Agentless))
            {
                count++;
                WriteMetric(ref bytes, ref offset, Trace.Metrics.TopLevelSpan, 1.0, tagProcessors);
            }

            if (count > 0)
            {
                // Back-patch the count. End of "metrics" dictionary. Do not add any numeric tags after this line.
                MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, countOffset, (uint)count);
            }

            return offset - originalOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteMetric(ref byte[] bytes, ref int offset, string key, double value, ITagProcessor[] tagProcessors)
        {
            if (tagProcessors is not null)
            {
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors[i]?.ProcessMetric(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteString(ref bytes, offset, key);
            offset += MessagePackBinary.WriteDouble(ref bytes, offset, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteMetric(ref byte[] bytes, ref int offset, ReadOnlySpan<byte> keyBytes, double value, ITagProcessor[] tagProcessors)
        {
            if (tagProcessors is not null)
            {
                string key = null;
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors[i]?.ProcessMetric(ref key, ref value);
                }
            }

            MessagePackBinary.EnsureCapacity(ref bytes, offset, keyBytes.Length + 9);
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, keyBytes);
            offset += MessagePackBinary.WriteDouble(ref bytes, offset, value);
        }

        TraceChunkModel IMessagePackFormatter<TraceChunkModel>.Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotSupportedException($"{nameof(SpanMessagePackFormatter)} does not support deserialization. For testing purposes, deserialize using the MessagePack NuGet package.");
        }

        private void InitializeAasTags()
        {
            if (_aasSiteNameTagNameBytes == null)
            {
                // AAS Tags are all computed from environment variables, they shouldn't change during the life of the process
                _aasSiteNameTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesSiteName);
                _aasSiteKindTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesSiteKind);
                _aasSiteTypeTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesSiteType);
                _aasResourceGroupTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesResourceGroup);
                _aasSubscriptionIdTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesSubscriptionId);
                _aasResourceIdTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesResourceId);
                _aasInstanceIdTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesInstanceId);
                _aasInstanceNameTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesInstanceName);
                _aasOperatingSystemTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesOperatingSystem);
                _aasRuntimeTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesRuntime);
                _aasExtensionVersionTagNameBytes = StringEncoding.UTF8.GetBytes(Datadog.Trace.Tags.AzureAppServicesExtensionVersion);
            }
        }

        internal struct TagWriter : IItemProcessor<string>, IItemProcessor<double>, IItemProcessor<byte[]>
        {
            private readonly SpanMessagePackFormatter _formatter;
            private readonly ITagProcessor[] _tagProcessors;

            public byte[] Bytes;
            public int Offset;
            public int Count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal TagWriter(SpanMessagePackFormatter formatter, ITagProcessor[] tagProcessors, byte[] bytes, int offset)
            {
                _formatter = formatter;
                _tagProcessors = tagProcessors;
                Bytes = bytes;
                Offset = offset;
                Count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Process(TagItem<string> item)
            {
                if (item.SerializedKey.IsEmpty)
                {
                    _formatter.WriteTag(ref Bytes, ref Offset, item.Key, item.Value, _tagProcessors);
                }
                else
                {
                    _formatter.WriteTag(ref Bytes, ref Offset, item.SerializedKey, item.Value, _tagProcessors);
                }

                Count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Process(TagItem<double> item)
            {
                if (item.SerializedKey.IsEmpty)
                {
                    _formatter.WriteMetric(ref Bytes, ref Offset, item.Key, item.Value, _tagProcessors);
                }
                else
                {
                    _formatter.WriteMetric(ref Bytes, ref Offset, item.SerializedKey, item.Value, _tagProcessors);
                }

                Count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Process(TagItem<byte[]> item)
            {
                _formatter.WriteMetaStruct(ref Bytes, ref Offset, item.Key, item.Value);
                Count++;
            }
        }

        internal struct TraceTagWriter : TraceTagCollection.ITagEnumerator
        {
            private readonly SpanMessagePackFormatter _formatter;
            private readonly ITagProcessor[] _tagProcessors;
            private readonly bool _isLocalRoot;
            private readonly bool _isChunkOrphan;
            private readonly bool _isFirstSpanInChunk;

            public byte[] Bytes;
            public int Offset;
            public int Count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal TraceTagWriter(
                SpanMessagePackFormatter formatter,
                ITagProcessor[] tagProcessors,
                bool isLocalRoot,
                bool isChunkOrphan,
                bool isFirstSpanInChunk,
                byte[] bytes,
                int offset)
            {
                _formatter = formatter;
                _tagProcessors = tagProcessors;
                _isLocalRoot = isLocalRoot;
                _isChunkOrphan = isChunkOrphan;
                _isFirstSpanInChunk = isFirstSpanInChunk;
                Bytes = bytes;
                Offset = offset;
                Count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Next(KeyValuePair<string, string> item)
            {
                var isPropagatedTag = item.Key.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.Ordinal);

                // add propagated trace tags to the first span in every chunk,
                // add non-propagated trace tags to the root span
                if ((isPropagatedTag && _isFirstSpanInChunk) || (!isPropagatedTag && _isLocalRoot))
                {
                    _formatter.WriteTag(ref Bytes, ref Offset, item.Key, item.Value, _tagProcessors);
                    Count++;
                }
            }
        }
    }
}
