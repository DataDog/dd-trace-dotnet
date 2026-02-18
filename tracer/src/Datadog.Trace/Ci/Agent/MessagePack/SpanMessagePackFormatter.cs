// <copyright file="SpanMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Ci.Agent.MessagePack;

internal sealed class SpanMessagePackFormatter : IMessagePackFormatter<Span>
{
    public static readonly IMessagePackFormatter<Span> Instance = new SpanMessagePackFormatter();

    // Runtime values
    private readonly byte[][] _samplingPriorityValueBytes;
    private readonly byte[]? _processIdValueBytes;

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

        var correlationId = value.Type is SpanTypes.Test or SpanTypes.Browser ? TestOptimization.Instance.SkippableFeature?.GetCorrelationId() : null;
        if (correlationId is not null)
        {
            len++;
        }

        var originalOffset = offset;

        offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

        if (isSpan)
        {
            // trace_id field is 64-bits, truncate by using TraceId128.Lower
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TraceIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.TraceId128.Lower);

            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.SpanIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.SpanId);
        }

        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.NameBytes);
        offset += MessagePackBinary.WriteString(ref bytes, offset, value.OperationName);

        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ResourceBytes);
        offset += MessagePackBinary.WriteString(ref bytes, offset, value.ResourceName);

        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ServiceBytes);
        offset += MessagePackBinary.WriteString(ref bytes, offset, value.ServiceName);

        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TypeBytes);
        offset += MessagePackBinary.WriteString(ref bytes, offset, value.Type);

        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.StartBytes);
        offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.StartTime.ToUnixTimeNanoseconds());

        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.DurationBytes);
        offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.Duration.ToNanoseconds());

        if (context.ParentId is not null)
        {
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ParentIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, context.ParentId.Value);
        }

        if (testSuiteTags is not null)
        {
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestSuiteIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, testSuiteTags.SuiteId);
        }

        if (testModuleTags is not null)
        {
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestModuleIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, testModuleTags.ModuleId);
        }

        if (testSessionTags is not null && testSessionTags.SessionId != 0)
        {
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.TestSessionIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, testSessionTags.SessionId);
        }

        if (correlationId is not null)
        {
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ItrCorrelationIdBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, correlationId);
        }

        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ErrorBytes);
        offset += MessagePackBinary.WriteByte(ref bytes, offset, (byte)(value.Error ? 1 : 0));

        ITagProcessor[]? tagProcessors = null;
        if (context.TraceContext?.Tracer is Tracer tracer)
        {
            tagProcessors = tracer.TracerManager.TagProcessors;
        }

        offset += SerializeTags(ref bytes, offset, value, value.Tags, tagProcessors);

        return offset - originalOffset;
    }

    private int SerializeTags(ref byte[] bytes, int offset, Span span, ITags tags, ITagProcessor[]? tagProcessors)
    {
        int originalOffset = offset;

        offset += WriteTags(ref bytes, offset, span, tags, tagProcessors);
        offset += WriteMetrics(ref bytes, offset, span, tags, tagProcessors);

        return offset - originalOffset;
    }

    // TAGS

    private int WriteTags(ref byte[] bytes, int offset, Span span, ITags tags, ITagProcessor[]? tagProcessors)
    {
        int originalOffset = offset;
        var traceContext = span.Context.TraceContext;

        // Start of "meta" dictionary. Do not add any string tags before this line.
        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.MetaBytes);

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
        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.OriginBytes);
        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.CIAppTestOriginNameBytes);

        // add "env" to all spans
        var env = traceContext?.Environment;

        if (!string.IsNullOrWhiteSpace(env))
        {
            count++;
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.EnvBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, env);
        }

        // add "language=dotnet" tag to all spans, except those that
        // represents a downstream service or external dependency
        if (span.Tags is not InstrumentationTags { SpanKind: SpanKinds.Client or SpanKinds.Producer })
        {
            count++;
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.LanguageBytes);
            offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.DotnetLanguageValueBytes);
        }

        // add "version" tags to all spans whose service name is the default service name
        if (string.Equals(span.Context.ServiceName, traceContext?.Tracer.DefaultServiceName, StringComparison.OrdinalIgnoreCase))
        {
            var version = traceContext?.ServiceVersion;

            if (!string.IsNullOrWhiteSpace(version))
            {
                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.VersionBytes);
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
    private void WriteTag(ref byte[] bytes, ref int offset, string key, string value, ITagProcessor[]? tagProcessors)
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
    private void WriteTag(ref byte[] bytes, ref int offset, ReadOnlySpan<byte> keyBytes, string value, ITagProcessor[]? tagProcessors)
    {
        if (tagProcessors is not null)
        {
            string? key = null;
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

    private int WriteMetrics(ref byte[] bytes, int offset, Span span, ITags tags, ITagProcessor[]? tagProcessors)
    {
        int originalOffset = offset;

        // Start of "metrics" dictionary. Do not add any numeric tags before this line.
        offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.MetricsBytes);

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
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.ProcessIdBytes);
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, _processIdValueBytes);
            }

            // add "_sampling_priority_v1" tag
            if (span.Context.TraceContext.SamplingPriority is { } samplingPriority)
            {
                count++;
                offset += MessagePackBinary.WriteRaw(ref bytes, offset, MessagePackConstants.SamplingPriorityBytes);

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
    private void WriteMetric(ref byte[] bytes, ref int offset, string key, double value, ITagProcessor[]? tagProcessors)
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
    private void WriteMetric(ref byte[] bytes, ref int offset, ReadOnlySpan<byte> keyBytes, double value, ITagProcessor[]? tagProcessors)
    {
        if (tagProcessors is not null)
        {
            string? key = null;
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
        private readonly ITagProcessor[]? _tagProcessors;

        public byte[] Bytes;
        public int Offset;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TagWriter(SpanMessagePackFormatter formatter, ITagProcessor[]? tagProcessors, byte[] bytes, int offset)
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
        private readonly ITagProcessor[]? _tagProcessors;

        public byte[] Bytes;
        public int Offset;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TraceTagWriter(SpanMessagePackFormatter formatter, ITagProcessor[]? tagProcessors, byte[] bytes, int offset)
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
