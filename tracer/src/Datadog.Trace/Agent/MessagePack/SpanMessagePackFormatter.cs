// <copyright file="SpanMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class SpanMessagePackFormatter : IMessagePackFormatter<Span>
    {
        public static readonly IMessagePackFormatter<Span> Instance = new SpanMessagePackFormatter();

        private byte[] _traceIdBytes = StringEncoding.UTF8.GetBytes("trace_id");
        private byte[] _spanIdBytes = StringEncoding.UTF8.GetBytes("span_id");
        private byte[] _nameBytes = StringEncoding.UTF8.GetBytes("name");
        private byte[] _resourceBytes = StringEncoding.UTF8.GetBytes("resource");
        private byte[] _serviceBytes = StringEncoding.UTF8.GetBytes("service");
        private byte[] _typeBytes = StringEncoding.UTF8.GetBytes("type");
        private byte[] _startBytes = StringEncoding.UTF8.GetBytes("start");
        private byte[] _durationBytes = StringEncoding.UTF8.GetBytes("duration");
        private byte[] _parentIdBytes = StringEncoding.UTF8.GetBytes("parent_id");
        private byte[] _errorBytes = StringEncoding.UTF8.GetBytes("error");

        private SpanMessagePackFormatter()
        {
        }

        public int Serialize(ref byte[] bytes, int offset, Span value, IFormatterResolver formatterResolver)
        {
            // First, pack array length (or map length).
            // It should be the number of members of the object to be serialized.
            var len = 8;

            if (value.InternalContext.ParentId != null)
            {
                len++;
            }

            if (value.Error)
            {
                len++;
            }

            len += 2; // Tags and metrics

            int originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _traceIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.InternalContext.TraceId);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spanIdBytes);
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.InternalContext.SpanId);

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

            if (value.InternalContext.ParentId != null)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _parentIdBytes);
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, (ulong)value.InternalContext.ParentId);
            }

            if (value.Error)
            {
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _errorBytes);
                offset += MessagePackBinary.WriteByte(ref bytes, offset, 1);
            }

            offset += value.Tags.SerializeTo(ref bytes, offset, value);

            return offset - originalOffset;
        }

        public Span Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }
    }
}
