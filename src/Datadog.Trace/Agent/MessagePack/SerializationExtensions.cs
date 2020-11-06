using System.Collections.Generic;
using Datadog.Trace.Agent.NamedPipes;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Agent.MessagePack
{
    internal static class SerializationExtensions
    {
        public static int SerializeTo(this TraceRequest traceRequest, ref byte[] bytes, int offset)
        {
            int originalOffset = offset;
            offset += traceRequest.Headers.SerializeTo(ref bytes, offset, "headers");
            offset += traceRequest.Traces.SerializeTo(ref bytes, offset);
            return offset - originalOffset;
        }

        public static int SerializeTo(this Span span, ref byte[] bytes, int offset)
        {
            // First, pack array length(or map length).
            // It should be the number of members of the object to be serialized.
            var len = 8;

            if (span.Context.ParentId != null)
            {
                len++;
            }

            if (span.Error)
            {
                len++;
            }

            len += 2; // Tags and metrics

            int originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "trace_id");
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, span.Context.TraceId);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "span_id");
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, span.Context.SpanId);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "name");
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.OperationName);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "resource");
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.ResourceName);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "service");
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.ServiceName);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "type");
            offset += MessagePackBinary.WriteString(ref bytes, offset, span.Type);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "start");
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, span.StartTime.ToUnixTimeNanoseconds());

            offset += MessagePackBinary.WriteString(ref bytes, offset, "duration");
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, span.Duration.ToNanoseconds());

            if (span.Context.ParentId != null)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, "parent_id");
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, (ulong)span.Context.ParentId);
            }

            if (span.Error)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, "error");
                offset += MessagePackBinary.WriteByte(ref bytes, offset, 1);
            }

            offset += span.Tags.SerializeTo(ref bytes, offset);

            return offset - originalOffset;
        }

        public static int SerializeTo(this List<KeyValuePair<string, string>> map, ref byte[] bytes, int offset, string name)
        {
            int originalOffset = offset;

            offset += MessagePackBinary.WriteString(ref bytes, offset, name);

            int count = 0;

            if (map != null)
            {
                lock (map)
                {
                    count += map.Count;

                    offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, count);

                    foreach (var pair in map)
                    {
                        offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Key);
                        offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Value);
                    }
                }
            }
            else
            {
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, count);
            }

            return offset - originalOffset;
        }

        public static int SerializeTo(this Span[][] traces, ref byte[] bytes, int offset)
        {
            int originalOffset = offset;

            offset += MessagePackBinary.WriteString(ref bytes, offset, "traces");

            int count = 0;

            if (traces != null)
            {
                lock (traces)
                {
                    count += traces.Length;

                    offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, count);

                    foreach (var trace in traces)
                    {
                        offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, count);

                        foreach (var span in trace)
                        {
                            offset += span.SerializeTo(ref bytes, offset);
                        }
                    }
                }
            }
            else
            {
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, count);
            }

            return offset - originalOffset;
        }
    }
}
