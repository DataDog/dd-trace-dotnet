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
    internal class TraceActivitiesContainerMessagePackFormatter : IMessagePackFormatter<TraceActivitiesContainer>
    {
        public static ulong ToUInt63(ActivityTraceId activityTraceId)
        {
            var traceIdBytes = new byte[16];
            activityTraceId.CopyTo(traceIdBytes);
            return BitConverter.ToUInt64(traceIdBytes, 8) & 0x7FFFFFFFFFFFFFFF;
        }

        public static ulong ToUInt63(ActivitySpanId activitySpanId)
        {
            var spanIdBytes = new byte[8];
            activitySpanId.CopyTo(spanIdBytes);
            return BitConverter.ToUInt64(spanIdBytes, 0) & 0x7FFFFFFFFFFFFFFF;
        }

        public int Serialize(ref byte[] bytes, int offset, TraceActivitiesContainer value, IFormatterResolver formatterResolver)
        {
            var startOffset = offset;
            var formatter = formatterResolver.GetFormatterWithVerify<Activity>();

            offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, value.Activities.Count);

            foreach (Activity activity in value.Activities)
            {
                // First, pack array length (or map length).
                // It should be the number of members of the object to be serialized.
                var len = 8;

                if (activity.Parent != null)
                {
                    len++;
                }

                bool? error = (bool?)activity.GetCustomProperty("Error");
                if (error != null && error.Value)
                {
                    len++;
                }

                if (activity.Tags != null)
                {
                    len++;
                }

                Dictionary<string, double> metrics = (Dictionary<string, double>)activity.GetCustomProperty("Metrics");
                if (metrics != null)
                {
                    len++;
                }

                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, len);

                offset += MessagePackBinary.WriteString(ref bytes, offset, "trace_id");
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, ToUInt63(activity.TraceId));

                offset += MessagePackBinary.WriteString(ref bytes, offset, "span_id");
                offset += MessagePackBinary.WriteUInt64(ref bytes, offset, ToUInt63(activity.SpanId));

                offset += MessagePackBinary.WriteString(ref bytes, offset, "name");
                offset += MessagePackBinary.WriteString(ref bytes, offset, activity.OperationName);

                offset += MessagePackBinary.WriteString(ref bytes, offset, "resource");
                offset += MessagePackBinary.WriteString(ref bytes, offset, activity.DisplayName);

                offset += MessagePackBinary.WriteString(ref bytes, offset, "service");
                offset += MessagePackBinary.WriteString(ref bytes, offset, (string)activity.GetCustomProperty("ServiceName"));

                offset += MessagePackBinary.WriteString(ref bytes, offset, "type");
                offset += MessagePackBinary.WriteString(ref bytes, offset, (string)activity.GetCustomProperty("Type"));

                offset += MessagePackBinary.WriteString(ref bytes, offset, "start");
                offset += MessagePackBinary.WriteInt64(ref bytes, offset, ((DateTimeOffset)activity.StartTimeUtc).ToUnixTimeNanoseconds());

                offset += MessagePackBinary.WriteString(ref bytes, offset, "duration");
                offset += MessagePackBinary.WriteInt64(ref bytes, offset, activity.Duration.ToNanoseconds());

                if (activity.Parent != null)
                {
                    offset += MessagePackBinary.WriteString(ref bytes, offset, "parent_id");
                    offset += MessagePackBinary.WriteUInt64(ref bytes, offset, ToUInt63(activity.ParentSpanId));
                }

                if (error != null && error.Value)
                {
                    offset += MessagePackBinary.WriteString(ref bytes, offset, "error");
                    offset += MessagePackBinary.WriteByte(ref bytes, offset, 1);
                }

                if (activity.Tags != null)
                {
                    offset += MessagePackBinary.WriteString(ref bytes, offset, "meta");
                    offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, activity.Tags.Count());

                    foreach (var pair in activity.Tags)
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
            }

            return offset - startOffset;
        }

        public TraceActivitiesContainer Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }
    }
}
