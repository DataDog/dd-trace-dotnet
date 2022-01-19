// <copyright file="TestEventMessagePackFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using Datadog.Trace.Vendors.MessagePack.Resolvers;

namespace Datadog.Trace.Ci.Agent.MessagePack
{
    internal class TestEventMessagePackFormatter : IMessagePackFormatter<TestEvent>
    {
        private byte[] _typeBytes = StringEncoding.UTF8.GetBytes("type");
        private byte[] _contentBytes = StringEncoding.UTF8.GetBytes("content");

        public TestEvent Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }

        public int Serialize(ref byte[] bytes, int offset, TestEvent value, IFormatterResolver formatterResolver)
        {
            if (value is null)
            {
                return 0;
            }

            var originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, 2);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _typeBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset,  value.Type);

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _contentBytes);
            offset += formatterResolver.GetFormatter<Span>().Serialize(ref bytes, offset, value.Content, formatterResolver);

            return offset - originalOffset;
        }
    }
}
