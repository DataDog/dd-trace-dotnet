#if !NET45
using System;
using Datadog.Trace.ExtensionMethods;
using MessagePack;
using MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class SpanMessagePackFormatter : IMessagePackFormatter<Span>
    {
        public void Serialize(ref MessagePackWriter writer, Span value, MessagePackSerializerOptions options)
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

            writer.WriteMapHeader(len);

            writer.Write("trace_id");
            writer.Write(value.Context.TraceId);

            writer.Write("span_id");
            writer.Write(value.Context.SpanId);

            writer.Write("name");
            writer.Write(value.OperationName);

            writer.Write("resource");
            writer.Write(value.ResourceName);

            writer.Write("service");
            writer.Write(value.ServiceName);

            writer.Write("type");
            writer.Write(value.Type);

            writer.Write("start");
            writer.Write(value.StartTime.ToUnixTimeNanoseconds());

            writer.Write("duration");
            writer.Write(value.Duration.ToNanoseconds());

            if (value.Context.ParentId != null)
            {
                writer.Write("parent_id");
                writer.Write((ulong)value.Context.ParentId);
            }

            if (value.Error)
            {
                writer.Write("error");
                writer.Write(1);
            }

            if (value.Tags != null)
            {
                writer.Write("meta");
                writer.WriteMapHeader(value.Tags.Count);

                foreach (var pair in value.Tags)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }
            }

            if (value.Metrics != null)
            {
                writer.Write("metrics");
                writer.WriteMapHeader(value.Metrics.Count);

                foreach (var pair in value.Metrics)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }
            }
        }

        public Span Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
#endif
