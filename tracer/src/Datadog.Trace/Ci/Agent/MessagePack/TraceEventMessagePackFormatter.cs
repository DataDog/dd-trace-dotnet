// <copyright file="TraceEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class TraceEventMessagePackFormatter : IMessagePackFormatter<TraceEvent>
    {
        private byte[] _typeBytes = StringEncoding.UTF8.GetBytes("type");
        // .
        private byte[] _versionBytes = StringEncoding.UTF8.GetBytes("version");
        private byte[] _versionValueBytes = StringEncoding.UTF8.GetBytes("1.0.0");
        // .
        private byte[] _contentBytes = StringEncoding.UTF8.GetBytes("content");
        private byte[] _spansBytes = StringEncoding.UTF8.GetBytes("spans");

        public TraceEvent Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }

        public int Serialize(ref byte[] bytes, int offset, TraceEvent value, IFormatterResolver formatterResolver)
        {
            if (value is null)
            {
                return 0;
            }

            var originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 4);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _typeBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.Type);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _versionBytes);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _versionValueBytes);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _contentBytes);
            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 1);
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _spansBytes);

            offset += formatterResolver.GetFormatter<ArraySegment<Span>>().Serialize(ref bytes, offset, value.Content, formatterResolver);

            return offset - originalOffset;
        }
    }
}
