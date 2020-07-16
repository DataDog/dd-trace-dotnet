using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;
using MessagePack;
using MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class ActivityMessagePackFormatter : IMessagePackFormatter<Activity>
    {
        public static ulong ToUInt64(ActivityTraceId activityTraceId)
        {
            var traceIdBytes = new byte[16];
            activityTraceId.CopyTo(traceIdBytes);
            return BitConverter.ToUInt64(traceIdBytes, 8);
        }

        public static ulong ToUInt64(ActivitySpanId activitySpanId)
        {
            var spanIdBytes = new byte[8];
            activitySpanId.CopyTo(spanIdBytes);
            return BitConverter.ToUInt64(spanIdBytes, 0);
        }

        public int Serialize(ref byte[] bytes, int offset, Activity value, IFormatterResolver formatterResolver)
        {
            // First, pack array length (or map length).
            // It should be the number of members of the object to be serialized.
            var len = 8;

            if (value.Parent != null)
            {
                len++;
            }

            bool error = (bool)value.GetCustomProperty("Error");
            if (error)
            {
                len++;
            }

            if (value.Tags != null)
            {
                len++;
            }

            Dictionary<string, double> metrics = (Dictionary<string, double>)value.GetCustomProperty("Metrics");
            if (metrics != null)
            {
                len++;
            }

            int originalOffset = offset;

            offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "trace_id");
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, ToUInt64(value.TraceId));

            offset += MessagePackBinary.WriteString(ref bytes, offset, "span_id");
            offset += MessagePackBinary.WriteUInt64(ref bytes, offset, ToUInt64(value.SpanId));

            offset += MessagePackBinary.WriteString(ref bytes, offset, "name");
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.OperationName);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "resource");
            offset += MessagePackBinary.WriteString(ref bytes, offset, value.DisplayName);

            offset += MessagePackBinary.WriteString(ref bytes, offset, "service");
            offset += MessagePackBinary.WriteString(ref bytes, offset, (string)value.GetCustomProperty("ServiceName"));

            offset += MessagePackBinary.WriteString(ref bytes, offset, "type");
            offset += MessagePackBinary.WriteString(ref bytes, offset, (string)value.GetCustomProperty("Type"));

            offset += MessagePackBinary.WriteString(ref bytes, offset, "start");
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, ((DateTimeOffset)value.StartTimeUtc).ToUnixTimeNanoseconds());

            offset += MessagePackBinary.WriteString(ref bytes, offset, "duration");
            offset += MessagePackBinary.WriteInt64(ref bytes, offset, value.Duration.ToNanoseconds());

            if (value.Parent != null)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, "parent_id");
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, ToUInt64(value.ParentSpanId));
            }

            if (error)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, "error");
                offset += MessagePackBinary.WriteByte(ref bytes, offset, 1);
            }

            if (value.Tags != null)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, "meta");
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, value.Tags.Count());

                foreach (var pair in value.Tags)
                {
                    offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Key);
                    offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Value);
                }
            }

            if (metrics != null)
            {
                offset += MessagePackBinary.WriteString(ref bytes, offset, "metrics");
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, metrics.Count);

                foreach (var pair in metrics)
                {
                    offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Key);
                    offset += MessagePackBinary.WriteDouble(ref bytes, offset, pair.Value);
                }
            }

            return offset - originalOffset;
        }

        public Activity Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }
    }
}
