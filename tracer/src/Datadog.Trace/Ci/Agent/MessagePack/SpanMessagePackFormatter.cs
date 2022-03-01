// <copyright file="SpanMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Processors;
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

        private SpanMessagePackFormatter()
        {
        }

        public int Serialize(ref byte[] bytes, int offset, Span value, IFormatterResolver formatterResolver)
        {
            int originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 12);

            var context = value.Context;

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceIdBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, context.TraceId.ToString());

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spanIdBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, context.SpanId.ToString());

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

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _parentIdBytes);
            var parentId = value.Context.ParentId;
            if (parentId.HasValue)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, parentId.Value.ToString());
            }
            else
            {
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _errorBytes);
            offset += MessagePackBinary.WriteByte(ref bytes, offset, (byte)(value.Error ? 1 : 0));

            ITagProcessor[] tagProcessors = null;
            if (value.Context.TraceContext?.Tracer is Tracer tracer)
            {
                tagProcessors = tracer.TracerManager?.TagProcessors;
            }

            offset += value.Tags.SerializeTo(ref bytes, offset, value, tagProcessors);

            return offset - originalOffset;
        }

        public Span Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }
    }
}
