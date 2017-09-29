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

        public SpanContext()
        {
            Random r = new Random();
            TraceId = r.NextUInt64();
            SpanId = r.NextUInt64();
        }

        public SpanContext(UInt64 traceId, UInt64 parentId)
        {
            TraceId = traceId;
            ParentId = parentId;
            // TODO pool the random objects
            Random r = new Random();
            SpanId = r.NextUInt64();
        }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            throw new NotImplementedException();
        }
    }
}
