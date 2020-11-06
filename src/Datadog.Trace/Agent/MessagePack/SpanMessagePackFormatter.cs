using System;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class SpanMessagePackFormatter : IMessagePackFormatter<Span>
    {
        public int Serialize(ref byte[] bytes, int offset, Span value, IFormatterResolver formatterResolver)
        {
            return value.SerializeTo(ref bytes, offset);
        }

        public Span Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }
    }
}
