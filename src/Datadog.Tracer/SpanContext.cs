using OpenTracing;
using System;
using System.Collections.Generic;

namespace Datadog.Tracer
{
    class SpanContext : ISpanContext
    {
        public UInt64 TraceId { get; }

        public UInt64? ParentId { get; }

        public UInt64 SpanId { get; }

        // TODO:bertrand do we really want ServiceName to be mutable?
        public string ServiceName { get; set; }

        public SpanContext()
        {
            Random r = new Random();
            TraceId = r.NextUInt64();
            SpanId = r.NextUInt64();
        }

        public SpanContext(SpanContext parent)
        {
            TraceId = parent.TraceId;
            ParentId = parent.SpanId;
            ServiceName = parent.ServiceName;
            // TODO:bertrand pool the random objects
            Random r = new Random();
            SpanId = r.NextUInt64();
        }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            throw new NotImplementedException();
        }
    }
}
