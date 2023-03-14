// <copyright file="SpanMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci.Tagging;
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

        private readonly byte[][] _samplingPriorityValueBytes;
        private readonly byte[] _processIdValueBytes;

        private SpanMessagePackFormatter()
        {
            double processId = DomainMetadata.Instance.ProcessId;
            _processIdValueBytes = processId > 0 ? MessagePackSerializer.Serialize(processId) : null;

            // values begin at -1, so they are shifted by 1 from their array index: [-1, 0, 1, 2]
            // these must serialized as msgpack float64 (Double in .NET).
            _samplingPriorityValueBytes = new[]
            {
                MessagePackSerializer.Serialize((double)SamplingPriorityValues.UserReject),
                MessagePackSerializer.Serialize((double)SamplingPriorityValues.AutoReject),
                MessagePackSerializer.Serialize((double)SamplingPriorityValues.AutoKeep),
                MessagePackSerializer.Serialize((double)SamplingPriorityValues.UserKeep),
            };
        }

#if NETCOREAPP
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

        private ReadOnlySpan<byte> LanguageNameBytes => "language"u8;

        private ReadOnlySpan<byte> LanguageValueBytes => "dotnet"u8;

        private ReadOnlySpan<byte> EnvironmentNameBytes => "env"u8;

        private ReadOnlySpan<byte> VersionNameBytes => "version"u8;

        private ReadOnlySpan<byte> OriginNameBytes => "_dd.origin"u8;

        private ReadOnlySpan<byte> OriginValueBytes => "ciapp-test"u8;

        // numeric tags
        private ReadOnlySpan<byte> MetricsBytes => "metrics"u8;

        private ReadOnlySpan<byte> SamplingPriorityNameBytes => "_sampling_priority_v1"u8;

        private ReadOnlySpan<byte> ProcessIdNameBytes => "process_id"u8;
#else
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

        private byte[] EnvironmentNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.Env);

        private byte[] VersionNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.Version);

        private byte[] OriginNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Tags.Origin);

        private byte[] OriginValueBytes { get; } = StringEncoding.UTF8.GetBytes(TestTags.CIAppTestOriginName);

        // numeric tags
        private byte[] MetricsBytes { get; } = StringEncoding.UTF8.GetBytes("metrics");

        private byte[] SamplingPriorityNameBytes { get; } = StringEncoding.UTF8.GetBytes(Metrics.SamplingPriority);

        private byte[] ProcessIdNameBytes { get; } = StringEncoding.UTF8.GetBytes(Trace.Metrics.ProcessId);
