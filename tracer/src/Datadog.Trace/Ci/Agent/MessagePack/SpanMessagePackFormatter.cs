// <copyright file="SpanMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
        private readonly byte[] _itrCorrelationId = StringEncoding.UTF8.GetBytes("itr_correlation_id");

        // SuiteId, ModuleId, SessionId tags
        private readonly byte[] _testSuiteIdBytes = StringEncoding.UTF8.GetBytes(TestSuiteVisibilityTags.TestSuiteId);
        private readonly byte[] _testModuleIdBytes = StringEncoding.UTF8.GetBytes(TestSuiteVisibilityTags.TestModuleId);
        private readonly byte[] _testSessionIdBytes = StringEncoding.UTF8.GetBytes(TestSuiteVisibilityTags.TestSessionId);

        // string tags
        private readonly byte[] _metaBytes = StringEncoding.UTF8.GetBytes("meta");

        private readonly byte[] _languageNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Language);
        private readonly byte[] _languageValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.Language);

        private readonly byte[] _environmentNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Env);

        private readonly byte[] _versionNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Version);

        private readonly byte[] _originNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Origin);
        private readonly byte[] _originValueBytes = StringEncoding.UTF8.GetBytes(TestTags.CIAppTestOriginName);

        // numeric tags
        private readonly byte[] _metricsBytes = StringEncoding.UTF8.GetBytes("metrics");

        private readonly byte[] _samplingPriorityNameBytes = StringEncoding.UTF8.GetBytes(Metrics.SamplingPriority);
        private readonly byte[][] _samplingPriorityValueBytes;

        private readonly byte[] _processIdNameBytes = StringEncoding.UTF8.GetBytes(Trace.Metrics.ProcessId);
        private readonly byte[] _processIdValueBytes;

        private SpanMessagePackFormatter()
        {
            double processId = DomainMetadata.Instance.ProcessId;
            _processIdValueBytes = processId > 0 ? MessagePackSerializer.Serialize(processId) : null;

            // values begin at -1, so they are shifted by 1 from their array index: [-1, 0, 1, 2]
            // these must serialized as msgpack float64 (Double in .NET).
            _samplingPriorityValueBytes =
            [
                MessagePackSerializer.Serialize((double)SamplingPriorityValues.UserReject),
                MessagePackSerializer.Serialize((double)SamplingPriorityValues.AutoReject),
                MessagePackSerializer.Serialize((double)SamplingPriorityValues.AutoKeep),
                MessagePackSerializer.Serialize((double)SamplingPriorityValues.UserKeep)
            ];
        }

        public int Serialize(ref byte[] bytes, int offset, Span value, IFormatterResolver formatterResolver)
        {
            var context = value.Context;
            var testSessionTags = value.Tags as TestSessionSpanTags;
            var testModuleTags = value.Tags as TestModuleSpanTags;
            var testSuiteTags = value.Tags as TestSuiteSpanTags;

            // First, pack array length (or map length).
            // It should be the number of members of the object to be serialized.
            var len = 9;

            if (context.ParentIdInternal is not null)
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

            var correlationId = value.Type is SpanTypes.Test or SpanTypes.Browser ? CIVisibility.GetSkippableTestsCorrelationId() : null;
            if (correlationId is not null)
            {
                len++;
            }

            var originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

            if (isSpan)
            {
                // trace_id field is 64-bits, truncate by using TraceId128.Lower
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.TraceId128.Lower);

                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spanIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.SpanId);
            }

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

            if (context.ParentIdInternal is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _parentIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.ParentIdInternal.Value);
            }

            if (testSuiteTags is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSuiteIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, testSuiteTags.SuiteId);
            }

            if (testModuleTags is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testModuleIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, testModuleTags.ModuleId);
            }

            if (testSessionTags is not null && testSessionTags.SessionId != 0)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _testSessionIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, testSessionTags.SessionId);
            }

            if (correlationId is not null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _itrCorrelationId);
                offset += MessagePackBinary.WriteString(ref bytes, offset, correlationId);
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
            var traceContext = span.Context.TraceContext;

            // Start of "meta" dictionary. Do not add any string tags before this line.
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metaBytes);

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

            // Write trace tags
            // NOTE: this formatter for CI Visibility doesn't know if the span is "first in chunk" or "chunk orphan",
            // so we add the trace tags to the local root span only.
            if (span.IsRootSpan && traceContext?.Tags is { Count: > 0 } traceTags)
            {
                var traceTagWriter = new TraceTagWriter(this, tagProcessors, bytes, offset);
                traceTags.Enumerate(ref traceTagWriter);
                bytes = traceTagWriter.Bytes;
                offset = traceTagWriter.Offset;
                count += traceTagWriter.Count;
            }

            // add "_dd.origin" tag to all spans
            count++;
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _originNameBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _originValueBytes);

            // add "env" to all spans
            var env = traceContext?.Environment;

            if (!string.IsNullOrWhiteSpace(env))
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _environmentNameBytes);
                offset += MessagePackBinary.WriteString(ref bytes, offset, env);
            }

            // add "language=dotnet" tag to all spans, except those that
            // represents a downstream service or external dependency
            if (span.Tags is not InstrumentationTags { SpanKind: SpanKinds.Client or SpanKinds.Producer })
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageNameBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _languageValueBytes);
            }

            // add "version" tags to all spans whose service name is the default service name
            if (string.Equals(span.Context.ServiceNameInternal, traceContext?.Tracer.DefaultServiceName, StringComparison.OrdinalIgnoreCase))
            {
                var version = traceContext?.ServiceVersion;

                if (!string.IsNullOrWhiteSpace(version))
                {
                    count++;
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _versionNameBytes);
                    offset += MessagePackBinary.WriteString(ref bytes, offset, version);
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

            if (span.IsRootSpan)
            {
                if (_processIdValueBytes is not null)
                {
                    // add "process_id" tag
                    count++;
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _processIdNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, _processIdValueBytes);
                }

                // add "_sampling_priority_v1" tag
                if (span.Context.TraceContext.SamplingPriority is { } samplingPriority)
                {
                    count++;
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _samplingPriorityNameBytes);

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
        }

        internal struct TraceTagWriter : TraceTagCollection.ITagEnumerator
        {
            private readonly SpanMessagePackFormatter _formatter;
            private readonly ITagProcessor[] _tagProcessors;

            public byte[] Bytes;
            public int Offset;
            public int Count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal TraceTagWriter(SpanMessagePackFormatter formatter, ITagProcessor[] tagProcessors, byte[] bytes, int offset)
            {
                _formatter = formatter;
                _tagProcessors = tagProcessors;
                Bytes = bytes;
                Offset = offset;
                Count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Next(KeyValuePair<string, string> item)
            {
                _formatter.WriteTag(ref Bytes, ref Offset, item.Key, item.Value, _tagProcessors);
                Count++;
            }
        }
    }
}
