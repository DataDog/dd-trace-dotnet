#if MESSAGEPACK_1_9
using System;
using Datadog.Trace.ExtensionMethods;
using MessagePack;
using MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class SpanMessagePackFormatter : IMessagePackFormatter<Span>
    {
        public int Serialize(ref byte[] bytes, int offset, Span value, IFormatterResolver formatterResolver)
        {
            // First, pack array length (or map length).
            // It should be the number of members of the object to be serialized.
            var len = 8;

            if (value.Context.ParentId != null)
            {
                len++;
            }

            if (value.Error)
            {
                len++;
            }

            if (value.Tags != null)
            {
                len++;
            }

            if (value.Metrics != null)
            {
                len++;
            }

            int originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "trace_id");
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.Context.TraceId);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "span_id");
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, value.Context.SpanId);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "name");
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.OperationName);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "resource");
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.ResourceName);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "service");
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.ServiceName);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "type");
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.Type);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "start");
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.StartTime.ToUnixTimeNanoseconds());

            offset += MessagePackBinary.WriteString(ref bytes, offset, "duration");
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.Duration.ToNanoseconds());

            if (value.Context.ParentId != null)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, "parent_id");
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, (ulong)value.Context.ParentId);
            }

            if (value.Error)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, "error");
                offset += MessagePackBinary.WriteByte(ref bytes, offset, 1);
            }

            if (value.Tags != null)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, "meta");
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, value.Tags.Count);

                foreach (var pair in value.Tags)
                {
                    offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Key);
                    offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Value);
                }
            }

            if (value.Metrics != null)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, "metrics");
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, value.Metrics.Count);

                foreach (var pair in value.Metrics)
                {
                    offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Key);
                    offset += MessagePackBinary.WriteDouble(ref bytes, offset, pair.Value);
                }
            }

            return offset - originalOffset;
        }

        public Span Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }
    }
}
#endif
