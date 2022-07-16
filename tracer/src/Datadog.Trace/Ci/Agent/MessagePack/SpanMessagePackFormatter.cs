// <copyright file="SpanMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class SpanMessagePackFormatter : IMessagePackFormatter<Span>
    {
        public static readonly IMessagePackFormatter<Span> Instance = new SpanMessagePackFormatter();

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

        // name of string tag dictionary
        private readonly byte[] _metaBytes = StringEncoding.UTF8.GetBytes("meta");

        private readonly byte[] _languageNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Language);
        private readonly byte[] _languageValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.Language);
        private readonly byte[] _originNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Origin);
        private readonly byte[] _originValueBytes = StringEncoding.UTF8.GetBytes(TestTags.CIAppTestOriginName);

        // name of numeric tag dictionary
        private readonly byte[] _metricsBytes = StringEncoding.UTF8.GetBytes("metrics");

        private readonly byte[] _processIdNameBytes = StringEncoding.UTF8.GetBytes(Trace.Metrics.ProcessId);
        private readonly byte[] _processIdValueBytes;

        private SpanMessagePackFormatter()
        {
            double processId = DomainMetadata.Instance.ProcessId;
            _processIdValueBytes = processId > 0 ? MessagePackSerializer.Serialize(processId) : null;
        }

        public int Serialize(ref byte[] bytes, int offset, Span value, IFormatterResolver formatterResolver)
        {
            var context = value.Context;

            // First, pack array length (or map length).
            // It should be the number of members of the object to be serialized.
            var len = 9;

            if (context.ParentId is not null)
            {
                len++;
            }

            len += 2; // Tags and metrics

            int originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.TraceId);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spanIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.SpanId);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _nameBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.OperationName);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _resourceBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.ResourceName);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _serviceBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.ServiceName);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _typeBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.Type);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _startBytes);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.StartTime.ToUnixTimeNanoseconds());

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _durationBytes);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.Duration.ToNanoseconds());

            if (context.ParentId is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _parentIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.ParentId.Value);
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _errorBytes);
            offset += MessagePackBinary.WriteByte(ref bytes, offset, (byte)(value.Error ? 1 : 0));

            ITagProcessor[] tagProcessors = null;
            if (context.TraceContext?.Tracer is Tracer tracer)
            {
                tagProcessors = tracer.TracerManager.TagProcessors;
            }

            offset += SerializeTags(ref bytes, offset, value, value.Tags, tagProcessors);

            return offset - originalOffset;
        }

        private int SerializeTags(ref byte[] bytes, int offset, Span span, ITags tags, ITagProcessor[] tagProcessors)
        {
            int originalOffset = offset;

            offset += WriteTags(ref bytes, offset, span, tags, tagProcessors);
            offset += WriteMetrics(ref bytes, offset, span, tags, tagProcessors);

            return offset - originalOffset;
        }

        // TAGS

        private int WriteTags(ref byte[] bytes, int offset, Span span, ITags tags, ITagProcessor[] tagProcessors)
        {
            int originalOffset = offset;

            // Start of "meta" dictionary. Do not add any string tags before this line.
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metaBytes);

            int count = 0;

            // We don't know the final count yet, write a fixed-size header and note the offset
            var countOffset = offset;
            offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0);

            // add "language=dotnet" tag to all spans, except those that
            // represents a downstream service or external dependency
            if (tags is not InstrumentationTags { SpanKind: SpanKinds.Client or SpanKinds.Producer })
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageNameBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageValueBytes);
            }

            // Write span tags
            var tagWriter = new TagWriter(this, tagProcessors, bytes, offset);
            tags.EnumerateTags(ref tagWriter);
            bytes = tagWriter.Bytes;
            offset = tagWriter.Offset;
            count += tagWriter.Count;

            if (span.IsRootSpan && span.Context.TraceContext != null)
            {
                // write trace-level string tags
                var traceTags = span.Context.TraceContext.Tags.ToArray();
                count += traceTags.Length;

                foreach (var tag in traceTags)
                {
                    WriteTag(ref bytes, ref offset, tag.Key, tag.Value, tagProcessors);
                }
            }

            // add "_dd.origin" tag to all spans
            count++;
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _originNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _originValueBytes);

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

        private int WriteMetrics(ref byte[] bytes, int offset, Span span, ITags tags, ITagProcessor[] tagProcessors)
        {
            int originalOffset = offset;

            // Start of "metrics" dictionary. Do not add any numeric tags before this line.
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metricsBytes);

            int count = 0;

            // We don't know the final count yet, write a fixed-size header and note the offset
            var countOffset = offset;
            offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0);

            // Write span metrics
            var tagWriter = new TagWriter(this, tagProcessors, bytes, offset);
            tags.EnumerateMetrics(ref tagWriter);
            bytes = tagWriter.Bytes;
            offset = tagWriter.Offset;
            count += tagWriter.Count;

            if (span.IsRootSpan && _processIdValueBytes is not null)
            {
                // add "process_id" tag
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _processIdNameBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, _processIdValueBytes);
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

        public Span Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
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
