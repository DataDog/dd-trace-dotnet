// <copyright file="SpanMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Processors;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent.MessagePack
{
    internal sealed class SpanMessagePackFormatter : IMessagePackFormatter<TraceChunkModel>
    {
        public static readonly SpanMessagePackFormatter Instance = new();

        // MessagePack-encoded bytes for string constants are now generated at compile-time
        // by the MessagePackConstantsGenerator source generator and accessed via MessagePackConstants.
        // These bytes include the MessagePack string prefix, so use WriteRaw() not WriteStringBytes().
        //
        // Only instance fields that remain are for values determined at runtime (e.g., Tracer.RuntimeId,
        // Azure App Service environment variables, and dynamically cached values).

        // Runtime values (determined at process startup)
        private readonly byte[] _runtimeIdValueBytes = MessagePackSerializer.Serialize(Tracer.RuntimeId);
        private readonly Dictionary<string, byte[]> _wafRuleFileVersionValues = new();

        // Azure App Service tag value bytes (initialized lazily from environment variables, cached for process lifetime)
        private byte[] _aasSiteNameValueBytes;
        private byte[] _aasSiteKindValueBytes;
        private byte[] _aasSiteTypeValueBytes;
        private byte[] _aasResourceGroupValueBytes;
        private byte[] _aasSubscriptionIdValueBytes;
        private byte[] _aasResourceIdValueBytes;
        private byte[] _aasInstanceIdValueBytes;
        private byte[] _aasInstanceNameValueBytes;
        private byte[] _aasOperatingSystemValueBytes;
        private byte[] _aasRuntimeValueBytes;
        private byte[] _aasExtensionVersionValueBytes;

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

            if (span.Context.ParentId > 0)
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

            var hasSpanEvents = span.SpanEvents is { Count: > 0 };
            var nativeSpanEventsEnabled = (span.Context.TraceContext?.Tracer as Tracer)?.TracerManager?.SpanEventsManager?.NativeSpanEventsEnabled;
            var hasNativeSpanEvents = hasSpanEvents && nativeSpanEventsEnabled == true;
            var hasMetaSpanEvents = hasSpanEvents && nativeSpanEventsEnabled == false;

            if (hasNativeSpanEvents)
            {
                len++;
            }

            len += 2; // Tags and metrics

            int originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

            // trace_id field is 64-bits, truncate by using TraceId128.Lower
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TraceIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, span.Context.TraceId128.Lower);

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.SpanIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, span.Context.SpanId);

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.NameBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.OperationName);

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ResourceBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.ResourceName);

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ServiceBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.ServiceName);

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TypeBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.Type);

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.StartBytes);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, span.StartTime.ToUnixTimeNanoseconds());

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.DurationBytes);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, span.Duration.ToNanoseconds());

            if (span.Context.ParentId > 0)
            {
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ParentIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, (ulong)span.Context.ParentId);
            }

            if (span.Error)
            {
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ErrorBytes);
                offset += MessagePackBinary.WriteByte(ref bytes, offset, 1);
            }

            ITagProcessor[] tagProcessors = null;
            if (span.Context.TraceContext?.Tracer is Tracer tracer)
            {
                tagProcessors = tracer.TracerManager?.TagProcessors;
            }

            offset += WriteTags(ref bytes, offset, in spanModel, tagProcessors, hasMetaSpanEvents);
            offset += WriteMetrics(ref bytes, offset, in spanModel, tagProcessors);

            if (hasMetaStruct)
            {
                offset += WriteMetaStruct(ref bytes, offset, in spanModel);
            }

            if (hasSpanLinks)
            {
                offset += WriteSpanLink(ref bytes, offset, in spanModel);
            }

            if (hasNativeSpanEvents)
            {
                offset += WriteSpanEvent(ref bytes, offset, in spanModel);
            }

            return offset - originalOffset;
        }

        private int WriteSpanLink(ref byte[] bytes, int offset, in SpanModel spanModel)
        {
            int originalOffset = offset;

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.SpanLinksBytes);
            offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, spanModel.Span.SpanLinks.Count);

            foreach (var spanLink in spanModel.Span.SpanLinks)
            {
                var context = spanLink.Context;
                var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority;

                var traceFlags = samplingPriority switch
                {
                    null => 0u, // not set
                    > 0 => 1u + (1u << 31), // keep
                    <= 0 => 1u << 31, // drop
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
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TraceIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.TraceId128.Lower);
                // individual key-value pairs - traceid - higher
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TraceIdHighBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.TraceId128.Upper);
                // spanid
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.SpanIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.SpanId);
                // optional serialization
                if (hasAttributes)
                {
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AttributesBytes);
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
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TraceStateBytes);
                    offset += MessagePackBinary.WriteString(ref bytes, offset, traceState);
                }

                if (traceFlags > 0)
                {
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TraceFlagsBytes);
                    offset += MessagePackBinary.WriteUInt32(ref bytes, offset, traceFlags);
                }
            }

            return offset - originalOffset;
        }

        private int WriteSpanEvent(ref byte[] bytes, int offset, in SpanModel spanModel)
        {
            int originalOffset = offset;

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.SpanEventsBytes);
            offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, spanModel.Span.SpanEvents.Count);

            foreach (var spanEvent in spanModel.Span.SpanEvents)
            {
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, spanEvent.Attributes?.Count > 0 ? 3 : 2);

                // time_unix_nano
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TimeUnixNanoBytes);
                offset += MessagePackBinary.WriteInt64(ref bytes, offset, spanEvent.Timestamp.ToUnixTimeNanoseconds());

                // name
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.NameBytes);
                offset += MessagePackBinary.WriteString(ref bytes, offset, spanEvent.Name);

                // attributes (strings only)
                if (spanEvent.Attributes?.Count > 0)
                {
                    // Reserve space to patch the correct map header count later
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AttributesBytes);
                    int attributeCountOffset = offset;
                    offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0); // placeholder

                    int attrCount = 0;
                    foreach (var attribute in spanEvent.Attributes)
                    {
                        if (string.IsNullOrEmpty(attribute.Key) || !SpanEventConverter.IsAllowedType(attribute.Value))
                        {
                            continue;
                        }

                        offset += MessagePackBinary.WriteString(ref bytes, offset, attribute.Key);
                        offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 2);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TypeFieldBytes);

                        if (attribute.Value is not Array)
                        {
                            offset += WriteEventAttribute(ref bytes, offset, attribute.Value);
                            attrCount++;
                        }
                        else if (attribute.Value is Array arrayVal)
                        {
                            offset += MessagePackBinary.WriteInt32(ref bytes, offset, 4);
                            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ArrayValueFieldBytes);
                            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
                            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ValuesFieldBytes);
                            offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, arrayVal.Length);

                            foreach (var item in arrayVal)
                            {
                                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 2);
                                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TypeFieldBytes);
                                offset += WriteEventAttribute(ref bytes, offset, item);
                            }

                            attrCount++;
                        }
                    }

                    if (attrCount > 0)
                    {
                        MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, attributeCountOffset, (uint)attrCount);
                    }
                    else
                    {
                        offset = attributeCountOffset - MessagePackConstants.AttributesBytes.Length;
                    }
                }
            }

            return offset - originalOffset;
        }

        private int WriteEventAttribute(ref byte[] bytes, int offset, object value)
        {
            var originalOffset = offset;

            switch (value)
            {
                case string stringVal:
                case char charVal:
                    offset += MessagePackBinary.WriteInt32(ref bytes, offset, 0);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.StringValueFieldBytes);
                    offset += MessagePackBinary.WriteString(ref bytes, offset, value.ToString());
                    break;

                case bool boolVal:
                    offset += MessagePackBinary.WriteInt32(ref bytes, offset, 1);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.BoolValueFieldBytes);
                    offset += MessagePackBinary.WriteBoolean(ref bytes, offset, boolVal);
                    break;

                case sbyte or byte or short or ushort or int or uint or long:
                    offset += MessagePackBinary.WriteInt32(ref bytes, offset, 2);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.IntValueFieldBytes);
                    offset += MessagePackBinary.WriteInt64(ref bytes, offset, Convert.ToInt64(value));
                    break;

                case float or double:
                    offset += MessagePackBinary.WriteInt32(ref bytes, offset, 3);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.DoubleValueFieldBytes);
                    offset += MessagePackBinary.WriteDouble(ref bytes, offset, Convert.ToDouble(value));
                    break;
            }

            return offset - originalOffset;
        }

        private int WriteJsonEvents(ref byte[] bytes, int offset, in SpanModel spanModel)
        {
            int originalOffset = offset;

            var settings = new JsonSerializerSettings { Converters = new List<JsonConverter> { new SpanEventConverter() }, Formatting = Formatting.None };
            var eventsJson = JsonConvert.SerializeObject(spanModel.Span.SpanEvents, settings);

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.EventsBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, eventsJson);

            return offset - originalOffset;
        }

        private int WriteMetaStruct(ref byte[] bytes, int offset, in SpanModel model)
        {
            int originalOffset = offset;
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.MetaStructBytes);

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

        private int WriteTags(ref byte[] bytes, int offset, in SpanModel model, ITagProcessor[] tagProcessors, bool hasMetaSpanEvents)
        {
            var span = model.Span;
            int originalOffset = offset;

            // Start of "meta" dictionary. Do not add any string tags before this line.
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.MetaBytes);

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

            if (hasMetaSpanEvents)
            {
                count++;
                offset += WriteJsonEvents(ref bytes, offset, in model);
            }

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
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.LastParentIdBytes);
                offset += MessagePackBinary.WriteString(ref bytes, offset, span.Context.LastParentId);
            }

            // add "runtime-id" tag to service-entry (aka top-level) spans
            var testOptimization = Ci.TestOptimization.Instance;
            if (span.IsTopLevel && (!testOptimization.IsRunning || !testOptimization.Settings.Agentless))
            {
                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.RuntimeIdBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, _runtimeIdValueBytes);
            }

            // add "_dd.origin" tag to all spans
            var originRawBytes = MessagePackStringCache.GetOriginBytes(model.TraceChunk.Origin);

            if (originRawBytes is not null)
            {
                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.OriginBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, originRawBytes);
            }

            // add "env" to all spans
            var envRawBytes = MessagePackStringCache.GetEnvironmentBytes(model.TraceChunk.Environment);

            if (envRawBytes is not null)
            {
                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.EnvBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, envRawBytes);
            }

            // add "language=dotnet" tag to all spans
            count++;
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.LanguageBytes);
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.DotnetLanguageValueBytes);

            // add "version" tags to all spans whose service name is the default service name
            var serviceNameEqualsDefault = string.Equals(span.Context.ServiceName, model.TraceChunk.DefaultServiceName, StringComparison.OrdinalIgnoreCase);
            if (serviceNameEqualsDefault)
            {
                var versionRawBytes = MessagePackStringCache.GetVersionBytes(model.TraceChunk.ServiceVersion);

                if (versionRawBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.VersionBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, versionRawBytes);
                }
            }

            // add _dd.base_service tag to spans where the service name has been overridden
            if (!serviceNameEqualsDefault && !string.IsNullOrEmpty(model.TraceChunk.DefaultServiceName))
            {
                var serviceNameRawBytes = MessagePackStringCache.GetServiceBytes(model.TraceChunk.DefaultServiceName);

                if (serviceNameRawBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.BaseServiceBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, serviceNameRawBytes);
                }
            }

            // Process tags will be sent only once per buffer/payload (one payload can contain many chunks from different traces)
            if (model.IsFirstSpanInChunk && model.TraceChunk.IsFirstChunkInPayload && model.TraceChunk.ProcessTags is not null)
            {
                var processTagsRawBytes = MessagePackStringCache.GetProcessTagsBytes(model.TraceChunk.ProcessTags?.SerializedTags);

                if (processTagsRawBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ProcessTagsBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, processTagsRawBytes);
                }
            }

            // SCI tags will be sent only once per trace
            if (model.IsFirstSpanInChunk)
            {
                var gitCommitShaBytes = MessagePackStringCache.GetGitCommitShaBytes(model.TraceChunk.GitCommitSha);
                if (gitCommitShaBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.GitCommitShaBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, gitCommitShaBytes);
                }

                var gitRepositoryUrlBytes = MessagePackStringCache.GetGitRepositoryUrlBytes(model.TraceChunk.GitRepositoryUrl);
                if (gitRepositoryUrlBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.GitRepositoryUrlBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, gitRepositoryUrlBytes);
                }
            }

            if (Security.Instance.AppsecEnabled && model.IsLocalRoot && span.Context.TraceContext?.WafExecuted is true)
            {
                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.RuntimeFamilyBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.DotnetLanguageValueBytes);

                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AppSecRuleFileVersionBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, GetAppSecRulesetVersion(Security.Instance.WafRuleFileVersion));
            }

            // AAS tags need to be set on any span for the backend to properly handle the billing.
            // That said, it's more intuitive to find it on the local root for the customer.
            if (model.TraceChunk.IsRunningInAzureAppService && model.TraceChunk.AzureAppServiceSettings is { } azureAppServiceSettings)
            {
                // Done here to avoid initializing in most cases
                InitializeAasTags(azureAppServiceSettings);

                if (model.IsLocalRoot || model.IsChunkOrphan)
                {
                    if (_aasSiteKindValueBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesSiteKindBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasSiteKindValueBytes);
                    }

                    if (_aasResourceGroupValueBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesResourceGroupBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasResourceGroupValueBytes);
                    }

                    if (_aasSubscriptionIdValueBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesSubscriptionIdBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasSubscriptionIdValueBytes);
                    }

                    if (_aasResourceIdValueBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesResourceIdBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasResourceIdValueBytes);
                    }

                    if (_aasInstanceIdValueBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesInstanceIdBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasInstanceIdValueBytes);
                    }

                    if (_aasInstanceNameValueBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesInstanceNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasInstanceNameValueBytes);
                    }

                    if (_aasOperatingSystemValueBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesOperatingSystemBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasOperatingSystemValueBytes);
                    }

                    if (_aasRuntimeValueBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesRuntimeBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasRuntimeValueBytes);
                    }

                    if (_aasExtensionVersionValueBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesExtensionVersionBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasExtensionVersionValueBytes);
                    }
                }

                // the front-end identify AAS spans using aas.site.name and aas.site.type, so we need them on all spans
                if (_aasSiteNameValueBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesSiteNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasSiteNameValueBytes);
                }

                if (_aasSiteTypeValueBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AzureAppServicesSiteTypeBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, _aasSiteTypeValueBytes);
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
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.MetricsBytes);

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

            if (model.IsLocalRoot)
            {
                // add process id
                var processId = DomainMetadata.Instance.ProcessId;

                if (processId != 0)
                {
                    count++;
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ProcessIdBytes); // "process_id"
                    offset += MessagePackBinary.WriteDouble(ref bytes, offset, processId);
                }

                // add agent or rule sampling rate
                if (model.TraceChunk is { AppliedSamplingRate: { } samplingRate, SamplingMechanism: { } samplingMechanism })
                {
                    // Use the appropriate tag name based on sampling mechanism
                    switch (samplingMechanism)
                    {
                        case SamplingMechanism.AgentRate:
                            count++;
                            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.SamplingAgentDecisionBytes); // "_dd.agent_psr"
                            offset += MessagePackBinary.WriteDouble(ref bytes, offset, samplingRate);
                            break;

                        case SamplingMechanism.LocalTraceSamplingRule:
                        case SamplingMechanism.RemoteAdaptiveSamplingRule:
                        case SamplingMechanism.RemoteUserSamplingRule:
                            count++;
                            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.SamplingRuleDecisionBytes); // "_dd.rule_psr"
                            offset += MessagePackBinary.WriteDouble(ref bytes, offset, samplingRate);
                            break;
                    }
                }

                // add rate limiter rate
                if (model.TraceChunk.RateLimiterRate is { } limitSamplingRate)
                {
                    count++;
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.SamplingLimitDecisionBytes); // "_dd.limit_psr"
                    offset += MessagePackBinary.WriteDouble(ref bytes, offset, limitSamplingRate);
                }

                // add keep rate
                if (model.TraceChunk.TracesKeepRate is { } keepRate)
                {
                    count++;
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TracesKeepRateBytes); // "_dd.tracer_kr"
                    offset += MessagePackBinary.WriteDouble(ref bytes, offset, keepRate);
                }
            }

            // add the "apm.enabled" tag with a value of 0
            // to the first span in the chunk when APM is disabled
            if (!model.TraceChunk.IsApmEnabled && model.IsLocalRoot)
            {
                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ApmEnabledBytes);
                offset += MessagePackBinary.WriteDouble(ref bytes, offset, 0);
            }

            if (Security.Instance.AppsecEnabled && model.IsLocalRoot && span.Context.TraceContext?.WafExecuted is true)
            {
                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.AppSecEnabledBytes);
                offset += MessagePackBinary.WriteDouble(ref bytes, offset, 1.0);
            }

            // add "_sampling_priority_v1" tag to all "chunk orphans"
            // (spans whose parents are not found in the same chunk)
            if (model is { IsChunkOrphan: true, TraceChunk.SamplingPriority: { } samplingPriority })
            {
                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.SamplingPriorityBytes);
                offset += MessagePackBinary.WriteDouble(ref bytes, offset, samplingPriority);
            }

            // add "_dd.top_level" to top-level spans (aka service-entry spans)
            var testOptimization = Ci.TestOptimization.Instance;
            if (span.IsTopLevel && (!testOptimization.IsRunning || !testOptimization.Settings.Agentless))
            {
                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TopLevelSpanBytes); // "_dd.top_level"
                offset += MessagePackBinary.WriteDouble(ref bytes, offset, 1);
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

        private static byte[] SerializeIfNotNullOrWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : MessagePackSerializer.Serialize(value);
        }

        private byte[] GetAppSecRulesetVersion(string version)
        {
            if (_wafRuleFileVersionValues.TryGetValue(version, out byte[] bytes))
            {
                return bytes;
            }
            else
            {
                bytes = StringEncoding.UTF8.GetBytes(version);
                _wafRuleFileVersionValues.Add(version, bytes);
                return bytes;
            }
        }

        private void InitializeAasTags(ImmutableAzureAppServiceSettings azureAppServiceSettings)
        {
            if (_aasSiteNameValueBytes == null)
            {
                // Cache value bytes (these are constant for the lifetime of the process)
                _aasSiteNameValueBytes = SerializeIfNotNullOrWhiteSpace(azureAppServiceSettings.SiteName);
                _aasSiteKindValueBytes = SerializeIfNotNullOrWhiteSpace(azureAppServiceSettings.SiteKind);
                _aasSiteTypeValueBytes = SerializeIfNotNullOrWhiteSpace(azureAppServiceSettings.SiteType);
                _aasResourceGroupValueBytes = SerializeIfNotNullOrWhiteSpace(azureAppServiceSettings.ResourceGroup);
                _aasSubscriptionIdValueBytes = SerializeIfNotNullOrWhiteSpace(azureAppServiceSettings.SubscriptionId);
                _aasResourceIdValueBytes = SerializeIfNotNullOrWhiteSpace(azureAppServiceSettings.ResourceId);
                _aasInstanceIdValueBytes = SerializeIfNotNullOrWhiteSpace(azureAppServiceSettings.InstanceId);
                _aasInstanceNameValueBytes = SerializeIfNotNullOrWhiteSpace(azureAppServiceSettings.InstanceName);
                _aasOperatingSystemValueBytes = SerializeIfNotNullOrWhiteSpace(azureAppServiceSettings.OperatingSystem);
                _aasRuntimeValueBytes = SerializeIfNotNullOrWhiteSpace(FrameworkDescription.Instance.Name);
                _aasExtensionVersionValueBytes = SerializeIfNotNullOrWhiteSpace(azureAppServiceSettings.SiteExtensionVersion);
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
