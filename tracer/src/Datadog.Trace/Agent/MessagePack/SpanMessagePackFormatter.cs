// <copyright file="SpanMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.ExtensionMethods;
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

        // top-level span fields
        private readonly byte[] _traceIdBytes = StringEncoding.UTF8.GetBytes("trace_id");
        private readonly byte[] _spanIdBytes = StringEncoding.UTF8.GetBytes("span_id");
        private readonly byte[] _nameBytes = StringEncoding.UTF8.GetBytes("name");
        private readonly byte[] _resourceBytes = StringEncoding.UTF8.GetBytes("resource");
        private readonly byte[] _serviceBytes = StringEncoding.UTF8.GetBytes("service");
        private readonly byte[] _typeBytes = StringEncoding.UTF8.GetBytes("type");
        private readonly byte[] _startBytes = StringEncoding.UTF8.GetBytes("start");
        private readonly byte[] _durationBytes = StringEncoding.UTF8.GetBytes("duration");
        private readonly byte[] _parentIdBytes = StringEncoding.UTF8.GetBytes("parent_id");
        private readonly byte[] _errorBytes = StringEncoding.UTF8.GetBytes("error");

        // string tags
        private readonly byte[] _metaBytes = StringEncoding.UTF8.GetBytes("meta");

        private readonly byte[] _languageNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Language);
        private readonly byte[] _languageValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.Language);

        private readonly byte[] _runtimeIdNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.RuntimeId);
        private readonly byte[] _runtimeIdValueBytes = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);

        private readonly byte[] _originNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Origin);

        private readonly byte[] _environmentNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Env);

        private readonly byte[] _versionNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Version);

        // numeric tags
        private readonly byte[] _metricsBytes = StringEncoding.UTF8.GetBytes("metrics");

        private readonly byte[] _processIdNameBytes = StringEncoding.UTF8.GetBytes(Trace.Metrics.ProcessId);
        private readonly byte[] _processIdValueBytes;

        private SpanMessagePackFormatter()
        {
            double processId = DomainMetadata.Instance.ProcessId;
            _processIdValueBytes = processId > 0 ? MessagePackSerializer.Serialize(processId) : null;
        }

        int IMessagePackFormatter<TraceChunkModel>.Serialize(ref byte[] bytes, int offset, TraceChunkModel traceChunk, IFormatterResolver formatterResolver)
        {
            return Serialize(ref bytes, offset, traceChunk, formatterResolver);
        }

        // overload of IMessagePackFormatter<TraceChunkModel>.Serialize() with `in` modifier on `TraceChunkModel` parameter
        public int Serialize(ref byte[] bytes, int offset, in TraceChunkModel traceChunk, IFormatterResolver formatterResolver)
        {
            int originalOffset = offset;

            // some strings are only constants within a trace, not across traces,
            // but we will use them multiple times, so cache those here
            var cachedStringBytes = new CachedStringBytes(
                StringEncoding.UTF8,
                environment: TraceUtil.NormalizeTag(traceChunk.Environment),
                serviceVersion: traceChunk.ServiceVersion);

            // start writing span[]
            offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, traceChunk.SpanCount);

            // serialize each span
            for (var i = 0; i < traceChunk.SpanCount; i++)
            {
                // when serializing each span, we need additional information that is not
                // available in the span object itself, like its position in the trace chunk
                // or if its parent can also be found in the same chunk, so we use SpanModel
                // to pass that information to the serializer
                var spanModel = traceChunk.GetSpanModel(i);
                offset += Serialize(ref bytes, offset, in spanModel, cachedStringBytes);
            }

            return offset - originalOffset;
        }

        private int Serialize(ref byte[] bytes, int offset, in SpanModel spanModel, CachedStringBytes cachedStringBytes)
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

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, span.Context.TraceId);

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

            if (span.Context.ParentId > 0)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _parentIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, (ulong)span.Context.ParentId);
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

            offset += WriteTags(ref bytes, offset, in spanModel, cachedStringBytes, tagProcessors);
            offset += WriteMetrics(ref bytes, offset, in spanModel, tagProcessors);

            return offset - originalOffset;
        }

        // TAGS

        private int WriteTags(ref byte[] bytes, int offset, in SpanModel model, CachedStringBytes cachedStringBytes, ITagProcessor[] tagProcessors)
        {
            var span = model.Span;
            int originalOffset = offset;

            // Start of "meta" dictionary. Do not add any string tags before this line.
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metaBytes);

            int count = 0;

            // We don't know the final count yet, write a fixed-size header and note the offset
            var countOffset = offset;
            offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0);

            // add "language=dotnet" tag to all spans, except those that
            // represents a downstream service or external dependency
            if (span.Tags is not InstrumentationTags { SpanKind: SpanKinds.Client or SpanKinds.Producer })
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageNameBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageValueBytes);
            }

            // Write span tags
            var tagWriter = new TagWriter(this, tagProcessors, bytes, offset);
            span.Tags.EnumerateTags(ref tagWriter);
            bytes = tagWriter.Bytes;
            offset = tagWriter.Offset;
            count += tagWriter.Count;

            // and "env" to all spans
            if (cachedStringBytes.Environment is not null)
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentNameBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, cachedStringBytes.Environment);
            }

            // and "version" to all spans
            if (cachedStringBytes.ServiceVersion is not null)
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _versionNameBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, cachedStringBytes.ServiceVersion);
            }

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

            if (span.IsTopLevel && (!Ci.CIVisibility.IsRunning || !Ci.CIVisibility.Settings.Agentless))
            {
                // add "runtime-id" tag to service-entry (aka top-level) spans
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdNameBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdValueBytes);
            }

            // add "_dd.origin" tag to all spans
            string origin = span.Context.Origin;

            if (!string.IsNullOrEmpty(origin))
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _originNameBytes);
                offset += MessagePackBinary.WriteString(ref bytes, offset, origin);
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
        private void WriteTag(ref byte[] bytes, ref int offset, byte[] keyBytes, string value, ITagProcessor[] tagProcessors)
        {
            if (tagProcessors is not null)
            {
                string key = null;
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors[i]?.ProcessMeta(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, keyBytes);
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

            if (model.IsLocalRoot)
            {
                // add "process_id" tag
                if (_processIdValueBytes != null)
                {
                    count++;
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _processIdNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, _processIdValueBytes);
                }
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
        private void WriteMetric(ref byte[] bytes, ref int offset, byte[] keyBytes, double value, ITagProcessor[] tagProcessors)
        {
            if (tagProcessors is not null)
            {
                string key = null;
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors[i]?.ProcessMetric(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, keyBytes);
            offset += MessagePackBinary.WriteDouble(ref bytes, offset, value);
        }

        TraceChunkModel IMessagePackFormatter<TraceChunkModel>.Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotSupportedException($"{nameof(SpanMessagePackFormatter)} does not support deserialization. For testing purposes, deserialize using the MessagePack NuGet package.");
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
                if (item.KeyUtf8 is null)
                {
                    _formatter.WriteTag(ref Bytes, ref Offset, item.Key, item.Value, _tagProcessors);
                }
                else
                {
                    _formatter.WriteTag(ref Bytes, ref Offset, item.KeyUtf8, item.Value, _tagProcessors);
                }

                Count++;
            }

            public void Process(TagItem<double> item)
            {
                if (item.KeyUtf8 is null)
                {
                    _formatter.WriteMetric(ref Bytes, ref Offset, item.Key, item.Value, _tagProcessors);
                }
                else
                {
                    _formatter.WriteMetric(ref Bytes, ref Offset, item.KeyUtf8, item.Value, _tagProcessors);
                }

                Count++;
            }
        }
    }
}
