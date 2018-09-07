using System;
using Datadog.Trace.ExtensionMethods;
using MsgPack;
using MsgPack.Serialization;

namespace Datadog.Trace.Agent
{
    internal class SpanMessagePackSerializer : MessagePackSerializer<Span>
    {
        public SpanMessagePackSerializer(SerializationContext context)
            : base(context)
        {
        }

        protected override void PackToCore(Packer packer, Span value)
        {
            // First, pack array length (or map length).
            // It should be the number of members of the object to be serialized.
            var len = 8;
            if (value.Context.ParentId != null)
            {
                len += 1;
            }

            if (value.Error)
            {
                len += 1;
            }

            if (value.Tags != null)
            {
                len += 1;
            }

            packer.PackMapHeader(len);
            packer.PackString("trace_id");
            packer.Pack(value.Context.TraceId);
            packer.PackString("span_id");
            packer.Pack(value.Context.SpanId);
            packer.PackString("name");
            packer.PackString(value.OperationName);
            packer.PackString("resource");
            packer.PackString(value.ResourceName);
            packer.PackString("service");
            packer.PackString(value.ServiceName);
            packer.PackString("type");
            packer.PackString(value.Type);
            packer.PackString("start");
            packer.Pack(value.StartTime.ToUnixTimeNanoseconds());
            packer.PackString("duration");
            packer.Pack(value.Duration.ToNanoseconds());
            if (value.Context.ParentId != null)
            {
                packer.PackString("parent_id");
                packer.Pack(value.Context.ParentId);
            }

            if (value.Error)
            {
                packer.PackString("error");
                packer.Pack(1);
            }

            if (value.Tags != null)
            {
                packer.PackString("meta");
                packer.Pack(value.Tags);
            }
        }

        protected override Span UnpackFromCore(Unpacker unpacker)
        {
            throw new NotImplementedException();
        }
    }
}
