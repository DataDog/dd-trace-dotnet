using System.Collections.Generic;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal class TraceRequest
    {
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        public Span[][] Traces { get; set; }

        internal int WriteHeaders(ref byte[] bytes, int offset)
        {
            int originalOffset = offset;

            offset += MessagePackBinary.WriteString(ref bytes, offset, "headers");

            int count = 0;

            if (Headers != null)
            {
                lock (Headers)
                {
                    count += Headers.Count;

                    offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, count);

                    foreach (var pair in Headers)
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

        internal int WriteTraces(ref byte[] bytes, int offset)
        {
            int originalOffset = offset;

            offset += MessagePackBinary.WriteString(ref bytes, offset, "traces");

            int count = 0;

            if (Traces != null)
            {
                lock (Traces)
                {
                    count += Traces.Length;

                    offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, count);

                    foreach (var trace in Traces)
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
