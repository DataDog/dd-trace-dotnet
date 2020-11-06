using System;
using Datadog.Trace.Agent.NamedPipes;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class TraceRequestMessagePackFormatter : IMessagePackFormatter<TraceRequest>
    {
        public int Serialize(ref byte[] bytes, int offset, TraceRequest value, IFormatterResolver formatterResolver)
        {
            return value.SerializeTo(ref bytes, offset);
        }

        public TraceRequest Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }
    }
}
