// <copyright file="SpanMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Processors;
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

#if NETCOREAPP
        // top-level span fields
        private ReadOnlySpan<byte> TraceIdBytes => "trace_id"u8;

        private ReadOnlySpan<byte> SpanIdBytes => "span_id"u8;

        private ReadOnlySpan<byte> NameBytes => "name"u8;

        private ReadOnlySpan<byte> ResourceBytes => "resource"u8;

        private ReadOnlySpan<byte> ServiceBytes => "service"u8;

        private ReadOnlySpan<byte> TypeBytes => "type"u8;

        private ReadOnlySpan<byte> StartBytes => "start"u8;

        private ReadOnlySpan<byte> DurationBytes => "duration"u8;

        private ReadOnlySpan<byte> ParentIdBytes => "parent_id"u8;

        private ReadOnlySpan<byte> ErrorBytes => "error"u8;

        // string tags
        private ReadOnlySpan<byte> MetaBytes => "meta"u8;

        // Trace.Tags.Language
        private ReadOnlySpan<byte> LanguageNameBytes => "language"u8;

        // TracerConstants.Language
        private ReadOnlySpan<byte> LanguageValueBytes => "dotnet"u8;

        // Trace.Tags.RuntimeId
        private ReadOnlySpan<byte> RuntimeIdNameBytes => "runtime-id"u8;

        private byte[] RuntimeIdValueBytes { get; } = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);

        private ReadOnlySpan<byte> EnvironmentNameBytes => "env"u8;

        private ReadOnlySpan<byte> GitCommitShaNameBytes => "_dd.git.commit.sha"u8;

        private ReadOnlySpan<byte> GitRepositoryUrlNameBytes => "_dd.git.repository_url"u8;

        private ReadOnlySpan<byte> VersionNameBytes => "version"u8;

        private ReadOnlySpan<byte> OriginNameBytes => "_dd.origin"u8;

        // numeric tags
        private ReadOnlySpan<byte> MetricsBytes => "metrics"u8;

        private ReadOnlySpan<byte> SamplingPriorityNameBytes => "_sampling_priority_v1"u8;

        private ReadOnlySpan<byte> ProcessIdNameBytes => "process_id"u8;
#else
        // top-level span fields
        private byte[] TraceIdBytes { get; } = StringEncoding.UTF8.GetBytes("trace_id");

        private byte[] SpanIdBytes { get; } = StringEncoding.UTF8.GetBytes("span_id");

        private byte[] NameBytes { get; } = StringEncoding.UTF8.GetBytes("name");

        private byte[] ResourceBytes { get; } = StringEncoding.UTF8.GetBytes("resource");

        private byte[] ServiceBytes { get; } = StringEncoding.UTF8.GetBytes("service");

        private byte[] TypeBytes { get; } = StringEncoding.UTF8.GetBytes("type");

        private byte[] StartBytes { get; } = StringEncoding.UTF8.GetBytes("start");

        private byte[] DurationBytes { get; } = StringEncoding.UTF8.GetBytes("duration");

        private byte[] ParentIdBytes { get; } = StringEncoding.UTF8.GetBytes("parent_id");

        private byte[] ErrorBytes { get; } = StringEncoding.UTF8.GetBytes("error");

        // string tags
        private byte[] MetaBytes { get; } = StringEncoding.UTF8.GetBytes("meta");

        private byte[] LanguageNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.Language);

        private byte[] LanguageValueBytes { get; } = StringEncoding.UTF8.GetBytes(TracerConstants.Language);

        private byte[] RuntimeIdNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.RuntimeId);

        private byte[] RuntimeIdValueBytes { get; } = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);

        private byte[] EnvironmentNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.Env);

        private byte[] GitCommitShaNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.GitCommitSha);

        private byte[] GitRepositoryUrlNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.GitRepositoryUrl);

        private byte[] VersionNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.Version);

        private byte[] OriginNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.Origin);

        // numeric tags
        private byte[] MetricsBytes { get; } = StringEncoding.UTF8.GetBytes("metrics");

        private byte[] SamplingPriorityNameBytes { get; } = StringEncoding.UTF8.GetBytes(Metrics.SamplingPriority);

        private byte[] ProcessIdNameBytes { get; } = StringEncoding.UTF8.GetBytes(Metrics.ProcessId);