#endif

        public int Serialize(ref byte[] bytes, int offset, Span value, IFormatterResolver formatterResolver)
        {
            var context = value.Context;
            var testSessionTags = value.Tags as TestSessionSpanTags;
            var testModuleTags = value.Tags as TestModuleSpanTags;
            var testSuiteTags = value.Tags as TestSuiteSpanTags;

            // First, pack array length (or map length).
            // It should be the number of members of the object to be serialized.
            var len = 9;

            if (context.ParentId is not null)
            {
                len++;
            }

            if (testSessionTags is not null && testSessionTags.SessionId != 0)
            {
                // we need to add TestSessionId value to the root
                len++;
            }

            if (testModuleTags is not null)
            {
                // we need to add ModuleId value to the root
                len++;
            }

            if (testSuiteTags is not null)
            {
                // we need to add SuiteId value to the root
                len++;
            }

            var isSpan = false;
            if (value.Type is not (SpanTypes.TestSuite or SpanTypes.TestModule or SpanTypes.TestSession))
            {
                // we need to add TraceId and SpanId
                len++;
                len++;
                isSpan = true;
            }

            var originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

            if (isSpan)
            {
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, TraceIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.TraceId);

                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, SpanIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.SpanId);
            }

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, NameBytes);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, value.OperationName);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, ResourceBytes);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, value.ResourceName);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, ServiceBytes);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, value.ServiceName);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, TypeBytes);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, value.Type);

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, StartBytes);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.StartTime.ToUnixTimeNanoseconds());

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, DurationBytes);
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.Duration.ToNanoseconds());

            if (context.ParentId is not null)
            {
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, ParentIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.ParentId.Value);
            }

            if (testSuiteTags is not null)
            {
                offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, TestSuiteVisibilityTags.TestSuiteId);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, testSuiteTags.SuiteId);
            }

            if (testModuleTags is not null)
            {
                offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, TestSuiteVisibilityTags.TestModuleId);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, testModuleTags.ModuleId);
            }

            if (testSessionTags is not null && testSessionTags.SessionId != 0)
            {
                offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, TestSuiteVisibilityTags.TestSessionId);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, testSessionTags.SessionId);
            }

            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, ErrorBytes);
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
            var traceContext = span.Context.TraceContext;

            // Start of "meta" dictionary. Do not add any string tags before this line.
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, MetaBytes);

            int count = 0;

            // We don't know the final count yet, write a fixed-size header and note the offset
            var countOffset = offset;
            offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0);

            // Write span tags
            var tagWriter = new TagWriter(this, tagProcessors, bytes, offset);
            tags.EnumerateTags(ref tagWriter);
            bytes = tagWriter.Bytes;
            offset = tagWriter.Offset;
            count += tagWriter.Count;

            // TODO: for each trace tag, determine if it should be added to the local root,
            // to the first span in the chunk, or to all orphan spans.
            // For now, we add them to the local root which is correct in most cases.
            if (span.IsRootSpan && traceContext?.Tags?.ToArray() is { Length: > 0 } traceTags)
            {
                count += traceTags.Length;

                foreach (var tag in traceTags)
                {
                    WriteTag(ref bytes, ref offset, tag.Key, tag.Value, tagProcessors);
                }
            }

            // add "_dd.origin" tag to all spans
            count++;
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, OriginNameBytes);
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, OriginValueBytes);

            // add "env" to all spans
            var env = traceContext?.Environment;

            if (!string.IsNullOrWhiteSpace(env))
            {
                count++;
                offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, EnvironmentNameBytes);
                offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, env);
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
            if (string.Equals(span.Context.ServiceName, traceContext?.Tracer.DefaultServiceName, StringComparison.OrdinalIgnoreCase))
            {
                var version = traceContext?.ServiceVersion;

                if (!string.IsNullOrWhiteSpace(version))
                {
                    count++;
                    offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, VersionNameBytes);
                    offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, version);
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
                    tagProcessors[i]?.ProcessMeta(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, keyBytes);
            offset += MessagePackBinary.UnsafeWriteString(ref bytes, offset, value);
        }

        // METRICS

        private int WriteMetrics(ref byte[] bytes, int offset, Span span, ITags tags, ITagProcessor[] tagProcessors)
        {
            int originalOffset = offset;

            // Start of "metrics" dictionary. Do not add any numeric tags before this line.
            offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, MetricsBytes);

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

            if (span.IsRootSpan)
            {
                if (_processIdValueBytes is not null)
                {
                    // add "process_id" tag
                    count++;
                    offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, ProcessIdNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, _processIdValueBytes);
                }

                // add "_sampling_priority_v1" tag
                if (span.Context.TraceContext.SamplingPriority is { } samplingPriority)
                {
                    count++;
                    offset += MessagePackBinary.UnsafeWriteStringBytes(ref bytes, offset, SamplingPriorityNameBytes);

                    if (samplingPriority is >= -1 and <= 2)
                    {
                        // values begin at -1, so they are shifted by 1 from their array index: [-1, 0, 1, 2]
                        offset += MessagePackBinary.WriteRaw(ref bytes, offset, _samplingPriorityValueBytes[samplingPriority + 1]);
                    }
                    else
                    {
                        // fallback to support unknown future values that are not cached
                        offset += MessagePackBinary.WriteDouble(ref bytes, offset, samplingPriority);
                    }
                }
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
                    tagProcessors[i]?.ProcessMetric(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, keyBytes);
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
