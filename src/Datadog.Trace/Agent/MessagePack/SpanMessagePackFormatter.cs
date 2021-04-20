using System;
using System.Collections.Generic;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class SpanMessagePackFormatter : IMessagePackFormatter<Span>
    {
        private readonly Func<Span, KeyValuePair<string, double?>>[] _metricsFactories;
        private readonly Func<Span, KeyValuePair<string, string>>[] _tagsFactories;

        public SpanMessagePackFormatter(IKeepRateCalculator keepRateCalculator)
        {
            _metricsFactories = new Func<Span, KeyValuePair<string, double?>>[]
            {
                span => new(Metrics.TopLevelSpan, span.IsTopLevel ? 1.0 : null),
                span => new(Metrics.TracesKeepRate, span.IsTopLevel ? keepRateCalculator.GetKeepRate() : null)
            };

            _tagsFactories = ArrayHelper.Empty<Func<Span, KeyValuePair<string, string>>>();
        }

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

            len += 2; // Tags and metrics

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

            offset += value.Tags.SerializeTo(ref bytes, offset, value, _tagsFactories, _metricsFactories);

            return offset - originalOffset;
        }

        public Span Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            throw new NotImplementedException();
        }
    }
}