#endif

        int IMessagePackFormatter<TraceChunkModel>.Serialize(ref byte[] bytes, int offset, TraceChunkModel traceChunk, IFormatterResolver formatterResolver)
        {
            return Serialize(ref bytes, offset, traceChunk, formatterResolver);
        }

        // overload of IMessagePackFormatter<TraceChunkModel>.Serialize() with `in` modifier on `TraceChunkModel` parameter
        public int Serialize(ref byte[] bytes, int offset, in TraceChunkModel traceChunk, IFormatterResolver formatterResolver, int? maxSize = null)
        {
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

            len += 2; // Tags and metrics

            int originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, TraceIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, span.Context.TraceId);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, SpanIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, span.Context.SpanId);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, NameBytes);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, span.OperationName);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, ResourceBytes);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, span.ResourceName);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, ServiceBytes);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, span.ServiceName);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, TypeBytes);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, span.Type);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, StartBytes);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, span.StartTime.ToUnixTimeNanoseconds());

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, DurationBytes);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, span.Duration.ToNanoseconds());

            if (span.Context.ParentId > 0)
            {
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, ParentIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, (ulong)span.Context.ParentId);
            }

            if (span.Error)
            {
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, ErrorBytes);
                offset += MessagePackBinary.WriteByte(ref bytes, offset, 1);
            }

            ITagProcessor[] tagProcessors = null;
            if (span.Context.TraceContext?.Tracer is Tracer tracer)
            {
                tagProcessors = tracer.TracerManager?.TagProcessors;
            }

            offset += WriteTags(ref bytes, offset, in spanModel, tagProcessors);
            offset += WriteMetrics(ref bytes, offset, in spanModel, tagProcessors);

            return offset - originalOffset;
        }

        // TAGS

        private int WriteTags(ref byte[] bytes, int offset, in SpanModel model, ITagProcessor[] tagProcessors)
        {
            var span = model.Span;
            int originalOffset = offset;

            // Start of "meta" dictionary. Do not add any string tags before this line.
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, MetaBytes);

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

            // TODO: for each trace tag, determine if it should be added to the local root,
            // to the first span in the chunk, or to all orphan spans.
            // For now, we add them to the local root which is correct in most cases.
            if (model.IsLocalRoot && model.TraceChunk.Tags?.ToArray() is { Length: > 0 } traceTags)
            {
                count += traceTags.Length;

                foreach (var tag in traceTags)
                {
                    WriteTag(ref bytes, ref offset, tag.Key, tag.Value, tagProcessors);
                }
            }

            // add "runtime-id" tag to service-entry (aka top-level) spans
            if (span.IsTopLevel && (!Ci.CIVisibility.IsRunning || !Ci.CIVisibility.Settings.Agentless))
            {
                count++;
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, RuntimeIdNameBytes);
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, RuntimeIdValueBytes);
            }

            // add "_dd.origin" tag to all spans
            var originRawBytes = MessagePackStringCache.GetOriginBytes(model.TraceChunk.Origin);

            if (originRawBytes is not null)
            {
                count++;
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, OriginNameBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, originRawBytes);
            }

            // add "env" to all spans
            var envRawBytes = MessagePackStringCache.GetEnvironmentBytes(model.TraceChunk.Environment);

            if (envRawBytes is not null)
            {
                count++;
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, EnvironmentNameBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, envRawBytes);
            }

            var gitCommitShaRawBytes = MessagePackStringCache.GetGitCommitShaBytes(model.TraceChunk.GitCommitSha);
            if (gitCommitShaRawBytes is not null)
            {
                count++;
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, GitCommitShaNameBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, gitCommitShaRawBytes);
            }

            var gitRepositoryUrlRawBytes = MessagePackStringCache.GetGitRepositoryUrlBytes(model.TraceChunk.GitRepositoryUrl);
            if (gitRepositoryUrlRawBytes is not null)
            {
                count++;
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, GitRepositoryUrlNameBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, gitRepositoryUrlRawBytes);
            }

            // add "language=dotnet" tag to all spans, except those that
            // represents a downstream service or external dependency
            if (span.Tags is not InstrumentationTags { SpanKind: SpanKinds.Client or SpanKinds.Producer })
            {
                count++;
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, LanguageNameBytes);
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, LanguageValueBytes);
            }

            // add "version" tags to all spans whose service name is the default service name
            if (string.Equals(span.Context.ServiceName, model.TraceChunk.DefaultServiceName, StringComparison.OrdinalIgnoreCase))
            {
                var versionRawBytes = MessagePackStringCache.GetVersionBytes(model.TraceChunk.ServiceVersion);

                if (versionRawBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, VersionNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, versionRawBytes);
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
                        offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasSiteKindTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesResourceGroup, azureAppServiceSettings.ResourceGroup);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasResourceGroupTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesSubscriptionId, azureAppServiceSettings.SubscriptionId);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasSubscriptionIdTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesResourceId, azureAppServiceSettings.ResourceId);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasResourceIdTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesInstanceId, azureAppServiceSettings.InstanceId);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasInstanceIdTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesInstanceName, azureAppServiceSettings.InstanceName);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasInstanceNameTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesOperatingSystem, azureAppServiceSettings.OperatingSystem);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasOperatingSystemTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesRuntime, azureAppServiceSettings.Runtime);

                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasRuntimeTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }

                    tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesExtensionVersion, azureAppServiceSettings.SiteExtensionVersion);
                    if (tagBytes is not null)
                    {
                        count++;
                        offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasExtensionVersionTagNameBytes);
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                    }
                }

                tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesSiteName, azureAppServiceSettings.SiteName);
                // the front-end identify AAS spans using aas.site.name and aas.site.type, so we need them on all spans
                if (tagBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasSiteNameTagNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, tagBytes);
                }

                tagBytes = MessagePackStringCache.GetAzureAppServiceKeyBytes(Datadog.Trace.Tags.AzureAppServicesSiteType, azureAppServiceSettings.SiteType);
                if (tagBytes is not null)
                {
                    count++;
                    offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, _aasSiteTypeTagNameBytes);
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
                    tagProcessors.FastGetReference(i)?.ProcessMeta(ref key, ref value);
                }
            }

            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, key);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETCOREAPP
        private void WriteTag(ref byte[] bytes, ref int offset, byte[] keyBytes, string value, ITagProcessor[] tagProcessors)
