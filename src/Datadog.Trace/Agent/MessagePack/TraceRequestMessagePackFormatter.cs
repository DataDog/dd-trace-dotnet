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
            offset += value.WriteHeaders(ref bytes, offset);
            offset += value.WriteTraces(ref bytes, offset);
            return offset;
        }

        public TraceRequest Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }
    }
}