#else
        private void WriteTag(ref byte[] bytes, ref int offset, ReadOnlySpan<byte> keyBytes, string value, ITagProcessor[] tagProcessors)
#endif
        {
            if (tagProcessors is not null)
            {
                string key = null;
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors.FastGetReference(i)?.ProcessMeta(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, keyBytes);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, value);
        }

        // METRICS

        private int WriteMetrics(ref byte[] bytes, int offset, in SpanModel model, ITagProcessor[] tagProcessors)
        {
            var span = model.Span;
            int originalOffset = offset;

            // Start of "metrics" dictionary. Do not add any numeric tags before this line.
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, MetricsBytes);

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
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, ProcessIdNameBytes);
                offset += MessagePackBinary.WriteDouble(ref bytes, offset, processId);
            }

            // add "_sampling_priority_v1" tag to all "chunk orphans"
            // (spans whose parents are not found in the same chunk)
            if (model.IsChunkOrphan && model.TraceChunk.SamplingPriority is { } samplingPriority)
            {
                count++;
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, SamplingPriorityNameBytes);

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
                    tagProcessors.FastGetReference(i)?.ProcessMetric(ref key, ref value);
                }
            }

            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, key);
            offset += MessagePackBinary.WriteDouble(ref bytes, offset, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETCOREAPP
        private void WriteMetric(ref byte[] bytes, ref int offset, byte[] keyBytes, double value, ITagProcessor[] tagProcessors)
#else
        private void WriteMetric(ref byte[] bytes, ref int offset, ReadOnlySpan<byte> keyBytes, double value, ITagProcessor[] tagProcessors)
#endif
        {
            if (tagProcessors is not null)
            {
                string key = null;
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors.FastGetReference(i)?.ProcessMetric(ref key, ref value);
                }
            }

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

        internal struct TagWriter : IItemProcessor<string>, IItemProcessor<double>
        {
            private readonly SpanMessagePackFormatter _formatter;
            private readonly ITagProcessor[] _tagProcessors;

            public byte[] Bytes;
            public int Offset;
            public int Count;

            internal TagWriter(SpanMessagePackFormatter formatter, ITagProcessor[] tagProcessors, byte[] bytes, int offset)
            {
                _formatter = formatter;
                _tagProcessors = tagProcessors;
                Bytes = bytes;
                Offset = offset;
                Count = 0;
            }

            public void Process(TagItem<string> item)
            {
#if !NETCOREAPP
                if (item.KeyUtf8 is null)
                {
                    _formatter.WriteTag(ref Bytes, ref Offset, item.Key, item.Value, _tagProcessors);
                }
                else
                {
                    _formatter.WriteTag(ref Bytes, ref Offset, item.KeyUtf8, item.Value, _tagProcessors);
                }
#else
                if (item.KeyUtf8.IsEmpty)
                {
                    _formatter.WriteTag(ref Bytes, ref Offset, item.Key, item.Value, _tagProcessors);
                }
                else
                {
                    _formatter.WriteTag(ref Bytes, ref Offset, item.KeyUtf8, item.Value, _tagProcessors);
                }
#endif

                Count++;
            }

            public void Process(TagItem<double> item)
            {
#if !NETCOREAPP
                if (item.KeyUtf8 is null)
                {
                    _formatter.WriteMetric(ref Bytes, ref Offset, item.Key, item.Value, _tagProcessors);
                }
                else
                {
                    _formatter.WriteMetric(ref Bytes, ref Offset, item.KeyUtf8, item.Value, _tagProcessors);
                }
#else
                if (item.KeyUtf8.IsEmpty)
                {
                    _formatter.WriteMetric(ref Bytes, ref Offset, item.Key, item.Value, _tagProcessors);
                }
                else
                {
                    _formatter.WriteMetric(ref Bytes, ref Offset, item.KeyUtf8, item.Value, _tagProcessors);
                }
#endif

                Count++;
            }
        }
    }
}
